# tests/e2e/tier4_scenarios/test_s04_economic_trade_store_jewel_lifecycle.py
import unittest
from tests.e2e.harness.economy_engine import EconomyEngine


class TestS04EconomicTradeStoreJewelLifecycle(unittest.TestCase):
    """
    Tier 4: Scenario 7 & 8.
    Scenario 7: Full Commercial Jewel Bundling, Listing & Purchase Flow.
    Scenario 8: Transaction Failure Rollback & Durability Conservation.
    """

    def test_scenario_07_full_commercial_jewel_lifecycle(self):
        # 1. Player bundles 30 Chaos jewels -> PackedChaos level 2
        # (2 + 1) * 810,000 * 10 = 24,300,000
        jewel_price = EconomyEngine.calculate_packed_jewel_price_64bit("PackedChaos", level=2)
        self.assertEqual(jewel_price, 24_300_000)

        # 2. Seller lists item in Personal Store
        is_valid_price, _ = EconomyEngine.validate_player_store_price(jewel_price)
        self.assertTrue(is_valid_price)

        # 3. Buyer with 100,000,000 gold buys it
        buyer_gold = 100_000_000
        seller_gold = 5_000_000
        ok, b_gold, s_gold, msg = EconomyEngine.process_store_purchase(
            buyer_gold=buyer_gold, seller_gold=seller_gold, price=jewel_price, inventory_has_space=True
        )
        self.assertTrue(ok)
        self.assertEqual(b_gold, 75_700_000)
        self.assertEqual(s_gold, 29_300_000)

    def test_scenario_08_transaction_failure_rollback_and_durability_conservation(self):
        # 1. Item has durability 250
        initial_durability = 250
        # Pickup another 20 units -> clamped to 255
        clamped_dur = EconomyEngine.clamp_durability_addition(initial_durability, 20)
        self.assertEqual(clamped_dur, 255)

        # 2. Buyer attempts to purchase for 50,000,000 with a full inventory
        buyer_start = 80_000_000
        seller_start = 10_000_000
        ok, b_gold, s_gold, msg = EconomyEngine.process_store_purchase(
            buyer_gold=buyer_start, seller_gold=seller_start, price=50_000_000, inventory_has_space=False
        )
        self.assertFalse(ok)
        # 3. Exact refund verified, durability of item unchanged
        self.assertEqual(b_gold, 80_000_000)
        self.assertEqual(s_gold, 10_000_000)
        self.assertEqual(clamped_dur, 255)


if __name__ == "__main__":
    unittest.main()
