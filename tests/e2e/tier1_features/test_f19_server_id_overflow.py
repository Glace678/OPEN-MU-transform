# tests/e2e/tier1_features/test_f19_server_id_overflow.py
import unittest
from tests.e2e.harness.systemic_engine import ServerConfigIdValidator


class TestF19ServerIdOverflow(unittest.TestCase):
    """
    Feature 19: Server Config Next Server ID Overflow (CreateGameServerConfig.razor.cs).
    Guards nextServerId byte increment to prevent wrapping to 0 when servers reach 256.
    """

    def test_valid_next_server_id_generation(self):
        success, next_id = ServerConfigIdValidator.get_next_server_id(1)
        self.assertTrue(success)
        self.assertEqual(next_id, 2)

    def test_server_id_under_byte_limit(self):
        success, next_id = ServerConfigIdValidator.get_next_server_id(100)
        self.assertTrue(success)
        self.assertEqual(next_id, 101)

    def test_server_id_max_boundary(self):
        success, next_id = ServerConfigIdValidator.get_next_server_id(254)
        self.assertTrue(success)
        self.assertEqual(next_id, 255)

    def test_server_id_overflow_at_255_rejected(self):
        # 255 reached -> cannot allocate next byte id without wrapping to 0
        success, next_id = ServerConfigIdValidator.get_next_server_id(255)
        self.assertFalse(success)
        self.assertIsNone(next_id)

    def test_zero_server_count_initialization(self):
        success, next_id = ServerConfigIdValidator.get_next_server_id(0)
        self.assertTrue(success)
        self.assertEqual(next_id, 1)


if __name__ == "__main__":
    unittest.main()
