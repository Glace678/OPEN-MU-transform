# tests/e2e/tier3_cross_features/test_x04_durability_and_item_stacking.py
import unittest
from tests.e2e.harness.economy_engine import EconomyEngine
from tests.e2e.tier1_features.test_f13_solo_cash_shop_rollback import MockSoloCashShopService


class TestX04DurabilityAndItemStacking(unittest.TestCase):
    """
    Tier 3 Interaction: F14 Dropped Durability Clamp + F17 Wing Crafting Underflow + F13 Cash Shop.
    Verifies item state consistency across crafting, ground pickup, stacking, and shop interactions.
    """

    def test_wing_crafting_downgrade_and_durability_preservation(self):
        # 1. Wing at level 0 fails crafting -> stays level 0 (no underflow to 255)
        new_lvl = EconomyEngine.downgrade_wing_level(0)
        self.assertEqual(new_lvl, 0)

        # 2. Durability is maintained and clamped
        dur = EconomyEngine.clamp_durability_addition(150, 50)
        self.assertEqual(dur, 200)

    def test_cash_shop_purchased_item_durability_clamped(self):
        # 1. Buy stackable potion from cash shop
        cash_service = MockSoloCashShopService(initial_cash=500)
        self.assertTrue(cash_service.buy_item_two_stage(price=50))

        # 2. Stack newly bought 50 potions onto existing 220 potions
        stacked_dur = EconomyEngine.clamp_durability_addition(220, 50)
        # 220 + 50 = 270 -> clamped to 255 (no wrap to 14)
        self.assertEqual(stacked_dur, 255)

    def test_multiple_stacking_cycles_remain_at_255(self):
        # Durability at 255 receives 10 drops in sequence
        dur = 255
        for _ in range(10):
            dur = EconomyEngine.clamp_durability_addition(dur, 20)
            self.assertEqual(dur, 255)

    def test_crafting_failure_then_pickup_durability_invariants(self):
        # Wing downgrades from 2 to 1
        lvl = EconomyEngine.downgrade_wing_level(2)
        self.assertEqual(lvl, 1)

        # Drop and re-pick item with durability additions
        res_dur = EconomyEngine.clamp_durability_addition(100, 155)
        self.assertEqual(res_dur, 255)

    def test_cash_shop_purchase_then_wing_downgrade_chain(self):
        # Buy crafting ingredient from cash shop
        svc = MockSoloCashShopService(initial_cash=1000)
        self.assertTrue(svc.buy_item_two_stage(100))
        self.assertEqual(svc.cash_balance, 900)

        # Attempt wing craft, fail downgrade
        lvl = EconomyEngine.downgrade_wing_level(0)
        self.assertEqual(lvl, 0)

    def test_durability_underflow_and_overflow_composite_bounds(self):
        # Lower bound: 0
        min_dur = EconomyEngine.clamp_durability_addition(0, 0)
        self.assertEqual(min_dur, 0)
        # Upper bound: 255
        max_dur = EconomyEngine.clamp_durability_addition(255, 255)
        self.assertEqual(max_dur, 255)


if __name__ == "__main__":
    unittest.main()
