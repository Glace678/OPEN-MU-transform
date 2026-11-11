# tests/e2e/tier3_cross_features/test_x05_client_loader_and_event_rankings.py
import unittest
from tests.e2e.harness.systemic_engine import (
    BuffScriptValidator, DevilSquareRankValidator, QuestRewardValidator
)
from tests.e2e.harness.economy_engine import EconomyEngine


class TestX05ClientLoaderAndEventRankings(unittest.TestCase):
    """
    Tier 3 Interaction: F02 Buff Loader + F03 Devil Square Rank + F05 Quest Reward + F18 Ticket Index.
    Verifies event entry arbitration, score rankings boundary clamping, and quest/loader memory bounds.
    """

    def test_devil_square_ticket_entry_and_rank_dispatch(self):
        # 1. Player submits valid ticket index (slot 15 >= EquippableSlotsCount 12)
        ticket_ok, _ = EconomyEngine.validate_devil_square_ticket_index(15)
        self.assertTrue(ticket_ok)

        # 2. Match concludes with 15 participants; client clamps to MaxMatchResults = 10
        clamped_ranks = DevilSquareRankValidator.clamp_ranks(15)
        self.assertEqual(clamped_ranks, 10)

    def test_negative_ticket_blocks_entry_to_ranking(self):
        # Cheater submits negative ticket -1
        ticket_ok, msg = EconomyEngine.validate_devil_square_ticket_index(-1)
        self.assertFalse(ticket_ok)
        self.assertIn("Negative ticket index rejected", msg)

    def test_quest_reward_and_buff_loader_bounds_safety(self):
        # Buff loader bounds validated
        loader_ok = BuffScriptValidator.is_valid_record_count(500, 128)
        self.assertTrue(loader_ok)

        # Quest reward count bounded
        req_count = QuestRewardValidator.clamp_request_count(10) # 10 -> 5
        self.assertEqual(req_count, 5)

    def test_max_ranks_and_max_quest_rewards_coexistence(self):
        # Simultaneous max boundary event state
        max_ranks = DevilSquareRankValidator.clamp_ranks(DevilSquareRankValidator.MAX_RANKS)
        max_rewards = QuestRewardValidator.clamp_reward_count(QuestRewardValidator.MAX_QUEST_REWARDS)
        self.assertEqual(max_ranks, 10)
        self.assertEqual(max_rewards, 5)

    def test_corrupted_bmd_and_oversized_quest_payload_isolation(self):
        # 32-bit overflow BMD loader payload rejected
        bmd_ok = BuffScriptValidator.is_valid_record_count(0x40000000, 128)
        self.assertFalse(bmd_ok)

        # Malicious quest packet with 255 count clamped safely
        clamped_quest = QuestRewardValidator.clamp_request_count(255)
        self.assertEqual(clamped_quest, 5)

    def test_event_lifecycle_pipeline(self):
        # Equipped slot 0 rejected; backpack slot 12 accepted -> Admission -> Scoring -> Reward
        self.assertFalse(EconomyEngine.validate_devil_square_ticket_index(0)[0])
        entry_ok, _ = EconomyEngine.validate_devil_square_ticket_index(12)
        self.assertTrue(entry_ok)

        ranks = DevilSquareRankValidator.clamp_ranks(8)
        self.assertEqual(ranks, 8)

        rewards = QuestRewardValidator.clamp_reward_count(3)
        self.assertEqual(rewards, 3)


if __name__ == "__main__":
    unittest.main()
