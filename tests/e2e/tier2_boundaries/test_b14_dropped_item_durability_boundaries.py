# tests/e2e/tier2_boundaries/test_b14_dropped_item_durability_boundaries.py
import unittest
from tests.e2e.harness.economy_engine import EconomyEngine


class TestB14DroppedItemDurabilityBoundaries(unittest.TestCase):
    """
    Feature 14 Boundaries: Dropped Item Durability Clamp.
    Tests boundary durability values: 255 + 1, 255 + 255, 0 + 0, and negative input rejection.
    """

    def test_durability_255_plus_1_clamped_to_255(self):
        # 255 is byte max
        res = EconomyEngine.clamp_durability_addition(255, 1)
        self.assertEqual(res, 255)

    def test_durability_255_plus_255_clamped_to_255(self):
        # Max + Max
        res = EconomyEngine.clamp_durability_addition(255, 255)
        self.assertEqual(res, 255)

    def test_durability_zero_plus_zero(self):
        res = EconomyEngine.clamp_durability_addition(0, 0)
        self.assertEqual(res, 0)

    def test_durability_one_plus_254(self):
        res = EconomyEngine.clamp_durability_addition(1, 254)
        self.assertEqual(res, 255)

    def test_negative_durability_raises_value_error(self):
        with self.assertRaises(ValueError):
            EconomyEngine.clamp_durability_addition(-1, 10)


if __name__ == "__main__":
    unittest.main()
