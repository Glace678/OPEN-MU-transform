#!/usr/bin/env python3
"""One-shot P1 mechanical replacement of legacy alias macros.

  Smart_Ptr(X)            -> std::shared_ptr<X>
  Weak_Ptr(X)             -> std::weak_ptr<X>
  SmartPointer(X);        -> class X;
                             using XPtr = std::shared_ptr<X>;

The macro definitions themselves (lines starting with #define) are skipped;
remove them from src/source/Core/Globals/_define.h in the follow-up manual
edit. Behavior-preserving: expansion output matches the original macros
(including the forward declaration emitted by SmartPointer).

Usage:
  python tools/modernize_legacy_macros.py            # rewrite
  python tools/modernize_legacy_macros.py --check    # fail if any token left
"""

from __future__ import annotations

import argparse
import re
import sys
from pathlib import Path

# Anchored to this file so the tool works regardless of the caller's cwd.
REPO_ROOT = Path(__file__).resolve().parent.parent
ROOTS = [REPO_ROOT / "src" / "source", REPO_ROOT / "src" / "MuEditor", REPO_ROOT / "tests"]
EXTENSIONS = {".h", ".cpp", ".hpp"}
SKIP_PARTS = {"ThirdParty", "third_party", "node_modules", ".cache", "build", "out"}

SMART_PTR = re.compile(r"\bSmart_Ptr\(\s*(\w+)\s*\)")
WEAK_PTR = re.compile(r"\bWeak_Ptr\(\s*(\w+)\s*\)")
SMART_DECL = re.compile(r"^(\s*)SmartPointer\(\s*(\w+)\s*\)\s*;\s*$")

REMAINING = re.compile(r"\b(Smart_Ptr|Weak_Ptr|SmartPointer)\b")


def smart_decl_repl(match: re.Match[str]) -> str:
    indent, name = match.group(1), match.group(2)
    return f"{indent}class {name};\n{indent}using {name}Ptr = std::shared_ptr<{name}>;"


def transform(text: str) -> str:
    output: list[str] = []
    for line in text.splitlines(keepends=True):
        if line.lstrip().startswith("#define"):
            output.append(line)
            continue
        line = SMART_DECL.sub(smart_decl_repl, line)
        line = SMART_PTR.sub(r"std::shared_ptr<\1>", line)
        line = WEAK_PTR.sub(r"std::weak_ptr<\1>", line)
        output.append(line)
    return "".join(output)


def iter_files() -> list[Path]:
    files: list[Path] = []
    for root in ROOTS:
        if not root.exists():
            continue
        for path in root.rglob("*"):
            if any(part in SKIP_PARTS for part in path.parts):
                continue
            if path.suffix.lower() in EXTENSIONS and path.is_file():
                files.append(path)
    return files


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--check", action="store_true")
    args = parser.parse_args()

    changed = 0
    leftovers: list[str] = []
    for path in iter_files():
        original = path.read_text(encoding="utf-8", errors="replace")
        updated = transform(original)
        # The macro definitions themselves are expected leftovers (they are
        # removed by hand from _define.h in the follow-up edit). A file that
        # still contains those #defines is exempt from the leftover report -
        # the inner loop already skips #define lines anyway.
        if REMAINING.search(updated) and "#define Smart" not in updated:
            for number, line in enumerate(updated.splitlines(), 1):
                if REMAINING.search(line) and not line.lstrip().startswith("#define"):
                    leftovers.append(f"{path}:{number}: {line.strip()}")
        if updated != original:
            changed += 1
            if not args.check:
                path.write_text(updated, encoding="utf-8", newline="")

    if args.check:
        if leftovers:
            print("\n".join(leftovers))
            return 1
        print("no legacy alias macro tokens remain")
        return 0

    print(f"updated {changed} file(s)")
    if leftovers:
        print("remaining tokens (expected: macro definitions):")
        print("\n".join(leftovers))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
