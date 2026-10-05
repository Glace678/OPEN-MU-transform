//*****************************************************************************
// File: WinEx.cpp
//*****************************************************************************

#include "stdafx.h"
#include "UI/Widgets/WinEx.h"

#include "Core/Input/Input.h"
#include "UI/Widgets/Button.h"
#include "Core/Utilities/UsefulDef.h"

#define	WE_CENTER_SPR_POS		3

CWinEx::CWinEx()
{
}

CWinEx::~CWinEx()
{
    Release();
}

void CWinEx::Create(SImgInfo* aImgInfo, int nBgSideMin, int nBgSideMax)
{
    Release();

    CWin::m_psprBg = new CSprite[WE_BG_MAX];

    CWin::m_psprBg[WE_BG_CENTER].Create(aImgInfo, 0, 0, true);
    CWin::m_psprBg[WE_BG_TOP].Create(aImgInfo + 1);
    CWin::m_psprBg[WE_BG_BOTTOM].Create(aImgInfo + 2);
    CWin::m_psprBg[WE_BG_LEFT].Create(aImgInfo + 3, 0, 0, true);
    CWin::m_psprBg[WE_BG_RIGHT].Create(aImgInfo + 4, 0, 0, true);

    CWin::m_psprBg[WE_BG_CENTER].SetSizeArt(
        CWin::m_psprBg[WE_BG_TOP].GetWidth() / CSprite::ResolutionScaleX()
        - WE_CENTER_SPR_POS * 2, 0, X);

    CWin::m_ptPos.x = CWin::m_ptPos.y = 0;
    CWin::m_ptHeld = CWin::m_ptTemp = CWin::m_ptPos;
    CWin::m_bDocking = CWin::m_bActive = CWin::m_bShow = false;
    CWin::m_nState = WS_NORMAL;
    CWin::m_Size.cx = CWin::m_psprBg[WE_BG_TOP].GetWidth();
    CWin::m_Size.cy = CWin::m_psprBg[WE_BG_TOP].GetHeight()
        + CWin::m_psprBg[WE_BG_BOTTOM].GetHeight()
        + CWin::m_psprBg[WE_BG_LEFT].GetHeight();

    m_nBgSideNow = m_nBgSideMin = nBgSideMin;
    m_nBgSideMax = nBgSideMax;
}

void CWinEx::Release()
{
    PreRelease();
    CButton* pBtn;
    while (CWin::m_BtnList.GetCount())
    {
        pBtn = (CButton*)CWin::m_BtnList.RemoveHead();
        pBtn->Release();
    }

    SafeDeleteArray(CWin::m_psprBg);
}

void CWinEx::SetPosition(int nXCoord, int nYCoord)
{
    SetPositionArt((float)nXCoord / CSprite::ResolutionScaleX(),
        (float)nYCoord / CSprite::ResolutionScaleY());
}

void CWinEx::SetPositionArt(float fXCoord, float fYCoord)
{
    CSprite* psprBg = CWin::m_psprBg;

    psprBg[WE_BG_TOP].SetPositionArt(fXCoord, fYCoord);

    psprBg[WE_BG_CENTER].SetPositionArt(fXCoord + WE_CENTER_SPR_POS,
        fYCoord + WE_CENTER_SPR_POS);

    const float topArtH = psprBg[WE_BG_TOP].GetHeight()
        / CSprite::ResolutionScaleY();
    psprBg[WE_BG_LEFT].SetPositionArt(fXCoord, fYCoord + topArtH);

    const float topArtW = psprBg[WE_BG_TOP].GetWidth()
        / CSprite::ResolutionScaleX();
    const float rightArtW = psprBg[WE_BG_RIGHT].GetWidth()
        / CSprite::ResolutionScaleX();
    const float leftArtY = psprBg[WE_BG_LEFT].GetYPos()
        / CSprite::ResolutionScaleY();
    psprBg[WE_BG_RIGHT].SetPositionArt(fXCoord + topArtW - rightArtW, leftArtY);

    const float leftArtH = psprBg[WE_BG_LEFT].GetHeight()
        / CSprite::ResolutionScaleY();
    psprBg[WE_BG_BOTTOM].SetPositionArt(fXCoord, leftArtY + leftArtH);

    // m_ptPos stays in device space for win hit testing / drag math.
    m_ptPos.x = int(fXCoord * CSprite::ResolutionScaleX());
    m_ptPos.y = int(fYCoord * CSprite::ResolutionScaleY());
}

int CWinEx::SetLine(int nLine)
{
    nLine = LIMIT(nLine, m_nBgSideMin, m_nBgSideMax);

    if (m_nBgSideNow == nLine)
        return m_nBgSideNow;

    int nOldLine = m_nBgSideNow;
    m_nBgSideNow = nLine;

    int nBgSideHeight
        = CWin::m_psprBg[WE_BG_LEFT].GetTexHeight() * m_nBgSideNow;

    CWin::m_psprBg[WE_BG_LEFT].SetSizeArt(0, (float)nBgSideHeight, Y);
    CWin::m_psprBg[WE_BG_RIGHT].SetSizeArt(0, (float)nBgSideHeight, Y);

    const float leftArtY = CWin::m_psprBg[WE_BG_LEFT].GetYPos()
        / CSprite::ResolutionScaleY();
    const float leftArtH = CWin::m_psprBg[WE_BG_LEFT].GetHeight()
        / CSprite::ResolutionScaleY();
    CWin::m_psprBg[WE_BG_BOTTOM].SetPositionArt(0, leftArtY + leftArtH, Y);

    CWin::m_Size.cy = CWin::m_psprBg[WE_BG_TOP].GetHeight()
        + CWin::m_psprBg[WE_BG_BOTTOM].GetHeight()
        + CWin::m_psprBg[WE_BG_LEFT].GetHeight();

    CWin::m_psprBg[WE_BG_CENTER].SetSizeArt(0,
        (float)CWin::m_Size.cy / CSprite::ResolutionScaleY()
        - WE_CENTER_SPR_POS * 2, Y);

    return nOldLine;
}

void CWinEx::SetSize(int nHeight)
{
    const int sideHeight = GetSideTextureHeight();
    if (sideHeight <= 0)
        return;
    int nLine = (nHeight - CWin::m_psprBg[WE_BG_TOP].GetHeight()
        - CWin::m_psprBg[WE_BG_BOTTOM].GetHeight())
        / sideHeight;

    SetLine(nLine);
}

bool CWinEx::CursorInWin(int nArea)
{
    if (!CWin::m_bShow)
        return false;

    CInput& rInput = CInput::Instance();
    RECT rc = { 0, 0, 0, 0 };

    switch (nArea)
    {
    case WA_EXTEND_DN:
        ::SetRect(&rc, CWin::m_ptPos.x, CWin::m_ptPos.y + CWin::m_Size.cy - 5,
            CWin::m_ptPos.x + CWin::m_Size.cx,
            CWin::m_ptPos.y + CWin::m_Size.cy);
        if (::PtInRect(&rc, rInput.GetCursorPos()))
            return true;
        break;

    case WA_EXTEND_UP:
        ::SetRect(&rc, CWin::m_ptPos.x, CWin::m_ptPos.y,
            CWin::m_ptPos.x + CWin::m_Size.cx, CWin::m_ptPos.y + 4);
        if (::PtInRect(&rc, rInput.GetCursorPos()))
            return true;
        break;
    }

    return CWin::CursorInWin(nArea);
}

void CWinEx::Show(bool bShow)
{
    for (int i = 0; i < WE_BG_MAX; ++i)
        CWin::m_psprBg[i].Show(bShow);

    CWin::m_bShow = bShow;
    if (!CWin::m_bShow)
        CWin::m_bActive = false;
}

void CWinEx::CheckAdditionalState()
{
    const int sideHeight = GetSideTextureHeight();
    if (sideHeight <= 0)
        return;
    CInput& rInput = CInput::Instance();

    if (rInput.IsLBtnDn())
    {
        if (CursorInWin(WA_EXTEND_UP))
        {
            m_nBasisY = CWin::m_ptPos.y
                + sideHeight * m_nBgSideNow;
            CWin::m_nState = WS_EXTEND_UP;
        }

        if (CursorInWin(WA_EXTEND_DN))
        {
            m_nBasisY = CWin::m_ptPos.y + CWin::m_psprBg[WE_BG_TOP].GetHeight()
                + CWin::m_psprBg[WE_BG_BOTTOM].GetHeight();
            CWin::m_nState = WS_EXTEND_DN;
        }
    }

    int nBgSideHeight;
    switch (CWin::m_nState)
    {
    case WS_EXTEND_UP:
        nBgSideHeight = m_nBasisY - rInput.GetCursorY();
        if (nBgSideHeight
            < sideHeight * m_nBgSideMin)
            SetLine(m_nBgSideMin);
        else
            SetLine(nBgSideHeight / sideHeight
                + 1);

        SetPosition(CWin::m_ptPos.x, m_nBasisY
            - sideHeight * m_nBgSideNow);

        break;

    case WS_EXTEND_DN:
        nBgSideHeight = rInput.GetCursorY() - m_nBasisY;
        if (nBgSideHeight
            < sideHeight * m_nBgSideMin)
            SetLine(m_nBgSideMin);
        else
            SetLine(nBgSideHeight / sideHeight
                + 1);

        break;
    }
}

void CWinEx::Render()
{
    if (CWin::m_bShow)
    {
        for (int i = 0; i < WE_BG_MAX; ++i)
            CWin::m_psprBg[i].Render();

        RenderControls();
    }
}
int CWinEx::GetSideTextureHeight() const
{
    return CWin::m_psprBg != nullptr ? CWin::m_psprBg[WE_BG_LEFT].GetTexHeight() : 0;
}
