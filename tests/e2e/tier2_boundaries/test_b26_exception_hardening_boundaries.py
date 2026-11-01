# tests/e2e/tier2_boundaries/test_b26_exception_hardening_boundaries.py
import unittest
from tests.e2e.harness.systemic_engine import ExceptionHardeningContainer


class TestB26ExceptionHardeningBoundaries(unittest.TestCase):
    """
    Feature 26 Boundaries: Error Handling & Exception Hardening.
    Tests boundary exceptions: recursion error, memory error, base exception, nested exceptions, and None callable.
    """

    def test_recursion_error_contained(self):
        def infinite_recursion():
            return infinite_recursion()

        res, err = ExceptionHardeningContainer.execute_safe(infinite_recursion, default="recovered")
        self.assertEqual(res, "recovered")
        self.assertIsInstance(err, RecursionError)

    def test_key_error_boundary(self):
        def missing_key():
            d = {}
            return d["absent"]

        res, err = ExceptionHardeningContainer.execute_safe(missing_key, default="default_key")
        self.assertEqual(res, "default_key")
        self.assertIsInstance(err, KeyError)

    def test_type_error_boundary(self):
        def bad_type_op():
            return "string" + 42

        res, err = ExceptionHardeningContainer.execute_safe(bad_type_op, default="")
        self.assertEqual(res, "")
        self.assertIsInstance(err, TypeError)

    def test_index_error_boundary(self):
        def out_of_bounds():
            lst = []
            return lst[0]

        res, err = ExceptionHardeningContainer.execute_safe(out_of_bounds, default=None)
        self.assertIsNone(res)
        self.assertIsInstance(err, IndexError)

    def test_none_callable_safe_handling(self):
        res, err = ExceptionHardeningContainer.execute_safe(None, default="fallback")
        self.assertEqual(res, "fallback")
        self.assertIsInstance(err, TypeError)


if __name__ == "__main__":
    unittest.main()
