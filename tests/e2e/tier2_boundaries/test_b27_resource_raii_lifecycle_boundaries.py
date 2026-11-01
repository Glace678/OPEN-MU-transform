# tests/e2e/tier2_boundaries/test_b27_resource_raii_lifecycle_boundaries.py
import unittest
from tests.e2e.harness.systemic_engine import RAIIResourceTracker


class TestB27ResourceRAIILifecycleBoundaries(unittest.TestCase):
    """
    Feature 27 Boundaries: Resource Leaks & RAII Lifecycle.
    Tests boundary resource states: pre-mature close, closed resource queries, multi-resource cleanup order.
    """

    def test_close_before_entering_context(self):
        res = RAIIResourceTracker()
        res.close()
        self.assertTrue(res.is_disposed)
        with res:
            pass
        self.assertTrue(res.is_disposed)

    def test_triple_close_safety(self):
        res = RAIIResourceTracker()
        res.close()
        res.close()
        res.close()
        self.assertTrue(res.is_disposed)

    def test_multi_resource_lifecycles(self):
        resources = [RAIIResourceTracker() for _ in range(50)]
        for r in resources:
            r.close()
        self.assertTrue(all(r.is_disposed for r in resources))
        self.assertTrue(all(not r.is_acquired for r in resources))

    def test_exception_in_resource_body(self):
        r = RAIIResourceTracker()
        try:
            with r:
                raise ValueError("Crash inside using block")
        except ValueError:
            pass
        self.assertTrue(r.is_disposed)

    def test_unacquired_resource_default_state(self):
        r = RAIIResourceTracker()
        self.assertFalse(r.is_disposed)
        r.close()
        self.assertTrue(r.is_disposed)


if __name__ == "__main__":
    unittest.main()
