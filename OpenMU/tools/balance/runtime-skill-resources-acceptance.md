# balance-v1 Skill/AP Acceptance, 2026-09-27

## Result

- Full S6 in-memory resource fixture: 28/28 focused tests passed without the slow clock test.
- Full core regression: 959 passed, zero failed; the separate explicit probe was skipped in
  the normal run. TRX counter total 960, passed 959, ignored explicit 1.
- Actual-wall-clock 60-second MG200 skill test passed inside the full run (60.87 seconds):
  starts at half AP, uses real `Player.RegenerateAsync` and `TryConsumeForSkillAsync`, real
  MP potions with `ConsumeItemAsync` cooldown and stock decrement, no AP/MP reset.
- Explicit full S6 probe passed 1/1 with 144 actors, 504 scenes and 144 normal-scene
  resource-only traces; output includes four Dragon Slasher-specific burst traces.
- Candidate independent lab `verify`: 30,228 passed, **20 failed**, concentrated on
  RF/vitality progression economy L80..L98 after correcting the fake L20 Dark Side unlock.
  Candidate verification is not an integrated game pass and its failure must not be hidden.

## Actual Resource Deltas

Probe source: runtime-combat-measurements.v1.skill-resources.json (2,051,279 bytes).
Pre-change comparison: runtime-combat-measurements.v1.json. Both reports use random damage
samples, so small hit/TTK variance is expected; the deterministic AP/MP deltas are the
evidence for this change. AP capacity/recovery and monster budget were not modified.

| Legal Stage / Level | Reviewed Skill | AP Cap | R AP/s | AP Before > After | MP | Baseline -> Current Successful Casts / 60 s |
| --- | --- | ---: | ---: | ---: | ---: | ---: |
| BK 6 / 200 | Death Stab 43 | 261.25 | 5.02 | 12 > 7 | 15 | 40 -> 40 (MP-limited) |
| FE 8 / 200 | Penetration 52 | 249.40 | 3.16 | 9 > 5 | 7 | 48 -> 60 |
| MG 12 / 200 | Fire Slash 55 | 247.30 | 3.14 | 20 > 5 | 15 | 21 -> 60 |
| MG 12 / 300 | Fire Slash 55 | 359.80 | 4.26 | 20 > 7 | 15 | 30 -> 60 |
| MG 12 / 400 | Fire Slash 55 | 472.30 | 5.39 | 20 > 9 | 15 | 39 -> 60 |
| RF 24 / 300 | Chain Drive 262 | 407.00 | 4.74 | 20 > 8 | 15 | new legal route 60/60 |
| RF 24 / 400 | Chain Drive 262 | 530.75 | 5.97 | 20 > 10 | 15 | new legal route 60/60 |
| RF 24 / 300 | Dragon Slasher 265 | 407.00 | 4.74 | 100 > 100 | 100 | burst-only trace 6/60 |
| RF 24 / 400 | Dragon Slasher 265 | 530.75 | 5.97 | 100 > 100 | 100 | burst-only trace 8/60 |

The old RF probe incorrectly selected 265 as if it were Phoenix Shot 270; old 6/8
successful casts were for 265. Current 262 is a distinct live skill and should not be
compared as if it were a numerical improvement to the same attack. Phoenix Shot 270 did
not appear in RF common +0..3 gear availability at those levels; it remains 30 MP/zero AP.
Twenty actor/skill pairs had a lower AP charge (IDs 43/52/55/262); four actor cases
with a learned 265 retained their 100 AP + 100 MP charge.

## Pipeline Boundary

The real `TargetedSkillDefaultPlugin` and `AreaSkillAttackAction` tests charged resources
once per cast and caused damage to an initialized monster; area/target skill tests ran
in-memory without wire clients. Original requirements and class acquisition are retained.
One pre-existing learning issue was observed: `LearnablesConsumeHandlerPlugIn` previously
checked numeric item requirements only, allowing a class-ineligible orb to be taught.
The opt-in guard now checks both item and skill class eligibility for exactly the four
reviewed IDs. Original/Solo profiles preserve their previous learning behavior.

No boss/elite HP, attack/defense, candidate damage normalization, skill animations, native
input or monster AI has been accepted in this phase. At level 400, MG versus rank-400
ordinary monster 565 still had a roughly 501-second single-target estimated TTK (one
second action sensitivity input), and no-potion survival about 1.66 seconds. AP uptime
is not a claim that the full combat economy is playable. Knight200 remains MP-limited.

## Reproduce Without Server/Database

```powershell
dotnet test tests/MUnique.OpenMU.Tests/MUnique.OpenMU.Tests.csproj --no-restore -m:1 --filter 'FullyQualifiedName~BalanceV1SkillResourceTests&FullyQualifiedName!~ActualClock' --logger 'console;verbosity=minimal' -v:q
$env:OPENMU_COMBAT_PROBE_REPORT = 'D:\openmu自用\OpenMU\tools\balance\runtime-combat-measurements.v1.skill-resources.json'
dotnet test tests/MUnique.OpenMU.Tests/MUnique.OpenMU.Tests.csproj --no-build --no-restore -m:1 --filter FullyQualifiedName~BalanceV1CombatProbeTests --logger 'console;verbosity=minimal' -v:q
dotnet test tests/MUnique.OpenMU.Tests/MUnique.OpenMU.Tests.csproj --no-build --no-restore -m:1 --logger 'console;verbosity=minimal' -v:q
```

The full run includes the 60-second wall-clock test. Existing Startup DLLs, the running
RAM QA server and the real PostgreSQL server were not rebuilt, migrated or restarted.
