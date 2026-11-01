# tests/e2e/tier2_boundaries/test_b32_dropped_item_stack_ownership_boundaries.py
import unittest
from datetime import datetime, timedelta, timezone
from tests.e2e.harness.regression_models import (
    ItemDefinition,
    MockItem,
    MockPlayer,
    DroppedItemEngine,
)


class TestB32DroppedItemStackOwnershipBoundaries(unittest.TestCase):
    """
    Feature 32 Boundaries: DroppedItem Stack Ownership Defense.
    Tests boundary conditions:
    - Exact 10.0-second boundary transition (t = 9.999s rejected vs t = 10.000s allowed).
    - Zero durability drop handling.
    - Stacking to exact 255 byte boundary.
    - Free drops with empty owners set.
    - Calling try_pickup after item is already consumed.
    """

    def setUp(self):
        self.item_def = ItemDefinition(
            item_id=102,
            name="Mana Potion",
            is_stackable=True,
            is_bound_to_character=False,
            max_durability=255,
        )
        self.drop_time = datetime(2026, 11, 1, 12, 0, 0, tzinfo=timezone.utc)
        self.owner = MockPlayer(player_id="owner_alpha", character_name="Alpha")
        self.stranger = MockPlayer(player_id="stranger_beta", character_name="Beta")
        self.stranger.inventory.append(MockItem(definition=self.item_def, durability=10))

    def test_exact_10_second_boundary_protection_transition(self):
        """At 9.999s non-owner pickup is rejected; at exactly 10.000s it succeeds."""
        ground_item = MockItem(definition=self.item_def, durability=5)
        engine = DroppedItemEngine(
            item=ground_item,
            drop_timestamp=self.drop_time,
            owner_player_ids={self.owner.player_id},
        )

        # 9.999 seconds: still inside 10.000s window -> REJECTED
        t_sub_boundary = self.drop_time + timedelta(seconds=9, milliseconds=999)
        ok1, _, msg1 = engine.try_pickup_hardened(self.stranger, t_sub_boundary)
        self.assertFalse(ok1)
        self.assertTrue(engine.available_to_pick)

        # 10.000 seconds: protection window has expired -> ALLOWED
        t_boundary = self.drop_time + timedelta(seconds=10)
        ok2, target, msg2 = engine.try_pickup_hardened(self.stranger, t_boundary)
        self.assertTrue(ok2)
        self.assertFalse(engine.available_to_pick)
        self.assertEqual(target.durability, 15)  # 10 + 5

    def test_zero_durability_dropped_item_stack(self):
        """Dropped item with durability 0 adds 0 to target durability."""
        ground_item = MockItem(definition=self.item_def, durability=0)
        engine = DroppedItemEngine(
            item=ground_item,
            drop_timestamp=self.drop_time,
            owner_player_ids={self.owner.player_id},
        )
        now = self.drop_time + timedelta(seconds=1)
        # Owner stacks 0 onto 0
        owner_potion = MockItem(definition=self.item_def, durability=0)
        self.owner.inventory.append(owner_potion)

        ok, target, _ = engine.try_pickup_hardened(self.owner, now)
        self.assertTrue(ok)
        self.assertEqual(target.durability, 0)

    def test_stack_to_exact_255_durability_boundary(self):
        """Current durability 250 + dropped 5 reaches exactly 255 without overflow."""
        ground_item = MockItem(definition=self.item_def, durability=5)
        engine = DroppedItemEngine(
            item=ground_item,
            drop_timestamp=self.drop_time,
            owner_player_ids={self.owner.player_id},
        )
        owner_potion = MockItem(definition=self.item_def, durability=250)
        self.owner.inventory.append(owner_potion)

        ok, target, _ = engine.try_pickup_hardened(self.owner, self.drop_time + timedelta(seconds=1))
        self.assertTrue(ok)
        self.assertEqual(target.durability, 255)

    def test_empty_owners_set_behavior(self):
        """Unowned drop has no owner protection and can be stacked immediately by any player."""
        ground_item = MockItem(definition=self.item_def, durability=10)
        free_engine = DroppedItemEngine(
            item=ground_item,
            drop_timestamp=self.drop_time,
            owner_player_ids=set(),  # No owner
        )
        # Pickup at t = 0.5s succeeds for stranger because no owner is specified
        now = self.drop_time + timedelta(milliseconds=500)
        ok, target, _ = free_engine.try_pickup_hardened(self.stranger, now)
        self.assertTrue(ok)
        self.assertEqual(target.durability, 20)  # 10 + 10

    def test_already_picked_up_reentry_rejection(self):
        """Second pickup attempt on already consumed dropped item returns False."""
        ground_item = MockItem(definition=self.item_def, durability=10)
        engine = DroppedItemEngine(
            item=ground_item,
            drop_timestamp=self.drop_time,
            owner_player_ids={self.owner.player_id},
        )
        self.owner.inventory.append(MockItem(definition=self.item_def, durability=10))

        # First pickup succeeds
        ok1, _, _ = engine.try_pickup_hardened(self.owner, self.drop_time + timedelta(seconds=1))
        self.assertTrue(ok1)

        # Second pickup fails immediately
        ok2, target2, msg2 = engine.try_pickup_hardened(self.owner, self.drop_time + timedelta(seconds=2))
        self.assertFalse(ok2)
        self.assertIsNone(target2)
        self.assertIn("unavailable", msg2)


if __name__ == "__main__":
    unittest.main()
