# tests/e2e/tier2_boundaries/test_b12_economy_double_deduction_boundaries.py
import unittest
from tests.e2e.harness.economy_engine import EconomyEngine


class TestB12EconomyDoubleDeductionBoundaries(unittest.TestCase):
    """
    Feature 12 Boundaries: Economy Double-Deduction Fix.
    Tests boundary monetary values: buyer gold == price, buyer gold == price - 1, price 0, and seller at MAX_GOLD.
    """

    def test_buyer_gold_exact_equality_with_price(self):
        # Buyer: 5000, Price: 5000 -> 0 remaining
        success, buyer_gold, seller_gold, _ = EconomyEngine.process_store_purchase(
            buyer_gold=5000, seller_gold=0, price=5000, inventory_has_space=True
        )
        self.assertTrue(success)
        self.assertEqual(buyer_gold, 0)
        self.assertEqual(seller_gold, 5000)

    def test_buyer_gold_one_short_of_price(self):
        # Buyer: 4999, Price: 5000 -> Rejected
        success, buyer_gold, _, _ = EconomyEngine.process_store_purchase(
            buyer_gold=4999, seller_gold=0, price=5000, inventory_has_space=True
        )
        self.assertFalse(success)
        self.assertEqual(buyer_gold, 4999)

    def test_zero_price_transaction_rejected(self):
        success, buyer_gold, _, msg = EconomyEngine.process_store_purchase(
            buyer_gold=5000, seller_gold=0, price=0, inventory_has_space=True
        )
        self.assertFalse(success)
        self.assertIn("Invalid price", msg)

    def test_negative_price_transaction_rejected(self):
        success, buyer_gold, _, _ = EconomyEngine.process_store_purchase(
            buyer_gold=5000, seller_gold=0, price=-500, inventory_has_space=True
        )
        self.assertFalse(success)

    def test_seller_max_gold_clamp(self):
        # Seller at 1,999,999,000 + 2000 -> clamped to 2,000,000,000
        success, _, seller_gold, _ = EconomyEngine.process_store_purchase(
            buyer_gold=5000, seller_gold=1_999_999_000, price=2000, inventory_has_space=True
        )
        self.assertTrue(success)
        self.assertEqual(seller_gold, EconomyEngine.MAX_GOLD)


if __name__ == "__main__":
    unittest.main()
