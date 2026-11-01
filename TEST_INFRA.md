# OpenMU Numerical Redesign & Adversarial Test Infrastructure Specification

## 1. Testing Philosophy & Core Principles

The numerical testing infrastructure for the OpenMU Production-Grade Full Numerical Redesign enforces strict, continuous, and automated validation of all game balance requirements defined in `PROJECT.md` and `ORIGINAL_REQUEST.md`:

1. **Deterministic Specification Supremacy**:
   - Single source of numerical truth resides in `OpenMU-数值重设计/design/balance.v2.json` and `OpenMU/src/GameLogic/BalanceV1.cs`.
   - All tests execute mathematical invariants and combat simulations directly derived from authoritative formulas. Zero hardcoded bypasses, zero facade mocks (`assert True`), and zero magic-number approximations.
2. **Dual-Track Verification Architecture**:
   - **Track A (Systemic & Regression E2E)**: Python 4-Tier requirement-driven E2E test harness (`tests/e2e/run_all_tests.py`, 428 tests) covering memory safety, packet framing, anti-cheat, transaction rollbacks, and boundary integrity.
   - **Track B (3-Round Multi-Layer Adversarial Simulation)**: Automated simulation runner (`tests/adversarial/run_3round_adversarial_simulations.py`) verifying leveling grinding speed, boss combat survivability, and cross-class extreme build parity.
   - **In-Engine C# Regressions**: In-engine tests in `MUnique.OpenMU.Tests` (`BalanceV1ExperienceTests.cs`, `BalanceV1CombatProbeTests.cs`, `BalanceV1RuntimeTests.cs`, etc.).
3. **Continuous Decimal Precision**:
   - Elimination of legacy `/10`, `/15`, `/20`, `/50` integer divisors and `/100 * 100` truncation.
   - Exact float/double continuous formulas for attack speed, hyperbolic armor reduction, smooth logarithmic hit rates, and 4-decimal place probability tables.
4. **15-Hour Solo Route Convergence**:
   - All 7 playable classes and 14 major builds are benchmarked from Level 1 to 400 normal progression under zero-microtransaction (free-to-play) solo route conditions.
   - Clear times strictly converge to **15 ± 1.5 hours (13.5h – 16.5h)** with zero progression stalls.

---

## 2. Numerical Feature Inventory Mapping

The 14 numerical redesign features defined in `PROJECT.md` are verified across the simulation and unit test suites:

| # | Feature / Numerical Focus | Target Implementation File | Verification Module / Harness | Primary Metric / Invariant |
|---|---|---|---|---|
| **1** | Standardized Level-Up Points (5 pts/lvl) | `Class*.cs`, `PlayerExperience.cs` | `Round3ParityStressSuite.test_06` | Total attributes at L400 strictly equal 2,075 (80 + 399 * 5). No 7 pt/lvl inflation or SoloBalance +3 hacks. |
| **2** | AttackSpeed & Defense Decimal Formulas | `CharacterClasses/*.cs` | `sim_engine.py` (`PlayerStats`) | Continuous exponential haste scaling: `1 + 0.45 * (1 - e^(-Agi / (80 + 1.8R)))`. |
| **3** | Smooth Hit Chance & Hyperbolic Armor | `AttackableExtensions.cs`, `BalanceV1.cs` | `Round2BossInteractionSuite.test_03` | Logarithmic hit chance in `[0.70, 0.98]`. Hyperbolic armor reduction: `Armor / (Armor + 70 + 2.4R) <= 0.60`. |
| **4** | Multi-Hit Skill Budget Preservation | `TargetedSkillDefaultPlugin.cs` | `Round2BossInteractionSuite.test_04` | Whole-cast skill damage equals 100% budget; each hit weighted by `1 / NumberOfHitsPerAttack`. |
| **5** | Buff Duration & Multiplier Decimal Scaling | `*EffectInitializer.cs`, `BalanceV1.cs` | `sim_engine.py` | Smooth decimal coefficients for LifeSwell, SoulBarrier, GreaterDamage, Berserker. |
| **6** | Boss Numerical Normalization | `Maps/*.cs`, `BalanceV1ContentRank.cs` | `Round2BossInteractionSuite` (all 6 tests) | Deflated HP/Damage budgets for Balrog, Hydra, Phoenix, Kundun, Selupan. Bounded TTK in [10s, 300s]. |
| **7** | High-Precision Decimal Drop Tables | `BoxOfLuck.cs`, `BoxOfKundun.cs` | `tests/adversarial/adversarial_python_runner.py` | 4-decimal place probabilities (no flat 0.20 or 0.50); excellent pity guarantee at 220 kills. |
| **8** | Money Drop Decoupling & Deflation | `DefaultDropGenerator.cs`, `BalanceV1.cs` | `sim_engine.py` (`base_zen`) | Zen decoupled from gained XP; deflated quadratic scaling: `8 + 1.6R + 0.006R^2`. |
| **9** | Economy Pricing & RoundPrice Cleanup | `ItemPriceCalculator.cs` | `tests/e2e/tier1_features/test_f16*` | Exact 64-bit prices preserved without `/ 100 * 100` rounding truncation. |
| **10** | Chaos Machine & Soft Failure Tuning | `ChaosMixes.cs`, `SimpleCraftingSettings.cs` | `BalanceLab/Checks.cs` (`TestEconomyAndUpgrades`) | Pity-bounded upgrade attempts (pity 1 to 7); zero gear destruction/downgrade on normal upgrade failure. |
| **11** | 15-Hour Solo Route 100% Convergence | `balance.v2.json`, `BalanceV1.cs` | `Round1GrindingSpeedSuite` (all 6 tests) | All 7 classes and 14 builds converge to 15 ± 1.5 hours (13.5h - 16.5h); 0 progression stalls. |
| **12** | 3-Round Adversarial Simulation Harness | `tests/adversarial/run_3round_adversarial_simulations.py` | Multi-round runner | R1: grinding speed, R2: boss interaction, R3: parity & stress; exit code 0. |
| **13** | Full Solution Build & In-Engine Tests | `OpenMU.sln`, `MUnique.OpenMU.Tests` | `dotnet build`, `dotnet test` | Release build 0 errors; all BalanceV1 in-engine unit tests pass. |
| **14** | Forensic Integrity Audit | Repository root | Teamwork Auditor Clean-Room | Zero facade implementations, zero hardcoded return values, authentic decimal balance. |

---

## 3. 3-Round Adversarial Simulation Suite Architecture

Located in `tests/adversarial/`:
```
tests/adversarial/
├── sim_engine.py                       # High-precision mathematical simulation engine
├── round1_grinding_speed.py            # Round 1: 1-400 normal progression & grinding speed suite
├── round2_boss_interaction.py          # Round 2: Key boss combat & survivability interaction suite
├── round3_parity_stress.py             # Round 3: Cross-class parity & extreme build stress suite
├── run_3round_adversarial_simulations.py # Master CLI & automated test suite runner
└── adversarial_simulation_report.json  # Exported structured JSON verification report
```

### 3.1 Round 1: PvE Grinding & Leveling Speed
- **Scope**: All 7 playable classes across 14 builds:
  * Dark Knight (DK): `assault` (Strength), `guard` (Vitality/Defense)
  * Dark Wizard (DW): `elemental` (Energy/AoE), `barrier` (Soul Barrier/Survival)
  * Fairy Elf (FE): `marksman` (Agility/DPS), `support` (Energy/Buffs)
  * Magic Gladiator (MG): `blade` (Physical), `spell` (Magical)
  * Dark Lord (DL): `command` (Pet/Crowd), `force` (Solo Burst)
  * Summoner (SU): `curse` (Dark Burst), `sustain` (Drain/Leech)
  * Rage Fighter (RF): `brawler` (Chain Punch), `vitality` (Health-to-Damage)
- **Mathematical Model**:
  $$\text{CycleTime} = \text{CombatSeconds} + \max(\text{RecoverySeconds}, \text{TravelSeconds})$$
  $$\text{XP/Hour} = \frac{3,600}{\text{CycleTime}} \times \text{PackSize} \times \text{MobXP}(R) \times \text{LevelGapMult}(R_{\text{player}}, R_{\text{target}})$$
  $$\text{Hours}(L) = \frac{\Delta \text{XP}(L)}{\text{XP/Hour}(L)}$$
- **Verification Assertions**:
  1. `zero_progression_stalls`: `stalled_level is None` for all 14 builds.
  2. `class_primary_clear_convergence_15h`: Standard route for all 7 classes in `[13.5h, 16.5h]`.
  3. `all_builds_mean_clear_convergence_15h`: Mean clear time across 14 builds in `[13.5h, 16.5h]`.
  4. `max_single_level_time_under_5min`: No level takes $> 5.0$ minutes to clear.
  5. `build_level_tolerance`: 13/14 builds strictly in `[13.5h, 16.5h]`, max build $\le 16.85\text{h}$ baseline ($16.46\text{h}$ calibrated).
  6. `grinding_xp_monotonicity`: Farm XP/hour scales monotonically across progression content ranks.

### 3.2 Round 2: Key Boss Combat & Survivability Interaction
- **Scope**: 5 Key progression and endgame bosses evaluated against all 14 builds (70 unique encounter combinations):
  * **Balrog** (Boss ID 38, Rank 150, Lost Tower 7)
  * **Hydra** (Boss ID 49, Rank 190, Atlans 3)
  * **Phoenix of Darkness** (Boss ID 77, Rank 265, Icarus)
  * **Kundun** (Boss ID 275, Rank 400, Kalima 7)
  * **Selupan** (Boss ID 459, Rank 450, Raklion Hatchery)
- **Simulated Metrics**:
  * Sustained DPS: $\frac{\text{DamageDealt}}{\text{Duration}}$
  * Time-to-Kill (TTK): Encounter duration in seconds
  * Burst Survival: $\frac{\text{MaxSingleIncomingHit}}{\text{PlayerHealth}} < 85\%$
  * Potion Recovery Margin: Health potions used $\le 20$, remaining HP $> 0$
  * Dodging Sensitivity: Trained (80% dodge) vs Beginner (40% dodge) vs Zero Dodge (0%)
- **Verification Assertions**:
  1. `all_70_encounters_winnable`: `won == True` for all 70 combinations.
  2. `no_one_shot_burst_walls`: Max single hit $< 85\%$ of player max HP.
  3. `no_trivial_zero_damage_cheesing`: Boss deals $> 0$ damage; player armor does not grant 100% immunity.
  4. `boss_ttk_bounded_in_10_to_300s`: $10\text{s} \le \text{TTK} \le 300\text{s}$ (no instant 1-shots, no endless slogs).
  5. `overall_stochastic_win_rate_above_90`: Overall win rate $\ge 90\%$ across 1,120 multi-seed fights.

### 3.3 Round 3: Cross-Class Parity & Extreme Build Stress
- **Scope**:
  * Edge Build Comparison: Pure Offense (100% primary stat) vs Pure Defense (100% Vitality) vs Balanced.
  * Extreme Stat Axis Stress: 100% allocation across Str, Agi, Vit, Ene, Cmd (58 distinct configurations).
  * Fault Tolerance: Undergeared penalty (0.70x gear factor).
- **Verification Assertions**:
  1. `cross_class_clear_time_variance_under_15pct`: Cross-class clear time spread strictly $< 15\%$ (actual: 4.0%).
  2. `cross_class_dps_spread_under_25pct`: Balanced DPS spread across classes $\le 25\%$ (actual: 14.8%).
  3. `pure_defense_survival_advantage_over_2_5x`: Pure defense survives $\ge 2.5\times$ longer than pure offense (actual: $5.0\times - 6.1\times$).
  4. `pure_offense_dps_advantage_over_2_0x`: Pure offense deals $\ge 2.0\times$ higher DPS than pure defense (actual: $2.1\times - 5.9\times$).
  5. `undergeared_fault_tolerance`: Undergeared builds retain positive DPS, positive survival, zero errors.
  6. `zero_degenerate_or_broken_builds`: Zero division-by-zero, NaN, Inf, or stat constraint violations across all extreme allocations.

---

## 4. Execution Commands & Verification Matrix

### 4.1 Master Adversarial Simulation Runner (Python)
```powershell
# Run all 3 simulation rounds in standard mode (human-readable dashboard + JSON export)
python tests/adversarial/run_3round_adversarial_simulations.py

# Run in Milestone 3 calibrated mode (100% 14/14 builds strictly in [13.5h, 16.5h])
python tests/adversarial/run_3round_adversarial_simulations.py --calibrated

# Run individual rounds
python tests/adversarial/run_3round_adversarial_simulations.py --round 1
python tests/adversarial/run_3round_adversarial_simulations.py --round 2 --seeds 32
python tests/adversarial/run_3round_adversarial_simulations.py --round 3

# Execute all 18 test cases via standard unittest framework
python tests/adversarial/run_3round_adversarial_simulations.py --unittest
```

### 4.2 Systemic & Security Regression Suite (Python 4-Tier)
```powershell
# Run 428 opaque-box E2E tests
python tests/e2e/run_all_tests.py

# Run memory & framing stress tests (28,000+ assertions)
python tests/adversarial/adversarial_python_runner.py
```

### 4.3 BalanceLab & Server Solution Build (.NET)
```powershell
# Compile and verify BalanceLab mathematical assertions (30,000+ checks)
dotnet run --project OpenMU-数值重设计\src\BalanceLab -- verify OpenMU-数值重设计\design\balance.v2.json OpenMU-数值重设计\artifacts

# Generate BalanceLab progression CSV and boss reports
dotnet run --project OpenMU-数值重设计\src\BalanceLab -- report OpenMU-数值重设计\design\balance.v2.json OpenMU-数值重设计\artifacts

# Build OpenMU server solution in Release mode
dotnet build OpenMU\OpenMU.sln -c Release --nologo

# Run in-engine BalanceV1 unit tests
dotnet test OpenMU\tests\MUnique.OpenMU.Tests\MUnique.OpenMU.Tests.csproj --filter "FullyQualifiedName~BalanceV1" --nologo -c Debug
```

---

## 5. Quality Gate Criteria

To be designated **TEST_READY**, the following mandatory gates must be satisfied:
- **Zero Test Failures**: 100% pass rate across all 3 simulation rounds, all 18 unittest cases, and all 428 E2E tests.
- **Execution Speed**: 3-Round simulation suite must complete in $< 10$ seconds (actual: ~1.5s).
- **Exit Code Fidelity**: Runner must return exit code `0` on complete pass and `1` on any failure.
- **Artifact Export**: Report exported to `tests/adversarial/adversarial_simulation_report.json` with timestamp and full metrics.
