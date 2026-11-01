# tests/e2e/tier2_boundaries/test_b10_server_skill_hit_anti_cheat_boundaries.py
import unittest
from tests.e2e.harness.skill_engine import SkillEngine


class TestB10ServerSkillHitAntiCheatBoundaries(unittest.TestCase):
    """
    Feature 10 Boundaries: Server Skill Hit Anti-Cheat.
    Tests boundary animation counters (0, 255, 256 rejected), exact delay 1500ms, and delay 1501ms.
    """

    def setUp(self):
        self.engine = SkillEngine()

    def test_animation_counter_zero(self):
        self.assertTrue(self.engine.register_animation(animation_counter=0, skill_id=9, timestamp_ms=1000))
        valid, _, _ = self.engine.validate_skill_hit(0, 1, 9, (100, 100), (102, 102), 1200)
        self.assertTrue(valid)

    def test_animation_counter_255(self):
        # 255 is byte max
        self.assertTrue(self.engine.register_animation(animation_counter=255, skill_id=9, timestamp_ms=1000))
        valid, _, _ = self.engine.validate_skill_hit(255, 0, 9, (100, 100), (102, 102), 1200)
        self.assertTrue(valid)

    def test_animation_counter_256_rejected(self):
        # 256 is out of byte range
        self.assertFalse(self.engine.register_animation(animation_counter=256, skill_id=9, timestamp_ms=1000))

    def test_delay_exactly_1500ms_accepted(self):
        self.engine.register_animation(10, 9, 1000)
        # 1000 + 1500 = 2500ms (exact boundary)
        valid, hacker, msg = self.engine.validate_skill_hit(10, 11, 9, (100, 100), (102, 102), 2500)
        self.assertTrue(valid)

    def test_delay_1501ms_rejected(self):
        self.engine.register_animation(10, 9, 1000)
        # 1000 + 1501 = 2501ms (boundary + 1ms)
        valid, hacker, msg = self.engine.validate_skill_hit(10, 11, 9, (100, 100), (102, 102), 2501)
        self.assertFalse(valid)
        self.assertTrue(hacker)


if __name__ == "__main__":
    unittest.main()
