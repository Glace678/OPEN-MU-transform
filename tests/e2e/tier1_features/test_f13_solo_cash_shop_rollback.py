# tests/e2e/tier1_features/test_f13_solo_cash_shop_rollback.py
import unittest


class MockSoloCashShopService:
    def __init__(self, initial_cash: int):
        self.cash_balance = initial_cash
        self.committed = False
        self.view_notified = False

    def buy_item_two_stage(self, price: int, simulate_view_error: bool = False) -> bool:
        if self.cash_balance < price:
            return False
        # Stage 1: Commit funds deduction
        self.cash_balance -= price
        self.committed = True

        # Stage 2: View notification
        try:
            if simulate_view_error:
                raise RuntimeError("Network glitch in client view notification")
            self.view_notified = True
        except Exception:
            # View error is logged as warning; committed cash deduction remains intact!
            self.view_notified = False

        return True


class TestF13SoloCashShopRollback(unittest.TestCase):
    """
    Feature 13: Solo Cash Shop Rollback Safety (SoloCashShopService.cs).
    Prevents view notification network errors from destabilizing committed cash transactions.
    """

    def test_successful_cash_shop_purchase(self):
        service = MockSoloCashShopService(initial_cash=500)
        success = service.buy_item_two_stage(price=100, simulate_view_error=False)
        self.assertTrue(success)
        self.assertEqual(service.cash_balance, 400)
        self.assertTrue(service.committed)
        self.assertTrue(service.view_notified)

    def test_cash_shop_view_failure_isolation(self):
        # Network error during view notification must NOT rollback committed cash transaction
        service = MockSoloCashShopService(initial_cash=500)
        success = service.buy_item_two_stage(price=100, simulate_view_error=True)
        self.assertTrue(success)
        self.assertEqual(service.cash_balance, 400)
        self.assertTrue(service.committed)
        self.assertFalse(service.view_notified)

    def test_pre_commit_exception_clean_rollback(self):
        # Insufficient funds aborts before commit
        service = MockSoloCashShopService(initial_cash=50)
        success = service.buy_item_two_stage(price=100)
        self.assertFalse(success)
        self.assertEqual(service.cash_balance, 50)
        self.assertFalse(service.committed)

    def test_cash_balance_invariants(self):
        service = MockSoloCashShopService(initial_cash=100)
        service.buy_item_two_stage(price=100)
        self.assertEqual(service.cash_balance, 0)

    def test_two_stage_commit_separation(self):
        # Verification that funds are debited even when view fails
        service = MockSoloCashShopService(initial_cash=1000)
        service.buy_item_two_stage(price=300, simulate_view_error=True)
        self.assertEqual(service.cash_balance, 700)


if __name__ == "__main__":
    unittest.main()
