# tests/e2e/tier1_features/test_f22_happyhour_duplication.py
import unittest
from tests.e2e.harness.systemic_engine import HappyHourAttributeManager


class TestF22HappyHourDuplication(unittest.TestCase):
    """
    Feature 22: HappyHour Multiplier Duplication (HappyHourPlugIn.cs).
    Prevents duplicate multiplier accumulation when player re-enters map during Happy Hour.
    """

    def setUp(self):
        self.mgr = HappyHourAttributeManager()

    def test_apply_happyhour_multiplier(self):
        success = self.mgr.apply_multiplier("HappyHour", 2.0)
        self.assertTrue(success)
        self.assertEqual(self.mgr.get_total_multiplier(), 2.0)

    def test_duplicate_multiplier_rejected(self):
        self.mgr.apply_multiplier("HappyHour", 2.0)
        # Re-entry attempts to re-apply the same source
        duplicate_success = self.mgr.apply_multiplier("HappyHour", 2.0)
        self.assertFalse(duplicate_success)

    def test_total_multiplier_unmodified_by_reentry(self):
        self.mgr.apply_multiplier("HappyHour", 2.0)
        self.mgr.apply_multiplier("HappyHour", 2.0)
        # Total multiplier must remain 2.0, not 4.0 or more
        self.assertEqual(self.mgr.get_total_multiplier(), 2.0)

    def test_removal_of_multiplier(self):
        self.mgr.apply_multiplier("HappyHour", 2.0)
        self.mgr.remove_multiplier("HappyHour")
        self.assertEqual(self.mgr.get_total_multiplier(), 1.0)

    def test_distinct_sources_allowed(self):
        self.mgr.apply_multiplier("HappyHour", 2.0)
        self.mgr.apply_multiplier("VIP", 1.5)
        self.assertAlmostEqual(self.mgr.get_total_multiplier(), 3.0)


if __name__ == "__main__":
    unittest.main()
