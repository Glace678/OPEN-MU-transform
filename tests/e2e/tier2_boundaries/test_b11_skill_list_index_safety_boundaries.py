# tests/e2e/tier2_boundaries/test_b11_skill_list_index_safety_boundaries.py
import unittest
from tests.e2e.harness.skill_engine import SkillEngine


class TestB11SkillListIndexSafetyBoundaries(unittest.TestCase):
    """
    Feature 11 Boundaries: Skill List Index Safety.
    Tests boundary indices: -255, -1, 0 on single-item list, 1 on single-item list, and 255.
    """

    def setUp(self):
        self.engine = SkillEngine()
        self.engine.learn_skill(10)  # List has 1 skill at index 0

    def test_extreme_negative_index_rejected(self):
        success, _ = self.engine.remove_skill_by_index(-255)
        self.assertFalse(success)

    def test_negative_one_index_rejected(self):
        success, _ = self.engine.remove_skill_by_index(-1)
        self.assertFalse(success)

    def test_index_zero_on_single_element_list(self):
        success, skill = self.engine.remove_skill_by_index(0)
        self.assertTrue(success)
        self.assertEqual(skill, 10)

    def test_index_one_on_single_element_list_rejected(self):
        # Index 1 is out of bounds for 1-element list
        success, _ = self.engine.remove_skill_by_index(1)
        self.assertFalse(success)

    def test_index_255_rejected(self):
        success, _ = self.engine.remove_skill_by_index(255)
        self.assertFalse(success)


if __name__ == "__main__":
    unittest.main()
