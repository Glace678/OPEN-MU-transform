# tests/e2e/tier1_features/test_f01_client_network_dispatch.py
import unittest
from tests.e2e.harness.packet_framing import PacketFramingEngine


class TestF01ClientNetworkDispatch(unittest.TestCase):
    """
    Feature 01: Client Network Dispatch Guard (WSclient.cpp).
    Verifies minimum packet size, headcode table verification, and valid packet routing.
    """

    def test_valid_c1_packet_dispatch(self):
        # C1 packet with JoinServer (0xF1, 0x00) with full 12-byte payload
        payload = b"\x00" * 8
        pkt = PacketFramingEngine.build_c1_packet(head_code=0xF1, sub_code=0x00, payload=payload)
        is_valid, err = PacketFramingEngine.validate_client_dispatch(pkt)
        self.assertTrue(is_valid)
        self.assertIsNone(err)

    def test_valid_c2_packet_dispatch(self):
        # C2 packet with DevilSquareScore (0x86) with 12 bytes
        payload = b"\x01" * 8
        pkt = PacketFramingEngine.build_c2_packet(head_code=0x86, payload=payload)
        is_valid, err = PacketFramingEngine.validate_client_dispatch(pkt)
        self.assertTrue(is_valid)
        self.assertIsNone(err)

    def test_headcode_subcode_matching(self):
        # CharacterCreateResponse (0xF3, 0x03) requires 32 bytes
        payload = b"\xAA" * 28
        pkt = PacketFramingEngine.build_c1_packet(head_code=0xF3, sub_code=0x03, payload=payload)
        is_valid, err = PacketFramingEngine.validate_client_dispatch(pkt)
        self.assertTrue(is_valid)

    def test_expected_minimum_size_compliance(self):
        # MoneyDropped (0x24) requires 14 bytes
        payload = b"\x00" * 11
        pkt = PacketFramingEngine.build_c1_packet(head_code=0x24, payload=payload)
        self.assertEqual(len(pkt), 14)
        is_valid, err = PacketFramingEngine.validate_client_dispatch(pkt)
        self.assertTrue(is_valid)

    def test_diagnostic_logging_on_rejection(self):
        # Truncated packet triggers diagnostic rejection
        raw = bytes([0xC1, 0x03, 0xF1])  # Only 3 bytes, requires 12
        is_valid, err = PacketFramingEngine.validate_client_dispatch(raw)
        self.assertFalse(is_valid)
        self.assertIsNotNone(err)
        self.assertIn("less than required", err)


if __name__ == "__main__":
    unittest.main()
