#include <doctest.h>

#include "Core/Input/GamepadMapper.h"
#include "Core/Platform/LegacyGeometry.h"

using namespace Core::Input;

TEST_CASE("Legacy pointer geometry retains Win32 widths on LP64 hosts")
{
    const auto point = Core::Platform::MakePoint(1279L, -8.75);
    const auto size = Core::Platform::MakeSize(1920L, 1080L);
    const auto rectangle = Core::Platform::MakeRect(-1.75, 2L, 640L, 480.5);
    CHECK(sizeof(point.x) == 4);
    CHECK(point.x == 1279);
    CHECK(point.y == -8);
    CHECK(size.cx == 1920);
    CHECK(size.cy == 1080);
    CHECK(rectangle.left == -1);
    CHECK(rectangle.top == 2);
    CHECK(rectangle.right == 640);
    CHECK(rectangle.bottom == 480);
}

namespace
{
    std::size_t Index(GamepadButton button)
    {
        return static_cast<std::size_t>(button);
    }
}

TEST_CASE("GamepadMapper emits complete pointer press hold release lifecycle")
{
    GamepadMapper mapper;
    GamepadSnapshot snapshot;
    snapshot.connected = true;

    mapper.Update(snapshot, InputContext::UserInterface, 0.0, 640.0f, 480.0f, true);
    snapshot.buttons[Index(GamepadButton::South)] = true;
    auto pressed = mapper.Update(snapshot, InputContext::UserInterface, 0.016, 640.0f, 480.0f, true);
    CHECK(pressed.pointer.leftPressed);
    CHECK(pressed.pointer.leftDown);

    auto held = mapper.Update(snapshot, InputContext::UserInterface, 0.016, 640.0f, 480.0f, true);
    CHECK_FALSE(held.pointer.leftPressed);
    CHECK(held.pointer.leftDown);

    snapshot.buttons[Index(GamepadButton::South)] = false;
    auto released = mapper.Update(snapshot, InputContext::UserInterface, 0.016, 640.0f, 480.0f, true);
    CHECK(released.pointer.leftReleased);
    CHECK_FALSE(released.pointer.leftDown);
}

TEST_CASE("GamepadMapper uses time based pointer movement")
{
    GamepadSettings settings;
    settings.stickDeadZone = 0.0f;
    settings.pointerSpeed = 500.0f;
    GamepadMapper mapper(settings);
    mapper.Reset(100.0f, 100.0f);

    GamepadSnapshot snapshot;
    snapshot.connected = true;
    mapper.Update(snapshot, InputContext::World, 0.0, 640.0f, 480.0f, true);
    snapshot.rightX = 1.0f;
    const auto frame = mapper.Update(snapshot, InputContext::World, 0.1, 640.0f, 480.0f, true);
    CHECK(frame.pointer.x == doctest::Approx(150.0f));
}

TEST_CASE("UI pointer fallback accepts either stick without changing world movement")
{
    GamepadSettings settings;
    settings.stickDeadZone = 0.0f;
    settings.pointerSpeed = 500.0f;
    GamepadMapper mapper(settings);
    mapper.Reset(100.0f, 100.0f);

    GamepadSnapshot snapshot;
    snapshot.connected = true;
    mapper.Update(snapshot, InputContext::UserInterface, 0.0, 640.0f, 480.0f, true);
    snapshot.leftX = 1.0f;
    auto uiFrame = mapper.Update(snapshot, InputContext::UserInterface, 0.1, 640.0f, 480.0f, true);
    CHECK(uiFrame.pointer.x == doctest::Approx(150.0f));

    mapper.Reset(100.0f, 100.0f);
    snapshot = {};
    snapshot.connected = true;
    mapper.Update(snapshot, InputContext::World, 0.0, 640.0f, 480.0f, true);
    snapshot.leftX = 1.0f;
    const auto worldFrame = mapper.Update(snapshot, InputContext::World, 0.1, 640.0f, 480.0f, true);
    CHECK(worldFrame.pointer.x == doctest::Approx(100.0f));
    CHECK(worldFrame.moveX == doctest::Approx(1.0f));
}

TEST_CASE("D-pad moves the UI pointer fallback")
{
    GamepadSettings settings;
    settings.pointerSpeed = 400.0f;
    GamepadMapper mapper(settings);
    mapper.Reset(100.0f, 100.0f);

    GamepadSnapshot snapshot;
    snapshot.connected = true;
    mapper.Update(snapshot, InputContext::UserInterface, 0.0, 640.0f, 480.0f, true);
    snapshot.buttons[Index(GamepadButton::DpadRight)] = true;
    snapshot.buttons[Index(GamepadButton::DpadDown)] = true;
    const auto frame = mapper.Update(snapshot, InputContext::UserInterface, 0.1, 640.0f, 480.0f, true);
    CHECK(frame.pointer.x == doctest::Approx(140.0f));
    CHECK(frame.pointer.y == doctest::Approx(140.0f));
}

TEST_CASE("GamepadMapper requires neutral after focus returns")
{
    GamepadMapper mapper;
    GamepadSnapshot snapshot;
    snapshot.connected = true;
    mapper.Update(snapshot, InputContext::World, 0.0, 640.0f, 480.0f, true);

    snapshot.buttons[Index(GamepadButton::South)] = true;
    REQUIRE(mapper.Update(snapshot, InputContext::World, 0.016, 640.0f, 480.0f, true).pointer.leftDown);
    mapper.Update(snapshot, InputContext::World, 0.016, 640.0f, 480.0f, false);

    const auto blocked = mapper.Update(snapshot, InputContext::World, 0.016, 640.0f, 480.0f, true);
    CHECK_FALSE(blocked.pointer.leftDown);
    snapshot.buttons[Index(GamepadButton::South)] = false;
    mapper.Update(snapshot, InputContext::World, 0.016, 640.0f, 480.0f, true);
    snapshot.buttons[Index(GamepadButton::South)] = true;
    CHECK(mapper.Update(snapshot, InputContext::World, 0.016, 640.0f, 480.0f, true).pointer.leftDown);
}

TEST_CASE("Suspended input emits pointer releases once")
{
    GamepadMapper mapper;
    GamepadSnapshot snapshot;
    snapshot.connected = true;
    mapper.Update(snapshot, InputContext::UserInterface, 0.0, 640.0f, 480.0f, true);

    snapshot.buttons[Index(GamepadButton::South)] = true;
    snapshot.rightTrigger = 1.0f;
    const auto pressed = mapper.Update(
        snapshot, InputContext::UserInterface, 0.016, 640.0f, 480.0f, true);
    REQUIRE(pressed.pointer.leftDown);
    REQUIRE(pressed.pointer.rightDown);

    const auto released = mapper.Update(
        snapshot, InputContext::UserInterface, 0.016, 640.0f, 480.0f, false);
    CHECK(released.pointer.leftReleased);
    CHECK(released.pointer.rightReleased);

    const auto stillSuspended = mapper.Update(
        snapshot, InputContext::UserInterface, 0.016, 640.0f, 480.0f, false);
    CHECK_FALSE(stillSuspended.pointer.leftReleased);
    CHECK_FALSE(stillSuspended.pointer.rightReleased);
}

TEST_CASE("Ownership reset suppresses stale release and requires neutral input")
{
    GamepadMapper mapper;
    GamepadSnapshot snapshot;
    snapshot.connected = true;
    mapper.Update(snapshot, InputContext::UserInterface, 0.0, 640.0f, 480.0f, true);

    snapshot.buttons[Index(GamepadButton::South)] = true;
    REQUIRE(mapper.Update(
        snapshot, InputContext::UserInterface, 0.016, 640.0f, 480.0f, true).pointer.leftDown);

    mapper.Reset(100.0f, 100.0f);
    snapshot.connected = false;
    const auto disconnected = mapper.Update(
        snapshot, InputContext::UserInterface, 0.016, 640.0f, 480.0f, true);
    CHECK_FALSE(disconnected.pointer.leftDown);
    CHECK_FALSE(disconnected.pointer.leftReleased);

    snapshot.connected = true;
    const auto stillHeld = mapper.Update(
        snapshot, InputContext::UserInterface, 0.016, 640.0f, 480.0f, true);
    CHECK_FALSE(stillHeld.pointer.leftDown);
    CHECK_FALSE(stillHeld.pointer.leftPressed);

    snapshot.buttons[Index(GamepadButton::South)] = false;
    mapper.Update(snapshot, InputContext::UserInterface, 0.016, 640.0f, 480.0f, true);
    snapshot.buttons[Index(GamepadButton::South)] = true;
    CHECK(mapper.Update(
        snapshot, InputContext::UserInterface, 0.016, 640.0f, 480.0f, true).pointer.leftPressed);
}

TEST_CASE("GamepadMapper exposes UI secondary and details actions")
{
    GamepadMapper mapper;
    GamepadSnapshot snapshot;
    snapshot.connected = true;
    mapper.Update(snapshot, InputContext::UserInterface, 0.0, 640.0f, 480.0f, true);

    snapshot.buttons[Index(GamepadButton::West)] = true;
    snapshot.buttons[Index(GamepadButton::North)] = true;
    const auto frame = mapper.Update(snapshot, InputContext::UserInterface, 0.016, 640.0f, 480.0f, true);

    CHECK(frame.actions[static_cast<std::size_t>(InputAction::SecondaryAction)].pressed);
    CHECK(frame.actions[static_cast<std::size_t>(InputAction::Details)].pressed);
}

TEST_CASE("Gamepad action aliases preserve matching press hold release edges")
{
    GamepadMapper mapper;
    GamepadSnapshot snapshot;
    snapshot.connected = true;
    mapper.Update(snapshot, InputContext::UserInterface, 0.0, 640.0f, 480.0f, true);

    snapshot.buttons[Index(GamepadButton::West)] = true;
    const auto pressed = mapper.Update(
        snapshot, InputContext::UserInterface, 0.016, 640.0f, 480.0f, true);
    const auto primary = static_cast<std::size_t>(InputAction::PrimaryAttack);
    const auto secondary = static_cast<std::size_t>(InputAction::SecondaryAction);
    CHECK(pressed.actions[primary].pressed);
    CHECK(pressed.actions[secondary].pressed);
    CHECK(pressed.actions[primary].down == pressed.actions[secondary].down);

    const auto held = mapper.Update(
        snapshot, InputContext::UserInterface, 0.016, 640.0f, 480.0f, true);
    CHECK_FALSE(held.actions[primary].pressed);
    CHECK_FALSE(held.actions[secondary].pressed);
    CHECK(held.actions[primary].down);
    CHECK(held.actions[secondary].down);

    snapshot.buttons[Index(GamepadButton::West)] = false;
    const auto released = mapper.Update(
        snapshot, InputContext::UserInterface, 0.016, 640.0f, 480.0f, true);
    CHECK(released.actions[primary].released);
    CHECK(released.actions[secondary].released);
    CHECK_FALSE(released.actions[primary].down);
    CHECK_FALSE(released.actions[secondary].down);
}

TEST_CASE("Gamepad dead zone boundary remains neutral")
{
    GamepadSettings settings;
    settings.stickDeadZone = 0.2f;
    GamepadMapper mapper(settings);

    GamepadSnapshot snapshot;
    snapshot.connected = true;
    mapper.Update(snapshot, InputContext::World, 0.0, 640.0f, 480.0f, true);

    snapshot.leftX = settings.stickDeadZone;
    const auto boundary = mapper.Update(
        snapshot, InputContext::World, 0.016, 640.0f, 480.0f, true);
    CHECK(boundary.moveX == 0.0f);
    CHECK_FALSE(boundary.actions[static_cast<std::size_t>(InputAction::Move)].down);

    snapshot.leftX = settings.stickDeadZone + 0.01f;
    const auto beyond = mapper.Update(
        snapshot, InputContext::World, 0.016, 640.0f, 480.0f, true);
    CHECK(beyond.moveX > 0.0f);
    CHECK(beyond.actions[static_cast<std::size_t>(InputAction::Move)].down);
}

TEST_CASE("Gamepad rebinding swaps occupied controls without conflicts")
{
    GamepadBindings bindings = DefaultGamepadBindings();
    REQUIRE_FALSE(HasGamepadBindingConflicts(bindings));

    const auto confirm = static_cast<std::size_t>(RemappableGamepadAction::Confirm);
    const auto cancel = static_cast<std::size_t>(RemappableGamepadAction::Cancel);
    REQUIRE(RebindGamepadAction(
        bindings,
        RemappableGamepadAction::Confirm,
        GamepadControl::East));

    CHECK(bindings[confirm] == GamepadControl::East);
    CHECK(bindings[cancel] == GamepadControl::South);
    CHECK_FALSE(HasGamepadBindingConflicts(bindings));
}

TEST_CASE("GamepadMapper follows remapped confirmation control")
{
    GamepadSettings settings;
    REQUIRE(RebindGamepadAction(
        settings.bindings,
        RemappableGamepadAction::Confirm,
        GamepadControl::North));
    GamepadMapper mapper(settings);
    GamepadSnapshot snapshot;
    snapshot.connected = true;
    mapper.Update(snapshot, InputContext::UserInterface, 0.0, 640.0f, 480.0f, true);

    snapshot.buttons[Index(GamepadButton::North)] = true;
    const auto frame = mapper.Update(snapshot, InputContext::UserInterface, 0.016, 640.0f, 480.0f, true);
    CHECK(frame.pointer.leftPressed);
    CHECK(frame.actions[static_cast<std::size_t>(InputAction::Confirm)].pressed);
    CHECK_FALSE(frame.actions[static_cast<std::size_t>(InputAction::ContextAction)].down);
}

TEST_CASE("Configured trigger dead zone affects digital trigger bindings")
{
    GamepadSettings settings;
    settings.triggerDeadZone = 0.5f;
    REQUIRE(RebindGamepadAction(
        settings.bindings,
        RemappableGamepadAction::Confirm,
        GamepadControl::LeftTrigger));
    GamepadMapper mapper(settings);

    GamepadSnapshot snapshot;
    snapshot.connected = true;
    mapper.Update(snapshot, InputContext::UserInterface, 0.0, 640.0f, 480.0f, true);

    snapshot.leftTrigger = 0.6f;
    auto belowThreshold = mapper.Update(
        snapshot, InputContext::UserInterface, 0.016, 640.0f, 480.0f, true);
    CHECK_FALSE(belowThreshold.actions[static_cast<std::size_t>(InputAction::Confirm)].down);

    snapshot.leftTrigger = 0.8f;
    auto aboveThreshold = mapper.Update(
        snapshot, InputContext::UserInterface, 0.016, 640.0f, 480.0f, true);
    CHECK(aboveThreshold.actions[static_cast<std::size_t>(InputAction::Confirm)].pressed);
}
