# tests/e2e/tier4_scenarios/test_s10_client_packet_dispatch_and_theft_defense.py
import unittest
from datetime import datetime, timedelta, timezone
from tests.e2e.harness.packet_framing import PacketFramingEngine
from tests.e2e.harness.regression_models import (
    HeroState,
    PlayerCharacterState,
    PlayerRegenerationEngine,
    ItemDefinition,
    MockItem,
    MockPlayer,
    DroppedItemEngine,
)


class TestS10ClientPacketDispatchAndTheftDefense(unittest.TestCase):
    """
    Tier 4: Scenario 11 & 12.
    Scenario 11: End-to-End Drop Protection under Adversarial Stacking Flood.
    Scenario 12: Complete PK & Hero State Lifecycle Progression.
    """

    def setUp(self):
        self.t0 = datetime(2026, 11, 1, 12, 0, 0, tzinfo=timezone.utc)

    def test_scenario_11_end_to_end_drop_protection_under_adversarial_flood(self):
        """
        Adversarial drop protection scenario:
        1. Player Alice defeats Dungeon Boss; rare stackable Jewel drops (10s protection).
        2. Adversary Bob detects drop, floods 50 pickup requests attempting stacking bypass.
        3. Server rejects all 50 unauthorized attempts.
        4. At t = 5s, Alice sends pickup request; server completes stack and clamps durability.
        5. Alice disconnects cleanly.
        """
        rare_jewel_def = ItemDefinition(
            item_id=888,
            name="JewelOfChaos",
            is_stackable=True,
            is_bound_to_character=False,
            max_durability=255,
        )
        ground_drop = MockItem(definition=rare_jewel_def, durability=10)
        alice = MockPlayer(player_id="alice_champion", character_name="Alice")
        bob = MockPlayer(player_id="bob_adversary", character_name="Bob")

        # Bob pre-stacks 90 Chaos jewels in inventory
        bob.inventory.append(MockItem(definition=rare_jewel_def, durability=90))

        # Alice has 200 Chaos jewels in inventory
        alice.inventory.append(MockItem(definition=rare_jewel_def, durability=200))

        drop_engine = DroppedItemEngine(
            item=ground_drop,
            drop_timestamp=self.t0,
            owner_player_ids={alice.player_id},
        )

        # Bob floods 50 C3 pickup packets between t = 0.1s and t = 4.9s
        for i in range(50):
            t_flood = self.t0 + timedelta(milliseconds=100 + i * 80)
            # Bob builds framed pickup packet
            pkt = PacketFramingEngine.build_c3_packet(head_code=0x22, sub_code=None, payload=b"\x03\x78\x00\x00\x00\x00")
            valid, _ = PacketFramingEngine.validate_client_dispatch(pkt)
            self.assertTrue(valid)

            # Server rejects theft attempt
            success, _, msg = drop_engine.try_pickup_hardened(bob, t_flood)
            self.assertFalse(success)
            self.assertEqual(bob.inventory[0].durability, 90)  # Bob got nothing

        # At t = 5.0s, Alice legitimately picks up her drop
        t_alice = self.t0 + timedelta(seconds=5)
        alice_pkt = PacketFramingEngine.build_c3_packet(head_code=0x22, sub_code=None, payload=b"\x03\x78\x00\x00\x00\x00")
        valid_alice, _ = PacketFramingEngine.validate_client_dispatch(alice_pkt)
        self.assertTrue(valid_alice)

        alice_success, alice_target, _ = drop_engine.try_pickup_hardened(alice, t_alice)
        self.assertTrue(alice_success)
        self.assertEqual(alice_target.durability, 210)  # 200 + 10
        self.assertFalse(drop_engine.available_to_pick)

    def test_scenario_12_pk_hero_lifecycle_combat_and_peace_redemption(self):
        """
        Complete PK & Hero State Lifecycle:
        1. Player enters Outlaw state (PlayerKiller1) with 600s penalty.
        2. Countdown decrements monotonically across 6 sequential ticks.
        3. Status smoothly transitions to Normal (0).
        4. Player performs heroic deeds, gains Hero1 status with 300s countdown.
        5. Hero countdown regenerates to Normal.
        """
        player_char = PlayerCharacterState(
            name="Warrior_Journey",
            state=HeroState.PlayerKiller1,
            state_remaining_seconds=600,
            last_regenerate_utc=self.t0,
        )

        # 6 regeneration ticks of 100 seconds
        for step in range(1, 7):
            t_step = self.t0 + timedelta(seconds=step * 100)
            remaining = PlayerRegenerationEngine.regenerate_hardened(player_char, t_step)
            expected = max(0, 600 - step * 100)
            self.assertEqual(remaining, expected)

        # Transitions to Normal
        self.assertEqual(player_char.state, HeroState.Normal)
        self.assertEqual(player_char.state_remaining_seconds, 0)

        # Player achieves Hero1 status with 300s duration
        player_char.state = HeroState.Hero1
        player_char.state_remaining_seconds = 300
        t_hero_start = self.t0 + timedelta(seconds=700)
        player_char.last_regenerate_utc = t_hero_start

        # Hero countdown expires at t = +1000s
        t_hero_end = t_hero_start + timedelta(seconds=300)
        final_rem = PlayerRegenerationEngine.regenerate_hardened(player_char, t_hero_end)
        self.assertEqual(final_rem, 0)
        self.assertEqual(player_char.state, HeroState.Normal)


if __name__ == "__main__":
    unittest.main()
