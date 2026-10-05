#!/usr/bin/env python3
"""Architectural dependency guard (P2 baseline-lock, becomes strict at P9).

Rule (foundational): the Core layer must not #include any higher layer.
The legacy tree still contains known reverse dependencies (aggregate shims,
Input/Log plumbing); those are recorded in core_includes_baseline.txt. This
guard fails only on NEW reverse dependencies outside the baseline, so the
current architecture is locked while cleanup shrinks the baseline over time.
Run via CTest (arch_core_includes)."""
import re
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
ROOT = HERE.parent.parent
SRC = ROOT / "src" / "source"
HIGHER = {"App", "UI", "Render", "Network", "World", "GameLogic", "Data",
          "Scenes", "Camera", "Audio", "Character", "GameShop", "Engine"}
INCLUDE_RE = re.compile(r'\s*#\s*include\s+"([^"]+)"')


def scan():
    found = set()
    for p in (SRC / "Core").rglob("*"):
        if p.suffix.lower() not in (".h", ".cpp", ".hpp"):
            continue
        for i, line in enumerate(p.read_text(encoding="utf-8",
                                             errors="replace").splitlines(), 1):
            m = INCLUDE_RE.match(line)
            if not m:
                continue
            inc = m.group(1).replace("\\", "/")
            if inc.split("/")[0] in HIGHER:
                found.add(f"{p.relative_to(ROOT).as_posix()}:{i}:{inc}")
    return found


def main():
    baseline_file = HERE / "core_includes_baseline.txt"
    baseline = {l for l in baseline_file.read_text(
        encoding="utf-8").splitlines() if l.strip()}
    found = scan()
    new = sorted(found - baseline)
    removed = sorted(baseline - found)
    print(f"Core->higher includes: current={len(found)} "
          f"baseline={len(baseline)} new={len(new)} resolved={len(removed)}")
    for n in new:
        print("NEW VIOLATION:", n)
    for r in removed:
        print("RESOLVED (shrink baseline):", r)
    if new:
        print("\nFAIL: new Core->higher include(s). Refactor the dependency or, "
              "if intentional, update the baseline after review.")
        return 1
    print("PASS: no new Core->higher includes.")
    return 0


if __name__ == "__main__":
    sys.exit(main())