//////////////////////////////////////////////////////////////////////
// NewUIItemMng.cpp: implementation of the CNewUIItemMng class.
//////////////////////////////////////////////////////////////////////

#include "stdafx.h"
#include "UI/NewUI/Inventory/NewUIItemMng.h"
#include "GameLogic/Items/CSItemOption.h"
#include "GameLogic/Pets/GIPetManager.h"
#include "Engine/Object/ZzzInfomation.h"
#include "Network/Server/SocketSystem.h"

using namespace SEASON3B;

ItemCreationParams ParseItemData(std::span<const BYTE> itemData)
{
    ItemCreationParams params = { };

    if (itemData.size() < 5)
        return params;

    params.Group = (itemData[0] >> 4) & 0xF;
    params.Number = ((itemData[0] & 0xF) << 8) + itemData[1];
    params.Level = itemData[2];
    params.Durability = itemData[3];
    auto flags = static_cast<ItemOptionFlags>(itemData[4]);
    params.WithLuck = flags & ItemOptionFlags::HasLuck;
    params.WithSkill = flags & ItemOptionFlags::HasSkill;

    const int size = static_cast<int>(itemData.size());
    int offset = 0;

    // 86-69 (P0): the server packet drives which optional bytes are present via
    // the flags, but a truncated/malformed packet can set a flag without
    // shipping the byte for it. Resolve the optional byte at itemData[5+offset]
    // only when that index actually exists; a missing byte means "no option"
    // rather than an out-of-bounds read.
    auto optionalByte = [&](void) -> const BYTE*
    {
        const int idx = 5 + offset;
        return (idx < size) ? &itemData[idx] : nullptr;
    };

    if (flags & ItemOptionFlags::HasOption)
    {
        if (const BYTE* pOpt = optionalByte())
        {
            params.OptionLevel = (*pOpt) & 0xF;
            params.OptionType = (*pOpt >> 4) & 0xF;
            offset++;
        }
    }

    if (flags & ItemOptionFlags::HasExcellent)
    {
        if (const BYTE* pExc = optionalByte())
        {
            params.ExcellentFlags = *pExc;
            offset++;
        }
    }

    if (flags & ItemOptionFlags::HasAncient)
    {
        if (const BYTE* pAnc = optionalByte())
        {
            params.AncientDiscriminator = (*pAnc) & 0xF;
            params.AncientBonusOption = (*pAnc >> 4) & 0xF;
            offset++;
        }
    }

    if (flags & ItemOptionFlags::HasHarmony)
    {
        if (const BYTE* pHam = optionalByte())
        {
            params.HasHarmonyOption = true;
            params.HarmonyOptionLevel = (*pHam) & 0xF;
            params.HarmonyOptionType = (*pHam >> 4) & 0xF;
            offset++;
        }
    }

    if (flags & ItemOptionFlags::HasSockets)
    {
        if (const BYTE* pSock = optionalByte())
        {
            params.SocketBonusOption = (*pSock >> 4) & 0xF;

            // SocketCount is the packet low nibble (0..15) straight from the
            // server. SocketOptions[] only holds MAX_SOCKETS entries, so clamp
            // to the fixed-array capacity and drop any excess sockets instead
            // of writing past the array (which would corrupt the params struct
            // and, downstream, the ITEM).
            const int rawSocketCount = (*pSock) & 0xF;
            const int socketCount = (rawSocketCount < MAX_SOCKETS) ? rawSocketCount : MAX_SOCKETS;
            params.SocketCount = static_cast<BYTE>(socketCount);

            // The one-byte socket header lives at 5+offset; the payload bytes
            // immediately follow it at (5+offset)+1+i. Only consume payload
            // bytes the packet actually carries; a truncated packet stops after
            // the sockets it ships instead of reading past the span.
            const int headerIdx = 5 + offset;
            for (int i = 0; i < socketCount; ++i)
            {
                const int idx = headerIdx + 1 + i;
                if (idx >= size)
                    break;
                params.SocketOptions[i] = itemData[idx];
            }
            offset++;
        }
    }

    return params;
}

SEASON3B::CNewUIItemMng::CNewUIItemMng()
{
    m_dwAlternate = 0;
    m_dwAvailableKeyStream = 0x80000000;
    m_UpdateTimer.SetTimer(1000);
}

SEASON3B::CNewUIItemMng::~CNewUIItemMng()
{
    DeleteAllItems();
}

ITEM* SEASON3B::CNewUIItemMng::CreateItem(std::span<const BYTE> itemData)
{
    
    return CreateItemExtended(itemData);
    
}

/// <summary>
    /// Layout:
    ///   Group:  4 bit
    ///   Number: 12 bit
    ///   Level:  8 bit
    ///   Dura:   8 bit
    ///   OptFlags: 8 bit
    ///     HasOpt
    ///     HasLuck
    ///     HasSkill
    ///     HasExc
    ///     HasAnc
    ///     HasGuardian
    ///     HasHarmony
    ///     HasSockets
    ///   Optional, depending on Flags:
    ///     Opt_Lvl 4 bit
    ///     Opt_Typ 4 bit
    ///     Exc:    8 bit
    ///     Anc_Dis 4 bit
    ///     Anc_Bon 4 bit
    ///     Harmony 8 bit
    ///     Soc_Bon 4 bit
    ///     Soc_Cnt 4 bit
    ///     Sockets n * 8 bit
    ///   
    ///  Total: 5 ~ 15 bytes.
    /// </summary>
ITEM* SEASON3B::CNewUIItemMng::CreateItemExtended(std::span<const BYTE> itemData)
{
    if (itemData.size() < 5)
        return nullptr;

    ItemCreationParams params = ParseItemData(itemData);
    auto item = CNewUIItemMng::CreateItemByParameters(&params);
    return item;
}

ITEM* SEASON3B::CNewUIItemMng::CreateItemOld(std::span<const BYTE> pbyItemPacket)
{
    WORD wType = ExtractItemType(pbyItemPacket);
    BYTE byOption380 = 0, byOptionHarmony = 0;

    byOption380 = pbyItemPacket[5];
    byOptionHarmony = pbyItemPacket[6];

    BYTE bySocketOption[5] = { pbyItemPacket[7], pbyItemPacket[8], pbyItemPacket[9], pbyItemPacket[10], pbyItemPacket[11] };

    return CNewUIItemMng::CreateItem(wType / MAX_ITEM_INDEX, wType % MAX_ITEM_INDEX, pbyItemPacket[1], pbyItemPacket[2], pbyItemPacket[3], pbyItemPacket[4], byOption380, byOptionHarmony, bySocketOption);
}

ITEM* SEASON3B::CNewUIItemMng::CreateItemByParameters(const ItemCreationParams* parameters)
{
    if (parameters == nullptr)
    {
        return nullptr;
    }

    // 86-72: Type is built straight from the packet Group/Number fields and is
    // later used to index the global ItemAttribute table (and, downstream, the
    // slot-occupancy writes in 86-71). Reject an out-of-range type up front
    // instead of reading the attribute table out of bounds. MAX_ITEM is the
    // fixed size of the ItemAttribute population.
    const int itemType = parameters->Group * MAX_ITEM_INDEX + parameters->Number;
    if (itemType < 0 || itemType >= MAX_ITEM)
    {
        return nullptr;
    }

    ITEM* pNewItem = new ITEM;
    memset(pNewItem, 0, sizeof(ITEM));
    pNewItem->RefCount = 1;
    pNewItem->bPeriodItem = parameters->WithExpiration;
    pNewItem->bExpiredPeriod = parameters->IsExpired;
    // pNewItem->lExpireTime is received by another packet? should we integrate that?
    pNewItem->Key = GenerateItemKey();
    pNewItem->Type = itemType;
    pNewItem->Level = parameters->Level;
    pNewItem->Durability = parameters->Durability;
    pNewItem->HasLuck = parameters->WithLuck;
    pNewItem->HasSkill = parameters->WithSkill;
    pNewItem->OptionType = parameters->OptionType;
    pNewItem->OptionLevel = parameters->OptionLevel;
    pNewItem->ExcellentFlags = parameters->ExcellentFlags;
    pNewItem->AncientDiscriminator = parameters->AncientDiscriminator;
    pNewItem->AncientBonusOption = parameters->AncientBonusOption;
    pNewItem->Jewel_Of_Harmony_Option = parameters->HarmonyOptionType;
    pNewItem->Jewel_Of_Harmony_OptionLevel = parameters->HarmonyOptionLevel;
    pNewItem->option_380 = parameters->HasGuardianOption;
    pNewItem->SocketCount = parameters->SocketCount;
    pNewItem->SocketSeedSetOption = parameters->SocketBonusOption;
    for (int i = 0; i < MAX_SOCKETS; ++i)
    {
        pNewItem->bySocketOption[i] = parameters->SocketOptions[i];
        if (pNewItem->bySocketOption[i] == SOCKET_EMPTY)
        {
            pNewItem->SocketSeedID[i] = SOCKET_EMPTY;
        }
        else
        {
            pNewItem->SocketSeedID[i] = pNewItem->bySocketOption[i] % SEASON4A::MAX_SOCKET_OPTION;
            pNewItem->SocketSphereLv[i] = (pNewItem->bySocketOption[i] / SEASON4A::MAX_SOCKET_OPTION) + 1;
        }
    }

    pNewItem->byColorState = ITEM_COLOR_NORMAL;

    SetItemAttributes(pNewItem);
    // SetItemAttr(pNewItem, byLevel, byOption1, ancientByte);

    m_listItem.push_back(pNewItem);
    return pNewItem;
}

ITEM* SEASON3B::CNewUIItemMng::CreateItem(
    BYTE byType, 
    BYTE bySubType,
    BYTE byLevel /* = 0 */,
    BYTE byDurability /* = 255 */,
    BYTE byOption1 /* = 0 */,
    BYTE ancientByte /* = 0 */,
    BYTE byOption380 /* = 0 */,
    BYTE byOptionHarmony /* = 0 */,
    BYTE* pbySocketOptions /*= NULL*/)
{
    ITEM* pNewItem = new ITEM;
    memset(pNewItem, 0, sizeof(ITEM));

    WORD wType = byType * MAX_ITEM_INDEX + bySubType;
    pNewItem->Key = GenerateItemKey();
    pNewItem->Type = wType;
    pNewItem->Durability = byDurability;
    pNewItem->ExcellentFlags = byOption1;
    pNewItem->AncientDiscriminator = ancientByte & 0x03;
    pNewItem->AncientBonusOption = (ancientByte & (0x04+0x08)) >> 2;
    if ((((byOption380 & 0x08) << 4) >> 7) > 0)
        pNewItem->option_380 = true;
    else
        pNewItem->option_380 = false;
    pNewItem->Jewel_Of_Harmony_Option = (byOptionHarmony & 0xf0) >> 4;
    pNewItem->Jewel_Of_Harmony_OptionLevel = byOptionHarmony & 0x0f;

    if (pbySocketOptions == NULL)
    {
        pNewItem->SocketCount = 0;
        assert(!"Socket options expected but missing");
    }
    else
    {
        pNewItem->SocketCount = MAX_SOCKETS;

        for (int i = 0; i < MAX_SOCKETS; ++i)
        {
            pNewItem->bySocketOption[i] = pbySocketOptions[i];
        }

        for (int i = 0; i < MAX_SOCKETS; ++i)
        {
            if (pbySocketOptions[i] == 0xFF)
            {
                pNewItem->SocketCount = i;
                break;
            }
            else if (pbySocketOptions[i] == 0xFE)
            {
                pNewItem->SocketSeedID[i] = SOCKET_EMPTY;
            }
            else
            {
                pNewItem->SocketSeedID[i] = pbySocketOptions[i] % SEASON4A::MAX_SOCKET_OPTION;
                pNewItem->SocketSphereLv[i] = int(pbySocketOptions[i] / SEASON4A::MAX_SOCKET_OPTION) + 1;
            }
        }

        if (g_SocketItemMgr.IsSocketItem(pNewItem))
        {
            pNewItem->SocketSeedSetOption = byOptionHarmony;
            pNewItem->Jewel_Of_Harmony_Option = 0;
            pNewItem->Jewel_Of_Harmony_OptionLevel = 0;
        }
        else
        {
            pNewItem->SocketSeedSetOption = SOCKET_EMPTY;
        }
    }

    pNewItem->byColorState = ITEM_COLOR_NORMAL;

    pNewItem->RefCount = 1;

    if (((byOption380 & 0x02) >> 1) > 0)
    {
        pNewItem->bPeriodItem = true;
    }
    else
    {
        pNewItem->bPeriodItem = false;
    }

    if (((byOption380 & 0x04) >> 2) > 0)
    {
        pNewItem->bExpiredPeriod = true;
    }
    else
    {
        pNewItem->bExpiredPeriod = false;
    }

    SetItemAttributes(pNewItem);

    m_listItem.push_back(pNewItem);
    return pNewItem;
}

ITEM* SEASON3B::CNewUIItemMng::CreateItem(ITEM* pItem)
{
    pItem->RefCount++;
    return pItem;
}

ITEM* SEASON3B::CNewUIItemMng::DuplicateItem(ITEM* pItem)
{
    ITEM* pNewItem = new ITEM;
    memcpy(pNewItem, pItem, sizeof(ITEM));
    pNewItem->Key = GenerateItemKey();
    pNewItem->RefCount = 1;
    m_listItem.push_back(pNewItem);
    return pNewItem;
}

void SEASON3B::CNewUIItemMng::DeleteItem(ITEM* pItem)
{
    if (pItem == NULL)
        return;

    if (--pItem->RefCount <= 0)
    {
        auto li = m_listItem.begin();
        for (; li != m_listItem.end(); li++)
        {
            if ((*li) == pItem)
            {
                SafeDelete(*li);
                m_listItem.erase(li);
                break;
            }
        }
    }
}

void SEASON3B::CNewUIItemMng::DeleteDuplicatedItem(ITEM* pItem)
{
    if (pItem == NULL)
        return;

    if (--pItem->RefCount <= 0)
    {
        DeleteItem(pItem);
    }
}

void SEASON3B::CNewUIItemMng::DeleteAllItems()
{
    auto li = m_listItem.begin();
    for (; li != m_listItem.end(); li++)
    {
        SafeDelete(*li);
    }
    m_listItem.clear();

    m_dwAlternate = 0;
    m_dwAvailableKeyStream = 0x80000000;
}

bool SEASON3B::CNewUIItemMng::IsEmpty()
{
    return m_listItem.empty();
}

void SEASON3B::CNewUIItemMng::Update()
{
    m_UpdateTimer.UpdateTime();
    if (m_UpdateTimer.IsTime())
    {
        auto li = m_listItem.begin();
        for (; li != m_listItem.end(); )
        {
            if ((*li)->RefCount <= 0)
            {
                delete (*li);
                li = m_listItem.erase(li);
            }
            else
                li++;
        }
    }
}

DWORD SEASON3B::CNewUIItemMng::GenerateItemKey()
{
    DWORD dwAvailableItemKey = FindAvailableKeyIndex(m_dwAvailableKeyStream);
    if (dwAvailableItemKey >= 0x8F000000)
    {
        m_dwAvailableKeyStream = 0;
        m_dwAlternate++;
        dwAvailableItemKey = FindAvailableKeyIndex(m_dwAvailableKeyStream);
    }
    return m_dwAvailableKeyStream = dwAvailableItemKey;
}

DWORD SEASON3B::CNewUIItemMng::FindAvailableKeyIndex(DWORD dwSeed)
{
    if (m_dwAlternate > 0)
    {
        auto li = m_listItem.begin();
        for (; li != m_listItem.end(); li++)
        {
            ITEM* pItem = (*li);
            if (pItem->Key == dwSeed + 1)
                return FindAvailableKeyIndex(dwSeed + 1);
        }
    }
    return dwSeed + 1;
}

WORD SEASON3B::CNewUIItemMng::ExtractItemType(std::span<const BYTE> pbyItemPacket)
{
    return pbyItemPacket[0] + (pbyItemPacket[3] & 128) * 2 + (pbyItemPacket[5] & 240) * 32;
}
