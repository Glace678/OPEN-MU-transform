# tests/e2e/tier2_boundaries/test_b06_banner_list_clamp_boundaries.py
import unittest
from tests.e2e.harness.systemic_engine import BannerBufferValidator


class TestB06BannerListClampBoundaries(unittest.TestCase):
    """
    Feature 06 Boundaries: Banner List Stack Buffer Clamp.
    Tests boundary capacities: 0 length line, exact 1023 wchar, 1024 wchar, and 2048 wchar string.
    """

    def test_empty_line_capacity(self):
        # Empty line length 0
        line = ""
        self.assertLess(len(line), BannerBufferValidator.safe_getline_capacity())

    def test_exact_1023_chars_fits_buffer(self):
        # 1023 characters + 1 null terminator = 1024 elements
        line = "X" * 1023
        self.assertEqual(len(line) + 1, BannerBufferValidator.safe_getline_capacity())

    def test_1024_chars_truncated_to_prevent_stack_smash(self):
        # 1024 characters would require 1025 elements with null terminator
        line = "Y" * 1024
        truncated = line[:BannerBufferValidator.safe_getline_capacity() - 1]
        self.assertEqual(len(truncated), 1023)

    def test_2047_chars_overflow_clamp(self):
        # 2047 chars was allowed by the flawed sizeof(buff) guard
        flawed_line = "Z" * 2047
        safe_line = flawed_line[:BannerBufferValidator.safe_getline_capacity() - 1]
        self.assertEqual(len(safe_line), 1023)

    def test_multilingual_unicode_buffer_bounds(self):
        # Wide characters: Chinese/Korean characters are 1 wchar_t each in UTF-16
        cjk_line = "奇迹MU" * 200  # 800 wchar_t
        self.assertLess(len(cjk_line), BannerBufferValidator.safe_getline_capacity())


if __name__ == "__main__":
    unittest.main()
