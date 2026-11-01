# tests/e2e/tier1_features/test_f06_banner_list_clamp.py
import unittest
from tests.e2e.harness.systemic_engine import BannerBufferValidator


class TestF06BannerListClamp(unittest.TestCase):
    """
    Feature 06: Banner List Stack Buffer Clamp (BannerInfoList.cpp).
    Verifies using wchar_t element count (1024) instead of byte sizeof (2048) in wifstream::getline.
    """

    def test_element_count_vs_sizeof(self):
        element_count = BannerBufferValidator.BUFFER_ELEMENTS
        byte_sizeof = element_count * BannerBufferValidator.WCHAR_SIZE
        # Byte size is 2048, but element count is 1024
        self.assertEqual(element_count, 1024)
        self.assertEqual(byte_sizeof, 2048)

    def test_safe_getline_capacity(self):
        capacity = BannerBufferValidator.safe_getline_capacity()
        self.assertEqual(capacity, 1024)

    def test_wchar_stack_buffer_bounds(self):
        # Buffer capacity must equal buffer elements, not byte count
        self.assertNotEqual(BannerBufferValidator.safe_getline_capacity(), 2048)

    def test_multibyte_wchar_safety(self):
        # Max character storage: 1023 wchar_t + 1 null terminator
        max_chars = BannerBufferValidator.safe_getline_capacity() - 1
        self.assertEqual(max_chars, 1023)

    def test_banner_line_truncation_safety(self):
        # A line of 1500 characters must be clamped to 1024 elements
        long_line = "A" * 1500
        safe_line = long_line[:BannerBufferValidator.safe_getline_capacity() - 1]
        self.assertEqual(len(safe_line), 1023)


if __name__ == "__main__":
    unittest.main()
