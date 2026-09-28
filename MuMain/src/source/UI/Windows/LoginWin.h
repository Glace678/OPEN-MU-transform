//*****************************************************************************
// File: LoginWin.h
//*****************************************************************************
#pragma once

#include "UI/Widgets/Win.h"

#include "UI/Widgets/Button.h"
#include "Network/Login/AccountPortal.h"

class CUITextInputBox;

class CLoginWin : public CWin
{
protected:
    CSprite		m_asprInputBox[2];
    CButton		m_aBtn[2];
    CButton     m_aBtnRememberMe;
    CButton     m_aBtnSavePassword;
    CButton     m_aBtnRegister;
    CButton     m_aBtnChangePassword;
    CButton     m_aBtnForgotPassword;
    CUITextInputBox* m_pUsernameInputBox, * m_pPasswordInputBox;

    // Snapshot of the field contents, used to detect that the player edited the
    // username or password so the stored credentials can be dropped.
    wchar_t     m_prevUsername[MAX_USERNAME_SIZE + 1] = {};
    wchar_t     m_prevPassword[MAX_PASSWORD_SIZE + 1] = {};

public:
    enum class EntryFocus { None, Username, Password };
    struct ResizeState
    {
        wchar_t username[MAX_USERNAME_SIZE + 1] = {};
        wchar_t password[MAX_PASSWORD_SIZE + 1] = {};
        wchar_t previousUsername[MAX_USERNAME_SIZE + 1] = {};
        wchar_t previousPassword[MAX_PASSWORD_SIZE + 1] = {};
        bool rememberUsername = false;
        bool savePassword = false;
        bool initialFocusPending = false;
        EntryFocus focus = EntryFocus::None;
    };

    CLoginWin();
    virtual ~CLoginWin();
    void Create();
    void SetPosition(int nXCoord, int nYCoord);
    void SetPositionArt(float fXCoord, float fYCoord);
    void Show(bool bShow);
    bool CursorInWin(int nArea);

    void ConnectConnectionServer();
    bool RequestAutomaticLogin();
    ResizeState CaptureResizeState();
    void RestoreResizeState(const ResizeState& state);
    void OpenAccountService(Network::Login::AccountPortalView view);

    CUITextInputBox* GetUsernameInputBox() const { return m_pUsernameInputBox; }
    CUITextInputBox* GetPasswordInputBox() const { return m_pPasswordInputBox; }

private:
    int FirstLoad = 0;
    void CreateAccountPortalButtons();
    bool UpdateAccountPortalButtons();

protected:
    void PreRelease();
    void UpdateWhileActive(double dDeltaTick);
    void UpdateWhileShow(double dDeltaTick);
    void RenderControls();
    void RequestLogin();
    void SendLoginRequest();
    void CancelLogin();

    // "Remember me" credential handling, split out of the update loop.
    void UpdateRememberCheckboxes();
    void ApplyRememberPasswordChoice();
    void RevokeSavedCredentialsIfEdited();
};
