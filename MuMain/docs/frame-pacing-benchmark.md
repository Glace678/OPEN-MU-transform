# Real-time frame pacing benchmark

Last run: 2026-09-04, Windows x64 Release, no game window.

## Purpose

`FrameClockTests.cpp` proves deadline arithmetic with a fake clock. The
standalone benchmark under `tools/FrameRateAudit` additionally measures the
same production `FrameClock` and `FramePacer` against `steady_clock`, real OS
waits, and real elapsed time. It covers fixed 30, 60, 90, 120, 144, and 180
FPS plus a fractional 143.98 Hz `FollowDisplay` policy.

The benchmark reports average FPS error, interval percentiles, 1% Low,
intervals longer than 1.5 times the target interval, total drift, and process
CPU as a percentage of one logical core. Its exit code is non-zero when the
FollowDisplay policy is wrong or average FPS differs from its target by more
than the configured tolerance.

## Failure found before the production fix

A three-second-per-mode run using `std::this_thread::sleep_until` plus a
0.75 ms final yield exposed scheduler batching near 16.3 ms even though
`timeBeginPeriod(1)` returned success:

| Mode | Target | Actual | Error | Missed intervals |
|---|---:|---:|---:|---:|
| 120 | 120 | 119.480 | 0.433% | 193 |
| 144 | 144 | 129.071 | 10.368% | 214 |
| 180 | 180 | 107.939 | 40.034% | 320 |
| FollowDisplay | 143.98 | 128.132 | 11.007% | 216 |

This demonstrated that correct absolute-deadline arithmetic alone was not
enough for a reliable high-refresh limiter on Windows.

## Production wait strategy

`Core::Time::FramePacer` now uses a Windows high-resolution waitable timer,
waits in slices no longer than 10 ms so input and network queues remain
responsive, and busy-spins only for the final 0.5 ms before a frame deadline.
Older Windows builds fall back to an ordinary waitable timer with a 2 ms spin
reserve; the existing `timeBeginPeriod(1)` request remains active for that
fallback. `FrameClock` still owns the cross-frame absolute phase and
resynchronizes after a stall, so the waiter cannot create a burst of old
catch-up frames.

The game refreshes `FrameClock` immediately before waiting. This prevents
input or network processing earlier in the loop from making the remaining
time stale at 120-180 FPS.

## Post-fix repeated result

Three independent two-second-per-mode runs, each with a 0.1 second warm-up,
all passed at a +/-2% tolerance. Every mode had zero intervals longer than
1.5 times its target interval. The largest observed total drift was 0.023 ms.

The acceptance-length run below was then performed with no compiler or other
test process running. It used a five-second warm-up and a 30-second measurement
for each mode. All average rates matched their targets at the displayed
precision, including fixed 180 FPS and fractional FollowDisplay. A total of six
isolated scheduler stalls occurred across the 120 and 144 FPS samples; the
180 FPS and fractional-display samples had none. Reporting these outliers is
intentional: average FPS alone is not a smoothness measurement.

| Mode | Actual FPS | Maximum interval | 1% Low | One-core CPU | Missed intervals | Drift |
|---|---:|---:|---:|---:|---:|---:|
| 30 | 30.000 | 33.465 ms | 29.930 | 2.917% | 0 | 0.002 ms |
| 60 | 60.000 | 17.174 ms | 59.122 | 3.750% | 0 | 0.001 ms |
| 90 | 90.000 | 11.971 ms | 87.729 | 1.615% | 0 | 0.000 ms |
| 120 | 120.000 | 14.767 ms | 109.029 | 0.365% | 2 | 0.001 ms |
| 144 | 144.000 | 13.179 ms | 125.218 | 0.000% | 4 | 0.005 ms |
| 180 | 180.000 | 7.897 ms | 162.551 | 7.031% | 0 | 0.001 ms |
| FollowDisplay 143.98 | 143.980 | 8.073 ms | 135.557 | 2.552% | 0 | 0.002 ms |

Windows accounts process CPU in coarse quanta, so short per-mode values are
quantized. A pure busy-spin comparison consumed 81.516-100% of one core. The
hybrid waiter therefore keeps the same average timing precision without
reserving a whole core.

## Reproduction

Quick check:

```powershell
tools/FrameRateAudit/Test-FramePacing.ps1 `
  -DurationSeconds 2 `
  -WarmupSeconds 0.1 `
  -TolerancePercent 2 `
  -DisplayHertz 143.98 `
  -WaitMode sleep
```

Long acceptance run (about four minutes for all seven modes):

```powershell
tools/FrameRateAudit/Test-FramePacing.ps1 `
  -DurationSeconds 30 `
  -WarmupSeconds 5 `
  -TolerancePercent 2 `
  -DisplayHertz 143.98 `
  -WaitMode sleep
```

Use `-WaitMode spin` only as a short reference that isolates clock arithmetic
from OS waits; it intentionally consumes most of one CPU core.

## Scope limit

This is a no-window limiter test. It validates real elapsed-time pacing and
the fractional FollowDisplay policy, but it does not measure OpenGL swap,
driver-forced VSync, VRR, GPU saturation, cross-monitor refresh detection, or
gameplay workload. When VSync is engaged at the display refresh rate,
`ResolveFramePolicy` correctly marks pacing as external and the actual swap
cadence must be validated in the running client on the target display.
