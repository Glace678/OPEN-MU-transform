# Combat Calibration: Current Runtime Boundary

Creation-path correction (2026-09-27): this historical direct-construction
probe omitted configured character-created plugins. Real new base wizard/RF
characters receive Energy Ball/Charge. See `runtime-early-economy.md` and its
separate actual CreateCharacterAction probe; do not infer startup skill absence
or a guaranteed zero-income progression from the historical actors below.

Historical baseline warning: the no-monster-attributes-installed statement in
this report predates the opt-in normal-instance cap. See
`runtime-normal-monster-budget.md` and the post-budget measurements for the
current `balance-v1` implementation. The player-side and unresolved-content
limitations below still apply.

Historical pre-skill-resource baseline: the two reports below predate the reviewed AP
rule. See runtime-skill-resources.md and its acceptance artifact for current AP costs and
the corrected RF routine-selection contract. Keep these reports for before/after comparison.

## Status And Evidence

The explicit probe passed twice with the same full Season 6 inputs: 18 stages x eight levels
(1, 11, 50, 51, 100, 200, 300, 400), 144 actors, 504 encounters and 144 resource-only traces.
The two local JSON reports contain each actor's base/growth attributes, candidate allocation,
equipment identity/source, learned skill source, real consumption and recovery deltas,
512 actual damage samples per direction and a clearly labeled virtual-cadence trace.
Replication changed mean outgoing damage by 1.13% on average, at most 6.22% across rows.
This verifies the measurement path; it does NOT mean that balance or client timing passed.
No production combat values, Startup outputs, server process or database were changed.

Reports: runtime-combat-measurements.v1.json and runtime-combat-measurements.v1.replicate.json.
Harness: tests/MUnique.OpenMU.Tests/BalanceV1CombatProbeTests.cs.

## Original Attributes Still Active

The opt-in profile uses its hit/armor/rank/XP/potion rules while the original S6 HP, MP, AP,
equipment, skill requirements, skill multipliers, attack delays and class stat relations remain.
Only five normal stat points per level have been installed. Candidate damage weights, player
growth, action intervals, resource requirements, normalized hit weights and boss telegraphs
have not. All characters here retain that original attribute combination.
Character construction does not verify creation unlocks or evolution quests. Low evolved/master
stages are explicitly structural-only. Gear availability proves a merchant listing or actual
common drop outcome at/below the rank, not affordability or a successful progression route.
No buffs, wings, pets, excellent/ancient/socket items or master-tree investment were supplied.

## Measured Ordinary Budget At Level 400

Scene: map 57, monster 565, rank 400, original raw level 148, original HP 265000.
Numbers below use a one-second player action sensitivity input, not measured animations.
Area skills mean one successful single-target application, not multi-target throughput.

| Stage | Skill | Mean Payload | Estimated TTK s | No-Potion Survival s | Resource-Only Casts / 60 |
| --- | --- | ---: | ---: | ---: | ---: |
| Dark Wizard 0 | Aqua Beam 12 | 247.2 | 1072.2 | 1.23 | 60 |
| Blade Knight 6 | Death Stab 43 | 598.8 | 442.6 | 3.18 | 60 |
| Fairy Elf 8 | Penetration 52 | 574.6 | 461.2 | 1.67 | 60 |
| Magic Gladiator 12 | Fire Slash 55 | 536.8 | 493.7 | 1.63 | 39 |
| Dark Lord 16 | Fire Burst 61 | 448.3 | 591.1 | 1.82 | 60 |
| Summoner 20 | Drain Life 214 | 182.9 | 1448.7 | 1.54 | 60 |
| Rage Fighter 24 | Dragon Slasher 265 | 303.8 | 872.3 | 2.28 | 8 |

Health recovery is zero without original equipment/effects; the largest available HP potion
restores 28% maximum HP at most once per eight seconds. Incoming net damage remains about
670..830 HP/s before Drain Life healing/status effects, so this potion rule alone cannot
sustain the original late monsters. Drain Life healing is included in its discrete trace.
MP recovery is original 0.037 * max MP per three seconds, plus actual 40% MP potion / 12s.
MG's 20 AP Fire Slash and RF's 100 AP Dragon Slasher exhaust their resource budget even in
the no-incoming trace. The incoming trace deliberately does not remove a killed target: it is
a stationary continuous-contact stress budget, not an AI-driven or client playable encounter.

## Early Playtest Boundary

Root's dummy test0 Elf HP80/MP30 agrees with original stage 8. Its factory sets inventory money
to 10000000 (AccountInitializerBase.cs, CreateCharacter); the HUD zero is experience percentage,
not evidence of zero Zen. Its observed empty quick slots are not the probe's sourced loadout.
New-account starter money is a different initialization boundary and was not validated here.
Potions in stress traces assume unlimited stock; no purchase affordability was validated.

| Geared Home Scene | Monster | Rank | HP | Payload TTK s | No-Potion Survival s |
| --- | --- | ---: | ---: | ---: | ---: |
| Elf 1, Noria | Goblin 26 | 1 | 45 | 4.33 | 21.20 |
| Elf 11, Noria | Elite Goblin 33 | 10.67 | 120 | 4.63 | 10.13 |
| Elf 51, Noria | Stone Golem 32 | 30 | 465 | 6.78 | 10.65 |
| Wizard 1, Lorencia | Spider 3 | 1 | 30 | 20.73 | 29.33 |
| Knight 1, Lorencia | Spider 3 | 1 | 30 | 2.93 | 55.73 |

Wizard level 1 has no legal attack scroll or staff under this allocation and availability
boundary. Its early damage differs about sevenfold from the knight. At level 11, the global
nearest-rank mob is Hideous Rabbit 420 in Elbeland (rank 10.89, HP520), not the Elf's Noria route;
the report therefore keeps both global-nearest and home-map scenes instead of substituting one.

## Content And Skill Contract Gaps

- The four mapped boss references are Balrog 38/rank150, Hydra 49/rank190,
  Dark Phoenix 77/rank265 and Illusion of Kundun 275/rank400. Early boss rank gaps are not
  appropriate encounters. The rank400 boss HP140000 is below the ordinary monster HP265000.
- The proposed eight elite IDs have definitions but no initialized map spawn. Their synthetic
  reference placement is not a deployed normal-route elite encounter. High-level reference
  selection still uses original level90; it must not be presented as a rank400 elite.
- Candidate rf_phoenix coalesces IDs270 and265, but original270 is Phoenix Shot (four hits,
  30 MP) and265 is Dragon Slasher (100 MP +100 AP, level200). They are not interchangeable.
- Candidate su_chain coalesces215 Chain Lightning and230 Lightning Shock; the latter has
  an area hit-budget contract instead of the former's explicit-target chaining strategy.
- Candidate dk_stab unlock40 conflicts with original43's evolved-class qualification and
  level160 orb. Candidate FE unlock40 conflicts with Penetration's level130 requirement.
  Keeping those requirements while applying model unlock numbers would be inconsistent.
- Multi-hit original direct/area applications repeat full damage payloads, not candidate
  normalized weights. Player action/animation cadence and status/effect uptime are unverified.

## Decision Before Installing Monster Budgets

A monster-only HP cannot meet the candidate 4..6 second target for these measured original
class budgets with common +0..3 gear. This is not proof about unmeasured upgraded +9/+15,
buffed or master-tree builds, and it does not validate the candidate's progression-gear target.
Even for the bounded one-target payload, the fastest measured stage requires HP
at least 4*598.8=2395, while the slowest requires HP at most 6*182.9=1097: no overlap.
HP scaling also cannot repair missing early wizard tools or AP exhaustion. Accordingly, no
ordinary HP/attack/defense proposal has been installed based on this inconsistent boundary.

Recommended next scoped change: agree a versioned player-skill contract first, separating
RF270/265 and SU215/230, preserving class/evolution requirements unless explicitly changed,
and defining sustained AP costs or recovery for the solo route. Add a legal starter action for
the Wizard as a separate onboarding decision. Validate actual single-target skill callers and
client cadence, then derive map-GUID/monster/difficulty-specific ordinary instance budgets.
Do not rewrite shared MonsterDefinition attributes by rank: e.g. monster14 is rank30 on
Lorencia and rank60 in Dungeon. Keep original raw level for item pools and original-profile
fallback; do not install Boss/elite model HP, telegraphs or missing master mappings.

Acceptance for that next change: all seven lineages at legal stages/levels, sourced equipment,
actual action execution plus 60s resource traces, finite target HP intersection, 8/12/15s real
recovery gates, old profile unchanged, unknown mapping fallback and raw item-tier unchanged.
Until then this report is evidence of the remaining redesign gap, not a claim of all-values completion.

## Validation Commands

```powershell
$env:OPENMU_COMBAT_PROBE_REPORT = 'D:\openmu自用\OpenMU\tools\balance\runtime-combat-measurements.v1.json'
dotnet test tests/MUnique.OpenMU.Tests/MUnique.OpenMU.Tests.csproj --no-restore -m:1 --filter FullyQualifiedName~BalanceV1CombatProbeTests --logger 'console;verbosity=minimal' -v:q
dotnet test tests/MUnique.OpenMU.Tests/MUnique.OpenMU.Tests.csproj --no-build --no-restore -m:1 --logger 'console;verbosity=minimal' -v:q
```

The explicit probe passed 1/1 on each of two stable runs. Full core regression passed 930/930.
The normal run excludes the explicit calibration probe. Git diff whitespace checks passed;
existing LF/CRLF warnings and unrelated XML-documentation warnings were not modified.
