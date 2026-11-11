# tests/e2e/tier1_features/test_f16_jewel_price_64bit_overflow.py
import unittest
from tests.e2e.harness.economy_engine import EconomyEngine


class TestF16JewelPrice64BitOverflow(unittest.TestCase):
    """
    Feature 16: Item Price 32-bit Integer Overflow (ItemPriceCalculator.cs).
    Computes packed jewel prices using 64-bit integer arithmetic to prevent signed 32-bit overflow.
    """

    def test_packed_bless_price_calculation(self):
        # ItemPriceCalculator.cs:106: (0 + 1) * 1,480 * 10 = 14,800
        price = EconomyEngine.calculate_packed_jewel_price_64bit("PackedBless", level=0)
        self.assertEqual(price, 14_800)

    def test_packed_guardian_high_level_overflow_prevented(self):
        # Level 60,000: (60,000 + 1) * 3,900 * 10 = 2,340,039,000 > int32.MaxValue -> clamped to MAX_GOLD (2B)
        price = EconomyEngine.calculate_packed_jewel_price_64bit("PackedGuardian", level=60_000)
        self.assertEqual(price, EconomyEngine.MAX_GOLD)
        self.assertGreater(price, 0)

    def test_packed_life_level2_calculation(self):
        # ItemPriceCalculator.cs:109: (2 + 1) * 2,950 * 10 = 88,500
        price = EconomyEngine.calculate_packed_jewel_price_64bit("PackedLife", level=2)
        self.assertEqual(price, 88_500)

    def test_packed_creation_calculation(self):
        # ItemPriceCalculator.cs:110: (1 + 1) * 2,360 * 10 = 47,200
        price = EconomyEngine.calculate_packed_jewel_price_64bit("PackedCreation", level=1)
        self.assertEqual(price, 47_200)

    def test_comparison_against_32bit_overflow_bug(self):
        # Demonstrates that 32-bit signed arithmetic overflows to negative on identical multipliers
        buggy_price = EconomyEngine.calculate_packed_jewel_price_32bit_buggy("PackedGuardian", level=60_000)
        self.assertLess(buggy_price, 0)
        safe_price = EconomyEngine.calculate_packed_jewel_price_64bit("PackedGuardian", level=60_000)
        self.assertEqual(safe_price, EconomyEngine.MAX_GOLD)


if __name__ == "__main__":
    unittest.main()
