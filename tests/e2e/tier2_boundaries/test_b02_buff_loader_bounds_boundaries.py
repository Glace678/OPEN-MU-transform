# tests/e2e/tier2_boundaries/test_b02_buff_loader_bounds_boundaries.py
import unittest
from tests.e2e.harness.systemic_engine import BuffScriptValidator


class TestB02BuffLoaderBoundsBoundaries(unittest.TestCase):
    """
    Feature 02 Boundaries: Client Buff Loader Bounds.
    Tests listsize = 0, negative values, 32-bit integer overflow values (0x40000000), and max limit + 1.
    """

    def test_zero_listsize_rejected(self):
        self.assertFalse(BuffScriptValidator.is_valid_record_count(0, BuffScriptValidator.STRUCT_SIZE))

    def test_negative_listsize_rejected(self):
        self.assertFalse(BuffScriptValidator.is_valid_record_count(-1, BuffScriptValidator.STRUCT_SIZE))

    def test_32bit_multiplication_overflow_listsize_rejected(self):
        # 0x40000000 (1073741824) in 32-bit * 128 wraps around to 0
        overflow_listsize = 0x40000000
        self.assertFalse(BuffScriptValidator.is_valid_record_count(overflow_listsize, BuffScriptValidator.STRUCT_SIZE))

    def test_exceeded_maximum_record_count_rejected(self):
        # Exactly 10,001 (MAXIMUM_BUFF_RECORDS + 1)
        self.assertFalse(BuffScriptValidator.is_valid_record_count(BuffScriptValidator.MAXIMUM_BUFF_RECORDS + 1, 128))

    def test_extreme_structsize_rejected(self):
        # Even if listsize is 10, if structsize is 10MB, total size exceeds 50MB
        extreme_structsize = 10 * 1024 * 1024
        self.assertFalse(BuffScriptValidator.is_valid_record_count(10, extreme_structsize))


if __name__ == "__main__":
    unittest.main()
