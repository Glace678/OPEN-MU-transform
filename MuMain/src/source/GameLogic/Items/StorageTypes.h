#pragma once

// Item storage destination type enum, extracted from the legacy aggregate
// Core/Globals/_define.h (P2 disassembly).

/**
 * \brief Types of storage where items can be moved from/to.
 */
enum struct STORAGE_TYPE
{
    UNDEFINED = -1,
    INVENTORY = 0,
    TRADE = 1,
    VAULT = 2,
    CHAOS_MIX = 3,
    MYSHOP = 4,
    TRAINER_MIX = 5,
    ELPIS_MIX = 6,
    OSBOURNE_MIX = 7,
    JERRIDON_MIX = 8,
    CHAOS_CARD_MIX = 9,
    CHERRYBLOSSOM_MIX = 10,
    EXTRACT_SEED_MIX = 11,
    SEED_SPHERE_MIX = 12,
    ATTACH_SOCKET_MIX = 13,
    DETACH_SOCKET_MIX = 14,
    LUCKYITEM_TRADE = 15,
    LUCKYITEM_REFINERY = 16,
};
