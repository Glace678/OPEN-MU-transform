# tests/e2e/tier2_boundaries/test_b25_script_security_and_fixes_boundaries.py
import unittest
import os
import tempfile
from tests.e2e.harness.security_scanner import SecurityScanner


class TestB25ScriptSecurityAndFixesBoundaries(unittest.TestCase):
    """
    Feature 25 Boundaries: Script Error Fixes & Secret Protection.
    Tests boundary script errors: syntax error in python, missing os import, and missing finally block in sidecar script.
    """

    def test_syntax_error_in_python_detected(self):
        with tempfile.NamedTemporaryFile("w", delete=False, suffix=".py") as f:
            f.write("def broken_syntax(:\n  pass")
            path = f.name
        try:
            audit = SecurityScanner.audit_python_script_imports(path)
            self.assertTrue(audit["exists"])
            self.assertFalse(audit["syntax_valid"])
            self.assertIn("error", audit)
        finally:
            os.remove(path)

    def test_missing_os_import_detected(self):
        with tempfile.NamedTemporaryFile("w", delete=False, suffix=".py") as f:
            f.write("import sys\nprint(sys.argv)")
            path = f.name
        try:
            audit = SecurityScanner.audit_python_script_imports(path)
            self.assertTrue(audit["syntax_valid"])
            self.assertFalse(audit["has_os_import"])
        finally:
            os.remove(path)

    def test_alias_os_import_detected(self):
        with tempfile.NamedTemporaryFile("w", delete=False, suffix=".py") as f:
            f.write("import os as _os\nprint(_os.name)")
            path = f.name
        try:
            audit = SecurityScanner.audit_python_script_imports(path)
            self.assertTrue(audit["syntax_valid"])
            self.assertIn("_os", audit["imports"])
        finally:
            os.remove(path)

    def test_script_missing_finally_cleanup_detected(self):
        with tempfile.NamedTemporaryFile("w", delete=False, suffix=".ps1") as f:
            f.write("Start-Process client.exe\nWrite-Host 'Done'")
            path = f.name
        try:
            audit = SecurityScanner.audit_sidecar_credentials_lifecycle(path)
            self.assertTrue(audit["exists"])
            self.assertFalse(audit["has_finally_cleanup"])
        finally:
            os.remove(path)

    def test_script_with_proper_finally_cleanup_detected(self):
        with tempfile.NamedTemporaryFile("w", delete=False, suffix=".ps1") as f:
            f.write("try { Start-Process client.exe } finally { Remove-Item creds.txt -Force }")
            path = f.name
        try:
            audit = SecurityScanner.audit_sidecar_credentials_lifecycle(path)
            self.assertTrue(audit["exists"])
            self.assertTrue(audit["has_finally_cleanup"])
        finally:
            os.remove(path)


if __name__ == "__main__":
    unittest.main()
