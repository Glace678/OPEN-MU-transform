#include "stdafx.h"
#include "doctest.h"
#include "UI/Widgets/Win.h"
#include "Core/Input/Input.h"
#include <array>
#include <cmath>

namespace
{
    struct ResolutionScope
    {
        ResolutionScope(float x, float y)
        {
            g_fScreenRate_x = x;
            g_fScreenRate_y = y;
        }
        ~ResolutionScope()
        {
            g_fScreenRate_x = 1.0f;
            g_fScreenRate_y = 1.0f;
        }
    };

    constexpr int LoginWidth = 329;
    constexpr int LoginHeight = 245;
    constexpr int NoBackground = -2;
}

TEST_CASE("CWin Create scales its actual device hit region for desktop and mobile")
{
    for (float scale : std::array{1.0f, 1.5f, 2.0f, 4.5f})
    {
        ResolutionScope resolution(scale, scale);
        CWin window;
        window.Create(LoginWidth, LoginHeight, NoBackground);
        window.SetPositionArt(110, 90);
        window.Show(true);
        CHECK(window.GetWidth() == int(LoginWidth * scale));
        CHECK(window.GetHeight() == int(LoginHeight * scale));
        const LONG right = window.GetXPos() + window.GetWidth();
        const LONG bottom = window.GetYPos() + window.GetHeight();
        CInput::Instance().SetCursorDevice(right - 1, bottom - 1);
        CHECK(window.CursorInWin(WA_ALL));
        CInput::Instance().SetCursorDevice(right, bottom - 1);
        CHECK_FALSE(window.CursorInWin(WA_ALL));
        CInput::Instance().SetCursorDevice(right - 1, bottom);
        CHECK_FALSE(window.CursorInWin(WA_ALL));
    }
}

TEST_CASE("CWin accepts the lower login button center at the Android drawable scale")
{
    ResolutionScope resolution(4.5f, 2.25f);
    constexpr float LoginArtX = (640.0f - LoginWidth) / 2;
    constexpr float LoginArtY = (480.0f - LoginHeight) / 2;
    constexpr float OkButtonCenterX = 150.0f + 54.0f / 2;
    constexpr float OkButtonCenterY = 200.0f + 30.0f / 2;
    CWin window;
    window.Create(LoginWidth, LoginHeight, NoBackground);
    window.SetPositionArt(LoginArtX, LoginArtY);
    window.Show(true);
    CInput::Instance().SetCursorDevice(
        LONG((LoginArtX + OkButtonCenterX) * g_fScreenRate_x),
        LONG((LoginArtY + OkButtonCenterY) * g_fScreenRate_y));
    CHECK(window.CursorInWin(WA_ALL));
    window.Show(false);
    CHECK_FALSE(window.CursorInWin(WA_ALL));
}

TEST_CASE("CWin nonuniform keyboard viewport size keeps scene centering in device pixels")
{
    ResolutionScope resolution(4.5f, 1.5f);
    CWin window;
    window.Create(LoginWidth, LoginHeight, NoBackground);
    window.SetPosition(
        int((640 - window.GetWidth() / g_fScreenRate_x) / 2 * g_fScreenRate_x),
        int((480 - window.GetHeight() / g_fScreenRate_y) / 2 * g_fScreenRate_y));
    CHECK(window.GetWidth() == 1480);
    CHECK(window.GetHeight() == 367);
    // CWin's integer device coordinates can truncate a half-pixel center.
    CHECK(std::abs(window.GetXPos() - (640 * g_fScreenRate_x - window.GetWidth()) / 2.0f) <= 1.0f);
    CHECK(std::abs(window.GetYPos() - (480 * g_fScreenRate_y - window.GetHeight()) / 2.0f) <= 1.0f);
}

TEST_CASE("CWin recreation applies the new scale exactly once")
{
    CWin window;
    for (auto scale : std::array{POINT{1, 1}, POINT{4, 2}, POINT{2, 1}})
    {
        ResolutionScope resolution(float(scale.x), float(scale.y));
        window.Create(LoginWidth, LoginHeight, NoBackground);
        CHECK(window.GetWidth() == LoginWidth * scale.x);
        CHECK(window.GetHeight() == LoginHeight * scale.y);
        CHECK_FALSE(window.IsShow());
    }
}

TEST_CASE("CWin Create and art size setters use the same device units")
{
    ResolutionScope resolution(3.0f, 2.0f);
    CWin window;
    window.Create(LoginWidth, LoginHeight, NoBackground);
    window.SetSize(LoginWidth, LoginHeight);
    CHECK(window.GetWidth() == LoginWidth * 3);
    CHECK(window.GetHeight() == LoginHeight * 2);
    window.SetSizeArt(400, 500, X);
    CHECK(window.GetWidth() == 1200);
    CHECK(window.GetHeight() == LoginHeight * 2);
    window.SetSizeArt(400, 500, Y);
    CHECK(window.GetHeight() == 1000);
}

TEST_CASE("CWin description device height is converted before composing an art size")
{
    for (float scale : std::array{1.0f, 2.0f, 4.5f})
    {
        ResolutionScope resolution(scale, scale);
        constexpr int DescriptionArtHeight = 52;
        constexpr int ButtonsArtHeight = 290;
        CWin description;
        description.Create(512, DescriptionArtHeight, NoBackground);
        CWin serverList;
        serverList.Create(0, 0, NoBackground);
        serverList.SetSizeArt(446, ButtonsArtHeight + description.GetHeight() / scale);
        CHECK(serverList.GetHeight() == int((ButtonsArtHeight + DescriptionArtHeight) * scale));
    }
}

TEST_CASE("character selection art-width bottom bar keeps its enter button on screen")
{
    constexpr float CanvasScale = 640.f / 800.f;
    constexpr int ButtonWidth = int(54 * CanvasScale);
    constexpr int ButtonHeight = int(30 * CanvasScale);
    constexpr int InfoWidth = 640 - int(266 * CanvasScale);
    constexpr int BarWidth = 4 * ButtonWidth + InfoWidth + int(6 * CanvasScale);
    for (float scale : std::array{1.f, 1.5f, 2.f, 3.75f, 4.5f})
    {
        ResolutionScope resolution(scale, 2.25f);
        CWin window;
        window.Create(BarWidth, ButtonHeight, NoBackground);
        const float x = 22 * CanvasScale;
        const float y = 567 * CanvasScale - window.GetHeight() / g_fScreenRate_y - 11 * CanvasScale;
        window.SetPositionArt(x, y);
        window.Show(true);
        const float right = x + window.GetWidth() / scale;
        const float enterX = right - (2 * ButtonWidth + CanvasScale) + ButtonWidth / 2.f;
        CHECK(right <= 640);
        CHECK(enterX < 640);
        CHECK(y + ButtonHeight <= 480);
        CInput::Instance().SetCursorDevice(LONG(enterX * scale), LONG((y + ButtonHeight / 2.f) * g_fScreenRate_y));
        CHECK(window.CursorInWin(WA_ALL));
    }
}
