# tests/e2e/tier4_scenarios/test_s03_combat_and_skill_cooldown_stress.py
import unittest
from tests.e2e.harness.skill_engine import SkillEngine


class TestS03CombatAndSkillCooldownStress(unittest.TestCase):
    """
    Tier 4: Scenario 5 & 6.
    Scenario 5: Multi-Target Area Skill Execution & Animation Synchronization.
    Scenario 6: Combat Skill Cooldown Violation & Out-of-Range Rejection.
    """

    def setUp(self):
        self.engine = SkillEngine()
        self.engine.learn_skill(9)   # EvilSpirit (max range 8.0, max hits 5)
        self.engine.learn_skill(41)  # TwistingSlash (max range 3.0, max hits 3)

    def test_scenario_05_multi_target_area_skill_execution(self):
        # 1. Cast EvilSpirit animation at t=1000ms
        self.assertTrue(self.engine.register_animation(animation_counter=10, skill_id=9, timestamp_ms=1000))

        # 2. Hits 4 targets within valid radius (range <= 8.0)
        targets = [
            (102, 102),  # dist 2.83
            (105, 100),  # dist 5.0
            (100, 106),  # dist 6.0
            (104, 104),  # dist 5.66
        ]

        player_pos = (100, 100)
        hit_counter_base = 20
        for i, target_pos in enumerate(targets):
            valid, hacker, _ = self.engine.validate_skill_hit(
                animation_counter=10, hit_counter=hit_counter_base + i, skill_id=9,
                player_pos=player_pos, target_pos=target_pos, timestamp_ms=1100 + i * 50
            )
            self.assertTrue(valid, f"Target {i} failed hit validation")
            self.assertFalse(hacker)

    def test_scenario_06_combat_cooldown_and_out_of_range_rejection(self):
        player_pos = (100, 100)

        # Violation A: Instant hit without prior animation registration
        bad_hit, hacker_a, msg_a = self.engine.validate_skill_hit(
            animation_counter=99, hit_counter=100, skill_id=41,
            player_pos=player_pos, target_pos=(101, 101), timestamp_ms=1000
        )
        self.assertFalse(bad_hit)
        self.assertTrue(hacker_a)
        self.assertIn("not registered as animation", msg_a)

        # Register animation for TwistingSlash (max range 3.0)
        self.engine.register_animation(animation_counter=50, skill_id=41, timestamp_ms=2000)

        # Violation B: Out of range target (dist 20.0 > 3.0)
        far_target = (120, 100)
        bad_range, hacker_b, msg_b = self.engine.validate_skill_hit(
            animation_counter=50, hit_counter=51, skill_id=41,
            player_pos=player_pos, target_pos=far_target, timestamp_ms=2100
        )
        self.assertFalse(bad_range)
        self.assertTrue(hacker_b)
        self.assertIn("out of skill range", msg_b)


if __name__ == "__main__":
    unittest.main()
