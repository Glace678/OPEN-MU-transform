#!/usr/bin/env python3
"""Scan TUs and report quoted #includes that fail to resolve against the
actual -I paths in compile_commands.json (lost local-untracked headers)."""

import json
import os
import re
from pathlib import Path

REPO = Path(__file__).resolve().parent.parent
BUILD = REPO / "out" / "build" / "windows-x86"
INC_RE = re.compile(r'^\s*#\s*include\s*"([^"]+)"', re.MULTILINE)  # 91-35: ^ matches every line start
FLAG_RE = re.compile(r'(?:^|\s)-I([^\s]+)')


def scan_missing(data):
    """Scan compile_commands entries.

    Returns the set of unresolvable headers ({header: {includer, ...}}) and
    the number of translation units actually checked.
    """
    commands = {
        Path(entry["file"].replace("/", os.sep)): entry["command"]
        for entry in data
    }

    missing = {}
    checked = 0
    for source, command in commands.items():
        if not source.exists():
            continue

        checked += 1
        include_dirs = [Path(p.replace("/", os.sep)) for p in FLAG_RE.findall(command)]
        text = source.read_text(encoding="utf-8", errors="replace")
        for match in INC_RE.finditer(text):
            header = match.group(1)
            resolves = (source.parent / header).resolve().exists() \
                or any((directory / header).exists() for directory in include_dirs)
            if not resolves:
                missing.setdefault(header.replace("/", os.sep), set()).add(
                    str(source.relative_to(REPO)))

    return missing, checked


def print_report(missing, checked):
    print(f"scanned {checked} TUs")
    print(f"=== {len(missing)} unresolvable quoted headers ===")
    for header in sorted(missing):
        users = sorted(missing[header])
        print(f"\n{header}  ({len(users)} includer(s))")
        for user in users[:6]:
            print(f"    <- {user}")
        if len(users) > 6:
            print(f"    ... +{len(users) - 6} more")


def main():
    data = json.loads((BUILD / "compile_commands.json").read_text(encoding="utf-8"))
    missing, checked = scan_missing(data)
    print_report(missing, checked)


if __name__ == "__main__":
    main()
