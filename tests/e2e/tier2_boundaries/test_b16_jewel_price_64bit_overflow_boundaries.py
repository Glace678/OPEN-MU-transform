# tests/e2e/tier2_boundaries/test_b16_jewel_price_64bit_overflow_boundaries.py
import unittest
from tests.e2e.harness.economy_engine import EconomyEngine


class TestB16JewelPrice64BitOverflowBoundaries(unittest.TestCase):
    """
    Feature 16 Boundaries: Item Price 32-bit Integer Overflow.
    Tests boundary jewel pack levels (0, 15, MAX_GOLD clamp) and unknown jewel types.
    """

    def test_packed_guardian_level_15_max_clamp(self):
        # Level 15: (15 + 1) * 60,000,000 * 10 = 9,600,000,000 -> clamped to 2,000,000,000
        price = EconomyEngine.calculate_packed_jewel_price_64bit("PackedGuardian", level=15)
        self.assertEqual(price, EconomyEngine.MAX_GOLD)

    def test_packed_life_level_15_max_clamp(self):
        # Level 15: (15 + 1) * 45,000,000 * 10 = 7,200,000,000 -> clamped to 2,000,000,000
        price = EconomyEngine.calculate_packed_jewel_price_64bit("PackedLife", level=15)
        self.assertEqual(price, EconomyEngine.MAX_GOLD)

    def test_packed_gemstone_price_calculation(self):
        # PackedGemstone level 0: (0 + 1) * 18,600 * 10 = 186,000
        price = EconomyEngine.calculate_packed_jewel_price_64bit("PackedGemstone", level=0)
        self.assertEqual(price, 186_000)

    def test_packed_harmony_price_calculation(self):
        # PackedHarmony level 2: (2 + 1) * 18,600 * 10 = 558,000
        price = EconomyEngine.calculate_packed_jewel_price_64bit("PackedHarmony", level=2)
        self.assertEqual(price, 558_000)

    def test_unknown_jewel_type_raises_value_error(self):
        with self.assertRaises(ValueError):
            EconomyEngine.calculate_packed_jewel_price_64bit("PackedUnknown", level=1)


if __name__ == "__main__":
    unittest.main()
