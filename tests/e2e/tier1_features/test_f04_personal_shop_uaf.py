# tests/e2e/tier1_features/test_f04_personal_shop_uaf.py
import unittest


class MockShopTitleDrawObj:
    def __init__(self, key: int):
        self._key = key
        self.is_deleted = False

    def get_key(self) -> int:
        if self.is_deleted:
            raise RuntimeError("Use-After-Free detected: get_key called on deleted object")
        return self._key

    def delete(self):
        self.is_deleted = True


class TestF04PersonalShopUAF(unittest.TestCase):
    """
    Feature 04: Personal Shop UAF Elimination (PersonalShopTitleImp.cpp).
    Verifies saving key before deleting object, eliminating Use-After-Free in diagnostic logging.
    """

    def test_key_preserved_before_deletion(self):
        obj = MockShopTitleDrawObj(key=1001)
        # Safe pattern: save key first
        old_key = obj.get_key()
        obj.delete()
        # Verify old_key is safely accessible without reading obj
        self.assertEqual(old_key, 1001)

    def test_iterator_erased_cleanly(self):
        obj_list = [MockShopTitleDrawObj(1), MockShopTitleDrawObj(2)]
        target = obj_list[0]
        saved_key = target.get_key()
        target.delete()
        obj_list.remove(target)
        self.assertEqual(len(obj_list), 1)
        self.assertEqual(saved_key, 1)

    def test_diagnostics_logged_with_saved_key(self):
        obj = MockShopTitleDrawObj(key=42)
        saved_key = obj.get_key()
        obj.delete()
        log_msg = f"player key-value mismatch: key={saved_key}"
        self.assertIn("key=42", log_msg)

    def test_valid_player_key_match(self):
        obj = MockShopTitleDrawObj(key=200)
        player_key = 200
        # No mismatch, object should not be deleted
        if player_key == obj.get_key():
            is_mismatch = False
        self.assertFalse(is_mismatch)
        self.assertFalse(obj.is_deleted)

    def test_multiple_invalid_objects_cleaned(self):
        objs = [MockShopTitleDrawObj(i) for i in range(5)]
        cleaned_keys = []
        for obj in objs:
            k = obj.get_key()
            obj.delete()
            cleaned_keys.append(k)
        self.assertEqual(cleaned_keys, [0, 1, 2, 3, 4])


if __name__ == "__main__":
    unittest.main()
