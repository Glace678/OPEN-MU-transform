# Frame-rate audit

Last static pass: 2026-09-05.

## Confirmed architecture

- `FrameClock` advances absolute deadlines, keeps phase for ordinary late
  frames, and resynchronizes after a full missed interval instead of rendering
  catch-up bursts.
- The render loop derives `FPS_ANIMATION_FACTOR` from monotonic elapsed time at
  the original 25 FPS reference rate. Character movement, model animation,
  attack timing, ability durations, particle/effect/joint lifetimes, and skill
  cooldowns already consume this factor or elapsed milliseconds.
- Display refresh is retained as a fractional value. Follow-display uses the
  mode of the display containing the window; `SDL_EVENT_WINDOW_DISPLAY_CHANGED`
  reapplies policy. The native-maximum option now enumerates modes at the
  current resolution and selects the highest refresh rate.
- Foreground loss reapplies the configured background cap (30 FPS by default),
  while network and wall-clock systems continue to update. Focus gain restores
  the foreground policy.
- When VSync is active and the requested rate is at or above display refresh,
  VSync owns pacing and the effective policy reports the display refresh rate.
  Lower fixed limits continue to use the absolute-deadline limiter.

## Remediated in this pass

- Eagle ground/flying transitions no longer multiply already-scaled ranges
  twice; vertical motion and bank rotation use the elapsed-time factor.
- Meteor spawn offsets keep a constant world-space radius at high FPS.
- Pet update checks the object pointer before reading its live state.
- DisplayMaximum persists as a mode rather than a one-time fixed number.
  Windowed detection uses desktop resolution; exclusive fullscreen requests
  the highest compatible refresh at the target resolution.
- Duplicate frame-scaling and server-result haptic test files were consolidated
  into FrameClockTests and HapticsTests without dropping their assertions.
  Scaling now covers 25, 30, 60, 90, 120, 144, 180, 240, and 360 FPS.

- Generic object alpha transitions now scale linear changes and exponential
  smoothing correctly.
- Login logo fade and Chaos Castle object fade now use frame-time scaling.
- Falling-monster and pushed-ball movement now scales acceleration, angular
  motion, displacement, and exponential damping.
- Integer action-object state machines now advance through a fractional
  accumulator at exactly 25 reference ticks per second. This preserves exact
  integer trigger points without accelerating them at high render rates.
- Added focused tests across 30, 60, 90, 120, 144, and 180 FPS for 30-second
  deadline drift and one-second linear, blend, and damping equivalence.

## Residual review queue

Run `tools/FrameRateAudit/scan-frame-dependent-code.ps1` after animation or
effects changes. Its CSV is evidence for review, not proof that each match is a
bug: many random conditions execute once per action, and many numeric increments
are setup operations rather than per-frame updates.

The 2026-09-01 pass still found these manually reviewable groups after the
safe linear fixes:

- 10 raw state-step candidates after contextual filtering. Four action-object
  velocity lines are intentionally protected by the new 25 Hz state-machine
  gate, two timer increments are protected by FPS-aware event checks, and the
  chat alpha line is a user-invoked setting step rather than a frame update.
- 77 raw random conditions. Core particle creation mostly uses the existing
  FPS-aware wrappers, but map-specific conditions need action-by-action review.
- Two integer blur lifetime decrements and one siege minimap command lifetime
  decrement. These need reference-tick accumulators plus visual tests; changing
  only the integer decrement would alter trail sample density.

## Not yet proven by static tests

Set `MU_FRAME_AUDIT=1` before launching the client to write actual rolling
render FPS, 1% low, scene, requested/effective cap and active display refresh to
the normal error-report log every five seconds. No account data is included.
This records rendering, not physical scanout or GPU presentation timestamps.

- The options flow now provides a 10-second confirmation and automatic rollback
  after a resolution or fullscreen-mode change. It still requires runtime
  coverage across fullscreen transitions and multiple displays.
- VRR state, driver-forced VSync, and GPU/CPU saturation cannot be identified
  reliably from this OpenGL/SDL code alone. Requested/effective/actual FPS and
  1% low are exposed; after enough samples, sustained under-target output is
  labeled `Performance/external limit`. Separating GPU/CPU saturation from a
  driver override still requires runtime measurement and driver inspection.
- Non-integral fixed-rate-to-refresh combinations can exhibit swap cadence
  variation with VSync enabled. The options diagnostics now identify this as
  `VSync uneven cadence`; runtime capture must still verify the actual cadence.
- Map-specific effects, dense combat, fullscreen transitions, cross-monitor
  movement, and background recovery require real-client play traces before the
  30-180 FPS entries can be called fully accepted.
