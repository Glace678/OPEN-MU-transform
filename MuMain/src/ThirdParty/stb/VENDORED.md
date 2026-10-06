# Vendored upstream code

This directory is a vendored snapshot of a third-party upstream project. It is not
original project code.

- Upstream: https://github.com/nothings/stb
- License: MIT or Public Domain (see LICENSE in this directory)
- Usage in this project: stb_truetype single-header, used as an include path only.

Do not edit these files directly to add features. If a local patch is unavoidable,
keep it minimal, record it against a specific upstream version/SHA, and prefer
upstreaming the fix or moving the abstraction into project code.
