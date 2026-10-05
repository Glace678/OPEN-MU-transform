//*****************************************************************************
// File: RegisterWin.cpp
//*****************************************************************************

#include "stdafx.h"
#include "UI/Windows/RegisterWin.h"
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
    // Same native login panel as the Connect window (login_back.tga).
    constexpr int kPanelWidth = 329;
    constexpr int kPanelHeight = 245;

    constexpr int kFieldWidth = 180;
    constexpr int kFieldHeight = 23;
    constexpr int kFieldX = 88;
    constexpr int kRowY[3] = { 52, 84, 116 };

    constexpr int kLabelX = 14;
    constexpr int kLabelWidth = 66;

    constexpr int kHintY = 152;
    constexpr int kButtonY = 190;
    constexpr int kStatusY = 228;

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

CRegisterWin::CRegisterWin()
{
}

CRegisterWin::~CRegisterWin()
{
    for (int i = 0; i < FIELD_COUNT; ++i)
        SafeDelete(m_pBox[i]);
}

void CRegisterWin::Create()
{
    // Native login panel background (same art as the Connect window).
    CWin::Create(kPanelWidth, kPanelHeight, BITMAP_LOG_IN + 7);

    for (int i = 0; i < FIELD_COUNT; ++i)
    {
        m_asprInputBox[i].Create(kFieldWidth, kFieldHeight, BITMAP_LOG_IN + 8);

        SafeDelete(m_pBox[i]);
        m_pBox[i] = new CUITextInputBox;
        m_pBox[i]->Init(g_hWnd, kFieldWidth - 12, 14,
            (i == FIELD_NAME) ? kNameLimit : kPasswordLimit,
            (i != FIELD_NAME) ? TRUE : FALSE);
        m_pBox[i]->SetBackColor(0, 0, 0, 25);
        m_pBox[i]->SetTextColor(255, 255, 230, 210);
        m_pBox[i]->SetFont(g_hFixFont);
        m_pBox[i]->SetState(UISTATE_HIDE);
    }

    m_pBox[FIELD_NAME]->SetTabTarget(m_pBox[FIELD_PASSWORD]);
    m_pBox[FIELD_PASSWORD]->SetTabTarget(m_pBox[FIELD_CONFIRM]);
    m_pBox[FIELD_CONFIRM]->SetTabTarget(m_pBox[FIELD_NAME]);

    // Native golden pixel buttons, identical to the Connect window:
    // message_ok_b_all (OK) and loding_cancel_b_all (Cancel). The wording
    // is baked into the art, so no text overlay is needed.
    m_aBtn[RB_OK].Create(54, 30, BITMAP_BUTTON, 3, 2, 1);
    CWin::RegisterButton(&m_aBtn[RB_OK]);
    m_aBtn[RB_CANCEL].Create(54, 30, BITMAP_BUTTON + 1, 3, 2, 1);
    CWin::RegisterButton(&m_aBtn[RB_CANCEL]);
}

void CRegisterWin::PreRelease()
{
    for (int i = 0; i < FIELD_COUNT; ++i)
        m_asprInputBox[i].Release();
}

void CRegisterWin::SetPosition(int x, int y)
{
    SetPositionArt((float)x / CSprite::ResolutionScaleX(),
        (float)y / CSprite::ResolutionScaleY());
}

void CRegisterWin::SetPositionArt(float fArtX, float fArtY)
{
    CWin::SetPositionArt(fArtX, fArtY);

    // Child offsets are authored art units relative to the panel.
    for (int i = 0; i < FIELD_COUNT; ++i)
    {
        m_asprInputBox[i].SetPositionArt(fArtX + kFieldX, fArtY + kRowY[i]);
        m_pBox[i]->SetPosition(
            int(fArtX + kFieldX + 6),
            int(fArtY + kRowY[i] + 6));
    }

    m_aBtn[RB_OK].SetPositionArt(fArtX + 160, fArtY + kButtonY);
    m_aBtn[RB_CANCEL].SetPositionArt(fArtX + 220, fArtY + kButtonY);
}

void CRegisterWin::Show(bool bShow)
{
    CWin::Show(bShow);

    for (int i = 0; i < 2; ++i)
        m_aBtn[i].Show(bShow);

    for (int i = 0; i < FIELD_COUNT; ++i)
    {
        m_asprInputBox[i].Show(bShow);
        if (m_pBox[i])
            m_pBox[i]->SetState(bShow ? UISTATE_NORMAL : UISTATE_HIDE);
    }

    if (!bShow)
        CUITextInputBox::ReleaseFocus();
}

bool CRegisterWin::CursorInWin(int nArea)
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

void CRegisterWin::Open(int opener)
{
    m_opener = opener;
    m_registered = false;
    m_status[0] = L'\0';

    for (int i = 0; i < FIELD_COUNT; ++i)
        m_pBox[i]->SetText(L"");

    CUIMng& ui = CUIMng::Instance();
    ui.HideWin((m_opener == FromLogin)
        ? static_cast<CWin*>(&ui.m_LoginWin)
        : static_cast<CWin*>(&ui.m_ServerSelWin));
    ui.ShowWin(this);
    m_pBox[FIELD_NAME]->GiveFocus();
}

void CRegisterWin::Close()
{
    CUIMng& ui = CUIMng::Instance();
    ui.HideWin(this);

    if (m_opener == FromLogin)
    {
        if (m_registered)
        {
            // Hand the freshly created credentials to the login form so the
            // player only has to press Connect.
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

void CRegisterWin::SetStatus(const wchar_t* text, BYTE red, BYTE green, BYTE blue)
{
    wcsncpy_s(m_status, text, _TRUNCATE);
    m_statusColorRgb[0] = red;
    m_statusColorRgb[1] = green;
    m_statusColorRgb[2] = blue;
}

void CRegisterWin::UpdateWhileShow(double)
{
    for (int i = 0; i < FIELD_COUNT; ++i)
        m_pBox[i]->DoAction();
}

void CRegisterWin::UpdateWhileActive(double)
{
    if (m_aBtn[RB_CANCEL].IsClick() || CInput::Instance().IsKeyDown(VK_ESCAPE))
    {
        PlayBuffer(SOUND_CLICK01);
        Close();
        return;
    }

    if (m_aBtn[RB_OK].IsClick() || CInput::Instance().IsKeyDown(VK_RETURN))
    {
        PlayBuffer(SOUND_CLICK01);
        SubmitRegistration();
    }
}

void CRegisterWin::SubmitRegistration()
{
    if (m_registered)
        return;

    wchar_t name[kNameLimit + 1] = {};
    wchar_t pass[kPasswordLimit + 1] = {};
    wchar_t confirm[kPasswordLimit + 1] = {};
    m_pBox[FIELD_NAME]->GetText(name, _countof(name));
    m_pBox[FIELD_PASSWORD]->GetText(pass, _countof(pass));
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
    const int passLength = static_cast<int>(wcslen(pass));

    if (nameLength == 0)
    {
        SetStatus(L"\u8BF7\u8F93\u5165\u8D26\u53F7\u3002", kStatusRed[0], kStatusRed[1], kStatusRed[2]);
        return;
    }

    for (int i = 0; i < nameLength; ++i)
    {
        if (!IsAsciiAlphaDigit(name[i]))
        {
            SetStatus(L"\u8D26\u53F7\u9700\u4E3A 3-10 \u4F4D\u5B57\u6BCD\u6216\u6570\u5B57\u3002", kStatusRed[0], kStatusRed[1], kStatusRed[2]);
            return;
        }
    }

    if (nameLength < 3 || nameLength > kNameLimit)
    {
        SetStatus(L"\u8D26\u53F7\u9700\u4E3A 3-10 \u4F4D\u5B57\u6BCD\u6216\u6570\u5B57\u3002", kStatusRed[0], kStatusRed[1], kStatusRed[2]);
        return;
    }

    if (passLength == 0)
    {
        SetStatus(L"\u8BF7\u8F93\u5165\u5BC6\u7801\u3002", kStatusRed[0], kStatusRed[1], kStatusRed[2]);
        return;
    }

    if (passLength < 8 || passLength > kPasswordLimit)
    {
        SetStatus(L"\u5BC6\u7801\u9700\u4E3A 8-20 \u4F4D\uFF0C\u4E14\u4E0D\u80FD\u542B\u7A7A\u683C\u3002", kStatusRed[0], kStatusRed[1], kStatusRed[2]);
        return;
    }

    for (int i = 0; i < passLength; ++i)
    {
        if (pass[i] < 0x21 || pass[i] > 0x7e)
        {
            SetStatus(L"\u5BC6\u7801\u9700\u4E3A 8-20 \u4F4D\uFF0C\u4E14\u4E0D\u80FD\u542B\u7A7A\u683C\u3002", kStatusRed[0], kStatusRed[1], kStatusRed[2]);
            return;
        }
    }

    if (wcscmp(pass, confirm) != 0)
    {
        SetStatus(L"\u4E24\u6B21\u8F93\u5165\u7684\u5BC6\u7801\u4E0D\u4E00\u81F4\u3002", kStatusRed[0], kStatusRed[1], kStatusRed[2]);
        return;
    }

    const auto result = Network::Login::PostAccountRegistration(szServerIpAddress, name, pass);

    if (!result.transportOk)
    {
        SetStatus(L"\u7F51\u7EDC\u8FDE\u63A5\u5931\u8D25\uFF0C\u8BF7\u786E\u8BA4\u670D\u52A1\u5668\u5DF2\u542F\u52A8\u540E\u91CD\u8BD5\u3002", kStatusRed[0], kStatusRed[1], kStatusRed[2]);
        return;
    }

    if (result.success)
    {
        m_registered = true;
        wcsncpy_s(m_lastName, name, _TRUNCATE);
        wcsncpy_s(m_lastPass, pass, _TRUNCATE);
        m_pBox[FIELD_PASSWORD]->SetText(L"");
        m_pBox[FIELD_CONFIRM]->SetText(L"");
        SetStatus(L"\u6CE8\u518C\u6210\u529F\uFF01\u8BF7\u70B9\u51FB Cancel \u8FD4\u56DE\u3002", kStatusGreen[0], kStatusGreen[1], kStatusGreen[2]);
        return;
    }

    if (result.code == "duplicate")
        SetStatus(L"\u8BE5\u8D26\u53F7\u540D\u5DF2\u5B58\u5728\uFF0C\u8BF7\u6362\u4E00\u4E2A\u3002", kStatusRed[0], kStatusRed[1], kStatusRed[2]);
    else if (result.code == "invalid_name")
        SetStatus(L"\u8D26\u53F7\u9700\u4E3A 3-10 \u4F4D\u5B57\u6BCD\u6216\u6570\u5B57\u3002", kStatusRed[0], kStatusRed[1], kStatusRed[2]);
    else if (result.code == "invalid_password")
        SetStatus(L"\u5BC6\u7801\u9700\u4E3A 8-20 \u4F4D\uFF0C\u4E14\u4E0D\u80FD\u542B\u7A7A\u683C\u3002", kStatusRed[0], kStatusRed[1], kStatusRed[2]);
    else if (result.code == "password_mismatch")
        SetStatus(L"\u4E24\u6B21\u8F93\u5165\u7684\u5BC6\u7801\u4E0D\u4E00\u81F4\u3002", kStatusRed[0], kStatusRed[1], kStatusRed[2]);
    else if (result.code == "error")
        SetStatus(L"\u670D\u52A1\u5668\u6B63\u5FD9\uFF0C\u8BF7\u7A0D\u540E\u91CD\u8BD5\u3002", kStatusYellow[0], kStatusYellow[1], kStatusYellow[2]);
    else
        SetStatus(L"\u6CE8\u518C\u5931\u8D25\uFF0C\u8BF7\u7A0D\u540E\u518D\u8BD5\u3002", kStatusRed[0], kStatusRed[1], kStatusRed[2]);
}

void CRegisterWin::RenderControls()
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

    // Title.
    g_pRenderText->SetTextColor(255, 220, 120, 255);
    g_pRenderText->RenderText(
        baseX + 24,
        baseY + 20,
        L"\u6CE8\u518C\u8D26\u53F7");
    g_pRenderText->SetTextColor(CLRDW_WHITE);

    // Row labels (right-aligned in the left column).
    g_pRenderText->RenderText(
        baseX + kLabelX,
        baseY + kRowY[0] + 7,
        L"\u8D26\u53F7", kLabelWidth, 14, RT3_SORT_RIGHT);
    g_pRenderText->RenderText(
        baseX + kLabelX,
        baseY + kRowY[1] + 7,
        L"\u5BC6\u7801", kLabelWidth, 14, RT3_SORT_RIGHT);
    g_pRenderText->RenderText(
        baseX + kLabelX,
        baseY + kRowY[2] + 7,
        L"\u786E\u8BA4\u5BC6\u7801", kLabelWidth, 14, RT3_SORT_RIGHT);

    // Rules hint.
    g_pRenderText->SetTextColor(200, 200, 200, 200);
    g_pRenderText->RenderText(
        baseX + 24,
        baseY + kHintY,
        L"\u8D26\u53F7 3-10 \u4F4D\u5B57\u6BCD\u6216\u6570\u5B57\uFF1B\u5BC6\u7801 8-20 \u4F4D\u4E14\u4E0D\u542B\u7A7A\u683C",
        285, 26);

    // Status line.
    if (m_status[0] != L'\0')
    {
        g_pRenderText->SetTextColor(255, m_statusColorRgb[0], m_statusColorRgb[1], m_statusColorRgb[2]);
        g_pRenderText->RenderText(
            baseX + 14,
            baseY + kStatusY,
            m_status, 301, 15);
    }

    g_pRenderText->SetTextColor(CLRDW_WHITE);
}
