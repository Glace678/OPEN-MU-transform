#pragma once

// Lucky-set and restricted-item / item-action constants, extracted from the legacy aggregate
// Core/Globals/_define.h (P2 disassembly).

// Lucky Set armor constants - verified as indices 62-72 (Phoenix Soul is at 73)
constexpr int LUCKY_SET_ARMOR_START_INDEX = 62;
constexpr int LUCKY_SET_ARMOR_COUNT = 11;  // Indices 62-72

// Restricted ITEM_HELPER special items starting at local index 135.
// Exact item-name coverage may vary across older client logic.
constexpr int RESTRICTED_SPECIAL_MISC_START_INDEX = 135;
constexpr int RESTRICTED_SPECIAL_MISC_COUNT = 12;  // 135..146

// Restricted ITEM_POTION special jewels starting at local index 160.
// Exact item-name coverage may vary across older client logic.
constexpr int RESTRICTED_SPECIAL_JEWEL_START_INDEX = 160;
constexpr int RESTRICTED_SPECIAL_JEWEL_COUNT = 2;   // 160..161

// Item action restriction types for Check_ItemAction
// Index into nItemOption array: [PERSONALSHOP, STORE, TRADE, DROP, SELL, REPAIR]
constexpr int ITEM_ACTION_BLOCK_STORAGE_TRADE = 0;  // 0,1,1,0,0,0 - Blocks storage & trade
constexpr int ITEM_ACTION_BLOCK_SELL_ONLY = 1;      // 0,0,0,0,1,0 - Blocks sell only
