# tests/e2e/tier4_scenarios/test_s05_dungeon_event_progression.py
import unittest
from tests.e2e.harness.economy_engine import EconomyEngine
from tests.e2e.harness.systemic_engine import DevilSquareRankValidator, QuestRewardValidator
from tests.e2e.harness.packet_framing import PacketFramingEngine


class TestS05DungeonEventProgression(unittest.TestCase):
    """
    Tier 4: Scenario 9 & 10.
    Scenario 9: Devil Square Dungeon Entry & Negative Index Rejection.
    Scenario 10: Event Completion & Bounded Leaderboard Ranking Dispatch.
    """

    def test_scenario_09_devil_square_dungeon_entry_validation(self):
        # 1. Attacker attempts to enter with negative ticket index (-1)
        bad_ok, bad_msg = EconomyEngine.validate_devil_square_ticket_index(-1)
        self.assertFalse(bad_ok)
        self.assertIn("Negative ticket index rejected", bad_msg)

        # 2. Legitimate player enters with ticket at slot 22
        good_ok, _ = EconomyEngine.validate_devil_square_ticket_index(22)
        self.assertTrue(good_ok)

    def test_scenario_10_event_completion_and_bounded_ranking_dispatch(self):
        # 1. Devil Square match finishes with 25 participants
        raw_participant_count = 25
        clamped_ranks = DevilSquareRankValidator.clamp_ranks(raw_participant_count)
        self.assertEqual(clamped_ranks, 11)  # Buffer capacity MatchResult[11]

        # 2. Server serializes DevilSquareScore packet (HeadCode 0x86, 12 bytes total: 3 header + 9 payload)
        score_payload = b"\x00" * 9
        score_pkt = PacketFramingEngine.build_c1_packet(head_code=0x86, payload=score_payload)
        self.assertEqual(len(score_pkt), 12)
        valid, _ = PacketFramingEngine.validate_client_dispatch(score_pkt)
        self.assertTrue(valid)

        # 3. Quest completion rewards distributed and clamped to 5
        rewards = QuestRewardValidator.clamp_reward_count(8)
        self.assertEqual(rewards, 5)


if __name__ == "__main__":
    unittest.main()
