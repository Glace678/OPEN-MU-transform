# tests/e2e/tier1_features/test_f17_wing_crafting_underflow.py
import unittest
from tests.e2e.harness.economy_engine import EconomyEngine


class TestF17WingCraftingUnderflow(unittest.TestCase):
    """
    Feature 17: Wing Crafting Byte Underflow (BaseItemCraftingHandler.cs).
    Prevents byte underflow to 255 during wing downgrade on crafting failure.
    """

    def test_level_2_wing_downgrade(self):
        # ThirdWingsDowngradedRandom downgrades by 2 or 3 steps and clamps at 0
        self.assertEqual(EconomyEngine.downgrade_wing_level(2, downgrade_steps=2), 0)
        self.assertEqual(EconomyEngine.downgrade_wing_level(2, downgrade_steps=3), 0)

    def test_level_1_wing_downgrade(self):
        # Level 1 minus 2 or 3 clamps to 0 without byte wrap
        self.assertEqual(EconomyEngine.downgrade_wing_level(1, downgrade_steps=2), 0)
        self.assertEqual(EconomyEngine.downgrade_wing_level(1, downgrade_steps=3), 0)

    def test_level_0_wing_downgrade_clamped_to_zero(self):
        # Downgrading level 0 wing must stay level 0
        self.assertEqual(EconomyEngine.downgrade_wing_level(0, downgrade_steps=2), 0)
        self.assertEqual(EconomyEngine.downgrade_wing_level(0, downgrade_steps=3), 0)

    def test_no_underflow_to_255(self):
        # Legacy unchecked byte subtraction wrapped < 0 into 253..255
        new_lvl = EconomyEngine.downgrade_wing_level(0, downgrade_steps=3)
        self.assertNotIn(new_lvl, (253, 254, 255))

    def test_high_level_wing_downgrade(self):
        self.assertEqual(EconomyEngine.downgrade_wing_level(15, downgrade_steps=2), 13)
        self.assertEqual(EconomyEngine.downgrade_wing_level(15, downgrade_steps=3), 12)


if __name__ == "__main__":
    unittest.main()
