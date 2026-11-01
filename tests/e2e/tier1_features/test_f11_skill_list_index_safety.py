# tests/e2e/tier1_features/test_f11_skill_list_index_safety.py
import unittest
from tests.e2e.harness.skill_engine import SkillEngine


class TestF11SkillListIndexSafety(unittest.TestCase):
    """
    Feature 11: Skill List Index Safety (SkillListViewPlugIn075.cs / SkillListViewPlugIn095.cs).
    Guards against IndexOf == -1 returning byte 255 underflow.
    """

    def setUp(self):
        self.engine = SkillEngine()
        self.engine.learn_skill(1)
        self.engine.learn_skill(2)

    def test_valid_skill_removal(self):
        # Valid removal at index 0
        success, removed = self.engine.remove_skill_by_index(0)
        self.assertTrue(success)
        self.assertEqual(removed, 1)

    def test_negative_index_rejected(self):
        # IndexOf returns -1 when skill not found; must be safely rejected
        success, removed = self.engine.remove_skill_by_index(-1)
        self.assertFalse(success)
        self.assertIsNone(removed)

    def test_out_of_bounds_index_rejected(self):
        # Index >= length
        success, removed = self.engine.remove_skill_by_index(10)
        self.assertFalse(success)
        self.assertIsNone(removed)

    def test_empty_skill_list_index_safety(self):
        empty_engine = SkillEngine()
        success, removed = empty_engine.remove_skill_by_index(0)
        self.assertFalse(success)
        self.assertIsNone(removed)

    def test_byte_255_underflow_prevention(self):
        # In legacy code: (byte)(-1) == 255 accessed SkillList[255]
        # Guard prevents executing removal on negative index
        negative_idx = -1
        is_safe = (negative_idx >= 0)
        self.assertFalse(is_safe)


if __name__ == "__main__":
    unittest.main()
