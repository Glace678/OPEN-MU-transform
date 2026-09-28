#include "UI/Input/InterfaceInputPolicy.h"
#include "Core/Platform/WinCompat.h"
#include "Core/Globals/_define.h"
#include <map>
#include "Core/Globals/_enum.h"

namespace UI::Input
{
    bool IsReadOnlyLayer(int key, bool hasModalMessage)
    {
        using namespace SEASON3B;
        if (key >= INTERFACE_3DRENDERING_CAMERA_BEGIN && key <= INTERFACE_3DRENDERING_CAMERA_END)
            return true;
        if (key == INTERFACE_MESSAGEBOX) return !hasModalMessage;
        switch (key)
        {
        case INTERFACE_CHATLOGWINDOW: case INTERFACE_BLOODCASTLE_TIME:
        case INTERFACE_CHAOSCASTLE_TIME: case INTERFACE_BATTLE_SOCCER_SCORE:
        case INTERFACE_HERO_POSITION_INFO: case INTERFACE_MAINFRAME:
        case INTERFACE_ITEM_ENDURANCE_INFO: case INTERFACE_BUFF_WINDOW:
        case INTERFACE_DUELWATCH_MAINFRAME: case INTERFACE_DOPPELGANGER_FRAME:
        case INTERFACE_EMPIREGUARDIAN_TIMER: case INTERFACE_HOTKEY:
        case INTERFACE_ITEM_TOOLTIP: case INTERFACE_SYSTEMLOGWINDOW:
        case INTERFACE_SLIDEWINDOW: case INTERFACE_NAME_WINDOW:
        case INTERFACE_CRYWOLF:
            return true;
        default:
            return false;
        }
    }

    bool BlocksWorldInput(int key, const LayerState& state)
    {
        using namespace SEASON3B;
        if (key == INTERFACE_SKILL_LIST) return state.skillSelectionOpen;
        if (key == INTERFACE_PARTY_INFO_WINDOW) return state.hasParty;
        if (key == INTERFACE_SIEGEWARFARE) return state.siegeActive;
        return !IsReadOnlyLayer(key, state.hasModalMessage);
    }
}
