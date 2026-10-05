#!/usr/bin/env bash
# One-click refactor verification loop: configure (only when requested) ->
# build Main + tests (Debug) -> run ctest.
#
# Usage:
#   scripts/refactor-smoke.sh           # build + ctest
#   scripts/refactor-smoke.sh --configure   # reconfigure first
#
# The in-game 5-step smoke (login -> enter map -> walk -> kill mob -> open
# inventory against the local OpenMU server) is driven manually/scripted per
# phase; see docs/refactoring-plan.md.
set -euo pipefail

cd "$(dirname "$0")/.."

if [ "${1:-}" = "--configure" ]; then
  cmd //c ".\\scripts\\refactor-configure_ascii.bat"
fi

cmd //c ".\\scripts\\refactor-build_ascii.bat"
cmd //c ".\\scripts\\refactor-test_ascii.bat"

echo "[smoke] build + ctest OK"
