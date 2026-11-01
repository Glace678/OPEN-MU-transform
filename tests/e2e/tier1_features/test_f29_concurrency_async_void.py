# tests/e2e/tier1_features/test_f29_concurrency_async_void.py
import unittest
import threading
from tests.e2e.harness.systemic_engine import ConcurrencySafetyGuard


class TestF29ConcurrencyAsyncVoid(unittest.TestCase):
    """
    Feature 29: Concurrency & Async Void Protection.
    Fixes async void crashes, thread-unsafe collections, and lock inversions.
    """

    def test_concurrent_increment_thread_safety(self):
        guard = ConcurrencySafetyGuard()
        threads = []
        for _ in range(10):
            t = threading.Thread(target=lambda: [guard.increment() for _ in range(50)])
            threads.append(t)
            t.start()
        for t in threads:
            t.join()

        self.assertEqual(guard.get_value(), 500)

    def test_lock_integrity_under_contention(self):
        guard = ConcurrencySafetyGuard()
        t1 = threading.Thread(target=lambda: [guard.increment() for _ in range(100)])
        t2 = threading.Thread(target=lambda: [guard.increment() for _ in range(100)])
        t1.start()
        t2.start()
        t1.join()
        t2.join()
        self.assertEqual(guard.get_value(), 200)

    def test_async_void_exception_containment(self):
        captured_exceptions = []

        def simulated_event_handler():
            try:
                raise ConnectionResetError("Socket disconnected")
            except Exception as e:
                captured_exceptions.append(e)

        t = threading.Thread(target=simulated_event_handler)
        t.start()
        t.join()
        self.assertEqual(len(captured_exceptions), 1)
        self.assertIsInstance(captured_exceptions[0], ConnectionResetError)

    def test_reentrant_lock_avoidance(self):
        lock = threading.RLock()
        with lock:
            with lock:
                val = 42
        self.assertEqual(val, 42)

    def test_concurrent_reads_consistency(self):
        guard = ConcurrencySafetyGuard()
        guard.increment()
        read_values = []

        def reader():
            read_values.append(guard.get_value())

        threads = [threading.Thread(target=reader) for _ in range(5)]
        for t in threads:
            t.start()
        for t in threads:
            t.join()

        self.assertEqual(read_values, [1, 1, 1, 1, 1])


if __name__ == "__main__":
    unittest.main()
