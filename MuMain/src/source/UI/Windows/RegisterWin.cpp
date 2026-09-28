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
    constexpr int kPanelWidth = 300;
    constexpr int kPanelHeight = 220;

    constexpr int kFieldWidth = 190;
    constexpr int kFieldHeight = 23;
    constexpr int kFieldX = 84;
    constexpr int kRowY[3] = { 44, 74, 104 };

    constexpr int kNameLimit = 10;
    constexpr int kPasswordLimit = 20;

    // Status line colors (a, r, g, b).
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
        SAFE_DELETE(m_pBox[i]);
}

void CRegisterWin::Create()
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

    m_pBox[FIELD_NAME]->SetTabTarget(m_pBox[FIELD_PASSWORD]);
    m_pBox[FIELD_PASSWORD]->SetTabTarget(m_pBox[FIELD_CONFIRM]);
    m_pBox[FIELD_CONFIRM]->SetTabTarget(m_pBox[FIELD_NAME]);

    static const DWORD btnColors[4] =
    {
        CLRDW_BR_GRAY, CLRDW_BR_GRAY, CLRDW_WHITE, 0
    };

    for (int i = 0; i < 2; ++i)
    {
        m_aBtn[i].Create(80, 26, BITMAP_LOG_IN + 1, 3, 2, 1);
        m_aBtn[i].SetText((i == RB_OK)
            ? L"\u6CE8\u518C"
            : L"\u53D6\u6D88", const_cast<DWORD*>(btnColors));
        CWin::RegisterButton(&m_aBtn[i]);
    }
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

    m_aBtn[RB_OK].SetPositionArt(fArtX + 58, fArtY + 172);
    m_aBtn[RB_CANCEL].SetPositionArt(fArtX + 162, fArtY + 172);
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

#if defined(_WIN32)
    // TEMP DEBUG
    if (bShow)
    {
        FILE* fp = ::fopen("debug_regwin.txt", "w");
        if (fp != nullptr)
        {
            ::fprintf(fp, "win x=%d y=%d w=%d h=%d\n",
                GetXPos(), GetYPos(), GetWidth(), GetHeight());
            for (int i = 0; i < FIELD_COUNT; ++i)
                ::fprintf(fp, "field[%d] x=%d y=%d w=%d h=%d\n", i,
                    m_asprInputBox[i].GetXPos(), m_asprInputBox[i].GetYPos(),
                    m_asprInputBox[i].GetWidth(), m_asprInputBox[i].GetHeight());
            for (int i = 0; i < 2; ++i)
                ::fprintf(fp, "btn[%d] x=%d y=%d w=%d h=%d\n", i,
                    m_aBtn[i].GetXPos(), m_aBtn[i].GetYPos(),
                    m_aBtn[i].GetWidth(), m_aBtn[i].GetHeight());
            ::fclose(fp);
        }
    }
#endif
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

#if defined(_WIN32)
    // TEMP DEBUG
    if (FILE* ofp = ::fopen("debug_open.txt", "w"))
    {
        ::fprintf(ofp, "Open opener=%d this-win x=%d y=%d\n",
            m_opener, GetXPos(), GetYPos());
        ::fclose(ofp);
    }
#endif
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

#if defined(_WIN32)
    // TEMP DEBUG
    if (FILE* dfp = ::fopen("debug_submit.txt", "w"))
    {
        ::fprintf(dfp, "name='%ls'(%d) pass='%ls'(%d) confirm='%ls' host='%ls'\n",
            name, int(wcslen(name)), pass, int(wcslen(pass)), confirm, szServerIpAddress);
        ::fclose(dfp);
    }
#endif

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

    if (passLength < 3 || passLength > kPasswordLimit)
    {
        SetStatus(L"\u5BC6\u7801\u9700\u4E3A 3-20 \u4F4D\uFF0C\u4E14\u4E0D\u80FD\u542B\u7A7A\u683C\u3002", kStatusRed[0], kStatusRed[1], kStatusRed[2]);
        return;
    }

    for (int i = 0; i < passLength; ++i)
    {
        if (pass[i] < 0x21 || pass[i] > 0x7e)
        {
            SetStatus(L"\u5BC6\u7801\u9700\u4E3A 3-20 \u4F4D\uFF0C\u4E14\u4E0D\u80FD\u542B\u7A7A\u683C\u3002", kStatusRed[0], kStatusRed[1], kStatusRed[2]);
            return;
        }
    }

    if (wcscmp(pass, confirm) != 0)
    {
        SetStatus(L"\u4E24\u6B21\u8F93\u5165\u7684\u5BC6\u7801\u4E0D\u4E00\u81F4\u3002", kStatusRed[0], kStatusRed[1], kStatusRed[2]);
        return;
    }

    const auto result = Network::Login::PostAccountRegistration(szServerIpAddress, name, pass);

#if defined(_WIN32)
    // TEMP DEBUG
    if (FILE* rfp = ::fopen("debug_submit.txt", "a"))
    {
        ::fprintf(rfp, "transport=%d success=%d code='%s'\n",
            result.transportOk ? 1 : 0, result.success ? 1 : 0, result.code.c_str());
        ::fclose(rfp);
    }
#endif

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
        SetStatus(L"\u6CE8\u518C\u6210\u529F\uFF01\u8BF7\u70B9\u51FB\u53D6\u6D88\u8FD4\u56DE\u3002", kStatusGreen[0], kStatusGreen[1], kStatusGreen[2]);
        return;
    }

    if (result.code == "duplicate")
        SetStatus(L"\u8BE5\u8D26\u53F7\u540D\u5DF2\u5B58\u5728\uFF0C\u8BF7\u6362\u4E00\u4E2A\u3002", kStatusRed[0], kStatusRed[1], kStatusRed[2]);
    else if (result.code == "invalid_name")
        SetStatus(L"\u8D26\u53F7\u9700\u4E3A 3-10 \u4F4D\u5B57\u6BCD\u6216\u6570\u5B57\u3002", kStatusRed[0], kStatusRed[1], kStatusRed[2]);
    else if (result.code == "invalid_password")
        SetStatus(L"\u5BC6\u7801\u9700\u4E3A 3-20 \u4F4D\uFF0C\u4E14\u4E0D\u80FD\u542B\u7A7A\u683C\u3002", kStatusRed[0], kStatusRed[1], kStatusRed[2]);
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
    g_pRenderText->SetTextColor(255, 255, 220, 120);
    g_pRenderText->RenderText(
        baseX + 18,
        baseY + 14,
        L"\u6CE8\u518C\u8D26\u53F7");
    g_pRenderText->SetTextColor(CLRDW_WHITE);

    // Row labels (aligned to the box center).
    g_pRenderText->RenderText(
        baseX + 18,
        baseY + kRowY[0] + 7,
        L"\u8D26\u53F7");
    g_pRenderText->RenderText(
        baseX + 18,
        baseY + kRowY[1] + 7,
        L"\u5BC6\u7801");
    g_pRenderText->RenderText(
        baseX + 4,
        baseY + kRowY[2] + 7,
        L"\u786E\u8BA4\u5BC6\u7801");

    // Rules hint.
    g_pRenderText->SetTextColor(200, 200, 200, 200);
    g_pRenderText->RenderText(
        baseX + 18,
        baseY + 138,
        L"\u8D26\u53F7\uFF1A3-10 \u4F4D\u5B57\u6BCD\u6216\u6570\u5B57");
    g_pRenderText->RenderText(
        baseX + 18,
        baseY + 154,
        L"\u5BC6\u7801\uFF1A3-20 \u4F4D\uFF0C\u4E0D\u80FD\u542B\u7A7A\u683C");

    // Status line.
    if (m_status[0] != L'\0')
    {
        g_pRenderText->SetTextColor(255, m_statusColorRgb[0], m_statusColorRgb[1], m_statusColorRgb[2]);
        g_pRenderText->RenderText(
            baseX + 14,
            baseY + 204,
            m_status, 272, 14);
    }

    g_pRenderText->SetTextColor(CLRDW_WHITE);
}
