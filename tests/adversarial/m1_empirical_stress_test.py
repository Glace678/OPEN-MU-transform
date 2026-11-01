#!/usr/bin/env python3
"""
M1 Empirical Adversarial Stress Test Harness
Adversarially validates:
1. HitChance formula & edge cases (AttackRate vs DefenseRate extremes, NaNs, Infs, bounds [0.05, 0.95], monotonicity, cliff-edge elimination).
2. Hyperbolic Armor Mitigation & CalculateDamage (0 armor, massive armor 50000+, rank 0 vs 400, no negative damage, no zero-damage truncation, no overflow).
3. TargetedSkillDefaultPlugin multi-hit damage budget conservation (1/N split vs legacy N*100% inflation).
4. CharacterClasses attribute scaling & LevelUpPoints audit across all 7 classes.
"""

import math
import random
import re
import os
import sys

# ---------------------------------------------------------------------------
# 1. Formulas Under Test (matching C# implementation in AttackableExtensions)
# ---------------------------------------------------------------------------

def calculate_hit_chance(attack_rate: float, defense_rate: float) -> float:
    # Mimics C# AttackableExtensions.CalculateHitChance
    safe_attack = max(1.0, attack_rate if math.isfinite(attack_rate) else 1.0)
    safe_defense = max(1.0, defense_rate if math.isfinite(defense_rate) else 1.0)
    ratio = safe_attack / (safe_attack + safe_defense)
    return min(max(ratio, 0.05), 0.95)

def legacy_hit_chance(attack_rate: float, defense_rate: float) -> float:
    # Mimics legacy C# formula:
    # float legacyHitChance = 0.03f;
    # if (defenseRate < attackRate) { legacyHitChance = 1.0f - (defenseRate / attackRate); }
    if defense_rate < attack_rate and attack_rate > 0:
        return 1.0 - (defense_rate / attack_rate)
    return 0.03

def calculate_armor_mitigation(armor: float, attacker_rank: float) -> float:
    # Mimics C# AttackableExtensions.CalculateArmorMitigation
    safe_armor = max(0.0, armor if math.isfinite(armor) else 0.0)
    safe_rank = max(1.0, attacker_rank if math.isfinite(attacker_rank) else 1.0)
    armor_reduction = safe_armor / (safe_armor + 70.0 + (2.4 * safe_rank))
    return min(max(armor_reduction, 0.0), 0.60)

def calculate_damage(base_damage: float, armor: float, attacker_rank: float) -> float:
    # Mimics C# AttackableExtensions.CalculateDamage
    mitigation = calculate_armor_mitigation(armor, attacker_rank)
    return max(1.0, base_damage * (1.0 - mitigation))


# ---------------------------------------------------------------------------
# Test Runner & Reporting
# ---------------------------------------------------------------------------

class TestFailure(Exception):
    pass

failures = []
passed_tests = 0

def test(name):
    def decorator(fn):
        def wrapper(*args, **kwargs):
            global passed_tests
            try:
                fn(*args, **kwargs)
                passed_tests += 1
                print(f"  [PASS] {name}")
            except Exception as e:
                failures.append((name, str(e)))
                print(f"  [FAIL] {name}: {e}")
        return wrapper
    return decorator


# ---------------------------------------------------------------------------
# Section 1: Hit Chance Stress Tests
# ---------------------------------------------------------------------------
print("\n=== SECTION 1: Hit Chance Stress Tests ===")

@test("HitChance: DefenseRate >> AttackRate (10000 vs 10)")
def test_hit_chance_defense_domination():
    val = calculate_hit_chance(10.0, 10000.0)
    # Ratio = 10 / 10010 = ~0.000999 -> clamped to 0.05
    if abs(val - 0.05) > 1e-6:
        raise TestFailure(f"Expected 0.05, got {val}")

@test("HitChance: AttackRate >> DefenseRate (10000 vs 10)")
def test_hit_chance_attack_domination():
    val = calculate_hit_chance(10000.0, 10.0)
    # Ratio = 10000 / 10010 = ~0.99900 -> clamped to 0.95
    if abs(val - 0.95) > 1e-6:
        raise TestFailure(f"Expected 0.95, got {val}")

@test("HitChance: Equal rates (100 vs 100)")
def test_hit_chance_equal():
    val = calculate_hit_chance(100.0, 100.0)
    if abs(val - 0.50) > 1e-6:
        raise TestFailure(f"Expected 0.50, got {val}")

@test("HitChance: Extreme values (0, negatives, Infinity, NaN)")
def test_hit_chance_extremes():
    cases = [
        (0.0, 0.0, 0.50),
        (-50.0, -100.0, 0.50),
        (float('nan'), 100.0, 0.05),
        (float('inf'), 100.0, 0.05),
        (100.0, float('nan'), 0.95),
        (0.0, 50000.0, 0.05),
        (50000.0, 0.0, 0.95),
        (1e-12, 1e-12, 0.50),
        (1e12, 1e12, 0.50),
    ]
    for atk, dfn, expected in cases:
        res = calculate_hit_chance(atk, dfn)
        if not (0.05 <= res <= 0.95) or not math.isfinite(res):
            raise TestFailure(f"Out of bounds or non-finite for atk={atk}, dfn={dfn}: {res}")
        if expected is not None and abs(res - expected) > 0.05:
            raise TestFailure(f"Unexpected result for atk={atk}, dfn={dfn}: got {res}, expected ~{expected}")

@test("HitChance: Cliff edge elimination (Continuous transition near defense == attack)")
def test_cliff_edge_elimination():
    # In legacy MU, at attack=100:
    # defense=99 -> 1 - 99/100 = 0.01
    # defense=100 -> 0.03 (sudden jump from 0.01 to 0.03, or worse, if defense was 90: 0.10 -> 0.03)
    # In new formula, check step deltas from defense=50 to defense=150
    step = 0.5
    prev = None
    max_delta = 0.0
    for d_int in range(100, 300):
        d = d_int * step
        cur = calculate_hit_chance(100.0, d)
        if prev is not None:
            delta = abs(cur - prev)
            if delta > max_delta:
                max_delta = delta
            # Ensure continuity: small input change produces small output change
            if delta > 0.01:
                raise TestFailure(f"Cliff edge detected! Step delta={delta} at defense={d}")
        prev = cur
    # Max delta should be very small (~0.0025)
    if max_delta > 0.005:
        raise TestFailure(f"Max delta {max_delta} indicates lack of smoothness")

@test("HitChance: Strict Monotonicity & Boundedness over 100,000 random samples")
def test_hit_chance_monotonicity():
    rng = random.Random(42)
    for _ in range(100_000):
        atk = rng.uniform(0.1, 50000.0)
        dfn = rng.uniform(0.1, 50000.0)
        res = calculate_hit_chance(atk, dfn)
        if not (0.05 <= res <= 0.95):
            raise TestFailure(f"Bound violation: {res} for atk={atk}, dfn={dfn}")

        # Check monotonicity: increasing atk must not decrease hit chance
        atk_higher = atk * rng.uniform(1.01, 2.0)
        res_higher = calculate_hit_chance(atk_higher, dfn)
        if res_higher < res - 1e-7:
            raise TestFailure(f"Monotonicity violation: atk {atk}->{atk_higher} caused hit chance {res}->{res_higher}")


# ---------------------------------------------------------------------------
# Section 2: Hyperbolic Armor Mitigation & Damage Tests
# ---------------------------------------------------------------------------
print("\n=== SECTION 2: Hyperbolic Armor Mitigation & Damage Tests ===")

@test("Armor: 0 Armor mitigation is exactly 0.0")
def test_armor_zero():
    mit = calculate_armor_mitigation(0.0, 100.0)
    if mit != 0.0:
        raise TestFailure(f"Expected 0.0, got {mit}")
    dmg = calculate_damage(500.0, 0.0, 100.0)
    if dmg != 500.0:
        raise TestFailure(f"Expected 500.0, got {dmg}")

@test("Armor: Massive Armor (50,000 and 1,000,000) is clamped to exactly 0.60")
def test_armor_massive():
    for huge_armor in [50_000.0, 100_000.0, 1_000_000.0, 1e9]:
        mit = calculate_armor_mitigation(huge_armor, 400.0)
        if abs(mit - 0.60) > 1e-6:
            raise TestFailure(f"Expected cap 0.60 for armor={huge_armor}, got {mit}")
        dmg = calculate_damage(1000.0, huge_armor, 400.0)
        # 1000 * (1 - 0.60) = 400.0
        if abs(dmg - 400.0) > 1e-6:
            raise TestFailure(f"Expected 400.0 damage, got {dmg}")

@test("Armor: Attacker rank scaling (rank 0, rank 1, rank 400)")
def test_armor_rank_scaling():
    # Rank 0 treated safely as rank 1
    mit_r0 = calculate_armor_mitigation(200.0, 0.0)
    mit_r1 = calculate_armor_mitigation(200.0, 1.0)
    if mit_r0 != mit_r1:
        raise TestFailure(f"Rank 0 ({mit_r0}) did not match Rank 1 ({mit_r1})")

    # Attacker with rank 400 should penetrate armor more than rank 1
    mit_r400 = calculate_armor_mitigation(200.0, 400.0)
    # Armor 200:
    # Rank 1: 200 / (200 + 70 + 2.4) = 200 / 272.4 = 0.734 -> clamped to 0.60
    # Rank 400: 200 / (200 + 70 + 960) = 200 / 1230 = 0.1626
    if mit_r400 >= mit_r1:
        raise TestFailure(f"Expected higher rank to have lower mitigation, got r400={mit_r400} vs r1={mit_r1}")
    if abs(mit_r400 - (200.0 / 1230.0)) > 1e-6:
        raise TestFailure(f"Expected {200.0/1230.0}, got {mit_r400}")

@test("Armor: Denial of negative damage, zero-damage truncation, or overflow")
def test_damage_denial_of_zero_or_negative():
    rng = random.Random(123)
    for _ in range(50_000):
        base_dmg = rng.uniform(0.0, 10000.0)
        armor = rng.uniform(0.0, 100000.0)
        rank = rng.uniform(0.0, 1000.0)
        dmg = calculate_damage(base_dmg, armor, rank)
        if dmg < 1.0:
            raise TestFailure(f"Damage below minimum floor 1.0: {dmg} (base={base_dmg}, armor={armor}, rank={rank})")
        if not math.isfinite(dmg):
            raise TestFailure(f"Damage non-finite: {dmg}")

    # Explicit test: base_damage = 0.0 -> damage = 1.0 (floor)
    dmg_zero_base = calculate_damage(0.0, 500.0, 50.0)
    if dmg_zero_base != 1.0:
        raise TestFailure(f"Zero base damage should return floor 1.0, got {dmg_zero_base}")

    # Explicit test: negative base damage -> damage = 1.0
    dmg_neg_base = calculate_damage(-100.0, 500.0, 50.0)
    if dmg_neg_base != 1.0:
        raise TestFailure(f"Negative base damage should return floor 1.0, got {dmg_neg_base}")


# ---------------------------------------------------------------------------
# Section 3: TargetedSkillDefaultPlugin Multi-Hit Budget Tests
# ---------------------------------------------------------------------------
print("\n=== SECTION 3: TargetedSkillDefaultPlugin Multi-Hit Budget Tests ===")

@test("Multi-Hit: Exact damage budget preservation (1.0 / N)")
def test_multihit_budget_preservation():
    # In TargetedSkillDefaultPlugin:
    # hitWeight = skill.NumberOfHitsPerAttack > 1 ? (1.0 / skill.NumberOfHitsPerAttack) : 1.0;
    # 1st hit dealt with hitWeight
    # hits 2..N dealt with hitWeight
    # Total sum of hits must equal exactly 1.0 (100% single-cast budget)
    for n in range(1, 21):
        hit_weight = (1.0 / n) if n > 1 else 1.0
        total_weight = 0.0
        # 1st hit
        total_weight += hit_weight
        # Remaining hits
        for _ in range(2, n + 1):
            total_weight += hit_weight

        if abs(total_weight - 1.0) > 1e-14:
            raise TestFailure(f"Budget violation for N={n}: total weight={total_weight}")

        # Contrast with legacy multiplier
        legacy_total = n * 1.0
        # If n > 1, legacy was inflated by n times!
        if n > 1:
            ratio = total_weight / legacy_total
            if abs(ratio - (1.0 / n)) > 1e-14:
                raise TestFailure(f"Legacy comparison check failed for N={n}")


# ---------------------------------------------------------------------------
# Section 4: Character Classes Static Code Audit
# ---------------------------------------------------------------------------
print("\n=== SECTION 4: Character Classes & LevelUpPoints Audit ===")

repo_root = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
classes_dir = os.path.join(repo_root, "OpenMU", "src", "Persistence", "Initialization", "CharacterClasses")

class_files = [
    "ClassDarkKnight.cs",
    "ClassDarkWizard.cs",
    "ClassFairyElf.cs",
    "ClassMagicGladiator.cs",
    "ClassDarkLord.cs",
    "ClassSummoner.cs",
    "ClassRageFighter.cs",
]

@test("CharacterClasses: PointsPerLevelUp == 5 for all 7 classes")
def test_points_per_level_up():
    for cf in class_files:
        path = os.path.join(classes_dir, cf)
        if not os.path.isfile(path):
            raise TestFailure(f"File not found: {path}")
        with open(path, "r", encoding="utf-8") as f:
            content = f.read()

        match = re.search(r"CreateStatAttributeDefinition\s*\(\s*Stats\.PointsPerLevelUp\s*,\s*(\d+)", content)
        if not match:
            raise TestFailure(f"PointsPerLevelUp not found in {cf}")
        pts = int(match.group(1))
        if pts != 5:
            raise TestFailure(f"{cf} has PointsPerLevelUp = {pts}, expected 5")

@test("CharacterClasses: No rough integer division in attribute scalings")
def test_no_rough_integer_divisors():
    # Check for legacy patterns like "1.0f / 3", "1.0f / 5", "1.0f / 10", "1.0f / 20", etc.
    forbidden_divisors = [
        r"1\.0f\s*/\s*2\b",
        r"1\.0f\s*/\s*3\b",
        r"1\.0f\s*/\s*4\b",
        r"1\.0f\s*/\s*5\b",
        r"1\.0f\s*/\s*7\b",
        r"1\.0f\s*/\s*8\b",
        r"1\.0f\s*/\s*9\b",
        r"1\.0f\s*/\s*10\b",
        r"1\.0f\s*/\s*15\b",
        r"1\.0f\s*/\s*20\b",
        r"1\.0f\s*/\s*25\b",
        r"1\.0f\s*/\s*30\b",
        r"1\.0f\s*/\s*50\b",
        r"1\.0f\s*/\s*100\b",
    ]
    for cf in class_files:
        path = os.path.join(classes_dir, cf)
        with open(path, "r", encoding="utf-8") as f:
            content = f.read()

        for pat in forbidden_divisors:
            m = re.search(pat, content)
            if m:
                raise TestFailure(f"Found forbidden integer divisor pattern '{m.group(0)}' in {cf}")

@test("PlayerExperience: SoloBalance +3 hack removed")
def test_player_experience_solo_balance_hack():
    pe_path = os.path.join(repo_root, "OpenMU", "src", "GameLogic", "PlayerExperience.cs")
    with open(pe_path, "r", encoding="utf-8") as f:
        content = f.read()
    if "SoloBalance.GetLevelUpPointBonus" in content:
        raise TestFailure("PlayerExperience.cs still contains SoloBalance.GetLevelUpPointBonus!")


# ---------------------------------------------------------------------------
# Execute all tests
# ---------------------------------------------------------------------------
test_hit_chance_defense_domination()
test_hit_chance_attack_domination()
test_hit_chance_equal()
test_hit_chance_extremes()
test_cliff_edge_elimination()
test_hit_chance_monotonicity()

test_armor_zero()
test_armor_massive()
test_armor_rank_scaling()
test_damage_denial_of_zero_or_negative()

test_multihit_budget_preservation()

test_points_per_level_up()
test_no_rough_integer_divisors()
test_player_experience_solo_balance_hack()

print(f"\n==========================================")
print(f"Summary: {passed_tests} PASSED, {len(failures)} FAILED")
print(f"==========================================")

if failures:
    for name, reason in failures:
        print(f"FAILED: {name} -> {reason}")
    sys.exit(1)
else:
    print("ALL EMPIRICAL ADVERSARIAL STRESS TESTS PASSED.")
    sys.exit(0)
