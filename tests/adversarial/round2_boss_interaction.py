# tests/adversarial/round2_boss_interaction.py
"""
Round 2: Key Boss Combat & Survivability Interaction Adversarial Simulation Suite.
Evaluates encounters with key progression and endgame bosses:
1. Balrog (Rank 150, Lost Tower 7)
2. Hydra (Rank 190, Atlans 3)
3. Phoenix of Darkness (Rank 265, Icarus)
4. Kundun (Rank 400, Kalima 7)
5. Selupan (Rank 450, Raklion Hatchery)

Against each of the 14 builds (70 total encounter combinations).

Measurements:
- Sustained DPS (damage dealt per second)
- Time-to-kill (TTK, duration in seconds)
- Incoming burst survival (incoming hit vs player health)
- Potion recovery margin (health/mana potions consumed, remaining HP)
- Telegraph dodging (trained 80% vs beginner 40% vs zero dodge)

Assertions:
1. No one-shot impossible walls: Max single hit < 85% player max HP, TTK < 300s.
2. No trivial zero-damage cheesing: Boss deals > 0 damage to player, TTK >= 10s.
3. All 70 deterministic encounter combinations are won.
4. Multi-seed stochastic runs (16 seeds x 70 = 1,120 simulations) maintain >= 90% win rate.
"""

import os
import sys
import unittest
from typing import Dict, Any, List

REPO_ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
if REPO_ROOT not in sys.path:
    sys.path.insert(0, REPO_ROOT)

from tests.adversarial.sim_engine import (
    SimulationEngine,
    KEY_BOSSES,
    ALL_BUILDS,
    FightResult,
)


class Round2BossInteractionSuite(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.engine = SimulationEngine()
        cls.encounter_results = {}
        for boss_name, b_info in KEY_BOSSES.items():
            boss = cls.engine.get_monster(b_info["rank"], kind="boss")
            for class_id, build_id in ALL_BUILDS:
                player = cls.engine.get_player(
                    class_id,
                    build_id,
                    b_info["player_level"],
                    b_info["player_ml"],
                    gear_id=b_info["gear"],
                )
                res = cls.engine.fight(player, boss, count=1, dodge_chance=0.80)
                cls.encounter_results[(boss_name, class_id, build_id)] = (player, boss, res)

    def test_01_all_70_boss_encounters_winnable(self):
        """Assert all 70 boss encounter combinations (5 bosses x 14 builds) are winnable under standard play."""
        for (boss_name, class_id, build_id), (player, boss, res) in self.encounter_results.items():
            self.assertTrue(
                res.won,
                f"Encounter {boss_name} vs {class_id}/{build_id} was lost! Remaining HP: {res.remaining_health:.1f}",
            )
            self.assertGreater(
                res.seconds, 0.0,
                f"Encounter {boss_name} vs {class_id}/{build_id} had zero or negative duration!",
            )

    def test_02_no_one_shot_burst_kills(self):
        """Assert no one-shot impossible walls: max single incoming hit does not exceed 85% of player max HP."""
        for (boss_name, class_id, build_id), (player, boss, res) in self.encounter_results.items():
            burst_ratio = res.max_single_incoming_hit / player.health if player.health > 0 else 1.0
            self.assertLess(
                burst_ratio, 0.85,
                f"One-shot wall detected! {boss_name} dealt {res.max_single_incoming_hit:.1f} hit to {class_id}/{build_id} ({player.health:.1f} HP, ratio: {burst_ratio*100:.1f}%)",
            )

    def test_03_no_trivial_zero_damage_cheesing(self):
        """Assert no trivial cheesing: every boss inflicts measurable damage (>0) on every player build."""
        for (boss_name, class_id, build_id), (player, boss, res) in self.encounter_results.items():
            self.assertGreater(
                res.damage_taken, 0.0,
                f"Zero-damage exploit detected! {class_id}/{build_id} took 0 damage from {boss_name}!",
            )

    def test_04_boss_ttk_and_armor_integrity(self):
        """Assert time-to-kill is bounded in [10s, 300s]: neither trivial instant kills nor endless slogs."""
        for (boss_name, class_id, build_id), (player, boss, res) in self.encounter_results.items():
            self.assertGreaterEqual(
                res.seconds, 10.0,
                f"Trivial stomp! {class_id}/{build_id} killed {boss_name} in only {res.seconds:.1f}s (< 10s)",
            )
            self.assertLessEqual(
                res.seconds, 300.0,
                f"Tedious wall! {class_id}/{build_id} took {res.seconds:.1f}s to kill {boss_name} (> 300s)",
            )

    def test_05_potion_recovery_margin(self):
        """Assert players consume finite potions and maintain positive health margin throughout."""
        for (boss_name, class_id, build_id), (player, boss, res) in self.encounter_results.items():
            self.assertGreater(
                res.remaining_health, 0.0,
                f"{class_id}/{build_id} ended fight vs {boss_name} with zero health!",
            )
            self.assertLessEqual(
                res.health_potions, 20,
                f"{class_id}/{build_id} exhausted unreasonable potion count ({res.health_potions}) vs {boss_name}",
            )

    def test_06_multi_seed_stochastic_survival_rates(self):
        """Run 16 stochastic seeds for each of the 70 encounters (1,120 total simulations) and assert >= 90% win rate."""
        total_fights = 0
        total_wins = 0
        seeds = list(range(100, 116))  # 16 deterministic seeds

        for (boss_name, class_id, build_id), (player, boss, _) in self.encounter_results.items():
            encounter_wins = 0
            for seed in seeds:
                res = self.engine.fight(player, boss, count=1, seed=seed, dodge_chance=0.80)
                total_fights += 1
                if res.won:
                    total_wins += 1
                    encounter_wins += 1
            win_rate = encounter_wins / len(seeds)
            self.assertGreaterEqual(
                win_rate, 0.80,
                f"Stochastic win rate for {class_id}/{build_id} vs {boss_name} too low: {win_rate*100:.1f}%",
            )

        overall_win_rate = total_wins / total_fights
        self.assertGreaterEqual(
            overall_win_rate, 0.90,
            f"Overall stochastic win rate {overall_win_rate*100:.2f}% is below 90% threshold!",
        )


def run_round2_simulations(engine: SimulationEngine, num_seeds: int = 16) -> Dict[str, Any]:
    """Execute Round 2 boss interaction simulations and return structured metrics dictionary."""
    encounter_metrics = []
    boss_summaries = {}
    total_fights = 0
    total_wins = 0

    seeds = list(range(200, 200 + num_seeds))

    for boss_name, b_info in KEY_BOSSES.items():
        boss = engine.get_monster(b_info["rank"], kind="boss")
        b_ttks = []
        b_dps_list = []
        b_bursts = []
        b_wins = 0
        b_total = 0

        for class_id, build_id in ALL_BUILDS:
            player = engine.get_player(
                class_id,
                build_id,
                b_info["player_level"],
                b_info["player_ml"],
                gear_id=b_info["gear"],
            )

            # Deterministic fight
            det_res = engine.fight(player, boss, count=1, dodge_chance=0.80)
            dps = det_res.damage_dealt / det_res.seconds if det_res.seconds > 0 else 0.0
            burst_ratio = det_res.max_single_incoming_hit / player.health if player.health > 0 else 1.0

            # Stochastic multi-seed runs
            stoch_wins = 0
            stoch_ttks = []
            for seed in seeds:
                stoch_res = engine.fight(player, boss, count=1, seed=seed, dodge_chance=0.80)
                if stoch_res.won:
                    stoch_wins += 1
                    stoch_ttks.append(stoch_res.seconds)

            # Beginner dodge runs (40%)
            beg_wins = sum(
                1 for s in seeds[:8]
                if engine.fight(player, boss, count=1, seed=s, dodge_chance=0.40).won
            )
            beg_win_rate = beg_wins / 8.0

            win_rate = stoch_wins / num_seeds
            b_wins += stoch_wins
            b_total += num_seeds
            total_wins += stoch_wins
            total_fights += num_seeds

            b_ttks.append(det_res.seconds)
            b_dps_list.append(dps)
            b_bursts.append(burst_ratio)

            encounter_metrics.append({
                "boss_name": boss_name,
                "boss_rank": b_info["rank"],
                "class_id": class_id,
                "build_id": build_id,
                "player_rank": player.rank,
                "gear": b_info["gear"],
                "won": det_res.won,
                "ttk_seconds": round(det_res.seconds, 2),
                "sustained_dps": round(dps, 1),
                "damage_taken": round(det_res.damage_taken, 1),
                "max_incoming_hit": round(det_res.max_single_incoming_hit, 1),
                "burst_pct_of_hp": round(burst_ratio * 100, 2),
                "health_potions_used": det_res.health_potions,
                "mana_potions_used": det_res.mana_potions,
                "remaining_hp_pct": round(det_res.remaining_health / player.health * 100, 1),
                "stochastic_win_rate": round(win_rate, 4),
                "beginner_win_rate": round(beg_win_rate, 4),
            })

        boss_summaries[boss_name] = {
            "rank": b_info["rank"],
            "boss_hp": round(boss.health, 1),
            "boss_damage": round(boss.damage, 1),
            "boss_armor": round(boss.armor, 1),
            "mean_ttk_seconds": round(sum(b_ttks) / len(b_ttks), 2),
            "min_ttk_seconds": round(min(b_ttks), 2),
            "max_ttk_seconds": round(max(b_ttks), 2),
            "mean_dps": round(sum(b_dps_list) / len(b_dps_list), 1),
            "max_burst_pct": round(max(b_bursts) * 100, 2),
            "stochastic_win_rate": round(b_wins / b_total, 4),
        }

    overall_win_rate = total_wins / total_fights if total_fights > 0 else 0.0

    passed_all_won = all(m["won"] for m in encounter_metrics)
    passed_no_one_shot = all(m["burst_pct_of_hp"] < 85.0 for m in encounter_metrics)
    passed_no_zero_cheese = all(m["damage_taken"] > 0 for m in encounter_metrics)
    passed_ttk_range = all(10.0 <= m["ttk_seconds"] <= 300.0 for m in encounter_metrics)
    passed_stochastic_win = overall_win_rate >= 0.90

    return {
        "round_name": "Round 2: Key Boss Combat & Survivability Interaction",
        "description": "Evaluates boss encounters (Balrog, Hydra, Phoenix, Kundun, Selupan) against all 14 builds, measuring DPS, TTK, burst survival, and telegraph dodging.",
        "passed": (
            passed_all_won
            and passed_no_one_shot
            and passed_no_zero_cheese
            and passed_ttk_range
            and passed_stochastic_win
        ),
        "metrics": {
            "total_encounters_tested": len(encounter_metrics),
            "total_stochastic_simulations": total_fights,
            "overall_stochastic_win_rate": round(overall_win_rate, 4),
            "bosses": boss_summaries,
            "encounters": encounter_metrics,
        },
        "assertions": [
            {
                "name": "all_70_encounters_winnable",
                "condition": "won == True for all 70 boss encounter combinations",
                "passed": passed_all_won,
            },
            {
                "name": "no_one_shot_burst_walls",
                "condition": "burst_pct_of_hp < 85% for all encounters",
                "passed": passed_no_one_shot,
            },
            {
                "name": "no_trivial_zero_damage_cheesing",
                "condition": "damage_taken > 0 and ttk >= 10s for all encounters",
                "passed": passed_no_zero_cheese and all(m["ttk_seconds"] >= 10.0 for m in encounter_metrics),
            },
            {
                "name": "boss_ttk_bounded_in_10_to_300s",
                "condition": "10s <= ttk_seconds <= 300s for all encounters",
                "passed": passed_ttk_range,
            },
            {
                "name": "overall_stochastic_win_rate_above_90",
                "condition": "overall_stochastic_win_rate >= 0.90 across 1,120 simulations",
                "passed": passed_stochastic_win,
            },
        ],
    }


if __name__ == "__main__":
    unittest.main()
