//*****************************************************************************
// File: LoginWin.cpp
//*****************************************************************************

#include "stdafx.h"
#include "UI/Windows/LoginWin.h"
#include "Core/Input/Input.h"
#include "UI/Legacy/UIMng.h"
#include "Render/Models/ZzzBMD.h"
#include "Engine/Object/ZzzInfomation.h"
#include "Engine/Object/ZzzObject.h"
#include "Engine/Object/ZzzCharacter.h"
#include "Engine/Object/ZzzInterface.h"
#include "Network/Reconnect/ReconnectManager.h"
#include "Network/Login/LocalAutoLogin.h"
#include "Network/Login/LocalLoginCredentials.h"
#include "UI/Legacy/UIControls.h"
#include "Scenes/SceneCore.h"
#include "I18N/All.h"

#include "Audio/DSPlaySound.h"
#include "UI/NewUI/NewUISystem.h"
#include "UI/NewUI/Dialogs/NewUIMessageBox.h"
#include "UI/Windows/RememberPasswordPrompt.h"


#include "Network/Server/ServerListManager.h"
#ifdef _WIN32
#include <dpapi.h>
#endif

#include "Data/GameConfig/GameConfig.h"
#include "Data/GameConfig/GameConfigConstants.h"
#include <SDL3/SDL.h>
#include <cstdlib>
#include <cstring>
#include <string_view>

#define	LIW_ACCOUNT		0
#define	LIW_PASSWORD	1

#define LIW_OK			0
#define LIW_CANCEL		1



extern int g_iChatInputType;
extern int  LogIn;
extern wchar_t LogInID[MAX_USERNAME_SIZE + 1];
extern BYTE Version[SIZE_PROTOCOLVERSION];
extern BYTE Serial[SIZE_PROTOCOLSERIAL + 1];

#if defined(__ANDROID__) || defined(__OHOS__)
namespace
{
    void LogMobileLoginInput(CButton& ok)
    {
        const char* diagnostics = std::getenv("MU_INPUT_DIAGNOSTICS");
        auto& input = CInput::Instance();
        if (diagnostics == nullptr || std::strcmp(diagnostics, "1") != 0
            || (!input.IsLBtnDn() && !input.IsLBtnUp()))
            return;
        SDL_Log("[Login] protocol=%d blocked=%d cursor=%ld,%ld ok=%d,%d %dx%d inside=%d click=%d",
            CurrentProtocolState, !g_MessageBox->IsEmpty(),
            input.GetCursorX(), input.GetCursorY(), ok.GetXPos(), ok.GetYPos(),
            ok.GetWidth(), ok.GetHeight(), ok.CursorInObject(), ok.IsClick());
    }
}
#endif

CLoginWin::CLoginWin()
{
    m_pUsernameInputBox = NULL;
    m_pPasswordInputBox = NULL;
}

CLoginWin::~CLoginWin()
{
    SAFE_DELETE(m_pUsernameInputBox);
    SAFE_DELETE(m_pPasswordInputBox);
}

void CLoginWin::Create()
{
    m_RememberMe = GameConfig::GetInstance().GetRememberMe();
    if (m_RememberMe)
    {
        // Use the helper we built to fill m_Username[11] and m_Password[21]
        GameConfig::GetInstance().DecryptCredentials(m_Username, m_Password, _countof(m_Username), _countof(m_Password));
    }
    else
    {
        // Ensure they are empty if RememberMe is off
        m_Username[0] = L'\0';
        m_Password[0] = L'\0';
    }

    // The login background is fixed artwork and does not scale with the window
    // size, so keep the original height. The credential-consent controls fit on
    // the panel and the trust warning is drawn just below it (issue #462).
    CWin::Create(329, 245, BITMAP_LOG_IN + 7);

    m_asprInputBox[LIW_ACCOUNT].Create(156, 23, BITMAP_LOG_IN + 8);
    m_asprInputBox[LIW_PASSWORD].Create(156, 23, BITMAP_LOG_IN + 8);

    for (int i = 0; i < 2; ++i)
    {
        m_aBtn[i].Create(54, 30, BITMAP_BUTTON + i, 3, 2, 1);
        CWin::RegisterButton(&m_aBtn[i]);
    }

    m_aBtnRememberMe.Create(16, 16, BITMAP_CHECK_BTN, 2, 0, 0, -1, 1, 1, 1);
    CWin::RegisterButton(&m_aBtnRememberMe);

    m_aBtnSavePassword.Create(16, 16, BITMAP_CHECK_BTN, 2, 0, 0, -1, 1, 1, 1);
    CWin::RegisterButton(&m_aBtnSavePassword);

    CreateAccountPortalButtons();

    SAFE_DELETE(m_pUsernameInputBox);

    m_pUsernameInputBox = new CUITextInputBox;
    m_pUsernameInputBox->Init(g_hWnd, 140, 14, MAX_USERNAME_SIZE);
    m_pUsernameInputBox->SetBackColor(0, 0, 0, 25);
    m_pUsernameInputBox->SetTextColor(255, 255, 230, 210);
    m_pUsernameInputBox->SetFont(g_hFixFont);
    m_pUsernameInputBox->SetState(UISTATE_NORMAL);
    if (m_RememberMe) {
        m_pUsernameInputBox->SetText(m_Username);
        m_aBtnRememberMe.SetCheck(true);
    }

    SAFE_DELETE(m_pPasswordInputBox);

    m_pPasswordInputBox = new CUITextInputBox;
    m_pPasswordInputBox->Init(g_hWnd, 140, 14, MAX_PASSWORD_SIZE, TRUE);
    m_pPasswordInputBox->SetBackColor(0, 0, 0, 25);
    m_pPasswordInputBox->SetTextColor(255, 255, 230, 210);
    m_pPasswordInputBox->SetFont(g_hFixFont);
    m_pPasswordInputBox->SetState(UISTATE_NORMAL);

    m_pUsernameInputBox->SetTabTarget(m_pPasswordInputBox);
    m_pPasswordInputBox->SetTabTarget(m_pUsernameInputBox);

    if (m_RememberMe) {
        m_pPasswordInputBox->SetText(m_Password);
        m_aBtnRememberMe.SetCheck(true);
    }

    // The password is only pre-filled and re-saved when the player previously
    // opted in on a trusted machine.
    m_aBtnSavePassword.SetCheck(m_RememberMe && GameConfig::GetInstance().GetSavePassword());

    // Seed the edit-detection snapshot with what we just loaded so filling the
    // boxes here is not mistaken for the player editing them.
    m_pUsernameInputBox->GetText(m_prevUsername, _countof(m_prevUsername));
    m_pPasswordInputBox->GetText(m_prevPassword, _countof(m_prevPassword));

    this->FirstLoad = 1;
}

void CLoginWin::PreRelease()
{
    for (int i = 0; i < 2; ++i)
        m_asprInputBox[i].Release();
}

CLoginWin::ResizeState CLoginWin::CaptureResizeState()
{
    ResizeState state;
    if (m_pUsernameInputBox)
        m_pUsernameInputBox->GetText(state.username, _countof(state.username));
    if (m_pPasswordInputBox)
        m_pPasswordInputBox->GetText(state.password, _countof(state.password));
    wcscpy_s(state.previousUsername, _countof(state.previousUsername), m_prevUsername);
    wcscpy_s(state.previousPassword, _countof(state.previousPassword), m_prevPassword);
    state.rememberUsername = m_aBtnRememberMe.IsCheck();
    state.savePassword = m_aBtnSavePassword.IsCheck();
    state.initialFocusPending = FirstLoad != 0;
    if (m_pUsernameInputBox && m_pUsernameInputBox->HaveFocus())
        state.focus = EntryFocus::Username;
    else if (m_pPasswordInputBox && m_pPasswordInputBox->HaveFocus())
        state.focus = EntryFocus::Password;
    return state;
}

void CLoginWin::RestoreResizeState(const ResizeState& state)
{
    if (!m_pUsernameInputBox || !m_pPasswordInputBox)
        return;

    m_pUsernameInputBox->SetText(state.username);
    m_pPasswordInputBox->SetText(state.password);
    m_aBtnRememberMe.SetCheck(state.rememberUsername);
    m_aBtnSavePassword.SetCheck(state.savePassword);
    m_RememberMe = state.rememberUsername ? 1 : 0;
    // Keep the original edit baseline: a pending real edit must still revoke
    // saved credentials, but rebuilding the fields must not count as an edit.
    wcscpy_s(m_prevUsername, _countof(m_prevUsername), state.previousUsername);
    wcscpy_s(m_prevPassword, _countof(m_prevPassword), state.previousPassword);
    FirstLoad = state.initialFocusPending ? 1 : 0;
    if (!IsShow() || state.focus == EntryFocus::None)
        return;

    FirstLoad = 0;
    CUITextInputBox* field = state.focus == EntryFocus::Username
        ? m_pUsernameInputBox : m_pPasswordInputBox;
    field->GiveFocus(FALSE, false);
}

void CLoginWin::SetPosition(int x, int y)
{
    SetPositionArt((float)x / CSprite::ResolutionScaleX(),
        (float)y / CSprite::ResolutionScaleY());
}

void CLoginWin::SetPositionArt(float fArtX, float fArtY)
{
    CWin::SetPositionArt(fArtX, fArtY);

    // Every offset below is authored art units relative to the window.
    const float boxOffsetX = fArtX + 109;
    m_asprInputBox[LIW_ACCOUNT].SetPositionArt(boxOffsetX, fArtY + 106);
    m_asprInputBox[LIW_PASSWORD].SetPositionArt(boxOffsetX, fArtY + 131);

    if (g_iChatInputType == 1)
    {
        m_pUsernameInputBox->SetPosition(int(fArtX + 115), int(fArtY + 112));
        m_pPasswordInputBox->SetPosition(int(fArtX + 115), int(fArtY + 137));
    }

    // "Remember Username" (row 1) and "Remember Password" (row 2) stack
    // vertically; the OK/Cancel buttons move down to make room.
    m_aBtnRememberMe.SetPositionArt(fArtX + 109, fArtY + 156);
    m_aBtnSavePassword.SetPositionArt(fArtX + 109, fArtY + 176);
    m_aBtn[LIW_OK].SetPositionArt(fArtX + 150, fArtY + 200);
    m_aBtn[LIW_CANCEL].SetPositionArt(fArtX + 211, fArtY + 200);
    m_aBtnRegister.SetPositionArt(fArtX + 15, fArtY + 202);
    m_aBtnChangePassword.SetPositionArt(fArtX + 59, fArtY + 202);
    m_aBtnForgotPassword.SetPositionArt(fArtX + 103, fArtY + 202);
}

void CLoginWin::Show(bool bShow)
{
    CWin::Show(bShow);

    for (int i = 0; i < 2; ++i)
    {
        m_asprInputBox[i].Show(bShow);
        m_aBtn[i].Show(bShow);
    }
    m_aBtnRememberMe.Show(bShow);
    m_aBtnSavePassword.Show(bShow);
    m_aBtnRegister.Show(bShow);
    m_aBtnChangePassword.Show(bShow);
    m_aBtnForgotPassword.Show(bShow);

    // Drive the text fields' state so a hidden login screen releases keyboard
    // focus (portable fields stop SDL text input when hidden, #447).
    const int iState = bShow ? UISTATE_NORMAL : UISTATE_HIDE;
    if (m_pUsernameInputBox) m_pUsernameInputBox->SetState(iState);
    if (m_pPasswordInputBox) m_pPasswordInputBox->SetState(iState);
}

bool CLoginWin::CursorInWin(int nArea)
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

void CLoginWin::UpdateWhileActive(double)
{
#if defined(__ANDROID__) || defined(__OHOS__)
    LogMobileLoginInput(m_aBtn[LIW_OK]);
#endif
	// While the "Remember Password" confirmation dialog is open, let it own the
	// input so Enter/Esc/clicks don't also drive the login screen behind it. The
	// dialog's outcome is applied on the next frame, once it has closed.
	if (!g_MessageBox->IsEmpty())
		return;

	if (m_aBtn[LIW_OK].IsClick() || CInput::Instance().IsKeyDown(VK_RETURN))
	{
		PlayBuffer(SOUND_CLICK01);
		RequestLogin();
		return;
	}

	if (m_aBtn[LIW_CANCEL].IsClick() || CInput::Instance().IsKeyDown(VK_ESCAPE))
	{
		PlayBuffer(SOUND_CLICK01);
		CancelLogin();
		CUIMng::Instance().SetSysMenuWinShow(false);
		return;
	}

    if (UpdateAccountPortalButtons()) return;

    UpdateRememberCheckboxes();
}

void CLoginWin::CreateAccountPortalButtons()
{
    static const DWORD colors[3] = { CLRDW_BR_GRAY, CLRDW_BR_GRAY, CLRDW_WHITE };
    CButton* buttons[] = { &m_aBtnRegister, &m_aBtnChangePassword, &m_aBtnForgotPassword };
    const wchar_t* labels[] = { I18N::Game::AccountRegister,
        I18N::Game::AccountChangePassword, I18N::Game::AccountResetPassword };
    for (std::size_t index = 0; index < std::size(buttons); ++index)
    {
        buttons[index]->Create(42, 26, BITMAP_LOG_IN + 1, 3, 2, 1);
        buttons[index]->SetText(labels[index], const_cast<DWORD*>(colors));
        CWin::RegisterButton(buttons[index]);
    }
}

bool CLoginWin::UpdateAccountPortalButtons()
{
    using Network::Login::AccountPortalView;
    CButton* buttons[] = { &m_aBtnRegister, &m_aBtnChangePassword, &m_aBtnForgotPassword };
    const AccountPortalView views[] = { AccountPortalView::Register,
        AccountPortalView::ChangePassword, AccountPortalView::ResetPassword };
    for (std::size_t index = 0; index < std::size(buttons); ++index)
    {
        if (!buttons[index]->IsClick()) continue;
        PlayBuffer(SOUND_CLICK01);
        OpenAccountService(views[index]);
        return true;
    }
    return false;
}

void CLoginWin::OpenAccountService(Network::Login::AccountPortalView view)
{
    const auto& config = GameConfig::GetInstance();
    std::wstring origin = config.GetAccountPortalUrl();
    if (const char* configured = std::getenv("MU_ACCOUNT_PORTAL_URL"); configured != nullptr)
    {
        origin.clear();
        for (const unsigned char character : std::string_view(configured))
            origin.push_back(static_cast<wchar_t>(character));
    }
    const char* paired = std::getenv("MU_MOBILE_LOCAL_AUTO_LOGIN");
    const bool allowPrivateHttp = paired != nullptr && std::string_view(paired) == "1";
    std::wstring culture = config.GetUILocale();
    if (const char* configured = std::getenv("MU_ACCOUNT_PORTAL_CULTURE"); configured != nullptr)
    {
        culture.clear();
        for (const unsigned char character : std::string_view(configured))
            culture.push_back(static_cast<wchar_t>(character));
    }
    const auto url = Network::Login::BuildAccountPortalUrl(origin, view,
        culture, allowPrivateHttp);
    if (url.empty() || !SDL_OpenURL(url.c_str()))
        MessageBoxW(g_hWnd, I18N::Game::AccountPortalUnavailable, I18N::Game::Account, MB_OK | MB_ICONERROR);
}

void CLoginWin::UpdateRememberCheckboxes()
{
	GameConfig& config = GameConfig::GetInstance();

	if (m_aBtnRememberMe.IsClick())
	{
		m_RememberMe = m_aBtnRememberMe.IsCheck();
		config.SetRememberMe(m_RememberMe != 0);

		// Switching off "remember me" revokes everything: drop the stored
		// credentials from config.ini now so they can't linger if the game is
		// closed before the next login.
		if (!m_RememberMe)
		{
			m_aBtnSavePassword.SetCheck(false);
			config.ClearCredentials();
		}
	}

	if (!m_aBtnSavePassword.IsClick())
		return;

	if (!m_aBtnSavePassword.IsCheck())
	{
		// Player unticked it: drop the stored password from config.ini now.
		config.SetSavePassword(false);
		config.SetEncryptedPassword(L"");
		config.Save();
		return;
	}

	// Enabling requires confirmation. Revert the tick immediately; it is
	// re-applied only if the dialog is accepted, so a cancel (however the dialog
	// closes) always leaves the box unchecked.
	m_aBtnSavePassword.SetCheck(false);

	// Storing the password implies remembering the account.
	if (!m_RememberMe)
	{
		m_RememberMe = 1;
		m_aBtnRememberMe.SetCheck(true);
		config.SetRememberMe(true);
	}
	UI::Login::OpenRememberPasswordPrompt();
}

void CLoginWin::UpdateWhileShow(double dDeltaTick)
{
    m_pUsernameInputBox->DoAction();
    m_pPasswordInputBox->DoAction();

    ApplyRememberPasswordChoice();
    RevokeSavedCredentialsIfEdited();
}

void CLoginWin::ApplyRememberPasswordChoice()
{
    // Applied here rather than in UpdateWhileActive because the modal message box
    // leaves the login window inactive (so UpdateWhileActive stops running),
    // while UpdateWhileShow keeps being called.
    const UI::Login::RememberPasswordChoice choice = UI::Login::RememberPasswordChoiceState();
    if (choice != UI::Login::RememberPasswordChoice::Ok
        && choice != UI::Login::RememberPasswordChoice::Cancel)
        return;

    UI::Login::ClearRememberPasswordChoice();

    const bool bAccepted = (choice == UI::Login::RememberPasswordChoice::Ok);
    GameConfig::GetInstance().SetSavePassword(bAccepted);
    m_aBtnSavePassword.SetCheck(bAccepted);
}

void CLoginWin::RevokeSavedCredentialsIfEdited()
{
    // Editing the account or password drops any stored credentials and revokes
    // the save-password consent, so an out-of-date password never lingers in
    // config.ini for the next person on this machine.
    wchar_t curUser[MAX_USERNAME_SIZE + 1] = {};
    wchar_t curPass[MAX_PASSWORD_SIZE + 1] = {};
    m_pUsernameInputBox->GetText(curUser, _countof(curUser));
    m_pPasswordInputBox->GetText(curPass, _countof(curPass));

    if (wcscmp(curUser, m_prevUsername) == 0 && wcscmp(curPass, m_prevPassword) == 0)
        return;

    GameConfig& config = GameConfig::GetInstance();
    const bool bHadStored = m_aBtnSavePassword.IsCheck()
        || !config.GetEncryptedUsername().empty()
        || !config.GetEncryptedPassword().empty();
    if (bHadStored)
    {
        m_aBtnSavePassword.SetCheck(false);
        config.ClearCredentials();
    }

    wcscpy_s(m_prevUsername, _countof(m_prevUsername), curUser);
    wcscpy_s(m_prevPassword, _countof(m_prevPassword), curPass);
}

void CLoginWin::RenderControls()
{
    if (FirstLoad)
    {
        (wcslen(m_Username) > 0 ? m_pPasswordInputBox : m_pUsernameInputBox)->GiveFocus();
        FirstLoad = 0;
    }

    CWin::RenderButtons();
    m_asprInputBox[LIW_ACCOUNT].Render();
    m_asprInputBox[LIW_PASSWORD].Render();
    m_pUsernameInputBox->Render();
    m_pPasswordInputBox->Render();

    g_pRenderText->SetFont(g_hFixFont);
    g_pRenderText->SetBgColor(0);
    g_pRenderText->SetTextColor(CLRDW_WHITE);

    const float rateX = CSprite::ResolutionScaleX();
    const float rateY = CSprite::ResolutionScaleY();
    const int baseX = int(GetXPos() / rateX);
    const int baseY = int(GetYPos() / rateY);

    g_pRenderText->RenderText(baseX + 30, baseY + 113, I18N::Game::Account);
    g_pRenderText->RenderText(baseX + 30, baseY + 139, I18N::Game::Password);

    wchar_t szServerName[MAX_TEXT_LENGTH] = {};
    const wchar_t* pServerStatus = g_ServerListManager->GetNonPVPInfo() ? I18N::Game::SDServer : I18N::Game::SDNonPvPServer;
    mu_swprintf(szServerName, pServerStatus, g_ServerListManager->GetSelectServerName(), g_ServerListManager->GetSelectServerIndex());
    g_pRenderText->RenderText(baseX + 111, baseY + 80, szServerName);

    g_pRenderText->RenderText(baseX + 130, baseY + 159, I18N::Game::LoginRememberUsername);
    g_pRenderText->RenderText(baseX + 130, baseY + 179, I18N::Game::LoginRememberPassword);

    // Trust warning rendered just below the login panel (the panel artwork is a
    // fixed size, so there is no room for this long line inside it).
    g_pRenderText->SetTextColor(255, 210, 60, 255);
    g_pRenderText->RenderText(baseX + 30, baseY + 252, I18N::Game::LoginTrustWarning);
    g_pRenderText->SetTextColor(CLRDW_WHITE);
}

void CLoginWin::RequestLogin()
{
    if (CurrentProtocolState == REQUEST_JOIN_SERVER)
        return;

    CUIMng::Instance().HideWin(this);

    m_pUsernameInputBox->GetText(m_Username, _countof(m_Username));
    m_pPasswordInputBox->GetText(m_Password, _countof(m_Password));

    // Handle credentials saving. The username is remembered when "remember me"
    // is set; the password is only stored on top of that with explicit consent.
    if (m_aBtnRememberMe.IsCheck())
    {
        GameConfig::GetInstance().SetSavePassword(m_aBtnSavePassword.IsCheck());
        GameConfig::GetInstance().EncryptAndSaveCredentials(m_Username, m_Password);
    }
    else
    {
        // Clear saved credentials if user unchecked "Remember Me"
        GameConfig::GetInstance().ClearCredentials();
    }

    if (wcslen(m_Username) <= 0)
        CUIMng::Instance().PopUpMsgWin(MESSAGE_INPUT_ID);
    else if (wcslen(m_Password) <= 0)
        CUIMng::Instance().PopUpMsgWin(MESSAGE_INPUT_PASSWORD);
    else
    {
        SendLoginRequest();
    }
}

bool CLoginWin::RequestAutomaticLogin()
{
    GameConfig& config = GameConfig::GetInstance();
    if (CurrentProtocolState != RECEIVE_JOIN_SERVER_SUCCESS)
    {
        return false;
    }

    m_Username[0] = L'\0';
    m_Password[0] = L'\0';
    const char* mobileEnabled = std::getenv(Network::Login::MobileLocalAutoLoginEnvironment);
    const bool isMobileAutomaticLogin = mobileEnabled != nullptr && std::string_view(mobileEnabled) == "1";
    if (!isMobileAutomaticLogin && config.GetRememberMe() && !config.GetEncryptedUsername().empty())
    {
        // Never switch an existing player's saved account to an empty local account.
        if (!config.GetSavePassword())
        {
            return false;
        }
        config.DecryptCredentials(m_Username, m_Password, _countof(m_Username), _countof(m_Password));
    }
    else
    {
        const auto credentials = Network::Login::LocalLoginCredentials::FromEnvironment();
        if (!credentials.IsValid())
        {
            return false;
        }
        wcsncpy_s(m_Username, _countof(m_Username), credentials.Username().c_str(), _TRUNCATE);
        wcsncpy_s(m_Password, _countof(m_Password), credentials.Password().c_str(), _TRUNCATE);
    }

    if (m_Username[0] == L'\0' || m_Password[0] == L'\0')
    {
        return false;
    }

    // Reuse the normal protocol without displaying or re-saving the login form.
    SendLoginRequest();
    return true;
}

void CLoginWin::SendLoginRequest()
{
    if (CurrentProtocolState != RECEIVE_JOIN_SERVER_SUCCESS)
    {
        return;
    }

    g_ErrorReport.Write(L"> Login Request.\r\n");
    LogIn = 1;
    wcscpy(LogInID, m_Username);
    CurrentProtocolState = REQUEST_LOG_IN;
    SocketClient->ToGameServer()->SendLogin(m_Username, m_Password, Version, Serial);
    ReconnectManager::Instance().CacheCredentials(m_Username, m_Password);
    g_pSystemLogBox->AddText(I18N::Game::VerifyingYourAccount, SEASON3B::TYPE_SYSTEM_MESSAGE);
    g_pSystemLogBox->AddText(I18N::Game::PleaseWait, SEASON3B::TYPE_SYSTEM_MESSAGE);
}

void CLoginWin::CancelLogin()
{
    ConnectConnectionServer();
    CUIMng::Instance().HideWin(this);
}

void CLoginWin::ConnectConnectionServer()
{
    LogIn = 0;
    CurrentProtocolState = REQUEST_JOIN_SERVER;
    CreateSocket(szServerIpAddress, g_ServerPort);
}
