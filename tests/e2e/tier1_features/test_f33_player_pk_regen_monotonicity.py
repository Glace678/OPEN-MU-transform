# tests/e2e/tier1_features/test_f33_player_pk_regen_monotonicity.py
import unittest
from datetime import datetime, timedelta, timezone
from tests.e2e.harness.regression_models import (
    HeroState,
    PlayerCharacterState,
    PlayerRegenerationEngine,
)


class TestF33PlayerPKRegenMonotonicity(unittest.TestCase):
    """
    Feature 33: Player PK/Hero Regeneration Calculation Monotonicity (GL-03).
    Ensures that StateRemainingSeconds monotonically decreases over time,
    correcting the legacy inverted subtraction defect that caused countdowns to increase.
    """

    def setUp(self):
        self.t0 = datetime(2026, 11, 1, 12, 0, 0, tzinfo=timezone.utc)
        self.character = PlayerCharacterState(
            name="DarkKnight_PK",
            state=HeroState.PlayerKiller1,
            state_remaining_seconds=300,
            last_regenerate_utc=self.t0,
        )

    def test_countdown_decreases_over_positive_elapsed_time(self):
        """Advancing 50 seconds decreases remaining time by exactly 50 seconds."""
        t1 = self.t0 + timedelta(seconds=50)
        remaining = PlayerRegenerationEngine.regenerate_hardened(self.character, t1)
        self.assertEqual(remaining, 250)
        self.assertEqual(self.character.last_regenerate_utc, t1)

    def test_state_transitions_to_normal_on_zero_remaining(self):
        """When remaining seconds reaches 0, PK status increments towards Normal."""
        t_expire = self.t0 + timedelta(seconds=300)
        remaining = PlayerRegenerationEngine.regenerate_hardened(self.character, t_expire)
        self.assertEqual(remaining, 0)
        # PlayerKiller1 (-1) + 1 -> Normal (0)
        self.assertEqual(self.character.state, HeroState.Normal)

    def test_hero_state_transitions_to_normal(self):
        """When Hero countdown expires, state decrements towards Normal."""
        hero = PlayerCharacterState(
            name="Archangel_Hero",
            state=HeroState.Hero1,
            state_remaining_seconds=120,
            last_regenerate_utc=self.t0,
        )
        t_expire = self.t0 + timedelta(seconds=120)
        remaining = PlayerRegenerationEngine.regenerate_hardened(hero, t_expire)
        self.assertEqual(remaining, 0)
        # Hero1 (+1) - 1 -> Normal (0)
        self.assertEqual(hero.state, HeroState.Normal)

    def test_monotonic_decrease_across_sequential_ticks(self):
        """Sequence of ticks confirms countdown never increases: S(t_{k+1}) <= S(t_k)."""
        prev_remaining = self.character.state_remaining_seconds
        for step in range(1, 11):
            t_step = self.t0 + timedelta(seconds=step * 10)
            cur_remaining = PlayerRegenerationEngine.regenerate_hardened(self.character, t_step)
            self.assertLess(cur_remaining, prev_remaining)
            prev_remaining = cur_remaining

    def test_vulnerable_inverted_time_regression_contrast(self):
        """Contrast against vulnerable implementation: legacy bug caused countdown to INCREASE."""
        vulnerable_char = PlayerCharacterState(
            name="Vulnerable_PK",
            state=HeroState.PlayerKiller1,
            state_remaining_seconds=300,
            last_regenerate_utc=self.t0,
        )
        t1 = self.t0 + timedelta(seconds=60)
        # Vulnerable subtraction: 300 - (-60) = 360
        vulnerable_remaining = PlayerRegenerationEngine.regenerate_vulnerable(vulnerable_char, t1)
        self.assertGreater(vulnerable_remaining, 300)
        self.assertEqual(vulnerable_remaining, 360)


if __name__ == "__main__":
    unittest.main()
