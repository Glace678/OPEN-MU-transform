#include "Core/Input/GamepadMapper.h"

#include <algorithm>
#include <array>
#include <cmath>
#include <utility>

namespace Core::Input
{
    namespace
    {
        constexpr float TriggerPressed = 0.5f;
        constexpr float NeutralThreshold = 0.25f;
        constexpr double MaximumPointerDeltaSeconds = 0.1;

        constexpr auto DigitalActionMappings = std::to_array<std::pair<InputAction, RemappableGamepadAction>>({
            {InputAction::Confirm, RemappableGamepadAction::Confirm},
            {InputAction::Cancel, RemappableGamepadAction::Cancel},
            {InputAction::PrimaryAttack, RemappableGamepadAction::PrimaryAttack},
            {InputAction::ContextAction, RemappableGamepadAction::ContextAction},
            {InputAction::SecondaryAction, RemappableGamepadAction::PrimaryAttack},
            {InputAction::Details, RemappableGamepadAction::ContextAction},
            {InputAction::PreviousPage, RemappableGamepadAction::PreviousPage},
            {InputAction::NextPage, RemappableGamepadAction::NextPage},
            {InputAction::QuickItem1, RemappableGamepadAction::QuickItem1},
            {InputAction::QuickItem2, RemappableGamepadAction::QuickItem2},
            {InputAction::QuickItem3, RemappableGamepadAction::QuickItem3},
            {InputAction::QuickItem4, RemappableGamepadAction::QuickItem4},
            {InputAction::Map, RemappableGamepadAction::Map},
            {InputAction::Menu, RemappableGamepadAction::Menu},
            {InputAction::AutoMove, RemappableGamepadAction::AutoMove},
            {InputAction::NextTarget, RemappableGamepadAction::NextTarget},
            {InputAction::UseSkill, RemappableGamepadAction::UseSkill},
            {InputAction::LockTarget, RemappableGamepadAction::LockTarget},
        });

        std::size_t Index(InputAction action)
        {
            return static_cast<std::size_t>(action);
        }
    }

    GamepadMapper::GamepadMapper(GamepadSettings settings)
        : m_settings(settings)
    {
    }

    GamepadFrameState GamepadMapper::Update(
        const GamepadSnapshot& snapshot,
        InputContext context,
        double deltaSeconds,
        float pointerWidth,
        float pointerHeight,
        bool acceptInput)
    {
        auto frame = BeginFrame();
        if (ReleaseInputOwnership(frame, snapshot, acceptInput))
            return frame;
        if (ConsumeNeutralSample(snapshot))
            return frame;

        const auto axes = ProcessAxes(snapshot, context);
        MovePointer(frame, axes, deltaSeconds, pointerWidth, pointerHeight);
        PopulateDigitalActions(frame, snapshot);
        PopulateAnalogActions(frame, axes);

        m_pointer = frame.pointer;
        m_previous = snapshot;
        return frame;
    }

    GamepadFrameState GamepadMapper::BeginFrame() const
    {
        GamepadFrameState frame;
        frame.pointer = m_pointer;
        frame.pointer.leftPressed = false;
        frame.pointer.leftReleased = false;
        frame.pointer.rightPressed = false;
        frame.pointer.rightReleased = false;
        return frame;
    }

    bool GamepadMapper::ReleaseInputOwnership(
        GamepadFrameState& frame,
        const GamepadSnapshot& snapshot,
        bool acceptInput)
    {
        if (m_settings.enabled && snapshot.connected && acceptInput)
            return false;

        if (m_previous.connected)
        {
            frame.pointer.leftReleased = m_pointer.leftDown;
            frame.pointer.rightReleased = m_pointer.rightDown;
        }
        frame.pointer.leftDown = false;
        frame.pointer.rightDown = false;
        m_pointer = frame.pointer;
        m_previous = {};
        m_requireNeutral = true;
        return true;
    }

    bool GamepadMapper::ConsumeNeutralSample(const GamepadSnapshot& snapshot)
    {
        if (!m_requireNeutral)
            return false;

        const float maxAxis = std::max({
            std::abs(snapshot.leftX), std::abs(snapshot.leftY),
            std::abs(snapshot.rightX), std::abs(snapshot.rightY),
            ApplyTriggerDeadZone(snapshot.leftTrigger),
            ApplyTriggerDeadZone(snapshot.rightTrigger) });
        m_requireNeutral = maxAxis > NeutralThreshold
            || std::any_of(snapshot.buttons.begin(), snapshot.buttons.end(), [](bool down) { return down; });

        // Consume the first fully neutral sample as part of the ownership
        // handoff too, so hidden controls cannot synthesize release edges.
        m_previous = snapshot;
        return true;
    }

    GamepadMapper::ProcessedAxes GamepadMapper::ProcessAxes(
        const GamepadSnapshot& snapshot,
        InputContext context) const
    {
        ProcessedAxes axes;
        axes.leftX = ApplyDeadZone(snapshot.leftX, m_settings.stickDeadZone);
        axes.leftY = ApplyDeadZone(snapshot.leftY, m_settings.stickDeadZone);
        axes.pointerX = ApplyDeadZone(snapshot.rightX, m_settings.stickDeadZone);
        axes.pointerY = ApplyDeadZone(snapshot.rightY, m_settings.stickDeadZone);
        if (m_settings.invertPointerY)
            axes.pointerY = -axes.pointerY;

        if (context == InputContext::World)
            return axes;

        const float pointerLeftY = m_settings.invertPointerY ? -axes.leftY : axes.leftY;
        if (std::abs(axes.leftX) > std::abs(axes.pointerX))
            axes.pointerX = axes.leftX;
        if (std::abs(pointerLeftY) > std::abs(axes.pointerY))
            axes.pointerY = pointerLeftY;

        const float dpadX = ButtonDown(snapshot, GamepadButton::DpadRight)
            - ButtonDown(snapshot, GamepadButton::DpadLeft);
        const float dpadY = ButtonDown(snapshot, GamepadButton::DpadDown)
            - ButtonDown(snapshot, GamepadButton::DpadUp);
        if (dpadX != 0.0f)
            axes.pointerX = dpadX;
        if (dpadY != 0.0f)
            axes.pointerY = dpadY;
        return axes;
    }

    void GamepadMapper::MovePointer(
        GamepadFrameState& frame,
        const ProcessedAxes& axes,
        double deltaSeconds,
        float pointerWidth,
        float pointerHeight) const
    {
        const float seconds = static_cast<float>(
            std::clamp(deltaSeconds, 0.0, MaximumPointerDeltaSeconds));
        frame.pointer.x = std::clamp(
            frame.pointer.x + axes.pointerX * m_settings.pointerSpeed * seconds,
            0.0f,
            std::max(0.0f, pointerWidth - 1.0f));
        frame.pointer.y = std::clamp(
            frame.pointer.y + axes.pointerY * m_settings.pointerSpeed * seconds,
            0.0f,
            std::max(0.0f, pointerHeight - 1.0f));
    }

    void GamepadMapper::PopulateDigitalActions(
        GamepadFrameState& frame,
        const GamepadSnapshot& snapshot) const
    {
        for (const auto& [inputAction, gamepadAction] : DigitalActionMappings)
        {
            SetDigitalAction(
                frame,
                inputAction,
                ActionDown(snapshot, gamepadAction),
                ActionDown(m_previous, gamepadAction));
        }

        const auto& confirm = frame.actions[Index(InputAction::Confirm)];
        const auto& skill = frame.actions[Index(InputAction::UseSkill)];
        frame.pointer.leftDown = confirm.down;
        frame.pointer.leftPressed = confirm.pressed;
        frame.pointer.leftReleased = confirm.released;
        frame.pointer.rightDown = skill.down;
        frame.pointer.rightPressed = skill.pressed;
        frame.pointer.rightReleased = skill.released;
    }

    void GamepadMapper::PopulateAnalogActions(
        GamepadFrameState& frame,
        const ProcessedAxes& axes) const
    {
        frame.moveX = axes.leftX;
        frame.moveY = axes.leftY;

        auto& move = frame.actions[Index(InputAction::Move)];
        move.value = std::hypot(axes.leftX, axes.leftY);
        move.down = move.value > 0.0f;

        auto& point = frame.actions[Index(InputAction::Point)];
        point.value = std::hypot(axes.pointerX, axes.pointerY);
        point.down = point.value > 0.0f;
    }

    void GamepadMapper::Reset(float pointerX, float pointerY)
    {
        m_pointer = {};
        m_pointer.x = pointerX;
        m_pointer.y = pointerY;
        m_previous = {};
        m_requireNeutral = true;
    }

    void GamepadMapper::SetPointerPosition(float pointerX, float pointerY)
    {
        m_pointer.x = pointerX;
        m_pointer.y = pointerY;
    }

    float GamepadMapper::ApplyDeadZone(float value, float deadZone) const
    {
        const float magnitude = std::abs(value);
        if (magnitude <= deadZone) return 0.0f;
        const float normalized = (magnitude - deadZone) / (1.0f - deadZone);
        return std::copysign(std::clamp(normalized, 0.0f, 1.0f), value);
    }

    float GamepadMapper::ApplyTriggerDeadZone(float value) const
    {
        const float deadZone = std::clamp(m_settings.triggerDeadZone, 0.0f, 0.95f);
        if (value <= deadZone)
            return 0.0f;
        return std::clamp((value - deadZone) / (1.0f - deadZone), 0.0f, 1.0f);
    }

    bool GamepadMapper::ButtonDown(const GamepadSnapshot& snapshot, GamepadButton button) const
    {
        return snapshot.connected && snapshot.buttons[static_cast<std::size_t>(button)];
    }

    bool GamepadMapper::ControlDown(const GamepadSnapshot& snapshot, GamepadControl control) const
    {
        switch (control)
        {
        case GamepadControl::South: return ButtonDown(snapshot, GamepadButton::South);
        case GamepadControl::East: return ButtonDown(snapshot, GamepadButton::East);
        case GamepadControl::West: return ButtonDown(snapshot, GamepadButton::West);
        case GamepadControl::North: return ButtonDown(snapshot, GamepadButton::North);
        case GamepadControl::Back: return ButtonDown(snapshot, GamepadButton::Back);
        case GamepadControl::Start: return ButtonDown(snapshot, GamepadButton::Start);
        case GamepadControl::LeftStick: return ButtonDown(snapshot, GamepadButton::LeftStick);
        case GamepadControl::RightStick: return ButtonDown(snapshot, GamepadButton::RightStick);
        case GamepadControl::LeftShoulder: return ButtonDown(snapshot, GamepadButton::LeftShoulder);
        case GamepadControl::RightShoulder: return ButtonDown(snapshot, GamepadButton::RightShoulder);
        case GamepadControl::DpadUp: return ButtonDown(snapshot, GamepadButton::DpadUp);
        case GamepadControl::DpadDown: return ButtonDown(snapshot, GamepadButton::DpadDown);
        case GamepadControl::DpadLeft: return ButtonDown(snapshot, GamepadButton::DpadLeft);
        case GamepadControl::DpadRight: return ButtonDown(snapshot, GamepadButton::DpadRight);
        case GamepadControl::LeftTrigger: return ApplyTriggerDeadZone(snapshot.leftTrigger) >= TriggerPressed;
        case GamepadControl::RightTrigger: return ApplyTriggerDeadZone(snapshot.rightTrigger) >= TriggerPressed;
        default: return false;
        }
    }

    bool GamepadMapper::ActionDown(
        const GamepadSnapshot& snapshot,
        RemappableGamepadAction action) const
    {
        const auto index = static_cast<std::size_t>(action);
        return index < m_settings.bindings.size()
            && ControlDown(snapshot, m_settings.bindings[index]);
    }

    void GamepadMapper::SetDigitalAction(
        GamepadFrameState& frame,
        InputAction action,
        bool down,
        bool previousDown) const
    {
        auto& state = frame.actions[Index(action)];
        state.down = down;
        state.pressed = down && !previousDown;
        state.released = !down && previousDown;
        state.value = down ? 1.0f : 0.0f;
    }
}
