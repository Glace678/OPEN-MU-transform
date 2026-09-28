#pragma once

#include "Core/Input/RimePinyinEngine.h"

#include <array>
#include <string_view>

class CUITextInputBox;

namespace Core::Input
{
    struct GamepadFrameState;
    class GamepadService;
}

namespace UI::Controller
{
    class ControllerKeyboard
    {
    public:
        static ControllerKeyboard& Instance();

        // Returns true while the keyboard owns the controller for this frame.
        bool Update(
            const Core::Input::GamepadFrameState& frame,
            CUITextInputBox* focusedField,
            double nowMs,
            Core::Input::GamepadService& gamepad,
            bool inputAvailable);
        void Render();
        void Reset();
        void Shutdown();
        bool IsVisible() const { return m_active && m_field != nullptr; }

    private:
        ControllerKeyboard() = default;

        bool HasControllerActivity(const Core::Input::GamepadFrameState& frame) const;
        int ResolveNavigation(const Core::Input::GamepadFrameState& frame, double nowMs);
        void BeginForField(CUITextInputBox* field);
        void MoveSelection(int direction);
        void ActivateSelection(Core::Input::GamepadService& gamepad, double nowMs);
        void Backspace(Core::Input::GamepadService& gamepad, double nowMs);
        void InsertSpace(Core::Input::GamepadService& gamepad, double nowMs);
        void ToggleInputMode(Core::Input::GamepadService& gamepad, double nowMs);
        void DrainPinyinCommit();
        void DrawPanel();
        void DrawCandidates(float top);
        void DrawKeys(float top);

        static constexpr std::array<std::wstring_view, 5> Rows = {
            L"1234567890",
            L"qwertyuiop",
            L"asdfghjkl",
            L"zxcvbnm",
            L".-_@!?#$%+",
        };

        CUITextInputBox* m_field = nullptr;
        Core::Input::RimePinyinEngine m_pinyin;
        int m_selectedRow = 1;
        int m_selectedColumn = 0;
        int m_navigationDirection = 0;
        double m_nextNavigationMs = 0.0;
        bool m_active = false;
        bool m_chineseAllowed = false;
        bool m_chineseMode = false;
        bool m_shiftDown = false;
    };
}
