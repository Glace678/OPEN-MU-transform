# tests/e2e/tier2_boundaries/test_b07_letter_text_overflow_boundaries.py
import unittest
from tests.e2e.harness.systemic_engine import LetterTextBufferValidator


class TestB07LetterTextOverflowBoundaries(unittest.TestCase):
    """
    Feature 07 Boundaries: Letter Text Heap Overflow Clamp.
    Tests boundary buffer lengths: 0 bytes, 1 byte, 1024 bytes, 1025 bytes, and 65535 bytes.
    """

    def test_zero_byte_receive_buffer(self):
        self.assertEqual(LetterTextBufferValidator.get_safe_copy_length(0), 0)

    def test_single_byte_receive_buffer(self):
        self.assertEqual(LetterTextBufferValidator.get_safe_copy_length(1), 1)

    def test_exact_1024_bytes_boundary(self):
        self.assertEqual(LetterTextBufferValidator.get_safe_copy_length(1024), 1024)

    def test_1025_bytes_clamped_to_1024(self):
        # 1025 is capacity + 1
        self.assertEqual(LetterTextBufferValidator.get_safe_copy_length(1025), 1024)

    def test_max_unsigned_16bit_size_clamped(self):
        # 65535 is maximum C2 packet size
        self.assertEqual(LetterTextBufferValidator.get_safe_copy_length(65535), 1024)


if __name__ == "__main__":
    unittest.main()
