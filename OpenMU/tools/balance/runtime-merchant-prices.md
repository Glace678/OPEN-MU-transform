# Authoritative NPC Purchase Prices

Date: 2026-09-27. Local source, in-memory .NET tests and native C++ tests only.
No Startup output, database, save, running QA service, APK or remote repository
was modified by this phase.

## Actual Baseline And Scope

Season-six StoreItemList (`C2 31`) carries a window type, count, slots and item
bytes, but no price. MuMain previously displayed ItemValue's local valuation.
Its ordinary NPC click sends BuyItemFromNpcRequest directly; the inspected
production path did NOT contain a legacy-price wallet refusal. The earlier
description of such a refusal was an unverified inference and is corrected.
The local value was used as a pending-request marker (BuyCost), not proof of
the money charged by the server. The new wallet check is explicitly limited to
server-quoted purchases; original/Solo clicks keep their existing behavior.

Amy (253) sells actual HP/MP potions, including single and three-piece stock
and +1 HP variants. Pasi (254) sells scrolls/staves, NOT mana potions. Hanzo
(251) supplies the paid Sacred Glove installed by the prior phase. Tests use
these actual S6 merchant stores, not substitutes created to fit a price.

This patch fixes NPC BUY display and affordability, including tax and exact
stock quantities. It does not install another local price table, infer a mode
from notices, reuse Solo's /1000 scale, change a local configuration flag or
modify the server's purchase authorization. Inventory resale/repair/crafting
tooltips still need their own authoritative valuation integration; this is
not a claim that every client-side money display is now redesigned.

## Minimal Quote Contract

The existing merchant view sends a supplemental quote before its unchanged
legacy stock packet, in the SAME OutputLock/SendAsync write. This cannot
interleave another packet between the two. Quotes are emitted only when:

- balance-v1 is explicitly enabled and Solo is not enabled;
- the client is 106.3 or newer AND actually selected the extended S6 item
  serializer (other versions retain their original stock packet);
- the view is StoreKind.Normal;
- the collection is exactly the opened NPC's merchant collection, not a vault,
  crafting storage or arbitrary copied list.

No DB migration or capability setting is needed. Receipt of a supported quote
is the explicit server authority for that next merchant inventory. Servers
without quotes preserve the existing original/Solo path. An older unpatched
client cannot use these prices, so matching server/client builds are required.

Reserved supplemental packet: `C2 FA 00`, wire version 1, pricing contract 1.
This is a project extension, NOT a standard MU price field.

| Offset | Field |
| --- | --- |
| 0 | C2 header |
| 1-2 | Total byte length, big endian |
| 3 | FA custom merchant quote code |
| 4 | 00 purchase quote operation |
| 5 | 01 wire version |
| 6 | 01 explicit balance-v1 server price contract |
| 7 | Applicable player store tax rate, 0..3 |
| 8 | Item count, at most 120 |

Each entry has one-byte grid slot, one-byte serialized item length, identical
raw item bytes (at most 32), uint32 little-endian BASE price and uint32
little-endian TOTAL price. Price = active ItemPriceCalculator valuation.
Total = price + floor(price * applicable tax / 100), the actual store charge
equation. An unpayable item has both prices FFFFFFFF; it must never wrap into
a payable low price. Bounds follow the actual 8x15 NPC grid and int32 wallet.

The client checks the complete packet, version/profile/rate/count, unique slots,
exact lengths, totals, all identity bytes and completeness of the next inventory
before binding a quote to its newly allocated item key. Pending, unsupported,
duplicate, truncated or mismatched quotes do not permit a purchase. No quote
is copied onto the player's owned items merely because item type matches.
The purchase tooltip suppresses an unavailable/incomplete quoted price instead
of calling it free or displaying an unrelated legacy estimate.

Existing server store-tax notifications recompute totals with the same bounded
integer equation while preserving the original quoted base price, including
when the notice arrives between quote and stock. A duplicate quote revokes the
old binding until its matching stock arrives. Once a quote is recognized in an
NPC session, an unquoted replacement stock fails closed; only closing the
merchant, a new NPC dialog, or session InitGame permits the original/Solo
unquoted path again. The server ALWAYS recomputes the purchase cost and validates
the wallet independently; a quote is a display-time snapshot, not a price lock
or a client-supplied authority over the final transaction.

The wire is a custom extension for the matching MuMain build, not a negotiated
capability in an arbitrary upstream 106.3 executable. Unknown-client behavior
remains an interoperability gap; do not advertise mixed custom/stock clients as
tested. Taxes can change after a quote or the buyer's balance can change before
the click; the authoritative server may then refuse an apparently affordable
purchase. The quote never grants or reserves a purchase.

## Existing Marked-Configuration Stock Upgrade

The current startup path validates an already marked balance-v1 configuration
without updating its stock. `BalanceV1Initializer.Initialize()` has a repeatable
in-memory first-fit stock insertion method, but it is NOT an operator-facing
upgrade transaction, rollback journal or DB migration. Older marked saves may
therefore lack Hanzo's 0:32 Sacred Glove and Pasi's 5:0 Skull Staff. Do not
silently call the initializer on every boot or overwrite existing stock.

An explicit future maintenance command should:

1. Require a stopped server and a verified database backup. Select exactly one
   configuration by persistent ID and reject absent/mixed profile markers.
2. Dry-run each NPC by persistent identity (not display name), compare the
   complete item identity (definition, level 0, skill flag, durability and
   empty options),
   validate the 8x15 footprint and emit the exact proposed NPC/slot/item rows
   plus an expected configuration revision/fingerprint. Occupied or ambiguous
   slots fail the plan. No character, Zen or equipped item is modified.
3. On explicit apply, re-read the same configuration under a serializable
   transaction or an equivalent row lock, compare the revision/fingerprint,
   create only missing rows and save both merchants atomically. A plain
   read-then-write without isolation would race another administrator.
   Reapplying against already present identical stock is a no-op; a changed
   or partially conflicting row aborts instead of duplicating.
4. Record created persistent item IDs and their complete before/after stock
   fingerprints. Rollback may remove ONLY rows created by that apply if those
   IDs and fingerprints still match; otherwise restore the offline backup in a
   controlled outage rather than deleting any user-edited row.

That operator workflow is an assessed design, NOT implemented or run here.
No real database was opened, and no older marked configuration was migrated.

## Verification

Initial phase: focused .NET quote checks 15/15 (12s); full Core 1001/1001
(4m21s); native quote checks 9/9 with the fresh production export supplied
(264 assertions). Follow-up security-scope checks on 2026-09-27: focused .NET
merchant checks 20/20; full Core 1006/1006 (4m24s); native quote checks 11/11
(284 assertions) with replay, alias-slot, pending-tax, sentinel and zero-length
input. The five production-wire server cases re-exported the fixture from the
current test assembly and the native tests consumed that new binary. The
updated cache source separately compiled in the Windows x64 Release MuClient
project. The six original production C++ translation units compiled earlier;
this round did not relink or launch the entire client.
Scoped git diff checks report no whitespace errors. Existing compiler warnings
were not represented as a warning-free build.

The .NET merchant tests use real CreateCharacterAction, full S6 in-memory data,
the actual remote view/extended serializer, the real quote encoder and actual
BuyNpcItemAction. They check every serialized stock entry, rejected price-1
wallets, exact-price payments, retained stack count, Amy/Pasi/Hanzo offers,
excluded original/Solo/both/vault/crafting/no-opened-NPC paths and 0..3 tax
encoding with enhanced +15 stock. Stores and actors exist only in memory.

`OPENMU_MERCHANT_QUOTE_FIXTURE` exports the actual remote view's bytes from
the Amy test. The native production-wire test consumes that export, parses the
same extended item layout used by WSclient, binds all actual shop items, and
uses the exact QuoteCache lookup/CanBuy used by the UI. It proves real wire
compatibility, including the small MP 8 Zen unit price rather than 80 or 1.
The fixture is optional for ordinary unit runs; it was supplied for the
separately executed cross-language acceptance, not counted from an absent file.

Native tests also cover partial identity differences, every truncation, unknown
version/profile, malformed/duplicate slots, missing/repeated binds, overlarge
prices, tax updates, an unquoted stock within the quoted NPC session, explicit
original/Solo session reset, truncated option/socket item data and the full
production wire. Production-path probes additionally run complete original
and Solo S6 initializers with normal NPC purchase and no custom quote.

Reproduce .NET quote checks:

```powershell
$env:OPENMU_MERCHANT_QUOTE_FIXTURE = '<local fixture binary path>'
dotnet test tests/MUnique.OpenMU.Tests/MUnique.OpenMU.Tests.csproj --no-restore -m:1 --filter 'FullyQualifiedName~MerchantWireQuotesMatchStockAndActualPaymentAsync|FullyQualifiedName~MerchantQuotesAreExcludedFromOtherProfilesAndStoragesAsync|FullyQualifiedName~MerchantQuotesPreserveExactTaxAndEnhancedStockAsync'
```

Reproduce native checks from MuMain after building core_merchant_price_tests:

```powershell
$env:MU_MERCHANT_QUOTE_FIXTURE = '<the same local fixture binary path>'
& .\out\build\windows-x64-controller-acceptance\tests\core\Release\core_merchant_price_tests.exe
```

The touched production C++ translation units were separately compiled under
the existing Windows x64 Release project without linking Main, building an
APK, or starting a server. This proves production compilation plus wire/price
contracts, NOT visual or real network play acceptance. Root must rebuild the
matching QA server and client for real UI checks. The older-marker shop-stock
update remains an explicit future step; no old save is automatically migrated.
