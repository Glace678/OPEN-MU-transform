#pragma once

// Split chunk of legacy Core/Globals/_enum.h (P2).
// Enum names/values/order are unchanged.
#include "Core/Platform/WinCompat.h"  // BYTE and fixed underlying types

namespace SEASON3A
{
    enum eCursedTempleState
    {
        eCursedTempleState_None = 0,
        eCursedTempleState_Wait,
        eCursedTempleState_Ready,
        eCursedTempleState_Play,
        eCursedTempleState_End,
    };

    enum eCursedTempleTeam
    {
        eTeam_Allied = 0,
        eTeam_Illusion,
        eTeam_Count,
    };
};

namespace SEASON3B
{
    enum INTERFACE_LIST
    {
        INTERFACE_BEGIN = 0x00,
        INTERFACE_FRIEND,
        INTERFACE_MOVEMAP,
        INTERFACE_PARTY,
        INTERFACE_MYQUEST,
        INTERFACE_NPCQUEST,
        INTERFACE_GUILDINFO,
        INTERFACE_TRADE,
        INTERFACE_STORAGE,
        INTERFACE_STORAGE_EXT,
        INTERFACE_MIXINVENTORY,
        INTERFACE_COMMAND,
        INTERFACE_PET,
        INTERFACE_NPCSHOP,
        INTERFACE_INVENTORY,
        INTERFACE_INVENTORY_EXT,
        INTERFACE_MYSHOP_INVENTORY,
        INTERFACE_PURCHASESHOP_INVENTORY,
        INTERFACE_CHARACTER,
        INTERFACE_NPCBREEDER,
        INTERFACE_SERVERDIVISION,
        INTERFACE_DEVILSQUARE,
        INTERFACE_BLOODCASTLE,
        INTERFACE_NPCGUILDMASTER,
        INTERFACE_GUARDSMAN,
        INTERFACE_SENATUS,
        INTERFACE_GATEKEEPER,
        INTERFACE_GATESWITCH,
        INTERFACE_CATAPULT,
        INTERFACE_REFINERY,
        INTERFACE_REFINERYINFO,
        INTERFACE_KANTURU2ND_ENTERNPC,
        INTERFACE_CURSEDTEMPLE_NPC,
        INTERFACE_CURSEDTEMPLE_GAMESYSTEM,
        INTERFACE_CURSEDTEMPLE_RESULT,
        INTERFACE_CHATINPUTBOX,
        INTERFACE_WINDOW_MENU,
        INTERFACE_OPTION,
        INTERFACE_HELP,
        INTERFACE_ITEM_EXPLANATION,
        INTERFACE_SETITEM_EXPLANATION,
        INTERFACE_QUICK_COMMAND,
        INTERFACE_KANTURU_INFO,
        INTERFACE_CHATLOGWINDOW,
        INTERFACE_PARTY_INFO_WINDOW,
        INTERFACE_BLOODCASTLE_TIME,
        INTERFACE_CHAOSCASTLE_TIME,
        INTERFACE_BATTLE_SOCCER_SCORE,
        INTERFACE_SLIDEWINDOW,
        INTERFACE_HERO_POSITION_INFO,
        INTERFACE_MESSAGEBOX,
        INTERFACE_DUEL_WINDOW,
        INTERFACE_CRYWOLF,
        INTERFACE_NAME_WINDOW,
        INTERFACE_SIEGEWARFARE,
        INTERFACE_MAINFRAME,
        INTERFACE_SKILL_LIST,
        INTERFACE_ITEM_ENDURANCE_INFO,
        INTERFACE_BUFF_WINDOW,
        INTERFACE_MASTER_LEVEL,
        INTERFACE_GOLD_BOWMAN,
        INTERFACE_GOLD_BOWMAN_LENA,
        INTERFACE_LUCKYCOIN_REGISTRATION,
        INTERFACE_EXCHANGE_LUCKYCOIN,
        INTERFACE_DUELWATCH,
        INTERFACE_DUELWATCH_MAINFRAME,
        INTERFACE_DUELWATCH_USERLIST,
        INTERFACE_INGAMESHOP,
        INTERFACE_DOPPELGANGER_NPC,
        INTERFACE_DOPPELGANGER_FRAME,
        INTERFACE_QUEST_PROGRESS,
        INTERFACE_QUEST_PROGRESS_ETC,
        INTERFACE_EMPIREGUARDIAN_NPC,
        INTERFACE_EMPIREGUARDIAN_TIMER,
        INTERFACE_MINI_MAP,
        INTERFACE_NPC_DIALOGUE,
        INTERFACE_GENSRANKING,
        INTERFACE_UNITEDMARKETPLACE_NPC_JULIA,
        INTERFACE_LUCKYITEMWND,
        INTERFACE_HOTKEY,
        INTERFACE_3DRENDERING_CAMERA_BEGIN,
        INTERFACE_3DRENDERING_CAMERA_END = INTERFACE_3DRENDERING_CAMERA_BEGIN + 24,
        INTERFACE_ITEM_TOOLTIP,
        INTERFACE_MUHELPER,
        INTERFACE_MUHELPER_EXT,
        INTERFACE_MUHELPER_SKILL_LIST,
        INTERFACE_SYSTEMLOGWINDOW,
        INTERFACE_COMMAND_LIST,
        INTERFACE_END,
        INTERFACE_COUNT = INTERFACE_END - 2,
    };
}

namespace info
{
    enum InfoTextType
    {
        eInfo_Text_File = 0,
        eInfo_Item_File,
        eInfo_Skill_File,
        eInfo_Slide_File,
        eInfo_Dialog_File,
        eInfo_Credit_File,
        eInfo_Filter_File,
        eInfo_FilterName_File,
        eInfo_MonsterSkill_File,
        eInfo_Movereq_File,
        eInfo_Quest_File,
        eInfo_ItemSetType_File,
        eInfo_ItemSetOption_File,
        eInfo_NpcName_File,
        eInfo_JewelOfHarmonyOption_File,
        eInfo_JewelOfHarmonySmelt_File,
        eInfo_ItemAddOption_File,
        eInfo_File_Count,
    };
};
