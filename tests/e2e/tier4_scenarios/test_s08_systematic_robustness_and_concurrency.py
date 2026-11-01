# tests/e2e/tier4_scenarios/test_s08_systematic_robustness_and_concurrency.py
import unittest
import threading
from tests.e2e.harness.systemic_engine import (
    ConcurrencySafetyGuard, RAIIResourceTracker, ExceptionHardeningContainer
)
from tests.e2e.harness.movement_engine import MovementEngine


class TestS08SystematicRobustnessAndConcurrency(unittest.TestCase):
    """
    Tier 4: Scenario 15 & 16.
    Scenario 15: High-Concurrency Multi-Player Action & Async Safety.
    Scenario 16: Full RAII Resource Cleanup on Sudden Disconnect.
    """

    def test_scenario_15_high_concurrency_multi_player_actions(self):
        # 30 simulated concurrent players performing simultaneous movements
        engines = [MovementEngine(start_x=100 + i, start_y=100 + i) for i in range(30)]
        results = [False] * 30

        def player_session(idx: int):
            eng = engines[idx]
            ok1, _, _ = eng.validate_and_move_step(101 + idx, 100 + idx, timestamp_ms=500)
            ok2, _, _ = eng.validate_and_move_step(102 + idx, 100 + idx, timestamp_ms=800)
            results[idx] = (ok1 and ok2)

        threads = [threading.Thread(target=player_session, args=(i,)) for i in range(30)]
        for t in threads:
            t.start()
        for t in threads:
            t.join()

        self.assertTrue(all(results))

    def test_scenario_16_full_raii_resource_cleanup_on_sudden_disconnect(self):
        # Multiple resources acquired during active player session: db context, socket, lock
        db_resource = RAIIResourceTracker()
        socket_resource = RAIIResourceTracker()
        trade_lock = RAIIResourceTracker()

        def active_session():
            with db_resource:
                with socket_resource:
                    with trade_lock:
                        # Sudden client crash/teardown
                        raise ConnectionResetError("Client forced disconnection")

        res, err = ExceptionHardeningContainer.execute_safe(active_session)
        self.assertIsInstance(err, ConnectionResetError)

        # Asserts that all 3 resources are cleanly disposed without leak
        self.assertTrue(db_resource.is_disposed)
        self.assertTrue(socket_resource.is_disposed)
        self.assertTrue(trade_lock.is_disposed)


if __name__ == "__main__":
    unittest.main()
