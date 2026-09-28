# Early Supplies And Actual Starter Contracts

Date: 2026-09-27. Local source and in-memory tests only. No listener, database,
Startup output, user save, or remote Git repository was touched.

## Actual Boundary Found

The independent candidate still reports 30228 passed and 20 failed checks:
RF/vitality levels 80 through 98, plus the duplicate level-80 economy check.
The verifier, thresholds, candidate allocations, and candidate prices were NOT
changed in this phase. A new repeat is retained in the adjacent lab directory
at `artifacts/early-economy-runtime-crosscheck/verification.json`.

Those failures are not a runtime combat acceptance result. The candidate selects
basic attacks until its learned-skill unlocks and chooses the highest recovery
potion, including the price jump to medium HP at 80. Its repair/travel/crafting
charges are model parameters, not measured engine transactions.

Actual S6 creation activates AddEnergyBallForDarkWizard (17, 1 MP) and
AddChargeForRageFighter (269, 20 MP/15 AP). The earlier 144-actor probe created
characters directly with an empty plugin manager, omitting these creation
grants. Its level-1 wizard no-skill claim and RF basic-only assumption are
therefore not authoritative for newly created base characters. The new probe
uses CreateCharacterAction and the real configured creation plugins instead.

RF 263 Dark Side still requires level 180; 262 Chain Drive and 264 Dragon Roar
still require 150; 265 Dragon Slasher still requires 200 and costs 100 MP/100 AP.
No skill was granted early, and Charge's original cost was not modified.
RF's client creation requirement (a prior level-150 character) remains in the
class definition; the server CreateCharacterAction itself does not enforce
that field. Client creation eligibility is NOT validated by these tests.

## Installed Narrow Scope

Only the explicit balance-v1 marker enables these runtime valuations. Original
and Solo calculator/transaction behavior is unchanged. The mutual-exclusion
rule remains in place. Monster/player HP, damage, growth, point totals, potion
restoration, cooldowns, skill power, MP/AP costs and equip demands are unchanged.

| Item | Per Piece Buy | Per Piece Sell | Existing Recovery/Cooldown |
| --- | ---: | ---: | --- |
| 14:1 Small HP | 12 | 1 | 28%, cap 160, 8s |
| 14:2 Medium HP | 48 | 7 | 28%, cap 600, 8s, unlock 80 |
| 14:3 Large HP | 176 | 26 | 28%, cap 2200, 8s, unlock 180 |
| 14:4 Small MP | 8 | 1 | 40%, cap 120, 12s |
| 14:5 Medium MP | 32 | 4 | 40%, cap 400, 12s, unlock 80 |
| 14:6 Large MP | 112 | 16 | 40%, cap 1600, 12s, unlock 180 |
| 14:35/36/37 SD | 140/300/600 | 21/45/90 | Existing separate SD limits |
| 15:3 Fire Ball scroll | 36 | 5 | Original energy 40 and class qualification |
| 0:32 Sacred Glove family, plain +0 | 600 | 90 | Skill 260 if HasSkill; original demands |
| 5:0 Skull Staff family, plain +0 | 120 | 18 | Original demands/power; no free skill |

Old S6 HP/MP prices were 80/330/1500 per piece. Relative to the installed cap
ratios, medium HP is now 4 times small (600/160 = 3.75), and large HP about
14.67 times small (2200/160 = 13.75). Higher tiers purchase instantaneous
recovery/carry efficiency, not a mandatory upgrade at every unlock. At HP <=
571 or MP <= 300, the small potion is just as effective as medium. Even above
those thresholds, compare recovery per Zen and cooldown pressure instead of
automatically buying the largest unlocked item. +1 HP potions have the same
runtime restoration contract and price as +0; no pointless double charge.

Potion buy/sell amounts are linear in actual remaining count. Resale floors
15% per piece before multiplying by count, with a minimum positive unit of 1.
Splitting a stack cannot increase total resale. Empty stacks have no value.
Weapons use `ceil(base * (1 + 0.2 * min(level,15))^2 * (1 + options))`, where
luck contributes 0.25, ordinary option 0.25 per bounded option level and each
excellent option 1. This same family scale applies to enhanced descendants,
resale, NPC/field repair bases, ordinary crafting contribution, old-value
crafting fees and Fenrir contribution. It prevents cheap purchase followed by
legacy high-value resale or free high-value crafting contribution. Other item
families, quest fees, travel, and global sinks still need their own review.

The initializer adds plain +0/HasSkill Sacred Glove to Hanzo (251), and plain
+0 Skull Staff to Pasi (254), without replacing existing offers. It checks the
original 8x15 shop geometry and is idempotent, including an explicitly rerun
initializer on an already-marked configuration. Stores/data were modified only
in test contexts, not in a real database.

Important older-config boundary: current Startup skips Initialize for an
already-marked balance-v1 configuration, merely validating it. Runtime prices
take effect there, but the two new offers do NOT automatically appear. An
explicit, separately reviewed stock update or a fresh isolated initialization
is required. No implicit database update was added in this task.

## Stat-Respecting Resource Path

Sacred Glove's 85/35 fields are requirement coefficients, NOT final stats. The
existing engine formula gives a plain +0 glove requirements of 152 strength
and 74 agility. An unmodified 25/20/50/5 vitality allocation at RF 80 has only
130 strength and remains unable to wear it. A failing regression preserves
this fact; assertions/weapon demands were not loosened.

The explicit equipment-first alternative satisfies the original glove demand
before further vitality investment. It redirects 22 earned points from
vitality to strength at 80 and 9 at 90; total earned points remain exactly
`5 * (level-1)`. At 98 the original allocation already satisfies plain +0.
HP changes in the alternative reflect player allocation only, NOT a class
growth or saved-character mutation. Existing already-allocated characters
are NOT silently redistributed and need a future authenticated respec path
or a deliberately planned new build.

Buying and equipping the paid glove supplies the real four-hit Killing Blow
(260, 9 MP, 0 AP), and tests invoke TargetedSkillDefaultPlugin on a real
initialized S6 monster. No four-hit assumption was attached to Charge or Dark
Side. The possible common armor pool is identified in the JSON, not considered
freely granted or guaranteed drops. Merchant items in the probe gear pool are
possible offers; actual payment is verified separately in commerce tests.

A newly created wizard has Energy Ball and 60 starting MP (62 maximum). The
test kills an actual spider through the targeted skill path, generates the
normal Zen channel with a successful controlled roll, picks up 9 Zen once and
pays 8 for the first small MP potion. This proves the earning/purchase path,
NOT that every first kill is guaranteed to drop Zen (the actual chance is 65%).
The wizard can later buy Fire Ball at its real energy requirement and a plain
staff after meeting its demand. No repeatable starting-money gift was added.

## Measurement And Remaining Gaps

Before: `runtime-early-economy.before.v1.json`, 64 actors (seven classes plus
the original RF vitality allocation, eight levels). After:
`runtime-early-economy.after.v1.json`, 72 actors (also the distinct RF
equipment-first alternative). Actor levels/allocations are test inputs, not
changes to any user character. Damage uses 256 actual CalculateDamage calls
per direction, real TryConsumeForSkill deltas, mapped ordinary spawns,
possible legal common gear, actual creation skills, and original recovery
rates. Mean values are stochastic; the JSON records the exact sample run.

For the paid-glove vitality alternative:

| RF Level | HP / STR | Target Rank | Payload / TTK At 1s | Legacy / Reviewed Supply Zen | Expected Zen | Net Before Other Sinks |
| --- | --- | ---: | --- | --- | ---: | ---: |
| 80 | 565 / 152 | 81.11 | 214.59 / 6.52s | 338.51 / 47.06 | 80.60 | 33.54 |
| 90 | 652 / 152 | 90 | 221.75 / 11.27s | 586.35 / 82.31 | 90.35 | 8.04 |
| 98 | 720.4 / 153 | 100 | 223.14 / 7.17s | 186.01 / 24.45 | 102.70 | 78.25 |

The supply column is a LOWER-BOUND stationary one-action/second estimate:
casts = ceil(monster HP / measured payload), health loss = whole enemy attacks
at original attack delay minus original HP regeneration, mana loss = measured
MP per cast minus original MP regeneration. Replace resources with the lowest
available actual-price/recovery ratio; amortize fractional potions across
many kills. Gross Zen = 0.65 * actual rank/tier reward. Equipment sale income,
repair, travel, spell acquisition, gear purchase, crafting and knockback/kiting
are excluded. Post-fight potion cooldown recovery is required; this is NOT a
win, sustained 1Hz combat, real hourly income or complete progression proof.

The seven-class sense check intentionally retains negatives. At level 1 the
wizard's measured Energy Ball path improves the lower-bound net from -33.66
to +0.11 before other sinks. Elf/Summoner/Charge-only RF still have negative
stationary starter budgets. At 11, naked wizard versus rank-12.94 Bull and
Summoner versus the Elbeland rank-10.89 encounter are clearly unsafe/negative.
At 50/80 the nearest-rank early wizard matchup remains unsuitable for face
tanking despite cheaper supplies. Its damage, gear demands, route choice,
range/movement and early ordinary spawn budgets need another measured phase.
The RF 90 row has insufficient margin to declare full progression economy
accepted. Unadjusted vitality 80-96 still lacks glove strength. None of these
gaps is converted to a passing validator result.

Actual resource-only 60s test: no HP/MP/AP resets, no potion supply, real wall
clock RegenerateAsync; wizard casts Energy Ball once/second, paid RF glove
casts Killing Blow once/6 seconds. Both retain positive MP. This demonstrates
a conservative resource cadence, NOT incoming combat survival or animation
cadence. The four prior reviewed AP rules are unchanged.

At the end of the initial backend phase, client prices were still local and
unmatched. The subsequent server-quote integration is recorded separately in
`runtime-merchant-prices.md`: NPC buying price and affordability now use actual
server quotes with identity matching, not Solo division. Matching packaged
client/server builds and visual QA are still required. No APK, Startup output
or running server was rebuilt by these phases; resale/repair client displays
remain outside the purchase-only integration.

## Repeatable Checks

Focused Core tests (22/22 passed):

```powershell
dotnet test tests/MUnique.OpenMU.Tests/MUnique.OpenMU.Tests.csproj --no-restore -m:1 --filter 'FullyQualifiedName~BalanceV1EarlyEconomyTests'
```

Explicit 72-actor after probe (1/1 passed):

```powershell
$env:OPENMU_EARLY_ECONOMY_REPORT = '<local output JSON path>'
dotnet test tests/MUnique.OpenMU.Tests/MUnique.OpenMU.Tests.csproj --no-build --no-restore -m:1 --filter 'FullyQualifiedName~MeasureEarlyEconomyAsync'
```

Focused coverage: nine potion identities x 2 levels x 255 stack counts;
seven-class real buy/consume/separate cooldown/sell; exact insufficient-money
refusal; all 19 RF levels 80-98 paid glove/equip/actual resource use; real
targeted RF attack and wizard kill/reward/single pickup/purchase; idempotent
shop update; enhanced +0..15 fair valuation/positive repair; unchanged
original/Solo pricing; explicitly retained raw vitality demand failure;
60s original real regeneration without reset.

Full Core: 986/986 passed (4m32s); explicit inventory/combat/early probes are
excluded from the normal run and invoked separately. Existing initialization
regression: BalanceV1InitializerTests 1/1 passed (5s). No claim of all-number,
all-content or real-client completion.
