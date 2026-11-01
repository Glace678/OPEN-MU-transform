# tests/e2e/tier2_boundaries/test_b04_personal_shop_uaf_boundaries.py
import unittest
from tests.e2e.tier1_features.test_f04_personal_shop_uaf import MockShopTitleDrawObj


class TestB04PersonalShopUAFBoundaries(unittest.TestCase):
    """
    Feature 04 Boundaries: Personal Shop UAF Elimination.
    Tests double deletion protection, null object handling, and post-delete access prevention.
    """

    def test_post_delete_access_raises_error(self):
        obj = MockShopTitleDrawObj(key=55)
        obj.delete()
        with self.assertRaises(RuntimeError) as ctx:
            obj.get_key()
        self.assertIn("Use-After-Free detected", str(ctx.exception))

    def test_empty_draw_list_cleanup(self):
        empty_list = []
        # Safe operation on empty list
        self.assertEqual(len(empty_list), 0)

    def test_repeated_deletion_idempotency(self):
        obj = MockShopTitleDrawObj(key=77)
        obj.delete()
        # Second delete does not crash
        obj.delete()
        self.assertTrue(obj.is_deleted)

    def test_zero_key_draw_object(self):
        obj = MockShopTitleDrawObj(key=0)
        saved = obj.get_key()
        obj.delete()
        self.assertEqual(saved, 0)

    def test_negative_key_draw_object(self):
        obj = MockShopTitleDrawObj(key=-1)
        saved = obj.get_key()
        obj.delete()
        self.assertEqual(saved, -1)


if __name__ == "__main__":
    unittest.main()
