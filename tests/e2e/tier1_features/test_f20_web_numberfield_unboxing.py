# tests/e2e/tier1_features/test_f20_web_numberfield_unboxing.py
import unittest
from tests.e2e.harness.systemic_engine import WebNumberFieldConverter


class TestF20WebNumberFieldUnboxing(unittest.TestCase):
    """
    Feature 20: Web NumberField Boxing Crash (NumberField.razor).
    Fixes short/byte unboxing InvalidCastException by using dynamic type-safe parsing.
    """

    def test_short_number_conversion(self):
        val = WebNumberFieldConverter.parse_number("32000", int)
        self.assertEqual(val, 32000)

    def test_byte_number_conversion(self):
        val = WebNumberFieldConverter.parse_number("200", int)
        self.assertEqual(val, 200)

    def test_int_number_conversion(self):
        val = WebNumberFieldConverter.parse_number("1000000", int)
        self.assertEqual(val, 1000000)

    def test_float_number_conversion(self):
        val = WebNumberFieldConverter.parse_number("12.5", float)
        self.assertEqual(val, 12.5)

    def test_invalid_string_handled_cleanly(self):
        val = WebNumberFieldConverter.parse_number("not_a_number", int)
        self.assertIsNone(val)


if __name__ == "__main__":
    unittest.main()
