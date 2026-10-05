#!/usr/bin/env python3
"""Scan TUs and report quoted #includes that fail to resolve against the
actual -I paths in compile_commands.json (lost local-untracked headers)."""
import json, os, re
from pathlib import Path

BUILD = Path(r"D:\openmu自用\MuMain\out\build\windows-x86")
REPO = Path(r"D:\openmu自用\MuMain")
INC_RE = re.compile(r'^\s*#\s*include\s*"([^"]+)"')
FLAG_RE = re.compile(r'(?:^|\s)-I([^\s]+)')

def main():
    data = json.loads((BUILD / "compile_commands.json").read_text(encoding="utf-8"))
    seen = {}
    for e in data:
        f = e["file"].replace("/", os.sep)
        seen.setdefault(f, e["command"])
    missing = {}
    checked = 0
    for src, cmd in seen.items():
        srcp = Path(src)
        if not srcp.exists():
            continue
        checked += 1
        idirs = [Path(p.replace("/", os.sep)) for p in FLAG_RE.findall(cmd)]
        text = srcp.read_text(encoding="utf-8", errors="replace")
        for m in INC_RE.finditer(text):
            inc = m.group(1)
            ok = (srcp.parent / inc).resolve().exists() or any((d / inc).exists() for d in idirs)
            if not ok:
                missing.setdefault(inc.replace("/", os.sep), set()).add(str(srcp.relative_to(REPO)))
    print(f"scanned {checked} TUs")
    print(f"=== {len(missing)} unresolvable quoted headers ===")
    for h in sorted(missing):
        users = sorted(missing[h])
        print(f"\n{h}  ({len(users)} includer(s))")
        for u in users[:6]:
            print(f"    <- {u}")
        if len(users) > 6:
            print(f"    ... +{len(users)-6} more")

main()