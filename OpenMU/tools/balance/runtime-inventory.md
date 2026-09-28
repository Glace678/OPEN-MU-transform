# Numeric Runtime Inventory

This is the read-only audit snapshot taken before the normal-route rank adapter.
It describes source integration, not a database inspection or completed playthrough.
The Solo and balance-v1 profiles are mutually exclusive; their coverage cannot be combined.

| Domain | balance-v1 runtime integration | Remaining gap |
| --- | --- | --- |
| Activation | Startup opt-in, exclusive markers, existing configuration conversion blocked | Existing save migration is not implemented |
| Progression | Exact normal/master tables, 400/200 caps, three XP rates, actual PlayerExperience and Party paths | Raw monster levels are still used as content rank in this snapshot |
| Stat points | Five points per level for all 18 stages | Initial stats, quest rewards and master node budgets remain original |
| HP/MP/AP/SD | Original CharacterClasses attribute networks feed ItemAwareAttributeSystem | New growth and regeneration budgets are model-only |
| Hit/armor | New logarithmic hit and armor formulas in AttackableExtensions | Later original reductions and Soul Barrier are not governed by a unified total cap |
| Skills/buffs/pets | Original skill, area-hit and resource-consumption paths remain active | New action/target/hit budgets, coefficients and caps are not installed |
| Monster content | Original map spawns, attributes and AI remain active | Candidate HP/damage/AI and boss telegraphs are not installed |
| Loot and Zen | Independent equipment/Zen/jewel channels; Zen independent of awarded XP; quest channel retained | Smart drops, pity and tokens are model-only; raw rank mismatch remains |
| Potions | Nine HP/MP/SD rules, unlocks, recovery caps and resource-group cooldowns | New vendor prices and persistent reconnect cooldowns are not installed |
| Economy | Original ItemPriceCalculator and transaction handlers are active | New buy/sell/repair/travel/respec/start-money budgets are not installed |
| Enhancement | Candidate success chances and preservation of the base item on six high-level craftings | Pity state, material/Zen budgets and durable one-shot transactions are incomplete |
| Events/quests/PvP | Original systems remain active; party XP has a candidate branch | Candidate admission/reward/party-monster/PvP budgets are not installed |
| Local cash shop | Old Solo-only 1000 welcome coins and 1000 Zen to 100 coins exchange | SoloCashShopService.CanUse rejects balance-v1; no new-profile replacement has been approved |

## Evidence

- D:/openmu自用/OpenMU/src/Persistence/Initialization/BalanceV1Initializer.cs: only ConfigureProgression and ConfigureLevelEnhancement are installed in this snapshot.
- AttackableExtensions.CalculateBaseExperience uses killedObject.Attributes[Stats.Level].
- DefaultDropGenerator.GenerateBalanceV1DropsAsync uses monster[Stats.Level]; GetPossibleList rejects values outside 0..255.
- CharacterClasses/ClassDarkWizard.cs retains original health/mana relationships; ItemAwareAttributeSystem consumes these relationships.
- BuyNpcItemAction, SellItemToNpcAction, ItemRepairAction, ItemStackAction and MuHelperZenCostCalculator only apply the old Solo price conversion.
- SoloCashShopService.CanUse requires the old Solo marker.
- The existing engine catalog contains 468 objects, 73 maps, 284 skills, 677 items and 18 character stages. Its 335 combat monsters have a maximum original level of 148.
- The candidate content mapping contains 135 normal-route rows and 73 master-instance rows. Those counts do not establish gameplay integration.
- Existing experience tests create synthetic levels 100 and 300. The independent model verification count is not a runtime acceptance count.

## Approved Next Scope

Integrate only the 135 fixed normal-route content ranks using map definition GUID,
monster number and the explicit normal difficulty key. Do not overwrite Stats.Level,
apply candidate boss attributes, migrate saves, connect a real database or restart servers.
Keep the 73 master-instance mappings unimplemented. Unknown mappings explicitly retain
the original level as the compatibility fallback, rather than pretending to be covered.
Item selection must keep a separate bounded original drop level when content rank exceeds 255.

Acceptance must use a complete Season 6 in-memory configuration and real Monster
instances through XP/drop paths: map identity isolation, legacy behavior, unknown mapping,
valid equipment selection at ranks 300..400, and Zen independence from XP rates.
Passing these tests will not complete HP/MP/skills/economy/boss or master-content acceptance.
