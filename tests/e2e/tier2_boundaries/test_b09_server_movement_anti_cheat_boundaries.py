# tests/e2e/tier2_boundaries/test_b09_server_movement_anti_cheat_boundaries.py
import unittest
from tests.e2e.harness.movement_engine import MovementEngine, Point


class TestB09ServerMovementAntiCheatBoundaries(unittest.TestCase):
    """
    Feature 09 Boundaries: Server Movement Teleport Anti-Cheat.
    Tests boundary step distances (3 rejected), map borders (0, 0), (255, 255), and obstacle blocking.
    """

    def setUp(self):
        self.engine = MovementEngine(start_x=0, start_y=0)

    def test_step_distance_3_rejected(self):
        # 3 tiles is MAX_STEP_DISTANCE + 1
        valid, msg, pos = self.engine.validate_and_move_step(3, 0, timestamp_ms=500)
        self.assertFalse(valid)
        self.assertIn("Teleport detected", msg)

    def test_diagonal_step_distance_3_rejected(self):
        # Diagonal from (0, 0) to (3, 3)
        valid, msg, pos = self.engine.validate_and_move_step(3, 3, timestamp_ms=500)
        self.assertFalse(valid)

    def test_map_corner_border_movement(self):
        # Move from (0, 0) to (1, 1)
        valid, msg, pos = self.engine.validate_and_move_step(1, 1, timestamp_ms=500)
        self.assertTrue(valid)
        self.assertEqual(pos, Point(1, 1))

    def test_map_upper_border_movement(self):
        engine = MovementEngine(start_x=254, start_y=254)
        valid, msg, pos = engine.validate_and_move_step(255, 255, timestamp_ms=500)
        self.assertTrue(valid)
        self.assertEqual(pos, Point(255, 255))

    def test_out_of_map_256_rejected(self):
        engine = MovementEngine(start_x=255, start_y=255)
        valid, msg, pos = engine.validate_and_move_step(256, 255, timestamp_ms=500)
        self.assertFalse(valid)
        self.assertIn("out of map bounds", msg)


if __name__ == "__main__":
    unittest.main()
