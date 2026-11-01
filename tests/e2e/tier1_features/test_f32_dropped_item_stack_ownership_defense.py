# tests/e2e/tier1_features/test_f32_dropped_item_stack_ownership_defense.py
import unittest
from datetime import datetime, timedelta, timezone
from tests.e2e.harness.regression_models import (
    ItemDefinition,
    MockItem,
    MockPlayer,
    DroppedItemEngine,
)


class TestF32DroppedItemStackOwnershipDefense(unittest.TestCase):
    """
    Feature 32: DroppedItem Stack Ownership Defense (GL-01).
    Verifies that unauthorized players cannot bypass the 10-second drop protection window
    or character binding restriction by stacking onto existing inventory items.
    """

    def setUp(self):
        self.item_def = ItemDefinition(
            item_id=101,
            name="Healing Potion",
            is_stackable=True,
            is_bound_to_character=False,
            max_durability=255,
        )
        self.drop_time = datetime(2026, 11, 1, 12, 0, 0, tzinfo=timezone.utc)
        self.owner = MockPlayer(player_id="player_owner_1", character_name="Alice")
        self.thief = MockPlayer(player_id="player_thief_2", character_name="Bob")

        # Thief already has 50 potions in inventory
        thief_potion = MockItem(definition=self.item_def, durability=50)
        self.thief.inventory.append(thief_potion)

        # Ground item has 20 potions
        self.ground_item = MockItem(definition=self.item_def, durability=20)
        self.dropped_engine = DroppedItemEngine(
            item=self.ground_item,
            drop_timestamp=self.drop_time,
            owner_player_ids={self.owner.player_id},
        )

    def test_owner_pickup_and_stacking_succeeds(self):
        """Owner can pick up and stack immediately within the 10-second protection period."""
        owner_potion = MockItem(definition=self.item_def, durability=30)
        self.owner.inventory.append(owner_potion)

        now = self.drop_time + timedelta(seconds=2)
        success, target, msg = self.dropped_engine.try_pickup_hardened(self.owner, now)

        self.assertTrue(success)
        self.assertIsNotNone(target)
        self.assertEqual(target.durability, 50)  # 30 + 20
        self.assertFalse(self.dropped_engine.available_to_pick)

    def test_unauthorized_player_stacking_blocked_during_protection(self):
        """Unauthorized player cannot stack or pick up during 10-second protection window."""
        now = self.drop_time + timedelta(seconds=5)
        success, target, msg = self.dropped_engine.try_pickup_hardened(self.thief, now)

        self.assertFalse(success)
        self.assertIsNone(target)
        self.assertIn("Ownership bypass rejected", msg)
        self.assertTrue(self.dropped_engine.available_to_pick)
        self.assertEqual(self.thief.inventory[0].durability, 50)  # Unchanged!

    def test_unauthorized_player_pickup_after_protection_expires(self):
        """After 10 seconds protection expires, non-owner can pick up and stack unbound item."""
        now = self.drop_time + timedelta(seconds=11)
        success, target, msg = self.dropped_engine.try_pickup_hardened(self.thief, now)

        self.assertTrue(success)
        self.assertIsNotNone(target)
        self.assertEqual(target.durability, 70)  # 50 + 20
        self.assertFalse(self.dropped_engine.available_to_pick)

    def test_character_bound_item_never_claimable_by_other_players(self):
        """Character-bound items cannot be picked up or stacked by other players even after 100 seconds."""
        bound_def = ItemDefinition(
            item_id=202,
            name="Quest Jewel",
            is_stackable=True,
            is_bound_to_character=True,
            max_durability=255,
        )
        bound_item = MockItem(definition=bound_def, durability=1)
        bound_engine = DroppedItemEngine(
            item=bound_item,
            drop_timestamp=self.drop_time,
            owner_player_ids={self.owner.player_id},
        )

        # Thief has matching item in inventory
        self.thief.inventory.append(MockItem(definition=bound_def, durability=5))

        now = self.drop_time + timedelta(seconds=100)
        success, target, msg = bound_engine.try_pickup_hardened(self.thief, now)

        self.assertFalse(success)
        self.assertIn("bound to character", msg)
        self.assertTrue(bound_engine.available_to_pick)

    def test_durability_clamped_on_defended_stacking(self):
        """When stacking legitimately, durability is capped at 255 to prevent overflow."""
        owner_potion = MockItem(definition=self.item_def, durability=250)
        self.owner.inventory.append(owner_potion)

        now = self.drop_time + timedelta(seconds=1)
        success, target, _ = self.dropped_engine.try_pickup_hardened(self.owner, now)

        self.assertTrue(success)
        self.assertEqual(target.durability, 255)  # 250 + 20 clamped to 255


if __name__ == "__main__":
    unittest.main()
