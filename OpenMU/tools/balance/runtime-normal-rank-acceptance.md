# Normal Content Rank Runtime Acceptance

The pre-change audit is preserved in `runtime-inventory.md`. This document describes
only the approved normal-route adapter. It is not acceptance of the complete numeric redesign.

## Installed Scope

- Embedded schema-v1 resource: `src/GameLogic/Balance/NormalContentRanks.v1.json`.
- Exact key: persistent map definition GUID, original monster number, fixed `normal` difficulty.
- 135 bindings on 17 distinct map identities, covering ranks 1..400.
- 50 bindings are in ranks 300..400. All 73 candidate master-instance rows are excluded.
- Resource SHA256: `16466D120C0A3EE59A578948328991A10CF4DC8519449EF8174FE554E54DAE71`.
- Only identity and rank fields were copied from the candidate content artifact. No HP, damage,
  boss, skill or AI proposal is present in the resource.

`BalanceV1ContentRank` loads immutable mappings and validates the complete configuration
before `BalanceV1Initializer` publishes its marker. Every binding must have exactly one map,
an existing combat monster spawn and an original item-tier level in 1..255. Subsequent
profile validation checks these bindings again. There are no model fields or database migrations.

## Actual Callers

| Caller | Changed behavior | Preserved boundary |
| --- | --- | --- |
| AttackableExtensions.CalculateBaseExperience -> PlayerExperience / Party | Mapped monsters supply a fixed reward rank | Legacy profile and summoned-XP rules remain unchanged |
| AttackableExtensions.GetBalanceCombatRank -> CalculateDamageAsync | Normal monster attacks use mapped rank in the existing balance-v1 armor formula | Original HP, base damage, level, effects and later reductions remain unchanged |
| DefaultDropGenerator.GenerateBalanceV1DropsAsync | Reward rank controls independent Zen and rare-channel eligibility | Common/socket/excellent item selection uses the separate original item tier |
| balance-v1 excellent item channel | Filters for definitions which can actually receive an excellent option | Original-profile GenerateRandomExcellentItem is unchanged |

Unknown map GUIDs, unmapped monsters and uninstalled difficulties retain original-level
compatibility behavior. No difficulty is inferred from player level. There is no master-instance
runtime adapter; a `master_2` or `master_3` lookup is explicitly absent.

The drop interface still receives a monster definition plus a player, not a live monster.
It resolves the map captured at the start of the existing drop call. The current NPC death
caller suppresses summoned-monster drops before invoking that interface.

## Focused Verification

`BalanceV1ContentRankTests` initializes full Season 6 configuration in memory for each test,
uses real Monster objects with original definitions, and uses a real Player with the actual
Dark Wizard class and attribute system. NPC scheduling, optional plug-ins and network views
are not exercised by this fixture. Drops have deterministic random channels; XP retains the
original S6 random range.

| Acceptance | Evidence |
| --- | --- |
| All 135 bindings reach rewards | Actual CalculateExpAfterKillAsync and GenerateItemDropsAsync, with original level unchanged |
| Actual XP counters change | AddExpAfterKillAsync on Lorencia, Dungeon, Aida and Raklion monsters |
| Cross-map isolation | The same Skeleton definition has ranks 30 and 60 on Lorencia and Dungeon |
| Identity, not display number | Unknown map GUID with Lorencia's display number retains original-level XP/Zen |
| Unknown event/master boundary | Selupan remains raw-level fallback; every installed key rejects master_2/master_3 |
| Fixed encounter | Changing player level and gained XP leaves mapped rank/Zen unchanged |
| Legal high-rank equipment | All 50 rank-300..400 bindings produce legal common and actual excellent items |
| Rare endpoint pools | Rank-300 and rank-400 excellent pools, plus rank-400 socket pool use real S6 definitions |
| Real combat caller | A real Raklion monster attack uses rank 400 in armor calculation while Stats.Level stays 148 |
| No candidate stats installed | Every original monster attribute is compared before/after initialization |
| Legacy compatibility | Original XP formula and gained-XP + 7 money group contract remain unchanged |
| Startup refusal | Missing map/spawn or out-of-byte original item tier cannot publish a marker; installed mappings are revalidated |
| Zen independence | Standard, Relaxed and Journey profile rewards do not change mapped Zen |

| Run | Passed | Failed | Skipped |
| --- | --- | --- | --- |
| Content-rank focused runtime tests | 25 | 0 | 0 |
| Complete core test project, including the new tests | 930 | 0 | 0 |
| Complete persistence initialization test project | 25 | 0 | 2 |

The two skipped initialization tests are explicitly marked database/manual tests.
The builds completed without errors and emitted existing analyzer/obsolete API warnings.

```powershell
dotnet test tests/MUnique.OpenMU.Tests/MUnique.OpenMU.Tests.csproj --no-restore -m:1 --filter FullyQualifiedName~BalanceV1ContentRankTests --logger 'console;verbosity=minimal'
dotnet test tests/MUnique.OpenMU.Tests/MUnique.OpenMU.Tests.csproj --no-build --no-restore -m:1 --logger 'console;verbosity=minimal'
dotnet test tests/MUnique.OpenMU.Persistence.Initialization.Tests/MUnique.OpenMU.Persistence.Initialization.Tests.csproj --no-restore -m:1 --logger 'console;verbosity=minimal' -v:q
```

## Not Accepted

- The 73 master bindings, dynamic instance difficulty, monster/boss HP/damage/AI and telegraphs.
- Class HP/MP/AP/SD growth, skill target/action costs, regeneration and a unified reduction cap.
- New equipment stat budgets, smart loot, pity/tokens and persistent progression transaction rules.
- Vendor buy/sell/repair/travel/respec prices, starting money, material fees and the balance-v1 cash-shop replacement.
- A full 1..400 and master playthrough, controller/mobile gameplay feel or production database conversion.

No real database, migration, existing server, client source or GitHub upload was used in this task.
The already running QA demo still uses its previously loaded assemblies; it was not restarted.
