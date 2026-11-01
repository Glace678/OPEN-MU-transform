# tests/e2e/tier2_boundaries/test_b31_adversarial_hardening_boundaries.py
import unittest
from tests.e2e.harness.packet_framing import PacketFramingEngine
from tests.e2e.harness.movement_engine import MovementEngine


class TestB31AdversarialHardeningBoundaries(unittest.TestCase):
    """
    Feature 31 Boundaries: Adversarial Coverage Hardening.
    Tests boundary adversarial attacks: zero-byte packets, format string injection, coordinate INT_MAX, and corrupted headers.
    """

    def test_null_byte_packet_injection(self):
        is_valid, err = PacketFramingEngine.validate_client_dispatch(b"\x00\x00\x00\x00")
        self.assertFalse(is_valid)

    def test_format_string_attack_in_chat_packet(self):
        # Malicious chat packet containing %n %s %d %x
        chat_payload = b"%s%n%x%d%08x"
        pkt = PacketFramingEngine.build_c1_packet(head_code=0x00, payload=chat_payload)
        is_valid, err = PacketFramingEngine.validate_client_dispatch(pkt)
        self.assertTrue(is_valid)  # Valid frame structure, content safely carried without format string execution

    def test_teleport_across_int_max_coordinates(self):
        engine = MovementEngine(100, 100)
        # 2147483647
        valid, msg, pos = engine.validate_and_move_step(2147483647, 2147483647, timestamp_ms=500)
        self.assertFalse(valid)
        self.assertIn("out of map bounds", msg)

    def test_negative_int_min_coordinates(self):
        engine = MovementEngine(100, 100)
        valid, msg, pos = engine.validate_and_move_step(-2147483648, -2147483648, timestamp_ms=500)
        self.assertFalse(valid)
        self.assertIn("out of map bounds", msg)

    def test_corrupted_c1_packet_length_overflow(self):
        # C1 header with declared length 255 but only 3 bytes sent
        fake_pkt = bytes([0xC1, 0xFF, 0x00])
        is_valid, err = PacketFramingEngine.validate_client_dispatch(fake_pkt)
        self.assertFalse(is_valid)
        self.assertIn("Truncated", err)


if __name__ == "__main__":
    unittest.main()
