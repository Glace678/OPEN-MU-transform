# Gamepad, haptics, and frame-rate controls

The client routes controller input through a semantic action mapper. Legacy
mouse-driven windows remain reachable through the shared logical pointer, and
text fields use the controller keyboard. The complete static workflow inventory
is generated in `docs/controller-coverage.md`; runtime rows intentionally stay
pending until replayed against a running client and server.

NewUI and legacy buttons, radio controls, and text fields register their
visible rectangles with the shared focus navigator every frame. D-pad or
left-stick input moves to the nearest control in that direction and snaps the
logical pointer to its center; complex grids and unregistered controls retain
the right-stick pointer fallback. Hidden, locked, and disabled controls are not
focus targets.

## Player configuration

The options window exposes the same values that are stored in `config.ini`:

- `[Input]` enables the gamepad, controls stick and trigger dead zones, pointer
  speed and Y inversion, and stores the 16 action bindings. Assigning an already
  used control swaps the two bindings, so every required action remains bound.
- `[Haptics]` enables all feedback, controls the 0-100 percent master intensity,
  and independently enables combat, UI, and transaction feedback. Short and
  long test actions are available in the options window.
- `[Render]` stores `DisplayMaximum` (new default), `FollowDisplay`, or `Fixed`, the exact fixed rate in milli-Hz,
  the background limit, and VSync. Supported fixed choices are 30, 60, 90, 120,
  144, and 180 FPS; the native/follow-display choices keep fractional refresh
  rates such as 59.94 Hz internally.

New configurations default to VSync off so an older active desktop refresh
does not silently cap the requested maximum. Existing explicit preferences are
preserved. DisplayMaximum is recomputed when the window changes monitor; in a
window it uses desktop resolution, not window dimensions. Exclusive fullscreen
selects the highest compatible refresh for the requested resolution. Windowed
rendering cannot make a 60 Hz desktop physically display 240 unique frames;
the active desktop mode remains an OS setting.

The diagnostics distinguish application limiting, display/VSync limiting,
non-integer VSync cadence, missing refresh detection, background limiting, and
sustained performance or external-driver limiting. SDL/OpenGL cannot reliably
separate GPU/CPU saturation from a driver-forced synchronization override, so
that case is deliberately reported as a combined reason instead of guessed.

Changing resolution or fullscreen mode starts a ten-second confirmation. Press
Confirm/Enter to keep it or Cancel/Escape to revert it. A timeout and closing
the options window also revert the unconfirmed mode.

When the window loses focus, gameplay input is released, rumble is stopped, and
the background frame cap is applied while network and time processing continue.
The controller must return to neutral before input resumes after focus returns.

## Default controller actions

| Control | World action | UI action |
| --- | --- | --- |
| Left stick | Move | Move directional focus |
| Right stick | Aim/select world target | Move logical pointer |
| South / A | Interact or pick up | Confirm, press, hold, release, or drop |
| East / B | Cancel | Back or cancel |
| West / X | Basic attack | Secondary action or right click |
| North / Y | Shortcut keyboard | Details or double click |
| Right trigger | Current skill | Hold Shift |
| Left trigger | Lock target | Hold Ctrl |
| LB / RB | Previous/next skill page | Previous/next page |
| D-pad | Quick items | Move directional focus |
| Back / View | Map | Next text field in the controller keyboard |
| Start / Menu | System menu | Submit controller keyboard text |
| L3 | Auto move / MU Helper | Remappable action |
| R3 | Next target | Toggle ABC/pinyin in eligible text fields |

Controller labels follow the connected SDL gamepad family. The release build
accepts physical gamepads only.

View + Start also opens the shortcut keyboard outside text entry. It exposes
letters, function keys, digits, navigation, scroll, and numeric keypad keys.
Move selection with D-pad/left stick, activate with A, and close/reset with B.
Ctrl/Shift/Alt toggle on for combinations; active modifiers remain visible
after closing. Focus loss, disconnect, and text-entry handoff clear them.
Normal shortcut keys are held for 120 ms so frame-based legacy readers see
them even with a low render cap. This fallback improves reachability; it does
not turn the static workflow inventory into completed runtime acceptance.

## Haptic behavior

Gameplay code publishes semantic events. `HapticScheduler` owns timing,
category filtering, rate limiting, priority, and mixing. Transaction results,
item moves, combat hits, damage, and failures are published from response or
resolved gameplay paths rather than from the initiating button press.

Every mapped button/trigger activation and stick movement start also requests
an 18 ms low-priority input acknowledgement (35 ms minimum spacing). It is not
a success result and cannot override combat or transaction feedback. Disabled
feedback, zero intensity, absent/unsupported motors, or lost focus correctly
produce no rumble. Holding a stick does not continuously vibrate.

At any instant, only pulses at the highest active priority participate; their
strongest low- and high-frequency motor values are mixed. A lower-priority UI
event cannot interrupt combat damage or a transaction result. Timings use the
monotonic clock, each pulse is limited to 500 ms, and a complete pattern is
limited to 1000 ms. Disconnect, focus loss, device switching, category/master
disable, and shutdown stop the motors and clear queued feedback.

## Virtual acceptance gamepad

The virtual input channel is compiled only into the Debug configuration when
CMake is configured with `-DENABLE_VIRTUAL_GAMEPAD_TESTS=ON`. It is absent from
Release, RelWithDebInfo, and MinSizeRel builds even when the same multi-config
build tree has the acceptance-test option enabled.
Set `MU_VIRTUAL_GAMEPAD_STATE` to an absolute INI path before starting the test
client. The client polls the following format:

```ini
[Gamepad]
Connected=1
LeftX=0
LeftY=0
RightX=0
RightY=0
LeftTrigger=-32768
RightTrigger=-32768
South=0
East=0
West=0
North=0
Back=0
Start=0
LeftStick=0
RightStick=0
LeftShoulder=0
RightShoulder=0
DpadUp=0
DpadDown=0
DpadLeft=0
DpadRight=0
```

Stick axes use `-32768..32767`; trigger axes use the same SDL range and are
normalized to `0..1`. Use a press frame and a release frame for deterministic
button edges. Setting `Connected=0` simulates unplugging the controller.

The same file receives the last requested motor output:

```ini
[Rumble]
Low=16384
High=32768
DurationMs=80
Sequence=12
```

`Low` and `High` use SDL's `0..65535` motor range. `Sequence` increments for
each output change, including a final zero when feedback stops, and lets a test
distinguish a repeated pulse from stale state.

Use `tools/ControllerAcceptance/Invoke-VirtualGamepad.ps1` to initialize the
file, set axes, tap buttons, simulate disconnects, and capture every rumble
sequence change observed during a button action. The script requires an
absolute state-file path and is useful only with the Debug acceptance build.

## Verification

From a Visual Studio developer PowerShell or command prompt:

```powershell
cmake --build out/build/x86-debug --target core_input_timing_tests
ctest --test-dir out/build/x86-debug -R core_input_timing_tests --output-on-failure
& tools/FrameRateAudit/scan-frame-dependent-code.ps1
& tools/ControllerCoverage/Test-ControllerCoverage.ps1
```

Use `tools/ControllerCoverage/Test-ControllerCoverage.ps1 -UpdateGenerated`
only after intentionally changing a scene, window, input dependency, or send
entry point. Static checks are a gate for inventory drift, not evidence that a
workflow has passed runtime replay or real-motor validation.
