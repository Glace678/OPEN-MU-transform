# tests/e2e/tier4_scenarios/test_s02_anti_cheat_teleport_speed_enforcement.py
import unittest
from tests.e2e.harness.movement_engine import MovementEngine, Point


class TestS02AntiCheatTeleportSpeedEnforcement(unittest.TestCase):
    """
    Tier 4: Scenario 3 & 4.
    Scenario 3: Anti-Teleport Enforcement under High Latency / Lag Simulation.
    Scenario 4: Speedhack Packet Flood Burst Rejection.
    """

    def test_scenario_03_anti_teleport_enforcement_and_rubberband(self):
        engine = MovementEngine(start_x=130, start_y=130)

        # Normal walk steps
        engine.validate_and_move_step(132, 130, timestamp_ms=500)
        engine.validate_and_move_step(134, 130, timestamp_ms=800)
        self.assertEqual(engine.current_position, Point(134, 130))

        # Attacker injects 50-tile teleport jump to (184, 130)
        tp_ok, msg, pos = engine.validate_and_move_step(184, 130, timestamp_ms=1100)
        self.assertFalse(tp_ok)
        self.assertIn("Teleport detected", msg)
        # Server forces rubberband to (134, 130)
        self.assertEqual(pos, Point(134, 130))
        self.assertEqual(engine.current_position, Point(134, 130))

        # Subsequent legitimate step resumes from (134, 130)
        res_ok, _, res_pos = engine.validate_and_move_step(135, 130, timestamp_ms=1400)
        self.assertTrue(res_ok)
        self.assertEqual(res_pos, Point(135, 130))

    def test_scenario_04_speedhack_packet_flood_burst_rejection(self):
        engine = MovementEngine(start_x=100, start_y=100)

        # Attacker floods 50 move packets within 20 milliseconds
        accepted = 0
        rejected = 0
        current_x = 100
        for i in range(50):
            target_x = current_x + 1
            ok, _, _ = engine.validate_and_move_step(target_x, 100, timestamp_ms=1000 + (i % 20))
            if ok:
                accepted += 1
                current_x = target_x
            else:
                rejected += 1

        # Only initial move accepted, remainder dropped by rate-limiting anti-cheat
        self.assertGreater(rejected, 40)
        self.assertLessEqual(accepted, 5)


if __name__ == "__main__":
    unittest.main()
