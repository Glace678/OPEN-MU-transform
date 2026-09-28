# Solo Balance v1

Opt in with `-solo -version:season6`. The local launcher supplies these flags
and launches the matching client with `MU_SOLO_BALANCE=1`. Do not use this
client price mode against an unmodified public server.

## Progression and economy

| Area | Solo setting |
| --- | --- |
| Normal / master experience | 8x / 5x |
| Level-up points | +3 per future level |
| Base health | +100 |
| Health / mana after a kill | +3% of maximum |
| Recovery interval | 2 seconds |
| Monster/trap/destructible HP | 45%, soft compression above 20,000, cap 300,000 |
| Monster physical damage | 40%, soft compression above 400, cap 1,500 |
| Monster defense / defense rate | 45%, soft compression above 300, cap 1,000 |
| Monster attack rating | 70%, soft compression above 1,500, cap 4,000 |
| NPC buy/sell, repair, crafting, Lahap, helper | Final positive fee / 1,000, minimum 1 Zen |
| Quest starts, quest-map entry, warps, event admission, letters | Cost / 1,000 |
| Gold amount rate | 25%, credited directly instead of ground drops |
| Durability | Equipment damage/hit thresholds 5x; pet damage threshold 10x |
| Excellent / jewel / ancient / socket drop groups | At least 2% / 3% / 10% / 2% per applicable group |
| Eligible crafting / Soul / Life / Harmony / refine upgrades | 100% success; item eligibility and maximum levels still apply |
| Character creation | Removes unlock-level requirement for creatable classes |
| NPC support buffs | Available through normal level cap |
| Event entry window | 30 seconds |

These values form a progression profile, not a blanket division of every
integer. Equipment formulas, item IDs, attribute IDs, protocol fields, level
caps, terrain, player-set trade prices and saved inventories remain intact.
Existing character gold, XP and allocated points are not destructively rescaled.
An old high-level save does not retroactively receive the extra per-level points.

Amy (NPC 253) sells upgrade materials, pet spirits and mounts for Zen. Lumen
(255) sells defined event tickets. Learnable skills are distributed across
Pasi (254), Lumen, Amy and Alex (230) as space permits. Existing store items are
preserved; new items must fit the 8-by-15 grid.

## Persistence and rollback

The profile marker is saved last with the configuration in one transaction.
Repeated startup skips the conversion, so prices and stats are not repeatedly
divided. The launcher makes a `before-solo-v1` database backup before first
conversion of an existing database; backup failure aborts startup.

Use the launcher's normal backup/restore flow to restore a pre-solo database,
and run the original binaries without `-solo` / `MU_SOLO_BALANCE`. Removing the
flag alone does not undo a configuration already persisted. Never delete the
profile marker to "reset" it: that would apply the conversion again.

Source/data baseline created before this change:
`%LOCALAPPDATA%\OpenMU\BalanceBackups\20260905-solo-before`.

## Local cash shop

The solo server now supplies the in-game cash-shop catalog over its own
connection. No external paid catalog or payment provider is used.

- An account receives 1,000 local WCoin(C) once.
- Recharge exchanges 1,000 earned Zen for 100 local WCoin(C).
- Permanent configured NPC supplies and implemented consumables cost 1-20
  coins. Item level, durability and skill flags survive purchase and delivery.
- Purchase storage is account-wide; gifts are limited to characters on that
  same account. Cross-account gifting and real-currency payments are excluded.
- Claiming, deleting, exchanging and purchasing are safe-zone operations.
  Ledger changes and delivered inventory are saved together; a failed save
  restores the previous in-memory balances and unclaimed item.
- Existing saves receive an account-ledger database migration. The launcher
  backs up an existing database before the first shop-enabled startup.

`SoloCashShopTests.cs` concentrates transaction, corruption, duplicate-claim,
save-failure and wire-format checks in one file. These tests use an in-memory
player context and packet buffers, not a running PostgreSQL server or game UI.

## Acceptance boundaries

Competitive guild/castle/PvP features are not converted into a single-player
campaign. Custom plugins, hard-coded event UI text and every map-specific
encounter have not all been replayed. Actual shop UI acceptance, every monster
and mount's frame cadence, all 117 controller workflows and physical motor
feedback remain unverified.

Initialization tests check profile application, repeat-start protection,
monster HP bounds and new shop-item placement. Price tests check rounding;
item-consumption tests check solo success without shared configuration mutation.
These are regression checks, not evidence of a complete solo playthrough.
