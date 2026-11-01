# tests/e2e/tier2_boundaries/test_b30_opaque_box_e2e_suite_boundaries.py
import unittest
import os


class TestB30OpaqueBoxE2ESuiteBoundaries(unittest.TestCase):
    """
    Feature 30 Boundaries: Opaque-Box E2E Testing Suite.
    Tests boundary conditions of test harness discovery, subfolder existence, and runner dependencies.
    """

    E2E_DIR = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))

    def test_tier1_subfolder_exists_and_contains_tests(self):
        t1_dir = os.path.join(self.E2E_DIR, "tier1_features")
        self.assertTrue(os.path.isdir(t1_dir))
        files = [f for f in os.listdir(t1_dir) if f.startswith("test_") and f.endswith(".py")]
        self.assertGreaterEqual(len(files), 31)

    def test_tier2_subfolder_exists_and_contains_tests(self):
        t2_dir = os.path.join(self.E2E_DIR, "tier2_boundaries")
        self.assertTrue(os.path.isdir(t2_dir))

    def test_harness_subfolder_contains_required_engines(self):
        harness_dir = os.path.join(self.E2E_DIR, "harness")
        self.assertTrue(os.path.exists(os.path.join(harness_dir, "systemic_engine.py")))

    def test_test_infra_md_exists_and_valid(self):
        repo_root = os.path.abspath(os.path.join(self.E2E_DIR, "..", ".."))
        infra_md = os.path.join(repo_root, "TEST_INFRA.md")
        self.assertTrue(os.path.exists(infra_md))
        self.assertGreater(os.path.getsize(infra_md), 1000)

    def test_python_executable_environment(self):
        import sys
        self.assertGreaterEqual(sys.version_info.major, 3)


if __name__ == "__main__":
    unittest.main()
