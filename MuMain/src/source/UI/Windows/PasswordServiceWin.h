//*****************************************************************************
// File: PasswordServiceWin.h
// Account password self-service dialog, shared by two flows:
//   - change password: name + current password + new password twice
//   - forgot password: name + new password twice (loopback reset only)
//*****************************************************************************
#pragma once

#include "UI/Widgets/Win.h"
#include "UI/Widgets/Button.h"

class CUITextInputBox;

class CPasswordServiceWin : public CWin
{
protected:
    enum FieldIndex
    {
        FIELD_NAME = 0,
        FIELD_OLD = 1,     // change-password mode only
        FIELD_NEW = 2,
        FIELD_CONFIRM = 3,
        FIELD_COUNT = 4,
    };

    enum ServiceButton
    {
        SB_OK = 0,
        SB_CANCEL = 1,
    };

    enum ServiceMode
    {
        ModeChange = 0,
        ModeReset = 1,
    };

    enum Opener
    {
        FromServerSelect = 0,
        FromLogin = 1,
    };

    CSprite             m_asprInputBox[FIELD_COUNT];
    CButton             m_aBtn[2];
    CUITextInputBox*    m_pBox[FIELD_COUNT] = {};

    int                 m_mode = ModeChange;
    int                 m_opener = FromLogin;
    bool                m_succeeded = false;
    wchar_t             m_status[160] = {};
    BYTE                m_statusColorRgb[3] = { 255, 255, 255 };

    // Remembered art-space origin so children can be repositioned when the
    // dialog reopens in a different mode.
    float               m_fArtX = 0.0f;
    float               m_fArtY = 0.0f;

    // Credentials of the last successful submit in this session, handed to the
    // login form when the dialog was opened from it.
    wchar_t             m_lastName[11] = {};   // name limit 10 + NUL
    wchar_t             m_lastPass[21] = {};   // password limit 20 + NUL

public:
    CPasswordServiceWin();
    virtual ~CPasswordServiceWin();

    void Create();
    void SetPosition(int nXCoord, int nYCoord);
    void SetPositionArt(float fXCoord, float fYCoord);
    void Show(bool bShow);
    bool CursorInWin(int nArea);

    // Shows the dialog in the requested mode and remembers which window to
    // return focus to on close.
    void Open(int mode, int opener, const wchar_t* prefillName = nullptr);

protected:
    void PreRelease();
    void UpdateWhileActive(double dDeltaTick);
    void UpdateWhileShow(double dDeltaTick);
    void RenderControls();

private:
    void Close();
    void Submit();
    void LayoutChildren();
    void SetStatus(const wchar_t* text, BYTE red, BYTE green, BYTE blue);
};
