#pragma once

// Pathing-domain constants extracted from the legacy aggregate
// Core/Globals/_define.h (P2 disassembly). Terrain-walk flags, path
// distance factors and node/direction enums live with the pathing layer.
#include "Core/Platform/WinCompat.h"  // BYTE, DEFINE_ENUM_FLAG_OPERATORS

constexpr auto MAX_PATH_FIND = 15;

#define TW_SAFEZONE		(0x0001)
#define TW_CHARACTER	(0x0002)
#define TW_NOMOVE		(0x0004)
#define TW_NOGROUND		(0x0008)
#define TW_WATER		(0x0010)
#define TW_ACTION       (0x0020)
#define TW_HEIGHT       (0x0040)
#define TW_CAMERA_UP    (0x0080)
#define TW_NOATTACKZONE (0x0100)
#define TW_ATT1         (0x0200)
#define TW_ATT2         (0x0400)
#define TW_ATT3         (0x0800)
#define TW_ATT4         (0x1000)
#define TW_ATT5         (0x2000)
#define TW_ATT6         (0x4000)
#define TW_ATT7         (0x8000)

constexpr auto FACTOR_PATH_DIST = 5;
constexpr auto FACTOR_PATH_DIST_DIAG = ((int)((float)FACTOR_PATH_DIST * 1.414f));

constexpr auto MAX_COUNT_PATH = 500;
constexpr auto MAX_INT_FORPATH = (65000 * 30000);

enum EPathNodeState : BYTE
{
    PATH_INTESTLIST = (0x01),
    PATH_TESTED = (0x02),
    PATH_END = (0x04),
};

DEFINE_ENUM_FLAG_OPERATORS(EPathNodeState)

enum EPathDirection
{
    UNDEFINED = 0x00,
    WEST = 0x1,
    SOUTHWEST = 0x2,
    SOUTH = 0x3,
    SOUTHEAST = 0x4,
    EAST = 0x5,
    NORTHEAST = 0x6,
    NORTH = 0x7,
    NORTHWEST = 0x8,
};
