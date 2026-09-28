#pragma once

#include <algorithm>

namespace Core::Text
{
    struct TextBoxLayout
    {
        float scale;
        float offsetX;
        float offsetY;
    };

    inline TextBoxLayout FitTextInBox(float textWidth, float textHeight, float boxWidth, float boxHeight)
    {
        if (textWidth <= 0.f || textHeight <= 0.f || boxWidth <= 0.f || boxHeight <= 0.f)
            return { 0.f, 0.f, 0.f };
        const float scale = std::min({ 1.f, boxWidth / textWidth, boxHeight / textHeight });
        return { scale, (boxWidth - textWidth * scale) / 2.f, (boxHeight - textHeight * scale) / 2.f };
    }
}
