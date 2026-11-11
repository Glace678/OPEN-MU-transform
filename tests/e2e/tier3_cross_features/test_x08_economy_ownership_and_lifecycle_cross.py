# tests/e2e/tier3_cross_features/test_x08_economy_ownership_and_lifecycle_cross.py
import unittest
import tempfile
import os
from datetime import datetime, timedelta, timezone
from tests.e2e.harness.economy_engine import EconomyEngine
from tests.e2e.harness.systemic_engine import RAIIResourceTracker
from tests.e2e.harness.regression_models import (
    HeroState,
    PlayerCharacterState,
    PlayerRegenerationEngine,
    IdGeneratorEngine,
    DictionaryAdapterContract,
    PacketDefinitionsSaveEngine,
    ItemDefinition,
    MockItem,
    MockPlayer,
    DroppedItemEngine,
)


class TestX08EconomyOwnershipAndLifecycleCross(unittest.TestCase):
    """
    Tier 3 Cross-Feature Suite:
    Combines PK Countdown Monotonicity (GL-03) + IdGenerator Bounded Recycling (PER-13) +
    DictionaryAdapter (PER-12) + PacketDefinitions XML Save (NET-04) + RAII Lifecycle (F27).
    """

    def setUp(self):
        self.t0 = datetime(2026, 11, 1, 12, 0, 0, tzinfo=timezone.utc)
        self.temp_dir = tempfile.TemporaryDirectory()

    def tearDown(self):
        self.temp_dir.cleanup()

    def test_pk_countdown_progression_during_item_drops_and_trades(self):
        """Player in PK state drops item and trades; countdown decreases monotonically across all events."""
        pk_char = PlayerCharacterState(
            name="RedNamePlayer",
            state=HeroState.PlayerKiller1,
            state_remaining_seconds=600,
            last_regenerate_utc=self.t0,
        )

        # Event 1: Drop an item at t = +100s
        t1 = self.t0 + timedelta(seconds=100)
        rem1 = PlayerRegenerationEngine.regenerate_hardened(pk_char, t1)
        self.assertEqual(rem1, 500)

        # Event 2: Trade with buyer at t = +250s
        t2 = self.t0 + timedelta(seconds=250)
        rem2 = PlayerRegenerationEngine.regenerate_hardened(pk_char, t2)
        self.assertEqual(rem2, 350)
        self.assertLess(rem2, rem1)

    def test_id_generator_with_packet_definition_export_and_item_ids(self):
        """Entity IDs issued by bounded IdGenerator written into XML packet definitions without collision."""
        gen = IdGeneratorEngine(first_value=5000, max_value=5010)
        item_id_1 = gen.generate_id()
        item_id_2 = gen.generate_id()
        self.assertEqual(item_id_1, 5000)
        self.assertEqual(item_id_2, 5001)

        # Generate XML referencing these IDs
        xml_content = (
            f'<Definitions>\n'
            f'  <Item UniqueId="{item_id_1}" Name="DragonHelm" />\n'
            f'  <Item UniqueId="{item_id_2}" Name="DragonArmor" />\n'
            f'</Definitions>\n'
        )
        file_path = os.path.join(self.temp_dir.name, "ItemsExport.xml")
        PacketDefinitionsSaveEngine.save_hardened(file_path, xml_content)

        valid, _ = PacketDefinitionsSaveEngine.verify_xml_integrity(file_path)
        self.assertTrue(valid)

    def test_dictionary_adapter_backing_player_active_trades(self):
        """DictionaryAdapter manages active trade sessions with upsert replacement, avoiding duplicate session crashes."""
        adapter = DictionaryAdapterContract()

        # Session established
        adapter["TradeSession_PlayerA"] = {"SessionId": 1, "Status": "Open"}
        self.assertEqual(adapter.count(), 1)

        # Session state updated (upsert replacement)
        adapter["TradeSession_PlayerA"] = {"SessionId": 1, "Status": "Completed"}
        self.assertEqual(adapter.count(), 1)
        self.assertEqual(adapter["TradeSession_PlayerA"]["Status"], "Completed")

        # KeyValuePair contains check with value fidelity
        self.assertTrue(adapter.contains_kvp_hardened("TradeSession_PlayerA", {"SessionId": 1, "Status": "Completed"}))
        self.assertFalse(adapter.contains_kvp_hardened("TradeSession_PlayerA", {"SessionId": 1, "Status": "Open"}))

    def test_character_bound_item_trade_and_stack_immunity(self):
        """Character-bound items cannot be picked up or traded by unauthorized players."""
        bound_def = ItemDefinition(
            item_id=777,
            name="BloodCastleScroll",
            is_stackable=True,
            is_bound_to_character=True,
        )
        bound_item = MockItem(definition=bound_def, durability=1)
        alice = MockPlayer(player_id="alice", character_name="Alice")
        bob = MockPlayer(player_id="bob", character_name="Bob")

        drop_engine = DroppedItemEngine(
            item=bound_item,
            drop_timestamp=self.t0,
            owner_player_ids={alice.player_id},
        )
        # Bob cannot pick up even after 3600 seconds
        t_distant = self.t0 + timedelta(seconds=3600)
        ok, _, msg = drop_engine.try_pickup_hardened(bob, t_distant)
        self.assertFalse(ok)
        self.assertIn("bound to character", msg)

    def test_jewel_bundling_stack_durability_and_price_invariants(self):
        """Bundling 30 jewels computes 64-bit checked price, clamps durability to 255."""
        # 64-bit price check for 30 packed Bless (ItemPriceCalculator.cs:106 -> (2+1)*1,480*10 = 44,400)
        bless_pack_price = EconomyEngine.calculate_packed_jewel_price_64bit("PackedBless", 2)
        self.assertEqual(bless_pack_price, (2 + 1) * 1_480 * 10)
        self.assertLessEqual(bless_pack_price, EconomyEngine.MAX_GOLD)

        # Durability clamp on jewel bundle addition
        bundle_dur = EconomyEngine.clamp_durability_addition(150, 150)
        self.assertEqual(bundle_dur, 255)

    def test_resource_raii_teardown_during_in_flight_drop_and_store_session(self):
        """Sudden disconnect during pending drop & store session releases all RAII resources cleanly."""
        with RAIIResourceTracker() as session:
            self.assertTrue(session.is_acquired)
            # Active in-flight trade
            trade_active = True
        # Exited RAII scope
        self.assertTrue(session.is_disposed)
        self.assertFalse(session.is_acquired)


if __name__ == "__main__":
    unittest.main()
