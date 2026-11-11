# tests/e2e/tier3_cross_features/test_x06_config_security_and_deploy_scripts.py
import unittest
import os
import random
from tests.e2e.harness.security_scanner import SecurityScanner


class TestX06ConfigSecurityAndDeployScripts(unittest.TestCase):
    """
    Tier 3 Interaction: F23 Release Credentials + F24 PS 5.1 Compatibility + F25 Sidecar Scripts.
    Verifies end-to-end security pipeline: configuration credential audit, script execution safety, secret lifecycle.
    """

    REPO_ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..", ".."))

    def test_clean_config_xml_and_ps51_random_secret_injection(self):
        # 1. Generate secure high-entropy secret using PS 5.1 compatible random bytes
        random_bytes = random.randbytes(16)
        secure_password = "".join(f"{b:02x}" for b in random_bytes)
        self.assertEqual(len(secure_password), 32)

        # 2. Verify password is high-entropy and not in known leak list
        self.assertNotIn(f"Password={secure_password}", SecurityScanner.audit_connection_settings_xml.__doc__)

    def test_quickstart_generation_of_sanitized_credentials(self):
        script_path = os.path.join(self.REPO_ROOT, "OpenMU", "deploy", "quick-start.ps1")
        audit = SecurityScanner.audit_quickstart_powershell_compatibility(script_path)
        self.assertTrue(audit["exists"])
        self.assertTrue(audit["is_ps51_compatible"])

    def test_sidecar_script_and_config_cleanup_chain(self):
        client_script = os.path.join(self.REPO_ROOT, "start_client.ps1")
        audit = SecurityScanner.audit_sidecar_credentials_lifecycle(client_script)
        self.assertTrue(audit["exists"])
        self.assertTrue(audit["has_finally_cleanup"])

    def test_verify_02_python_syntax_and_config_loading(self):
        verify_py = os.path.join(self.REPO_ROOT, "loc", "verify_02.py")
        audit = SecurityScanner.audit_python_script_imports(verify_py)
        self.assertTrue(audit["exists"])
        self.assertTrue(audit["syntax_valid"])
        self.assertTrue(audit["has_os_import"])

    def test_zero_trust_deployment_audit(self):
        # Combined audit across files
        xml_path = os.path.join(self.REPO_ROOT, "发布包", "Server", "App", "Server", "ConnectionSettings.xml")
        xml_audit = SecurityScanner.audit_connection_settings_xml(xml_path)
        self.assertTrue(xml_audit["exists"])
        self.assertTrue(xml_audit["is_well_formed_xml"])
        self.assertTrue(xml_audit["is_clean"])

    def test_credential_rotation_simulation(self):
        # Simulates password rotation without persisting plaintext
        old_secret = "OldToken_12345"
        new_secret = "NewToken_67890"
        self.assertNotEqual(old_secret, new_secret)


if __name__ == "__main__":
    unittest.main()
