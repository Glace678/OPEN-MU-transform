# tests/e2e/tier1_features/test_f27_resource_raii_lifecycle.py
import unittest
from tests.e2e.harness.systemic_engine import RAIIResourceTracker


class TestF27ResourceRAIILifecycle(unittest.TestCase):
    """
    Feature 27: Resource Leaks & RAII Lifecycle.
    Ensures IDisposable using blocks and C++ RAII resource wrappers prevent resource leaks.
    """

    def test_raii_automatic_cleanup_with_context(self):
        with RAIIResourceTracker() as res:
            self.assertTrue(res.is_acquired)
            self.assertFalse(res.is_disposed)
        # Exited context
        self.assertTrue(res.is_disposed)
        self.assertFalse(res.is_acquired)

    def test_explicit_close_idempotency(self):
        res = RAIIResourceTracker()
        res.close()
        self.assertTrue(res.is_disposed)
        # Calling close again must be safe and idempotent
        res.close()
        self.assertTrue(res.is_disposed)

    def test_cleanup_upon_exception(self):
        res_ref = None
        try:
            with RAIIResourceTracker() as res:
                res_ref = res
                raise RuntimeError("Unexpected socket abort")
        except RuntimeError:
            pass
        self.assertIsNotNone(res_ref)
        self.assertTrue(res_ref.is_disposed)

    def test_resource_acquisition_state(self):
        res = RAIIResourceTracker()
        self.assertTrue(res.is_acquired)
        res.close()

    def test_nested_raii_scopes(self):
        with RAIIResourceTracker() as outer:
            with RAIIResourceTracker() as inner:
                self.assertTrue(outer.is_acquired)
                self.assertTrue(inner.is_acquired)
            self.assertTrue(inner.is_disposed)
            self.assertTrue(outer.is_acquired)
        self.assertTrue(outer.is_disposed)


if __name__ == "__main__":
    unittest.main()
