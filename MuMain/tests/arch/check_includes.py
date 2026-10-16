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

# The guard compares dependency *edges* ("file:included-header") only. Line
# numbers are intentionally excluded so that reformatting (which shifts lines)
# never reports a spurious violation. Baseline entries may still carry a legacy
# ":<line>:" segment; it is stripped on load.
EDGE_RE = re.compile(r'^(.*?):(?:\d+:)?(.+)$')


def edge(entry):
    m = EDGE_RE.match(entry)
    return f"{m.group(1)}:{m.group(2)}" if m else entry


def scan():
    """Return (edges, line_of) where edges is the dependency-edge set and
    line_of maps each edge to its current line number for reporting."""
    found = set()
    line_of = {}
    for p in (SRC / "Core").rglob("*"):
        if p.suffix.lower() not in (".h", ".cpp", ".hpp"):
            continue
        rel = p.relative_to(ROOT).as_posix()
        for i, line in enumerate(p.read_text(encoding="utf-8",
                                             errors="replace").splitlines(), 1):
            m = INCLUDE_RE.match(line)
            if not m:
                continue
            inc = m.group(1).replace("\\", "/")
            if inc.split("/")[0] in HIGHER:
                e = f"{rel}:{inc}"
                found.add(e)
                line_of[e] = i
    return found, line_of


def main():
    baseline_file = HERE / "core_includes_baseline.txt"
    baseline = {edge(l) for l in baseline_file.read_text(
        encoding="utf-8").splitlines() if l.strip()}
    found, line_of = scan()
    new = sorted(found - baseline)
    removed = sorted(baseline - found)
    print(f"Core->higher includes: current={len(found)} "
          f"baseline={len(baseline)} new={len(new)} resolved={len(removed)}")
    for n in new:
        print("NEW VIOLATION:", f"{n} (line {line_of.get(n, '?')})")
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