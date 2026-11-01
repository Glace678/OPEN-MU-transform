# tests/e2e/tier2_boundaries/test_b24_deploy_ps51_compatibility_boundaries.py
import unittest
import os
import tempfile
from tests.e2e.harness.security_scanner import SecurityScanner


class TestB24DeployPS51CompatibilityBoundaries(unittest.TestCase):
    """
    Feature 24 Boundaries: Deployment Script PS 5.1 Compatibility.
    Tests boundary conditions of script analysis: non-existent script, empty script, .NET Core GetInt32 detection.
    """

    def test_non_existent_script_handling(self):
        res = SecurityScanner.audit_quickstart_powershell_compatibility("non_existent.ps1")
        self.assertFalse(res["exists"])

    def test_detection_of_flawed_getint32_api(self):
        with tempfile.NamedTemporaryFile("w", delete=False, suffix=".ps1") as f:
            f.write("$j = [System.Security.Cryptography.RandomNumberGenerator]::GetInt32(0, $i + 1)")
            path = f.name
        try:
            res = SecurityScanner.audit_quickstart_powershell_compatibility(path)
            self.assertTrue(res["has_getint32"])
            self.assertFalse(res["is_ps51_compatible"])
        finally:
            os.remove(path)

    def test_detection_of_clean_getbytes_api(self):
        with tempfile.NamedTemporaryFile("w", delete=False, suffix=".ps1") as f:
            f.write("$b = New-Object byte[] 4\n$generator.GetBytes($b)\n$j = $b[0] % ($i + 1)")
            path = f.name
        try:
            res = SecurityScanner.audit_quickstart_powershell_compatibility(path)
            self.assertFalse(res["has_getint32"])
            self.assertTrue(res["has_getbytes"])
            self.assertTrue(res["is_ps51_compatible"])
        finally:
            os.remove(path)

    def test_detection_of_clean_get_random_api(self):
        with tempfile.NamedTemporaryFile("w", delete=False, suffix=".ps1") as f:
            f.write("$j = Get-Random -Minimum 0 -Maximum ($i + 1)")
            path = f.name
        try:
            res = SecurityScanner.audit_quickstart_powershell_compatibility(path)
            self.assertFalse(res["has_getint32"])
            self.assertTrue(res["has_get_random"])
            self.assertTrue(res["is_ps51_compatible"])
        finally:
            os.remove(path)

    def test_empty_script_boundary(self):
        with tempfile.NamedTemporaryFile("w", delete=False, suffix=".ps1") as f:
            f.write("")
            path = f.name
        try:
            res = SecurityScanner.audit_quickstart_powershell_compatibility(path)
            self.assertFalse(res["has_getint32"])
        finally:
            os.remove(path)


if __name__ == "__main__":
    unittest.main()
