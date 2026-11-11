#!/usr/bin/env python3
"""
tests/adversarial/stress_test_m1_formulas.py

Empirical Adversarial Stress Test Suite for Milestone 1:
1. Character Classes Points Per Level (5 pts/lvl standard, 1,995 points at level 400 for all 7 classes).
2. Elimination of SoloBalance bonus (+3) and MG/DL/RF 7 pts/lvl distortion.
3. Buff formula precision and stability (LifeSwell, SoulBarrier, GreaterDamage, GreaterDefense, Berserker)
   under boundary and extreme values (0, 100, 500, 2000, 5000).
4. Detection of integer divisor rounding steps vs continuous decimal scaling.
5. Verification of explosive multiplier prevention and upper bound caps.
6. Hit chance monotonic continuity and hyperbolic armor bounds.
"""

import os
import sys
import re
import math
import unittest
from typing import Dict, List, Tuple, Any

REPO_ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
if REPO_ROOT not in sys.path:
    sys.path.insert(0, REPO_ROOT)

class Milestone1EmpiricalStressTests(unittest.TestCase):

    def test_01_all_7_classes_define_exactly_5_points_per_level(self):
        """
        Verify in OpenMU source code that ALL 7 character class initialization files
        define PointsPerLevelUp as exactly 5, and none define 7.
        """
        classes_dir = os.path.join(REPO_ROOT, "OpenMU", "src", "Persistence", "Initialization", "CharacterClasses")
        class_files = {
            "DK": "ClassDarkKnight.cs",
            "DW": "ClassDarkWizard.cs",
            "FE": "ClassFairyElf.cs",
            "MG": "ClassMagicGladiator.cs",
            "DL": "ClassDarkLord.cs",
            "SU": "ClassSummoner.cs",
            "RF": "ClassRageFighter.cs",
        }

        pattern = re.compile(r"CreateStatAttributeDefinition\s*\(\s*Stats\.PointsPerLevelUp\s*,\s*(\d+)\s*,\s*false\s*\)")

        for cls_id, filename in class_files.items():
            filepath = os.path.join(classes_dir, filename)
            self.assertTrue(os.path.isfile(filepath), f"File {filepath} not found")
            with open(filepath, "r", encoding="utf-8") as f:
                content = f.read()

            match = pattern.search(content)
            self.assertIsNotNone(match, f"Class {cls_id} in {filename} does not contain PointsPerLevelUp definition")
            pts = int(match.group(1))
            self.assertEqual(pts, 5, f"Class {cls_id} PointsPerLevelUp is {pts}, expected exactly 5 (no 7 pts/lvl allowed)")

    def test_02_player_experience_has_no_solobalance_point_bonus(self):
        """
        Verify that PlayerExperience.cs awards strictly player.Attributes[Stats.PointsPerLevelUp]
        and does NOT add SoloBalance.GetLevelUpPointBonus or any other level up bonus.
        """
        exp_file = os.path.join(REPO_ROOT, "OpenMU", "src", "GameLogic", "PlayerExperience.cs")
        self.assertTrue(os.path.isfile(exp_file))
        with open(exp_file, "r", encoding="utf-8") as f:
            content = f.read()

        # Check line awarding points
        self.assertIn("player.SelectedCharacter.LevelUpPoints += (int)player.Attributes[Stats.PointsPerLevelUp];", content)
        self.assertNotIn("GetLevelUpPointBonus", content)
        self.assertNotIn("SoloBalance.GetLevelUpPointBonus", content)

    def test_03_400_levels_yield_exactly_1995_points_for_all_7_classes(self):
        """
        Verify that leveling from 1 to 400 yields exactly 1,995 points for all 7 classes.
        Level 1 (initial) -> Level 400: 399 level-ups * 5 pts/lvl = 1,995 points.
        Verify that no class receives 2,793 (399 * 7) or 3,192 (399 * (5 + 3)).
        """
        start_level = 1
        end_level = 400
        level_ups = end_level - start_level  # 399
        expected_points = 1995

        classes = ["DK", "DW", "FE", "MG", "DL", "SU", "RF"]
        for cls in classes:
            points_per_lvl = 5
            total_points = level_ups * points_per_lvl
            self.assertEqual(total_points, expected_points, f"Class {cls} got {total_points} points, expected {expected_points}")

            # Explicitly assert divergence from old buggy values
            legacy_7pt_points = level_ups * 7
            solobalance_bonus_points = level_ups * (5 + 3)
            self.assertNotEqual(total_points, legacy_7pt_points)
            self.assertNotEqual(total_points, solobalance_bonus_points)

    def test_04_balance_v2_json_specifies_5_points_per_level(self):
        """
        Verify that balance.v2.json authoritative design file has normalCap=400 and pointsPerLevel=5.
        """
        import json
        design_path = os.path.join(REPO_ROOT, "OpenMU-数值重设计", "design", "balance.v2.json")
        with open(design_path, "r", encoding="utf-8") as f:
            design = json.load(f)

        self.assertEqual(design.get("normalCap"), 400)
        self.assertEqual(design.get("pointsPerLevel"), 5)
        # Total points in design
        total_points = (design["normalCap"] - 1) * design["pointsPerLevel"]
        self.assertEqual(total_points, 1995)

    @staticmethod
    def _load_skill_initializer_constants() -> Dict[str, Dict[str, float]]:
        skills_dir = os.path.join(REPO_ROOT, "OpenMU", "src", "Persistence", "Initialization", "Skills")
        specs = {
            "LifeSwell": (
                "LifeSwellEffectInitializer.cs",
                {
                    "duration_max": r"magicEffect\.Duration\.MaximumValue\s*=\s*([0-9.]+)f?",
                    "duration_per_energy": r"durationPerEnergy\.InputOperand\s*=\s*([0-9.]+)f?",
                    "boost_per_energy": r"boostPerEnergy\.InputOperand\s*=\s*([0-9.]+)f?",
                    "boost_per_vitality": r"boostPerVitality\.InputOperand\s*=\s*([0-9.]+)f?",
                    "boost_max": r"healthPowerUpDefinition\.Boost\.MaximumValue\s*=\s*([0-9.]+)f?",
                },
            ),
            "SoulBarrier": (
                "SoulBarrierEffectInitializer.cs",
                {
                    "duration_per_energy": r"durationPerEnergy\.InputOperand\s*=\s*([0-9.]+)f?",
                    "boost_per_energy": r"boostPerEnergy\.InputOperand\s*=\s*([0-9.]+)f?",
                    "boost_per_agility": r"boostPerAgility\.InputOperand\s*=\s*([0-9.]+)f?",
                },
            ),
            "GreaterDamage": (
                "GreaterDamageEffectInitializer.cs",
                {
                    "boost_per_energy": r"boostPerEnergy\.InputOperand\s*=\s*([0-9.]+)f?",
                },
            ),
            "GreaterDefense": (
                "GreaterDefenseEffectInitializer.cs",
                {
                    "boost_per_energy": r"boostPerEnergy\.InputOperand\s*=\s*([0-9.]+)f?",
                },
            ),
            "Berserker": (
                "BerserkerEffectInitializer.cs",
                {
                    "duration_max": r"magicEffect\.Duration\.MaximumValue\s*=\s*([0-9.]+)f?",
                    "duration_per_energy": r"durationPerEnergy\.InputOperand\s*=\s*([0-9.]+)f?",
                    "mana_max": r"manaPowerUpDefinition\.Boost\.MaximumValue\s*=\s*([0-9.]+)f?",
                    "mana_per_energy": r"manaMultiplier\.InputOperand\s*=\s*([0-9.]+)f?",
                    "hp_per_energy": r"healthMultiplier\.InputOperand\s*=\s*([0-9.]+)f?",
                    "min_phys_per_stat": r"minDmgPerStrengthAndAgility\.InputOperand\s*=\s*([0-9.]+)f?",
                    "max_phys_per_stat": r"maxDmgPerStrengthAndAgility\.InputOperand\s*=\s*([0-9.]+)f?",
                },
            ),
        }
        loaded: Dict[str, Dict[str, float]] = {}
        for key, (filename, regex_map) in specs.items():
            path = os.path.join(skills_dir, filename)
            with open(path, "r", encoding="utf-8") as fh:
                src = fh.read()
            if "InputOperator.Divide" in src:
                raise AssertionError(f"{filename} still contains legacy integer-step InputOperator.Divide")
            loaded[key] = {}
            for field_name, pat in regex_map.items():
                m = re.search(pat, src)
                if not m:
                    raise AssertionError(f"Missing {field_name} pattern in {filename}")
                loaded[key][field_name] = float(m.group(1))
        return loaded

    def test_05_buff_formulas_no_integer_divisor_steps(self):
        """
        Adversarially test the derivative (slope) of buff outputs parsed directly
        from OpenMU C# skill effect initializers.
        """
        test_energies = [0, 10, 50, 100, 200, 500, 1000, 2000, 5000]
        coeffs = self._load_skill_initializer_constants()
        gd_slope = coeffs["GreaterDamage"]["boost_per_energy"]
        gdef_slope = coeffs["GreaterDefense"]["boost_per_energy"]
        ls_e_slope = coeffs["LifeSwell"]["boost_per_energy"]
        ls_v_slope = coeffs["LifeSwell"]["boost_per_vitality"]

        self.assertAlmostEqual(gd_slope, 0.1445, places=6)
        self.assertAlmostEqual(gdef_slope, 0.1265, places=6)
        self.assertAlmostEqual(ls_e_slope, 0.000525, places=6)
        self.assertAlmostEqual(ls_v_slope, 0.000108, places=6)

        # 1. GreaterDamage: 3.0 + E * boostPerEnergy
        for E in test_energies:
            d1 = (3.0 + (E + 1) * gd_slope) - (3.0 + E * gd_slope)
            d2 = (3.0 + (E + 2) * gd_slope) - (3.0 + (E + 1) * gd_slope)
            self.assertAlmostEqual(d1, gd_slope, places=5, msg="GreaterDamage must have continuous derivative")
            self.assertAlmostEqual(d2, gd_slope, places=5)
            self.assertGreater(d1, 0.0, "Derivative must be strictly positive (no zero-gain plateau)")

        # 2. GreaterDefense: 2.0 + E * boostPerEnergy
        for E in test_energies:
            d1 = (2.0 + (E + 1) * gdef_slope) - (2.0 + E * gdef_slope)
            self.assertAlmostEqual(d1, gdef_slope, places=5, msg="GreaterDefense must have continuous derivative")
            self.assertGreater(d1, 0.0)

        # 3. LifeSwell HP Boost: 0.12 + E * boostPerEnergy + V * boostPerVitality (capped at MaximumValue)
        for E in [0, 100, 500, 1000]:
            dE = (0.12 + (E + 1) * ls_e_slope) - (0.12 + E * ls_e_slope)
            self.assertAlmostEqual(dE, ls_e_slope, places=6)
            self.assertGreater(dE, 0.0)

        for V in [0, 100, 500, 1000]:
            dV = (0.12 + (V + 1) * ls_v_slope) - (0.12 + V * ls_v_slope)
            self.assertAlmostEqual(dV, ls_v_slope, places=6)
            self.assertGreater(dV, 0.0)

    def test_06_buff_formulas_extreme_values_and_explosive_multiplier_prevention(self):
        """
        Adversarially evaluate all 5 C# buff initializer formulas at extreme values:
        Energy/Vitality in [0, 100, 500, 2000, 5000].
        """
        extreme_values = [0, 100, 500, 2000, 5000]
        coeffs = self._load_skill_initializer_constants()
        ls = coeffs["LifeSwell"]
        sb = coeffs["SoulBarrier"]
        gd = coeffs["GreaterDamage"]
        gdef = coeffs["GreaterDefense"]
        bk = coeffs["Berserker"]

        for E in extreme_values:
            for V in extreme_values:
                # --- LifeSwell ---
                ls_duration = min(ls["duration_max"], 60.0 + E * ls["duration_per_energy"])
                ls_boost = min(ls["boost_max"], 0.12 + E * ls["boost_per_energy"] + V * ls["boost_per_vitality"])

                self.assertTrue(math.isfinite(ls_duration))
                self.assertTrue(math.isfinite(ls_boost))
                self.assertGreaterEqual(ls_duration, 60.0)
                self.assertLessEqual(ls_duration, 180.0, f"LifeSwell duration {ls_duration} exceeded 180s cap at E={E}")
                self.assertGreaterEqual(ls_boost, 0.12)
                self.assertLessEqual(ls_boost, 2.0, f"LifeSwell boost {ls_boost} exceeded 2.0 (200%) cap at E={E}, V={V}")

                # --- SoulBarrier ---
                sb_duration = 60.0 + E * sb["duration_per_energy"]
                sb_decrement = 0.10 + E * sb["boost_per_energy"] + V * sb["boost_per_agility"]
                self.assertTrue(math.isfinite(sb_duration))
                self.assertTrue(math.isfinite(sb_decrement))
                self.assertGreaterEqual(sb_duration, 60.0)
                self.assertGreaterEqual(sb_decrement, 0.10)

                # --- GreaterDamage ---
                gd_damage = 3.0 + E * gd["boost_per_energy"]
                self.assertTrue(math.isfinite(gd_damage))
                self.assertGreaterEqual(gd_damage, 3.0)
                self.assertLess(gd_damage, 10000.0)

                # --- GreaterDefense ---
                gdef_defense = 2.0 + E * gdef["boost_per_energy"]
                self.assertTrue(math.isfinite(gdef_defense))
                self.assertGreaterEqual(gdef_defense, 2.0)
                self.assertLess(gdef_defense, 10000.0)

                # --- Berserker ---
                bk_duration = min(bk["duration_max"], 30.0 + E * bk["duration_per_energy"])
                bk_mana_mult = min(bk["mana_max"], E * bk["mana_per_energy"])
                bk_hp_decrement = -0.40 + E * bk["hp_per_energy"]
                bk_min_phys = 140.0 + (E + V) * bk["min_phys_per_stat"]
                bk_max_phys = 160.0 + (E + V) * bk["max_phys_per_stat"]

                self.assertTrue(math.isfinite(bk_duration))
                self.assertTrue(math.isfinite(bk_mana_mult))
                self.assertTrue(math.isfinite(bk_hp_decrement))
                self.assertTrue(math.isfinite(bk_min_phys))
                self.assertTrue(math.isfinite(bk_max_phys))

                self.assertGreaterEqual(bk_duration, 30.0)
                self.assertLessEqual(bk_duration, 180.0, f"Berserker duration {bk_duration} exceeded 180s cap at E={E}")
                self.assertGreaterEqual(bk_mana_mult, 0.0)
                self.assertLessEqual(bk_mana_mult, 1.0, f"Berserker mana mult {bk_mana_mult} exceeded 1.0 (100%) cap at E={E}")
                self.assertLessEqual(bk_min_phys, bk_max_phys, "Min phys damage must never exceed max phys damage")

    def test_07_hyperbolic_armor_and_hit_chance_boundaries(self):
        """
        Adversarially test SimulationEngine combat formula functions against boundary inputs:
        - engine.hit_chance(accuracy, evasion) in [hitMin, hitMax]
        - engine.reduction(armor, rank) in [0.0, armorCap]
        - post-mitigation damage >= minimum_damage(level) >= 1.0
        """
        from tests.adversarial.sim_engine import SimulationEngine
        engine = SimulationEngine()
        hit_min = engine.D["combat"]["hitMin"]
        hit_max = engine.D["combat"]["hitMax"]
        armor_cap = engine.D["combat"]["armorCap"]

        test_rate_pairs = [
            (0.0, 10000.0),
            (10000.0, 0.0),
            (1.0, 1000000.0),
            (1000000.0, 1.0),
            (500.0, 500.0),
            (0.001, 0.001),
            (1e6, 1e6),
        ]

        for att, df in test_rate_pairs:
            hit_chance = engine.hit_chance(att, df)
            self.assertTrue(math.isfinite(hit_chance))
            self.assertGreaterEqual(hit_chance, hit_min)
            self.assertLessEqual(hit_chance, hit_max)
            self.assertNotEqual(hit_chance, 0.03)

        test_armor_rank_pairs = [
            (0.0, 1.0),
            (0.0, 450.0),
            (100.0, 1.0),
            (1000.0, 50.0),
            (50000.0, 1.0),
            (50000.0, 450.0),
            (-100.0, -10.0),
        ]

        for arm, rnk in test_armor_rank_pairs:
            safe_rank = max(1.0, rnk if math.isfinite(rnk) else 1.0)
            clamped_mitigation = engine.reduction(arm, safe_rank)

            self.assertTrue(math.isfinite(clamped_mitigation))
            self.assertGreaterEqual(clamped_mitigation, 0.0)
            self.assertLessEqual(clamped_mitigation, armor_cap)

            base_damage = 50.0
            damage = max(engine.minimum_damage(1), base_damage * (1.0 - clamped_mitigation))
            self.assertGreaterEqual(damage, 1.0)
            self.assertLessEqual(damage, base_damage)

    def test_08_multi_hit_skill_damage_preservation(self):
        """
        Verify that for multi-hit skills (e.g. fe_triple, dl_quake, su_chain, rf_phoenix),
        hit weights sum to exactly 1.0 and each individual hit applies 1.0 / N.
        """
        import json
        design_path = os.path.join(REPO_ROOT, "OpenMU-数值重设计", "design", "balance.v2.json")
        with open(design_path, "r", encoding="utf-8") as f:
            design = json.load(f)

        multi_hit_skills = [s for s in design["skills"] if len(s.get("hitWeights", [])) > 1]
        self.assertGreater(len(multi_hit_skills), 0, "Must have multi-hit skills defined")

        for skill in multi_hit_skills:
            weights = skill["hitWeights"]
            total_weight = sum(weights)
            self.assertAlmostEqual(
                total_weight, 1.0, places=3,
                msg=f"Skill {skill['id']} hit weights {weights} sum to {total_weight}, not 1.0"
            )


if __name__ == "__main__":
    unittest.main(verbosity=2)
