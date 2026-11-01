# tests/e2e/tier1_features/test_f07_letter_text_overflow.py
import unittest
from tests.e2e.harness.systemic_engine import LetterTextBufferValidator


class TestF07LetterTextOverflow(unittest.TestCase):
    """
    Feature 07: Letter Text Heap Overflow Clamp (WSclient.cpp).
    Verifies bounding memcpy size to sizeof(FS_LETTER_TEXT) to prevent heap overflow.
    """

    def test_valid_letter_size_copy(self):
        # 256 bytes payload
        copy_len = LetterTextBufferValidator.get_safe_copy_length(256)
        self.assertEqual(copy_len, 256)

    def test_max_fs_letter_text_capacity(self):
        # Full buffer size
        copy_len = LetterTextBufferValidator.get_safe_copy_length(LetterTextBufferValidator.SIZEOF_FS_LETTER_TEXT)
        self.assertEqual(copy_len, 1024)

    def test_oversized_letter_clamping(self):
        # 2048 bytes packet received -> clamped to 1024
        copy_len = LetterTextBufferValidator.get_safe_copy_length(2048)
        self.assertEqual(copy_len, 1024)

    def test_exact_boundary_copy(self):
        # 1024 bytes packet
        copy_len = LetterTextBufferValidator.get_safe_copy_length(1024)
        self.assertEqual(copy_len, 1024)

    def test_heap_memory_safety(self):
        # Malicious packet claiming 65535 bytes
        copy_len = LetterTextBufferValidator.get_safe_copy_length(65535)
        self.assertLessEqual(copy_len, LetterTextBufferValidator.SIZEOF_FS_LETTER_TEXT)


if __name__ == "__main__":
    unittest.main()
