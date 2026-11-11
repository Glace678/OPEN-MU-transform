#!/usr/bin/env python3
"""Scan TUs and report quoted #includes that fail to resolve against the
actual -I paths in compile_commands.json (lost local-untracked headers)."""

import json
import os
import re
import shlex
from pathlib import Path

REPO = Path(__file__).resolve().parent.parent
BUILD = REPO / "out" / "build" / "windows-x86"
INC_RE = re.compile(r'^\s*#\s*include\s*"([^"]+)"', re.MULTILINE)  # 91-35: ^ matches every line start
FLAG_RE = re.compile(
    r'(?:^|\s)(?:-I|/I|-isystem|-iquote|-external:I|/external:I)\s*(?:"([^"]+)"|(\S+))'
)


def _extract_include_dirs(entry):
    """Extract include search paths from a compile_commands entry across GCC/Clang and MSVC."""
    base_dir = Path(entry.get("directory", str(REPO)).replace("/", os.sep))
    raw_paths = []
    tokens = entry.get("arguments")
    if tokens:
        i = 0
        while i < len(tokens):
            tok = tokens[i]
            if tok in ("-I", "/I", "-isystem", "-iquote", "/external:I", "-external:I"):
                if i + 1 < len(tokens):
                    raw_paths.append(tokens[i + 1])
                    i += 1
            elif tok.startswith(("-I", "/I")) and len(tok) > 2:
                raw_paths.append(tok[2:])
            elif tok.startswith("-isystem") and len(tok) > 8:
                raw_paths.append(tok[8:])
            elif tok.startswith("-iquote") and len(tok) > 7:
                raw_paths.append(tok[7:])
            elif tok.startswith(("/external:I", "-external:I")) and len(tok) > 11:
                raw_paths.append(tok[11:])
            i += 1
    else:
        command = entry.get("command", "")
        for match in FLAG_RE.finditer(command):
            raw_paths.append(match.group(1) or match.group(2))

    dirs = []
    for inc_path in raw_paths:
        if not inc_path:
            continue
        candidate = Path(inc_path.strip('"').replace("/", os.sep))
        if not candidate.is_absolute():
            candidate = (base_dir / candidate).resolve()
        dirs.append(candidate)
    return dirs


def _display_source_path(source: Path) -> str:
    resolved = source.resolve()
    if resolved.is_relative_to(REPO):
        return str(resolved.relative_to(REPO))
    return str(resolved)


def scan_missing(data):
    """Scan compile_commands entries.

    Returns the set of unresolvable headers ({header: {includer, ...}}) and
    the number of translation units actually checked.
    """
    entries_by_source = {}
    for entry in data:
        file_str = entry.get("file", "")
        if not file_str:
            continue
        src_path = Path(file_str.replace("/", os.sep))
        if not src_path.is_absolute() and "directory" in entry:
            src_path = Path(entry["directory"].replace("/", os.sep)) / src_path
        entries_by_source[src_path] = entry

    missing = {}
    checked = 0
    for source, entry in entries_by_source.items():
        if not source.exists():
            continue

        checked += 1
        include_dirs = _extract_include_dirs(entry)
        text = source.read_text(encoding="utf-8", errors="replace")
        for match in INC_RE.finditer(text):
            header = match.group(1)
            resolves = (source.parent / header).resolve().exists() \
                or any((directory / header).exists() for directory in include_dirs)
            if not resolves:
                missing.setdefault(header.replace("/", os.sep), set()).add(
                    _display_source_path(source))

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
