//////////////////////////////////////////////////////////////////////////
//  npcBreeder.cpp
//////////////////////////////////////////////////////////////////////////
#include "stdafx.h"
#ifdef _WIN32
#include <process.h>
#endif
#include "UI/Legacy/UIManager.h"
#include "Render/Models/ZzzBMD.h"
#include "Engine/Object/ZzzInfomation.h"
#include "Engine/Object/ZzzCharacter.h"
#include "Engine/Object/ZzzInventory.h"
#include "Render/Textures/ZzzTexture.h"
#include "I18N/All.h"

#include "GameLogic/NPCs/npcBreeder.h"
#include "GameLogic/Pets/GIPetManager.h"
#include "UI/NewUI/NewUISystem.h"

extern int SrcInventoryIndex;

namespace npcBreeder
{
namespace
{
constexpr size_t kRecoveryTextCapacity = 256;

void CopyRecoveryText(wchar_t* dest, const wchar_t* src)
{
    if (dest == nullptr)
    {
        return;
    }
    if (src == nullptr)
    {
        dest[0] = L'\0';
        return;
    }
    wcsncpy(dest, src, kRecoveryTextCapacity - 1);
    dest[kRecoveryTextCapacity - 1] = L'\0';
}
}  // namespace

int CalcRecoveryZen(BYTE type, wchar_t* Text)
{
    if (Text == nullptr || CharacterMachine == nullptr)
    {
        return -1;
    }
    Text[0] = L'\0';

    ITEM* ip;

    if (g_pMyInventory != nullptr)
    {
        g_pMyInventory->SetRepairEnableLevel(false);
    }

    switch (type)
    {
        case REVIVAL_DARKHORSE:
            ip = &CharacterMachine->Equipment[EQUIPMENT_HELPER];
            if (ip->Type != ITEM_DARK_HORSE_ITEM)
            {
                CopyRecoveryText(Text, I18N::Game::PetIsNotEquipped);
                return -1;
            }
            break;

        case REVIVAL_DARKSPIRIT:
            ip = &CharacterMachine->Equipment[EQUIPMENT_WEAPON_LEFT];
            if (ip->Type != ITEM_DARK_RAVEN_ITEM)
            {
                CopyRecoveryText(Text, I18N::Game::PetIsNotEquipped);
                return -1;
            }
            break;
        default:
            CopyRecoveryText(Text, I18N::Game::PetIsNotEquipped);
            return -1;
    }

    ITEM_ATTRIBUTE* p = &ItemAttribute[ip->Type];

    int maxDurability = CalcMaxDurability(ip, p, ip->Level);

    int Gold = 0;
    if (ip->Durability < maxDurability)
    {
        DWORD dwValue = 0;
        dwValue       = giPetManager::GetPetItemValue(giPetManager::GetPetInfo(ip));

        Gold = ConvertRepairGold(dwValue, ip->Durability, maxDurability, ip->Type, Text);
    }

    switch (Gold)
    {
        case 0:
            CopyRecoveryText(Text, I18N::Game::LifeHasBeenRecovered);
            break;

        default: {
            wchar_t Text2[kRecoveryTextCapacity] = {};

            if ((int)CharacterMachine->Gold < Gold)
            {
                ConvertGold((double)Gold - CharacterMachine->Gold, Text);
                mu_swprintf_trunc(Text2, I18N::Game::SZenIsLackingToRecoverLife, Text);
            }
            else
            {
                mu_swprintf_trunc(Text2, I18N::Game::SZenIsRequiredToRecoverLife, Text);
            }

            CopyRecoveryText(Text, Text2);
        }
        break;
    }

    return Gold;
}

void RecoverPet(BYTE type)
{
    wchar_t Text[kRecoveryTextCapacity] = {};
    int     Gold                        = CalcRecoveryZen(type, Text);

    if (CharacterMachine != nullptr && SocketClient != nullptr && (int)CharacterMachine->Gold >= Gold && Gold != -1)
    {
        switch (type)
        {
            case REVIVAL_DARKHORSE:
                SocketClient->ToGameServer()->SendRepairItemRequest(EQUIPMENT_HELPER, (BYTE)Gold);
                break;

            case REVIVAL_DARKSPIRIT:
                SocketClient->ToGameServer()->SendRepairItemRequest(EQUIPMENT_WEAPON_LEFT, (BYTE)Gold);
                break;
        }
        giPetManager::InitItemBackup();
    }
}
}