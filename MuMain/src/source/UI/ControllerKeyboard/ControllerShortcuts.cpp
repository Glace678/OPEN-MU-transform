#include "stdafx.h"
#include "UI/ControllerKeyboard/ControllerShortcuts.h"
#include "Core/Input/GamepadService.h"
#include "Core/Input/KeyState.h"
#include "Render/Textures/ZzzOpenglUtil.h"
#include "Scenes/SceneCore.h"
#include "UI/Legacy/UIControls.h"

#include <array>
#include <cmath>

namespace UI::Controller
{
    namespace
    {
        constexpr int Columns = 10;
        constexpr int WheelUp = -1;
        constexpr int WheelDown = -2;
        constexpr double KeyPulseMs = 120.0;
        constexpr double NavigationRepeatMs = 150.0;
        constexpr float AxisThreshold = 0.55f;
        struct Shortcut { const wchar_t* label; int key; };
        constexpr auto Keys = std::to_array<Shortcut>({
            {L"F1", VK_F1}, {L"F2", VK_F2}, {L"F3", VK_F3}, {L"F4", VK_F4},
            {L"F5", VK_F5}, {L"F6", VK_F6}, {L"F7", VK_F7}, {L"F8", VK_F8},
            {L"F9", VK_F9}, {L"F10", VK_F10},
            {L"1", '1'}, {L"2", '2'}, {L"3", '3'}, {L"4", '4'}, {L"5", '5'},
            {L"6", '6'}, {L"7", '7'}, {L"8", '8'}, {L"9", '9'}, {L"0", '0'},
            {L"Q", 'Q'}, {L"W", 'W'}, {L"E", 'E'}, {L"R", 'R'}, {L"T", 'T'},
            {L"Y", 'Y'}, {L"U", 'U'}, {L"I", 'I'}, {L"O", 'O'}, {L"P", 'P'},
            {L"A", 'A'}, {L"S", 'S'}, {L"D", 'D'}, {L"F", 'F'}, {L"G", 'G'},
            {L"H", 'H'}, {L"J", 'J'}, {L"K", 'K'}, {L"L", 'L'}, {L"Enter", VK_RETURN},
            {L"Z", 'Z'}, {L"X", 'X'}, {L"C", 'C'}, {L"V", 'V'}, {L"B", 'B'},
            {L"N", 'N'}, {L"M", 'M'}, {L"Space", VK_SPACE}, {L"Tab", VK_TAB}, {L"Esc", VK_ESCAPE},
            {L"Ctrl", VK_CONTROL}, {L"Shift", VK_SHIFT}, {L"Alt", VK_MENU},
            {L"Back", VK_BACK}, {L"Del", VK_DELETE}, {L"Ins", VK_INSERT},
            {L"Home", VK_HOME}, {L"End", VK_END}, {L"PgUp", VK_PRIOR}, {L"PgDn", VK_NEXT},
            {L"Left", VK_LEFT}, {L"Up", VK_UP}, {L"Right", VK_RIGHT}, {L"Down", VK_DOWN},
            {L"F11", VK_F11}, {L"F12", VK_F12}, {L"Scroll+", WheelUp}, {L"Scroll-", WheelDown},
            {L"Num+", VK_ADD}, {L"Num-", VK_SUBTRACT},
            {L"Num0", VK_NUMPAD0}, {L"Num1", VK_NUMPAD1}, {L"Num2", VK_NUMPAD2}, {L"Num3", VK_NUMPAD3},
            {L"Num4", VK_NUMPAD4}, {L"Num5", VK_NUMPAD5}, {L"Num6", VK_NUMPAD6}, {L"Num7", VK_NUMPAD7},
            {L"Num8", VK_NUMPAD8}, {L"Num9", VK_NUMPAD9}
        });

        const Core::Input::InputActionState& Action(const Core::Input::GamepadFrameState& frame, Core::Input::InputAction action)
        {
            return frame.actions[static_cast<std::size_t>(action)];
        }
    }

    ControllerShortcuts& ControllerShortcuts::Instance()
    {
        static ControllerShortcuts instance;
        return instance;
    }

    void ControllerShortcuts::Reset()
    {
        m_visible = false;
        m_control = m_shift = m_alt = false;
        m_emittedKey = 0;
        m_direction = 0;
    }

    void ControllerShortcuts::EmitModifiers() const
    {
        Core::Input::SetVirtualKeyDown(VK_CONTROL, m_control);
        Core::Input::SetVirtualKeyDown(VK_SHIFT, m_shift);
        Core::Input::SetVirtualKeyDown(VK_MENU, m_alt);
    }

    bool ControllerShortcuts::Update(const Core::Input::GamepadFrameState& frame,
        Core::Input::InputContext context, double nowMs, bool inputAvailable)
    {
        using Core::Input::InputAction;
        if (!inputAvailable || context == Core::Input::InputContext::TextEntry
            || context == Core::Input::InputContext::Editor)
        {
            Reset();
            return false;
        }
        if (m_emittedKey != 0 && nowMs < m_releaseAtMs)
        {
            EmitModifiers();
            Core::Input::SetVirtualKeyDown(m_emittedKey, true);
            return true;
        }
        m_emittedKey = 0;

        const bool openChord = Action(frame, InputAction::Map).down && Action(frame, InputAction::Menu).pressed;
        const bool worldShortcut = context == Core::Input::InputContext::World && Action(frame, InputAction::ContextAction).pressed;
        if (!m_visible && (openChord || worldShortcut))
        {
            m_visible = true;
            return true;
        }
        if (!m_visible)
        {
            EmitModifiers();
            return false;
        }
        if (Action(frame, InputAction::Cancel).pressed)
        {
            Reset();
            Core::Input::GamepadService::Instance().RequireNeutralInput();
            return true;
        }
        int direction = 0;
        if (Action(frame, InputAction::QuickItem1).down || frame.moveX < -AxisThreshold) direction = -1;
        else if (Action(frame, InputAction::QuickItem3).down || frame.moveX > AxisThreshold) direction = 1;
        else if (Action(frame, InputAction::QuickItem2).down || frame.moveY < -AxisThreshold) direction = -Columns;
        else if (Action(frame, InputAction::QuickItem4).down || frame.moveY > AxisThreshold) direction = Columns;
        if (direction != 0 && (direction != m_direction || nowMs >= m_nextNavigationMs))
        {
            m_selected = (m_selected + direction + static_cast<int>(Keys.size())) % static_cast<int>(Keys.size());
            m_nextNavigationMs = nowMs + NavigationRepeatMs;
            Core::Input::GamepadService::Instance().PublishHaptic(Core::Haptics::HapticEvent::FocusMoved, nowMs);
        }
        m_direction = direction;
        if (Action(frame, InputAction::Confirm).pressed)
            Activate(nowMs);
        return true;
    }

    void ControllerShortcuts::Activate(double nowMs)
    {
        const int key = Keys[m_selected].key;
        if (key == VK_CONTROL) m_control = !m_control;
        else if (key == VK_SHIFT) m_shift = !m_shift;
        else if (key == VK_MENU) m_alt = !m_alt;
        else
        {
            m_visible = false;
            if (key == WheelUp || key == WheelDown)
                MouseWheel = key == WheelUp ? 1 : -1;
            else
            {
                m_emittedKey = key;
                m_releaseAtMs = nowMs + KeyPulseMs;
                EmitModifiers();
                Core::Input::SetVirtualKeyDown(key, true);
                if (key == VK_RETURN)
                    SetEnterPressed(true);
            }
        }
        Core::Input::GamepadService::Instance().PublishHaptic(Core::Haptics::HapticEvent::Confirmed, nowMs);
    }

    void ControllerShortcuts::Render()
    {
        if (!m_visible)
        {
            if (m_control || m_shift || m_alt)
            {
                g_pRenderText->SetFont(g_hFixFont);
                g_pRenderText->SetBgColor(0, 0, 0, 190);
                g_pRenderText->SetTextColor(255, 225, 120, 255);
                wchar_t label[48]{};
                mu_swprintf(label, L"%ls%ls%ls", m_control ? L"Ctrl " : L"",
                    m_shift ? L"Shift " : L"", m_alt ? L"Alt " : L"");
                g_pRenderText->RenderText(20, 96, label);
                g_pRenderText->SetBgColor(0);
            }
            return;
        }
        constexpr float Left = 20.0f;
        constexpr float Top = 112.0f;
        constexpr float Width = 60.0f;
        constexpr float Height = 32.0f;
        EnableAlphaTestRaw();
        g_pRenderText->SetFont(g_hFixFont);
        g_pRenderText->SetBgColor(0);
        for (int i = 0; i < static_cast<int>(Keys.size()); ++i)
        {
            const auto& entry = Keys[i];
            const bool toggled = (entry.key == VK_CONTROL && m_control)
                || (entry.key == VK_SHIFT && m_shift) || (entry.key == VK_MENU && m_alt);
            const float x = Left + (i % Columns) * Width;
            const float y = Top + (i / Columns) * Height;
            glColor4f(i == m_selected ? 0.38f : 0.06f, toggled ? 0.40f : 0.08f, 0.10f, 0.97f);
            RenderColor(x, y, Width - 2, Height - 2);
            EndRenderColor();
            g_pRenderText->SetTextColor(235, 238, 240, 255);
            g_pRenderText->RenderText(static_cast<int>(x), static_cast<int>(y + 9),
                entry.label, static_cast<int>(Width - 2), 0, RT3_SORT_CENTER);
        }
        glColor4f(1, 1, 1, 1);
    }
}
