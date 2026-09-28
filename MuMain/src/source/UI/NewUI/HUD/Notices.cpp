#include "stdafx.h"
#include "Core/Text/TextLineWrap.h"
#include "UI/NewUI/HUD/Notices.h"

#include "UI/Legacy/UIControls.h"        // g_pRenderText, RT3_WRITE_CENTER
#include "App/Platform/Windows/Winmain.h"    // g_hFontBold
#include "Render/Textures/ZzzOpenglUtil.h" // EnableAlphaTest
#include "UI/NewUI/NewUISystem.h"        // g_pNewUISystem
#include "Engine/AI/ZzzAI.h"             // FPS_ANIMATION_FACTOR
#include <cmath>

namespace
{
    constexpr int MAX_NOTICE = 6;
    constexpr int NOTICE_LIFETIME = 300;
    constexpr int NOTICE_TEXT_MAX = 256;
    constexpr int NOTICE_WIDTH = 256;
    constexpr int NOTICE_LINE_GAP = 2;

    struct Notice
    {
        wchar_t Text[NOTICE_TEXT_MAX];
        int     LifeTime;
        BYTE    Color;
    };

    int    s_count = 0;
    int    s_time = NOTICE_LIFETIME;
    float  s_blinkPhase = 0.f;
    Notice s_notices[MAX_NOTICE];

    // Shift the buffer up by one when it is full so the newest line fits.
    void Scroll()
    {
        if (s_count > MAX_NOTICE - 1)
        {
            s_count = MAX_NOTICE - 1;
            for (int i = 1; i < MAX_NOTICE; i++)
            {
                s_notices[i - 1].Color = s_notices[i].Color;
                wcscpy(s_notices[i - 1].Text, s_notices[i].Text);
            }
        }
    }

    void AppendLine(const std::wstring& text, int color)
    {
        Scroll();
        auto& notice = s_notices[s_count++];
        notice.Color = color;
        wcsncpy_s(notice.Text, NOTICE_TEXT_MAX, text.c_str(), _TRUNCATE);
    }
}

namespace UI::Notices
{
    void Clear()
    {
        memset(s_notices, 0, sizeof(s_notices));
    }

    void Create(const wchar_t* text, int color)
    {
        if (text == nullptr)
            return;
        g_pRenderText->SetFont(g_hFontBold);
        const auto measure = [](const wchar_t* value, size_t length)
        {
            SIZE size{};
            GetTextExtentPoint32(g_pRenderText->GetFontDC(), value, static_cast<int>(length), &size);
            return static_cast<int>(size.cx);
        };
        const auto lines = WrapTextToWidth(text, static_cast<int>(NOTICE_WIDTH * g_fScreenRate_x), measure);
        if (lines.empty())
            AppendLine(L"", color);
        for (const auto& line : lines)
            AppendLine(line, color);
        s_time = NOTICE_LIFETIME;
    }

    void Move()
    {
        s_time -= FPS_ANIMATION_FACTOR;
        if (s_time <= 0)
        {
            s_time = NOTICE_LIFETIME;
            Create(L"", 0);
        }
    }

    void Render()
    {
#ifdef KJH_ADD_INGAMESHOP_UI_SYSTEM
        if (g_pNewUISystem->IsVisible(SEASON3B::INTERFACE_INGAMESHOP) == true)
            return;
#endif // KJH_ADD_INGAMESHOP_UI_SYSTEM

        EnableAlphaTest();

        g_pRenderText->SetFont(g_hFontBold);

        SIZE fontSize{};
        GetTextExtentPoint32(g_pRenderText->GetFontDC(), L"Ag", 2, &fontSize);
        const int lineHeight = static_cast<int>(std::ceil(fontSize.cy / g_fScreenRate_y)) + NOTICE_LINE_GAP;

        glColor3f(1.f, 1.f, 1.f);
        for (int i = 0; i < MAX_NOTICE; i++)
        {
            Notice* n = &s_notices[i];
            if (n->Color == 0)
            {
                g_pRenderText->SetBgColor(0, 0, 0, 128);
                if ((int)s_blinkPhase % 10 < 5)
                {
                    g_pRenderText->SetTextColor(255, 200, 80, 128);
                }
                else
                {
                    g_pRenderText->SetTextColor(255, 200, 80, 255);
                }
            }
            else
            {
                g_pRenderText->SetTextColor(100, 255, 200, 255);
                g_pRenderText->SetBgColor(0, 0, 0, 128);
            }

            g_pRenderText->RenderText(320, 300 + i * lineHeight, n->Text, 0, 0, RT3_WRITE_CENTER);
        }

        s_blinkPhase += FPS_ANIMATION_FACTOR;
    }
}
