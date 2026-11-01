# tests/e2e/tier2_boundaries/test_b20_web_numberfield_unboxing_boundaries.py
import unittest
from tests.e2e.harness.systemic_engine import WebNumberFieldConverter


class TestB20WebNumberFieldUnboxingBoundaries(unittest.TestCase):
    """
    Feature 20 Boundaries: Web NumberField Boxing Crash.
    Tests boundary string inputs: -32768 (short.MinValue), 32767 (short.MaxValue), 255 (byte.MaxValue), empty, whitespace.
    """

    def test_short_min_value_string(self):
        val = WebNumberFieldConverter.parse_number("-32768", int)
        self.assertEqual(val, -32768)

    def test_short_max_value_string(self):
        val = WebNumberFieldConverter.parse_number("32767", int)
        self.assertEqual(val, 32767)

    def test_byte_max_value_string(self):
        val = WebNumberFieldConverter.parse_number("255", int)
        self.assertEqual(val, 255)

    def test_empty_string_returns_none(self):
        val = WebNumberFieldConverter.parse_number("", int)
        self.assertIsNone(val)

    def test_whitespace_string_returns_none(self):
        val = WebNumberFieldConverter.parse_number("   ", int)
        self.assertIsNone(val)


if __name__ == "__main__":
    unittest.main()
