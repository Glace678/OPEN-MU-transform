#pragma once

// Object kind bit flags and scene object capacity constants, extracted from the legacy aggregate
// Core/Globals/_define.h (P2 disassembly).

#define KIND_PLAYER  1
#define KIND_MONSTER 2
#define KIND_NPC     4
#define KIND_TRAP    8
#define KIND_OPERATE 16
#define KIND_EDIT    32
#define KIND_PET     64
#define KIND_TMP     128
#define MAX_OPERATES 200
#define MAX_ITEMS 1000
