# tests/e2e/tier1_features/test_f34_dictionary_adapter_contract.py
import unittest
from tests.e2e.harness.regression_models import DictionaryAdapterContract


class TestF34DictionaryAdapterContract(unittest.TestCase):
    """
    Feature 34: DictionaryAdapter / CollectionToDictionaryAdapter Contract (PER-01 / PER-12).
    Verifies that the indexer setter performs upsert replacement rather than throwing duplicate key,
    and that KeyValuePair Contains / Remove methods verify value equality.
    """

    def setUp(self):
        self.adapter = DictionaryAdapterContract()

    def test_indexer_setter_replaces_existing_key(self):
        """Assigning to an existing key replaces the value without throwing duplicate key exception."""
        self.adapter["MaxPlayers"] = 100
        self.assertEqual(self.adapter["MaxPlayers"], 100)
        self.assertEqual(self.adapter.count(), 1)

        # Upsert replacement
        self.adapter["MaxPlayers"] = 200
        self.assertEqual(self.adapter["MaxPlayers"], 200)
        self.assertEqual(self.adapter.count(), 1)

    def test_indexer_setter_inserts_new_key(self):
        """Assigning to an unmapped key inserts the key-value pair."""
        self.adapter["Port"] = 55901
        self.adapter["AutoSave"] = True
        self.assertEqual(self.adapter.count(), 2)
        self.assertEqual(self.adapter["Port"], 55901)
        self.assertTrue(self.adapter["AutoSave"])

    def test_contains_kvp_requires_value_match(self):
        """Contains(KeyValuePair) returns True only if key exists AND value matches."""
        self.adapter["ExperienceRate"] = 10.5
        # Matches key and value
        self.assertTrue(self.adapter.contains_kvp_hardened("ExperienceRate", 10.5))
        # Matches key but wrong value -> must be False
        self.assertFalse(self.adapter.contains_kvp_hardened("ExperienceRate", 99.9))
        # Non-existent key
        self.assertFalse(self.adapter.contains_kvp_hardened("DropRate", 10.5))

    def test_remove_kvp_requires_value_match(self):
        """Remove(KeyValuePair) does NOT remove if value does not match."""
        self.adapter["GuildId"] = 42
        # Wrong value -> removal fails, item preserved
        removed = self.adapter.remove_kvp_hardened("GuildId", 999)
        self.assertFalse(removed)
        self.assertTrue(self.adapter.contains_key("GuildId"))
        self.assertEqual(self.adapter["GuildId"], 42)

    def test_remove_kvp_success_when_both_match(self):
        """Remove(KeyValuePair) removes successfully when both key and value match."""
        self.adapter["GuildId"] = 42
        removed = self.adapter.remove_kvp_hardened("GuildId", 42)
        self.assertTrue(removed)
        self.assertFalse(self.adapter.contains_key("GuildId"))
        self.assertEqual(self.adapter.count(), 0)


if __name__ == "__main__":
    unittest.main()
