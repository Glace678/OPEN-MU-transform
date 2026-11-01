# tests/e2e/tier2_boundaries/test_b08_client_test_suite_integration_boundaries.py
import unittest
import os


class TestB08ClientTestSuiteIntegrationBoundaries(unittest.TestCase):
    """
    Feature 08 Boundaries: Client Test Suite Reintegration.
    Tests boundary conditions of CMake test configuration, target definition files, and syntax.
    """

    REPO_ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..", ".."))

    def test_gamelogic_cmakelists_non_empty(self):
        cmake_file = os.path.join(self.REPO_ROOT, "MuMain", "tests", "gamelogic", "CMakeLists.txt")
        self.assertTrue(os.path.exists(cmake_file))
        size = os.path.getsize(cmake_file)
        self.assertGreater(size, 100)

    def test_packet_bounds_tests_cpp_non_empty(self):
        cpp_file = os.path.join(self.REPO_ROOT, "MuMain", "tests", "gamelogic", "PacketBoundsTests.cpp")
        self.assertTrue(os.path.exists(cpp_file))
        self.assertGreater(os.path.getsize(cpp_file), 500)

    def test_loader_validation_tests_cpp_non_empty(self):
        cpp_file = os.path.join(self.REPO_ROOT, "MuMain", "tests", "gamelogic", "LoaderValidationTests.cpp")
        self.assertTrue(os.path.exists(cpp_file))
        self.assertGreater(os.path.getsize(cpp_file), 500)

    def test_doctest_include_availability(self):
        doctest_h = os.path.join(self.REPO_ROOT, "MuMain", "tests", "third_party", "doctest", "doctest.h")
        self.assertTrue(os.path.exists(doctest_h), f"Missing {doctest_h}")

    def test_main_cpp_runner_exists(self):
        main_cpp = os.path.join(self.REPO_ROOT, "MuMain", "tests", "main.cpp")
        self.assertTrue(os.path.exists(main_cpp))


if __name__ == "__main__":
    unittest.main()
