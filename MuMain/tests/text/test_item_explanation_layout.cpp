#include "doctest.h"
#include "UI/NewUI/Inventory/ItemExplanationLayout.h"
#include <initializer_list>

TEST_CASE("item explanation supports resized and high-resolution game windows")
{
    for (int width : { 0, 320, 640, 799, 800, 960, 1024, 1280, 1366, 1920, 2560, 3840 })
    {
        const auto layout = UI::Items::ItemExplanationLayout::ForWidth(width);
        CHECK(layout.infoWidth > 0);
        CHECK(layout.labelHeight > 0);
        CHECK(layout.dataHeight > layout.labelHeight);
    }
}

TEST_CASE("item explanation retains the four legacy layouts")
{
    CHECK(UI::Items::ItemExplanationLayout::ForWidth(640).dataHeight == 52);
    CHECK(UI::Items::ItemExplanationLayout::ForWidth(800).labelHeight == 33);
    CHECK(UI::Items::ItemExplanationLayout::ForWidth(1024).infoWidth == 103);
    CHECK(UI::Items::ItemExplanationLayout::ForWidth(1280).infoWidth == 123);
}
