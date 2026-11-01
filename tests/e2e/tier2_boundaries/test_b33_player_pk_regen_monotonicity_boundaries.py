# tests/e2e/tier2_boundaries/test_b33_player_pk_regen_monotonicity_boundaries.py
import unittest
from datetime import datetime, timedelta, timezone
from tests.e2e.harness.regression_models import (
    HeroState,
    PlayerCharacterState,
    PlayerRegenerationEngine,
)


class TestB33PlayerPKRegenMonotonicityBoundaries(unittest.TestCase):
    """
    Feature 33 Boundaries: Player PK/Hero Regeneration Calculation Monotonicity.
    Tests boundary conditions:
    - Zero elapsed time (identical timestamps).
    - Negative elapsed time (backward clock warp defense).
    - Extremely large elapsed time (clamping to 0 without underflow).
    - Already Normal state boundary.
    - Multi-stage sequential state transitions across stages.
    """

    def setUp(self):
        self.t0 = datetime(2026, 11, 1, 12, 0, 0, tzinfo=timezone.utc)

    def test_zero_elapsed_time_no_change(self):
        """Calling regenerate with identical timestamp preserves countdown exactly."""
        char = PlayerCharacterState(
            name="PK_Neutral",
            state=HeroState.PlayerKiller1,
            state_remaining_seconds=180,
            last_regenerate_utc=self.t0,
        )
        remaining = PlayerRegenerationEngine.regenerate_hardened(char, self.t0)
        self.assertEqual(remaining, 180)
        self.assertEqual(char.last_regenerate_utc, self.t0)

    def test_negative_elapsed_time_no_increase(self):
        """If clock steps backward (time warp), countdown does not increase."""
        char = PlayerCharacterState(
            name="PK_TimeWarp",
            state=HeroState.PlayerKiller1,
            state_remaining_seconds=180,
            last_regenerate_utc=self.t0,
        )
        t_past = self.t0 - timedelta(seconds=60)
        remaining = PlayerRegenerationEngine.regenerate_hardened(char, t_past)
        # Monotonicity preserved: countdown remains 180, does not increase
        self.assertEqual(remaining, 180)
        self.assertEqual(char.last_regenerate_utc, self.t0)

    def test_large_elapsed_time_clamps_at_zero(self):
        """Huge elapsed time (100,000s) cleanly transitions to next stage without negative underflow."""
        char = PlayerCharacterState(
            name="PK_AFK",
            state=HeroState.PlayerKiller1,
            state_remaining_seconds=60,
            last_regenerate_utc=self.t0,
        )
        t_huge = self.t0 + timedelta(seconds=100000)
        remaining = PlayerRegenerationEngine.regenerate_hardened(char, t_huge)
        self.assertEqual(remaining, 0)
        self.assertEqual(char.state, HeroState.Normal)

    def test_already_normal_state_boundary(self):
        """Normal state with 0 remaining seconds stays at Normal 0."""
        char = PlayerCharacterState(
            name="Citizen",
            state=HeroState.Normal,
            state_remaining_seconds=0,
            last_regenerate_utc=self.t0,
        )
        t_future = self.t0 + timedelta(seconds=500)
        remaining = PlayerRegenerationEngine.regenerate_hardened(char, t_future)
        self.assertEqual(remaining, 0)
        self.assertEqual(char.state, HeroState.Normal)

    def test_multi_stage_transition_boundary(self):
        """Severe PK (PlayerKiller2) regenerates first to PlayerKiller1 then Normal."""
        char = PlayerCharacterState(
            name="PK_Outlaw",
            state=HeroState.PlayerKiller2,
            state_remaining_seconds=100,
            last_regenerate_utc=self.t0,
        )
        # Stage 1: PK2 -> PK1
        t1 = self.t0 + timedelta(seconds=100)
        rem1 = PlayerRegenerationEngine.regenerate_hardened(char, t1, stage_duration_seconds=100)
        self.assertEqual(char.state, HeroState.PlayerKiller1)
        self.assertEqual(rem1, 100)

        # Stage 2: PK1 -> Normal
        t2 = t1 + timedelta(seconds=100)
        rem2 = PlayerRegenerationEngine.regenerate_hardened(char, t2, stage_duration_seconds=100)
        self.assertEqual(char.state, HeroState.Normal)
        self.assertEqual(rem2, 0)


if __name__ == "__main__":
    unittest.main()
