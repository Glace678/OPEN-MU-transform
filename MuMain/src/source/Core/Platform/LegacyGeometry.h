#pragma once

#include "Core/Platform/WinCompat.h"

namespace Core::Platform
{
    template <typename X, typename Y>
    inline POINT MakePoint(X x, Y y)
    {
        return { static_cast<LONG>(x), static_cast<LONG>(y) };
    }

    template <typename Width, typename Height>
    inline SIZE MakeSize(Width width, Height height)
    {
        return { static_cast<LONG>(width), static_cast<LONG>(height) };
    }

    template <typename Left, typename Top, typename Right, typename Bottom>
    inline RECT MakeRect(Left left, Top top, Right right, Bottom bottom)
    {
        return { static_cast<LONG>(left), static_cast<LONG>(top),
            static_cast<LONG>(right), static_cast<LONG>(bottom) };
    }
}
