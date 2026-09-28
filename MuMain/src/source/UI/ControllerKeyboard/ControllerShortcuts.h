#pragma once

#include "Core/Input/GamepadMapper.h"

namespace UI::Controller
{
    class ControllerShortcuts
    {
    public:
        static ControllerShortcuts& Instance();
        bool Update(const Core::Input::GamepadFrameState& frame,
            Core::Input::InputContext context, double nowMs, bool inputAvailable);
        void Render();
        void Reset();

    private:
        void Activate(double nowMs);
        void EmitModifiers() const;
        bool m_visible = false;
        bool m_control = false;
        bool m_shift = false;
        bool m_alt = false;
        int m_selected = 0;
        int m_direction = 0;
        int m_emittedKey = 0;
        double m_nextNavigationMs = 0.0;
        double m_releaseAtMs = 0.0;
    };
}
