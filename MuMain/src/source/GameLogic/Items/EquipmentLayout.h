#pragma once

// Equipment slots and derived combined inventory index constants, extracted from the legacy aggregate
// Core/Globals/_define.h (P2 disassembly).

#define EQUIPMENT_WEAPON_RIGHT 0
#define EQUIPMENT_WEAPON_LEFT  1
#define EQUIPMENT_HELM         2
#define EQUIPMENT_ARMOR        3
#define EQUIPMENT_PANTS        4
#define EQUIPMENT_GLOVES       5
#define EQUIPMENT_BOOTS        6
#define EQUIPMENT_WING         7
#define EQUIPMENT_HELPER       8
#define EQUIPMENT_AMULET       9
#define EQUIPMENT_RING_RIGHT   10
#define EQUIPMENT_RING_LEFT    11
#define MAX_EQUIPMENT          12

#define MAX_EQUIPMENT_INDEX			MAX_EQUIPMENT
#define MAX_MY_INVENTORY_INDEX		(MAX_EQUIPMENT_INDEX + MAX_INVENTORY)
#define MAX_MY_INVENTORY_EX_INDEX		(MAX_MY_INVENTORY_INDEX + MAX_INVENTORY_EXT)

#define MAX_SETITEM_OPTIONS		12

#define MAX_MY_SHOP_INVENTORY_INDEX (MAX_MY_INVENTORY_EX_INDEX + MAX_SHOP_INVENTORY)
