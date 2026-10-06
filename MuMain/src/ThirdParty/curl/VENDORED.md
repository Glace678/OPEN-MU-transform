# Vendored upstream code

This directory is a vendored snapshot of a third-party upstream project. It is not
original project code.

- Upstream: https://github.com/curl/curl
- License: curl license (see COPYING in this directory)
- Usage in this project: Vendored source snapshot. Desktop builds use
  find_package(CURL); Android/OpenHarmony use the MU_*_CURL_INCLUDE_DIR /
  MU_*_CURL_LIBRARY settings. This tree is not compiled on desktop.

Do not edit these files directly to add features. If a local patch is unavoidable,
keep it minimal, record it against a specific upstream version/SHA, and prefer
upstreaming the fix or moving the abstraction into project code.
