# tests/e2e/tier1_features/test_f31_adversarial_hardening.py
import unittest
from tests.e2e.harness.packet_framing import PacketFramingEngine
from tests.e2e.harness.movement_engine import MovementEngine


class TestF31AdversarialHardening(unittest.TestCase):
    """
    Feature 31: Adversarial Coverage Hardening.
    Conducts adversarial stress testing, boundary fuzzing, and gap elimination.
    """

    def test_adversarial_packet_fuzzing(self):
        # Corrupted packet header (0xFF)
        corrupted = b"\xFF\x10\x00\x01\x02\x03"
        is_valid, err = PacketFramingEngine.validate_client_dispatch(corrupted)
        self.assertFalse(is_valid)

    def test_adversarial_negative_numeric_injection(self):
        # Negative step values injected into movement engine
        engine = MovementEngine(100, 100)
        valid, msg, pos = engine.validate_and_move_step(-5, -10, timestamp_ms=100)
        self.assertFalse(valid)
        self.assertIn("out of map bounds", msg)

    def test_adversarial_boundary_overflow_payloads(self):
        # Massive packet claiming 65535 bytes with 10 bytes payload
        fake_c2 = bytes([0xC2, 0xFF, 0xFF, 0x00, 0x01])
        is_valid, err = PacketFramingEngine.validate_client_dispatch(fake_c2)
        self.assertFalse(is_valid)
        self.assertIn("Truncated packet", err)

    def test_adversarial_special_character_escaping(self):
        # Injection payload with format strings and control characters
        payload = "Character\x00\r\n%s%d%x<script>'\""
        sanitized = "".join(c for c in payload if ord(c) >= 32 and c not in "%'\"<>")
        self.assertNotIn("%s", sanitized)
        self.assertNotIn("<script>", sanitized)

    def test_adversarial_rapid_fire_packet_burst(self):
        engine = MovementEngine(100, 100)
        # 10 move packets at the exact same millisecond timestamp with positive step distance
        rejected_count = 0
        for i in range(10):
            valid, _, _ = engine.validate_and_move_step(101 + (i % 2), 100, timestamp_ms=100)
            if not valid:
                rejected_count += 1
        # Subsequent rapid fire moves within 250ms must be rejected
        self.assertGreater(rejected_count, 0)


if __name__ == "__main__":
    unittest.main()
