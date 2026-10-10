// Test-only stub stdafx.h: lets the REAL NewUIItemMng.cpp (and its real
// header NewUIItemMng.h) compile in a headless unit test. Only the symbols the
// translation unit actually touches are provided; the real item-type catalog
// (ItemTypes.h) and the real ParseItemData/ItemCreationParams header are used
// unchanged so the test exercises production code, not a copy.
#pragma once

#include <cstdint>
#include <cstddef>
#include <cstring>
#include <cassert>

typedef unsigned char  BYTE;
typedef unsigned short WORD;
typedef unsigned long  DWORD;

// Real wire/type constants: MAX_SOCKETS=5, MAX_ITEM_INDEX=512, MAX_ITEM=8192,
// SOCKET_EMPTY=0xFE.
#include "GameLogic/Items/ItemTypes.h"

// Option-flag wire bits (mirror of Core/Enums/GlobalEnums_1.h ItemOptionFlags).
// Copied verbatim so ParseItemData can be driven with the real packet layout.
enum ItemOptionFlags : BYTE
{
    HasOption    = 0x01,
    HasLuck      = 0x02,
    HasSkill     = 0x04,
    HasExcellent = 0x08,
    HasAncient   = 0x10,
    HasHarmony   = 0x20,
    HasGuardian = 0x40,
    HasSockets  = 0x80,
};
inline ItemOptionFlags operator&(ItemOptionFlags a, ItemOptionFlags b) { return static_cast<ItemOptionFlags>(static_cast<BYTE>(a) & static_cast<BYTE>(b)); }
inline ItemOptionFlags operator|(ItemOptionFlags a, ItemOptionFlags b) { return static_cast<ItemOptionFlags>(static_cast<BYTE>(a) | static_cast<BYTE>(b)); }

constexpr int ITEM_COLOR_NORMAL = 0;
namespace SEASON4A { constexpr int MAX_SOCKET_OPTION = 256; }

// Minimal ITEM covering every field NewUIItemMng.cpp reads/writes. This is a
// test double; only ParseItemData (which never touches ITEM) and the
// bounds-bearing paths are exercised.
struct ITEM
{
    int   RefCount = 0;
    bool  bPeriodItem = false;
    bool  bExpiredPeriod = false;
    DWORD Key = 0;
    int   Type = 0;
    BYTE  Level = 0;
    BYTE  Durability = 0;
    bool  HasLuck = false;
    bool  HasSkill = false;
    BYTE  OptionType = 0;
    BYTE  OptionLevel = 0;
    BYTE  ExcellentFlags = 0;
    BYTE  AncientDiscriminator = 0;
    BYTE  AncientBonusOption = 0;
    BYTE  Jewel_Of_Harmony_Option = 0;
    BYTE  Jewel_Of_Harmony_OptionLevel = 0;
    bool  option_380 = false;
    BYTE  SocketCount = 0;
    BYTE  SocketSeedSetOption = 0;
    BYTE  bySocketOption[5] = {};
    BYTE  SocketSeedID[5] = {};
    BYTE  SocketSphereLv[5] = {};
    int   byColorState = 0;
};

template <typename T> inline void SafeDelete(T*& p) { delete p; p = nullptr; }

// External symbols the compiled class methods reference; defined in
// item_parse_stubs.cpp.
void SetItemAttributes(ITEM* ip);
struct SocketItemMgrStub { bool IsSocketItem(ITEM* p); };
extern SocketItemMgrStub g_SocketItemMgr;