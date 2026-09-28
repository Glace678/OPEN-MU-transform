//*****************************************************************************
// File: SysMenuWin.cpp
//*****************************************************************************

#include "stdafx.h"
#include "UI/Windows/SysMenuWin.h"
#include "I18N/All.h"

#include "Core/Input/Input.h"
#include "UI/Legacy/UIMng.h"
#include "Engine/Object/ZzzInfomation.h"
#include "Scenes/SceneCore.h"

#include "Audio/DSPlaySound.h"
#include "UI/NewUI/NewUISystem.h"

#include "Network/Server/WSclient.h"
#include "Core/Utilities/Log/muConsoleDebug.h"

#define	SMW_BTN_GAP		4

extern EGameScene  SceneFlag;
extern bool LogOut;

CSysMenuWin::CSysMenuWin()
{
}

CSysMenuWin::~CSysMenuWin()
{
}

void CSysMenuWin::Create()
{
    CInput rInput = CInput::Instance();
    // Modal dim backdrop sized to the art canvas; hit size stays device space.
    CWin::Create(REFERENCE_WIDTH, REFERENCE_HEIGHT);
    m_Size.cx = int(REFERENCE_WIDTH * CSprite::ResolutionScaleX());
    m_Size.cy = int(REFERENCE_HEIGHT * CSprite::ResolutionScaleY());

    SImgInfo aiiBack[WE_BG_MAX] =
    {
        { BITMAP_SYS_WIN, 0, 0, 128, 128 },
        { BITMAP_SYS_WIN + 1, 0, 0, 213, 64 },
        { BITMAP_SYS_WIN + 2, 0, 0, 213, 43 },
        { BITMAP_SYS_WIN + 3, 0, 0, 5, 8 },
        { BITMAP_SYS_WIN + 4, 0, 0, 5, 8 }
    };
    m_winBack.Create(aiiBack, 1, 10);

    const wchar_t* apszBtnText[SMW_BTN_MAX] =
    { I18N::Game::ExitGame, I18N::Game::SelectServer, I18N::Game::Option385, I18N::Game::Close388 };
    DWORD adwBtnClr[4] =
    { CLRDW_BR_GRAY, CLRDW_BR_GRAY, CLRDW_WHITE, 0 };
    for (int i = 0; i < SMW_BTN_MAX; ++i)
    {
        m_aBtn[i].Create(108, 30, BITMAP_TEXT_BTN, 4, 2, 1);
        m_aBtn[i].SetText(apszBtnText[i], adwBtnClr);
        CWin::RegisterButton(&m_aBtn[i]);
    }

    switch (SceneFlag)
    {
    case LOG_IN_SCENE:
        m_aBtn[SMW_BTN_SERVER_SEL].SetEnable(false);
        m_winBack.SetLine(6);
        break;
    case CHARACTER_SCENE:
        m_aBtn[SMW_BTN_SERVER_SEL].SetEnable(true);
        m_winBack.SetLine(10);
        break;
    }

    SetPosition((rInput.GetScreenWidth() - m_winBack.GetWidth()) / 2,
        (rInput.GetScreenHeight() - m_winBack.GetHeight()) / 2);
}

void CSysMenuWin::PreRelease()
{
    m_winBack.Release();
}

void CSysMenuWin::SetPosition(int nXCoord, int nYCoord)
{
    // Window base position is device-space; all child offsets are art units.
    const float backArtX = (float)nXCoord / CSprite::ResolutionScaleX();
    const float backArtY = (float)nYCoord / CSprite::ResolutionScaleY();
    m_winBack.SetPositionArt(backArtX, backArtY);

    const float winArtX = m_winBack.GetXPos() / CSprite::ResolutionScaleX();
    const float winArtY = m_winBack.GetYPos() / CSprite::ResolutionScaleY();
    const float winArtW = m_winBack.GetWidth() / CSprite::ResolutionScaleX();
    const float winArtH = m_winBack.GetHeight() / CSprite::ResolutionScaleY();
    const float btnArtW = m_aBtn[0].GetWidth() / CSprite::ResolutionScaleX();
    const float btnArtH = m_aBtn[0].GetHeight() / CSprite::ResolutionScaleY();

    float fBtnPosX = winArtX + (winArtW - btnArtW) / 2.0f;
    float fBtnGap = (float)SMW_BTN_GAP + btnArtH;
    float fBtnPosBaseTop = winArtY + 33.0f;
    for (int i = 0; i < SMW_BTN_OPTION; ++i)
        m_aBtn[i].SetPositionArt(fBtnPosX, fBtnPosBaseTop + i * fBtnGap);

    float fCloseBtnPosY = winArtY + winArtH - 52.0f;
    m_aBtn[SMW_BTN_CLOSE].SetPositionArt(fBtnPosX, fCloseBtnPosY);
    m_aBtn[SMW_BTN_OPTION].SetPositionArt(fBtnPosX, fCloseBtnPosY - fBtnGap);
}
void CSysMenuWin::Show(bool bShow)
{
    CWin::Show(bShow);

    m_winBack.Show(bShow);
    for (int i = 0; i < SMW_BTN_MAX; ++i)
        m_aBtn[i].Show(bShow);
}

bool CSysMenuWin::CursorInWin(int nArea)
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

void CSysMenuWin::UpdateWhileActive(double dDeltaTick)
{
    if (m_aBtn[SMW_BTN_GAME_END].IsClick())
    {
        // Exit immediately -- the old flow showed a 5-second countdown popup.
        ::PostMessage(g_hWnd, WM_CLOSE, 0, 0);
    }
    else if (m_aBtn[SMW_BTN_SERVER_SEL].IsClick())
    {
        LogOut = true;
        SocketClient->ToGameServer()->SendLogOut(LogOutType::BackToServerSelection);
        g_ConsoleDebug->Write(MCD_SEND, L"0xF1 [SendRequestLogOut] 2");

        CUIMng& rUIMng = CUIMng::Instance();
        rUIMng.HideWin(this);
        rUIMng.HideWin(&rUIMng.m_CharSelMainWin);
    }
    else if (m_aBtn[SMW_BTN_OPTION].IsClick())
    {
        CUIMng& rUIMng = CUIMng::Instance();
        rUIMng.HideWin(this);
        g_pNewUISystem->Show(SEASON3B::INTERFACE_OPTION);
    }
    else if (m_aBtn[SMW_BTN_CLOSE].IsClick())
    {
        CUIMng::Instance().HideWin(this);
    }
    else if (CInput::Instance().IsKeyDown(VK_ESCAPE))
    {
        // ESC toggle is handled by CUIMng::Update()
        // No action needed here — CUIMng already hid this window
    }
}

void CSysMenuWin::RenderControls()
{
    m_winBack.Render();
    CWin::RenderButtons();
}

#if defined(MU_ENABLE_VIRTUAL_GAMEPAD_TESTS)
CSysMenuWin::SpriteSnapshot CSysMenuWin::DebugBackSnapshot() const
{
    return SpriteSnapshot{
        m_winBack.GetXPos(), m_winBack.GetYPos(),
        m_winBack.GetWidth(), m_winBack.GetHeight(),
        m_winBack.DebugBgScaleX(), m_winBack.DebugBgScaleY(), 0.f
    };
}

CSysMenuWin::SpriteSnapshot CSysMenuWin::DebugButtonSnapshot(int index) const
{
    if (index < 0 || index >= SMW_BTN_MAX)
        return SpriteSnapshot{ 0, 0, 0, 0, 0.f, 0.f, 0.f };

    const CButton& button = m_aBtn[index];
    return SpriteSnapshot{
        button.GetXPos(), button.GetYPos(),
        button.GetWidth(), button.GetHeight(),
        button.GetScaleX(), button.GetScaleY(), button.GetScrHeight()
    };
}
#endif