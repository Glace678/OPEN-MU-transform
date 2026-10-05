#pragma once

// Interface/chat-edit/IME/auto-attack/text-render and movement constants, extracted from the legacy aggregate
// Core/Globals/_define.h (P2 disassembly).

#define MAX_WHISPER      120
#define MAX_WHISPER_LINE 6

#define EDIT_NONE    0
#define EDIT_MAPPING 1
#define EDIT_OBJECT  2
#define EDIT_WALL    3
#define EDIT_HEIGHT  4
#define EDIT_LIGHT   5
#define EDIT_SOUND   6
#define EDIT_MONSTER 7

#define IME_CONVERSIONMODE  1
#define IME_SENTENCEMODE    2

#define AUTOATTACK_ON   0x01
#define AUTOATTACK_OFF  0x02

#define WHISPER_SOUND_ON    0x04
#define WHISPER_SOUND_OFF   0x08
#define SLIDE_HELP_OFF     0x10

#define RT3_SORT_LEFT 1
#define RT3_SORT_LEFT_CLIP 2
#define RT3_SORT_CENTER 3
#define RT3_SORT_RIGHT 4

#define RT3_WRITE_RIGHT_TO_LEFT 7
#define RT3_WRITE_CENTER 8

#define CHAOS_MIX_LEVEL 10

#define MOVEMENT_MOVE    0
#define MOVEMENT_GET     1
#define MOVEMENT_TALK    2
#define MOVEMENT_ATTACK  3
#define MOVEMENT_OPERATE 4
#define MOVEMENT_SKILL	 5

#define MAX_WHISPER_ID 5
