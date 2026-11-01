# tests/e2e/tier1_features/test_f12_economy_double_deduction.py
import unittest
from tests.e2e.harness.economy_engine import EconomyEngine


class TestF12EconomyDoubleDeduction(unittest.TestCase):
    """
    Feature 12: Economy Double-Deduction Fix (BuyRequestAction.cs).
    Corrects rollback logic in BuyRequestAction to refund buyer instead of double-deducting.
    """

    def test_successful_store_purchase(self):
        # Buyer: 10,000, Seller: 5,000, Price: 2,000, Space: True
        success, buyer_gold, seller_gold, msg = EconomyEngine.process_store_purchase(
            buyer_gold=10000, seller_gold=5000, price=2000, inventory_has_space=True
        )
        self.assertTrue(success)
        self.assertEqual(buyer_gold, 8000)
        self.assertEqual(seller_gold, 7000)

    def test_failed_purchase_refunds_buyer(self):
        # Transaction fails because buyer inventory is full
        success, buyer_gold, seller_gold, msg = EconomyEngine.process_store_purchase(
            buyer_gold=10000, seller_gold=5000, price=2000, inventory_has_space=False
        )
        self.assertFalse(success)
        # Buyer gold should be fully refunded to 10,000
        self.assertEqual(buyer_gold, 10000)

    def test_no_double_deduction_on_failure(self):
        # In legacy bug, buyer was deducted twice: 10,000 - 2,000 - 2,000 = 6,000
        success, buyer_gold, seller_gold, msg = EconomyEngine.process_store_purchase(
            buyer_gold=10000, seller_gold=5000, price=2000, inventory_has_space=False
        )
        self.assertNotEqual(buyer_gold, 6000)
        self.assertEqual(buyer_gold, 10000)

    def test_insufficient_funds_rejection(self):
        # Buyer: 1,000, Price: 5,000
        success, buyer_gold, seller_gold, msg = EconomyEngine.process_store_purchase(
            buyer_gold=1000, seller_gold=5000, price=5000, inventory_has_space=True
        )
        self.assertFalse(success)
        self.assertEqual(buyer_gold, 1000)

    def test_seller_gold_preserved_on_failure(self):
        # On failure, seller gold must remain exactly 5,000
        success, buyer_gold, seller_gold, msg = EconomyEngine.process_store_purchase(
            buyer_gold=10000, seller_gold=5000, price=2000, inventory_has_space=False
        )
        self.assertEqual(seller_gold, 5000)


if __name__ == "__main__":
    unittest.main()
