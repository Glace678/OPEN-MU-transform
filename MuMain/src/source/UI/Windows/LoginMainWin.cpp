//*****************************************************************************
// File: LoginMainWin.cpp
//*****************************************************************************

#include "stdafx.h"
#include "UI/Windows/LoginMainWin.h"

#include "Core/Input/Input.h"
#include "UI/Legacy/UIMng.h"
#include "Network/Server/WSclient.h"

//=============================================================================
// Global Variables
//=============================================================================


//=============================================================================
// Constructor / Destructor
//=============================================================================

CLoginMainWin::CLoginMainWin()
{
}

CLoginMainWin::~CLoginMainWin()
{
}

//=============================================================================
// Public Methods
//=============================================================================

void CLoginMainWin::Create()
{
    for (int i = 0; i <= LMW_BTN_CREDIT; ++i)
        m_aBtn[i].Create(54, 30, BITMAP_LOG_IN + 4 + i, 3, 2, 1);

    // Width spans the canvas with 30-art margins; CWin hit size is device space.
    constexpr int kArtWidth = REFERENCE_WIDTH - 30 * 2;
    constexpr int kArtHeight = 30;
    CWin::Create(kArtWidth, kArtHeight, -2);
    m_Size.cx = int(kArtWidth * CSprite::ResolutionScaleX());
    m_Size.cy = int(kArtHeight * CSprite::ResolutionScaleY());

    for (int i = 0; i < LMW_BTN_MAX; ++i)
        CWin::RegisterButton(&m_aBtn[i]);

    m_sprDeco.Create(189, 103, BITMAP_LOG_IN + 6, 0, nullptr, 105, 59);
}

void CLoginMainWin::PreRelease()
{
    m_sprDeco.Release();
}

void CLoginMainWin::SetPosition(int nXCoord, int nYCoord)
{
    SetPositionArt((float)nXCoord / CSprite::ResolutionScaleX(),
        (float)nYCoord / CSprite::ResolutionScaleY());
}

void CLoginMainWin::SetPositionArt(float fArtX, float fArtY)
{
    CWin::SetPositionArt(fArtX, fArtY);

    const float rateX = CSprite::ResolutionScaleX();
    const float winArtWidth = CWin::GetWidth() / rateX;
    const float creditArtWidth = m_aBtn[LMW_BTN_CREDIT].GetWidth() / rateX;

    m_aBtn[LMW_BTN_MENU].SetPositionArt(fArtX, fArtY);

    m_aBtn[LMW_BTN_CREDIT].SetPositionArt(
        fArtX + winArtWidth - creditArtWidth,
        fArtY
    );

    m_sprDeco.SetPositionArt(
        m_aBtn[LMW_BTN_CREDIT].GetXPos() / rateX,
        m_aBtn[LMW_BTN_CREDIT].GetYPos() / CSprite::ResolutionScaleY()
    );
}

void CLoginMainWin::Show(bool bShow)
{
    CWin::Show(bShow);

    for (int i = 0; i < LMW_BTN_MAX; ++i)
        m_aBtn[i].Show(bShow);

    m_sprDeco.Show(bShow);
}

bool CLoginMainWin::CursorInWin(int nArea)
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

void CLoginMainWin::UpdateWhileActive(double dDeltaTick)
{
    CUIMng& rUIMng = CUIMng::Instance();

    if (m_aBtn[LMW_BTN_MENU].IsClick())
    {
        rUIMng.ShowWin(&rUIMng.m_SysMenuWin);
        rUIMng.SetSysMenuWinShow(true);
    }
    else if (m_aBtn[LMW_BTN_CREDIT].IsClick())
    {
        SocketClient->ToConnectServer()->SendServerListRequest();

        rUIMng.ShowWin(&rUIMng.m_CreditWin);

        ::StopMp3(MUSIC_MAIN_THEME);
        ::PlayMp3(MUSIC_MUTHEME);
    }
}

void CLoginMainWin::RenderControls()
{
    m_sprDeco.Render();
    CWin::RenderButtons();
}
