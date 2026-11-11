# tests/e2e/tier2_boundaries/test_b16_jewel_price_64bit_overflow_boundaries.py
import unittest
from tests.e2e.harness.economy_engine import EconomyEngine


class TestB16JewelPrice64BitOverflowBoundaries(unittest.TestCase):
    """
    Feature 16 Boundaries: Item Price 32-bit Integer Overflow.
    Tests boundary jewel pack levels (0, 15, MAX_GOLD clamp) and unknown jewel types.
    """

    def test_packed_guardian_level_15_and_overflow_clamp(self):
        # Level 15: (15 + 1) * 3,900 * 10 = 624,000; adversarial high level clamps to MAX_GOLD
        self.assertEqual(EconomyEngine.calculate_packed_jewel_price_64bit("PackedGuardian", level=15), 624_000)
        self.assertEqual(EconomyEngine.calculate_packed_jewel_price_64bit("PackedGuardian", level=60_000), EconomyEngine.MAX_GOLD)

    def test_packed_life_level_15_and_overflow_clamp(self):
        # Level 15: (15 + 1) * 2,950 * 10 = 472,000; adversarial high level clamps to MAX_GOLD
        self.assertEqual(EconomyEngine.calculate_packed_jewel_price_64bit("PackedLife", level=15), 472_000)
        self.assertEqual(EconomyEngine.calculate_packed_jewel_price_64bit("PackedLife", level=80_000), EconomyEngine.MAX_GOLD)

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
