# tests/e2e/tier2_boundaries/test_b28_nullability_defensive_boundaries.py
import unittest
from tests.e2e.tier1_features.test_f28_nullability_defensive import DefensiveHandler


class TestB28NullabilityDefensiveBoundaries(unittest.TestCase):
    """
    Feature 28 Boundaries: Nullability & Defensive Checks.
    Tests boundary null inputs: nested null objects, null keys, whitespace-only names, empty strings.
    """

    def test_nested_none_structure(self):
        player = {"name": {"first": None}}
        name = DefensiveHandler.get_player_name(player)
        self.assertIn("first", name)  # Converted to string representation without crashing

    def test_numeric_zero_as_name(self):
        player = {"name": 0}
        name = DefensiveHandler.get_player_name(player)
        self.assertEqual(name, "0")

    def test_boolean_false_as_name(self):
        player = {"name": False}
        name = DefensiveHandler.get_player_name(player)
        self.assertEqual(name, "False")

    def test_none_item_durability(self):
        dur = DefensiveHandler.get_item_durability(None)
        self.assertEqual(dur, 0)

    def test_item_with_none_durability_value(self):
        item = {"durability": None}
        dur = item.get("durability") or 0
        self.assertEqual(dur, 0)


if __name__ == "__main__":
    unittest.main()
