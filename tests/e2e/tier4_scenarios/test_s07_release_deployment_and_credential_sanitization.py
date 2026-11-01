# tests/e2e/tier4_scenarios/test_s07_release_deployment_and_credential_sanitization.py
import unittest
import os
from tests.e2e.harness.security_scanner import SecurityScanner


class TestS07ReleaseDeploymentAndCredentialSanitization(unittest.TestCase):
    """
    Tier 4: Scenario 13 & 14.
    Scenario 13: Zero-Trust Release Package Credential Audit.
    Scenario 14: Windows PowerShell 5.1 One-Click Deployment Pipeline.
    """

    REPO_ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..", ".."))

    def test_scenario_13_zero_trust_release_package_credential_audit(self):
        # Audit Server App ConnectionSettings.xml
        server_xml = os.path.join(self.REPO_ROOT, "发布包", "Server", "App", "Server", "ConnectionSettings.xml")
        server_audit = SecurityScanner.audit_connection_settings_xml(server_xml)
        self.assertTrue(server_audit["exists"])
        self.assertTrue(server_audit["is_well_formed_xml"])

        # Audit Mobile Server App ConnectionSettings.xml
        mobile_xml = os.path.join(self.REPO_ROOT, "OpenMU-安卓手机版-可安装", "OpenMU-手机服务器", "App", "Server", "ConnectionSettings.xml")
        mobile_audit = SecurityScanner.audit_connection_settings_xml(mobile_xml)
        self.assertTrue(mobile_audit["exists"])
        self.assertTrue(mobile_audit["is_well_formed_xml"])

    def test_scenario_14_powershell_51_one_click_deployment_pipeline(self):
        # 1. Audit quick-start.ps1
        quickstart = os.path.join(self.REPO_ROOT, "OpenMU", "deploy", "quick-start.ps1")
        audit_ps = SecurityScanner.audit_quickstart_powershell_compatibility(quickstart)
        self.assertTrue(audit_ps["exists"])

        # 2. Audit sidecar credentials cleanup in start_client.ps1
        start_client = os.path.join(self.REPO_ROOT, "start_client.ps1")
        audit_sidecar = SecurityScanner.audit_sidecar_credentials_lifecycle(start_client)
        self.assertTrue(audit_sidecar["exists"])

        # 3. Audit verify_02.py syntax
        verify_py = os.path.join(self.REPO_ROOT, "loc", "verify_02.py")
        audit_py = SecurityScanner.audit_python_script_imports(verify_py)
        self.assertTrue(audit_py["exists"])
        self.assertTrue(audit_py["syntax_valid"])


if __name__ == "__main__":
    unittest.main()
