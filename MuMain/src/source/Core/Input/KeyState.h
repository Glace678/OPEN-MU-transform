#pragma once

namespace Core::Input
{
    // Portable replacement for the Win32 "is this key currently down" check
    // (HIBYTE(GetAsyncKeyState(vk)) == 128 / & 0x8000), backed by SDL keyboard
    // state. The argument is a Win32 virtual-key code (VK_*) or an ASCII letter
    // or digit, matching what the existing call sites already pass.
    bool IsKeyDown(int virtualKey);

    // Preserve key-down events until a game frame consumes them, even when the
    // corresponding key-up was drained in the same SDL event pump.
    void RecordKeyboardPress(int scancode);
    void ClearKeyboardPresses();

    // Controller and other non-keyboard backends feed the existing legacy key
    // state machine through these overrides. The main thread owns all calls.
    void SetVirtualKeyDown(int virtualKey, bool down);
    void ClearVirtualKeys();

    // Touch is independent of controller overrides. A quick tap stays down
    // for one game frame even if its release arrives in the same event pump.
    void SetTouchMouseButtonDown(int virtualKey, bool down);
    bool IsTouchMouseButtonDown(int virtualKey);
    void CancelTouchMouseButton(int virtualKey);
    void ClearTouchMousePresses();
    void ResetTouchMouseButtons();
}
