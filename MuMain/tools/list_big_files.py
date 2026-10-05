#!/usr/bin/env python3
"""List source files that exceed size thresholds (refactor dashboard).

Behavior: scans .cpp/.h/.hpp/.inl files under the given roots (default:
src/source and src/MuEditor), counts physical lines, and prints files at or
above the threshold sorted by size. Used to track the P0-P9 giant-file
shrink-down; regenerate after every phase.

Usage:
  python tools/list_big_files.py                 # table, threshold 2000
  python tools/list_big_files.py --threshold 500
  python tools/list_big_files.py --json
"""

from __future__ import annotations

import argparse
import json
from pathlib import Path

DEFAULT_ROOTS = ["src/source", "src/MuEditor"]
EXTENSIONS = {".cpp", ".h", ".hpp", ".inl"}


def count_lines(path: Path) -> int:
    # Binary/odd-encoded legacy files never appear in scope; utf-8 with
    # replacement is enough for a line-counting dashboard.
    with path.open("r", encoding="utf-8", errors="replace") as handle:
        return sum(1 for _ in handle)


def collect(root: Path) -> list[tuple[Path, int]]:
    results: list[tuple[Path, int]] = []
    for path in root.rglob("*"):
        if path.suffix.lower() in EXTENSIONS and path.is_file():
            results.append((path, count_lines(path)))
    return results


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--threshold", type=int, default=2000)
    parser.add_argument("--json", action="store_true", dest="as_json")
    parser.add_argument("roots", nargs="*", default=DEFAULT_ROOTS)
    args = parser.parse_args()

    entries: list[tuple[Path, int]] = []
    for raw_root in args.roots:
        root = Path(raw_root)
        if root.exists():
            entries.extend(collect(root))

    entries = [(path, lines) for path, lines in entries if lines >= args.threshold]
    entries.sort(key=lambda item: item[1], reverse=True)

    if args.as_json:
        print(json.dumps([
            {"file": str(path).replace("\\", "/"), "lines": lines}
            for path, lines in entries
        ], indent=2))
        return 0

    header = f"{'LINES':>7}  FILE (threshold >= {args.threshold})"
    print(header)
    print("-" * len(header))
    for path, lines in entries:
        print(f"{lines:>7}  {path.as_posix()}")
    print("-" * len(header))
    print(f"{len(entries)} file(s)")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
