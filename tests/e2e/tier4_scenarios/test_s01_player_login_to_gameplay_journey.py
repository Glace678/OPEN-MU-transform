# tests/e2e/tier4_scenarios/test_s01_player_login_to_gameplay_journey.py
import unittest
from tests.e2e.harness.packet_framing import PacketFramingEngine
from tests.e2e.harness.movement_engine import MovementEngine, Point


class TestS01PlayerLoginToGameplayJourney(unittest.TestCase):
    """
    Tier 4: Scenario 1 & 2.
    Scenario 1: Authenticated Client Ingress & Packet Stream Framing.
    Scenario 2: Initial Player World Entry & Safe Coordinate Placement.
    """

    def test_scenario_01_authenticated_client_ingress_and_packet_framing(self):
        # Step 1: Handshake and join server response (HeadCode 0xF1, SubCode 0x00, 12 bytes)
        join_payload = b"\x01\x00\x00\x00\x00\x00\x00\x00"
        join_pkt = PacketFramingEngine.build_c1_packet(head_code=0xF1, sub_code=0x00, payload=join_payload)
        self.assertEqual(len(join_pkt), 12)
        valid_join, err = PacketFramingEngine.validate_client_dispatch(join_pkt)
        self.assertTrue(valid_join)

        # Step 2: Login response (HeadCode 0xF1, SubCode 0x01, 8 bytes)
        login_payload = b"\x01\x00\x00\x00"
        login_pkt = PacketFramingEngine.build_c1_packet(head_code=0xF1, sub_code=0x01, payload=login_payload)
        valid_login, _ = PacketFramingEngine.validate_client_dispatch(login_pkt)
        self.assertTrue(valid_login)

        # Step 3: Character list packet (HeadCode 0xF3, SubCode 0x00, 6 bytes)
        char_list_pkt = PacketFramingEngine.build_c1_packet(head_code=0xF3, sub_code=0x00, payload=b"\x01\x00")
        valid_chars, _ = PacketFramingEngine.validate_client_dispatch(char_list_pkt)
        self.assertTrue(valid_chars)

    def test_scenario_02_initial_player_world_entry_and_safe_placement(self):
        # Step 1: Character spawns at Lorencia tavern coordinates (125, 125)
        engine = MovementEngine(start_x=125, start_y=125, map_id=0)
        self.assertEqual(engine.current_position, Point(125, 125))

        # Step 2: Move within tavern (125, 125) -> (126, 125)
        ok1, _, pos1 = engine.validate_and_move_step(126, 125, timestamp_ms=500)
        self.assertTrue(ok1)
        self.assertEqual(pos1, Point(126, 125))

        # Step 3: Move out towards door (126, 125) -> (128, 125) (Chebyshev distance 2)
        ok2, _, pos2 = engine.validate_and_move_step(128, 125, timestamp_ms=800)
        self.assertTrue(ok2)
        self.assertEqual(pos2, Point(128, 125))


if __name__ == "__main__":
    unittest.main()
