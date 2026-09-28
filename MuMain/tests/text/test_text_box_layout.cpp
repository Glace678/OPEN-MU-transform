#include "doctest.h"
#include "Core/Text/TextBoxLayout.h"

TEST_CASE("Text fitting keeps full labels inside both dimensions at supported window scales")
{
    for (float windowScale : { 1.f, 1.25f, 1.6f, 2.25f, 3.f, 4.5f })
    {
        for (float textWidth : { 20.f, 80.f, 400.f, 2000.f })
        {
            for (float textHeight : { 12.f, 28.f, 64.f })
            {
                const float width = 54.f * windowScale;
                const float height = 18.f * windowScale;
                const auto layout = Core::Text::FitTextInBox(textWidth * windowScale,
                    textHeight * windowScale, width, height);
                CHECK(layout.scale > 0.f);
                CHECK(layout.scale <= 1.f);
                CHECK(layout.offsetX >= -0.001f);
                CHECK(layout.offsetY >= -0.001f);
                CHECK(layout.offsetX + textWidth * windowScale * layout.scale <= width + 0.001f);
                CHECK(layout.offsetY + textHeight * windowScale * layout.scale <= height + 0.001f);
                CHECK(layout.offsetX * 2.f + textWidth * windowScale * layout.scale == doctest::Approx(width));
                CHECK(layout.offsetY * 2.f + textHeight * windowScale * layout.scale == doctest::Approx(height));
            }
        }
    }
}

TEST_CASE("Text fitting leaves small labels at their requested font size")
{
    const auto layout = Core::Text::FitTextInBox(20.f, 12.f, 60.f, 20.f);
    CHECK(layout.scale == 1.f);
    CHECK(layout.offsetX == 20.f);
    CHECK(layout.offsetY == 4.f);
}

TEST_CASE("Text fitting rejects empty or invalid boxes")
{
    CHECK(Core::Text::FitTextInBox(0.f, 12.f, 60.f, 20.f).scale == 0.f);
    CHECK(Core::Text::FitTextInBox(20.f, 0.f, 60.f, 20.f).scale == 0.f);
    CHECK(Core::Text::FitTextInBox(20.f, 12.f, -1.f, 20.f).scale == 0.f);
    CHECK(Core::Text::FitTextInBox(20.f, 12.f, 60.f, 0.f).scale == 0.f);
}
