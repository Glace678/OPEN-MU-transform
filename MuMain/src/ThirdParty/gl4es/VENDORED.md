# Vendored upstream code

This directory is a vendored snapshot of a third-party upstream project. It is not
original project code.

- Upstream: https://github.com/ptitSeb/gl4es
- License: MIT (see LICENSE in this directory)
- Usage in this project: Desktop GL to OpenGL ES translation. It is built only for
  Android/OpenHarmony when MU_GL4ES_ROOT points at this tree; desktop builds use
  native OpenGL and do not compile it.

Do not edit these files directly to add features. If a local patch is unavoidable,
keep it minimal, record it against a specific upstream version/SHA, and prefer
upstreaming the fix or moving the abstraction into project code.
