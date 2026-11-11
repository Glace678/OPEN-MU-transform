# tests/e2e/tier1_features/test_f23_release_tree_credentials.py
import unittest
import os
from tests.e2e.harness.security_scanner import SecurityScanner


class TestF23ReleaseTreeCredentials(unittest.TestCase):
    """
    Feature 23: Release Tree Credential Sanitization (ConnectionSettings.xml).
    Audits packaged releases to verify absence of plaintext credentials and XML structural integrity.
    """

    REPO_ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..", ".."))

    def setUp(self):
        self.server_xml = os.path.join(self.REPO_ROOT, "发布包", "Server", "App", "Server", "ConnectionSettings.xml")
        self.android_xml = os.path.join(self.REPO_ROOT, "OpenMU-安卓手机版-可安装", "OpenMU-手机服务器", "App", "Server", "ConnectionSettings.xml")

    def test_server_app_connection_settings_xml_exists(self):
        self.assertTrue(os.path.exists(self.server_xml), f"Missing {self.server_xml}")

    def test_android_app_connection_settings_xml_exists(self):
        self.assertTrue(os.path.exists(self.android_xml), f"Missing {self.android_xml}")

    def test_connection_settings_xml_well_formed(self):
        audit_res = SecurityScanner.audit_connection_settings_xml(self.server_xml)
        self.assertTrue(audit_res["exists"])
        self.assertTrue(audit_res["is_well_formed_xml"])
        self.assertTrue(audit_res["is_clean"], f"Leaked credentials in server ConnectionSettings.xml: {audit_res.get('leaks')}")
        android_res = SecurityScanner.audit_connection_settings_xml(self.android_xml)
        self.assertTrue(android_res["exists"])
        self.assertTrue(android_res["is_well_formed_xml"])
        self.assertTrue(android_res["is_clean"], f"Leaked credentials in android ConnectionSettings.xml: {android_res.get('leaks')}")

    def test_connection_strings_parsed(self):
        audit_res = SecurityScanner.audit_connection_settings_xml(self.server_xml)
        self.assertGreaterEqual(audit_res["connection_count"], 5)

    def test_audit_credentials_scanner_capability(self):
        # Verify scanner accurately identifies plaintext leaks on a mock file
        mock_clean = "<ConnectionSettings><Connections><Connection><ConnectionString>Server=localhost;Port=5432;User Id=postgres;Password=${DB_PASSWORD};Database=openmu;</ConnectionString></Connection></Connections></ConnectionSettings>"
        mock_dirty = "<ConnectionSettings><Connections><Connection><ConnectionString>Server=localhost;Port=5432;User Id=postgres;Password=admin;Database=openmu;</ConnectionString></Connection></Connections></ConnectionSettings>"

        import tempfile
        with tempfile.NamedTemporaryFile("w", delete=False, suffix=".xml") as f:
            f.write(mock_clean)
            clean_path = f.name
        with tempfile.NamedTemporaryFile("w", delete=False, suffix=".xml") as f:
            f.write(mock_dirty)
            dirty_path = f.name

        try:
            self.assertTrue(SecurityScanner.audit_connection_settings_xml(clean_path)["is_clean"])
            self.assertFalse(SecurityScanner.audit_connection_settings_xml(dirty_path)["is_clean"])
        finally:
            os.remove(clean_path)
            os.remove(dirty_path)


if __name__ == "__main__":
    unittest.main()
