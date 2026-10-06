#!/usr/bin/env python3
"""Invariant assertions for the deploy variant matrix (XC-15).

The project ships ~10 docker-compose variants; a defect fixed in one variant
(all-in-one-traefik hard-coded POSTGRES_PASSWORD, the dapr Redis component
shipped without redisPassword) kept surviving in others because nothing
checked the whole matrix. Run this from CI or before a release:

    python deploy/audit_compose.py

It scans every compose file and dapr component YAML under deploy/ for:

  1. hard-coded password literals on *_PASSWORD / password fields
     (must come from an environment interpolation like ${VAR:?...});
  2. images pinned to ":latest";
  3. duplicate keys at the same mapping indentation (e.g. a doubled "spec:");
  4. dapr redis components without a redisPassword metadata entry.

Exit code is non-zero when any invariant is violated.
"""

from __future__ import annotations

import pathlib
import re
import sys

REPO_ROOT = pathlib.Path(__file__).resolve().parent.parent
DEPLOY_DIR = REPO_ROOT / "deploy"

# Fields whose value must not be a plain literal. Matches the key portion of
# lines like "POSTGRES_PASSWORD: admin" or "- name: redisPassword".
PASSWORD_KEY_RE = re.compile(r"(?i:password)")
KEY_VALUE_RE = re.compile(r"^(?P<indent>\s*)(?P<key>[A-Za-z0-9_-]+)\s*:\s*(?P<value>.*?)\s*$")

INTERPOLATION_RE = re.compile(r"\$\{[A-Za-z_][A-Za-z0-9_]*(:[?\-+][^}]*)?}")

COMMENT_ONLY_RE = re.compile(r"^\s*#")


def hard_coded_password_violation(path: pathlib.Path, line_number: int, key: str) -> str:
    """Build the invariant message for a password field holding a plain literal."""
    return (
        f"{path.relative_to(REPO_ROOT)}:{line_number}: hard-coded password literal on '{key}' "
        "(use an environment interpolation such as ${PASSWORD:?...})")


def is_plain_secret_value(value: str) -> bool:
    """True when a password value is a hard-coded literal instead of an
    environment interpolation."""
    value = value.strip()
    if not value:
        return False
    if value.startswith("#"):
        return False
    # Strip surrounding quotes.
    if len(value) >= 2 and value[0] in "\"'" and value[-1] == value[0]:
        value = value[1:-1]
    return INTERPOLATION_RE.search(value) is None


def audit_compose_file(path: pathlib.Path) -> list[str]:
    violations: list[str] = []
    # Indentation stack of (indent, key) describing the current mapping path.
    stack: list[tuple[int, str]] = []
    # Keys already seen in each mapping, keyed by the parent mapping path.
    mapping_keys: dict[tuple[str, ...], set[str]] = {}

    with path.open(encoding="utf-8") as handle:
        for line_number, raw_line in enumerate(handle, start=1):
            line = raw_line.rstrip("\n")
            if COMMENT_ONLY_RE.match(line):
                continue

            # A list item marker starts a new sequence element; note its
            # indentation so nested keys cannot be confused with siblings.
            list_match = re.match(r"^(?P<indent>\s*)-\s*(?P<rest>.*)$", line)

            match = KEY_VALUE_RE.match(line)
            if match:
                indent = len(match.group("indent"))
                key = match.group("key")
                value = match.group("value")

                # Pop to the mapping this key belongs to.
                while stack and indent <= stack[-1][0]:
                    stack.pop()

                parent_path = tuple(entry[1] for entry in stack)

                if key in mapping_keys.setdefault(parent_path, set()):
                    violations.append(
                        f"{path.relative_to(REPO_ROOT)}:{line_number}: duplicate key '{key}' in the same mapping")
                mapping_keys[parent_path].add(key)

                # A key with a nested mapping becomes part of the path.
                if not value:
                    stack.append((indent, key))

                if PASSWORD_KEY_RE.search(key) and is_plain_secret_value(value):
                    violations.append(hard_coded_password_violation(path, line_number, key))

                if key == "image" and re.search(r":latest\b", value):
                    violations.append(
                        f"{path.relative_to(REPO_ROOT)}:{line_number}: image pinned to ':latest'")
            elif list_match and list_match.group("rest"):
                # Inline "- key: value" -- check it like a regular entry by
                # giving it the list marker's indentation.
                indent = len(list_match.group("indent")) + 1
                inline = " " * indent + list_match.group("rest")
                inline_match = KEY_VALUE_RE.match(inline)
                if inline_match:
                    key = inline_match.group("key")
                    value = inline_match.group("value")
                    if PASSWORD_KEY_RE.search(key) and is_plain_secret_value(value):
                        violations.append(hard_coded_password_violation(path, line_number, key))

    return violations


def audit_dapr_component(path: pathlib.Path) -> list[str]:
    violations: list[str] = []
    text = path.read_text(encoding="utf-8")

    if re.search(r"type:\s*(state\.redis|pubsub\.redis)", text) and not re.search(
            r"name:\s*redisPassword", text):
        violations.append(
            f"{path.relative_to(REPO_ROOT)}: dapr redis component has no 'redisPassword' metadata entry")

    return violations


def main() -> int:
    compose_files = sorted(DEPLOY_DIR.rglob("docker-compose*.yml"))
    component_files = sorted(DEPLOY_DIR.rglob("*.yaml"))

    all_violations: list[str] = []
    for compose in compose_files:
        all_violations.extend(audit_compose_file(compose))
    for component in component_files:
        all_violations.extend(audit_dapr_component(component))

    print(f"Audited {len(compose_files)} compose file(s) and {len(component_files)} dapr component(s).")
    if all_violations:
        print(f"FAILED - {len(all_violations)} invariant violation(s):", file=sys.stderr)
        for violation in all_violations:
            print(f"  - {violation}", file=sys.stderr)
        return 1

    print("PASS - all deploy invariants hold.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
