# tests/e2e/tier2_boundaries/test_b35_id_generator_bounds_and_dedup_boundaries.py
import unittest
from tests.e2e.harness.regression_models import IdGeneratorEngine


class TestB35IdGeneratorBoundsAndDedupBoundaries(unittest.TestCase):
    """
    Feature 35 Boundaries: IdGenerator Bounds & Deduplication.
    Tests boundary conditions:
    - Extreme negative ID inputs (-2147483648, -1).
    - Unissued future IDs within [firstValue..maxValue].
    - Maximum capacity exhaustion throwing OverflowError.
    - Recycling IDs at maximum capacity allowing continued issuance.
    - Single ID generator boundary (first_value == max_value).
    """

    def test_extreme_negative_id_rejection(self):
        """Negative integers (int32 min, -1) are strictly rejected by GiveBack."""
        gen = IdGeneratorEngine(first_value=1, max_value=100)
        _ = gen.generate_id()  # Issues 1

        self.assertFalse(gen.give_back_hardened(-2147483648))
        self.assertFalse(gen.give_back_hardened(-1))
        self.assertFalse(gen.give_back_hardened(0))
        self.assertEqual(gen.recycled_count, 0)

    def test_unissued_future_id_rejection(self):
        """Giving back an ID that has not yet been generated is rejected."""
        gen = IdGeneratorEngine(first_value=10, max_value=50)
        _ = gen.generate_id()  # Issues 10
        _ = gen.generate_id()  # Issues 11
        # ID 20 is within [10..50], but has NEVER been issued (current_value is 11)
        self.assertFalse(gen.give_back_hardened(20))
        self.assertEqual(gen.recycled_count, 0)

    def test_max_value_boundary_exhaustion(self):
        """Generating past max_value without recycled IDs raises OverflowError."""
        gen = IdGeneratorEngine(first_value=1, max_value=3)
        self.assertEqual(gen.generate_id(), 1)
        self.assertEqual(gen.generate_id(), 2)
        self.assertEqual(gen.generate_id(), 3)

        # Exceeds max_value 3
        with self.assertRaises(OverflowError):
            gen.generate_id()

    def test_recycle_at_max_value_boundary(self):
        """Recycled IDs permit new allocations even after generator reaches max_value."""
        gen = IdGeneratorEngine(first_value=1, max_value=2)
        id1 = gen.generate_id()  # 1
        id2 = gen.generate_id()  # 2

        # Recycle 1
        self.assertTrue(gen.give_back_hardened(1))

        # Can now generate 1 without raising OverflowError
        recycled_id = gen.generate_id()
        self.assertEqual(recycled_id, 1)

    def test_first_value_equals_max_value_single_id_generator(self):
        """Generator configured with first_value == max_value works for exactly 1 item."""
        gen = IdGeneratorEngine(first_value=42, max_value=42)
        self.assertEqual(gen.generate_id(), 42)

        # Next call fails
        with self.assertRaises(OverflowError):
            gen.generate_id()

        # Recycle 42
        self.assertTrue(gen.give_back_hardened(42))
        self.assertEqual(gen.generate_id(), 42)


if __name__ == "__main__":
    unittest.main()
