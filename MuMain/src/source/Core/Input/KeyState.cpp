#include "Core/Input/KeyState.h"
#include "Core/Platform/WinCompat.h"

#include <SDL3/SDL.h>
#include <array>

namespace Core::Input
{
    namespace
    {
        std::array<bool, 256> g_virtualKeys{};
        std::array<bool, SDL_SCANCODE_COUNT> g_keyboardPresses{};
        std::array<bool, 2> g_touchMouseButtons{};
        std::array<bool, 2> g_touchMousePresses{};

        int TouchButtonIndex(int virtualKey)
        {
            if (virtualKey == VK_LBUTTON) return 0;
            if (virtualKey == VK_RBUTTON) return 1;
            return -1;
        }

        bool IsScancodeDown(SDL_Scancode scancode)
        {
            const bool* state = SDL_GetKeyboardState(nullptr);
            return g_keyboardPresses[scancode] || (state != nullptr && state[scancode]);
        }

        // Map a Win32 virtual-key code (or ASCII letter/digit) to an SDL
        // scancode. Returns SDL_SCANCODE_UNKNOWN for keys we don't translate.
        SDL_Scancode VkToScancode(int vk)
        {
            // ASCII letters and digits: VK codes equal their ASCII values.
            if (vk >= 'A' && vk <= 'Z') return static_cast<SDL_Scancode>(SDL_SCANCODE_A + (vk - 'A'));
            if (vk >= '1' && vk <= '9') return static_cast<SDL_Scancode>(SDL_SCANCODE_1 + (vk - '1'));
            if (vk == '0') return SDL_SCANCODE_0;

            switch (vk)
            {
            case VK_UP:      return SDL_SCANCODE_UP;
            case VK_DOWN:    return SDL_SCANCODE_DOWN;
            case VK_LEFT:    return SDL_SCANCODE_LEFT;
            case VK_RIGHT:   return SDL_SCANCODE_RIGHT;
            case VK_INSERT:  return SDL_SCANCODE_INSERT;
            case VK_DELETE:  return SDL_SCANCODE_DELETE;
            case VK_HOME:    return SDL_SCANCODE_HOME;
            case VK_END:     return SDL_SCANCODE_END;
            case VK_PRIOR:   return SDL_SCANCODE_PAGEUP;
            case VK_NEXT:    return SDL_SCANCODE_PAGEDOWN;
            case VK_SPACE:   return SDL_SCANCODE_SPACE;
            case VK_RETURN:  return SDL_SCANCODE_RETURN;
            case VK_ESCAPE:  return SDL_SCANCODE_ESCAPE;
            case VK_TAB:     return SDL_SCANCODE_TAB;
            case VK_BACK:    return SDL_SCANCODE_BACKSPACE;
            case VK_NUMPAD0: return SDL_SCANCODE_KP_0;
            case VK_NUMPAD1: return SDL_SCANCODE_KP_1;
            case VK_NUMPAD2: return SDL_SCANCODE_KP_2;
            case VK_NUMPAD3: return SDL_SCANCODE_KP_3;
            case VK_NUMPAD4: return SDL_SCANCODE_KP_4;
            case VK_NUMPAD5: return SDL_SCANCODE_KP_5;
            case VK_NUMPAD6: return SDL_SCANCODE_KP_6;
            case VK_NUMPAD7: return SDL_SCANCODE_KP_7;
            case VK_NUMPAD8: return SDL_SCANCODE_KP_8;
            case VK_NUMPAD9: return SDL_SCANCODE_KP_9;
            case VK_ADD:     return SDL_SCANCODE_KP_PLUS;
            case VK_SUBTRACT: return SDL_SCANCODE_KP_MINUS;
            case VK_F1:      return SDL_SCANCODE_F1;
            case VK_F2:      return SDL_SCANCODE_F2;
            case VK_F3:      return SDL_SCANCODE_F3;
            case VK_F4:      return SDL_SCANCODE_F4;
            case VK_F5:      return SDL_SCANCODE_F5;
            case VK_F6:      return SDL_SCANCODE_F6;
            case VK_F7:      return SDL_SCANCODE_F7;
            case VK_F8:      return SDL_SCANCODE_F8;
            case VK_F9:      return SDL_SCANCODE_F9;
            case VK_F10:     return SDL_SCANCODE_F10;
            case VK_F11:     return SDL_SCANCODE_F11;
            case VK_F12:     return SDL_SCANCODE_F12;
            case VK_SNAPSHOT: return SDL_SCANCODE_PRINTSCREEN;
            default:         return SDL_SCANCODE_UNKNOWN;
            }
        }
    }

    bool IsKeyDown(int virtualKey)
    {
        if (IsTouchMouseButtonDown(virtualKey))
            return true;
        if (virtualKey >= 0 && virtualKey < static_cast<int>(g_virtualKeys.size())
            && g_virtualKeys[virtualKey])
        {
            return true;
        }

        // Poll live input from SDL on every platform. The Win32 path used
        // GetAsyncKeyState because the old child EDIT controls stole keyboard
        // focus from the SDL window; they were replaced by the portable text
        // field (#447), so SDL input state is authoritative here too. The main
        // loop pumps SDL_PollEvent each frame, so this state stays current.

        // Mouse buttons and modifiers come from the SDL mouse / mod state.
        switch (virtualKey)
        {
        case VK_LBUTTON: return (SDL_GetMouseState(nullptr, nullptr) & SDL_BUTTON_MASK(SDL_BUTTON_LEFT)) != 0;
        case VK_RBUTTON: return (SDL_GetMouseState(nullptr, nullptr) & SDL_BUTTON_MASK(SDL_BUTTON_RIGHT)) != 0;
        case VK_MBUTTON: return (SDL_GetMouseState(nullptr, nullptr) & SDL_BUTTON_MASK(SDL_BUTTON_MIDDLE)) != 0;
        case VK_SHIFT:   return (SDL_GetModState() & SDL_KMOD_SHIFT) != 0 || g_keyboardPresses[SDL_SCANCODE_LSHIFT] || g_keyboardPresses[SDL_SCANCODE_RSHIFT];
        case VK_CONTROL: return (SDL_GetModState() & SDL_KMOD_CTRL) != 0 || g_keyboardPresses[SDL_SCANCODE_LCTRL] || g_keyboardPresses[SDL_SCANCODE_RCTRL];
        case VK_MENU:    return (SDL_GetModState() & SDL_KMOD_ALT) != 0 || g_keyboardPresses[SDL_SCANCODE_LALT] || g_keyboardPresses[SDL_SCANCODE_RALT];
        case VK_RETURN:  return IsScancodeDown(SDL_SCANCODE_RETURN) || IsScancodeDown(SDL_SCANCODE_KP_ENTER);
        default: break;
        }

        const SDL_Scancode sc = VkToScancode(virtualKey);
        if (sc == SDL_SCANCODE_UNKNOWN) return false;

        return IsScancodeDown(sc);
    }

    void RecordKeyboardPress(int scancode)
    {
        if (scancode > SDL_SCANCODE_UNKNOWN && scancode < SDL_SCANCODE_COUNT)
            g_keyboardPresses[scancode] = true;
    }

    void ClearKeyboardPresses()
    {
        g_keyboardPresses.fill(false);
    }

    void SetVirtualKeyDown(int virtualKey, bool down)
    {
        if (virtualKey >= 0 && virtualKey < static_cast<int>(g_virtualKeys.size()))
        {
            g_virtualKeys[virtualKey] = down;
        }
    }

    void ClearVirtualKeys()
    {
        g_virtualKeys.fill(false);
    }

    void SetTouchMouseButtonDown(int virtualKey, bool down)
    {
        const int index = TouchButtonIndex(virtualKey);
        if (index < 0) return;
        if (down && !g_touchMouseButtons[index])
            g_touchMousePresses[index] = true;
        g_touchMouseButtons[index] = down;
    }

    bool IsTouchMouseButtonDown(int virtualKey)
    {
        const int index = TouchButtonIndex(virtualKey);
        return index >= 0
            && (g_touchMouseButtons[index] || g_touchMousePresses[index]);
    }

    void CancelTouchMouseButton(int virtualKey)
    {
        const int index = TouchButtonIndex(virtualKey);
        if (index < 0) return;
        g_touchMouseButtons[index] = false;
        g_touchMousePresses[index] = false;
    }

    void ClearTouchMousePresses()
    {
        g_touchMousePresses.fill(false);
    }

    void ResetTouchMouseButtons()
    {
        g_touchMouseButtons.fill(false);
        ClearTouchMousePresses();
    }
}
