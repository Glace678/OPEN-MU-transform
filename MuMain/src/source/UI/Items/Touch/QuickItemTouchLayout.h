#pragma once

namespace UI::Items::Touch
{
    inline int QuickItemSlotAt(float x, float y)
    {
        constexpr float FirstCenterX = 20.0f;
        constexpr float CenterY = 455.0f;
        constexpr float SlotPitch = 38.0f;
        constexpr float HalfWidth = SlotPitch / 2.0f;
        constexpr float HalfHeight = 21.0f;
        constexpr int SlotCount = 4;
        if (y < CenterY - HalfHeight || y >= CenterY + HalfHeight) return -1;
        for (int slot = 0; slot < SlotCount; ++slot)
        {
            const float center = FirstCenterX + slot * SlotPitch;
            if (x >= center - HalfWidth && x < center + HalfWidth) return slot;
        }
        return -1;
    }
}
