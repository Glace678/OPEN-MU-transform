# tests/e2e/tier2_boundaries/test_b34_dictionary_adapter_boundaries.py
import unittest
from tests.e2e.harness.regression_models import DictionaryAdapterContract


class TestB34DictionaryAdapterBoundaries(unittest.TestCase):
    """
    Feature 34 Boundaries: DictionaryAdapter Contract.
    Tests boundary conditions:
    - Setting empty string, 0, None, and complex object values.
    - Contains(KeyValuePair) on empty adapter.
    - Remove on non-existent keys.
    - Repeated overwrite replacements on identical key.
    - Clear method fully emptying the underlying raw collection.
    """

    def setUp(self):
        self.adapter = DictionaryAdapterContract()

    def test_indexer_set_null_or_empty_values(self):
        """Adapter accepts empty string, 0, False, and None values under upsert."""
        self.adapter[""] = ""
        self.adapter[0] = 0
        self.adapter["NoneKey"] = None
        self.adapter["Flag"] = False

        self.assertEqual(self.adapter[""], "")
        self.assertEqual(self.adapter[0], 0)
        self.assertIsNone(self.adapter["NoneKey"])
        self.assertFalse(self.adapter["Flag"])
        self.assertEqual(self.adapter.count(), 4)

    def test_empty_dictionary_contains_kvp_returns_false(self):
        """Contains(KeyValuePair) on empty collection returns False gracefully."""
        self.assertFalse(self.adapter.contains_kvp_hardened("NonExistent", 123))
        self.assertFalse(self.adapter.contains_kvp_hardened(None, None))
        self.assertFalse(self.adapter.contains_key("NonExistent"))

    def test_remove_nonexistent_key_returns_false(self):
        """Remove on non-existent keys returns False without error."""
        self.assertFalse(self.adapter.remove_key("NonExistent"))
        self.assertFalse(self.adapter.remove_kvp_hardened("NonExistent", 123))

    def test_consecutive_replacements_same_key(self):
        """Replacing the same key 100 times in loop maintains exact collection size of 1."""
        for i in range(100):
            self.adapter["Counter"] = i
            self.assertEqual(self.adapter["Counter"], i)
            self.assertEqual(self.adapter.count(), 1)

    def test_clear_dictionary_resets_all_kvps(self):
        """Clear empties the underlying collection completely."""
        self.adapter["A"] = 1
        self.adapter["B"] = 2
        self.adapter["C"] = 3
        self.assertEqual(self.adapter.count(), 3)

        self.adapter._raw_collection.clear()
        self.assertEqual(self.adapter.count(), 0)
        self.assertFalse(self.adapter.contains_key("A"))


if __name__ == "__main__":
    unittest.main()
