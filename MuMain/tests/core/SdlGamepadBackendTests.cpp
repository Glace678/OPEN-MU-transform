#include <doctest.h>

#include "Core/Input/SdlGamepadBackend.h"
#include "Core/Input/GamepadService.h"

#include <SDL3/SDL.h>

#include <cstdint>
#include <string>
#include <utility>

using namespace Core::Input;

namespace
{
    struct RumbleState
    {
        std::uint16_t low = 0;
        std::uint16_t high = 0;
        int calls = 0;
    };

    bool SDLCALL RecordRumble(void* userdata, Uint16 low, Uint16 high)
    {
        auto& state = *static_cast<RumbleState*>(userdata);
        state.low = low;
        state.high = high;
        ++state.calls;
        return true;
    }

    class SdlGamepadSubsystem
    {
    public:
        SdlGamepadSubsystem()
            : initialized(SDL_InitSubSystem(SDL_INIT_GAMEPAD))
        {
        }

        ~SdlGamepadSubsystem()
        {
            if (initialized)
                SDL_QuitSubSystem(SDL_INIT_GAMEPAD);
        }

        bool initialized = false;
    };

    class VirtualGamepad
    {
    public:
        explicit VirtualGamepad(std::string name)
            : m_name(std::move(name))
        {
            SDL_INIT_INTERFACE(&m_description);
            m_description.type = SDL_JOYSTICK_TYPE_GAMEPAD;
            m_description.vendor_id = 0xffff;
            m_description.product_id = 0x0001;
            m_description.naxes = SDL_GAMEPAD_AXIS_COUNT;
            m_description.nbuttons = SDL_GAMEPAD_BUTTON_COUNT;
            m_description.button_mask = (1u << SDL_GAMEPAD_BUTTON_COUNT) - 1u;
            m_description.axis_mask = (1u << SDL_GAMEPAD_AXIS_COUNT) - 1u;
            m_description.name = m_name.c_str();
            m_description.userdata = &rumble;
            m_description.Rumble = RecordRumble;

            id = SDL_AttachVirtualJoystick(&m_description);
            if (id != 0)
                joystick = SDL_OpenJoystick(id);
        }

        ~VirtualGamepad()
        {
            Detach();
        }

        void Detach()
        {
            if (joystick != nullptr)
            {
                SDL_CloseJoystick(joystick);
                joystick = nullptr;
            }
            if (id != 0)
            {
                SDL_DetachVirtualJoystick(id);
                id = 0;
            }
        }

        SDL_JoystickID id = 0;
        SDL_Joystick* joystick = nullptr;
        RumbleState rumble;

    private:
        std::string m_name;
        SDL_VirtualJoystickDesc m_description{};
    };

    SDL_Event GamepadButtonEvent(SDL_JoystickID deviceId)
    {
        SDL_Event event{};
        event.type = SDL_EVENT_GAMEPAD_BUTTON_DOWN;
        event.gbutton.which = deviceId;
        event.gbutton.button = SDL_GAMEPAD_BUTTON_SOUTH;
        return event;
    }
}

TEST_CASE("SDL backend handles virtual input active-device switching and disconnect")
{
    SdlGamepadSubsystem subsystem;
    REQUIRE(subsystem.initialized);
    VirtualGamepad first("MuMain SDL backend test one");
    VirtualGamepad second("MuMain SDL backend test two");
    REQUIRE(first.id != 0);
    REQUIRE(second.id != 0);
    REQUIRE(first.joystick != nullptr);
    REQUIRE(second.joystick != nullptr);

    SdlGamepadBackend backend;
    REQUIRE(backend.Initialize());

    backend.HandleEvent(GamepadButtonEvent(second.id));
    REQUIRE(backend.GetActiveDeviceId() == second.id);
    REQUIRE(SDL_SetJoystickVirtualButton(second.joystick, SDL_GAMEPAD_BUTTON_SOUTH, true));
    REQUIRE(SDL_SetJoystickVirtualAxis(second.joystick, SDL_GAMEPAD_AXIS_LEFTX, 16384));
    REQUIRE(SDL_SetJoystickVirtualAxis(
        second.joystick, SDL_GAMEPAD_AXIS_RIGHT_TRIGGER, SDL_JOYSTICK_AXIS_MAX));
    SDL_UpdateJoysticks();

    const GamepadSnapshot snapshot = backend.Poll();
    CHECK(snapshot.connected);
    CHECK(snapshot.buttons[static_cast<std::size_t>(GamepadButton::South)]);
    CHECK(snapshot.leftX == doctest::Approx(0.5f).epsilon(0.001));
    CHECK(snapshot.rightTrigger == doctest::Approx(1.0f));
    CHECK(backend.SupportsRumble());

    backend.Play(1234, 5678, 80);
    SDL_UpdateJoysticks();
    CHECK(second.rumble.calls > 0);
    CHECK(second.rumble.low == 1234);
    CHECK(second.rumble.high == 5678);

    const int secondRumbleCalls = second.rumble.calls;
    backend.HandleEvent(GamepadButtonEvent(first.id));
    CHECK(backend.GetActiveDeviceId() == first.id);
    CHECK(second.rumble.calls > secondRumbleCalls);
    CHECK(second.rumble.low == 0);
    CHECK(second.rumble.high == 0);

    const SDL_JoystickID removedId = first.id;
    first.Detach();
    SDL_Event removed{};
    removed.type = SDL_EVENT_GAMEPAD_REMOVED;
    removed.gdevice.which = removedId;
    backend.HandleEvent(removed);
    CHECK(backend.GetActiveDeviceId() != removedId);

    backend.HandleEvent(GamepadButtonEvent(second.id));
    CHECK(backend.GetActiveDeviceId() == second.id);
    backend.Shutdown();
    second.Detach();
}

TEST_CASE("Every mapped button trigger and stick activation has input rumble")
{
    SdlGamepadSubsystem subsystem;
    REQUIRE(subsystem.initialized);
    VirtualGamepad gamepad("MuMain complete input rumble test");
    REQUIRE(gamepad.joystick != nullptr);
    auto& service = GamepadService::Instance();
    service.Shutdown();
    REQUIRE(service.Initialize(GamepadSettings{}, Core::Haptics::HapticSettings{}));
    service.OnFocusChanged(true);
    service.HandleEvent(GamepadButtonEvent(gamepad.id));
    double now = 100;
    service.Update(InputContext::World, now, 640, 480, true);
    for (int button = SDL_GAMEPAD_BUTTON_SOUTH; button <= SDL_GAMEPAD_BUTTON_DPAD_RIGHT; ++button)
    {
        if (button == SDL_GAMEPAD_BUTTON_GUIDE) continue;
        REQUIRE(SDL_SetJoystickVirtualButton(gamepad.joystick, button, true));
        SDL_UpdateJoysticks();
        service.Update(InputContext::World, now += 100, 640, 480, true);
        CHECK(gamepad.rumble.low > 0);
        CHECK(gamepad.rumble.high > 0);
        REQUIRE(SDL_SetJoystickVirtualButton(gamepad.joystick, button, false));
        SDL_UpdateJoysticks();
        service.Update(InputContext::World, now += 100, 640, 480, true);
    }
    for (int axis : {SDL_GAMEPAD_AXIS_LEFTX, SDL_GAMEPAD_AXIS_RIGHTX,
                     SDL_GAMEPAD_AXIS_LEFT_TRIGGER, SDL_GAMEPAD_AXIS_RIGHT_TRIGGER})
    {
        REQUIRE(SDL_SetJoystickVirtualAxis(gamepad.joystick, axis, SDL_JOYSTICK_AXIS_MAX));
        SDL_UpdateJoysticks();
        service.Update(InputContext::World, now += 100, 640, 480, true);
        CHECK(gamepad.rumble.high > 0);
        REQUIRE(SDL_SetJoystickVirtualAxis(gamepad.joystick, axis,
            axis >= SDL_GAMEPAD_AXIS_LEFT_TRIGGER ? SDL_JOYSTICK_AXIS_MIN : 0));
        SDL_UpdateJoysticks();
        service.Update(InputContext::World, now += 100, 640, 480, true);
    }
    service.OnFocusChanged(false);
    CHECK(gamepad.rumble.low == 0);
    CHECK(gamepad.rumble.high == 0);
    service.OnFocusChanged(true);
    service.Shutdown();
}

TEST_CASE("Gamepad service reports device ownership changes exactly once")
{
    SdlGamepadSubsystem subsystem;
    REQUIRE(subsystem.initialized);
    VirtualGamepad first("MuMain gamepad service test one");
    VirtualGamepad second("MuMain gamepad service test two");
    REQUIRE(first.id != 0);
    REQUIRE(second.id != 0);

    auto& service = GamepadService::Instance();
    service.Shutdown();
    REQUIRE(service.Initialize(GamepadSettings{}, Core::Haptics::HapticSettings{}));
    CHECK_FALSE(service.ConsumeInputOwnershipChanged());

    service.SetPointerPosition(321.0f, 123.0f);
    service.RequireNeutralInput();

    service.HandleEvent(GamepadButtonEvent(first.id));
    const bool changedToFirst = service.ConsumeInputOwnershipChanged();
    CHECK_FALSE(service.ConsumeInputOwnershipChanged());

    const auto& firstFrame = service.Update(
        InputContext::UserInterface, 10.0, 640.0f, 480.0f, true);
    CHECK(firstFrame.pointer.x == doctest::Approx(321.0f));
    CHECK(firstFrame.pointer.y == doctest::Approx(123.0f));

    service.HandleEvent(GamepadButtonEvent(second.id));
    const bool changedToSecond = service.ConsumeInputOwnershipChanged();
    CHECK((changedToFirst || changedToSecond));
    CHECK_FALSE(service.ConsumeInputOwnershipChanged());
    const auto& secondFrame = service.Update(
        InputContext::UserInterface, 20.0, 640.0f, 480.0f, true);
    CHECK(secondFrame.pointer.x == doctest::Approx(321.0f));
    CHECK(secondFrame.pointer.y == doctest::Approx(123.0f));

    service.Shutdown();
}

TEST_CASE("Gamepad service suppresses held input until a fresh post-neutral press")
{
    SdlGamepadSubsystem subsystem;
    REQUIRE(subsystem.initialized);
    VirtualGamepad gamepad("MuMain neutral input suppression test");
    REQUIRE(gamepad.id != 0);
    REQUIRE(gamepad.joystick != nullptr);

    auto& service = GamepadService::Instance();
    service.Shutdown();
    REQUIRE(service.Initialize(GamepadSettings{}, Core::Haptics::HapticSettings{}));
    service.HandleEvent(GamepadButtonEvent(gamepad.id));

    const auto mapIndex = static_cast<std::size_t>(InputAction::Map);
    service.SetPointerPosition(222.0f, 111.0f);
    service.RequireNeutralInput();
    const auto& initialNeutral = service.Update(
        InputContext::UserInterface, 10.0, 640.0f, 480.0f, true);
    CHECK(initialNeutral.pointer.x == doctest::Approx(222.0f));
    CHECK(initialNeutral.pointer.y == doctest::Approx(111.0f));
    REQUIRE(SDL_SetJoystickVirtualButton(gamepad.joystick, SDL_GAMEPAD_BUTTON_BACK, true));
    SDL_UpdateJoysticks();
    const auto& initialPress = service.Update(
        InputContext::UserInterface, 20.0, 640.0f, 480.0f, true);
    REQUIRE(initialPress.actions[mapIndex].pressed);

    service.RequireNeutralInput();
    const auto& held = service.Update(
        InputContext::UserInterface, 30.0, 640.0f, 480.0f, true);
    CHECK_FALSE(held.actions[mapIndex].down);
    CHECK_FALSE(held.actions[mapIndex].pressed);
    CHECK_FALSE(held.actions[mapIndex].released);

    REQUIRE(SDL_SetJoystickVirtualButton(gamepad.joystick, SDL_GAMEPAD_BUTTON_BACK, false));
    SDL_UpdateJoysticks();
    const auto& neutral = service.Update(
        InputContext::UserInterface, 40.0, 640.0f, 480.0f, true);
    CHECK_FALSE(neutral.actions[mapIndex].down);
    CHECK_FALSE(neutral.actions[mapIndex].pressed);
    CHECK_FALSE(neutral.actions[mapIndex].released);

    REQUIRE(SDL_SetJoystickVirtualButton(gamepad.joystick, SDL_GAMEPAD_BUTTON_BACK, true));
    SDL_UpdateJoysticks();
    const auto& freshPress = service.Update(
        InputContext::UserInterface, 50.0, 640.0f, 480.0f, true);
    CHECK(freshPress.actions[mapIndex].down);
    CHECK(freshPress.actions[mapIndex].pressed);
    CHECK_FALSE(freshPress.actions[mapIndex].released);

    service.Shutdown();
}

TEST_CASE("Gamepad setting ownership changes suppress held controls without release edges")
{
    SdlGamepadSubsystem subsystem;
    REQUIRE(subsystem.initialized);
    VirtualGamepad gamepad("MuMain settings ownership test");
    REQUIRE(gamepad.id != 0);
    REQUIRE(gamepad.joystick != nullptr);

    GamepadSettings settings;
    auto& service = GamepadService::Instance();
    service.Shutdown();
    REQUIRE(service.Initialize(settings, Core::Haptics::HapticSettings{}));
    service.HandleEvent(GamepadButtonEvent(gamepad.id));
    (void)service.ConsumeInputOwnershipChanged();
    service.Update(InputContext::UserInterface, 10.0, 640.0f, 480.0f, true);

    REQUIRE(SDL_SetJoystickVirtualButton(gamepad.joystick, SDL_GAMEPAD_BUTTON_SOUTH, true));
    SDL_UpdateJoysticks();
    const auto& confirm = service.Update(
        InputContext::UserInterface, 20.0, 640.0f, 480.0f, true);
    const auto confirmIndex = static_cast<std::size_t>(InputAction::Confirm);
    const auto cancelIndex = static_cast<std::size_t>(InputAction::Cancel);
    REQUIRE(confirm.actions[confirmIndex].pressed);

    settings.pointerSpeed += 10.0f;
    service.SetGamepadSettings(settings);
    CHECK_FALSE(service.ConsumeInputOwnershipChanged());

    REQUIRE(RebindGamepadAction(
        settings.bindings,
        RemappableGamepadAction::Confirm,
        GamepadControl::East));
    service.SetGamepadSettings(settings);
    CHECK(service.ConsumeInputOwnershipChanged());
    CHECK_FALSE(service.ConsumeInputOwnershipChanged());
    const auto& heldAfterRebind = service.Update(
        InputContext::UserInterface, 30.0, 640.0f, 480.0f, true);
    CHECK_FALSE(heldAfterRebind.actions[confirmIndex].down);
    CHECK_FALSE(heldAfterRebind.actions[confirmIndex].released);
    CHECK_FALSE(heldAfterRebind.actions[cancelIndex].down);
    CHECK_FALSE(heldAfterRebind.actions[cancelIndex].pressed);

    REQUIRE(SDL_SetJoystickVirtualButton(gamepad.joystick, SDL_GAMEPAD_BUTTON_SOUTH, false));
    SDL_UpdateJoysticks();
    service.Update(InputContext::UserInterface, 40.0, 640.0f, 480.0f, true);
    REQUIRE(SDL_SetJoystickVirtualButton(gamepad.joystick, SDL_GAMEPAD_BUTTON_SOUTH, true));
    SDL_UpdateJoysticks();
    const auto& freshCancel = service.Update(
        InputContext::UserInterface, 50.0, 640.0f, 480.0f, true);
    REQUIRE(freshCancel.actions[cancelIndex].pressed);

    settings.enabled = false;
    service.SetGamepadSettings(settings);
    CHECK(service.ConsumeInputOwnershipChanged());
    CHECK_FALSE(service.IsInputEnabled());
    const auto& disabled = service.Update(
        InputContext::UserInterface, 60.0, 640.0f, 480.0f, true);
    CHECK_FALSE(disabled.actions[cancelIndex].down);
    CHECK_FALSE(disabled.actions[cancelIndex].released);
    CHECK_FALSE(disabled.pointer.leftReleased);

    service.Shutdown();
}
