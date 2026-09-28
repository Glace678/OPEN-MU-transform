#include <doctest.h>
#include <initializer_list>
#include <map>
#include "Core/Platform/WinCompat.h"
#include "Core/Globals/_define.h"
#include "Core/Globals/_enum.h"
#include "UI/Input/InterfaceInputPolicy.h"

using namespace SEASON3B;
using UI::Input::IsReadOnlyLayer;

TEST_CASE("persistent world HUD and rolling notices do not capture player input")
{
    for (const int key : {INTERFACE_CHATLOGWINDOW, INTERFACE_SYSTEMLOGWINDOW,
        INTERFACE_SLIDEWINDOW, INTERFACE_HERO_POSITION_INFO, INTERFACE_MAINFRAME,
        INTERFACE_NAME_WINDOW, INTERFACE_CRYWOLF,
        INTERFACE_ITEM_ENDURANCE_INFO, INTERFACE_BUFF_WINDOW, INTERFACE_HOTKEY,
        INTERFACE_ITEM_TOOLTIP, INTERFACE_BLOODCASTLE_TIME, INTERFACE_CHAOSCASTLE_TIME,
        INTERFACE_BATTLE_SOCCER_SCORE, INTERFACE_DUELWATCH_MAINFRAME,
        INTERFACE_DOPPELGANGER_FRAME, INTERFACE_EMPIREGUARDIAN_TIMER})
    {
        CHECK(IsReadOnlyLayer(key, false));
        CHECK(IsReadOnlyLayer(key, true));
    }
}

TEST_CASE("always visible message manager blocks only while an actual message exists")
{
    CHECK(IsReadOnlyLayer(INTERFACE_MESSAGEBOX, false));
    CHECK_FALSE(IsReadOnlyLayer(INTERFACE_MESSAGEBOX, true));
}

TEST_CASE("menus shops inventories and editable panels still capture world input")
{
    for (const int key : {INTERFACE_INVENTORY, INTERFACE_INVENTORY_EXT,
        INTERFACE_NPCSHOP, INTERFACE_STORAGE, INTERFACE_MIXINVENTORY, INTERFACE_TRADE,
        INTERFACE_OPTION, INTERFACE_HELP, INTERFACE_CHARACTER, INTERFACE_MOVEMAP,
        INTERFACE_CHATINPUTBOX, INTERFACE_SKILL_LIST, INTERFACE_NPC_DIALOGUE,
        INTERFACE_MINI_MAP, INTERFACE_WINDOW_MENU, INTERFACE_MUHELPER})
    {
        CHECK_FALSE(IsReadOnlyLayer(key, false));
        CHECK_FALSE(IsReadOnlyLayer(key, true));
    }
}

TEST_CASE("persistent skill and party layers block only while their panels are active")
{
    const UI::Input::LayerState idle{};
    CHECK_FALSE(UI::Input::BlocksWorldInput(INTERFACE_SKILL_LIST, idle));
    CHECK_FALSE(UI::Input::BlocksWorldInput(INTERFACE_PARTY_INFO_WINDOW, idle));
    CHECK_FALSE(UI::Input::BlocksWorldInput(INTERFACE_SIEGEWARFARE, idle));
    CHECK_FALSE(UI::Input::BlocksWorldInput(INTERFACE_NAME_WINDOW, idle));
    CHECK_FALSE(UI::Input::BlocksWorldInput(INTERFACE_CRYWOLF, idle));
    CHECK(UI::Input::BlocksWorldInput(INTERFACE_INVENTORY, idle));
    CHECK(UI::Input::BlocksWorldInput(INTERFACE_NPCSHOP, idle));

    CHECK(UI::Input::BlocksWorldInput(INTERFACE_SKILL_LIST, {false, true, false, false}));
    CHECK(UI::Input::BlocksWorldInput(INTERFACE_PARTY_INFO_WINDOW, {false, false, true, false}));
    CHECK(UI::Input::BlocksWorldInput(INTERFACE_SIEGEWARFARE, {false, false, false, true}));
    CHECK_FALSE(UI::Input::BlocksWorldInput(INTERFACE_MESSAGEBOX, idle));
    CHECK(UI::Input::BlocksWorldInput(INTERFACE_MESSAGEBOX, {true}));
}

TEST_CASE("read only 3D render layers preserve every registered camera slot")
{
    for (int key = INTERFACE_3DRENDERING_CAMERA_BEGIN; key <= INTERFACE_3DRENDERING_CAMERA_END; ++key)
        CHECK(IsReadOnlyLayer(key, false));
    CHECK(IsReadOnlyLayer(INTERFACE_HOTKEY, false));
    CHECK(IsReadOnlyLayer(INTERFACE_ITEM_TOOLTIP, false));
    CHECK_FALSE(IsReadOnlyLayer(INTERFACE_3DRENDERING_CAMERA_END + 2, false));
}
