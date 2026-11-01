# tests/e2e/tier2_boundaries/test_b19_server_id_overflow_boundaries.py
import unittest
from tests.e2e.harness.systemic_engine import ServerConfigIdValidator


class TestB19ServerIdOverflowBoundaries(unittest.TestCase):
    """
    Feature 19 Boundaries: Server Config Next Server ID Overflow.
    Tests boundary counts: 254 (allocates 255), 255 (rejected), 256 (rejected), 500, and 65535.
    """

    def test_count_254_yields_255(self):
        success, next_id = ServerConfigIdValidator.get_next_server_id(254)
        self.assertTrue(success)
        self.assertEqual(next_id, 255)

    def test_count_255_rejected(self):
        success, next_id = ServerConfigIdValidator.get_next_server_id(255)
        self.assertFalse(success)
        self.assertIsNone(next_id)

    def test_count_256_rejected(self):
        success, next_id = ServerConfigIdValidator.get_next_server_id(256)
        self.assertFalse(success)

    def test_count_500_rejected(self):
        success, next_id = ServerConfigIdValidator.get_next_server_id(500)
        self.assertFalse(success)

    def test_count_65535_rejected(self):
        success, next_id = ServerConfigIdValidator.get_next_server_id(65535)
        self.assertFalse(success)


if __name__ == "__main__":
    unittest.main()
