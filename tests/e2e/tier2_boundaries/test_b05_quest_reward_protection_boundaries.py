# tests/e2e/tier2_boundaries/test_b05_quest_reward_protection_boundaries.py
import unittest
from tests.e2e.harness.systemic_engine import QuestRewardValidator


class TestB05QuestRewardProtectionBoundaries(unittest.TestCase):
    """
    Feature 05 Boundaries: Quest Reward Buffer Protection.
    Tests boundary counts: 0, negative values, 6 (5+1), 255 (byte max), and 65535.
    """

    def test_zero_request_and_reward_count(self):
        self.assertEqual(QuestRewardValidator.clamp_request_count(0), 0)
        self.assertEqual(QuestRewardValidator.clamp_reward_count(0), 0)

    def test_negative_counts_clamped_to_zero(self):
        self.assertEqual(QuestRewardValidator.clamp_request_count(-1), 0)
        self.assertEqual(QuestRewardValidator.clamp_reward_count(-5), 0)

    def test_count_6_clamped_to_5(self):
        # 6 is buffer capacity (5) + 1
        self.assertEqual(QuestRewardValidator.clamp_request_count(6), 5)
        self.assertEqual(QuestRewardValidator.clamp_reward_count(6), 5)

    def test_byte_max_255_clamped_to_5(self):
        # 255 is byte max value sent by malicious server
        self.assertEqual(QuestRewardValidator.clamp_request_count(255), 5)
        self.assertEqual(QuestRewardValidator.clamp_reward_count(255), 5)

    def test_extreme_count_clamped_to_5(self):
        self.assertEqual(QuestRewardValidator.clamp_request_count(65535), 5)
        self.assertEqual(QuestRewardValidator.clamp_reward_count(65535), 5)


if __name__ == "__main__":
    unittest.main()
