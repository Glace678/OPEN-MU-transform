#!/usr/bin/env python3
"""Regression tests for find_missing_headers.py (91-35).

The quoted-include regex used to be compiled without re.MULTILINE, so `^`
only matched the very start of the file. Any translation unit that began
with a comment, `#pragma once` or code had its quoted includes silently
skipped (0 found), and a file with several quoted includes only ever had
the first one checked. That made the tool report "0 unresolvable headers"
as a false green even though real lost headers were present.

These tests pin multi-line include detection so the old bug returns red.
"""

import importlib.util
import pathlib
import unittest

_HERE = pathlib.Path(__file__).resolve().parent
_MODULE_PATH = _HERE / "find_missing_headers.py"


def _load_module():
    spec = importlib.util.spec_from_file_location(
        "find_missing_headers", _MODULE_PATH)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def _headers(text):
    return [m.group(1) for m in _load_module().INC_RE.finditer(text)]


class IncludeRegexTests(unittest.TestCase):
    def test_finds_all_quoted_includes_in_a_file(self):
        text = (
            '#include "a.h"\n'
            '#include "b.h"\n'
            '#include "c.h"\n'
        )
        self.assertEqual(_headers(text), ["a.h", "b.h", "c.h"])

    def test_finds_includes_after_a_comment_header(self):
        text = (
            "// file header comment\n"
            "#pragma once\n"
            '#include "first.h"\n'
            "int x;\n"
            '#include "second.h"\n'
        )
        self.assertEqual(_headers(text), ["first.h", "second.h"])

    def test_finds_includes_with_indentation_and_tabs(self):
        text = (
            '\t  #include "indented.h"\n'
            '#include "plain.h"\n'
        )
        self.assertEqual(_headers(text), ["indented.h", "plain.h"])

    def test_msvc_and_external_include_flags_and_out_of_repo_source(self):
        import tempfile
        mod = _load_module()
        with tempfile.TemporaryDirectory() as tmp:
            tmp_path = pathlib.Path(tmp)
            inc_a = tmp_path / "inc with spaces"
            inc_b = tmp_path / "ext_inc"
            inc_a.mkdir()
            inc_b.mkdir()
            (inc_a / "found_msvc.h").write_text("#pragma once\n", encoding="utf-8")
            (inc_b / "found_ext.h").write_text("#pragma once\n", encoding="utf-8")
            tu = tmp_path / "out_of_repo.cpp"
            tu.write_text(
                '#include "found_msvc.h"\n'
                '#include "found_ext.h"\n'
                '#include "truly_missing.h"\n',
                encoding="utf-8",
            )
            data = [{
                "directory": str(tmp_path),
                "file": str(tu),
                "command": f'cl.exe /c /I"{inc_a}" /external:I "{inc_b}" "{tu}"',
            }]
            missing, checked = mod.scan_missing(data)
            self.assertEqual(checked, 1)
            self.assertEqual(set(missing.keys()), {"truly_missing.h"})


if __name__ == "__main__":
    unittest.main()