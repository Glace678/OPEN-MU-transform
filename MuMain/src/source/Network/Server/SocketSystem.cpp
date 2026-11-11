#include "stdafx.h"
#include "SocketSystem.h"
#include "I18N/All.h"

#include "Engine/Object/ZzzInventory.h"
#include "Render/Models/ZzzBMD.h"
#include "Engine/Object/ZzzCharacter.h"
#include "WSclient.h"
#include "UI/Legacy/UIControls.h"
#include "Character/CharacterManager.h"

using namespace SEASON4A;

CSocketItemMgr g_SocketItemMgr;

CSocketItemMgr::CSocketItemMgr()
{
    m_iNumEquitSetBonusOptions = 0;
    memset(m_SocketOptionInfo, 0, sizeof(m_SocketOptionInfo));
    memset(&m_StatusBonus, 0, sizeof(SOCKET_OPTION_STATUS_BONUS));
}

CSocketItemMgr::~CSocketItemMgr() {}

BOOL CSocketItemMgr::IsSocketItem(const ITEM* pItem)
{
    if (pItem == NULL)
        return FALSE;
    return IsSocketItem(pItem->Type);
}

BOOL CSocketItemMgr::IsSocketItem(const OBJECT* pObject)
{
    if (pObject == NULL)
        return FALSE;
    return IsSocketItem(pObject->Type - MODEL_SWORD);
}

BOOL CSocketItemMgr::IsSocketItem(int iItemType)
{
    switch (iItemType)
    {
        case ITEM_FLAMBERGE:
        case ITEM_SWORD_BREAKER:
        case ITEM_IMPERIAL_SWORD:
        case ITEM_FROST_MACE:
        case ITEM_ABSOLUTE_SCEPTER:
        case ITEM_STINGER_BOW:
        case ITEM_DEADLY_STAFF:
        case ITEM_IMPERIAL_STAFF:
        case ITEM_STAFF + 32:
        case ITEM_CRIMSONGLORY:
        case ITEM_SALAMANDER_SHIELD:
        case ITEM_FROST_BARRIER:
        case ITEM_GUARDIAN_SHILED:
        case ITEM_TITAN_HELM:
        case ITEM_TITAN_ARMOR:
        case ITEM_TITAN_PANTS:
        case ITEM_TITAN_GLOVES:
        case ITEM_TITAN_BOOTS:
        case ITEM_BRAVE_HELM:
        case ITEM_BRAVE_ARMOR:
        case ITEM_BRAVE_PANTS:
        case ITEM_BRAVE_GLOVES:
        case ITEM_BRAVE_BOOTS:
        case ITEM_HELM + 47:
        case ITEM_DESTORY_ARMOR:
        case ITEM_DESTORY_PANTS:
        case ITEM_DESTORY_GLOVES:
        case ITEM_DESTORY_BOOTS:
        case ITEM_HELM + 48:
        case ITEM_PHANTOM_ARMOR:
        case ITEM_PHANTOM_PANTS:
        case ITEM_PHANTOM_GLOVES:
        case ITEM_PHANTOM_BOOTS:
        case ITEM_SERAPHIM_HELM:
        case ITEM_SERAPHIM_ARMOR:
        case ITEM_SERAPHIM_PANTS:
        case ITEM_SERAPHIM_GLOVES:
        case ITEM_SERAPHIM_BOOTS:
        case ITEM_FAITH_HELM:
        case ITEM_FAITH_ARMOR:
        case ITEM_FAITH_PANTS:
        case ITEM_FAITH_GLOVES:
        case ITEM_FAITH_BOOTS:
        case ITEM_PAEWANG_MASK:
        case ITEM_PAEWANG_ARMOR:
        case ITEM_PAEWANG_PANTS:
        case ITEM_PAEWANG_GLOVES:
        case ITEM_PHAEWANG_BOOTS:
        case ITEM_HADES_HELM:
        case ITEM_HADES_ARMOR:
        case ITEM_HADES_PANTS:
        case ITEM_HADES_GLOVES:
        case ITEM_HADES_BOOTS:
        case ITEM_HELM + 53:
        case ITEM_ARMOR + 53:
        case ITEM_PANTS + 53:
        case ITEM_GLOVES + 53:
        case ITEM_BOOTS + 53:
            return TRUE;
        default:
            return FALSE;
    }
    // 	return (pItem->SocketCount > 0);
}

int CSocketItemMgr::GetSocketCategory(int iSeedID)
{
    if (iSeedID == SOCKET_EMPTY || iSeedID < 0 || iSeedID >= MAX_SOCKET_OPTION)
        return SOCKET_EMPTY;

    SOCKET_OPTION_INFO* pInfo = &m_SocketOptionInfo[SOT_SOCKET_ITEM_OPTIONS][iSeedID];
    return pInfo->m_iOptionCategory;
}

int CSocketItemMgr::GetSeedShpereSeedID(const ITEM* pItem)
{
    if (pItem == NULL)
        return SOCKET_EMPTY;

    int iSocketSeedID = SOCKET_EMPTY;

    if (pItem->Type >= ITEM_SEED_SPHERE_FIRE_1 && pItem->Type <= ITEM_SEED_SPHERE_EARTH_5)
    {
        int iCategoryIndex = (pItem->Type - (ITEM_SEED_SPHERE_FIRE_1)) % 6 + 1;
        int iLevel         = pItem->Level;
        switch (iCategoryIndex)
        {
            case 1:  // 0~9
                iSocketSeedID = 0 + iLevel;
                break;
            case 2:  // 10~15
                iSocketSeedID = 10 + iLevel;
                break;
            case 3:  // 16~20
                iSocketSeedID = 16 + iLevel;
                break;
            case 4:  // 21~28
                iSocketSeedID = 21 + iLevel;
                break;
            case 5:  // 29~33
                iSocketSeedID = 29 + iLevel;
                break;
            case 6:  // 34~40
                iSocketSeedID = 34 + iLevel;
                break;
        }
    }

    if (iSocketSeedID < 0 || iSocketSeedID >= MAX_SOCKET_OPTION)
        return SOCKET_EMPTY;

    return iSocketSeedID;
}

__int64 CSocketItemMgr::CalcSocketBonusItemValue(const ITEM* pItem, __int64 iOrgGold)
{
    __int64 iGoldResult = 0;

    if (IsSocketItem(pItem))
    {
        const int socketCount = std::clamp<int>(pItem->SocketCount, 0, MAX_SOCKETS);
        iGoldResult += iOrgGold * (socketCount * 0.8f);

        ITEM TempSeedSphere{};
        for (int i = 0; i < socketCount; ++i)
        {
            if (pItem->SocketSeedID[i] == SOCKET_EMPTY)
                continue;

            const int sphereLv = pItem->SocketSphereLv[i];
            if (sphereLv < 1 || sphereLv > MAX_SPHERE_LEVEL)
                continue;

            int iSeedSphereType = -1;
            if (pItem->SocketSeedID[i] >= 0 && pItem->SocketSeedID[i] <= 9)
                iSeedSphereType = 0;
            else if (pItem->SocketSeedID[i] >= 10 && pItem->SocketSeedID[i] <= 15)
                iSeedSphereType = 1;
            else if (pItem->SocketSeedID[i] >= 16 && pItem->SocketSeedID[i] <= 20)
                iSeedSphereType = 2;
            else if (pItem->SocketSeedID[i] >= 21 && pItem->SocketSeedID[i] <= 28)
                iSeedSphereType = 3;
            else if (pItem->SocketSeedID[i] >= 29 && pItem->SocketSeedID[i] <= 33)
                iSeedSphereType = 4;
            else if (pItem->SocketSeedID[i] >= 34 && pItem->SocketSeedID[i] <= 40)
                iSeedSphereType = 5;
            else
                continue;

            TempSeedSphere.Type = ITEM_SEED_SPHERE_FIRE_1 + (sphereLv - 1) * MAX_SOCKET_TYPES + iSeedSphereType;
            iGoldResult += ItemValue(&TempSeedSphere, 0);
        }
    }

    return iGoldResult;
}

int CSocketItemMgr::CalcSocketOptionValue(int iOptionType, float fOptionValue)
{
    switch (iOptionType)
    {
        case 1:
            return int(fOptionValue);
        case 2:
            return int(fOptionValue);
        case 3: {
            if (fOptionValue == 0.0f || CharacterAttribute == NULL)
                return 0;

            WORD wLevel;

            if (gCharacterManager.IsMasterLevel(CharacterAttribute->Class) == true)
                wLevel = CharacterAttribute->Level + Master_Level_Data.nMLevel;
            else
                wLevel = CharacterAttribute->Level;

            return int((float)wLevel / fOptionValue);
        }
        case 4: {
            if (fOptionValue == 0.0f || CharacterAttribute == NULL || Hero == NULL)
                return 0;

            DWORD wLifeMax;

            if (gCharacterManager.IsMasterLevel(Hero->Class) == true)
                wLifeMax = Master_Level_Data.wMaxLife;
            else
                wLifeMax = CharacterAttribute->LifeMax;

            return int((float)wLifeMax / fOptionValue);
        }
        case 5: {
            if (fOptionValue == 0.0f || CharacterAttribute == NULL || Hero == NULL)
                return 0;

            DWORD wManaMax;
            if (gCharacterManager.IsMasterLevel(Hero->Class) == true)
                wManaMax = Master_Level_Data.wMaxMana;
            else
                wManaMax = CharacterAttribute->ManaMax;

            return int((float)wManaMax / fOptionValue);
        }
        default:
            return 0;
    }
}

void CSocketItemMgr::CalcSocketOptionValueText(wchar_t* pszOptionValueText, int iOptionType, float fOptionValue)
{
    if (pszOptionValueText == NULL)
        return;

    wchar_t szTemp[16]{};
    switch (iOptionType)
    {
        case 2:
            mu_swprintf_trunc(szTemp, L"+%d%%", CalcSocketOptionValue(iOptionType, fOptionValue));
            break;
        default:
            mu_swprintf_trunc(szTemp, L"+%d", CalcSocketOptionValue(iOptionType, fOptionValue));
            break;
    }
    mu_swprintf_trunc(pszOptionValueText, _countof(szTemp), L"%ls", szTemp);
}

void CSocketItemMgr::CreateSocketOptionText(wchar_t* pszOptionText, int iSeedID, int iSphereLv)
{
    if (pszOptionText == NULL)
        return;

    pszOptionText[0] = L'\0';
    if (iSeedID < 0 || iSeedID >= MAX_SOCKET_OPTION || iSphereLv < 1 || iSphereLv > MAX_SPHERE_LEVEL)
        return;

    wchar_t szOptionValueText[16] = {
        0,
    };

    SOCKET_OPTION_INFO* pInfo = &m_SocketOptionInfo[SOT_SOCKET_ITEM_OPTIONS][iSeedID];

    auto fOptionValue = (float)pInfo->m_iOptionValue[iSphereLv - 1];

    CalcSocketOptionValueText(szOptionValueText, pInfo->m_bOptionType, fOptionValue);

    const int      categoryOffset = std::clamp<int>(pInfo->m_iOptionCategory - 1, 0, MAX_SOCKET_TYPES - 1);
    const wchar_t* categoryName   = I18N::Game::Lookup(2640 + categoryOffset);
    wchar_t        szTemp[MAX_SOCKET_OPTION_NAME_LENGTH]{};
    mu_swprintf_trunc(szTemp, L"%ls(%ls %ls)", categoryName ? categoryName : L"", pInfo->m_szOptionName,
                      szOptionValueText);
    mu_swprintf_trunc(pszOptionText, MAX_SOCKET_OPTION_NAME_LENGTH, L"%ls", szTemp);
}

extern int SkipNum;

int CSocketItemMgr::AttachToolTipForSocketItem(const ITEM* pItem, int iTextNum)
{
    if (pItem == NULL || pItem->SocketCount == 0)
        return iTextNum;

    const int socketCount = std::clamp<int>(pItem->SocketCount, 0, MAX_SOCKETS);

    mu_swprintf(TextList[iTextNum], L"\n");
    ++iTextNum;
    ++SkipNum;
    mu_swprintf_trunc(TextList[iTextNum], L"%ls %ls", I18N::Game::Socket, I18N::Game::ItemOptionInfo);
    TextListColor[iTextNum] = TEXT_COLOR_PURPLE;
    TextBold[iTextNum]      = false;
    ++iTextNum;
    mu_swprintf(TextList[iTextNum], L"\n");
    ++iTextNum;
    ++SkipNum;

    wchar_t szOptionText[64] = {
        0,
    };
    wchar_t szOptionValueText[16] = {
        0,
    };

    for (int i = 0; i < socketCount; ++i)
    {
        if (pItem->SocketSeedID[i] == SOCKET_EMPTY)
        {
            mu_swprintf_trunc(szOptionText, _countof(szOptionText), L"%ls", I18N::Game::NoItemApplication);
            TextListColor[iTextNum] = TEXT_COLOR_GRAY;
        }
        else if (pItem->SocketSeedID[i] < MAX_SOCKET_OPTION && pItem->SocketSphereLv[i] >= 1 &&
                 pItem->SocketSphereLv[i] <= MAX_SPHERE_LEVEL)
        {
            CreateSocketOptionText(szOptionText, pItem->SocketSeedID[i], pItem->SocketSphereLv[i]);
            TextListColor[iTextNum] = TEXT_COLOR_BLUE;
        }
        else
        {
            szOptionText[0]         = L'\0';
            TextListColor[iTextNum] = TEXT_COLOR_GRAY;
        }

        mu_swprintf_trunc(TextList[iTextNum], I18N::Game::SocketDS, i + 1, szOptionText);
        TextBold[iTextNum] = false;
        ++iTextNum;
    }

    if (pItem->SocketSeedSetOption != SOCKET_EMPTY && pItem->SocketSeedSetOption < MAX_SOCKET_OPTION)
    {
        SOCKET_OPTION_INFO* pInfo = &m_SocketOptionInfo[SOT_MIX_SET_BONUS_OPTIONS][pItem->SocketSeedSetOption];
        if (pInfo && pInfo->m_iOptionValue[0] > 0)
        {
            mu_swprintf(TextList[iTextNum], L"\n");
            ++iTextNum;
            ++SkipNum;

            mu_swprintf_trunc(TextList[iTextNum], L"%ls", I18N::Game::BonusSocketOption);
            TextListColor[iTextNum] = TEXT_COLOR_PURPLE;
            TextBold[iTextNum]      = false;
            ++iTextNum;
            mu_swprintf(TextList[iTextNum], L"\n");
            ++iTextNum;
            ++SkipNum;

            CalcSocketOptionValueText(szOptionValueText, pInfo->m_bOptionType, (float)pInfo->m_iOptionValue[0]);
            mu_swprintf_trunc(TextList[iTextNum], L"%ls %ls", pInfo->m_szOptionName, szOptionValueText);
            TextListColor[iTextNum] = TEXT_COLOR_BLUE;
            TextBold[iTextNum]      = false;
            ++iTextNum;
        }
    }
    return iTextNum;
}

int CSocketItemMgr::AttachToolTipForSeedSphereItem(const ITEM* pItem, int iTextNum)
{
    if (pItem == NULL)
        return iTextNum;

    SOCKET_OPTION_INFO* pInfo = NULL;

    if (pItem->Type >= ITEM_SEED_FIRE && pItem->Type <= ITEM_SEED_EARTH)
    {
        int            iCategoryIndex = pItem->Type - (ITEM_SEED_FIRE) + 1;
        const wchar_t* elementName    = I18N::Game::Lookup(2640 + iCategoryIndex - 1);
        mu_swprintf_trunc(TextList[iTextNum], I18N::Game::ElementS, elementName ? elementName : L"");
        TextListColor[iTextNum] = TEXT_COLOR_WHITE;
        TextBold[iTextNum]      = false;
        ++iTextNum;

        mu_swprintf(TextList[iTextNum], L"\n");
        ++iTextNum;
        ++SkipNum;

        int iSocketSeedID = SOCKET_EMPTY;
        int iLevel        = pItem->Level;
        switch (iCategoryIndex)
        {
            case 1:  // 0~9
                iSocketSeedID = 0 + iLevel;
                break;
            case 2:  // 10~15
                iSocketSeedID = 10 + iLevel;
                break;
            case 3:  // 16~20
                iSocketSeedID = 16 + iLevel;
                break;
            case 4:  // 21~28
                iSocketSeedID = 21 + iLevel;
                break;
            case 5:  // 29~33
                iSocketSeedID = 29 + iLevel;
                break;
            case 6:  // 34~40
                iSocketSeedID = 34 + iLevel;
                break;
        }
        if (iSocketSeedID < 0 || iSocketSeedID >= MAX_SOCKET_OPTION)
            return iTextNum;

        pInfo = &m_SocketOptionInfo[SOT_SOCKET_ITEM_OPTIONS][iSocketSeedID];
        mu_swprintf_trunc(TextList[iTextNum], L"%ls", pInfo->m_szOptionName);
        TextListColor[iTextNum] = TEXT_COLOR_BLUE;
        TextBold[iTextNum]      = false;
        ++iTextNum;
    }
    else if (pItem->Type >= ITEM_SPHERE_MONO && pItem->Type <= ITEM_SPHERE_5)
    {
        int iSphereLevel = pItem->Type - (ITEM_SPHERE_MONO) + 1;
        mu_swprintf_trunc(TextList[iTextNum], I18N::Game::LevelD, iSphereLevel);
        TextListColor[iTextNum] = TEXT_COLOR_WHITE;
        TextBold[iTextNum]      = false;
        ++iTextNum;
    }
    else if (pItem->Type >= ITEM_SEED_SPHERE_FIRE_1 && pItem->Type <= ITEM_SEED_SPHERE_EARTH_5)
    {
        int            iCategoryIndex = (pItem->Type - (ITEM_SEED_SPHERE_FIRE_1)) % 6 + 1;
        const wchar_t* elementName    = I18N::Game::Lookup(2640 + iCategoryIndex - 1);
        mu_swprintf_trunc(TextList[iTextNum], I18N::Game::ElementS, elementName ? elementName : L"");
        TextListColor[iTextNum] = TEXT_COLOR_WHITE;
        TextBold[iTextNum]      = false;
        ++iTextNum;

        mu_swprintf(TextList[iTextNum], L"\n");
        ++iTextNum;
        ++SkipNum;

        int iSocketSeedID = SOCKET_EMPTY;
        int iLevel        = pItem->Level;
        switch (iCategoryIndex)
        {
            case 1:  // 0~9
                iSocketSeedID = 0 + iLevel;
                break;
            case 2:  // 10~15
                iSocketSeedID = 10 + iLevel;
                break;
            case 3:  // 16~20
                iSocketSeedID = 16 + iLevel;
                break;
            case 4:  // 21~28
                iSocketSeedID = 21 + iLevel;
                break;
            case 5:  // 29~33
                iSocketSeedID = 29 + iLevel;
                break;
            case 6:  // 34~40
                iSocketSeedID = 34 + iLevel;
                break;
        }
        if (iSocketSeedID < 0 || iSocketSeedID >= MAX_SOCKET_OPTION)
            return iTextNum;

        pInfo = &m_SocketOptionInfo[SOT_SOCKET_ITEM_OPTIONS][iSocketSeedID];

        wchar_t szOptionValueText[16] = {
            0,
        };

        const int sphereIndex = std::clamp<int>((pItem->Type - (ITEM_SEED_SPHERE_FIRE_1)) / 6, 0, MAX_SPHERE_LEVEL - 1);
        auto      fOptionValue = (float)pInfo->m_iOptionValue[sphereIndex];
        CalcSocketOptionValueText(szOptionValueText, pInfo->m_bOptionType, fOptionValue);

        mu_swprintf_trunc(TextList[iTextNum], L"%ls %ls", pInfo->m_szOptionName, szOptionValueText);
        TextListColor[iTextNum] = TEXT_COLOR_BLUE;
        TextBold[iTextNum]      = false;
        ++iTextNum;
    }

    return iTextNum;
}

BOOL CSocketItemMgr::IsSocketSetOptionEnabled()
{
    return (!m_EquipSetBonusList.empty());
}

void CSocketItemMgr::RenderToolTipForSocketSetOption(int iPos_x, int iPos_y)
{
    if (IsSocketSetOptionEnabled())
    {
        g_pRenderText->SetTextColor(255, 255, 255, 255);
        g_pRenderText->SetBgColor(100, 0, 0, 0);

        int PosX, PosY;

        PosX = iPos_x + 95;  //+60;
        PosY = iPos_y + 40;

        BYTE TextNum  = 0;
        BYTE SkipNum  = 0;
        BYTE setIndex = 0;

        mu_swprintf(TextList[TextNum], L"\n");
        TextListColor[TextNum] = 0;
        TextBold[TextNum]      = false;
        TextNum++;
        SkipNum++;
        mu_swprintf(TextList[TextNum], L"\n");
        TextListColor[TextNum] = 0;
        TextBold[TextNum]      = false;
        TextNum++;
        SkipNum++;
        mu_swprintf(TextList[TextNum], L"\n");
        TextListColor[TextNum] = 0;
        TextBold[TextNum]      = false;
        TextNum++;
        SkipNum++;

        mu_swprintf_trunc(TextList[TextNum], L"%ls", I18N::Game::SocketPackageOption);
        TextListColor[TextNum] = TEXT_COLOR_PURPLE;
        TextBold[TextNum]      = true;
        TextNum++;

        wchar_t szOptionText[64] = {
            0,
        };
        wchar_t szOptionValueText[16] = {
            0,
        };
        SOCKET_OPTION_INFO* pInfo = NULL;
        for (std::deque<DWORD>::iterator iter = m_EquipSetBonusList.begin(); iter != m_EquipSetBonusList.end(); ++iter)
        {
            if (*iter >= MAX_SOCKET_OPTION || TextNum >= 50)
                continue;
            pInfo = &m_SocketOptionInfo[SOT_EQUIP_SET_BONUS_OPTIONS][*iter];
            CalcSocketOptionValueText(szOptionValueText, pInfo->m_bOptionType, (float)pInfo->m_iOptionValue[0]);
            mu_swprintf_trunc(TextList[TextNum], L"%ls %ls", pInfo->m_szOptionName, szOptionValueText);
            TextListColor[TextNum] = TEXT_COLOR_BLUE;
            TextBold[TextNum]      = false;
            TextNum++;
        }

        RenderTipTextList(PosX, PosY, TextNum, 140, RT3_SORT_CENTER);
    }
}

void CSocketItemMgr::CheckSocketSetOption()
{
    m_EquipSetBonusList.clear();
    if (CharacterMachine == NULL)
        return;

    int                 iSeedSum[6] = {0, 0, 0, 0, 0, 0};
    ITEM*               pItem       = NULL;
    SOCKET_OPTION_INFO* pInfo       = NULL;

    for (int i = 0; i < MAX_EQUIPMENT; ++i)
    {
        pItem                 = &CharacterMachine->Equipment[i];
        const int socketCount = std::clamp<int>(pItem->SocketCount, 0, MAX_SOCKETS);
        for (int j = 0; j < socketCount; ++j)
        {
            const BYTE seedId = pItem->SocketSeedID[j];
            if (seedId != SOCKET_EMPTY && seedId < MAX_SOCKET_OPTION)
            {
                pInfo              = &m_SocketOptionInfo[SOT_SOCKET_ITEM_OPTIONS][seedId];
                const int catIndex = pInfo->m_iOptionCategory - 1;
                if (catIndex >= 0 && catIndex < 6)
                    ++iSeedSum[catIndex];
            }
        }
    }

    const int bonusCount = std::clamp<int>(m_iNumEquitSetBonusOptions, 0, MAX_SOCKET_OPTION);
    for (int i = 0; i < bonusCount; ++i)
    {
        bool  matchedAll = true;
        BYTE* pbySetTest = m_SocketOptionInfo[SOT_EQUIP_SET_BONUS_OPTIONS][i].m_bySocketCheckInfo;
        for (int j = 0; j < 6; ++j)
        {
            if (iSeedSum[j] < pbySetTest[j])
            {
                matchedAll = false;
                break;
            }
        }
        if (!matchedAll)
            continue;

        const int optionId = m_SocketOptionInfo[SOT_EQUIP_SET_BONUS_OPTIONS][i].m_iOptionID;
        if (optionId >= 0 && optionId < MAX_SOCKET_OPTION)
            m_EquipSetBonusList.push_back(static_cast<DWORD>(optionId));
    }
}

int CSocketItemMgr::GetSocketOptionValue(const ITEM* pItem, int iSocketIndex)
{
    if (pItem == NULL || iSocketIndex < 0 || iSocketIndex >= MAX_SOCKETS || iSocketIndex >= pItem->SocketCount)
        return 0;

    const BYTE seedId   = pItem->SocketSeedID[iSocketIndex];
    const BYTE sphereLv = pItem->SocketSphereLv[iSocketIndex];
    if (seedId != SOCKET_EMPTY && seedId < MAX_SOCKET_OPTION && sphereLv >= 1 && sphereLv <= MAX_SPHERE_LEVEL)
    {
        SOCKET_OPTION_INFO* pInfo = &m_SocketOptionInfo[SOT_SOCKET_ITEM_OPTIONS][seedId];
        auto fOptionValue         = (float)pInfo->m_iOptionValue[sphereLv - 1];
        return CalcSocketOptionValue(pInfo->m_bOptionType, fOptionValue);
    }

    return 0;
}

void CSocketItemMgr::CalcSocketStatusBonus()
{
    memset(&m_StatusBonus, 0, sizeof(SOCKET_OPTION_STATUS_BONUS));
    m_StatusBonus.m_fDefenceRateBonus = 1.0f;
    if (CharacterMachine == NULL)
        return;

    ITEM*               pItem = NULL;
    SOCKET_OPTION_INFO* pInfo = NULL;

    for (int i = 0; i < MAX_EQUIPMENT; ++i)
    {
        pItem = &CharacterMachine->Equipment[i];

        if (!IsSocketItem(pItem))
            continue;

        const int socketCount = std::clamp<int>(pItem->SocketCount, 0, MAX_SOCKETS);
        for (int j = 0; j < socketCount; ++j)
        {
            const BYTE seedId   = pItem->SocketSeedID[j];
            const BYTE sphereLv = pItem->SocketSphereLv[j];
            if (seedId != SOCKET_EMPTY && seedId < MAX_SOCKET_OPTION && sphereLv >= 1 && sphereLv <= MAX_SPHERE_LEVEL)
            {
                pInfo             = &m_SocketOptionInfo[SOT_SOCKET_ITEM_OPTIONS][seedId];
                auto fOptionValue = (float)pInfo->m_iOptionValue[sphereLv - 1];
                int  iBonus       = CalcSocketOptionValue(pInfo->m_bOptionType, fOptionValue);

                switch (pInfo->m_iOptionID)
                {
                    case SOPT_ATTACK_N_MAGIC_DAMAGE_BONUS_BY_LEVEL:
                    case SOPT_ATTACK_N_MAGIC_DAMAGE_BONUS:
                        m_StatusBonus.m_iAttackDamageMinBonus += iBonus;
                        m_StatusBonus.m_iAttackDamageMaxBonus += iBonus;
                        break;
                    case SOPT_ATTACK_SPEED_BONUS:
                        m_StatusBonus.m_iAttackSpeedBonus += iBonus;
                        break;
                    case SOPT_ATTACT_N_MAGIC_DAMAGE_MAX_BONUS:
                        m_StatusBonus.m_iAttackDamageMaxBonus += iBonus;
                        break;
                    case SOPT_ATTACK_N_MAGIC_DAMAGE_MIN_BONUS:
                        m_StatusBonus.m_iAttackDamageMinBonus += iBonus;
                        break;
                    case SOPT_DEFENCE_RATE_BONUS:
                        m_StatusBonus.m_fDefenceRateBonus *= 1.0f + iBonus * 0.01f;
                        break;
                    case SOPT_DEFENCE_BONUS:
                        m_StatusBonus.m_iDefenceBonus += iBonus;
                        break;
                    case SOPT_SHIELD_DEFENCE_BONUS:
                        m_StatusBonus.m_iShieldDefenceBonus += iBonus;
                        break;
                    case SOPT_SKILL_DAMAGE_BONUS:
                        m_StatusBonus.m_iSkillAttackDamageBonus += iBonus;
                        break;
                    case SOPT_ATTACK_RATE_BONUS:
                        m_StatusBonus.m_iAttackRateBonus += iBonus;
                        break;
                    case SOPT_STRENGTH_BONUS:
                        m_StatusBonus.m_iStrengthBonus += iBonus;
                        break;
                    case SOPT_DEXTERITY_BONUS:
                        m_StatusBonus.m_iDexterityBonus += iBonus;
                        break;
                    case SOPT_VITALITY_BONUS:
                        m_StatusBonus.m_iVitalityBonus += iBonus;
                        break;
                    case SOPT_ENERGY_BONUS:
                        m_StatusBonus.m_iEnergyBonus += iBonus;
                        break;
                }
            }
        }

        if (pItem->SocketSeedSetOption != SOCKET_EMPTY && pItem->SocketSeedSetOption < MAX_SOCKET_OPTION)
        {
            pInfo      = &m_SocketOptionInfo[SOT_MIX_SET_BONUS_OPTIONS][pItem->SocketSeedSetOption];
            int iBonus = CalcSocketOptionValue(pInfo->m_bOptionType, (float)pInfo->m_iOptionValue[0]);

            switch (pInfo->m_iOptionID)
            {
                case SBOPT_ATTACK_DAMAGE_BONUS:
                    m_StatusBonus.m_iAttackDamageMinBonus += iBonus;
                    m_StatusBonus.m_iAttackDamageMaxBonus += iBonus;
                    break;
                case SBOPT_SKILL_DAMAGE_BONUS:
                case SBOPT_SKILL_DAMAGE_BONUS_2:
                    m_StatusBonus.m_iSkillAttackDamageBonus += iBonus;
                    break;
                case SBOPT_MAGIC_POWER_BONUS:
                    m_StatusBonus.m_iAttackDamageMinBonus += iBonus;
#ifdef YDG_FIX_SOCKET_MISSING_MAGIC_POWER_BONUS
                    m_StatusBonus.m_iMagicPowerMinBonus += iBonus;
                    m_StatusBonus.m_iMagicPowerMaxBonus += iBonus;
#else
                    m_StatusBonus.m_iMagicPowerBonus += iBonus;
#endif
                    break;
                case SBOPT_DEFENCE_BONUS:
                    m_StatusBonus.m_iDefenceBonus += iBonus;
                    break;
            }
        }
    }
}

void CSocketItemMgr::OpenSocketItemScript(const wchar_t* szFileName)
{
    if (szFileName == NULL)
        return;

    FILE* fp = _wfopen(szFileName, L"rb");
    if (fp == NULL)
    {
        wchar_t Text[256]{};
        mu_swprintf_trunc(Text, L"%ls - File not exist.", szFileName);
        g_ErrorReport.Write(Text);
        MessageBox(g_hWnd, Text, NULL, MB_OK);
        SendMessage(g_hWnd, WM_DESTROY, 0, 0);
        return;
    }

    memset(m_SocketOptionInfo, 0, sizeof(m_SocketOptionInfo));

    int  iSize     = sizeof(SOCKET_OPTION_INFO_FILE);
    bool truncated = false;
    for (int j = 0; j < MAX_SOCKET_OPTION_TYPES && !truncated; ++j)
    {
        for (int i = 0; i < MAX_SOCKET_OPTION; ++i)
        {
            SOCKET_OPTION_INFO_FILE current{};
            if (fread(&current, iSize, 1, fp) != 1)
            {
                g_ErrorReport.Write(L"%ls - Truncated socket script at [%d][%d].\r\n", szFileName, j, i);
                truncated = true;
                break;
            }
            BuxConvert((BYTE*)&current, iSize);

            auto target                    = &m_SocketOptionInfo[j][i];
            target->m_iOptionCategory      = current.m_iOptionCategory;
            target->m_iOptionID            = current.m_iOptionID;
            target->m_bOptionType          = current.m_bOptionType;
            target->m_iOptionValue[0]      = current.m_iOptionValue[0];
            target->m_iOptionValue[1]      = current.m_iOptionValue[1];
            target->m_iOptionValue[2]      = current.m_iOptionValue[2];
            target->m_iOptionValue[3]      = current.m_iOptionValue[3];
            target->m_iOptionValue[4]      = current.m_iOptionValue[4];
            target->m_bySocketCheckInfo[0] = current.m_bySocketCheckInfo[0];
            target->m_bySocketCheckInfo[1] = current.m_bySocketCheckInfo[1];
            target->m_bySocketCheckInfo[2] = current.m_bySocketCheckInfo[2];
            target->m_bySocketCheckInfo[3] = current.m_bySocketCheckInfo[3];
            target->m_bySocketCheckInfo[4] = current.m_bySocketCheckInfo[4];
            target->m_bySocketCheckInfo[5] = current.m_bySocketCheckInfo[5];

            CMultiLanguage::ConvertFromUtf8(target->m_szOptionName, current.m_szOptionName,
                                            MAX_SOCKET_OPTION_NAME_LENGTH);
        }
    }

    fclose(fp);

    m_iNumEquitSetBonusOptions = 0;
    for (int i = 0; i < MAX_SOCKET_OPTION; ++i)
    {
        BYTE* pbySetTest = m_SocketOptionInfo[SOT_EQUIP_SET_BONUS_OPTIONS][i].m_bySocketCheckInfo;
        if (pbySetTest[0] + pbySetTest[1] + pbySetTest[2] + pbySetTest[3] + pbySetTest[4] + pbySetTest[5] == 0)
            break;
        m_iNumEquitSetBonusOptions = i + 1;
    }
}