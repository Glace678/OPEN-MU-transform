#include "doctest.h"
#include "Core/Input/KeyState.h"
#include "Core/Platform/WinCompat.h"
#include <SDL3/SDL.h>

TEST_CASE("quick keyboard taps survive an event pump until a game frame consumes them")
{
    Core::Input::ClearKeyboardPresses();
    Core::Input::ClearVirtualKeys();
    Core::Input::RecordKeyboardPress(SDL_SCANCODE_I);
    CHECK(Core::Input::IsKeyDown('I'));
    CHECK(Core::Input::IsKeyDown('I')); // More than one input consumer in a frame.
    Core::Input::ClearKeyboardPresses();
    CHECK_FALSE(Core::Input::IsKeyDown('I'));
}

TEST_CASE("keyboard modifiers and keypad enter retain fast presses")
{
    Core::Input::RecordKeyboardPress(SDL_SCANCODE_RSHIFT);
    Core::Input::RecordKeyboardPress(SDL_SCANCODE_LCTRL);
    Core::Input::RecordKeyboardPress(SDL_SCANCODE_KP_ENTER);
    CHECK(Core::Input::IsKeyDown(VK_SHIFT));
    CHECK(Core::Input::IsKeyDown(VK_CONTROL));
    CHECK(Core::Input::IsKeyDown(VK_RETURN));
    Core::Input::ClearKeyboardPresses();
    CHECK_FALSE(Core::Input::IsKeyDown(VK_SHIFT));
    CHECK_FALSE(Core::Input::IsKeyDown(VK_RETURN));
}

TEST_CASE("clearing keyboard presses preserves independent controller keys")
{
    Core::Input::SetVirtualKeyDown('V', true);
    Core::Input::RecordKeyboardPress(SDL_SCANCODE_V);
    Core::Input::ClearKeyboardPresses();
    CHECK(Core::Input::IsKeyDown('V'));
    Core::Input::ClearVirtualKeys();
    CHECK_FALSE(Core::Input::IsKeyDown('V'));
}

TEST_CASE("unknown keyboard scancodes cannot access outside the input table")
{
    Core::Input::ClearKeyboardPresses();
    Core::Input::RecordKeyboardPress(-1);
    Core::Input::RecordKeyboardPress(SDL_SCANCODE_UNKNOWN);
    Core::Input::RecordKeyboardPress(SDL_SCANCODE_COUNT);
    CHECK_FALSE(Core::Input::IsKeyDown(-1));
    CHECK_FALSE(Core::Input::IsKeyDown('I'));
}

TEST_CASE("touch taps survive the event pump independently of controller keys")
{
    Core::Input::ResetTouchMouseButtons();
    Core::Input::SetTouchMouseButtonDown(VK_LBUTTON, true);
    Core::Input::SetTouchMouseButtonDown(VK_LBUTTON, false);
    Core::Input::ClearVirtualKeys();
    CHECK(Core::Input::IsTouchMouseButtonDown(VK_LBUTTON));
    CHECK(Core::Input::IsKeyDown(VK_LBUTTON));
    Core::Input::ClearTouchMousePresses();
    CHECK_FALSE(Core::Input::IsTouchMouseButtonDown(VK_LBUTTON));
    CHECK_FALSE(Core::Input::IsKeyDown(VK_LBUTTON));
}

TEST_CASE("held touch buttons persist across frames and cancel without a stray click")
{
    Core::Input::ResetTouchMouseButtons();
    Core::Input::SetTouchMouseButtonDown(VK_RBUTTON, true);
    Core::Input::ClearTouchMousePresses();
    CHECK(Core::Input::IsKeyDown(VK_RBUTTON));
    Core::Input::CancelTouchMouseButton(VK_RBUTTON);
    CHECK_FALSE(Core::Input::IsKeyDown(VK_RBUTTON));
    Core::Input::SetVirtualKeyDown(VK_RBUTTON, true);
    Core::Input::ResetTouchMouseButtons();
    CHECK(Core::Input::IsKeyDown(VK_RBUTTON));
    Core::Input::ClearVirtualKeys();
}

TEST_CASE("touch reset clears both buttons and ignores invalid virtual keys")
{
    Core::Input::SetTouchMouseButtonDown(VK_LBUTTON, true);
    Core::Input::SetTouchMouseButtonDown(VK_RBUTTON, true);
    Core::Input::SetTouchMouseButtonDown(-1, true);
    Core::Input::SetTouchMouseButtonDown('A', true);
    CHECK_FALSE(Core::Input::IsTouchMouseButtonDown(-1));
    CHECK_FALSE(Core::Input::IsTouchMouseButtonDown('A'));
    Core::Input::ResetTouchMouseButtons();
    CHECK_FALSE(Core::Input::IsKeyDown(VK_LBUTTON));
    CHECK_FALSE(Core::Input::IsKeyDown(VK_RBUTTON));
}
