//////////////////////////////////////////////////////////////////////////
//  npcGateSwitch.cpp
//////////////////////////////////////////////////////////////////////////
#include "stdafx.h"
#include "UI/Legacy/UIManager.h"
#include "Engine/Object/ZzzCharacter.h"
#include "Render/Textures/ZzzTexture.h"
#include "Engine/AI/ZzzAI.h"
#include "GameLogic/NPCs/npcGateSwitch.h"

#include "Audio/DSPlaySound.h"

namespace npcGateSwitch
{
    static  const   DWORD   GATE_OPEN = static_cast<DWORD>(eBuff_CastleGateIsOpen);
    static  const   DWORD   GATE_CLOSE = 0;
    static          bool   g_isCurrentGateopen = false;
    static          int     g_iNpcCharacterKey = 0;

    void GateOpen(CHARACTER* c, OBJECT* o)
    {
        SetAction(o, 1);

        g_CharacterRegisterBuff(o, eBuff_CastleGateIsOpen);

        battleCastle::SetCastleGate_Attribute((c->PositionX), (c->PositionY), 0);

        PlayBuffer(SOUND_BC_GATE_OPEN);
    }

    void GateClose(CHARACTER* c, OBJECT* o)
    {
        SetAction(o, 0);

        g_CharacterClearBuff(o);

        battleCastle::SetCastleGate_Attribute((c->PositionX), (c->PositionY), 1);

        PlayBuffer(SOUND_BC_GATE_OPEN);
    }

    bool DoInterfaceOpen(int Key)
    {
        int Index = FindCharacterIndex(Key);
        if (Index < 0 || Index >= MAX_CHARACTERS_CLIENT) return false;
        CHARACTER* c = &CharactersClient[Index];
        OBJECT* o = &c->Object;

        g_iNpcCharacterKey = Key;
        g_isCurrentGateopen = g_isCharacterBuff(o, eBuff_CastleGateIsOpen);
        return true;
    }

    void ProcessState(int Key, BYTE GateOnOff, BYTE State)
    {
        switch (State)
        {
        case 0:
            break;

        case 1:
        {
            int        Index = FindCharacterIndex(Key);
            if (Index < 0 || Index >= MAX_CHARACTERS_CLIENT) return;
            CHARACTER* c = &CharactersClient[Index];

            if (c->MonsterIndex != MONSTER_CASTLE_GATE1) return;

            OBJECT* o = &c->Object;

            if (GateOnOff)
            {
                GateOpen(c, o);
            }
            else
            {
                GateClose(c, o);
            }
        }
        break;

        case 2:
            break;

        case 3:
            break;

        case 4:
            break;
        }
    }

    void	SendToggleGate()
    {
        int State = 0;
        if (!IsGateOpened())
        {
            State = 1;
        }

        SocketClient->ToGameServer()->SendToggleCastleGateRequest(State, g_iNpcCharacterKey);
        g_iNpcCharacterKey = 0;
    }

    bool	IsGateOpened()
    {
        return g_isCurrentGateopen;
    }
};