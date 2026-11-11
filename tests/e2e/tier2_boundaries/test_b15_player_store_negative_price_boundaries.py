# tests/e2e/tier2_boundaries/test_b15_player_store_negative_price_boundaries.py
import unittest
from tests.e2e.harness.economy_engine import EconomyEngine


class TestB15PlayerStoreNegativePriceBoundaries(unittest.TestCase):
    """
    Feature 15 Boundaries: Player Store Negative Price Guard.
    Tests boundary pricing values: price 0, price -1, MAX_SHOP_PRICE, and int32 signed overflow values.
    """

    def test_price_zero_allowed(self):
        # 0 gold (free giveaway in personal shop)
        valid, _ = EconomyEngine.validate_player_store_price(0)
        self.assertTrue(valid)

    def test_price_negative_one_rejected(self):
        valid, msg = EconomyEngine.validate_player_store_price(-1)
        self.assertFalse(valid)

    def test_exact_max_shop_price_allowed(self):
        valid, _ = EconomyEngine.validate_player_store_price(
            2_000_000_000, max_inventory_money=EconomyEngine.MAX_SHOP_PRICE
        )
        self.assertTrue(valid)
        valid_unconfigured, _ = EconomyEngine.validate_player_store_price(EconomyEngine.INT32_MAX)
        self.assertTrue(valid_unconfigured)

    def test_max_shop_price_plus_one_rejected(self):
        valid, _ = EconomyEngine.validate_player_store_price(
            2_000_000_001, max_inventory_money=EconomyEngine.MAX_SHOP_PRICE
        )
        self.assertFalse(valid)
        valid_unconfigured, _ = EconomyEngine.validate_player_store_price(EconomyEngine.INT32_MAX + 1)
        self.assertFalse(valid_unconfigured)

    def test_int32_signed_overflow_boundary(self):
        # 2147483648 is -2147483648 when interpreted as signed 32-bit int
        valid, _ = EconomyEngine.validate_player_store_price(-2147483648)
        self.assertFalse(valid)


if __name__ == "__main__":
    unittest.main()
