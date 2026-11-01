# tests/e2e/tier1_features/test_f21_packet_definition_collisions.py
import unittest
import struct
from tests.e2e.harness.systemic_engine import PacketLayoutValidator


class TestF21PacketDefinitionCollisions(unittest.TestCase):
    """
    Feature 21: Packet Definition Collisions (IllusionTempleState & MoneyDropped).
    Fixes IllusionTempleState RemainingSeconds collision and MoneyDropped byte 12 skipping.
    """

    def test_money_dropped_full_4byte_amount(self):
        # Build MoneyDropped buffer with amount 0x12345678 at bytes 10..13
        buf = bytearray(14)
        buf[0:3] = [0xC1, 14, 0x24]
        struct.pack_into(">I", buf, 10, 0x12345678)

        amount = PacketLayoutValidator.parse_money_dropped(bytes(buf))
        self.assertEqual(amount, 0x12345678)

    def test_money_dropped_minimum_buffer_size(self):
        # Less than 14 bytes must be rejected
        short_buf = b"\xC1\x0A\x24\x00\x00\x00\x00\x00\x00\x00"
        amount = PacketLayoutValidator.parse_money_dropped(short_buf)
        self.assertIsNone(amount)

    def test_illusion_temple_state_offsets_non_overlapping(self):
        # RemainingSeconds at offset 4 (2 bytes), PlayerIndex at offset 6 (2 bytes)
        remaining_sec_offset = 4
        player_idx_offset = 6
        self.assertTrue(PacketLayoutValidator.validate_illusion_temple_state_offsets(remaining_sec_offset, player_idx_offset))

    def test_money_dropped_big_endian_accuracy(self):
        buf = bytearray(14)
        struct.pack_into(">I", buf, 10, 100000)
        self.assertEqual(PacketLayoutValidator.parse_money_dropped(bytes(buf)), 100000)

    def test_packet_layout_collision_elimination(self):
        # Offset 4 and offset 4 would collide
        self.assertFalse(PacketLayoutValidator.validate_illusion_temple_state_offsets(4, 4))


if __name__ == "__main__":
    unittest.main()
