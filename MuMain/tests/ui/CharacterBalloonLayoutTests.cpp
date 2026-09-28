#include <doctest.h>
#include "Character/CharacterBalloonLayout.h"

using UI::CharacterSelection::LayoutBalloon;

TEST_CASE("character balloons fit content rather than a 118-art-pixel minimum")
{
    const auto layout = LayoutBalloon(2400, 1080, 3.75f, 2.25f, 1200, 550, 85, 22, 245);
    CHECK(layout.width == 165);
    CHECK(layout.width < 245);
    CHECK(layout.lineHeight == 27);
    CHECK(layout.height == 109);
}

TEST_CASE("long translated character labels cannot overlap adjacent panels")
{
    for (float scale : {1.f, 1.5f, 2.f, 3.75f, 4.5f})
    {
        const int gap = int(64 * scale);
        const auto left = LayoutBalloon(int(640 * scale), 1080, scale, 2.25f,
            int(290 * scale), 600, int(150 * scale), 22, gap);
        const auto right = LayoutBalloon(int(640 * scale), 1080, scale, 2.25f,
            int(354 * scale), 600, int(150 * scale), 22, gap);
        CHECK(left.width <= gap - int(4 * scale));
        CHECK(left.centerX + (left.width + 1) / 2 < right.centerX - right.width / 2);
    }
}

TEST_CASE("character balloon bounds clamp safely at all viewport edges")
{
    for (const int centerX : {-50, 0, 639, 900})
        for (const int bottomY : {-50, 0, 479, 900})
        {
            const auto layout = LayoutBalloon(640, 480, 1.f, 1.f, centerX, bottomY, 80, 16, 640);
            CHECK(layout.centerX - layout.width / 2 >= 0);
            CHECK(layout.centerX + (layout.width + 1) / 2 <= 640);
            CHECK(layout.bottomY - layout.height >= 0);
            CHECK(layout.bottomY <= 480);
        }
}
