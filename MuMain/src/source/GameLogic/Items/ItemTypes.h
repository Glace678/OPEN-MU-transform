#pragma once

// Item type/index/group catalog constants, extracted from the legacy aggregate
// Core/Globals/_define.h (P2 disassembly).

constexpr int MAX_ITEM_TYPE = 16;

constexpr int MAX_ITEM_INDEX = 512;

constexpr int MAX_ITEM = MAX_ITEM_TYPE * MAX_ITEM_INDEX;

#define MAX_MINI_MAP_DATA	100

#define	MAX_MASTER		   24

#define MAX_ITEM_SPECIAL   8
#define ITEM_LEVEL_NORMAL  4
#define MAX_QUEST_ITEM     64
#define MAX_EVENT_ITEM     35

#define MAX_SOCKETS			5
#define SOCKET_EMPTY		0xFE

constexpr int ITEM_GROUP_SWORD = 0;
constexpr int ITEM_GROUP_AXE = 1;
constexpr int ITEM_GROUP_MACE = 2;
constexpr int ITEM_GROUP_SPEAR = 3;
constexpr int ITEM_GROUP_BOW = 4;
constexpr int ITEM_GROUP_STAFF = 5;
constexpr int ITEM_GROUP_SHIELD = 6;
constexpr int ITEM_GROUP_HELM = 7;
constexpr int ITEM_GROUP_ARMOR = 8;
constexpr int ITEM_GROUP_PANTS = 9;
constexpr int ITEM_GROUP_GLOVES = 10;
constexpr int ITEM_GROUP_BOOTS = 11;
constexpr int ITEM_GROUP_WING = 12;
constexpr int ITEM_GROUP_HELPER = 13;
constexpr int ITEM_GROUP_POTION = 14;
constexpr int ITEM_GROUP_ETC = 15;

//item index
#define ITEM_SWORD		 (ITEM_GROUP_SWORD)
#define ITEM_AXE		 (ITEM_GROUP_AXE*MAX_ITEM_INDEX)
#define ITEM_MACE		 (ITEM_GROUP_MACE*MAX_ITEM_INDEX)
#define ITEM_SPEAR		 (ITEM_GROUP_SPEAR*MAX_ITEM_INDEX)
#define ITEM_BOW		 (ITEM_GROUP_BOW*MAX_ITEM_INDEX)
#define ITEM_STAFF		 (ITEM_GROUP_STAFF*MAX_ITEM_INDEX)
#define ITEM_SHIELD		 (ITEM_GROUP_SHIELD*MAX_ITEM_INDEX)
#define ITEM_HELM		 (ITEM_GROUP_HELM*MAX_ITEM_INDEX)
#define ITEM_ARMOR		 (ITEM_GROUP_ARMOR*MAX_ITEM_INDEX)
#define ITEM_PANTS		 (ITEM_GROUP_PANTS*MAX_ITEM_INDEX)
#define ITEM_GLOVES		 (ITEM_GROUP_GLOVES*MAX_ITEM_INDEX)
#define ITEM_BOOTS		 (ITEM_GROUP_BOOTS*MAX_ITEM_INDEX)
#define ITEM_WING		 (ITEM_GROUP_WING*MAX_ITEM_INDEX)
#define ITEM_HELPER		 (ITEM_GROUP_HELPER*MAX_ITEM_INDEX)
#define ITEM_POTION  	 (ITEM_GROUP_POTION*MAX_ITEM_INDEX)
#define ITEM_ETC 		 (ITEM_GROUP_ETC*MAX_ITEM_INDEX)




#define ITEM_ZEN  	 (ITEM_POTION + 15)
