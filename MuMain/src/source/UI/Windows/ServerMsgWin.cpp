//*****************************************************************************
// File: ServerMsgWin.cpp
//*****************************************************************************

#include "stdafx.h"
#include "ServerMsgWin.h"
#include "UI/Legacy/UIControls.h"

CServerMsgWin::CServerMsgWin() : m_nMsgLine(0)
{
    ::memset(m_aszMsg, 0, sizeof(m_aszMsg));
}

CServerMsgWin::~CServerMsgWin() {}

void CServerMsgWin::Create()
{
    SImgInfo aiiDescBg[WE_BG_MAX] = {{BITMAP_LOG_IN + 11, 0, 0, 4, 4},
                                     {BITMAP_LOG_IN + 12, 0, 0, 512, 6},
                                     {BITMAP_LOG_IN + 12, 0, 6, 512, 6},
                                     {BITMAP_LOG_IN + 13, 0, 0, 3, 4},
                                     {BITMAP_LOG_IN + 13, 3, 0, 3, 4}};
    CWinEx::Create(aiiDescBg, 1, SMW_MSG_LINE_MAX * 5);

    ::memset(m_aszMsg, 0, sizeof(m_aszMsg));
    m_nMsgLine = 0;
}

bool CServerMsgWin::CursorInWin(int nArea)
{
    if (!CWin::m_bShow)
        return false;

    switch (nArea)
    {
        case WA_ALL:
            return false;
    }

    return CWinEx::CursorInWin(nArea);
}

void CServerMsgWin::AddMsg(wchar_t* pszMsg)
{
    if (pszMsg == nullptr)
        return;

    if (++m_nMsgLine > SMW_MSG_LINE_MAX)
    {
        m_nMsgLine = SMW_MSG_LINE_MAX;
        for (int i = 0; i < SMW_MSG_LINE_MAX - 1; ++i)
            wcsncpy_s(m_aszMsg[i], SMW_MSG_ROW_MAX, m_aszMsg[i + 1], _TRUNCATE);
    }
    else
        CWinEx::SetLine(m_nMsgLine * 5);

    wcsncpy_s(m_aszMsg[m_nMsgLine - 1], SMW_MSG_ROW_MAX, pszMsg, _TRUNCATE);

    CWinEx::Show(true);
}

void CServerMsgWin::RenderControls()
{
    g_pRenderText->SetFont(g_hFixFont);
    g_pRenderText->SetTextColor(CLRDW_WHITE);
    g_pRenderText->SetBgColor(0);

    const float rateX = CSprite::ResolutionScaleX();
    const float rateY = CSprite::ResolutionScaleY();
    int         i;
    for (i = 0; i < m_nMsgLine; ++i)
    {
        g_pRenderText->RenderText(CWin::GetXPos() / rateX + 11, CWin::GetYPos() / rateY + 12 + i * 20, m_aszMsg[i]);
    }
}