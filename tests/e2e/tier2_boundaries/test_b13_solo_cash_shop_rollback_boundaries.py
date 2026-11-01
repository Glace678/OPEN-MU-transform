# tests/e2e/tier2_boundaries/test_b13_solo_cash_shop_rollback_boundaries.py
import unittest
from tests.e2e.tier1_features.test_f13_solo_cash_shop_rollback import MockSoloCashShopService


class TestB13SoloCashShopRollbackBoundaries(unittest.TestCase):
    """
    Feature 13 Boundaries: Solo Cash Shop Rollback Safety.
    Tests boundary cash amounts: balance 0, price == balance, balance 1 short, and repeated failures.
    """

    def test_purchase_with_zero_initial_cash(self):
        service = MockSoloCashShopService(initial_cash=0)
        success = service.buy_item_two_stage(price=10)
        self.assertFalse(success)
        self.assertEqual(service.cash_balance, 0)

    def test_purchase_spending_entire_cash_balance(self):
        service = MockSoloCashShopService(initial_cash=250)
        success = service.buy_item_two_stage(price=250)
        self.assertTrue(success)
        self.assertEqual(service.cash_balance, 0)

    def test_purchase_one_cash_short(self):
        service = MockSoloCashShopService(initial_cash=99)
        success = service.buy_item_two_stage(price=100)
        self.assertFalse(success)
        self.assertEqual(service.cash_balance, 99)

    def test_repeated_view_failures_do_not_drain_balance(self):
        service = MockSoloCashShopService(initial_cash=500)
        # Attempt 1
        service.buy_item_two_stage(price=100, simulate_view_error=True)
        self.assertEqual(service.cash_balance, 400)
        # Attempt 2
        service.buy_item_two_stage(price=100, simulate_view_error=True)
        self.assertEqual(service.cash_balance, 300)

    def test_large_cash_shop_purchase(self):
        service = MockSoloCashShopService(initial_cash=10_000_000)
        success = service.buy_item_two_stage(price=5_000_000)
        self.assertTrue(success)
        self.assertEqual(service.cash_balance, 5_000_000)


if __name__ == "__main__":
    unittest.main()
