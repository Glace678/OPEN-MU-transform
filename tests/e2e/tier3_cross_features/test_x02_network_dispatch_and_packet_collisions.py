# tests/e2e/tier3_cross_features/test_x02_network_dispatch_and_packet_collisions.py
import unittest
import struct
from tests.e2e.harness.packet_framing import PacketFramingEngine
from tests.e2e.harness.systemic_engine import PacketLayoutValidator, LetterTextBufferValidator


class TestX02NetworkDispatchAndPacketCollisions(unittest.TestCase):
    """
    Tier 3 Interaction: F01 Client Network Dispatch + F07 Letter Text Heap Protection + F21 Packet Collisions.
    Verifies network framing dispatch integration with field layout decoding and memory copy limits.
    """

    def test_money_dropped_packet_dispatch_and_parse(self):
        # Build 14-byte MoneyDropped C1 frame
        buf = bytearray(14)
        buf[0:3] = [0xC1, 14, 0x24]
        struct.pack_into(">I", buf, 10, 500000)

        # 1. Dispatch validation
        valid, err = PacketFramingEngine.validate_client_dispatch(bytes(buf))
        self.assertTrue(valid)
        self.assertIsNone(err)

        # 2. Field decoding
        amount = PacketLayoutValidator.parse_money_dropped(bytes(buf))
        self.assertEqual(amount, 500000)

    def test_truncated_money_dropped_dispatch_rejection(self):
        # 12-byte truncated MoneyDropped frame (missing 2 bytes of amount)
        short_buf = bytes([0xC1, 12, 0x24] + [0] * 9)
        valid, err = PacketFramingEngine.validate_client_dispatch(short_buf)
        self.assertFalse(valid)
        self.assertIn("less than required 14 bytes", err)

    def test_letter_text_packet_dispatch_and_bounded_copy(self):
        # Valid letter frame (HeadCode 0x26, 32 bytes)
        letter_buf = PacketFramingEngine.build_c1_packet(head_code=0x26, payload=b"A" * 29)
        valid, _ = PacketFramingEngine.validate_client_dispatch(letter_buf)
        self.assertTrue(valid)

        # Safe copy length
        safe_len = LetterTextBufferValidator.get_safe_copy_length(len(letter_buf))
        self.assertEqual(safe_len, 32)

    def test_oversized_letter_dispatch_and_clamping(self):
        # Huge letter frame with 2000 bytes
        huge_payload = b"B" * 2000
        c2_letter = PacketFramingEngine.build_c2_packet(head_code=0x26, payload=huge_payload)
        valid, _ = PacketFramingEngine.validate_client_dispatch(c2_letter)
        self.assertTrue(valid)

        # Memory copy into FS_LETTER_TEXT is clamped to 1024
        safe_len = LetterTextBufferValidator.get_safe_copy_length(len(c2_letter))
        self.assertEqual(safe_len, 1024)

    def test_illusion_temple_packet_dispatch_and_layout(self):
        # IllusionTempleState (HeadCode 0x9E, 10 bytes)
        payload = b"\x00" * 7
        pkt = PacketFramingEngine.build_c1_packet(head_code=0x9E, payload=payload)
        valid, _ = PacketFramingEngine.validate_client_dispatch(pkt)
        self.assertTrue(valid)
        self.assertTrue(PacketLayoutValidator.validate_illusion_temple_state_offsets(4, 6))

    def test_packet_stream_multiplexing(self):
        # Sequence of distinct packets in single network stream
        pkt1 = PacketFramingEngine.build_c1_packet(head_code=0x24, payload=b"\x00" * 11)  # Money
        pkt2 = PacketFramingEngine.build_c1_packet(head_code=0x9E, payload=b"\x00" * 7)   # Illusion
        pkt3 = PacketFramingEngine.build_c1_packet(head_code=0xF1, sub_code=0x00, payload=b"\x00" * 8) # Join

        for p in (pkt1, pkt2, pkt3):
            valid, err = PacketFramingEngine.validate_client_dispatch(p)
            self.assertTrue(valid, f"Failed for packet {p[2]}: {err}")


if __name__ == "__main__":
    unittest.main()
