# Reviewed Skill Resources: balance-v1

Follow-up (2026-09-27): `runtime-early-economy.md` records paid starter stock,
cooled-down supply prices and the real creation-skill boundary. This did not
modify these four AP contracts or erase the independent RF model failures.

Historical scope note: the no-monster-HP-changes statements below describe
this earlier AP-only phase. The subsequent opt-in ordinary-instance budget
is documented in `runtime-normal-monster-budget.md`; RF candidate economic
failures and the original AP contracts below remain unresolved/unchanged.

## Installed Scope

Only the explicit balance-v1 marker enables this change. Default/original and Solo profiles
keep their original consumption code. A contradictory pair of markers does not enable it.
The rule operates on real learned entries, qualified class identities and exact original
type, hit-count, MP and AP contracts, not on a broad physical/area-skill category.

| Skill | Qualified Stages | Category / Caller | Original MP | Original AP |
| --- | --- | --- | ---: | ---: |
| 43 Death Stab | 6, 7 | routine / TargetedSkillDefaultPlugin | 15 | 12 |
| 52 Penetration | 8, 10, 11 | routine / AreaSkillAttackAction, deferred/frustum | 7 | 9 |
| 55 Fire Slash | 12, 13 | routine / AreaSkillAttackAction, frustum | 15 | 20 |
| 262 Chain Drive | 24, 25 | routine / TargetedSkillDefaultPlugin, four full hits | 15 | 20 |

The real entry must be in the player's SkillList and its definition in the configuration;
the entry must not be a master skill/level, and original skill-use requirements must hold.
Unknown/custom shapes, NPC/pet attacks, buffs, siege skills and unlearned copies fall back.
The actual learning handler also checks both item and skill class qualifications for these
same four IDs under balance-v1. The old helper only checked numeric item requirements and
could teach a base Knight the evolved Death Stab orb. No other learning or old-profile rule
was rewritten; this opt-in guard closes that reviewed-contract acquisition mismatch.
This is not an unlock or equipment grant. Death Stab still requires evolved Knight and
level 160; Penetration level 130; Chain Drive skill use level 150. Fire Slash learning still
requires Strength 320. The actual orb/drop route can unlock later than these prerequisites.

## Formula And Tradeoff

A = actual MaximumAbility; R = actual combat AP recovery per second.
R = (A * AbilityRecoveryMultiplier + AbilityRecoveryAbsolute) / 3.
Original S6 multipliers are 0.03 (0.05 for Knight), absolute recovery 2 in combat.
No AP capacity, regeneration relationship or timer was changed.

Base cost C = min(original AP, max(1, floor(R + A/120))).
The existing AP-reduction modifier applies afterward, bounded to [0,1] for these contracts.
Final cost is at least 1; original zero-AP skills remain zero and unchanged.

At one cast/second without an AP potion or kill/refill, net AP drain is at most A/120 per
second. A full pool retains at least half after 60 seconds; the actual half-filled login
pool can cover roughly 60 seconds before AP gates. This is a bounded encounter budget,
not indefinitely free casting. Faster animation/click cadences spend the reserve faster;
burst skills compete for the same reserve. MP can still limit casts, particularly Knight
200 Death Stab under the unchanged original energy allocation and 12-second MP potion.
The cadence is an explicit design/test input, not validated native animation throughput.

| Measured No-Paid-Gear Actor | AP Cap | Combat R/s | Original AP | New AP |
| --- | ---: | ---: | ---: | ---: |
| Knight 6 / 200, Death Stab | 261.25 | 5.0208 | 12 | 7 |
| Elf 8 / 200, Penetration | 249.40 | 3.1607 | 9 | 5 |
| MG 12 / 200, Fire Slash | 247.30 | 3.1397 | 20 | 5 |
| MG 12 / 400, Fire Slash | 472.30 | 5.3897 | 20 | 9 |
| RF 24 / 300, Chain Drive | 407.00 | 4.7367 | 20 | 8 |
| RF 24 / 400, Chain Drive | 530.75 | 5.9742 | 20 | 10 |

## Separate Contracts, Not Aliases

- RF265 Dragon Slasher: level 200 direct hit, one full payload, 100 MP and 100 AP unchanged.
  It remains tactical burst and is not the routine Phoenix Shot fallback. The probe now
  prioritizes legal Chain Drive 262, then equipped Phoenix Shot 270/Killing Blow 260.
  Reports keep a separate 265 resource-only stress trace when actually learned.
- RF270 Phoenix Shot: equipped glove skill; AreaSkillAttackAction + PhoenixShotSkillPlugIn,
  four full payload hits, 30 MP, zero AP. Its radius-two strategy can add up to seven targets.
  Availability in a catalog is not proof of a reachable item for the current actor.
- SU215 Chain Lightning: Energy 245, 85 MP, zero AP; explicit target plus the actual strategy's
  delayed 0.7/0.5 attacks. Targets may repeat; these are not normalized whole-cast weights.
- SU230 Lightning Shock: Energy 823, 115 MP and 7 AP unchanged; automatic area, diameter 14,
  original 5..12 hits-per-attack area budget. Not interchangeable with Chain Lightning.
- RF263 Dark Side and RF264 Dragon Roar are also separated in the candidate ID mapping.
  Original 263 has level 180, 70 MP, one direct payload; 264 level 150, 50 MP/30 AP, four
  payload hits plus its radius-three area strategy. Neither has a routine AP override.

Candidate fractional mana/coefficient/normalized-hit fields are model proposals, not live
requirements. Reviewed runtime metadata and coverage statuses explicitly distinguish them.
The lab's truthful 180-level Dark Side boundary exposes twenty RF vitality economy failures
at levels 80..98 (including the duplicate L80 focused check). These remain a candidate gap;
do not reuse an older model acceptance JSON as current success. No live economy/damage/HP
changes were made to force those independent model checks green.

## Validation Boundary

BalanceV1SkillResourceTests uses full S6 in-memory entities and sourced common/merchant
equipment without paid/ancient/excellent/socket gear. It covers all nine qualified stages
at legal levels, actual skill consumer deltas, old-profile learned costs across all eighteen
classes, class/orb/use unlock rejection, shape/profile exclusions and positive reductions.
Direct and area public action callers must both consume once and actually damage a live
original monster. A real wall-clock sixty-second MG200 test calls Player.RegenerateAsync,
TryConsumeForSkillAsync and the actual MP potion consumer with cooldown/stock assertions;
it starts AP at the real half-pool login value and never resets resources during the trace.
Other stage/level traces integrate the separately checked original recovery equation and
use explicit 12-second potion gates. Unchanged MP shortage is not labeled an AP failure.

The rerun combat probe keeps all 144 actors and 504 scenes, with old measurements retained
as the pre-change baseline. The resource change cannot repair the enormous original late
monster HP/attack mismatch, early Wizard skill availability, unbound elites, candidate
model economy or missing master-instance mappings. No server, Startup output or DB touched.

## Commands

```powershell
dotnet test tests/MUnique.OpenMU.Tests/MUnique.OpenMU.Tests.csproj --no-restore -m:1 --filter FullyQualifiedName~BalanceV1SkillResourceTests --logger 'console;verbosity=minimal' -v:q
$env:OPENMU_COMBAT_PROBE_REPORT = 'D:\openmu自用\OpenMU\tools\balance\runtime-combat-measurements.v1.skill-resources.json'
dotnet test tests/MUnique.OpenMU.Tests/MUnique.OpenMU.Tests.csproj --no-build --no-restore -m:1 --filter FullyQualifiedName~BalanceV1CombatProbeTests --logger 'console;verbosity=minimal' -v:q
dotnet test tests/MUnique.OpenMU.Tests/MUnique.OpenMU.Tests.csproj --no-build --no-restore -m:1 --logger 'console;verbosity=minimal' -v:q
```

This document records implemented scope and remaining gaps. Final command counts and actual
before/after deltas belong in runtime-skill-resources-acceptance.md after the runs complete.
