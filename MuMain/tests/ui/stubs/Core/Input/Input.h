#pragma once

#include "Core/Platform/WinCompat.h"

class CInput
{
public:
    static CInput& Instance()
    {
        static CInput input;
        return input;
    }
    POINT GetCursorPos() const { return m_cursor; }
    LONG GetCursorX() const { return m_cursor.x; }
    LONG GetCursorY() const { return m_cursor.y; }
    bool IsLBtnDn() const { return false; }
    bool IsLBtnUp() const { return false; }
    void SetCursorDevice(LONG x, LONG y) { m_cursor = {x, y}; }

private:
    POINT m_cursor = {};
};
