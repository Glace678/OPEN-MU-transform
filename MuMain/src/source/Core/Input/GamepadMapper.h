#pragma once

#include "Core/Input/GamepadTypes.h"

#include <array>

namespace Core::Input
{
    struct InputActionState
    {
        bool down = false;
        bool pressed = false;
        bool released = false;
        float value = 0.0f;
    };

    struct GamepadFrameState
    {
        PointerState pointer;
        float moveX = 0.0f;
        float moveY = 0.0f;
        std::array<InputActionState, static_cast<std::size_t>(InputAction::Count)> actions{};
    };

    class GamepadMapper
    {
    public:
        explicit GamepadMapper(GamepadSettings settings = {});

        GamepadFrameState Update(
            const GamepadSnapshot& snapshot,
            InputContext context,
            double deltaSeconds,
            float pointerWidth,
            float pointerHeight,
            bool acceptInput);

        void SetSettings(const GamepadSettings& settings) { m_settings = settings; }
        const GamepadSettings& GetSettings() const { return m_settings; }
        void Reset(float pointerX, float pointerY);
        void SetPointerPosition(float pointerX, float pointerY);

    private:
        struct ProcessedAxes
        {
            float leftX{};
            float leftY{};
            float pointerX{};
            float pointerY{};
        };

        GamepadFrameState BeginFrame() const;
        bool ReleaseInputOwnership(
            GamepadFrameState& frame,
            const GamepadSnapshot& snapshot,
            bool acceptInput);
        bool ConsumeNeutralSample(const GamepadSnapshot& snapshot);
        ProcessedAxes ProcessAxes(const GamepadSnapshot& snapshot, InputContext context) const;
        void MovePointer(
            GamepadFrameState& frame,
            const ProcessedAxes& axes,
            double deltaSeconds,
            float pointerWidth,
            float pointerHeight) const;
        void PopulateDigitalActions(GamepadFrameState& frame, const GamepadSnapshot& snapshot) const;
        void PopulateAnalogActions(GamepadFrameState& frame, const ProcessedAxes& axes) const;
        float ApplyDeadZone(float value, float deadZone) const;
        float ApplyTriggerDeadZone(float value) const;
        bool ButtonDown(const GamepadSnapshot& snapshot, GamepadButton button) const;
        bool ControlDown(const GamepadSnapshot& snapshot, GamepadControl control) const;
        bool ActionDown(const GamepadSnapshot& snapshot, RemappableGamepadAction action) const;
        void SetDigitalAction(GamepadFrameState& frame, InputAction action, bool down, bool previousDown) const;

        GamepadSettings m_settings;
        GamepadSnapshot m_previous;
        PointerState m_pointer;
        bool m_requireNeutral = true;
    };
}
