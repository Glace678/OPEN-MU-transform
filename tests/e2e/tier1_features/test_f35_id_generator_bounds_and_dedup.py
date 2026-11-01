# tests/e2e/tier1_features/test_f35_id_generator_bounds_and_dedup.py
import unittest
from tests.e2e.harness.regression_models import IdGeneratorEngine


class TestF35IdGeneratorBoundsAndDedup(unittest.TestCase):
    """
    Feature 35: IdGenerator Lower Bound Validation & Duplicate Rejection (PER-02 / PER-13).
    Ensures that GiveBack checks id >= firstValue, rejects unissued IDs,
    and prohibits duplicate enqueuing to prevent duplicate ID generation.
    """

    def setUp(self):
        # Generator configured with range [100..200]
        self.generator = IdGeneratorEngine(first_value=100, max_value=200)

    def test_generate_sequential_ids(self):
        """Generates sequential IDs starting at first_value."""
        id1 = self.generator.generate_id()
        id2 = self.generator.generate_id()
        id3 = self.generator.generate_id()
        self.assertEqual(id1, 100)
        self.assertEqual(id2, 101)
        self.assertEqual(id3, 102)

    def test_give_back_lower_bound_rejection(self):
        """Rejecting negative IDs and IDs below first_value."""
        # Negative numbers
        self.assertFalse(self.generator.give_back_hardened(-1))
        self.assertFalse(self.generator.give_back_hardened(-1000))
        # Zero
        self.assertFalse(self.generator.give_back_hardened(0))
        # Below first_value (99 < 100)
        self.assertFalse(self.generator.give_back_hardened(99))
        self.assertEqual(self.generator.recycled_count, 0)

    def test_give_back_upper_bound_rejection(self):
        """Rejecting IDs above max_value."""
        self.assertFalse(self.generator.give_back_hardened(201))
        self.assertFalse(self.generator.give_back_hardened(999999))
        self.assertEqual(self.generator.recycled_count, 0)

    def test_give_back_duplicate_rejection(self):
        """Giving back the same issued ID twice rejects the second enqueue attempt."""
        id1 = self.generator.generate_id()  # 100
        # First give-back succeeds
        self.assertTrue(self.generator.give_back_hardened(id1))
        self.assertEqual(self.generator.recycled_count, 1)

        # Second duplicate give-back must be REJECTED!
        self.assertFalse(self.generator.give_back_hardened(id1))
        self.assertEqual(self.generator.recycled_count, 1)

    def test_recycled_id_vending_no_duplicates(self):
        """Recycled ID is reused on next generation without creating duplicate active IDs."""
        id1 = self.generator.generate_id()  # 100
        id2 = self.generator.generate_id()  # 101
        self.assertEqual(id1, 100)
        self.assertEqual(id2, 101)

        # Return 100
        self.assertTrue(self.generator.give_back_hardened(100))

        # Next generate reuses 100
        id_reused = self.generator.generate_id()
        self.assertEqual(id_reused, 100)

        # Subsequent generate advances to 102 (no duplication with 100 or 101)
        id3 = self.generator.generate_id()
        self.assertEqual(id3, 102)


if __name__ == "__main__":
    unittest.main()
