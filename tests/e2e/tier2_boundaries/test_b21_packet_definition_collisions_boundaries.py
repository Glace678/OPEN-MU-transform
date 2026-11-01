# tests/e2e/tier2_boundaries/test_b21_packet_definition_collisions_boundaries.py
import unittest
import struct
from tests.e2e.harness.systemic_engine import PacketLayoutValidator


class TestB21PacketDefinitionCollisionsBoundaries(unittest.TestCase):
    """
    Feature 21 Boundaries: Packet Definition Collisions.
    Tests boundary buffer lengths for MoneyDropped (13 rejected, 14 accepted), amount 0, 0xFFFFFFFF, and offset overlaps.
    """

    def test_money_dropped_exact_13_bytes_rejected(self):
        # 13 bytes is buffer required length (14) - 1
        buf = bytes([0xC1, 13, 0x24] + [0] * 10)
        amount = PacketLayoutValidator.parse_money_dropped(buf)
        self.assertIsNone(amount)

    def test_money_dropped_zero_amount(self):
        buf = bytearray(14)
        buf[0:3] = [0xC1, 14, 0x24]
        struct.pack_into(">I", buf, 10, 0)
        self.assertEqual(PacketLayoutValidator.parse_money_dropped(bytes(buf)), 0)

    def test_money_dropped_max_uint32_amount(self):
        buf = bytearray(14)
        buf[0:3] = [0xC1, 14, 0x24]
        struct.pack_into(">I", buf, 10, 0xFFFFFFFF)
        self.assertEqual(PacketLayoutValidator.parse_money_dropped(bytes(buf)), 0xFFFFFFFF)

    def test_illusion_temple_identical_offset_collision(self):
        # Both fields at offset 5 must collide
        self.assertFalse(PacketLayoutValidator.validate_illusion_temple_state_offsets(5, 5))

    def test_illusion_temple_adjacent_offsets_valid(self):
        # Offsets 4 and 6 are valid and do not collide
        self.assertTrue(PacketLayoutValidator.validate_illusion_temple_state_offsets(4, 6))


if __name__ == "__main__":
    unittest.main()
