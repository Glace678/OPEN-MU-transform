# tests/e2e/tier1_features/test_f15_player_store_negative_price.py
import unittest
from tests.e2e.harness.economy_engine import EconomyEngine


class TestF15PlayerStoreNegativePrice(unittest.TestCase):
    """
    Feature 15: Player Store Negative Price Guard (PlayerShopSetItemPricePacketHandlerPlugIn.cs).
    Prevents integer overflow to negative price in player store.
    """

    def test_valid_store_price_acceptance(self):
        valid, msg = EconomyEngine.validate_player_store_price(50000)
        self.assertTrue(valid)

    def test_negative_price_rejected(self):
        valid, msg = EconomyEngine.validate_player_store_price(-1)
        self.assertFalse(valid)
        self.assertIn("negative", msg)

    def test_large_negative_price_rejected(self):
        valid, msg = EconomyEngine.validate_player_store_price(-2147483648)
        self.assertFalse(valid)

    def test_max_shop_price_accepted(self):
        valid, msg = EconomyEngine.validate_player_store_price(EconomyEngine.MAX_SHOP_PRICE)
        self.assertTrue(valid)

    def test_exceeded_max_shop_price_rejected(self):
        valid, msg = EconomyEngine.validate_player_store_price(EconomyEngine.MAX_SHOP_PRICE + 1)
        self.assertFalse(valid)
        self.assertIn("exceeds maximum", msg)


if __name__ == "__main__":
    unittest.main()
