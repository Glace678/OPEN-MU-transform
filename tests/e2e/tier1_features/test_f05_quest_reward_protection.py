# tests/e2e/tier1_features/test_f05_quest_reward_protection.py
import unittest
from tests.e2e.harness.systemic_engine import QuestRewardValidator


class TestF05QuestRewardProtection(unittest.TestCase):
    """
    Feature 05: Quest Reward Buffer Protection (QuestMng.cpp).
    Verifies clamping m_byRequestCount and m_byRewardCount to max 5.
    """

    def test_valid_request_count(self):
        # 3 requests is valid
        self.assertEqual(QuestRewardValidator.clamp_request_count(3), 3)

    def test_valid_reward_count(self):
        # 2 rewards is valid
        self.assertEqual(QuestRewardValidator.clamp_reward_count(2), 2)

    def test_request_count_upper_clamp(self):
        # Server sends 10 requests -> clamped to 5
        self.assertEqual(QuestRewardValidator.clamp_request_count(10), 5)

    def test_reward_count_upper_clamp(self):
        # Server sends 20 rewards -> clamped to 5
        self.assertEqual(QuestRewardValidator.clamp_reward_count(20), 5)

    def test_quest_reward_array_bounds_safety(self):
        # Standard quest with max 5 items
        self.assertEqual(QuestRewardValidator.clamp_request_count(5), 5)
        self.assertEqual(QuestRewardValidator.clamp_reward_count(5), 5)


if __name__ == "__main__":
    unittest.main()
