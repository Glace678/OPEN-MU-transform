# Vendored upstream code

This directory is a vendored snapshot of a third-party upstream project. It is not
original project code.

- Upstream: https://github.com/libsdl-org/SDL
- License: Zlib (see LICENSE.txt in this directory)
- Usage in this project: 3.x development branch, built via add_subdirectory.

Do not edit these files directly to add features. If a local patch is unavoidable,
keep it minimal, record it against a specific upstream version/SHA, and prefer
upstreaming the fix or moving the abstraction into project code.

## Local patches

Snapshot baseline: SDL release-3.4.8 (`android-project` Java glue included).

1. `android-project/app/src/main/java/org/libsdl/app/SDLSurface.java`
   (`getNormalizedX` / `getNormalizedY`): normalize touch coordinates against
   `getWidth()` / `getHeight()` (the current View size) instead of the `mWidth`
   / `mHeight` fields, which are only updated when the Surface changes size and
   can be stale (e.g. initial/inset/rotation states), producing wrong touch
   scaling. Verified that upstream SDL release-3.4.8/3.4.10/3.4.18 and `main`
   still use `mWidth`/`mHeight`, so this is a genuine local modification, not a
   version drift. Active on Android/OpenHarmony touch input. Candidate to
   upstream or re-verify on each SDL upgrade.
