# Vendored upstream code

This directory is a vendored snapshot of a third-party upstream project. It is not
original project code.

- Upstream: https://github.com/ocornut/imgui
- License: MIT (see LICENSE.txt in this directory)
- Usage in this project: Dear ImGui, built from the sources listed in
  src/CMakeLists.txt (core plus SDL3/OpenGL2 backends).

Do not edit these files directly to add features. If a local patch is unavoidable,
keep it minimal, record it against a specific upstream version/SHA, and prefer
upstreaming the fix or moving the abstraction into project code.
