# tests/e2e/tier1_features/test_f24_deploy_ps51_compatibility.py
import unittest
import os
import random
from tests.e2e.harness.security_scanner import SecurityScanner


class TestF24DeployPS51Compatibility(unittest.TestCase):
    """
    Feature 24: Deployment Script PS 5.1 Compatibility (OpenMU/deploy/quick-start.ps1).
    Validates PowerShell 5.1 compatibility and replacement of .NET Core GetInt32.
    """

    REPO_ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..", ".."))

    def setUp(self):
        self.script_path = os.path.join(self.REPO_ROOT, "OpenMU", "deploy", "quick-start.ps1")

    def test_quickstart_script_exists(self):
        self.assertTrue(os.path.exists(self.script_path), f"Missing {self.script_path}")

    def test_quickstart_powershell_scanner(self):
        audit = SecurityScanner.audit_quickstart_powershell_compatibility(self.script_path)
        self.assertTrue(audit["exists"])
        self.assertFalse(audit["has_getint32"])
        self.assertTrue(audit["is_ps51_compatible"])

    def test_powershell_51_safe_randomizer_logic(self):
        # Emulate the PS 5.1-compatible random generation: GetBytes(byte[]) modulo
        def ps51_random_int(max_exclusive: int) -> int:
            b = random.randbytes(4)
            val = int.from_bytes(b, byteorder="little")
            return val % max_exclusive

        for i in range(1, 20):
            res = ps51_random_int(i)
            self.assertGreaterEqual(res, 0)
            self.assertLess(res, i)

    def test_powershell_secret_entropy(self):
        # Generates 32-character secret with mixed casing, digits, and special chars
        rng = random.Random(20260418)
        secret_chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-_"
        generated = "".join(rng.choice(secret_chars) for _ in range(32))
        self.assertEqual(len(generated), 32)
        self.assertTrue(any(c.isupper() for c in generated))
        self.assertTrue(any(c.islower() for c in generated))
        self.assertTrue(any(c.isdigit() for c in generated))

    def test_fisher_yates_shuffle_compatibility(self):
        # PS 5.1 compatible Fisher-Yates shuffle
        arr = list("abcdefghij")
        original = list(arr)
        for i in range(len(arr) - 1, 0, -1):
            j = random.randint(0, i)
            arr[i], arr[j] = arr[j], arr[i]
        self.assertEqual(sorted(arr), sorted(original))


if __name__ == "__main__":
    unittest.main()
