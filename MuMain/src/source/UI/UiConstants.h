#pragma once

// Minimap and monster-name UI limit constants, extracted from the legacy aggregate
// Core/Globals/_define.h (P2 disassembly).

#define MAX_MINIMAP_NAME    100

// Was 40: Japanese NPC names are multi-byte UTF-8 (e.g. 42-byte katakana
// names in NpcName_jpn.txt) and a 40-byte read window cut a 3-byte sequence
// in half, leaving a U+FFFD (rendered as a diamond) at the end of the name.
// 64 wchar of destination also means the caller can pass 64 as the byte cap.
#define MAX_MONSTER_NAME    64
