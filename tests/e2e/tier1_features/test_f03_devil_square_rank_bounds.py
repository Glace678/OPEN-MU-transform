# tests/e2e/tier1_features/test_f03_devil_square_rank_bounds.py
import unittest
from tests.e2e.harness.systemic_engine import DevilSquareRankValidator


class TestF03DevilSquareRankBounds(unittest.TestCase):
    """
    Feature 03: Devil Square Rank Bounds (CSEventMatch.cpp:215-217).
    Verifies clamping iNumDevilRank <= 10 (strictly < 11) to protect MatchResult[11] buffer.
    """

    def test_valid_rank_count_at_max_10(self):
        # 10 ranks is the maximum allowed count (MaxMatchResults = 10)
        clamped = DevilSquareRankValidator.clamp_ranks(10)
        self.assertEqual(clamped, 10)

    def test_rank_11_clamped_to_10(self):
        # 11 equals MatchResult[11] buffer capacity and clamps to MaxMatchResults = 10
        clamped = DevilSquareRankValidator.clamp_ranks(11)
        self.assertEqual(clamped, 10)

    def test_single_rank_result(self):
        # 1 winner
        clamped = DevilSquareRankValidator.clamp_ranks(1)
        self.assertEqual(clamped, 1)

    def test_zero_rank_result(self):
        # 0 participants
        clamped = DevilSquareRankValidator.clamp_ranks(0)
        self.assertEqual(clamped, 0)

    def test_match_result_buffer_safety(self):
        # Standard tournament with 8 participants
        clamped = DevilSquareRankValidator.clamp_ranks(8)
        self.assertLessEqual(clamped, DevilSquareRankValidator.MAX_RANKS)


if __name__ == "__main__":
    unittest.main()
