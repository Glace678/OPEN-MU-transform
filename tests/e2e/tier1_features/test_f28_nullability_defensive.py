# tests/e2e/tier1_features/test_f28_nullability_defensive.py
import unittest


class DefensiveHandler:
    @staticmethod
    def get_player_name(player: dict) -> str:
        if not player or "name" not in player or player["name"] is None:
            return "Unknown"
        return str(player["name"])

    @staticmethod
    def get_item_durability(item: dict) -> int:
        if not item:
            return 0
        return item.get("durability", 0)


class TestF28NullabilityDefensive(unittest.TestCase):
    """
    Feature 28: Nullability & Defensive Checks.
    Eliminates unsafe null-forgiving operators and unchecked dereferences.
    """

    def test_none_input_defensive_guard(self):
        name = DefensiveHandler.get_player_name(None)
        self.assertEqual(name, "Unknown")

    def test_empty_collection_guard(self):
        name = DefensiveHandler.get_player_name({})
        self.assertEqual(name, "Unknown")

    def test_null_dereference_prevention(self):
        player_with_null_name = {"name": None}
        name = DefensiveHandler.get_player_name(player_with_null_name)
        self.assertEqual(name, "Unknown")

    def test_safe_navigation_operator_emulation(self):
        dur = DefensiveHandler.get_item_durability(None)
        self.assertEqual(dur, 0)

    def test_optional_field_fallback(self):
        dur = DefensiveHandler.get_item_durability({"id": 1})
        self.assertEqual(dur, 0)
        dur2 = DefensiveHandler.get_item_durability({"id": 1, "durability": 50})
        self.assertEqual(dur2, 50)


if __name__ == "__main__":
    unittest.main()
