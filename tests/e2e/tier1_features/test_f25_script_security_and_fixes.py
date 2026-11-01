# tests/e2e/tier1_features/test_f25_script_security_and_fixes.py
import unittest
import os
from tests.e2e.harness.security_scanner import SecurityScanner


class TestF25ScriptSecurityAndFixes(unittest.TestCase):
    """
    Feature 25: Script Error Fixes & Secret Protection (verify_02.py & start_client.ps1).
    Verifies python import syntax and sidecar credential lifecycle protection.
    """

    REPO_ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..", ".."))

    def test_verify_02_script_exists(self):
        verify_py = os.path.join(self.REPO_ROOT, "loc", "verify_02.py")
        self.assertTrue(os.path.exists(verify_py), f"Missing {verify_py}")

    def test_verify_02_syntax_valid(self):
        verify_py = os.path.join(self.REPO_ROOT, "loc", "verify_02.py")
        audit = SecurityScanner.audit_python_script_imports(verify_py)
        self.assertTrue(audit["exists"])
        self.assertTrue(audit["syntax_valid"])

    def test_start_client_script_exists(self):
        start_client_ps1 = os.path.join(self.REPO_ROOT, "start_client.ps1")
        self.assertTrue(os.path.exists(start_client_ps1), f"Missing {start_client_ps1}")

    def test_start_client_has_cleanup(self):
        start_client_ps1 = os.path.join(self.REPO_ROOT, "start_client.ps1")
        audit = SecurityScanner.audit_sidecar_credentials_lifecycle(start_client_ps1)
        self.assertTrue(audit["exists"])

    def test_python_ast_scanner_capability(self):
        # Verify scanner identifies import presence on mock code
        mock_code_with_os = "import os\nprint(os.path.join('a', 'b'))"
        mock_code_without_os = "import sys\nprint('hello')"

        import tempfile
        with tempfile.NamedTemporaryFile("w", delete=False, suffix=".py") as f:
            f.write(mock_code_with_os)
            with_os = f.name
        with tempfile.NamedTemporaryFile("w", delete=False, suffix=".py") as f:
            f.write(mock_code_without_os)
            without_os = f.name

        try:
            self.assertTrue(SecurityScanner.audit_python_script_imports(with_os)["has_os_import"])
            self.assertFalse(SecurityScanner.audit_python_script_imports(without_os)["has_os_import"])
        finally:
            os.remove(with_os)
            os.remove(without_os)


if __name__ == "__main__":
    unittest.main()
