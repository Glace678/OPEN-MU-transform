#pragma once
namespace SEASON3B
{
    inline bool CheckMouseIn(int x, int y, int width, int height)
    {
        return MouseX >= x && MouseX < x + width && MouseY >= y && MouseY < y + height;
    }
    inline bool IsPress(int) { return false; }
}
