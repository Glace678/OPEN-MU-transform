# Project: OpenMU Production-Grade Full Numerical Redesign

## Architecture
- **OpenMU Server**: Modular game server in C# / .NET 10 (`MUnique.OpenMU.sln`):
  * `GameLogic`: Core game state, combat mechanics (`AttackableExtensions.cs`), skill processing (`TargetedSkillDefaultPlugin.cs`), drop generation (`DefaultDropGenerator.cs`), item pricing (`ItemPriceCalculator.cs`), and numerical balance engine (`BalanceV1.cs`, `BalanceV1NormalMonsterBudget.cs`, `BalanceV1Economy.cs`).
  * `Persistence/Initialization`: Game configuration scripts (`GameConfigurationInitializerBase.cs`), character class initializers (`CharacterClasses/`), map monsters & bosses (`VersionSeasonSix/Maps/`, `Version075/Maps/`), drops & boxes (`Items/BoxOfLuck.cs`), chaos mixes (`ChaosMixes.cs`), and gates (`Gates.cs`).
  * `DataModel`: Configuration entities, monster attributes (`MonsterAttribute.cs`), crafting settings (`SimpleCraftingSettings.cs`).
  * `Startup`: Server bootstrapping and data initialization pipelines.
- **Numerical Laboratory (`OpenMU-数值重设计`)**:
  * `design/balance.v2.json`: Authoritative production numerical specification for all 7 classes, 18 stages, 14 builds, monster scaling, drop probabilities, and economic tables.
  * `src/BalanceLab`: High-performance deterministic simulation and verification engine (`Simulation.cs`, `Rules.cs`).
  * `artifacts/`: Exported progression benchmarks (`progression.csv`) and verification reports.
- **Dual-Track Testing & Adversarial Verification Infrastructure**:
  * `tests/e2e/`: 4-Tier requirement-driven E2E test runner (`run_all_tests.py`).
  * `tests/adversarial/`: Multi-round adversarial simulation test runner (`adversarial_python_runner.py` & automated balance suites).
  * `OpenMU/tests/`: In-engine unit and regression test projects (`MUnique.OpenMU.Tests`).

## Feature Inventory
Every feature from the Survey phase appears here with its assigned milestone:
| # | Feature / Numerical Focus | Description | Milestone | Source |
|---|---------------------------|-------------|-----------|--------|
| 1 | Standardized Level-Up Points (5 pts/lvl) | Eliminate 7 pt/lvl distortion (MG/DL/RF) and legacy SoloBalance (+3) hacks; standardize to 5 pts/lvl across all classes (1,995 total points) | M1 | explorer_survey_num_1 |
| 2 | AttackSpeed & Defense Decimal Formulas | Replace rough integer divisors (/10, /15, /20, /50) with continuous decimal formulas | M1 | explorer_survey_num_1 |
| 3 | Smooth Hit Chance & Hyperbolic Armor | Eliminate 3% hit rate cliff; replace subtractive defense with hyperbolic armor mitigation curve | M1 | explorer_survey_num_1 |
| 4 | Multi-Hit Skill Damage Budget Preservation | Fix TargetedSkillDefaultPlugin multi-hit loop to split damage budget instead of repeating 100% damage N times | M1 | explorer_survey_num_1 |
| 5 | Buff Duration & Multiplier Decimal Scaling | Re-scale LifeSwell, SoulBarrier, GreaterDamage, Berserker formulas with decimal coefficients | M1 | explorer_survey_num_1 |
| 6 | Boss Numerical Normalization | Re-scale hyper-inflated bosses (Selupan 4.8M -> ~8,500 HP, Dark Elf 2.5M -> ~5,200 HP, Kundun -> ~6,800 HP); remove boss exclusion from balance budget | M2 | explorer_survey_num_2 |
| 7 | High-Precision Decimal Drop Tables | Replace flat 0.2 (20%) and 0.5 (50%) in BoxOfKundun (+1~+5) and BoxOfLuck with 4-decimal-place probability tables | M2 | explorer_survey_num_2 |
| 8 | Money Drop Decoupling & Deflation | Decouple money drop from gained experience (replace gainedExperience + 7) with dedicated deflated formula | M2 | explorer_survey_num_2 |
| 9 | Economy Pricing & RoundPrice Cleanup | Remove rough `/100 * 100` and `/10 * 10` truncation from ItemPriceCalculator.RoundPrice; deflate jewel and item prices | M2 | explorer_survey_num_2 |
| 10 | Chaos Machine & Jewel Enhancement Tuning | Refactor crafting settings to support decimal rates; smooth +10..+15 curves; implement soft level degradation on failure | M3 | explorer_survey_num_2 |
| 11 | 15-Hour Solo Route 100% Convergence | Fine-tune DK Guard (from 16.82h down to ~15.2h) via physical scaling adjustment in balance.v2.json & BalanceV1.cs so 14/14 builds hit 13.5h - 16.5h | M3 | explorer_survey_num_3 |
| 12 | 3-Round Adversarial Simulation Harness | Build and execute 3-round adversarial test suite (R1: leveling/grinding, R2: boss combat, R3: cross-class parity & stress) | M4 (Dual Track) | explorer_survey_num_3 |
| 13 | Full Solution Build & In-Engine Tests | Ensure OpenMU.sln compiles in Release with 0 errors and all unit tests pass | M4 | explorer_survey_num_3 |
| 14 | Forensic Integrity Audit | Clean-room verification by teamwork_auditor confirming zero facades, zero hardcoded values, and authentic decimal balance | M4 | Governance |

## Milestones
| # | Name | Scope | Dependencies | Status |
|---|------|-------|-------------|--------|
| M1 | Core Combat Formulas & Character System | Standardize 5 pts/lvl, decimal attack speed/defense formulas, smooth hit rate & hyperbolic armor, multi-hit skill budget preservation | none | DONE |
| M2 | Monster/Boss Attributes, Drops & Economy | Boss HP/stat rebalancing, 4-decimal place drop tables, decoupled money drop, RoundPrice truncation elimination, jewel deflation | M1 | DONE |
| M3 | Chaos Machine, Enhancement & 15h Curve Tuning | Decimal crafting rates, soft jewel failure, DK Guard fine-tuning in balance.v2.json & BalanceV1.cs for 100% 15±1.5h convergence | M2 | DONE |
| M4 | Dual-Track Adversarial Simulation & Final Verification | 3-Round automated adversarial simulation execution, 4-Tier E2E tests, production build verification, Forensic Integrity Audit | M1, M2, M3 | DONE |

## Interface Contracts
### Combat Pipeline & Skill Budget Contract
- `AttackableExtensions.CalculateHitChance`: Continuous monotonic function in range `[0.05, 0.95]`; no cliff-edge drops.
- `AttackableExtensions.CalculateDamage`: Armor damage reduction must follow `armor / (armor + constant + scale * rank)`.
- `TargetedSkillDefaultPlugin`: When `skill.NumberOfHitsPerAttack > 1`, each individual hit must scale by `1.0f / skill.NumberOfHitsPerAttack` or the calibrated per-hit weight so cumulative damage equals exactly 100% of single-cast budget.

### Boss & Monster Numerical Contract
- All progression bosses must adhere to `BalanceV1ContentRank` scaling: HP $\in [1,200, 8,500]$, Defense $\in [85, 340]$, Base Damage $\in [120, 520]$.
- Bosses must not be excluded from balance capping logic unless handled by dedicated boss rank scaling.

### Drop & Economic Precision Contract
- All drop chances in `DropItemGroup` and box definitions must use `double` with non-zero decimal digits (no flat 0.2000 or 0.5000).
- `ItemPriceCalculator.RoundPrice` must preserve exact computed Zen without `/ 100 * 100` truncation.

## Code Layout
- `OpenMU/src/GameLogic/`: Owned by Milestone 1 & 2 Workers (combat formulas, skill plugin, item pricing, BalanceV1).
- `OpenMU/src/Persistence/Initialization/`: Owned by Milestone 1, 2 & 3 Workers (character classes, monster maps, box drops, chaos mixes).
- `OpenMU/src/DataModel/`: Owned by Milestone 3 Worker (crafting settings data types).
- `OpenMU-数值重设计/`: Owned by Milestone 3 Worker (balance.v2.json tuning, BalanceLab rules).
- `tests/`: Owned exclusively by E2E Testing Track and Challengers.
