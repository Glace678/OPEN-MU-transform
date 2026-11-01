# tests/e2e/tier2_boundaries/test_b01_client_network_dispatch_boundaries.py
import unittest
from tests.e2e.harness.packet_framing import PacketFramingEngine


class TestB01ClientNetworkDispatchBoundaries(unittest.TestCase):
    """
    Feature 01 Boundaries: Client Network Dispatch Guard.
    Tests extreme packet boundaries, empty buffers, truncated frames, and invalid headers.
    """

    def test_empty_buffer_rejected(self):
        is_valid, err = PacketFramingEngine.validate_client_dispatch(b"")
        self.assertFalse(is_valid)
        self.assertIn("empty", err.lower())

    def test_single_byte_packet_rejected(self):
        is_valid, err = PacketFramingEngine.validate_client_dispatch(b"\xC1")
        self.assertFalse(is_valid)

    def test_truncated_c2_header_rejected(self):
        # C2 requires at least 3 bytes for header
        is_valid, err = PacketFramingEngine.validate_client_dispatch(b"\xC2\x00")
        self.assertFalse(is_valid)

    def test_declared_size_exceeding_actual_buffer_rejected(self):
        # Header declares size 50, but buffer is only 4 bytes
        fake_c1 = b"\xC1\x32\x00\x00"
        is_valid, err = PacketFramingEngine.validate_client_dispatch(fake_c1)
        self.assertFalse(is_valid)
        self.assertIn("Truncated", err)

    def test_invalid_header_byte_rejected(self):
        # 0xAA is not a valid MU packet header
        is_valid, err = PacketFramingEngine.validate_client_dispatch(b"\xAA\x04\x00\x00")
        self.assertFalse(is_valid)
        self.assertIn("Invalid packet header", err)


if __name__ == "__main__":
    unittest.main()
