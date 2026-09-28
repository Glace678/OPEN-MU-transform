//*****************************************************************************
// File: PasswordServiceWin.cpp
//*****************************************************************************

#include "stdafx.h"
#include "UI/Windows/PasswordServiceWin.h"
#include "Core/Input/Input.h"
#include "UI/Legacy/UIMng.h"
#include "UI/Legacy/UIControls.h"
#include "Scenes/SceneCore.h"
#include "I18N/All.h"
#include "Audio/DSPlaySound.h"
#include "Network/Login/AccountRegistration.h"

#include <cwctype>

namespace
{
    constexpr int kPanelWidth = 300;
    constexpr int kPanelHeight = 250;

    constexpr int kFieldWidth = 190;
    constexpr int kFieldHeight = 23;
    constexpr int kFieldX = 84;

    // Rows differ per mode: change shows 4 rows, reset hides "current password".
    constexpr int kRowYChange[4] = { 44, 74, 104, 134 };
    constexpr int kRowYReset[4] = { 44, -1, 74, 104 };

    constexpr int kButtonY = 190;
    constexpr int kStatusY = 224;
    constexpr int kHint1Y = 152;
    constexpr int kHint2Y = 166;

    constexpr int kNameLimit = 10;
    constexpr int kPasswordLimit = 20;

    // Status line colors (r, g, b).
    constexpr BYTE kStatusRed[3] = { 255, 96, 96 };
    constexpr BYTE kStatusGreen[3] = { 120, 230, 130 };
    constexpr BYTE kStatusYellow[3] = { 255, 210, 90 };

    bool IsAsciiAlphaDigit(wchar_t ch)
    {
        return (ch >= L'0' && ch <= L'9')
            || (ch >= L'a' && ch <= L'z')
            || (ch >= L'A' && ch <= L'Z');
    }
}

CPasswordServiceWin::CPasswordServiceWin()
{
}

CPasswordServiceWin::~CPasswordServiceWin()
{
    for (int i = 0; i < FIELD_COUNT; ++i)
        SAFE_DELETE(m_pBox[i]);
}

void CPasswordServiceWin::Create()
{
    // -1 = translucent black panel (see CWin::Create).
    CWin::Create(kPanelWidth, kPanelHeight, -1);

    for (int i = 0; i < FIELD_COUNT; ++i)
    {
        m_asprInputBox[i].Create(kFieldWidth, kFieldHeight, BITMAP_LOG_IN + 8);

        SAFE_DELETE(m_pBox[i]);
        m_pBox[i] = new CUITextInputBox;
        m_pBox[i]->Init(g_hWnd, kFieldWidth - 12, 14,
            (i == FIELD_NAME) ? kNameLimit : kPasswordLimit,
            (i != FIELD_NAME) ? TRUE : FALSE);
        m_pBox[i]->SetBackColor(0, 0, 0, 25);
        m_pBox[i]->SetTextColor(255, 255, 230, 210);
        m_pBox[i]->SetFont(g_hFixFont);
        m_pBox[i]->SetState(UISTATE_HIDE);
    }

    static const DWORD btnColors[4] =
    {
        CLRDW_BR_GRAY, CLRDW_BR_GRAY, CLRDW_WHITE, 0
    };

    for (int i = 0; i < 2; ++i)
    {
        m_aBtn[i].Create(80, 26, BITMAP_LOG_IN + 1, 3, 2, 1);
        m_aBtn[i].SetText(L"确定", const_cast<DWORD*>(btnColors));
        CWin::RegisterButton(&m_aBtn[i]);
    }
}

void CPasswordServiceWin::PreRelease()
{
    for (int i = 0; i < FIELD_COUNT; ++i)
        m_asprInputBox[i].Release();
}

void CPasswordServiceWin::SetPosition(int x, int y)
{
    SetPositionArt((float)x / CSprite::ResolutionScaleX(),
        (float)y / CSprite::ResolutionScaleY());
}

void CPasswordServiceWin::SetPositionArt(float fArtX, float fArtY)
{
    CWin::SetPositionArt(fArtX, fArtY);
    m_fArtX = fArtX;
    m_fArtY = fArtY;
    LayoutChildren();
}

void CPasswordServiceWin::LayoutChildren()
{
    const int* rows = (m_mode == ModeChange) ? kRowYChange : kRowYReset;

    for (int i = 0; i < FIELD_COUNT; ++i)
    {
        if (rows[i] < 0)
            continue;

        m_asprInputBox[i].SetPositionArt(m_fArtX + kFieldX, m_fArtY + rows[i]);
        m_pBox[i]->SetPosition(
            int(m_fArtX + kFieldX + 6),
            int(m_fArtY + rows[i] + 6));
    }

    m_aBtn[SB_OK].SetPositionArt(m_fArtX + 58, m_fArtY + kButtonY);
    m_aBtn[SB_CANCEL].SetPositionArt(m_fArtX + 162, m_fArtY + kButtonY);
}

void CPasswordServiceWin::Show(bool bShow)
{
    CWin::Show(bShow);

    for (int i = 0; i < 2; ++i)
        m_aBtn[i].Show(bShow);

    for (int i = 0; i < FIELD_COUNT; ++i)
    {
        const bool hiddenRow = (i == FIELD_OLD && m_mode == ModeReset);
        m_asprInputBox[i].Show(bShow && !hiddenRow);
        if (m_pBox[i])
        {
            m_pBox[i]->SetState((bShow && !hiddenRow) ? UISTATE_NORMAL : UISTATE_HIDE);
        }
    }

    if (!bShow)
        CUITextInputBox::ReleaseFocus();
}

bool CPasswordServiceWin::CursorInWin(int nArea)
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

void CPasswordServiceWin::Open(int mode, int opener, const wchar_t* prefillName)
{
    m_mode = mode;
    m_opener = opener;
    m_succeeded = false;
    m_status[0] = L'\0';

    for (int i = 0; i < FIELD_COUNT; ++i)
        m_pBox[i]->SetText(L"");

    if (prefillName != nullptr)
        m_pBox[FIELD_NAME]->SetText(prefillName);

    static const DWORD btnColors[4] =
    {
        CLRDW_BR_GRAY, CLRDW_BR_GRAY, CLRDW_WHITE, 0
    };
    m_aBtn[SB_OK].SetText((m_mode == ModeChange)
        ? L"修改"
        : L"重置", const_cast<DWORD*>(btnColors));

    LayoutChildren();

    // Tab order skips the hidden row in reset mode.
    if (m_mode == ModeChange)
    {
        m_pBox[FIELD_NAME]->SetTabTarget(m_pBox[FIELD_OLD]);
        m_pBox[FIELD_OLD]->SetTabTarget(m_pBox[FIELD_NEW]);
        m_pBox[FIELD_NEW]->SetTabTarget(m_pBox[FIELD_CONFIRM]);
        m_pBox[FIELD_CONFIRM]->SetTabTarget(m_pBox[FIELD_NAME]);
    }
    else
    {
        m_pBox[FIELD_NAME]->SetTabTarget(m_pBox[FIELD_NEW]);
        m_pBox[FIELD_NEW]->SetTabTarget(m_pBox[FIELD_CONFIRM]);
        m_pBox[FIELD_CONFIRM]->SetTabTarget(m_pBox[FIELD_NAME]);
    }

    CUIMng& ui = CUIMng::Instance();
    ui.HideWin((m_opener == FromLogin)
        ? static_cast<CWin*>(&ui.m_LoginWin)
        : static_cast<CWin*>(&ui.m_ServerSelWin));
    ui.ShowWin(this);
    m_pBox[FIELD_NAME]->GiveFocus();
}

void CPasswordServiceWin::Close()
{
    CUIMng& ui = CUIMng::Instance();
    ui.HideWin(this);

    if (m_opener == FromLogin)
    {
        if (m_succeeded)
        {
            // Hand the updated credentials to the login form so the player only
            // has to press Connect.
            ui.m_LoginWin.GetUsernameInputBox()->SetText(m_lastName);
            ui.m_LoginWin.GetPasswordInputBox()->SetText(m_lastPass);
        }

        ui.ShowWin(&ui.m_LoginWin);
    }
    else
    {
        ui.ShowWin(&ui.m_ServerSelWin);
    }
}

void CPasswordServiceWin::SetStatus(const wchar_t* text, BYTE red, BYTE green, BYTE blue)
{
    wcsncpy_s(m_status, text, _TRUNCATE);
    m_statusColorRgb[0] = red;
    m_statusColorRgb[1] = green;
    m_statusColorRgb[2] = blue;
}

void CPasswordServiceWin::UpdateWhileShow(double)
{
    for (int i = 0; i < FIELD_COUNT; ++i)
        m_pBox[i]->DoAction();
}

void CPasswordServiceWin::UpdateWhileActive(double)
{
    if (m_aBtn[SB_CANCEL].IsClick() || CInput::Instance().IsKeyDown(VK_ESCAPE))
    {
        PlayBuffer(SOUND_CLICK01);
        Close();
        return;
    }

    if (m_aBtn[SB_OK].IsClick() || CInput::Instance().IsKeyDown(VK_RETURN))
    {
        PlayBuffer(SOUND_CLICK01);
        Submit();
    }
}

void CPasswordServiceWin::Submit()
{
    if (m_succeeded)
        return;

    wchar_t name[kNameLimit + 1] = {};
    wchar_t oldPass[kPasswordLimit + 1] = {};
    wchar_t newPass[kPasswordLimit + 1] = {};
    wchar_t confirm[kPasswordLimit + 1] = {};
    m_pBox[FIELD_NAME]->GetText(name, _countof(name));
    m_pBox[FIELD_OLD]->GetText(oldPass, _countof(oldPass));
    m_pBox[FIELD_NEW]->GetText(newPass, _countof(newPass));
    m_pBox[FIELD_CONFIRM]->GetText(confirm, _countof(confirm));

    // Trim trailing spaces (the classic fields never need them).
    const auto trim = [](wchar_t* value)
    {
        const size_t length = wcslen(value);
        size_t end = length;
        while (end > 0 && iswspace(value[end - 1]))
            --end;
        value[end] = L'\0';
    };
    trim(name);

    const int nameLength = static_cast<int>(wcslen(name));
    const int newLength = static_cast<int>(wcslen(newPass));

    if (nameLength == 0)
    {
        SetStatus(L"请输入账号。", kStatusRed[0], kStatusRed[1], kStatusRed[2]);
        return;
    }

    for (int i = 0; i < nameLength; ++i)
    {
        if (!IsAsciiAlphaDigit(name[i]))
        {
            SetStatus(L"账号需为 3-10 位字母或数字。", kStatusRed[0], kStatusRed[1], kStatusRed[2]);
            return;
        }
    }

    if (nameLength < 3 || nameLength > kNameLimit)
    {
        SetStatus(L"账号需为 3-10 位字母或数字。", kStatusRed[0], kStatusRed[1], kStatusRed[2]);
        return;
    }

    if (m_mode == ModeChange && oldPass[0] == L'\0')
    {
        SetStatus(L"请输入当前密码。", kStatusRed[0], kStatusRed[1], kStatusRed[2]);
        return;
    }

    if (newLength == 0)
    {
        SetStatus(L"请输入新密码。", kStatusRed[0], kStatusRed[1], kStatusRed[2]);
        return;
    }

    if (newLength < 3 || newLength > kPasswordLimit)
    {
        SetStatus(L"新密码需为 3-20 位，且不能含空格。", kStatusRed[0], kStatusRed[1], kStatusRed[2]);
        return;
    }

    for (int i = 0; i < newLength; ++i)
    {
        if (newPass[i] < 0x21 || newPass[i] > 0x7e)
        {
            SetStatus(L"新密码需为 3-20 位，且不能含空格。", kStatusRed[0], kStatusRed[1], kStatusRed[2]);
            return;
        }
    }

    if (m_mode == ModeChange && wcscmp(oldPass, newPass) == 0)
    {
        SetStatus(L"新密码不能与当前密码相同。", kStatusRed[0], kStatusRed[1], kStatusRed[2]);
        return;
    }

    if (wcscmp(newPass, confirm) != 0)
    {
        SetStatus(L"两次输入的新密码不一致。", kStatusRed[0], kStatusRed[1], kStatusRed[2]);
        return;
    }

    const auto result = (m_mode == ModeChange)
        ? Network::Login::PostPasswordChange(szServerIpAddress, name, oldPass, newPass)
        : Network::Login::PostPasswordReset(szServerIpAddress, name, newPass);

    if (!result.transportOk)
    {
        SetStatus(L"网络连接失败，请确认服务器已启动后重试。", kStatusRed[0], kStatusRed[1], kStatusRed[2]);
        return;
    }

    if (result.success)
    {
        m_succeeded = true;
        wcsncpy_s(m_lastName, name, _TRUNCATE);
        wcsncpy_s(m_lastPass, newPass, _TRUNCATE);
        m_pBox[FIELD_OLD]->SetText(L"");
        m_pBox[FIELD_NEW]->SetText(L"");
        m_pBox[FIELD_CONFIRM]->SetText(L"");
        SetStatus((m_mode == ModeChange)
                ? L"密码修改成功！请点击取消返回。"
                : L"密码已重置！请点击取消返回。",
            kStatusGreen[0], kStatusGreen[1], kStatusGreen[2]);
        return;
    }

    const std::string& code = result.code;
    if (code == "bad_credentials" || code == "invalid_old_password")
        SetStatus(L"账号名或当前密码不正确。", kStatusRed[0], kStatusRed[1], kStatusRed[2]);
    else if (code == "forbidden")
        SetStatus(L"忘记密码只能在运行服务器的电脑上重置，或联系管理员在后台重置。", kStatusYellow[0], kStatusYellow[1], kStatusYellow[2]);
    else if (code == "not_found")
        SetStatus(L"该账号名不存在。", kStatusRed[0], kStatusRed[1], kStatusRed[2]);
    else if (code == "invalid_name")
        SetStatus(L"账号需为 3-10 位字母或数字。", kStatusRed[0], kStatusRed[1], kStatusRed[2]);
    else if (code == "invalid_password" || code == "invalid_password_chars")
        SetStatus(L"新密码需为 3-20 位，且不能含空格。", kStatusRed[0], kStatusRed[1], kStatusRed[2]);
    else if (code == "password_mismatch")
        SetStatus(L"两次输入的新密码不一致。", kStatusRed[0], kStatusRed[1], kStatusRed[2]);
    else if (code == "error")
        SetStatus(L"服务器正忙，请稍后重试。", kStatusYellow[0], kStatusYellow[1], kStatusYellow[2]);
    else
        SetStatus(L"操作失败，请稍后再试。", kStatusRed[0], kStatusRed[1], kStatusRed[2]);
}

void CPasswordServiceWin::RenderControls()
{
    CWin::RenderButtons();

    for (int i = 0; i < FIELD_COUNT; ++i)
    {
        m_asprInputBox[i].Render();
        m_pBox[i]->Render();
    }

    g_pRenderText->SetFont(g_hFixFont);
    g_pRenderText->SetBgColor(0);
    g_pRenderText->SetTextColor(CLRDW_WHITE);

    const float rateX = CSprite::ResolutionScaleX();
    const float rateY = CSprite::ResolutionScaleY();
    const int baseX = int(GetXPos() / rateX);
    const int baseY = int(GetYPos() / rateY);

    const int* rows = (m_mode == ModeChange) ? kRowYChange : kRowYReset;

    // Title.
    g_pRenderText->SetTextColor(255, 255, 220, 120);
    g_pRenderText->RenderText(
        baseX + 18,
        baseY + 14,
        (m_mode == ModeChange)
            ? L"修改密码"
            : L"忘记密码");
    g_pRenderText->SetTextColor(CLRDW_WHITE);

    // Row labels (aligned to the box center).
    g_pRenderText->RenderText(baseX + 18, baseY + rows[FIELD_NAME] + 7, L"账号");

    if (m_mode == ModeChange)
        g_pRenderText->RenderText(baseX + 18, baseY + rows[FIELD_OLD] + 7, L"旧密码");

    g_pRenderText->RenderText(baseX + 18, baseY + rows[FIELD_NEW] + 7, L"新密码");
    g_pRenderText->RenderText(baseX + 4, baseY + rows[FIELD_CONFIRM] + 7, L"确认新密码");

    // Hint lines.
    g_pRenderText->SetTextColor(200, 200, 200, 200);
    if (m_mode == ModeChange)
    {
        g_pRenderText->RenderText(baseX + 18, baseY + kHint1Y,
            L"使用当前密码验证身份。");
        g_pRenderText->RenderText(baseX + 18, baseY + kHint2Y,
            L"新密码：3-20 位，不能含空格");
    }
    else
    {
        g_pRenderText->RenderText(baseX + 18, baseY + kHint1Y,
            L"只能在运行服务器的电脑上操作。");
        g_pRenderText->RenderText(baseX + 18, baseY + kHint2Y,
            L"远程设备请联系管理员重置。");
    }

    // Status line.
    if (m_status[0] != L'\0')
    {
        g_pRenderText->SetTextColor(255, m_statusColorRgb[0], m_statusColorRgb[1], m_statusColorRgb[2]);
        g_pRenderText->RenderText(
            baseX + 14,
            baseY + kStatusY,
            m_status, 272, 14);
    }

    g_pRenderText->SetTextColor(CLRDW_WHITE);
}
