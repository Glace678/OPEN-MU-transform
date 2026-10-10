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

2. `src/video/stb_image.h` — NV12 JPEG chroma subsampling guard (L5 r3-71 finding 98A-01).
   - `load_jpeg_image` nv12 branch now rejects a JPEG whose chroma horizontal/vertical subsample
     ratio exceeds 2 (4:1:1 / 4x vertical) via `stbi__errpuc("nv12subsample", ...)`, and
     `output_jpeg_nv12` defensively returns NULL if any of the u/v horizontal/vertical ratios is
     outside {1,2}. Previously such a JPEG made the UV source stride go negative and read before
     the chroma plane allocation. Offline local patch; re-verify against upstream on next sync.

3. `src/video/x11/SDL_x11events.c` — X11 clipboard/drag target bounds (L5 r3-71 finding 98B-03).
   - SelectionNotify TARGETS/SDL_FORMATS path now requires `XGetWindowProperty == Success`,
     non-NULL data and `format == 32` before treating the buffer as an Atom array; clamps the
     count to the requested 200; uses `size_t` for the allocation size; skips (never derefs)
     NULL atom names. The XdndTypeList drop path only calls `X11_PickTarget` when
     `p.format == 32 && p.data != NULL`. Offline local patch; re-verify against upstream.
   - NOTE: 98B-03 is X11-only; it does not trigger on Windows but the source is hardened here
     for the Linux/X11 build. EXTERNAL BLOCKER: needs network access to diff upstream SDL.
