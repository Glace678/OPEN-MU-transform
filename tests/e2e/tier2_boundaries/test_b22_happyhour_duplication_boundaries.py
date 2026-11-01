# tests/e2e/tier2_boundaries/test_b22_happyhour_duplication_boundaries.py
import unittest
from tests.e2e.harness.systemic_engine import HappyHourAttributeManager


class TestB22HappyHourDuplicationBoundaries(unittest.TestCase):
    """
    Feature 22 Boundaries: HappyHour Multiplier Duplication.
    Tests boundary multiplier values: 1.0 (neutral), multiple consecutive duplicates, removing non-existent source, re-add.
    """

    def setUp(self):
        self.mgr = HappyHourAttributeManager()

    def test_neutral_multiplier_1_0(self):
        success = self.mgr.apply_multiplier("Event_1", 1.0)
        self.assertTrue(success)
        self.assertEqual(self.mgr.get_total_multiplier(), 1.0)

    def test_five_consecutive_duplicate_attempts(self):
        self.mgr.apply_multiplier("HappyHour", 2.0)
        # Attempt to add 5 times
        for _ in range(5):
            res = self.mgr.apply_multiplier("HappyHour", 2.0)
            self.assertFalse(res)
        self.assertEqual(self.mgr.get_total_multiplier(), 2.0)

    def test_removing_non_existent_multiplier_is_safe(self):
        # Removing non-existent key must not raise exception
        self.mgr.remove_multiplier("NonExistent")
        self.assertEqual(self.mgr.get_total_multiplier(), 1.0)

    def test_re_add_after_removal(self):
        self.mgr.apply_multiplier("HappyHour", 2.0)
        self.mgr.remove_multiplier("HappyHour")
        # Now can re-add
        success = self.mgr.apply_multiplier("HappyHour", 3.0)
        self.assertTrue(success)
        self.assertEqual(self.mgr.get_total_multiplier(), 3.0)

    def test_empty_manager_default_multiplier(self):
        self.assertEqual(self.mgr.get_total_multiplier(), 1.0)


if __name__ == "__main__":
    unittest.main()
