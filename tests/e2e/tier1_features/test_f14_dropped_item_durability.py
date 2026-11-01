# tests/e2e/tier1_features/test_f14_dropped_item_durability.py
import unittest
from tests.e2e.harness.economy_engine import EconomyEngine


class TestF14DroppedItemDurability(unittest.TestCase):
    """
    Feature 14: Dropped Item Durability Clamp (DroppedItem.cs).
    Clamps stackTarget.Durability to byte.MaxValue (255) to prevent overflow wraparound.
    """

    def test_normal_durability_stacking(self):
        # 10 + 20 = 30
        res = EconomyEngine.clamp_durability_addition(10, 20)
        self.assertEqual(res, 30)

    def test_durability_capped_at_byte_max(self):
        # 200 + 100 = 300 -> clamped to 255
        res = EconomyEngine.clamp_durability_addition(200, 100)
        self.assertEqual(res, 255)

    def test_exact_255_boundary(self):
        # 250 + 5 = 255
        res = EconomyEngine.clamp_durability_addition(250, 5)
        self.assertEqual(res, 255)

    def test_zero_durability_addition(self):
        # 50 + 0 = 50
        res = EconomyEngine.clamp_durability_addition(50, 0)
        self.assertEqual(res, 50)

    def test_no_modulo_wrap_around(self):
        # Legacy bug wrapped 250 + 10 = 260 % 256 = 4
        res = EconomyEngine.clamp_durability_addition(250, 10)
        self.assertNotEqual(res, 4)
        self.assertEqual(res, 255)


if __name__ == "__main__":
    unittest.main()
