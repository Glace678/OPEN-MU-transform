//=============================================================================
//	NewUITextBox.cpp
//=============================================================================
#include "stdafx.h"
#include "UI/NewUI/Widgets/NewUITextBox.h"
#include "UI/Legacy/UIControls.h"
#include "Core/Utilities/UsefulDef.h"
#include "Core/Text/TextLineWrap.h"
#include <algorithm>
#include <cmath>


using namespace SEASON3B;

const int iLINE_INTERVAL = 2;

CNewUITextBox::CNewUITextBox()
{
    m_iWidth = 0;
    m_iHeight = 0;
    memset(&m_ptPos, 0, sizeof(POINT));

    m_iTextHeight = 0;
    m_iTextLineHeight = 1;
    m_iLimitLine = 0;

    m_iMaxLine = 0;
    m_iCurLine = 0;
}

CNewUITextBox::~CNewUITextBox()
{
    Release();
}

bool CNewUITextBox::Create(int iX, int iY, int iWidth, int iHeight)
{
    SetPos(iX, iY, iWidth, iHeight);
    Show(true);

    return true;
}

void CNewUITextBox::SetPos(int iX, int iY, int iWidth, int iHeight)
{
    m_ptPos.x = iX;
    m_ptPos.y = iY;
    m_iWidth = iWidth;
    m_iHeight = iHeight;

    m_iMaxLine = 0;
    m_iCurLine = 0;
    UpdateTextLayout();
}

void CNewUITextBox::UpdateTextLayout()
{
    g_pRenderText->SetFont(g_hFont);
    SIZE size{};
    GetTextExtentPoint32(g_pRenderText->GetFontDC(), L"Ag", 2, &size);
    m_iTextHeight = static_cast<int>(std::ceil(size.cy / g_fScreenRate_y));
    m_iTextLineHeight = std::max(1, m_iTextHeight + iLINE_INTERVAL);
    m_iLimitLine = std::max(0, (m_iHeight + iLINE_INTERVAL) / m_iTextLineHeight);
    if (!m_layoutDirty && m_layoutFont == g_hFont && m_layoutScaleX == g_fScreenRate_x && m_layoutWidth == m_iWidth)
        return;

    m_vecText.clear();
    const auto measure = [](const wchar_t* text, size_t length)
    {
        SIZE measured{};
        GetTextExtentPoint32(g_pRenderText->GetFontDC(), text, static_cast<int>(length), &measured);
        return static_cast<int>(measured.cx);
    };
    for (auto text : m_sourceText)
    {
        std::replace(text.begin(), text.end(), L'#', L'\n');
        const auto lines = WrapTextToWidth(text, static_cast<int>(m_iWidth * g_fScreenRate_x), measure);
        m_vecText.insert(m_vecText.end(), lines.begin(), lines.end());
    }
    m_iCurLine = std::clamp(m_iCurLine, 0, GetMoveableLine());
    m_layoutFont = g_hFont;
    m_layoutScaleX = g_fScreenRate_x;
    m_layoutWidth = m_iWidth;
    m_layoutDirty = false;
}

void CNewUITextBox::Release()
{
}

float CNewUITextBox::GetLayerDepth()
{
    return 4.4f;
}

bool CNewUITextBox::UpdateMouseEvent()
{
    return true;
}

bool CNewUITextBox::UpdateKeyEvent()
{
    return true;
}

bool CNewUITextBox::Update()
{
    return true;
}

bool CNewUITextBox::Render()
{
    UpdateTextLayout();
    glColor4f(1.0f, 1.0f, 1.0f, 1.0f);

    for (int iIndex = 0; iIndex < m_iLimitLine; iIndex++)
    {
        int iLineIndex = m_iCurLine + iIndex;

        if (GetLineText(iLineIndex).empty() == false)
        {
            g_pRenderText->SetFont(g_hFont);
            //g_pRenderText->SetBgColor( 0, 0, 0, 0 );
            g_pRenderText->SetTextColor(255, 255, 255, 255);
            g_pRenderText->RenderText(m_ptPos.x, m_ptPos.y + iIndex * m_iTextLineHeight, GetLineText(iLineIndex).c_str(),
                m_iWidth, 0, RT3_SORT_LEFT);
        }
    }

    return true;
}

void CNewUITextBox::AddText(wchar_t* strText)
{
    AddText(static_cast<const wchar_t*>(strText));
}

void CNewUITextBox::AddText(const wchar_t* strText)
{
    if (strText == nullptr)
        return;
    m_sourceText.emplace_back(strText);
    m_layoutDirty = true;
    UpdateTextLayout();
}

std::wstring CNewUITextBox::GetFullText()
{
   std::wstring strTemp;

    auto vi = m_vecText.begin();
    for (; vi != m_vecText.end(); vi++)
    {
        strTemp += (*vi);
    }

    return strTemp;
}

std::wstring CNewUITextBox::GetLineText(int iLineIndex)
{
    if (0 > iLineIndex || (int)m_vecText.size() <= iLineIndex)
        return L"";

    return m_vecText[iLineIndex];
}

int CNewUITextBox::GetMoveableLine()
{
    int iMoveableLine = m_vecText.size() - m_iLimitLine;
    if (iMoveableLine <= 0)
        iMoveableLine = 0;

    return iMoveableLine;
}
