# tests/e2e/tier3_cross_features/test_x01_movement_and_skill_combat.py
import unittest
from tests.e2e.harness.movement_engine import MovementEngine, Point
from tests.e2e.harness.skill_engine import SkillEngine


class TestX01MovementAndSkillCombat(unittest.TestCase):
    """
    Tier 3 Interaction: F09 Movement Anti-Cheat + F10 Skill Hit Anti-Cheat + F11 Skill List Safety.
    Verifies multi-system combat mechanics: movement + target range verification + cooldowns.
    """

    def setUp(self):
        self.move_engine = MovementEngine(start_x=100, start_y=100)
        self.skill_engine = SkillEngine()
        self.skill_engine.learn_skill(9)   # EvilSpirit (range 8.0)
        self.skill_engine.learn_skill(19)  # FallingSlash (range 2.0)

    def test_walk_then_skill_hit_in_range(self):
        # 1. Player walks (100, 100) -> (102, 100)
        mv_ok, _, pos = self.move_engine.validate_and_move_step(102, 100, timestamp_ms=500)
        self.assertTrue(mv_ok)
        self.assertEqual(pos, Point(102, 100))

        # 2. Player casts EvilSpirit at t=600ms
        self.assertTrue(self.skill_engine.register_animation(animation_counter=1, skill_id=9, timestamp_ms=600))

        # 3. Target is at (105, 100) -> distance 3.0 (within range 8.0)
        valid, hacker, _ = self.skill_engine.validate_skill_hit(
            animation_counter=1, hit_counter=2, skill_id=9,
            player_pos=(pos.x, pos.y), target_pos=(105, 100), timestamp_ms=800
        )
        self.assertTrue(valid)
        self.assertFalse(hacker)

    def test_teleport_attempt_blocks_subsequent_skill(self):
        # Cheater tries to teleport to (150, 150)
        mv_ok, _, pos = self.move_engine.validate_and_move_step(150, 150, timestamp_ms=500)
        self.assertFalse(mv_ok)
        self.assertEqual(pos, Point(100, 100))  # Rubberbanded!

        # Player tries to attack monster at (151, 150) assuming teleport succeeded
        self.skill_engine.register_animation(animation_counter=2, skill_id=19, timestamp_ms=600)
        valid, hacker, msg = self.skill_engine.validate_skill_hit(
            animation_counter=2, hit_counter=3, skill_id=19,
            player_pos=(pos.x, pos.y), target_pos=(151, 150), timestamp_ms=700
        )
        # Because player was rubberbanded to (100, 100), distance to (151, 150) is ~71 > range 2.0
        self.assertFalse(valid)
        self.assertTrue(hacker)
        self.assertIn("out of skill range", msg)

    def test_skill_animation_while_moving_synchronization(self):
        # Moving and animating at close timestamps
        self.move_engine.validate_and_move_step(101, 101, timestamp_ms=1000)
        self.skill_engine.register_animation(animation_counter=5, skill_id=9, timestamp_ms=1050)
        valid, hacker, _ = self.skill_engine.validate_skill_hit(
            5, 6, 9, (101, 101), (105, 105), timestamp_ms=1200
        )
        self.assertTrue(valid)

    def test_speedhack_and_instant_hit_cascade_rejection(self):
        # Speedhack move packet (< 250ms)
        self.move_engine.validate_and_move_step(101, 100, timestamp_ms=1000)
        speedhack_ok, msg, _ = self.move_engine.validate_and_move_step(102, 100, timestamp_ms=1050)
        self.assertFalse(speedhack_ok)
        self.assertIn("Speedhack detected", msg)

        # Unregistered hit packet
        hit_ok, hacker, _ = self.skill_engine.validate_skill_hit(
            animation_counter=99, hit_counter=100, skill_id=9,
            player_pos=(100, 100), target_pos=(101, 100), timestamp_ms=1060
        )
        self.assertFalse(hit_ok)
        self.assertTrue(hacker)

    def test_skill_removal_during_movement(self):
        # Player walking and unlearning skill
        self.move_engine.validate_and_move_step(101, 100, timestamp_ms=500)
        # Remove skill at index 0 (EvilSpirit)
        success, removed_skill = self.skill_engine.remove_skill_by_index(0)
        self.assertTrue(success)
        self.assertEqual(removed_skill, 9)
        # Invalid index removal does not cause underflow
        bad_remove, _ = self.skill_engine.remove_skill_by_index(-1)
        self.assertFalse(bad_remove)

    def test_instant_move_exploit_denial(self):
        # Attacker sends InstantMoveRequest right onto target (120, 120) to bypass melee range check
        inst_ok, msg, pos = self.move_engine.validate_instant_move_request(120, 120, debugger_attached=False)
        self.assertFalse(inst_ok)
        self.assertEqual(pos, Point(100, 100))

        # Melee hit from original position fails range check
        self.skill_engine.register_animation(animation_counter=8, skill_id=19, timestamp_ms=1000)
        valid, hacker, _ = self.skill_engine.validate_skill_hit(
            8, 9, 19, (pos.x, pos.y), (120, 120), timestamp_ms=1100
        )
        self.assertFalse(valid)
        self.assertTrue(hacker)


if __name__ == "__main__":
    unittest.main()
