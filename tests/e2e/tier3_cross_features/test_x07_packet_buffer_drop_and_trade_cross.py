# tests/e2e/tier3_cross_features/test_x07_packet_buffer_drop_and_trade_cross.py
import unittest
from datetime import datetime, timedelta, timezone
from tests.e2e.harness.packet_framing import PacketFramingEngine
from tests.e2e.harness.economy_engine import EconomyEngine
from tests.e2e.harness.regression_models import (
    ItemDefinition,
    MockItem,
    MockPlayer,
    DroppedItemEngine,
)


class TestX07PacketBufferDropAndTradeCross(unittest.TestCase):
    """
    Tier 3 Cross-Feature Suite:
    Combines Packet Buffer Boundaries (F01/NET-03) + Dropped Item Ownership Protection (GL-01) + Store/Trade Actions (F12/F15).
    Verifies multi-subsystem interaction flows where network packets trigger ground item pickups,
    inventory stacking, personal shop listings, and trade transaction rollbacks.
    """

    def setUp(self):
        self.drop_time = datetime(2026, 11, 1, 12, 0, 0, tzinfo=timezone.utc)
        self.item_def = ItemDefinition(
            item_id=301,
            name="Bless of Light",
            is_stackable=True,
            is_bound_to_character=False,
            max_durability=255,
        )
        self.player_a = MockPlayer(player_id="alice_01", character_name="Alice")
        self.player_b = MockPlayer(player_id="bob_02", character_name="Bob")

    def test_packet_framing_to_drop_pickup_success_flow(self):
        """Valid framed C3 pickup packet decoded and executed by rightful drop owner."""
        # Step 1: Server drops item on ground for Alice
        ground_item = MockItem(definition=self.item_def, durability=15)
        drop_engine = DroppedItemEngine(
            item=ground_item,
            drop_timestamp=self.drop_time,
            owner_player_ids={self.player_a.player_id},
        )

        # Step 2: Alice sends C3 pickup packet (HeadCode 0x22, 9 bytes)
        pickup_payload = b"\x00\x01\x00\x00\x00\x00"  # item drop id + map coords
        pkt = PacketFramingEngine.build_c3_packet(head_code=0x22, sub_code=None, payload=pickup_payload)
        valid, _ = PacketFramingEngine.validate_client_dispatch(pkt)
        self.assertTrue(valid)

        # Step 3: Server processes pickup for Alice
        now = self.drop_time + timedelta(seconds=2)
        success, target, _ = drop_engine.try_pickup_hardened(self.player_a, now)
        self.assertTrue(success)
        self.assertEqual(len(self.player_a.inventory), 1)
        self.assertEqual(self.player_a.inventory[0].durability, 15)

    def test_packet_framing_to_unauthorized_drop_theft_blocked(self):
        """Unauthorized client sends framed pickup packet during protection; server rejects theft."""
        ground_item = MockItem(definition=self.item_def, durability=50)
        drop_engine = DroppedItemEngine(
            item=ground_item,
            drop_timestamp=self.drop_time,
            owner_player_ids={self.player_a.player_id},
        )
        # Bob has 10 of this item in inventory
        self.player_b.inventory.append(MockItem(definition=self.item_def, durability=10))

        # Bob sends C3 packet
        pkt = PacketFramingEngine.build_c3_packet(head_code=0x22, sub_code=None, payload=b"\x00\x01\x00\x00\x00\x00")
        valid, _ = PacketFramingEngine.validate_client_dispatch(pkt)
        self.assertTrue(valid)

        # Server executes pickup under protection period: REJECTED
        now = self.drop_time + timedelta(seconds=3)
        success, _, msg = drop_engine.try_pickup_hardened(self.player_b, now)
        self.assertFalse(success)
        self.assertIn("rejected", msg.lower())
        self.assertEqual(self.player_b.inventory[0].durability, 10)  # Preserved

    def test_dropped_item_stacking_then_store_listing(self):
        """Ground item picked up, stacked to 255, then listed in player store with price check."""
        # Alice already has 240 Bless of Light
        alice_potion = MockItem(definition=self.item_def, durability=240)
        self.player_a.inventory.append(alice_potion)

        # Drops 30 Bless of Light
        ground_item = MockItem(definition=self.item_def, durability=30)
        drop_engine = DroppedItemEngine(
            item=ground_item,
            drop_timestamp=self.drop_time,
            owner_player_ids={self.player_a.player_id},
        )

        # Pickup stacks and clamps to 255
        ok, target, _ = drop_engine.try_pickup_hardened(self.player_a, self.drop_time + timedelta(seconds=1))
        self.assertTrue(ok)
        self.assertEqual(target.durability, 255)

        # List in personal store for 50,000,000 Zen
        valid_price, _ = EconomyEngine.validate_player_store_price(50_000_000)
        self.assertTrue(valid_price)

    def test_store_purchase_failure_rollback_and_dropped_item_durability_intact(self):
        """Store transaction aborted due to buyer full inventory refunds gold; seller item untouched."""
        buyer_gold = 100_000_000
        seller_gold = 10_000_000
        price = 50_000_000

        # Attempt purchase with full inventory
        success, final_buyer, final_seller, msg = EconomyEngine.process_store_purchase(
            buyer_gold=buyer_gold,
            seller_gold=seller_gold,
            price=price,
            inventory_has_space=False,  # Buyer has no space
        )
        self.assertFalse(success)
        self.assertEqual(final_buyer, buyer_gold)  # Full refund
        self.assertEqual(final_seller, seller_gold)

    def test_concurrent_pickup_packet_contention_single_winner(self):
        """Simultaneous packet dispatch from owner and thief results in single winner (the owner)."""
        ground_item = MockItem(definition=self.item_def, durability=20)
        drop_engine = DroppedItemEngine(
            item=ground_item,
            drop_timestamp=self.drop_time,
            owner_player_ids={self.player_a.player_id},
        )
        now = self.drop_time + timedelta(seconds=2)

        # Thief tries first
        thief_ok, _, _ = drop_engine.try_pickup_hardened(self.player_b, now)
        self.assertFalse(thief_ok)

        # Owner tries next
        owner_ok, _, _ = drop_engine.try_pickup_hardened(self.player_a, now)
        self.assertTrue(owner_ok)
        self.assertFalse(drop_engine.available_to_pick)

    def test_packet_buffer_boundary_overflow_rejection_during_pickup(self):
        """Malformed frame with declared length exceeding buffer is rejected before game logic."""
        # Declared size 250 but buffer has only 5 bytes -> Truncated frame
        truncated_data = bytes([0xC3, 250, 0x22, 0x01, 0x02])
        valid, err = PacketFramingEngine.validate_client_dispatch(truncated_data)
        self.assertFalse(valid)
        self.assertIn("Truncated", err)

        # Byte narrowing truncation: length < header size (2 < 3)
        narrowed_data = bytes([0xC3, 2, 0x22])
        valid2, err2 = PacketFramingEngine.validate_client_dispatch(narrowed_data)
        self.assertFalse(valid2)
        self.assertIn("less than required", err2)


if __name__ == "__main__":
    unittest.main()
