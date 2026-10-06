# Vendored upstream code

This directory is a vendored snapshot of a third-party upstream project. It is not
original project code.

- Upstream: https://github.com/libsdl-org/SDL_mixer
- License: Zlib (see LICENSE.txt in this directory)
- Usage in this project: 3.x development branch, built via add_subdirectory. The
  external/ directory contains bundled codec libraries.

Do not edit these files directly to add features. If a local patch is unavoidable,
keep it minimal, record it against a specific upstream version/SHA, and prefer
upstreaming the fix or moving the abstraction into project code.
