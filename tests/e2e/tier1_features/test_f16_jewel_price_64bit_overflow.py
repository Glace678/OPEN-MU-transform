# tests/e2e/tier1_features/test_f16_jewel_price_64bit_overflow.py
import unittest
from tests.e2e.harness.economy_engine import EconomyEngine


class TestF16JewelPrice64BitOverflow(unittest.TestCase):
    """
    Feature 16: Item Price 32-bit Integer Overflow (ItemPriceCalculator.cs).
    Computes packed jewel prices using 64-bit integer arithmetic to prevent signed 32-bit overflow.
    """

    def test_packed_bless_price_calculation(self):
        # (0 + 1) * 9,000,000 * 10 = 90,000,000
        price = EconomyEngine.calculate_packed_jewel_price_64bit("PackedBless", level=0)
        self.assertEqual(price, 90_000_000)

    def test_packed_guardian_level3_overflow_prevented(self):
        # Level 3: (3 + 1) * 60,000,000 * 10 = 2,400,000,000 -> clamped to MAX_GOLD (2,000,000,000)
        price = EconomyEngine.calculate_packed_jewel_price_64bit("PackedGuardian", level=3)
        self.assertEqual(price, EconomyEngine.MAX_GOLD)
        self.assertGreater(price, 0)

    def test_packed_life_high_level_calculation(self):
        # Level 2: (2 + 1) * 45,000,000 * 10 = 1,350,000,000
        price = EconomyEngine.calculate_packed_jewel_price_64bit("PackedLife", level=2)
        self.assertEqual(price, 1_350_000_000)

    def test_packed_creation_calculation(self):
        # Level 1: (1 + 1) * 36,000,000 * 10 = 720,000,000
        price = EconomyEngine.calculate_packed_jewel_price_64bit("PackedCreation", level=1)
        self.assertEqual(price, 720_000_000)

    def test_comparison_against_32bit_overflow_bug(self):
        # Demonstrates that legacy 32-bit calculation for PackedGuardian level 3 overflowed to negative
        buggy_price = EconomyEngine.calculate_packed_jewel_price_32bit_buggy("PackedGuardian", level=3)
        self.assertLess(buggy_price, 0)  # -1,894,967,296
        safe_price = EconomyEngine.calculate_packed_jewel_price_64bit("PackedGuardian", level=3)
        self.assertGreater(safe_price, 0)


if __name__ == "__main__":
    unittest.main()
