# tests/e2e/tier3_cross_features/test_x03_store_pricing_and_economy_deduction.py
import unittest
from tests.e2e.harness.economy_engine import EconomyEngine


class TestX03StorePricingAndEconomyDeduction(unittest.TestCase):
    """
    Tier 3 Interaction: F12 Buy Request Rollback + F15 Player Store Price Guard + F16 64-bit Jewel Pricing.
    Verifies full lifecycle of high-value jewel pricing, store listing validation, and purchase rollback refunds.
    """

    def test_high_value_jewel_store_listing_and_purchase(self):
        # 1. Calculate price for PackedBless level 2 using 64-bit: (2+1)*9M*10 = 270,000,000
        jewel_price = EconomyEngine.calculate_packed_jewel_price_64bit("PackedBless", level=2)
        self.assertEqual(jewel_price, 270_000_000)

        # 2. Validate price for player store listing
        valid, _ = EconomyEngine.validate_player_store_price(jewel_price)
        self.assertTrue(valid)

        # 3. Buyer purchases from store
        buyer_start = 500_000_000
        seller_start = 100_000_000
        ok, buyer_end, seller_end, _ = EconomyEngine.process_store_purchase(
            buyer_gold=buyer_start, seller_gold=seller_start, price=jewel_price, inventory_has_space=True
        )
        self.assertTrue(ok)
        self.assertEqual(buyer_end, 230_000_000)
        self.assertEqual(seller_end, 370_000_000)

    def test_overflow_jewel_price_store_listing_guard(self):
        # PackedGuardian level 5: 3,600,000,000 -> clamped to 2,000,000,000
        jewel_price = EconomyEngine.calculate_packed_jewel_price_64bit("PackedGuardian", level=5)
        self.assertEqual(jewel_price, EconomyEngine.MAX_GOLD)

        # Store validates max price
        valid, _ = EconomyEngine.validate_player_store_price(jewel_price)
        self.assertTrue(valid)

    def test_negative_store_price_blocks_purchase_initiation(self):
        # Attacker injects negative price -50,000
        bad_price = -50_000
        valid, msg = EconomyEngine.validate_player_store_price(bad_price)
        self.assertFalse(valid)

        # Purchase rejects negative price immediately
        ok, b_gold, s_gold, _ = EconomyEngine.process_store_purchase(100_000, 100_000, bad_price, True)
        self.assertFalse(ok)
        self.assertEqual(b_gold, 100_000)

    def test_purchase_failure_with_high_value_jewel_refund(self):
        # High value jewel priced at 270,000,000
        jewel_price = EconomyEngine.calculate_packed_jewel_price_64bit("PackedBless", level=2)
        buyer_start = 300_000_000

        # Purchase fails due to inventory full
        ok, buyer_end, seller_end, msg = EconomyEngine.process_store_purchase(
            buyer_gold=buyer_start, seller_gold=0, price=jewel_price, inventory_has_space=False
        )
        self.assertFalse(ok)
        # Buyer must receive 100% refund without double deduction!
        self.assertEqual(buyer_end, 300_000_000)
        self.assertIn("refunded", msg)

    def test_store_price_bounds_and_buyer_insufficient_funds(self):
        jewel_price = EconomyEngine.calculate_packed_jewel_price_64bit("PackedLife", level=1) # 900M
        buyer_start = 500_000_000 # 500M < 900M

        ok, b_gold, s_gold, _ = EconomyEngine.process_store_purchase(buyer_start, 0, jewel_price, True)
        self.assertFalse(ok)
        self.assertEqual(b_gold, buyer_start)

    def test_concurrent_store_purchase_contention(self):
        # Two buyers with enough funds attempt purchase
        price = 100_000_000
        # Buyer 1 succeeds
        ok1, b1_end, s_mid, _ = EconomyEngine.process_store_purchase(200_000_000, 0, price, inventory_has_space=True)
        self.assertTrue(ok1)
        self.assertEqual(b1_end, 100_000_000)
        self.assertEqual(s_mid, 100_000_000)

        # Buyer 2 tries to buy, but item is already gone (simulated via inventory_has_space=False)
        ok2, b2_end, s_final, _ = EconomyEngine.process_store_purchase(200_000_000, s_mid, price, inventory_has_space=False)
        self.assertFalse(ok2)
        self.assertEqual(b2_end, 200_000_000) # Full refund
        self.assertEqual(s_final, 100_000_000)


if __name__ == "__main__":
    unittest.main()
