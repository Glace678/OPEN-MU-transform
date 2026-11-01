# tests/e2e/tier1_features/test_f09_server_movement_anti_cheat.py
import unittest
from tests.e2e.harness.movement_engine import MovementEngine, Point


class TestF09ServerMovementAntiCheat(unittest.TestCase):
    """
    Feature 09: Server Movement Teleport Anti-Cheat (CharacterMoveBaseHandlerPlugIn.cs).
    Enforces strict distance, terrain walkability, and rubberbanding against teleportation.
    """

    def setUp(self):
        self.engine = MovementEngine(start_x=125, start_y=125)

    def test_valid_single_step_movement(self):
        # Move (125, 125) -> (126, 125)
        valid, msg, pos = self.engine.validate_and_move_step(126, 125, timestamp_ms=500)
        self.assertTrue(valid)
        self.assertEqual(pos, Point(126, 125))

    def test_valid_diagonal_two_step_movement(self):
        # Diagonal walk of 2 tiles: (125, 125) -> (127, 127)
        valid, msg, pos = self.engine.validate_and_move_step(127, 127, timestamp_ms=500)
        self.assertTrue(valid)
        self.assertEqual(pos, Point(127, 127))

    def test_teleport_distance_exceeded_rejected(self):
        # Cheater attempts to jump 20 tiles away: (125, 125) -> (145, 125)
        valid, msg, pos = self.engine.validate_and_move_step(145, 125, timestamp_ms=500)
        self.assertFalse(valid)
        self.assertIn("Teleport detected", msg)

    def test_rubberband_position_restored(self):
        # Teleport rejected must retain/rubberband to starting position
        valid, msg, pos = self.engine.validate_and_move_step(200, 200, timestamp_ms=500)
        self.assertFalse(valid)
        self.assertEqual(pos, Point(125, 125))
        self.assertEqual(self.engine.current_position, Point(125, 125))

    def test_instant_move_rejected_without_debugger(self):
        # InstantMoveRequest without debugger must be rejected (CharacterMoveBaseHandlerPlugIn.cs)
        valid, msg, pos = self.engine.validate_instant_move_request(150, 150, debugger_attached=False)
        self.assertFalse(valid)
        self.assertIn("debugger not attached", msg)
        self.assertEqual(pos, Point(125, 125))


if __name__ == "__main__":
    unittest.main()
