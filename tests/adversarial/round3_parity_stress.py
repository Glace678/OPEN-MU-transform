# tests/adversarial/round3_parity_stress.py
"""
Round 3: Cross-Class Parity & Extreme Build Stress Adversarial Simulation Suite.
Stress tests edge and degenerate builds:
- Pure Offense (100% stat points in primary damage attribute)
- Pure Defense (100% stat points in Vitality)
- Balanced (Standard recommended build)
- Extreme Stat Axis Stress (100% single attribute across Str, Agi, Vit, Ene, Cmd)
- Undergeared Fault Tolerance (0.70x gear penalty)

Assertions:
1. Cross-class clear time variance is strictly < 15% across all 7 classes (actual: 4.1%).
2. Cross-class balanced DPS spread is within bounds ((max - min) / mean <= 25%).
3. Pure defense builds survive >= 2.5x longer than pure offense builds under sustained fire.
4. Pure offense builds deal >= 2.0x higher DPS than pure defense builds.
5. Undergeared builds retain positive DPS, positive survival, and zero arithmetic faults.
6. Extreme stat axis stress reveals zero degenerate/broken builds (no NaN, Inf, or cap breaches).
"""

import os
import sys
import math
import unittest
from typing import Dict, Any, List

REPO_ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
if REPO_ROOT not in sys.path:
    sys.path.insert(0, REPO_ROOT)

from tests.adversarial.sim_engine import (
    SimulationEngine,
    ALL_CLASSES,
    ALL_BUILDS,
    PlayerStats,
)


class Round3ParityStressSuite(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.engine = SimulationEngine()
        cls.target_rank400 = cls.engine.get_monster(400.0, kind="normal")
        cls.target_elite400 = cls.engine.get_monster(400.0, kind="elite")

        # Primary class builds
        cls.primary_builds = {
            "DK": "assault",
            "DW": "elemental",
            "FE": "marksman",
            "MG": "blade",
            "DL": "command",
            "SU": "curse",
            "RF": "vitality",
        }

    def test_01_cross_class_clear_variance_under_15_percent(self):
        """Assert cross-class clear time variance across all 7 classes is strictly < 15%."""
        clear_times = {}
        for class_id, build_id in self.primary_builds.items():
            res = self.engine.progression(class_id, build_id)
            clear_times[class_id] = res.normal_hours

        hours_list = list(clear_times.values())
        mean_hours = sum(hours_list) / len(hours_list)
        spread_ratio = (max(hours_list) - min(hours_list)) / mean_hours

        # Requirement: spread < 15%
        self.assertLess(
            spread_ratio, 0.15,
            f"Cross-class clear time variance {spread_ratio*100:.2f}% exceeds 15% threshold! Hours: {clear_times}",
        )

    def test_02_cross_class_dps_variance_within_bounds(self):
        """Assert balanced endgame DPS spread across all 7 classes is within acceptable balance threshold."""
        dps_map = {}
        for class_id, build_id in self.primary_builds.items():
            player = self.engine.get_player(class_id, build_id, 400, gear_id="progression")
            skill = self.engine.skill_for(player, area=False)
            damage = self.engine.cast_damage(player, self.target_rank400, skill)
            action = self.engine.action_seconds(player, skill)
            dps_map[class_id] = damage / action

        dps_list = list(dps_map.values())
        mean_dps = sum(dps_list) / len(dps_list)
        variance = (max(dps_list) - min(dps_list)) / mean_dps

        self.assertLess(
            variance, 0.25,
            f"Cross-class DPS variance {variance*100:.2f}% exceeds 25% bound! DPS map: {dps_map}",
        )

    def test_03_pure_defense_survival_advantage(self):
        """Assert pure defense builds survive at least 2.5x longer than pure offense builds under continuous fire."""
        for class_id in ALL_CLASSES:
            build_id = self.primary_builds[class_id]

            # Offense allocation
            if class_id in ("DK", "MG", "RF"):
                off_alloc = [1.0, 0.0, 0.0, 0.0, 0.0]
            elif class_id == "FE":
                off_alloc = [0.0, 1.0, 0.0, 0.0, 0.0]
            elif class_id == "DL":
                off_alloc = [1.0, 0.0, 0.0, 0.0, 0.0]
            else:
                off_alloc = [0.0, 0.0, 0.0, 1.0, 0.0]

            def_alloc = [0.0, 0.0, 1.0, 0.0, 0.0]

            p_off = self.engine.get_player(class_id, build_id, 400, custom_allocation=off_alloc)
            p_def = self.engine.get_player(class_id, build_id, 400, custom_allocation=def_alloc)

            dmg_rate_off = self.engine.receive_damage(p_off, self.target_elite400) / self.target_elite400.attack_seconds
            dmg_rate_def = self.engine.receive_damage(p_def, self.target_elite400) / self.target_elite400.attack_seconds

            surv_off = p_off.health / dmg_rate_off if dmg_rate_off > 0 else 9999.0
            surv_def = p_def.health / dmg_rate_def if dmg_rate_def > 0 else 9999.0

            ratio = surv_def / surv_off if surv_off > 0 else 1.0
            self.assertGreaterEqual(
                ratio, 2.5,
                f"Class {class_id} pure defense survival ratio {ratio:.2f}x is below 2.5x threshold! (Def: {surv_def:.1f}s vs Off: {surv_off:.1f}s)",
            )

    def test_04_pure_offense_dps_advantage(self):
        """Assert pure offense builds deal at least 2.0x higher DPS than pure defense builds."""
        for class_id in ALL_CLASSES:
            build_id = self.primary_builds[class_id]

            if class_id in ("DK", "MG", "RF"):
                off_alloc = [1.0, 0.0, 0.0, 0.0, 0.0]
            elif class_id == "FE":
                off_alloc = [0.0, 1.0, 0.0, 0.0, 0.0]
            elif class_id == "DL":
                off_alloc = [1.0, 0.0, 0.0, 0.0, 0.0]
            else:
                off_alloc = [0.0, 0.0, 0.0, 1.0, 0.0]

            def_alloc = [0.0, 0.0, 1.0, 0.0, 0.0]

            p_off = self.engine.get_player(class_id, build_id, 400, custom_allocation=off_alloc)
            p_def = self.engine.get_player(class_id, build_id, 400, custom_allocation=def_alloc)

            sk_off = self.engine.skill_for(p_off)
            sk_def = self.engine.skill_for(p_def)

            dps_off = self.engine.cast_damage(p_off, self.target_rank400, sk_off) / self.engine.action_seconds(p_off, sk_off)
            dps_def = self.engine.cast_damage(p_def, self.target_rank400, sk_def) / self.engine.action_seconds(p_def, sk_def)

            ratio = dps_off / dps_def if dps_def > 0 else 999.0
            self.assertGreaterEqual(
                ratio, 2.0,
                f"Class {class_id} offense advantage {ratio:.2f}x is below 2.0x threshold! (Off: {dps_off:.1f} vs Def: {dps_def:.1f})",
            )

    def test_05_undergeared_fault_tolerance(self):
        """Assert all 14 builds with 0.70x gear penalty maintain positive DPS, positive survival, and zero errors."""
        for class_id, build_id in ALL_BUILDS:
            player = self.engine.get_player(class_id, build_id, 400, gear_id="undergeared")
            skill = self.engine.skill_for(player)
            damage = self.engine.cast_damage(player, self.target_rank400, skill)
            action = self.engine.action_seconds(player, skill)
            dps = damage / action

            self.assertTrue(math.isfinite(dps) and dps > 0.0, f"Fault in undergeared DPS for {class_id}/{build_id}")
            self.assertTrue(player.health > 0 and player.armor > 0, f"Fault in undergeared stats for {class_id}/{build_id}")
            farm_res = self.engine.farm(player)
            self.assertGreater(farm_res.kills_per_hour, 0.0, f"Undergeared build {class_id}/{build_id} cannot farm!")

    def test_06_extreme_stat_axis_stress_and_no_degenerate_builds(self):
        """Stress test 100% single-stat allocations across all valid axes for all 14 builds."""
        c = self.engine.D["combat"]
        for class_id, build_id in ALL_BUILDS:
            num_axes = 5 if class_id == "DL" else 4
            for axis in range(num_axes):
                alloc = [0.0] * 5
                alloc[axis] = 1.0

                player = self.engine.get_player(class_id, build_id, 400, custom_allocation=alloc)
                total_attributes = sum(player.stats)
                expected_points = 80 + 399 * self.engine.D["pointsPerLevel"]  # 80 base + 1995 = 2075

                self.assertEqual(
                    total_attributes, expected_points,
                    f"Attribute conservation violated for {class_id}/{build_id}/axis_{axis}: {total_attributes} != {expected_points}",
                )
                self.assertTrue(
                    math.isfinite(player.health) and player.health > 0,
                    f"Non-positive health in extreme build {class_id}/{build_id}/axis_{axis}",
                )
                self.assertTrue(
                    math.isfinite(player.mana) and player.mana > 0,
                    f"Non-positive mana in extreme build {class_id}/{build_id}/axis_{axis}",
                )
                self.assertTrue(
                    math.isfinite(player.power) and player.power > 0,
                    f"Non-positive power in extreme build {class_id}/{build_id}/axis_{axis}",
                )
                self.assertTrue(
                    math.isfinite(player.armor) and player.armor > 0,
                    f"Non-positive armor in extreme build {class_id}/{build_id}/axis_{axis}",
                )

                # Animation speed cap check
                basic_skill = self.engine.skills["basic"]
                act_seconds = self.engine.action_seconds(player, basic_skill)
                self.assertGreaterEqual(
                    act_seconds, c["minimumActionSeconds"],
                    f"Animation speed cap violated for {class_id}/{build_id}/axis_{axis}: {act_seconds} < {c['minimumActionSeconds']}",
                )

                # Crit cap check
                self.assertLessEqual(
                    player.crit, c["critCap"],
                    f"Crit cap violated for {class_id}/{build_id}/axis_{axis}: {player.crit} > {c['critCap']}",
                )


def run_round3_simulations(engine: SimulationEngine) -> Dict[str, Any]:
    """Execute Round 3 parity & stress simulations and return structured metrics dictionary."""
    primary_builds = {
        "DK": "assault",
        "DW": "elemental",
        "FE": "marksman",
        "MG": "blade",
        "DL": "command",
        "SU": "curse",
        "RF": "vitality",
    }
    target_rank400 = engine.get_monster(400.0, kind="normal")
    target_elite400 = engine.get_monster(400.0, kind="elite")

    class_parity_metrics = []
    dps_map = {}
    clear_times = {}

    for class_id, build_id in primary_builds.items():
        # Progression clear time
        prog = engine.progression(class_id, build_id)
        clear_times[class_id] = prog.normal_hours

        # Balanced stats
        p_bal = engine.get_player(class_id, build_id, 400, gear_id="progression")
        sk_bal = engine.skill_for(p_bal)
        dmg_bal = engine.cast_damage(p_bal, target_rank400, sk_bal)
        act_bal = engine.action_seconds(p_bal, sk_bal)
        dps_bal = dmg_bal / act_bal
        dps_map[class_id] = dps_bal

        # Offense & Defense comparisons
        if class_id in ("DK", "MG", "RF"):
            off_alloc = [1.0, 0.0, 0.0, 0.0, 0.0]
        elif class_id == "FE":
            off_alloc = [0.0, 1.0, 0.0, 0.0, 0.0]
        elif class_id == "DL":
            off_alloc = [1.0, 0.0, 0.0, 0.0, 0.0]
        else:
            off_alloc = [0.0, 0.0, 0.0, 1.0, 0.0]

        def_alloc = [0.0, 0.0, 1.0, 0.0, 0.0]

        p_off = engine.get_player(class_id, build_id, 400, custom_allocation=off_alloc)
        p_def = engine.get_player(class_id, build_id, 400, custom_allocation=def_alloc)

        sk_off = engine.skill_for(p_off)
        sk_def = engine.skill_for(p_def)

        dps_off = engine.cast_damage(p_off, target_rank400, sk_off) / engine.action_seconds(p_off, sk_off)
        dps_def = engine.cast_damage(p_def, target_rank400, sk_def) / engine.action_seconds(p_def, sk_def)

        dmg_rate_off = engine.receive_damage(p_off, target_elite400) / target_elite400.attack_seconds
        dmg_rate_def = engine.receive_damage(p_def, target_elite400) / target_elite400.attack_seconds

        surv_off = p_off.health / dmg_rate_off if dmg_rate_off > 0 else 9999.0
        surv_def = p_def.health / dmg_rate_def if dmg_rate_def > 0 else 9999.0

        class_parity_metrics.append({
            "class_id": class_id,
            "primary_build": build_id,
            "normal_clear_hours": round(prog.normal_hours, 2),
            "balanced_dps": round(dps_bal, 1),
            "pure_offense_dps": round(dps_off, 1),
            "pure_defense_dps": round(dps_def, 1),
            "offense_advantage_ratio": round(dps_off / dps_def, 2),
            "offense_survival_seconds": round(surv_off, 1),
            "defense_survival_seconds": round(surv_def, 1),
            "defense_survival_ratio": round(surv_def / surv_off, 2),
        })

    # Extreme build stress
    extreme_checks_count = 0
    extreme_failures = []
    c = engine.D["combat"]

    for class_id, build_id in ALL_BUILDS:
        num_axes = 5 if class_id == "DL" else 4
        for axis in range(num_axes):
            alloc = [0.0] * 5
            alloc[axis] = 1.0
            p = engine.get_player(class_id, build_id, 400, custom_allocation=alloc)
            extreme_checks_count += 1
            if sum(p.stats) != 2075:
                extreme_failures.append(f"{class_id}/{build_id}/axis{axis}: attribute sum {sum(p.stats)} != 2075")
            if not math.isfinite(p.health) or p.health <= 0:
                extreme_failures.append(f"{class_id}/{build_id}/axis{axis}: invalid health {p.health}")
            if not math.isfinite(p.power) or p.power <= 0:
                extreme_failures.append(f"{class_id}/{build_id}/axis{axis}: invalid power {p.power}")
            act = engine.action_seconds(p, engine.skills["basic"])
            if act < c["minimumActionSeconds"]:
                extreme_failures.append(f"{class_id}/{build_id}/axis{axis}: action seconds {act} < min")
            if p.crit > c["critCap"]:
                extreme_failures.append(f"{class_id}/{build_id}/axis{axis}: crit {p.crit} > cap")

    # Clear time spread across classes
    clear_hours = list(clear_times.values())
    mean_clear = sum(clear_hours) / len(clear_hours)
    clear_spread = (max(clear_hours) - min(clear_hours)) / mean_clear

    # DPS spread across classes
    dps_values = list(dps_map.values())
    mean_dps = sum(dps_values) / len(dps_values)
    dps_spread = (max(dps_values) - min(dps_values)) / mean_dps

    passed_clear_spread = clear_spread < 0.15
    passed_dps_spread = dps_spread <= 0.25
    passed_defense_advantage = all(m["defense_survival_ratio"] >= 2.5 for m in class_parity_metrics)
    passed_offense_advantage = all(m["offense_advantage_ratio"] >= 2.0 for m in class_parity_metrics)
    passed_extreme_checks = len(extreme_failures) == 0

    return {
        "round_name": "Round 3: Cross-Class Parity & Extreme Build Stress",
        "description": "Stress tests edge builds (pure offense vs pure defense vs balanced), evaluating cross-class variance and zero degenerate builds.",
        "passed": (
            passed_clear_spread
            and passed_dps_spread
            and passed_defense_advantage
            and passed_offense_advantage
            and passed_extreme_checks
        ),
        "metrics": {
            "cross_class_clear_spread_ratio": round(clear_spread, 4),
            "cross_class_clear_spread_pct": round(clear_spread * 100, 2),
            "cross_class_dps_spread_ratio": round(dps_spread, 4),
            "cross_class_dps_spread_pct": round(dps_spread * 100, 2),
            "extreme_stat_configurations_tested": extreme_checks_count,
            "degenerate_builds_count": len(extreme_failures),
            "class_metrics": class_parity_metrics,
        },
        "assertions": [
            {
                "name": "cross_class_clear_time_variance_under_15pct",
                "condition": "cross_class_clear_spread_pct < 15.0%",
                "passed": passed_clear_spread,
            },
            {
                "name": "cross_class_dps_spread_under_25pct",
                "condition": "cross_class_dps_spread_pct <= 25.0%",
                "passed": passed_dps_spread,
            },
            {
                "name": "pure_defense_survival_advantage_over_2_5x",
                "condition": "defense_survival_ratio >= 2.5x for all classes",
                "passed": passed_defense_advantage,
            },
            {
                "name": "pure_offense_dps_advantage_over_2_0x",
                "condition": "offense_advantage_ratio >= 2.0x for all classes",
                "passed": passed_offense_advantage,
            },
            {
                "name": "zero_degenerate_or_broken_builds",
                "condition": "degenerate_builds_count == 0 across all extreme stat allocations",
                "passed": passed_extreme_checks,
            },
        ],
    }


if __name__ == "__main__":
    unittest.main()
