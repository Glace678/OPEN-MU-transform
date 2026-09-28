#pragma once

#include <array>
#include <cstdint>
#include <optional>
#include <string>
#include <string_view>

namespace Core::Input
{
    enum class InputContext
    {
        Login,
        CharacterSelect,
        World,
        UserInterface,
        TextEntry,
        Editor,
    };

    enum class InputAction
    {
        Move,
        Point,
        Confirm,
        Cancel,
        PrimaryAttack,
        ContextAction,
        UseSkill,
        LockTarget,
        PreviousPage,
        NextPage,
        QuickItem1,
        QuickItem2,
        QuickItem3,
        QuickItem4,
        Map,
        Menu,
        AutoMove,
        NextTarget,
        SecondaryAction,
        Details,
        Count,
    };

    enum class GamepadButton : std::uint8_t
    {
        South,
        East,
        West,
        North,
        Back,
        Guide,
        Start,
        LeftStick,
        RightStick,
        LeftShoulder,
        RightShoulder,
        DpadUp,
        DpadDown,
        DpadLeft,
        DpadRight,
        Count,
    };

    enum class GamepadControl : std::uint8_t
    {
        South,
        East,
        West,
        North,
        Back,
        Start,
        LeftStick,
        RightStick,
        LeftShoulder,
        RightShoulder,
        DpadUp,
        DpadDown,
        DpadLeft,
        DpadRight,
        LeftTrigger,
        RightTrigger,
        Count,
    };

    enum class RemappableGamepadAction : std::uint8_t
    {
        Confirm,
        Cancel,
        PrimaryAttack,
        ContextAction,
        UseSkill,
        LockTarget,
        PreviousPage,
        NextPage,
        QuickItem1,
        QuickItem2,
        QuickItem3,
        QuickItem4,
        Map,
        Menu,
        AutoMove,
        NextTarget,
        Count,
    };

    using GamepadBindings = std::array<
        GamepadControl,
        static_cast<std::size_t>(RemappableGamepadAction::Count)>;

    constexpr GamepadBindings DefaultGamepadBindings()
    {
        return {
            GamepadControl::South,
            GamepadControl::East,
            GamepadControl::West,
            GamepadControl::North,
            GamepadControl::RightTrigger,
            GamepadControl::LeftTrigger,
            GamepadControl::LeftShoulder,
            GamepadControl::RightShoulder,
            GamepadControl::DpadLeft,
            GamepadControl::DpadUp,
            GamepadControl::DpadRight,
            GamepadControl::DpadDown,
            GamepadControl::Back,
            GamepadControl::Start,
            GamepadControl::LeftStick,
            GamepadControl::RightStick,
        };
    }

    const wchar_t* GamepadControlConfigName(GamepadControl control);
    std::optional<GamepadControl> ParseGamepadControl(std::wstring_view value);
    const wchar_t* GamepadBindingConfigKey(RemappableGamepadAction action);
    bool HasGamepadBindingConflicts(const GamepadBindings& bindings);
    void NormalizeGamepadBindings(GamepadBindings& bindings);
    bool RebindGamepadAction(
        GamepadBindings& bindings,
        RemappableGamepadAction action,
        GamepadControl control);

    struct GamepadSnapshot
    {
        bool connected = false;
        std::array<bool, static_cast<std::size_t>(GamepadButton::Count)> buttons{};
        float leftX = 0.0f;
        float leftY = 0.0f;
        float rightX = 0.0f;
        float rightY = 0.0f;
        float leftTrigger = 0.0f;
        float rightTrigger = 0.0f;
        std::uint64_t sequence = 0;
    };

    struct PointerState
    {
        float x = 320.0f;
        float y = 240.0f;
        bool leftDown = false;
        bool leftPressed = false;
        bool leftReleased = false;
        bool rightDown = false;
        bool rightPressed = false;
        bool rightReleased = false;
    };

    struct FocusNode
    {
        std::uintptr_t owner = 0;
        std::uint32_t id = 0;
        float x = 0.0f;
        float y = 0.0f;
        float width = 0.0f;
        float height = 0.0f;
        float centerX = 0.0f;
        float centerY = 0.0f;
        bool enabled = true;
    };

    struct GamepadSettings
    {
        bool enabled = true;
        float stickDeadZone = 0.18f;
        float triggerDeadZone = 0.08f;
        float pointerSpeed = 520.0f;
        bool invertPointerY = false;
        GamepadBindings bindings = DefaultGamepadBindings();
    };

    class IGamepadBackend
    {
    public:
        virtual ~IGamepadBackend() = default;
        virtual bool Initialize() = 0;
        virtual void Shutdown() = 0;
        virtual void RefreshDevices() = 0;
        virtual GamepadSnapshot Poll() = 0;
        virtual bool IsConnected() const = 0;
        virtual std::string GetDeviceName() const = 0;
    };
}
