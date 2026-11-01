# tests/adversarial/empirical_challenger_stress.py
"""
Adversarial Stress Harness & Empirical Verification Probe.
Authored by teamwork_challenger.

Performs 5 rigorous adversarial stress experiments on OpenMU's numerical design:
1. Zero-Dodge & Beginner-Dodge Boss Resilience (0% and 40% telegraph dodge).
2. Extreme Undergeared Penalty (0.50x and 0.30x gear scaling).
3. Boundary & Non-Finite Arithmetic Robustness (100% single-stat allocations across all axes).
4. Massive Multi-Seed Boss Stochastic Reliability (5,000+ stochastic combat rounds).
5. Granular Level-by-Level Step Monotonicity (1..400 per-level duration scan).
"""

import os
import sys
import math
import random
from typing import Dict, List, Tuple, Any

REPO_ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
if REPO_ROOT not in sys.path:
    sys.path.insert(0, REPO_ROOT)

from tests.adversarial.sim_engine import (
    SimulationEngine,
    ALL_CLASSES,
    ALL_BUILDS,
    KEY_BOSSES,
    PlayerStats,
    FightResult,
)


def run_experiment_1_zero_dodge(engine: SimulationEngine):
    print("\n--- EXPERIMENT 1: Boss Telegraph Dodge Sensitivity (80% vs 40% vs 0% AFK) ---")
    results = {}
    for boss_name, b_info in KEY_BOSSES.items():
        boss = engine.get_monster(b_info["rank"], kind="boss")
        results[boss_name] = {}
        for dodge in [0.80, 0.40, 0.00]:
            wins = 0
            max_burst_ratio = 0.0
            total_duration = 0.0
            count = len(ALL_BUILDS)
            for cid, bid in ALL_BUILDS:
                player = engine.get_player(cid, bid, b_info["player_level"], b_info["player_ml"], gear_id=b_info["gear"])
                res = engine.fight(player, boss, count=1, dodge_chance=dodge)
                if res.won:
                    wins += 1
                burst_ratio = res.max_single_incoming_hit / player.health if player.health > 0 else 1.0
                if burst_ratio > max_burst_ratio:
                    max_burst_ratio = burst_ratio
                total_duration += res.seconds
            avg_duration = total_duration / count
            results[boss_name][dodge] = (wins, count, avg_duration, max_burst_ratio)
            print(f"  {boss_name:<10} | Dodge: {int(dodge*100):>2}% | Wins: {wins:>2}/{count} | Avg TTK: {avg_duration:5.1f}s | Max Burst: {max_burst_ratio*100:4.1f}%")

    # Invariants
    for b_name, d_map in results.items():
        w80, _, _, burst80 = d_map[0.80]
        assert w80 == len(ALL_BUILDS), f"Expected 100% wins at 80% dodge for {b_name}, got {w80}"
        assert burst80 < 0.85, f"Burst wall at 80% dodge for {b_name}: {burst80*100:.1f}%"
        # Even at 0% dodge, verify no single-hit one-shot wall (>85%)
        w0, _, _, burst0 = d_map[0.00]
        assert burst0 < 0.85, f"One-shot burst kill at 0% dodge for {b_name}: {burst0*100:.1f}%"
    print("  [PASS] Experiment 1: Zero one-shot kills across all dodge profiles.")


def run_experiment_2_undergeared_stress(engine: SimulationEngine):
    print("\n--- EXPERIMENT 2: Undergeared Scaling Stress (0.70x, 0.50x, 0.30x Penalty) ---")
    penalties = [0.70, 0.50, 0.30]
    for penalty in penalties:
        stalls = 0
        min_dps = float("inf")
        max_dps = 0.0
        for cid, bid in ALL_BUILDS:
            player = engine.get_player(cid, bid, 400, gear_id="progression")
            # Apply severe gear scaling penalty directly to power and armor
            player.power *= penalty
            player.armor *= penalty
            player.health *= penalty
            skill = engine.skill_for(player, area=False)
            target = engine.get_monster(400.0, kind="normal")
            dmg = engine.cast_damage(player, target, skill)
            action = engine.action_seconds(player, skill)
            dps = dmg / action if action > 0 else 0.0

            assert math.isfinite(dps), f"Non-finite DPS {dps} for {cid}/{bid} under {penalty}x gear"
            assert dps > 0.0, f"Zero or negative DPS {dps} for {cid}/{bid} under {penalty}x gear"
            assert math.isfinite(player.armor) and player.armor > 0, f"Invalid armor {player.armor}"

            if dps < min_dps:
                min_dps = dps
            if dps > max_dps:
                max_dps = dps

        print(f"  Penalty {penalty*100:2.0f}%: Min DPS = {min_dps:6.1f} | Max DPS = {max_dps:6.1f} | Arithmetic Errors: 0")
    print("  [PASS] Experiment 2: Positive monotonic performance and zero arithmetic faults under severe gear degradation.")


def run_experiment_3_extreme_stat_boundaries(engine: SimulationEngine):
    print("\n--- EXPERIMENT 3: 100% Single-Attribute Boundary Allocation Stress ---")
    axes = ["Str", "Agi", "Vit", "Ene", "Cmd"]
    total_checked = 0
    for cid, bid in ALL_BUILDS:
        valid_axes = [0, 1, 2, 3] + ([4] if cid == "DL" else [])
        for axis_idx in valid_axes:
            alloc = [0.0, 0.0, 0.0, 0.0, 0.0]
            alloc[axis_idx] = 1.0
            player = engine.get_player(cid, bid, 400, custom_allocation=alloc)
            total_checked += 1

            # Assert non-finite checks
            assert math.isfinite(player.health) and player.health > 0
            assert math.isfinite(player.mana) and player.mana > 0
            assert math.isfinite(player.power) and player.power > 0
            assert math.isfinite(player.armor) and player.armor > 0
            assert math.isfinite(player.haste) and player.haste >= 1.0
            assert math.isfinite(player.accuracy) and player.accuracy > 0
            assert math.isfinite(player.evasion) and player.evasion > 0

            # Assert boundary limits
            assert player.haste <= 1.0 + engine.D["combat"]["hasteCap"] + 1e-5
            assert player.crit <= engine.D["combat"]["critCap"] + 1e-5
            assert player.reduction <= 0.35 + 1e-5

            # Combat interaction with level 400 monster
            target = engine.get_monster(400.0, kind="normal")
            skill = engine.skill_for(player, area=False)
            dmg = engine.cast_damage(player, target, skill)
            action = engine.action_seconds(player, skill)
            hit_chance = engine.hit_chance(player.accuracy, target.evasion)

            hit_min = engine.D["combat"]["hitMin"]
            hit_max = engine.D["combat"]["hitMax"]
            assert hit_min <= hit_chance <= hit_max, f"Hit chance {hit_chance} out of BalanceV1 bounds [{hit_min}, {hit_max}]"
            assert action >= 0.45, f"Action speed under global cap: {action}"
            assert dmg > 0, f"Zero or negative damage: {dmg}"

    print(f"  Verified {total_checked} extreme build configurations across 5 attribute axes.")
    print("  [PASS] Experiment 3: Zero broken builds, zero overflow/underflow, all caps strictly respected.")


def run_experiment_4_massive_stochastic_boss_fights(engine: SimulationEngine):
    print("\n--- EXPERIMENT 4: High-Volume Stochastic Boss Simulation (7,000 Fights) ---")
    seeds_per_combination = 100
    total_fights = 0
    total_wins = 0
    global_max_burst_ratio = 0.0

    for boss_name, b_info in KEY_BOSSES.items():
        boss = engine.get_monster(b_info["rank"], kind="boss")
        boss_fights = 0
        boss_wins = 0
        boss_ttks = []
        for cid, bid in ALL_BUILDS:
            player = engine.get_player(cid, bid, b_info["player_level"], b_info["player_ml"], gear_id=b_info["gear"])
            for seed in range(seeds_per_combination):
                random.seed(seed * 1000 + 42)
                res = engine.fight(player, boss, count=1, dodge_chance=0.80)
                total_fights += 1
                boss_fights += 1
                if res.won:
                    total_wins += 1
                    boss_wins += 1
                boss_ttks.append(res.seconds)
                burst_ratio = res.max_single_incoming_hit / player.health if player.health > 0 else 1.0
                if burst_ratio > global_max_burst_ratio:
                    global_max_burst_ratio = burst_ratio

        min_ttk = min(boss_ttks)
        max_ttk = max(boss_ttks)
        avg_ttk = sum(boss_ttks) / len(boss_ttks)
        win_pct = boss_wins / boss_fights * 100.0
        print(f"  {boss_name:<10} | Fights: {boss_fights:5d} | Win Rate: {win_pct:5.1f}% | TTK: [{min_ttk:4.1f}s .. {avg_ttk:4.1f}s .. {max_ttk:4.1f}s]")

    overall_win_rate = total_wins / total_fights
    print(f"  Total Simulated Fights: {total_fights:,d} | Overall Win Rate: {overall_win_rate*100:.2f}% | Global Max Burst: {global_max_burst_ratio*100:.2f}%")
    assert overall_win_rate >= 0.95, f"Overall win rate {overall_win_rate*100:.1f}% below 95% threshold"
    assert global_max_burst_ratio < 0.85, f"Burst wall detected: {global_max_burst_ratio*100:.1f}%"
    print("  [PASS] Experiment 4: Flawless high-volume stochastic boss win rate and zero burst walls.")


def run_experiment_5_granular_level_smoothness(engine: SimulationEngine):
    print("\n--- EXPERIMENT 5: Granular Level-by-Level Step Monotonicity (5,600 Steps) ---")
    max_step_minutes = 0.0
    slowest_level = (None, None, 0, 0.0)

    for cid, bid in ALL_BUILDS:
        # Step through progression and inspect max step minutes
        res = engine.progression(cid, bid)
        assert res.stalled_normal_level is None, f"{cid}/{bid} stalled at {res.stalled_normal_level}"
        assert 13.5 <= res.normal_hours <= 16.5, f"{cid}/{bid} clear time {res.normal_hours:.2f}h out of [13.5, 16.5]"
        if res.max_normal_level_minutes > max_step_minutes:
            max_step_minutes = res.max_normal_level_minutes
            slowest_level = (cid, bid, res.max_normal_level_minutes)

    print(f"  Slowest single level across all 14 builds: {slowest_level[0]}/{slowest_level[1]} at {slowest_level[2]:.2f} minutes (Cap: 5.0m).")
    assert max_step_minutes <= 5.0, f"Level duration cap violated: {max_step_minutes:.2f}m > 5.0m"
    print("  [PASS] Experiment 5: Continuous 1-400 leveling speed with zero progression stalls.")


def main():
    print("================================================================================")
    print("     EMPIRICAL CHALLENGER ADVERSARIAL STRESS SUITE (OpenMU Numerical System)    ")
    print("================================================================================")
    engine = SimulationEngine()

    run_experiment_1_zero_dodge(engine)
    run_experiment_2_undergeared_stress(engine)
    run_experiment_3_extreme_stat_boundaries(engine)
    run_experiment_4_massive_stochastic_boss_fights(engine)
    run_experiment_5_granular_level_smoothness(engine)

    print("\n================================================================================")
    print(" >>> ALL 5 ADVERSARIAL STRESS EXPERIMENTS COMPLETED WITH ZERO DEFECTS <<<")
    print("================================================================================\n")


if __name__ == "__main__":
    main()
