# tests/e2e/tier2_boundaries/test_b23_release_tree_credentials_boundaries.py
import unittest
import os
import tempfile
from tests.e2e.harness.security_scanner import SecurityScanner


class TestB23ReleaseTreeCredentialsBoundaries(unittest.TestCase):
    """
    Feature 23 Boundaries: Release Tree Credential Sanitization.
    Tests boundary conditions of credential auditing: missing files, empty files, malformed XML, zero connections.
    """

    def test_missing_file_audit_returns_not_found(self):
        res = SecurityScanner.audit_connection_settings_xml("non_existent_file.xml")
        self.assertFalse(res["exists"])
        self.assertIn("File not found", res["error"])

    def test_empty_file_audit_handles_parse_error(self):
        with tempfile.NamedTemporaryFile("w", delete=False, suffix=".xml") as f:
            f.write("")
            path = f.name
        try:
            res = SecurityScanner.audit_connection_settings_xml(path)
            self.assertTrue(res["exists"])
            self.assertFalse(res["is_clean"])
            self.assertIn("error", res)
        finally:
            os.remove(path)

    def test_malformed_xml_handling(self):
        with tempfile.NamedTemporaryFile("w", delete=False, suffix=".xml") as f:
            f.write("<ConnectionSettings><UnclosedTag>")
            path = f.name
        try:
            res = SecurityScanner.audit_connection_settings_xml(path)
            self.assertTrue(res["exists"])
            self.assertFalse(res["is_clean"])
        finally:
            os.remove(path)

    def test_xml_with_zero_connections(self):
        with tempfile.NamedTemporaryFile("w", delete=False, suffix=".xml") as f:
            f.write("<ConnectionSettings><Connections></Connections></ConnectionSettings>")
            path = f.name
        try:
            res = SecurityScanner.audit_connection_settings_xml(path)
            self.assertTrue(res["exists"])
            self.assertTrue(res["is_clean"])
            self.assertEqual(res["connection_count"], 0)
        finally:
            os.remove(path)

    def test_case_insensitive_password_detection(self):
        with tempfile.NamedTemporaryFile("w", delete=False, suffix=".xml") as f:
            f.write("<ConnectionSettings><Connection><ConnectionString>password=ADMIN;Database=openmu;</ConnectionString></Connection></ConnectionSettings>")
            path = f.name
        try:
            res = SecurityScanner.audit_connection_settings_xml(path)
            self.assertFalse(res["is_clean"])
            self.assertGreater(len(res["leaks"]), 0)
        finally:
            os.remove(path)


if __name__ == "__main__":
    unittest.main()
