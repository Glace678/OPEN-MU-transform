# tests/e2e/tier1_features/test_f10_server_skill_hit_anti_cheat.py
import unittest
from tests.e2e.harness.skill_engine import SkillEngine


class TestF10ServerSkillHitAntiCheat(unittest.TestCase):
    """
    Feature 10: Server Skill Hit Anti-Cheat (SkillHitValidator.cs).
    Restores and activates skill hit distance, target existence, and cooldown checks.
    """

    def setUp(self):
        self.engine = SkillEngine()

    def test_valid_skill_animation_and_hit(self):
        # Register animation for EvilSpirit (skill 9) at t=1000ms
        self.assertTrue(self.engine.register_animation(animation_counter=10, skill_id=9, timestamp_ms=1000))
        # Validate hit on target at distance 5 (max range 8) at t=1200ms
        valid, hacker, msg = self.engine.validate_skill_hit(
            animation_counter=10, hit_counter=11, skill_id=9,
            player_pos=(100, 100), target_pos=(103, 104), timestamp_ms=1200
        )
        self.assertTrue(valid)
        self.assertFalse(hacker)

    def test_unregistered_animation_counter_rejected(self):
        # Hit claiming animation_counter 45 without prior animation packet
        valid, hacker, msg = self.engine.validate_skill_hit(
            animation_counter=45, hit_counter=46, skill_id=9,
            player_pos=(100, 100), target_pos=(102, 102), timestamp_ms=1200
        )
        self.assertFalse(valid)
        self.assertTrue(hacker)
        self.assertIn("not registered as animation", msg)

    def test_skill_id_mismatch_rejected(self):
        # Register animation for Poison (skill 1)
        self.engine.register_animation(animation_counter=5, skill_id=1, timestamp_ms=1000)
        # Attempt hit with Twister (skill 8) using animation 5
        valid, hacker, msg = self.engine.validate_skill_hit(
            animation_counter=5, hit_counter=6, skill_id=8,
            player_pos=(100, 100), target_pos=(102, 102), timestamp_ms=1200
        )
        self.assertFalse(valid)
        self.assertTrue(hacker)
        self.assertIn("Wrong skill in referenced animation", msg)

    def test_skill_range_limit_enforced(self):
        # Register animation for FallingSlash (skill 19, max range 2.0)
        self.engine.register_animation(animation_counter=20, skill_id=19, timestamp_ms=1000)
        # Attempt hit on target 8 tiles away (target at (108, 100))
        valid, hacker, msg = self.engine.validate_skill_hit(
            animation_counter=20, hit_counter=21, skill_id=19,
            player_pos=(100, 100), target_pos=(108, 100), timestamp_ms=1100
        )
        self.assertFalse(valid)
        self.assertTrue(hacker)
        self.assertIn("out of skill range", msg)

    def test_hit_delay_timeout_rejected(self):
        # Register animation at t=1000ms
        self.engine.register_animation(animation_counter=30, skill_id=9, timestamp_ms=1000)
        # Hit arrives at t=3000ms (> 1500ms limit)
        valid, hacker, msg = self.engine.validate_skill_hit(
            animation_counter=30, hit_counter=31, skill_id=9,
            player_pos=(100, 100), target_pos=(102, 102), timestamp_ms=3000
        )
        self.assertFalse(valid)
        self.assertTrue(hacker)
        self.assertIn("delay", msg)


if __name__ == "__main__":
    unittest.main()
