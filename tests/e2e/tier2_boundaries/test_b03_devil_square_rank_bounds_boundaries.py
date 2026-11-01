# tests/e2e/tier2_boundaries/test_b03_devil_square_rank_bounds_boundaries.py
import unittest
from tests.e2e.harness.systemic_engine import DevilSquareRankValidator


class TestB03DevilSquareRankBoundsBoundaries(unittest.TestCase):
    """
    Feature 03 Boundaries: Devil Square Rank Bounds.
    Tests rank clamping at negative values, 12 ranks (11+1), 200 ranks, and max integer limits.
    """

    def test_negative_rank_clamped_to_zero(self):
        # Negative ranks clamped to 0
        self.assertEqual(DevilSquareRankValidator.clamp_ranks(-1), 0)
        self.assertEqual(DevilSquareRankValidator.clamp_ranks(-100), 0)

    def test_rank_count_12_clamped_to_11(self):
        # 12 is buffer_capacity + 1
        self.assertEqual(DevilSquareRankValidator.clamp_ranks(12), 11)

    def test_rank_count_199_clamped_to_11(self):
        # 199 was the legacy flawed guard threshold (iNumDevilRank >= 200)
        self.assertEqual(DevilSquareRankValidator.clamp_ranks(199), 11)

    def test_rank_count_200_clamped_to_11(self):
        self.assertEqual(DevilSquareRankValidator.clamp_ranks(200), 11)

    def test_extreme_rank_count_clamped_to_11(self):
        self.assertEqual(DevilSquareRankValidator.clamp_ranks(65535), 11)


if __name__ == "__main__":
    unittest.main()
