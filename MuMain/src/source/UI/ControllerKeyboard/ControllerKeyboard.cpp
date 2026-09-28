#include "stdafx.h"
#include "UI/ControllerKeyboard/ControllerKeyboard.h"

#include "Core/Haptics/Haptics.h"
#include "Core/Input/GamepadMapper.h"
#include "Core/Input/GamepadService.h"
#include "Core/Input/KeyState.h"
#include "I18N/All.h"
#include "Render/Textures/ZzzOpenglUtil.h"
#include "Scenes/SceneCore.h"
#include "UI/Legacy/UIControls.h"

#include <algorithm>
#include <cmath>
#include <cstring>
#include <cwctype>

namespace UI::Controller
{
    namespace
    {
        constexpr float PanelTop = 274.0f;
        constexpr float PanelHeight = 202.0f;
        constexpr float KeyWidth = 38.0f;
        constexpr float KeyHeight = 24.0f;
        constexpr float KeyGap = 4.0f;
        constexpr float CandidateHeight = 28.0f;

        const Core::Input::InputActionState& Action(
            const Core::Input::GamepadFrameState& frame,
            Core::Input::InputAction action)
        {
            return frame.actions[static_cast<std::size_t>(action)];
        }

        void DrawRectangle(float x, float y, float width, float height, float r, float g, float b, float a)
        {
            EnableAlphaTestRaw();
            glColor4f(r, g, b, a);
            RenderColor(x, y, width, height);
            EndRenderColor();
            glColor4f(1.0f, 1.0f, 1.0f, 1.0f);
        }

        void DrawSelectionBorder(float x, float y, float width, float height)
        {
            constexpr float Border = 2.0f;
            DrawRectangle(x, y, width, Border, 0.95f, 0.68f, 0.16f, 1.0f);
            DrawRectangle(x, y + height - Border, width, Border, 0.95f, 0.68f, 0.16f, 1.0f);
            DrawRectangle(x, y, Border, height, 0.95f, 0.68f, 0.16f, 1.0f);
            DrawRectangle(x + width - Border, y, Border, height, 0.95f, 0.68f, 0.16f, 1.0f);
        }
    }

    ControllerKeyboard& ControllerKeyboard::Instance()
    {
        static ControllerKeyboard instance;
        return instance;
    }

    bool ControllerKeyboard::Update(
        const Core::Input::GamepadFrameState& frame,
        CUITextInputBox* focusedField,
        double nowMs,
        Core::Input::GamepadService& gamepad,
        bool inputAvailable)
    {
        if (!inputAvailable || focusedField == nullptr)
        {
            Reset();
            return false;
        }

        if (focusedField != m_field)
        {
            Reset();
            m_field = focusedField;
        }

        if (!m_active)
        {
            if (!HasControllerActivity(frame))
                return false;
            BeginForField(focusedField);
            gamepad.PublishHaptic(Core::Haptics::HapticEvent::Confirmed, nowMs);
            return true;
        }

        m_shiftDown = Action(frame, Core::Input::InputAction::LockTarget).down;
        if (const int navigation = ResolveNavigation(frame, nowMs); navigation != 0)
        {
            MoveSelection(navigation);
            gamepad.PublishHaptic(Core::Haptics::HapticEvent::FocusMoved, nowMs);
        }

        if (Action(frame, Core::Input::InputAction::Confirm).pressed)
            ActivateSelection(gamepad, nowMs);
        if (Action(frame, Core::Input::InputAction::PrimaryAttack).pressed)
            Backspace(gamepad, nowMs);
        if (Action(frame, Core::Input::InputAction::ContextAction).pressed)
            InsertSpace(gamepad, nowMs);
        if (Action(frame, Core::Input::InputAction::NextTarget).pressed)
            ToggleInputMode(gamepad, nowMs);

        if (Action(frame, Core::Input::InputAction::PreviousPage).pressed && m_chineseMode)
        {
            if (m_pinyin.ChangePage(true))
            {
                m_selectedRow = -1;
                m_selectedColumn = 0;
                gamepad.PublishHaptic(Core::Haptics::HapticEvent::SettingChanged, nowMs);
            }
        }
        if (Action(frame, Core::Input::InputAction::NextPage).pressed && m_chineseMode)
        {
            if (m_pinyin.ChangePage(false))
            {
                m_selectedRow = -1;
                m_selectedColumn = 0;
                gamepad.PublishHaptic(Core::Haptics::HapticEvent::SettingChanged, nowMs);
            }
        }

        if (Action(frame, Core::Input::InputAction::Map).pressed)
        {
            m_pinyin.Clear();
            m_field->OnEditKey(VK_TAB, false, false);
            m_active = false;
            gamepad.RequireNeutralInput();
            gamepad.PublishHaptic(Core::Haptics::HapticEvent::SettingChanged, nowMs);
            return true;
        }

        if (Action(frame, Core::Input::InputAction::Menu).pressed)
        {
            if (m_chineseMode && !m_pinyin.GetComposition().preedit.empty())
            {
                const int candidate = std::clamp(
                    m_pinyin.GetComposition().highlightedCandidate,
                    0,
                    std::max(0, static_cast<int>(m_pinyin.GetComposition().candidates.size()) - 1));
                m_pinyin.SelectCandidate(candidate);
                DrainPinyinCommit();
            }
            m_field->OnEditKey(VK_RETURN, false, false);
            Core::Input::SetVirtualKeyDown(VK_RETURN, true);
            SetEnterPressed(true);
            m_active = false;
            gamepad.RequireNeutralInput();
            gamepad.PublishHaptic(Core::Haptics::HapticEvent::Confirmed, nowMs);
            return true;
        }

        if (Action(frame, Core::Input::InputAction::Cancel).pressed)
        {
            if (m_chineseMode && !m_pinyin.GetComposition().preedit.empty())
            {
                m_pinyin.Clear();
                m_selectedRow = 1;
                m_selectedColumn = 0;
                gamepad.PublishHaptic(Core::Haptics::HapticEvent::Cancelled, nowMs);
                return true;
            }

            m_active = false;
            CUITextInputBox::ReleaseFocus();
            gamepad.RequireNeutralInput();
            gamepad.PublishHaptic(Core::Haptics::HapticEvent::Cancelled, nowMs);
            return true;
        }

        return true;
    }

    void ControllerKeyboard::Reset()
    {
        m_active = false;
        m_field = nullptr;
        m_selectedRow = 1;
        m_selectedColumn = 0;
        m_navigationDirection = 0;
        m_nextNavigationMs = 0.0;
        m_chineseAllowed = false;
        m_chineseMode = false;
        m_shiftDown = false;
        m_pinyin.Clear();
    }

    void ControllerKeyboard::Shutdown()
    {
        Reset();
        m_pinyin.Shutdown();
    }

    bool ControllerKeyboard::HasControllerActivity(const Core::Input::GamepadFrameState& frame) const
    {
        if (std::abs(frame.moveX) > 0.35f || std::abs(frame.moveY) > 0.35f)
            return true;
        for (const auto& action : frame.actions)
        {
            if (action.pressed)
                return true;
        }
        return false;
    }

    int ControllerKeyboard::ResolveNavigation(
        const Core::Input::GamepadFrameState& frame,
        double nowMs)
    {
        constexpr float Threshold = 0.55f;
        int direction = 0;
        if (Action(frame, Core::Input::InputAction::QuickItem1).down)
            direction = 1;
        else if (Action(frame, Core::Input::InputAction::QuickItem3).down)
            direction = 2;
        else if (Action(frame, Core::Input::InputAction::QuickItem2).down)
            direction = 3;
        else if (Action(frame, Core::Input::InputAction::QuickItem4).down)
            direction = 4;
        else if (std::abs(frame.moveX) >= std::abs(frame.moveY) && frame.moveX < -Threshold)
            direction = 1;
        else if (std::abs(frame.moveX) >= std::abs(frame.moveY) && frame.moveX > Threshold)
            direction = 2;
        else if (frame.moveY < -Threshold)
            direction = 3;
        else if (frame.moveY > Threshold)
            direction = 4;

        if (direction == 0)
        {
            m_navigationDirection = 0;
            m_nextNavigationMs = 0.0;
            return 0;
        }
        if (direction != m_navigationDirection)
        {
            m_navigationDirection = direction;
            m_nextNavigationMs = nowMs + 340.0;
            return direction;
        }
        if (nowMs >= m_nextNavigationMs)
        {
            m_nextNavigationMs = nowMs + 110.0;
            return direction;
        }
        return 0;
    }

    void ControllerKeyboard::BeginForField(CUITextInputBox* field)
    {
        m_field = field;
        m_active = true;
        m_selectedRow = field->CheckOption(UIOPTION_NUMBERONLY) ? 0 : 1;
        m_selectedColumn = 0;
        m_navigationDirection = 0;
        const char* locale = I18N::GetCurrentLocale();
        m_chineseAllowed = SceneFlag == MAIN_SCENE
            && !field->IsPassword()
            && !field->CheckOption(UIOPTION_NUMBERONLY)
            && !field->CheckOption(UIOPTION_SERIALNUMBER)
            && !field->CheckOption(UIOPTION_NOLOCALIZEDCHARACTERS)
            && locale != nullptr
            && std::strcmp(locale, "zh-CN") == 0;
        m_chineseMode = m_chineseAllowed && m_pinyin.Initialize();
    }

    void ControllerKeyboard::MoveSelection(int direction)
    {
        const auto& candidates = m_pinyin.GetComposition().candidates;
        if (direction == 1 || direction == 2)
        {
            const int count = m_selectedRow < 0
                ? static_cast<int>(candidates.size())
                : static_cast<int>(Rows[static_cast<std::size_t>(m_selectedRow)].size());
            if (count <= 0)
                return;
            m_selectedColumn = (m_selectedColumn + (direction == 1 ? -1 : 1) + count) % count;
            return;
        }

        if (direction == 3)
        {
            if (m_selectedRow == 0 && !candidates.empty())
                m_selectedRow = -1;
            else if (m_selectedRow > 0)
                --m_selectedRow;
        }
        else if (direction == 4)
        {
            if (m_selectedRow < 0)
                m_selectedRow = 0;
            else if (m_selectedRow + 1 < static_cast<int>(Rows.size()))
                ++m_selectedRow;
        }

        const int count = m_selectedRow < 0
            ? static_cast<int>(candidates.size())
            : static_cast<int>(Rows[static_cast<std::size_t>(m_selectedRow)].size());
        m_selectedColumn = std::clamp(m_selectedColumn, 0, std::max(0, count - 1));
    }

    void ControllerKeyboard::ActivateSelection(Core::Input::GamepadService& gamepad, double nowMs)
    {
        if (m_field == nullptr)
            return;
        if (m_selectedRow < 0)
        {
            if (m_pinyin.SelectCandidate(m_selectedColumn))
            {
                DrainPinyinCommit();
                m_selectedRow = 1;
                m_selectedColumn = 0;
                gamepad.PublishHaptic(Core::Haptics::HapticEvent::Confirmed, nowMs);
            }
            return;
        }

        wchar_t character = Rows[static_cast<std::size_t>(m_selectedRow)][static_cast<std::size_t>(m_selectedColumn)];
        if (m_shiftDown && character >= L'a' && character <= L'z')
            character = static_cast<wchar_t>(std::towupper(character));

        bool handled = false;
        if (m_chineseMode && character >= L'a' && character <= L'z')
        {
            handled = m_pinyin.ProcessLetter(static_cast<char>(character));
            DrainPinyinCommit();
        }
        else
        {
            const wchar_t text[] = { character, L'\0' };
            m_field->OnTextInput(text);
            handled = true;
        }

        gamepad.PublishHaptic(
            handled ? Core::Haptics::HapticEvent::Confirmed : Core::Haptics::HapticEvent::OperationFailed,
            nowMs);
    }

    void ControllerKeyboard::Backspace(Core::Input::GamepadService& gamepad, double nowMs)
    {
        bool handled = false;
        if (m_chineseMode && !m_pinyin.GetComposition().preedit.empty())
        {
            handled = m_pinyin.Backspace();
            DrainPinyinCommit();
            if (m_pinyin.GetComposition().candidates.empty() && m_selectedRow < 0)
            {
                m_selectedRow = 1;
                m_selectedColumn = 0;
            }
        }
        else if (m_field != nullptr)
        {
            m_field->OnEditKey(VK_BACK, false, false);
            handled = true;
        }
        gamepad.PublishHaptic(
            handled ? Core::Haptics::HapticEvent::Cancelled : Core::Haptics::HapticEvent::OperationFailed,
            nowMs);
    }

    void ControllerKeyboard::InsertSpace(Core::Input::GamepadService& gamepad, double nowMs)
    {
        if (m_field == nullptr)
            return;
        if (m_chineseMode && !m_pinyin.GetComposition().candidates.empty())
        {
            const int candidate = std::clamp(
                m_pinyin.GetComposition().highlightedCandidate,
                0,
                static_cast<int>(m_pinyin.GetComposition().candidates.size()) - 1);
            m_pinyin.SelectCandidate(candidate);
            DrainPinyinCommit();
            m_selectedRow = 1;
            m_selectedColumn = 0;
        }
        else
        {
            m_field->OnTextInput(L" ");
        }
        gamepad.PublishHaptic(Core::Haptics::HapticEvent::Confirmed, nowMs);
    }

    void ControllerKeyboard::ToggleInputMode(Core::Input::GamepadService& gamepad, double nowMs)
    {
        if (!m_chineseAllowed)
        {
            gamepad.PublishHaptic(Core::Haptics::HapticEvent::OperationFailed, nowMs);
            return;
        }
        if (!m_pinyin.IsAvailable() && !m_pinyin.Initialize())
        {
            gamepad.PublishHaptic(Core::Haptics::HapticEvent::OperationFailed, nowMs);
            return;
        }
        m_pinyin.Clear();
        m_chineseMode = !m_chineseMode;
        m_selectedRow = 1;
        m_selectedColumn = 0;
        gamepad.PublishHaptic(Core::Haptics::HapticEvent::SettingChanged, nowMs);
    }

    void ControllerKeyboard::DrainPinyinCommit()
    {
        if (m_field == nullptr)
            return;
        const std::wstring committed = m_pinyin.TakeCommittedText();
        if (!committed.empty())
            m_field->OnTextInput(committed.c_str());
    }

    void ControllerKeyboard::Render()
    {
        if (!IsVisible())
            return;
        BeginBitmap();
        DrawPanel();
        EndBitmap();
    }

    void ControllerKeyboard::DrawPanel()
    {
        DrawRectangle(0.0f, PanelTop, static_cast<float>(REFERENCE_WIDTH), PanelHeight, 0.035f, 0.04f, 0.05f, 0.94f);
        DrawRectangle(0.0f, PanelTop, static_cast<float>(REFERENCE_WIDTH), 2.0f, 0.42f, 0.46f, 0.50f, 1.0f);

        g_pRenderText->SetFont(g_hFixFont);
        g_pRenderText->SetBgColor(0);
        g_pRenderText->SetTextColor(230, 235, 240, 255);

        const auto& composition = m_pinyin.GetComposition();
        const wchar_t* mode = m_chineseMode ? L"拼音" : L"ABC";
        g_pRenderText->RenderText(12, static_cast<int>(PanelTop + 8.0f), mode, 52, 0, RT3_SORT_LEFT);
        if (!composition.preedit.empty())
            g_pRenderText->RenderText(70, static_cast<int>(PanelTop + 8.0f), composition.preedit.c_str(), 280, 0, RT3_SORT_LEFT_CLIP);
        if (m_chineseAllowed && !m_pinyin.IsAvailable())
        {
            g_pRenderText->SetTextColor(235, 105, 85, 255);
            g_pRenderText->RenderText(360, static_cast<int>(PanelTop + 8.0f), L"拼音引擎不可用", 160, 0, RT3_SORT_LEFT);
        }

        g_pRenderText->SetTextColor(170, 180, 190, 255);
        g_pRenderText->RenderText(512, static_cast<int>(PanelTop + 8.0f), L"Menu", 48, 0, RT3_SORT_CENTER);
        g_pRenderText->RenderText(
            562,
            static_cast<int>(PanelTop + 8.0f),
            m_chineseAllowed ? L"完成" : L"Done",
            42,
            0,
            RT3_SORT_CENTER);
        DrawCandidates(PanelTop + 29.0f);
        DrawKeys(PanelTop + 61.0f);
    }

    void ControllerKeyboard::DrawCandidates(float top)
    {
        const auto& composition = m_pinyin.GetComposition();
        const int count = static_cast<int>(composition.candidates.size());
        const float width = 112.0f;
        const float total = count * width + std::max(0, count - 1) * KeyGap;
        float x = (REFERENCE_WIDTH - total) * 0.5f;
        for (int i = 0; i < count; ++i)
        {
            const bool selected = m_selectedRow < 0 && m_selectedColumn == i;
            DrawRectangle(x, top, width, CandidateHeight, selected ? 0.18f : 0.09f, selected ? 0.20f : 0.10f, selected ? 0.22f : 0.11f, 1.0f);
            if (selected)
                DrawSelectionBorder(x, top, width, CandidateHeight);
            g_pRenderText->SetTextColor(240, 240, 240, 255);
            g_pRenderText->RenderText(
                static_cast<int>(x),
                static_cast<int>(top + 7.0f),
                composition.candidates[static_cast<std::size_t>(i)].c_str(),
                static_cast<int>(width),
                0,
                RT3_SORT_CENTER);
            x += width + KeyGap;
        }
    }

    void ControllerKeyboard::DrawKeys(float top)
    {
        for (int row = 0; row < static_cast<int>(Rows.size()); ++row)
        {
            const auto keys = Rows[static_cast<std::size_t>(row)];
            const float total = keys.size() * KeyWidth + (keys.size() - 1) * KeyGap;
            float x = (REFERENCE_WIDTH - total) * 0.5f;
            const float y = top + row * (KeyHeight + KeyGap);
            for (int column = 0; column < static_cast<int>(keys.size()); ++column)
            {
                const bool selected = m_selectedRow == row && m_selectedColumn == column;
                DrawRectangle(x, y, KeyWidth, KeyHeight, selected ? 0.19f : 0.08f, selected ? 0.21f : 0.09f, selected ? 0.23f : 0.10f, 1.0f);
                if (selected)
                    DrawSelectionBorder(x, y, KeyWidth, KeyHeight);

                wchar_t key[] = { keys[static_cast<std::size_t>(column)], L'\0' };
                if (m_shiftDown && key[0] >= L'a' && key[0] <= L'z')
                    key[0] = static_cast<wchar_t>(std::towupper(key[0]));
                g_pRenderText->SetTextColor(235, 235, 235, 255);
                g_pRenderText->RenderText(
                    static_cast<int>(x),
                    static_cast<int>(y + 6.0f),
                    key,
                    static_cast<int>(KeyWidth),
                    0,
                    RT3_SORT_CENTER);
                x += KeyWidth + KeyGap;
            }
        }
    }
}
