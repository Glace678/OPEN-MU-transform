//*****************************************************************************
// File: RegisterWin.h
// Self-service account registration dialog (name + password twice -> POST).
//*****************************************************************************
#pragma once

#include "UI/Widgets/Win.h"
#include "UI/Widgets/Button.h"

class CUITextInputBox;

class CRegisterWin : public CWin
{
protected:
    enum FieldIndex
    {
        FIELD_NAME = 0,
        FIELD_PASSWORD = 1,
        FIELD_CONFIRM = 2,
        FIELD_COUNT = 3,
    };

    enum RegisterButton
    {
        RB_OK = 0,
        RB_CANCEL = 1,
    };

    enum Opener
    {
        FromServerSelect = 0,
        FromLogin = 1,
    };

    CSprite             m_asprInputBox[FIELD_COUNT];
    CButton             m_aBtn[2];
    CUITextInputBox*    m_pBox[FIELD_COUNT] = {};

    int                 m_opener = FromServerSelect;
    bool                m_registered = false;
    wchar_t             m_status[160] = {};
    BYTE                m_statusColorRgb[3] = { 255, 255, 255 };

    // Credentials of the last successful registration in this session, handed
    // to the login form when the dialog was opened from it.
    wchar_t             m_lastName[11] = {};   // name limit 10 + NUL
    wchar_t             m_lastPass[21] = {};   // password limit 20 + NUL

public:
    CRegisterWin();
    virtual ~CRegisterWin();

    void Create();
    void SetPosition(int nXCoord, int nYCoord);
    void SetPositionArt(float fXCoord, float fYCoord);
    void Show(bool bShow);
    bool CursorInWin(int nArea);

    // Shows the dialog and remembers which window to return focus to on close.
    void Open(int opener);

protected:
    void PreRelease();
    void UpdateWhileActive(double dDeltaTick);
    void UpdateWhileShow(double dDeltaTick);
    void RenderControls();

private:
    void Close();
    void SubmitRegistration();
    void SetStatus(const wchar_t* text, BYTE red, BYTE green, BYTE blue);
};
