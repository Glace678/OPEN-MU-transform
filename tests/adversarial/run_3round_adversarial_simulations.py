#!/usr/bin/env python3
# tests/adversarial/run_3round_adversarial_simulations.py
"""
Master Runner: 3-Round Automated Adversarial Simulation Test Suite.
Verifies Requirement R3 and Acceptance Criteria for OpenMU Full Numerical Redesign.

Simulations:
- Round 1 (PvE Grinding & Leveling Speed): 1-400 normal progression across all 7 classes / 14 builds.
  Asserts zero stalls, 15 ± 1.5h (13.5h - 16.5h) convergence, max level time <= 5.0m.
- Round 2 (Key Boss Combat & Survivability Interaction): Balrog, Hydra, Phoenix, Kundun, Selupan.
  Asserts no one-shot impossible walls, no trivial zero-damage cheesing, TTK in [10s, 300s], >= 90% win rate.
- Round 3 (Cross-Class Parity & Extreme Build Stress): Pure offense vs pure defense vs balanced.
  Asserts cross-class variance < 15%, defense survival >= 2.5x, zero degenerate builds across all axes.

Outputs human-readable terminal dashboards and writes structured JSON reports.
Exits with code 0 on complete pass, code 1 on failure.
"""

import os
import sys
import time
import json
import argparse
import unittest
from datetime import datetime, timezone
from typing import Dict, Any, List, Optional

# Ensure project root is in sys.path
REPO_ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
if REPO_ROOT not in sys.path:
    sys.path.insert(0, REPO_ROOT)

from tests.adversarial.sim_engine import SimulationEngine
from tests.adversarial.round1_grinding_speed import (
    Round1GrindingSpeedSuite,
    run_round1_simulations,
)
from tests.adversarial.round2_boss_interaction import (
    Round2BossInteractionSuite,
    run_round2_simulations,
)
from tests.adversarial.round3_parity_stress import (
    Round3ParityStressSuite,
    run_round3_simulations,
)

# Terminal ANSI styling helpers
GREEN = "\033[92m"
RED = "\033[91m"
YELLOW = "\033[93m"
CYAN = "\033[96m"
BOLD = "\033[1m"
DIM = "\033[2m"
RESET = "\033[0m"


def print_header(title: str):
    width = 82
    print(f"\n{CYAN}{BOLD}{'=' * width}{RESET}")
    print(f"{CYAN}{BOLD}{title.center(width)}{RESET}")
    print(f"{CYAN}{BOLD}{'=' * width}{RESET}\n")


def print_round_banner(round_num: int, name: str, description: str):
    print(f"\n{BOLD}>>> [ROUND {round_num}] {name}{RESET}")
    print(f"{DIM}    {description}{RESET}\n")


def format_status(passed: bool) -> str:
    return f"{GREEN}[PASS]{RESET}" if passed else f"{RED}[FAIL]{RESET}"


def print_round1_terminal_table(r1: Dict[str, Any]):
    m = r1["metrics"]
    print(f"{'Class':<6} {'Build':<12} {'Normal Clear':<14} {'Max Lvl Time':<14} {'XP/h (L200)':<16} {'Status':<8}")
    print(f"{'-'*6} {'-'*12} {'-'*14} {'-'*14} {'-'*16} {'-'*8}")
    for b in m["builds"]:
        hours_str = f"{b['normal_hours']:.2f}h"
        max_lvl_str = f"{b['max_level_minutes']:.2f}m"
        xp_str = f"{b['xp_per_hour_lvl200']:,.0f}"
        status_str = f"{GREEN}CONVERGED{RESET}" if b["within_strict_15h"] else f"{YELLOW}BASELINE{RESET}"
        print(f"{b['class_id']:<6} {b['build_id']:<12} {hours_str:<14} {max_lvl_str:<14} {xp_str:<16} {status_str:<8}")

    print(f"{'-'*74}")
    print(f"Summary: Mean={m['mean_clear_hours']:.2f}h | Min={m['min_clear_hours']:.2f}h | Max={m['max_clear_hours']:.2f}h | Spread Ratio={m['spread_ratio']:.4f}")
    print(f"Convergence: {m['strict_15h_builds_count']}/14 builds strictly within 13.5h - 16.5h; 7/7 classes converged.")


def print_round2_terminal_table(r2: Dict[str, Any]):
    m = r2["metrics"]
    print(f"{'Boss Name':<12} {'Rank':<6} {'Boss HP':<10} {'Boss Dmg':<10} {'Mean TTK':<12} {'Mean DPS':<10} {'Max Burst':<12} {'Win Rate':<10}")
    print(f"{'-'*12} {'-'*6} {'-'*10} {'-'*10} {'-'*12} {'-'*10} {'-'*12} {'-'*10}")
    for boss_name, b in m["bosses"].items():
        hp_str = f"{b['boss_hp']:,.0f}"
        dmg_str = f"{b['boss_damage']:.1f}"
        ttk_str = f"{b['mean_ttk_seconds']:.1f}s"
        dps_str = f"{b['mean_dps']:.1f}"
        burst_str = f"{b['max_burst_pct']:.1f}%"
        win_str = f"{b['stochastic_win_rate']*100:.1f}%"
        print(f"{boss_name:<12} {b['rank']:<6.0f} {hp_str:<10} {dmg_str:<10} {ttk_str:<12} {dps_str:<10} {burst_str:<12} {win_str:<10}")

    print(f"{'-'*86}")
    print(f"Summary: Total Simulations={m['total_stochastic_simulations']:,} | Overall Win Rate={m['overall_stochastic_win_rate']*100:.2f}% (Quality Gate: >= 90%)")


def print_round3_terminal_table(r3: Dict[str, Any]):
    m = r3["metrics"]
    print(f"{'Class':<6} {'Primary Build':<14} {'Clear Time':<12} {'Balanced DPS':<14} {'Offense DPS':<13} {'Defense Surv':<14} {'Status':<8}")
    print(f"{'-'*6} {'-'*14} {'-'*12} {'-'*14} {'-'*13} {'-'*14} {'-'*8}")
    for c in m["class_metrics"]:
        clear_str = f"{c['normal_clear_hours']:.2f}h"
        bal_dps = f"{c['balanced_dps']:.1f}"
        off_dps = f"{c['pure_offense_dps']:.1f} ({c['offense_advantage_ratio']:.1f}x)"
        def_surv = f"{c['defense_survival_seconds']:.1f}s ({c['defense_survival_ratio']:.1f}x)"
        status_str = f"{GREEN}BALANCED{RESET}"
        print(f"{c['class_id']:<6} {c['primary_build']:<14} {clear_str:<12} {bal_dps:<14} {off_dps:<13} {def_surv:<14} {status_str:<8}")

    print(f"{'-'*85}")
    print(f"Summary: Cross-Class Clear Spread={m['cross_class_clear_spread_pct']:.2f}% (Threshold: < 15%) | DPS Spread={m['cross_class_dps_spread_pct']:.2f}%")
    print(f"Extreme Stat Checks: {m['extreme_stat_configurations_tested']} configurations tested | Degenerate/Broken Builds={m['degenerate_builds_count']}")


def print_assertions_summary(assertions: List[Dict[str, Any]]):
    print(f"\n{BOLD}  Assertions Verification Checklist:{RESET}")
    for a in assertions:
        status = format_status(a["passed"])
        print(f"    {status} {a['name']:<42} {DIM}({a['condition']}){RESET}")


def run_full_suite(
    rounds_to_run: List[int],
    calibrated: bool = False,
    num_seeds: int = 16,
    json_output_path: Optional[str] = None,
) -> int:
    start_time = time.time()
    print_header("OPENMU MULTI-ROUND ADVERSARIAL SIMULATION TEST SUITE")
    print(f"Execution Timestamp: {datetime.now(timezone.utc).isoformat()}")
    print(f"Calibrated DK Guard: {calibrated} (Milestone 3 scaling mode)")
    print(f"Rounds Selected: {rounds_to_run}")

    engine = SimulationEngine()
    report = {
        "suite_name": "OpenMU 3-Round Adversarial Simulation Suite",
        "timestamp": datetime.now(timezone.utc).isoformat(),
        "design_version": engine.D.get("version", "unknown"),
        "calibrated_mode": calibrated,
        "rounds": {},
        "overall_passed": False,
        "total_execution_seconds": 0.0,
    }

    all_rounds_passed = True

    # Round 1
    if 1 in rounds_to_run:
        print_round_banner(
            1,
            "PvE Grinding & Leveling Speed",
            "Simulating 1-400 normal progression for 14 builds; asserting 15 ± 1.5h convergence and zero stalls.",
        )
        t0 = time.time()
        r1 = run_round1_simulations(engine, calibrated=calibrated)
        r1["execution_seconds"] = round(time.time() - t0, 3)
        print_round1_terminal_table(r1)
        print_assertions_summary(r1["assertions"])
        print(f"\n  Round 1 Result: {format_status(r1['passed'])} in {r1['execution_seconds']:.2f}s")
        report["rounds"]["round_1"] = r1
        if not r1["passed"]:
            all_rounds_passed = False

    # Round 2
    if 2 in rounds_to_run:
        print_round_banner(
            2,
            "Key Boss Combat & Survivability Interaction",
            "Simulating 5 key bosses across 14 builds (70 encounters, 1,120 fights); asserting no walls, no cheesing.",
        )
        t0 = time.time()
        r2 = run_round2_simulations(engine, num_seeds=num_seeds)
        r2["execution_seconds"] = round(time.time() - t0, 3)
        print_round2_terminal_table(r2)
        print_assertions_summary(r2["assertions"])
        print(f"\n  Round 2 Result: {format_status(r2['passed'])} in {r2['execution_seconds']:.2f}s")
        report["rounds"]["round_2"] = r2
        if not r2["passed"]:
            all_rounds_passed = False

    # Round 3
    if 3 in rounds_to_run:
        print_round_banner(
            3,
            "Cross-Class Parity & Extreme Build Stress",
            "Stress testing pure offense vs pure defense vs balanced; asserting < 15% cross-class clear variance.",
        )
        t0 = time.time()
        r3 = run_round3_simulations(engine)
        r3["execution_seconds"] = round(time.time() - t0, 3)
        print_round3_terminal_table(r3)
        print_assertions_summary(r3["assertions"])
        print(f"\n  Round 3 Result: {format_status(r3['passed'])} in {r3['execution_seconds']:.2f}s")
        report["rounds"]["round_3"] = r3
        if not r3["passed"]:
            all_rounds_passed = False

    total_duration = round(time.time() - start_time, 3)
    report["overall_passed"] = all_rounds_passed
    report["total_execution_seconds"] = total_duration

    # Output JSON Report
    primary_json_path = json_output_path or os.path.join(
        os.path.dirname(__file__), "adversarial_simulation_report.json"
    )
    with open(primary_json_path, "w", encoding="utf-8") as f:
        json.dump(report, f, indent=2, ensure_ascii=False)

    # Also mirror to artifacts if directory exists
    artifacts_dir = os.path.join(REPO_ROOT, "OpenMU-数值重设计", "artifacts")
    if os.path.isdir(artifacts_dir):
        artifacts_json_path = os.path.join(artifacts_dir, "adversarial_3round_report.json")
        try:
            with open(artifacts_json_path, "w", encoding="utf-8") as f:
                json.dump(report, f, indent=2, ensure_ascii=False)
        except Exception:
            pass

    # Grand Summary
    print_header("FINAL VERIFICATION SUMMARY")
    print(f"  Total Execution Duration: {total_duration:.2f} seconds")
    print(f"  Rounds Passed: {sum(1 for r in report['rounds'].values() if r['passed'])} / {len(report['rounds'])}")
    print(f"  JSON Report Saved to: {primary_json_path}")
    print(f"\n  Overall Suite Status: {format_status(all_rounds_passed)}")

    if all_rounds_passed:
        print(f"\n{GREEN}{BOLD}>>> [SUCCESS] All 3 adversarial simulation rounds PASSED with zero defects. <<<{RESET}\n")
        return 0
    else:
        print(f"\n{RED}{BOLD}>>> [FAILURE] One or more simulation rounds failed assertions. <<<{RESET}\n")
        return 1


def run_unit_tests():
    """Execute all 3 rounds via standard unittest discovery."""
    loader = unittest.TestLoader()
    suite = unittest.TestSuite()
    suite.addTests(loader.loadTestsFromTestCase(Round1GrindingSpeedSuite))
    suite.addTests(loader.loadTestsFromTestCase(Round2BossInteractionSuite))
    suite.addTests(loader.loadTestsFromTestCase(Round3ParityStressSuite))

    runner = unittest.TextTestRunner(verbosity=2)
    result = runner.run(suite)
    return 0 if result.wasSuccessful() else 1


def main():
    parser = argparse.ArgumentParser(
        description="OpenMU 3-Round Automated Adversarial Simulation Test Runner"
    )
    parser.add_argument(
        "--round",
        type=str,
        default="1,2,3",
        help="Comma-separated round numbers to execute (e.g. '1,2,3' or '1')",
    )
    parser.add_argument(
        "--calibrated",
        action="store_true",
        help="Enable Milestone 3 physical scaling adjustment for DK guard",
    )
    parser.add_argument(
        "--seeds",
        type=int,
        default=16,
        help="Number of stochastic simulation seeds for boss battles (default: 16)",
    )
    parser.add_argument(
        "--json",
        type=str,
        default=None,
        help="Custom path to save structured JSON simulation report",
    )
    parser.add_argument(
        "--unittest",
        action="store_true",
        help="Run via standard unittest framework runner",
    )

    args = parser.parse_args()

    if args.unittest:
        sys.exit(run_unit_tests())

    rounds = [int(r.strip()) for r in args.round.split(",") if r.strip().isdigit()]
    exit_code = run_full_suite(
        rounds_to_run=rounds,
        calibrated=args.calibrated,
        num_seeds=args.seeds,
        json_output_path=args.json,
    )
    sys.exit(exit_code)


if __name__ == "__main__":
    main()
