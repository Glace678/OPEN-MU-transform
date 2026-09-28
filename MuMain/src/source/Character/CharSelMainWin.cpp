//*****************************************************************************
// File: CharSelMainWin.cpp
//*****************************************************************************

#include "stdafx.h"
#include "CharSelMainWin.h"
#include "Core/Input/Input.h"
#include "UI/Legacy/UIMng.h"
#include "Render/Models/ZzzBMD.h"
#include "Engine/Object/ZzzInfomation.h"
#include "Engine/Object/ZzzObject.h"
#include "Engine/Object/ZzzCharacter.h"
#include "Engine/Object/ZzzInterface.h"
#include "Guild/UIGuildInfo.h"
#include "Engine/Object/ZzzOpenData.h"
#include "Render/Textures/ZzzOpenglUtil.h"
#include "Network/Server/ServerListManager.h"
#include "I18N/All.h"

#include <algorithm>
#include <utility>

#include "Scenes/SceneCommon.h"

namespace
{
    constexpr int kCharacterSlotCount = 5;
    constexpr int kButtonSpacing = 1;
    constexpr int kInfoSpacing = 2;
    constexpr int kInfoOffsetY = 5;
    constexpr int kStatPanelBaseXOffset = 346;
    constexpr int kStatPanelOffsetY = 24;
    constexpr int kJobButtonsStartY = 131;
    constexpr int kRageFighterButtonsY = 246;
    constexpr int kSummonerRow = 3;
    constexpr int kActionButtonsRowOffsetY = 325;
    constexpr int kCancelButtonOffsetX = 400;
    constexpr int kInputSpriteOffsetY = 317;
    constexpr int kInputTextOffsetX = 78;
    constexpr int kInputTextOffsetY = 21;
    constexpr int kDescriptionSpriteOffsetY = 355;
    constexpr int kDecorOffsetX = 22;
    constexpr int kDecorOffsetY = 59;
    constexpr int kAccountBlockMsgX = 320;
    constexpr int kAccountBlockPrimaryY = 330;
    constexpr int kAccountBlockSecondaryY = 348;
    constexpr int kWindowAlpha = 143;
    constexpr int kInfoSpriteHeight = 21;
    constexpr int kButtonSourceWidth = 54;
    constexpr int kButtonSourceHeight = 30;
    constexpr int kDecoSourceWidth = 189;
    constexpr int kDecoSourceHeight = 103;

    // Scene was authored at 800x600; map to the 640x480 art canvas.
    constexpr float kCanvasS = (float)REFERENCE_WIDTH / 800.0f;

    template <typename Predicate>
    bool AnyCharacter(Predicate&& predicate)
    {
        return std::any_of(
            CharactersClient,
            CharactersClient + kCharacterSlotCount,
            std::forward<Predicate>(predicate));
    }

    bool HasAccountBlockedCharacter()
    {
        return AnyCharacter([](const CHARACTER& character)
        {
            return character.Object.Live != 0
                && (character.CtlCode & CTLCODE_10ACCOUNT_BLOCKITEM);
        });
    }

    bool HasEmptyCharacterSlot()
    {
        return AnyCharacter([](const CHARACTER& character)
        {
            return character.Object.Live == 0;
        });
    }

    bool HasLiveCharacter()
    {
        return AnyCharacter([](const CHARACTER& character)
        {
            return character.Object.Live != 0;
        });
    }

    CHARACTER* GetSelectedCharacter()
    {
        if (SelectedHero < 0 || SelectedHero >= kCharacterSlotCount)
            return nullptr;
        return &CharactersClient[SelectedHero];
    }

    void RenderAccountBlockMessage()
    {
        g_pRenderText->SetTextColor(0, 0, 0, 255);
        g_pRenderText->SetBgColor(255, 255, 0, 128);
        const int msgX = int(kAccountBlockMsgX * kCanvasS);
        const int primaryY = int(kAccountBlockPrimaryY * kCanvasS);
        const int secondaryY = int(kAccountBlockSecondaryY * kCanvasS);
        g_pRenderText->RenderText(msgX, primaryY, I18N::Game::ThisAccountIsItemBlocked, 0, 0, RT3_WRITE_CENTER);
        g_pRenderText->RenderText(msgX, secondaryY, I18N::Game::PleaseCheckOnHttpMuonlineWebzenComSite, 0, 0, RT3_WRITE_CENTER);
    }
}

CCharSelMainWin::CCharSelMainWin()
{
}

CCharSelMainWin::~CCharSelMainWin()
{
}

void CCharSelMainWin::Create()
{
    const int decoArtW = int(kDecoSourceWidth * kCanvasS);
    const int decoArtH = int(kDecoSourceHeight * kCanvasS);
    const int infoArtW = REFERENCE_WIDTH - int(266 * kCanvasS);
    const int infoArtH = int(kInfoSpriteHeight * kCanvasS);
    const int btnArtW = int(kButtonSourceWidth * kCanvasS);
    const int btnArtH = int(kButtonSourceHeight * kCanvasS);

    // Release previous children before creating their new texture resources.
    const int artWinW = btnArtW * CSMW_BTN_MAX + infoArtW + int(6 * kCanvasS);
    CWin::Create(artWinW, btnArtH, -2);

    m_asprBack[CSMW_SPR_DECO].Create(kDecoSourceWidth, kDecoSourceHeight, BITMAP_LOG_IN + 2);
    m_asprBack[CSMW_SPR_DECO].SetSizeArt(decoArtW, decoArtH);
    m_asprBack[CSMW_SPR_INFO].Create(infoArtW, infoArtH);
    m_asprBack[CSMW_SPR_INFO].SetColor(0, 0, 0);
    m_asprBack[CSMW_SPR_INFO].SetAlpha(kWindowAlpha);

    // Texture frames retain their source dimensions; only the quad is resized.
    m_aBtn[CSMW_BTN_CREATE].Create(kButtonSourceWidth, kButtonSourceHeight, BITMAP_LOG_IN + 3, 4, 2, 1, 3);
    m_aBtn[CSMW_BTN_MENU].Create(kButtonSourceWidth, kButtonSourceHeight, BITMAP_LOG_IN + 4, 3, 2, 1);
    m_aBtn[CSMW_BTN_CONNECT].Create(kButtonSourceWidth, kButtonSourceHeight, BITMAP_LOG_IN + 5, 4, 2, 1, 3);
    m_aBtn[CSMW_BTN_DELETE].Create(kButtonSourceWidth, kButtonSourceHeight, BITMAP_LOG_IN + 6, 4, 2, 1, 3);

    for (int i = 0; i < CSMW_BTN_MAX; ++i)
    {
        m_aBtn[i].SetSizeArt(btnArtW, btnArtH);
        CWin::RegisterButton(&m_aBtn[i]);
    }

    m_bAccountBlockItem = HasAccountBlockedCharacter();
}

void CCharSelMainWin::PreRelease()
{
    for (int i = 0; i < CSMW_SPR_MAX; ++i)
        m_asprBack[i].Release();
}

void CCharSelMainWin::SetPosition(int nXCoord, int nYCoord)
{
    SetPositionArt((float)nXCoord / CSprite::ResolutionScaleX(),
        (float)nYCoord / CSprite::ResolutionScaleY());
}

void CCharSelMainWin::SetPositionArt(float fBaseArtX, float fBaseArtY)
{
    // CWin base stays device space for hit testing / drag.
    CWin::m_ptPos.x = int(fBaseArtX * CSprite::ResolutionScaleX());
    CWin::m_ptPos.y = int(fBaseArtY * CSprite::ResolutionScaleY());

    const float rateX = CSprite::ResolutionScaleX();
    const float rateY = CSprite::ResolutionScaleY();
    const float buttonWidth = m_aBtn[0].GetWidth() / rateX;

    m_aBtn[CSMW_BTN_CREATE].SetPositionArt(fBaseArtX, fBaseArtY);
    m_aBtn[CSMW_BTN_MENU].SetPositionArt(
        fBaseArtX + buttonWidth + kButtonSpacing * kCanvasS, fBaseArtY);

    const float infoX = m_aBtn[CSMW_BTN_MENU].GetXPos() / rateX
        + buttonWidth + kInfoSpacing * kCanvasS;
    m_asprBack[CSMW_SPR_INFO].SetPositionArt(
        infoX, fBaseArtY + kInfoOffsetY * kCanvasS);

    const float windowRightX = fBaseArtX + CWin::GetWidth() / rateX;
    const float decoWidth = m_asprBack[CSMW_SPR_DECO].GetWidth() / rateX;
    m_asprBack[CSMW_SPR_DECO].SetPositionArt(
        windowRightX - (decoWidth - kDecorOffsetX * kCanvasS),
        fBaseArtY - kDecorOffsetY * kCanvasS);

    m_aBtn[CSMW_BTN_DELETE].SetPositionArt(windowRightX - buttonWidth, fBaseArtY);
    m_aBtn[CSMW_BTN_CONNECT].SetPositionArt(
        windowRightX - (buttonWidth * 2 + kButtonSpacing * kCanvasS), fBaseArtY);
}

void CCharSelMainWin::Show(bool bShow)
{
    CWin::Show(bShow);

    for (auto& sprite : m_asprBack)
        sprite.Show(bShow);
    for (auto& button : m_aBtn)
        button.Show(bShow);
}

bool CCharSelMainWin::CursorInWin(int nArea)
{
    if (!CWin::m_bShow)
        return false;

    switch (nArea)
    {
    case WA_MOVE:
        return false;
    }

    return CWin::CursorInWin(nArea);
}

void CCharSelMainWin::UpdateDisplay()
{
    m_aBtn[CSMW_BTN_CREATE].SetEnable(HasEmptyCharacterSlot());

    const bool hasSelection = (SelectedHero > -1);
    m_aBtn[CSMW_BTN_CONNECT].SetEnable(hasSelection);
    m_aBtn[CSMW_BTN_DELETE].SetEnable(hasSelection);

    if (!HasLiveCharacter())
    {
        CUIMng& rUIMng = CUIMng::Instance();
        rUIMng.ShowWin(&rUIMng.m_CharMakeWin);
    }
}

void CCharSelMainWin::UpdateWhileActive(double dDeltaTick)
{
    CUIMng& uiManager = CUIMng::Instance();

    // The world selection is updated on mouse-down, while CButton commits on
    // mouse-up.  Synchronize here as well so Connect cannot remain disabled
    // for one frame when the selection and button event arrive in that order.
    if (SelectedHero < 0 && SelectedCharacter >= 0 && SelectedCharacter < kCharacterSlotCount)
    {
        SelectedHero = SelectedCharacter;
        m_aBtn[CSMW_BTN_CONNECT].SetEnable(true);
        m_aBtn[CSMW_BTN_DELETE].SetEnable(true);
    }

    if (m_aBtn[CSMW_BTN_CONNECT].IsClick())
    {
        ::StartGame();
    }
    else if (m_aBtn[CSMW_BTN_MENU].IsClick())
    {
        uiManager.ShowWin(&uiManager.m_SysMenuWin);
        uiManager.SetSysMenuWinShow(true);
    }
    else if (m_aBtn[CSMW_BTN_CREATE].IsClick())
    {
        uiManager.ShowWin(&uiManager.m_CharMakeWin);
    }
    else if (m_aBtn[CSMW_BTN_DELETE].IsClick())
    {
        DeleteCharacter();
    }
}

void CCharSelMainWin::RenderControls()
{
    for (auto& sprite : m_asprBack)
        sprite.Render();

    ::EnableAlphaTest();
    ::glColor4f(1.0f, 1.0f, 1.0f, 1.0f);

    g_pRenderText->SetFont(g_hFixFont);
    g_pRenderText->SetTextColor(CLRDW_WHITE);
    g_pRenderText->SetBgColor(0);

    if (m_bAccountBlockItem)
        RenderAccountBlockMessage();

    CWin::RenderButtons();
}

#if defined(MU_ENABLE_VIRTUAL_GAMEPAD_TESTS)
CCharSelMainWin::SpriteSnapshot CCharSelMainWin::DebugButtonSnapshot(int index) const
{
    if (index < 0 || index >= CSMW_BTN_MAX)
        return SpriteSnapshot{ 0, 0, 0, 0, 0.f, 0.f, 0.f };

    const CButton& button = m_aBtn[index];
    return SpriteSnapshot{
        button.GetXPos(), button.GetYPos(),
        button.GetWidth(), button.GetHeight(),
        button.GetScaleX(), button.GetScaleY(), button.GetScrHeight()
    };
}

CCharSelMainWin::SpriteSnapshot CCharSelMainWin::DebugBackSpriteSnapshot(int index) const
{
    if (index < 0 || index >= CSMW_SPR_MAX)
        return SpriteSnapshot{ 0, 0, 0, 0, 0.f, 0.f, 0.f };

    const CSprite& sprite = m_asprBack[index];
    return SpriteSnapshot{
        sprite.GetXPos(), sprite.GetYPos(),
        sprite.GetWidth(), sprite.GetHeight(),
        sprite.GetScaleX(), sprite.GetScaleY(), sprite.GetScrHeight()
    };
}
#endif

void CCharSelMainWin::DeleteCharacter()
{
    CHARACTER* selected = GetSelectedCharacter();
    if (selected == nullptr)
        return;

    CUIMng& uiManager = CUIMng::Instance();

    if (selected->GuildStatus != G_NONE)
    {
        uiManager.PopUpMsgWin(MESSAGE_DELETE_CHARACTER_GUILDWARNING);
    }
    else if (selected->CtlCode & (CTLCODE_02BLOCKITEM | CTLCODE_10ACCOUNT_BLOCKITEM))
    {
        uiManager.PopUpMsgWin(MESSAGE_DELETE_CHARACTER_ID_BLOCK);
    }
    else
    {
        uiManager.PopUpMsgWin(MESSAGE_DELETE_CHARACTER_CONFIRM);
    }
}
