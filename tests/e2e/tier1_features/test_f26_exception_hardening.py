# tests/e2e/tier1_features/test_f26_exception_hardening.py
import unittest
from tests.e2e.harness.systemic_engine import ExceptionHardeningContainer


class TestF26ExceptionHardening(unittest.TestCase):
    """
    Feature 26: Error Handling & Exception Hardening.
    Verifies elimination of swallowed exceptions, unobserved task faults, and crash prevention.
    """

    def test_safe_execution_captures_exceptions(self):
        def faulty_action():
            raise ValueError("Malformed packet parameter")

        res, err = ExceptionHardeningContainer.execute_safe(faulty_action, default=-1)
        self.assertEqual(res, -1)
        self.assertIsInstance(err, ValueError)
        self.assertEqual(str(err), "Malformed packet parameter")

    def test_default_value_on_exception(self):
        def divide_by_zero():
            return 10 / 0

        res, err = ExceptionHardeningContainer.execute_safe(divide_by_zero, default=0)
        self.assertEqual(res, 0)
        self.assertIsInstance(err, ZeroDivisionError)

    def test_exception_chaining_integrity(self):
        def network_timeout():
            raise TimeoutError("Connection timed out after 3000ms")

        res, err = ExceptionHardeningContainer.execute_safe(network_timeout)
        self.assertIsInstance(err, TimeoutError)

    def test_successful_action_execution(self):
        def healthy_action():
            return 42

        res, err = ExceptionHardeningContainer.execute_safe(healthy_action)
        self.assertEqual(res, 42)
        self.assertIsNone(err)

    def test_unhandled_crash_prevention(self):
        # Multiple faulty executions in sequence must not abort testing pipeline
        results = []
        for i in range(5):
            res, _ = ExceptionHardeningContainer.execute_safe(lambda: 1 / 0, default=-i)
            results.append(res)
        self.assertEqual(results, [0, -1, -2, -3, -4])


if __name__ == "__main__":
    unittest.main()
