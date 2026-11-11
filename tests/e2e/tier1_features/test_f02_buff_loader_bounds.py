# tests/e2e/tier1_features/test_f02_buff_loader_bounds.py
import unittest
from tests.e2e.harness.systemic_engine import BuffScriptValidator


class TestF02BuffLoaderBounds(unittest.TestCase):
    """
    Feature 02: Client Buff Loader Bounds (w_BuffScriptLoader.cpp & BuffScriptValidation.h).
    Verifies listsize bounds, safe multiplication, and memory allocation integrity.
    """

    def test_valid_buff_count_acceptance(self):
        # Normal buff count (e.g., 250 buffs)
        self.assertTrue(BuffScriptValidator.is_valid_record_count(250, BuffScriptValidator.STRUCT_SIZE))

    def test_buff_script_validation_method_called(self):
        # Count within allowed threshold (10,000)
        self.assertTrue(BuffScriptValidator.is_valid_record_count(10000, BuffScriptValidator.STRUCT_SIZE))

    def test_structsize_calculation(self):
        # 1000 records * 128 bytes = 128,000 bytes (< 50MB)
        self.assertTrue(BuffScriptValidator.is_valid_record_count(1000, 128))

    def test_maximum_records_threshold(self):
        # Exactly at maximum allowed limit
        self.assertTrue(BuffScriptValidator.is_valid_record_count(BuffScriptValidator.MAXIMUM_BUFF_RECORDS, 128))

    def test_memory_allocation_safety(self):
        # Reasonable size payload under safe memory ceiling
        safe_count = 500
        safe_size = 128
        self.assertTrue(BuffScriptValidator.is_valid_record_count(safe_count, safe_size))

    def test_non_positive_and_excessive_record_counts_rejected(self):
        # Non-positive, >10,000 records, or >50 MB allocation payloads must be rejected
        self.assertFalse(BuffScriptValidator.is_valid_record_count(0, BuffScriptValidator.STRUCT_SIZE))
        self.assertFalse(BuffScriptValidator.is_valid_record_count(-1, BuffScriptValidator.STRUCT_SIZE))
        self.assertFalse(BuffScriptValidator.is_valid_record_count(10001, BuffScriptValidator.STRUCT_SIZE))
        self.assertFalse(BuffScriptValidator.is_valid_record_count(1000, 60 * 1024))


if __name__ == "__main__":
    unittest.main()
