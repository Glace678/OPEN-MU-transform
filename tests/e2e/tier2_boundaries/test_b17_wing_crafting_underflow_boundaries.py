# tests/e2e/tier2_boundaries/test_b17_wing_crafting_underflow_boundaries.py
import unittest
from tests.e2e.harness.economy_engine import EconomyEngine


class TestB17WingCraftingUnderflowBoundaries(unittest.TestCase):
    """
    Feature 17 Boundaries: Wing Crafting Byte Underflow.
    Tests boundary wing levels: negative level, multiple successive downgrades, and high levels.
    """

    def test_negative_level_clamped_to_zero(self):
        self.assertEqual(EconomyEngine.downgrade_wing_level(-1), 0)

    def test_successive_downgrades_from_level_1(self):
        # 1 -> 0 -> 0 -> 0
        lvl = 1
        lvl = EconomyEngine.downgrade_wing_level(lvl)
        self.assertEqual(lvl, 0)
        lvl = EconomyEngine.downgrade_wing_level(lvl)
        self.assertEqual(lvl, 0)
        lvl = EconomyEngine.downgrade_wing_level(lvl)
        self.assertEqual(lvl, 0)

    def test_level_255_downgrade(self):
        # 255 is byte max
        self.assertEqual(EconomyEngine.downgrade_wing_level(255), 254)

    def test_level_3_wing_downgrade(self):
        self.assertEqual(EconomyEngine.downgrade_wing_level(3), 2)

    def test_level_0_does_not_mutate_to_positive(self):
        res = EconomyEngine.downgrade_wing_level(0)
        self.assertLessEqual(res, 0)


if __name__ == "__main__":
    unittest.main()
