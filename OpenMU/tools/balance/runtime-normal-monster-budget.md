# balance-v1 Normal-Route Monster Budget (2026-09-27)

## Installation Boundary

This is an opt-in `-balance-v1` correction for excessive ordinary S6 monster HP and
physical base damage. It affects 90 of 135 fixed normal bindings on 13 of 17 maps.
The remaining 41 early bindings and four mapped Boss bindings retain their original
attributes. Other Bosses, elite definitions without ordinary route spawns, event/wave
spawns, custom maximum-health overrides, summons, guards, unknown map GUIDs and the
original/Solo profiles are excluded. No shared `MonsterDefinition` or `Stats.Level`
is rewritten. The ordinary `MapInitializer` passes its own configuration to the
new instance before `Initialize`, so both first spawn and respawn use the same cap.

The fixed normal rank is keyed by persistent map GUID + monster number + literal
`normal` difficulty, never the player's current level. For eligible rank `r>=95`:

```
instance maximum HP = min(original maximum HP, round_away_from_zero(900 + 7*r))
physical factor    = min(1, (35 + 0.24*r) / original maximum physical base damage)
instance physical minimum/maximum = original minimum/maximum * physical factor
```

The multiplier is applied before the ordinary damage, armor, hit and skill pipeline.
It preserves the source attack interval, source physical attack spread, the real
player/monster hit calculations and source level for equipment eligibility. HP and
physical attack are upper caps, never bonuses. Magic/effect portions of monster
skills are **not** tuned by this correction. Instance modifiers remain attached if
an administrator hot-edits the shared definition; exact hot-reload cap adherence
after such edits has not been verified. The optional disabled-by-default monster
scaler plugin also has not been calibrated in combination with this rule.

## Route And Samples

The full in-memory S6 spawn inventory has 2,849 ordinary spawn entries for 135
fixed bindings, all marked `Automatic` and none with health overrides. The
per-map ranges below are binding ranks and computed target HP caps, not an
assertion that all routes/AI spawn spacing are accessible or difficulty-ordered.

| Map | Fixed rank range | Eligible HP cap range |
| ---: | ---: | ---: |
| Dungeon 1 | 100 | 1600 |
| Lost Tower 4 | 95..144 | 1565..1909 |
| Atlans 7 | 145..184 | 1915..2189 |
| Tarkan 8 | 185..230 | 2195..2510 |
| Icarus 10 | 225..255 | 2475..2687 |
| Aida 33 | 260..300 | 2720..3000 |
| Kanturu 37/38 | 295..335 | 2965..3245 |
| Karutan 80/81 | 330..365 | 3210..3455 |
| Kalima 7 36 | 355..392 | 3385..3646 |
| Swamp 56 | 360..390 | 3420..3630 |
| Raklion 57 | 385..400 | 3595..3700 |

Within affected ranks the target HP cap grows continuously at seven HP/rank.
The original route already has inversions across maps (e.g. Lost Tower rank144
has 6000 HP while Atlans rank145 has 2400), and the cap does not prove that
map travel, aggro, pack size or raw-item supply is progression-safe. At rank80
and below, including the candidate RF economic failure, monster stats stay as-is.

The full S6 explicit combat probe used 18 stages x levels 1, 11, 50, 51, 100,
200, 300, 400 = 144 actors, 504 encounters. Seven lineage results below use
available common/merchant gear +0..3 and actual learned skills, including class
4 (not evolved class 6) for the legal level-100 Knight. Means are 512 actual
`CalculateDamageAsync` samples per direction. TTK assumes one player action per
second; this is sensitivity, **not measured animation/cast cadence**. Area skills
count only one successful target application. No AI, movement, aggro packs,
purchase affordability, death-reset or real client session was simulated.

| Encounter | Original -> new HP | Seven-lineage virtual TTK, original -> new | Lowest no-potion survival, original -> new |
| --- | ---: | ---: | ---: |
| Dungeon 18, rank100 | 6000 -> 1600 | 26.6..84.7s -> 7.1..22.9s | 3.4s -> 10.3s |
| Tarkan 57, rank202 | 17000 -> 2315 | 36.3..127.2s -> 5.0..16.8s | 2.9s -> 12.0s |
| Aida 552, rank300 | 85700 -> 3000 | 136.9..506.9s -> 4.9..18.1s | 2.7s -> 22.1s |
| Raklion 565, rank400 | 265000 -> 3700 | 354.0..1466.0s -> 5.0..20.2s | 1.2s -> 25.1s |

The legal level-100 Knight class 4 specifically changed 28.1 -> 7.6 seconds
TTK and 8.2 -> 25.1 seconds no-potion survival; evolved class 6 at level 100
is structural-only and was not relied on for the summary. The 60-second virtual
stationary contact trace still exercises actual AP/MP cost, potion cooldowns,
regeneration and Drain Life; it does **not** remove killed targets. In that trace
Wizard level 200 dies after 19.6 seconds, despite a projected single-target
TTK of 12.4 seconds, illustrating why repeated packs and actual input timing
still need gameplay acceptance.

Level-400 legal +9 sensitivity uses a separately selected equipment loadout
from original merchant/common-drop definitions, checked against real character
requirements. Six families gained 4..10% payload per action; Elf lost 8.7%
because its legal +9 selection changed from bow 4:19 to bow 4:16. This is a
gear-optimizer/requirements warning, not a claim that all +9 equipment lowers
damage or that these upgrades are affordable. Those +9 selections were
synthetically equipped after legal requirement checks; crafting acquisition
was not replayed end to end.

The independent candidate model retains **20 failures** for RF/vitality
levels 80..98 (one additional duplicate L80 budget check). The corrected
class/skill unlock contract makes the candidate's rank-80 three-target
fight fall back to basic instead of an unearned area skill. Its modeled
L80 net Zen changes from +15009/h to -6349/h; 492/h kills and 29525/h potion
spend are candidate-only predictions, not actual server farm results. The
candidate also labels RF263 Dark Side an area skill although the live skill
contract is direct-target. Do not silence these failures through an unlock
rollback or by pretending the new rank95+ monster cap fixes them.

## Verification

Artifacts: `normal-spawn-inventory.v1.json`,
`runtime-combat-measurements.v1.skill-resources.json` (pre-budget),
`runtime-combat-measurements.v1.monster-budget.json` (post-budget),
`runtime-monster-gear.v1.json` (legal +9 sensitivity).

```
dotnet test tests/MUnique.OpenMU.Tests/MUnique.OpenMU.Tests.csproj --no-restore -m:1 --filter 'FullyQualifiedName~BalanceV1NormalMonsterBudgetTests|FullyQualifiedName~BalanceV1ContentRankTests' -v:q
$env:OPENMU_COMBAT_PROBE_REPORT='D:\openmu自用\OpenMU\tools\balance\runtime-combat-measurements.v1.monster-budget.json'
dotnet test tests/MUnique.OpenMU.Tests/MUnique.OpenMU.Tests.csproj --no-build --no-restore -m:1 --filter 'FullyQualifiedName~MeasureSeasonSixCombatBudgetsAsync' -v:q
dotnet test tests/MUnique.OpenMU.Tests/MUnique.OpenMU.Tests.csproj --no-build --no-restore -m:1 -v:q
```

The 30 targeted cases and the explicit 504-scenario probe passed. Full Core
regression passed 964/964; the two explicit inventory/combat probes are
separate from the default run.
No Startup build, database migration, live server restart or public deployment
was performed by this phase. This corrects one major normal-route budget;
original player stat relationships, early Wizard starter tools, complete loot/
shop economy, Boss/elite/master content and controller/client combat timing
remain incomplete for the user's all-values redesign.
