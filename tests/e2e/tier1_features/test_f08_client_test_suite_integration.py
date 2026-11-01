# tests/e2e/tier1_features/test_f08_client_test_suite_integration.py
import unittest
import os


class TestF08ClientTestSuiteIntegration(unittest.TestCase):
    """
    Feature 08: Client Test Suite Reintegration (MuMain/tests/CMakeLists.txt).
    Verifies gamelogic PacketBoundsTests and LoaderValidationTests are integrated in CMake.
    """

    REPO_ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..", ".."))

    def test_mumain_test_cmakelists_exists(self):
        cmake_file = os.path.join(self.REPO_ROOT, "MuMain", "tests", "CMakeLists.txt")
        self.assertTrue(os.path.exists(cmake_file), f"Missing {cmake_file}")

    def test_gamelogic_test_directory_exists(self):
        gamelogic_dir = os.path.join(self.REPO_ROOT, "MuMain", "tests", "gamelogic")
        self.assertTrue(os.path.isdir(gamelogic_dir), f"Missing {gamelogic_dir}")

    def test_packet_bounds_tests_source_exists(self):
        packet_bounds_cpp = os.path.join(self.REPO_ROOT, "MuMain", "tests", "gamelogic", "PacketBoundsTests.cpp")
        self.assertTrue(os.path.exists(packet_bounds_cpp), f"Missing {packet_bounds_cpp}")

    def test_loader_validation_tests_source_exists(self):
        loader_val_cpp = os.path.join(self.REPO_ROOT, "MuMain", "tests", "gamelogic", "LoaderValidationTests.cpp")
        self.assertTrue(os.path.exists(loader_val_cpp), f"Missing {loader_val_cpp}")

    def test_gamelogic_subproject_reintegration(self):
        # Inspect gamelogic CMakeLists.txt to confirm test targets are defined
        gamelogic_cmake = os.path.join(self.REPO_ROOT, "MuMain", "tests", "gamelogic", "CMakeLists.txt")
        self.assertTrue(os.path.exists(gamelogic_cmake))
        with open(gamelogic_cmake, "r", encoding="utf-8") as f:
            content = f.read()
        self.assertIn("PacketBoundsTests", content)
        self.assertIn("LoaderValidationTests", content)


if __name__ == "__main__":
    unittest.main()
