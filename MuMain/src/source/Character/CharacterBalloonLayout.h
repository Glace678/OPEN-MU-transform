#pragma once

#include <algorithm>
#include <cmath>

namespace UI::CharacterSelection
{
    struct BalloonLayout
    {
        int width, height, centerX, bottomY, textTop, lineHeight;
    };

    // All measurements and the projected anchor are device pixels. Neighbor
    // spacing bounds the text panel; long labels are fitted by RenderText.
    inline BalloonLayout LayoutBalloon(int screenWidth, int screenHeight,
        float scaleX, float scaleY, int centerX, int bottomY,
        int textWidth, int textHeight, int nearestNeighborDistance)
    {
        constexpr int LineCount = 3;
        constexpr float MinimumArtWidth = 44.f;
        constexpr float Padding = 6.f;
        constexpr float LineGap = 2.f;
        constexpr float NeighborGap = 4.f;
        const int textTop = static_cast<int>(std::ceil(Padding * scaleY));
        const int lineHeight = textHeight + static_cast<int>(std::ceil(LineGap * scaleY));
        const int desiredWidth = static_cast<int>(std::ceil(
            std::max(MinimumArtWidth * scaleX, textWidth + Padding * 2.f * scaleX)));
        const int availableWidth = std::max(1, std::min(screenWidth,
            nearestNeighborDistance - static_cast<int>(std::ceil(NeighborGap * scaleX))));
        const int width = std::min(desiredWidth, availableWidth);
        const int height = std::min(screenHeight, LineCount * lineHeight + 2 * textTop);
        return {width, height,
            std::clamp(centerX, width / 2, screenWidth - (width + 1) / 2),
            std::clamp(bottomY, height, screenHeight), textTop, lineHeight};
    }
}
