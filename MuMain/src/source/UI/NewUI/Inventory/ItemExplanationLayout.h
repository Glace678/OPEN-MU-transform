#pragma once

namespace UI::Items
{
    struct ItemExplanationLayout
    {
        int infoWidth;
        int labelHeight;
        int dataHeight;

        static constexpr ItemExplanationLayout ForWidth(int windowWidth)
        {
            if (windowWidth >= 1280)
                return { 123, 22, 32 };
            if (windowWidth >= 1024)
                return { 103, 28, 40 };
            if (windowWidth >= 800)
                return { 90, 33, 47 };
            return { 90, 38, 52 };
        }
    };
}
