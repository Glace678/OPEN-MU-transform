#pragma once

// Character roster/attack-result/ability/control-code/PVP/regiment and
// joint capacity constants, extracted from Core/Globals/_define.h (P2).

#define MAX_CHARACTERS_CLIENT  400
#define MAX_CHARACTERS_SERVER  10
#define MAX_CHARACTERS_PER_ACCOUNT 5

#define MAX_PATH_FIND 15

#define ATTACK_FAIL    0
#define ATTACK_SUCCESS 1
#define ATTACK_DIE     2

#define ABILITY_FAST_ATTACK_SPEED	0x01
#define ABILITY_PLUS_DAMAGE			0x02
#define ABILITY_FAST_ATTACK_RING	0x04
#define ABILITY_FAST_ATTACK_SPEED2	0x08

#define CTLCODE_01BLOCKCHAR			0x01
#define CTLCODE_02BLOCKITEM			0x02
#define CTLCODE_04FORTV				0x04
#define CTLCODE_08OPERATOR			0x08
#define CTLCODE_10ACCOUNT_BLOCKITEM	0x10
#define CTLCODE_20OPERATOR			0x20

#define PVP_HERO2		1
#define PVP_HERO1		2
#define PVP_NEUTRAL		3
#define PVP_CAUTION		4
#define PVP_MURDERER1	5
#define PVP_MURDERER2	6

#define REGIMENT_NONE       0
#define REGIMENT_DEFENSE    1
#define REGIMENT_ATTACK     2

#define MAX_JOINTS 500
#define MAX_TAILS  200

#define RENDER_FACE_ONE 0x01
#define RENDER_FACE_TWO 0x02
