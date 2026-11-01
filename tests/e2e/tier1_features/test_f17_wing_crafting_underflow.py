# tests/e2e/tier1_features/test_f17_wing_crafting_underflow.py
import unittest
from tests.e2e.harness.economy_engine import EconomyEngine


class TestF17WingCraftingUnderflow(unittest.TestCase):
    """
    Feature 17: Wing Crafting Byte Underflow (BaseItemCraftingHandler.cs).
    Prevents byte underflow to 255 during wing downgrade on crafting failure.
    """

    def test_level_2_wing_downgrade(self):
        new_lvl = EconomyEngine.downgrade_wing_level(2)
        self.assertEqual(new_lvl, 1)

    def test_level_1_wing_downgrade(self):
        new_lvl = EconomyEngine.downgrade_wing_level(1)
        self.assertEqual(new_lvl, 0)

    def test_level_0_wing_downgrade_clamped_to_zero(self):
        # Downgrading level 0 wing must stay level 0
        new_lvl = EconomyEngine.downgrade_wing_level(0)
        self.assertEqual(new_lvl, 0)

    def test_no_underflow_to_255(self):
        # In legacy code: (byte)(0 - 1) == 255 resulted in +255 god wing
        new_lvl = EconomyEngine.downgrade_wing_level(0)
        self.assertNotEqual(new_lvl, 255)

    def test_high_level_wing_downgrade(self):
        new_lvl = EconomyEngine.downgrade_wing_level(15)
        self.assertEqual(new_lvl, 14)


if __name__ == "__main__":
    unittest.main()
