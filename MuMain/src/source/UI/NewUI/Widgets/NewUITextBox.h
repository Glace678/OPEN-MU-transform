//=============================================================================
//	NewUITextBox.h
//=============================================================================
#pragma once
#include "UI/NewUI/NewUIBase.h"

namespace SEASON3B
{
    class CNewUITextBox : public CNewUIObj
    {
        typedef std::vector<std::wstring>		type_vector_textbase;
        type_vector_textbase	m_vecText;
        type_vector_textbase m_sourceText;
        HFONT m_layoutFont = nullptr;
        float m_layoutScaleX = 0.f;
        int m_layoutWidth = 0;
        bool m_layoutDirty = true;
        void UpdateTextLayout();

    protected:
        int							m_iWidth;
        int							m_iHeight;
        POINT						m_ptPos;

        int							m_iTextHeight;
        int							m_iTextLineHeight;
        int							m_iLimitLine;

        int							m_iMaxLine;
        int							m_iCurLine;

    public:
        CNewUITextBox();
        virtual ~CNewUITextBox();

        bool Create(int iX, int iY, int iWidth, int iHeight);
        void SetPos(int iX, int iY, int iWidth, int iHeight);
        void Release();

        float GetLayerDepth();

        bool UpdateMouseEvent();
        bool UpdateKeyEvent();

        bool Update();
        bool Render();

        void ClearText() { m_vecText.clear(); m_sourceText.clear(); m_iCurLine = 0; }
        void AddText(wchar_t* strText);
        void AddText(const wchar_t* strText);

       std::wstring GetFullText();
       std::wstring GetLineText(int iLineIndex);

        int GetMoveableLine();
        int GetLimitLine() { return m_iLimitLine; }
        int GetMaxLine() { return m_vecText.size(); }
        int GetCurLine() { return m_iCurLine; }
        void SetCurLine(int iLine) { m_iCurLine = iLine; }
    };
}
