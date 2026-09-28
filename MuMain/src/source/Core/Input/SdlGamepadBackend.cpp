#include "Core/Input/SdlGamepadBackend.h"

#ifdef MU_ENABLE_VIRTUAL_GAMEPAD_TESTS
#ifdef _WIN32
#include <windows.h>
#else
#include "Core/Platform/WinIni.h"
#endif
#endif

#include <algorithm>
#include <array>
#include <cmath>
#include <cstring>
#include <cstdlib>
#include <filesystem>

namespace Core::Input
{
    namespace
    {
#if !defined(__ANDROID__) && !defined(__OHOS__)
        constexpr float AxisPositiveMaximum = 32767.0f;
        constexpr float AxisNegativeMaximum = 32768.0f;
        constexpr Sint16 ActivityAxisThreshold = 12000;

        constexpr std::array<SDL_GamepadButton, static_cast<std::size_t>(GamepadButton::Count)> ButtonMap = {
            SDL_GAMEPAD_BUTTON_SOUTH,
            SDL_GAMEPAD_BUTTON_EAST,
            SDL_GAMEPAD_BUTTON_WEST,
            SDL_GAMEPAD_BUTTON_NORTH,
            SDL_GAMEPAD_BUTTON_BACK,
            SDL_GAMEPAD_BUTTON_GUIDE,
            SDL_GAMEPAD_BUTTON_START,
            SDL_GAMEPAD_BUTTON_LEFT_STICK,
            SDL_GAMEPAD_BUTTON_RIGHT_STICK,
            SDL_GAMEPAD_BUTTON_LEFT_SHOULDER,
            SDL_GAMEPAD_BUTTON_RIGHT_SHOULDER,
            SDL_GAMEPAD_BUTTON_DPAD_UP,
            SDL_GAMEPAD_BUTTON_DPAD_DOWN,
            SDL_GAMEPAD_BUTTON_DPAD_LEFT,
            SDL_GAMEPAD_BUTTON_DPAD_RIGHT,
        };
#endif
    }

    SdlGamepadBackend::~SdlGamepadBackend()
    {
        Shutdown();
    }

    bool SdlGamepadBackend::Initialize()
    {
        if (m_initialized) return true;
#if defined(__ANDROID__) || defined(__OHOS__)
        // Phone/tablet builds are touch-only: the SDL gamepad subsystem is
        // never initialized and no controller is ever opened. The haptics
        // subsystem stays up so phone vibration (combat/UI/touch feedback)
        // keeps working through the system vibrator.
        if (SDL_InitSubSystem(SDL_INIT_HAPTIC))
        {
            m_hapticSubsystemInitialized = true;
            int hapticCount = 0;
            SDL_HapticID* haptics = SDL_GetHaptics(&hapticCount);
            for (int index = 0; haptics != nullptr && index < hapticCount; ++index)
            {
                const char* name = SDL_GetHapticNameForID(haptics[index]);
                if (name == nullptr || std::strcmp(name, "VIBRATOR_SERVICE") != 0)
                    continue;
                SDL_Haptic* candidate = SDL_OpenHaptic(haptics[index]);
                if (candidate != nullptr && SDL_InitHapticRumble(candidate))
                {
                    m_systemHaptic = candidate;
                    break;
                }
                if (candidate != nullptr)
                    SDL_CloseHaptic(candidate);
            }
            SDL_free(haptics);
        }
        m_initialized = true;
        return true;
#else
#ifdef MU_ENABLE_VIRTUAL_GAMEPAD_TESTS
        if (const char* forceFocus = std::getenv("MU_VIRTUAL_GAMEPAD_FORCE_FOCUS");
            forceFocus != nullptr && std::strcmp(forceFocus, "1") == 0)
        {
            // Hidden acceptance clients have no SDL keyboard focus. Permit
            // their virtual device only when the explicit Debug runner asks
            // for it; normal clients continue to reject background input.
            SDL_SetHint(SDL_HINT_JOYSTICK_ALLOW_BACKGROUND_EVENTS, "1");
        }
#endif
        if (!SDL_InitSubSystem(SDL_INIT_GAMEPAD)) return false;
        m_initialized = true;
#ifdef MU_ENABLE_VIRTUAL_GAMEPAD_TESTS
        if (const char* statePath = std::getenv("MU_VIRTUAL_GAMEPAD_STATE"))
        {
            m_virtualStatePathNarrow = statePath;
            m_virtualStatePath = std::filesystem::path(statePath).wstring();
            AttachVirtualTestDevice();
        }
#endif
        RefreshDevices();
        return true;
#endif
    }

    void SdlGamepadBackend::Shutdown()
    {
        if (!m_initialized) return;
#if defined(__ANDROID__) || defined(__OHOS__)
        if (m_systemHaptic != nullptr)
        {
            SDL_StopHapticRumble(m_systemHaptic);
            SDL_CloseHaptic(m_systemHaptic);
            m_systemHaptic = nullptr;
        }
        if (m_hapticSubsystemInitialized)
        {
            SDL_QuitSubSystem(SDL_INIT_HAPTIC);
            m_hapticSubsystemInitialized = false;
        }
        m_initialized = false;
        return;
#else
        CloseDevice();
#ifdef MU_ENABLE_VIRTUAL_GAMEPAD_TESTS
        DetachVirtualTestDevice();
#endif
        SDL_QuitSubSystem(SDL_INIT_GAMEPAD);
        m_initialized = false;
#endif
    }

    void SdlGamepadBackend::RefreshDevices()
    {
#if defined(__ANDROID__) || defined(__OHOS__)
        return; // touch-only build: controllers are never enumerated
#else
        if (!m_initialized) return;
        if (m_gamepad && SDL_GamepadConnected(m_gamepad)) return;

        CloseDevice();
        int count = 0;
        SDL_JoystickID* devices = SDL_GetGamepads(&count);
        if (!devices) return;
        for (int i = 0; i < count; ++i)
        {
            if (OpenDevice(devices[i])) break;
        }
        SDL_free(devices);
#endif
    }

    GamepadSnapshot SdlGamepadBackend::Poll()
    {
#if defined(__ANDROID__) || defined(__OHOS__)
        return GamepadSnapshot{}; // touch-only build never produces gamepad input
#else
#ifdef MU_ENABLE_VIRTUAL_GAMEPAD_TESTS
        UpdateVirtualTestDevice();
#endif
        if (!IsConnected())
        {
            RefreshDevices();
        }

        GamepadSnapshot result;
        if (!IsConnected()) return result;

        result.connected = true;
        for (std::size_t i = 0; i < ButtonMap.size(); ++i)
        {
            result.buttons[i] = SDL_GetGamepadButton(m_gamepad, ButtonMap[i]);
        }

        result.leftX = NormalizeStick(SDL_GetGamepadAxis(m_gamepad, SDL_GAMEPAD_AXIS_LEFTX));
        result.leftY = NormalizeStick(SDL_GetGamepadAxis(m_gamepad, SDL_GAMEPAD_AXIS_LEFTY));
        result.rightX = NormalizeStick(SDL_GetGamepadAxis(m_gamepad, SDL_GAMEPAD_AXIS_RIGHTX));
        result.rightY = NormalizeStick(SDL_GetGamepadAxis(m_gamepad, SDL_GAMEPAD_AXIS_RIGHTY));
        result.leftTrigger = NormalizeTrigger(SDL_GetGamepadAxis(m_gamepad, SDL_GAMEPAD_AXIS_LEFT_TRIGGER));
        result.rightTrigger = NormalizeTrigger(SDL_GetGamepadAxis(m_gamepad, SDL_GAMEPAD_AXIS_RIGHT_TRIGGER));
        result.sequence = ++m_sequence;
        return result;
#endif
    }

    bool SdlGamepadBackend::IsConnected() const
    {
#if defined(__ANDROID__) || defined(__OHOS__)
        return false;
#else
        return m_gamepad != nullptr && SDL_GamepadConnected(m_gamepad);
#endif
    }

    std::string SdlGamepadBackend::GetDeviceName() const
    {
#if defined(__ANDROID__) || defined(__OHOS__)
        return {};
#else
        if (!m_gamepad) return {};
        const char* name = SDL_GetGamepadName(m_gamepad);
        return name ? name : "Gamepad";
#endif
    }

    void SdlGamepadBackend::HandleEvent(const SDL_Event& event)
    {
#if defined(__ANDROID__) || defined(__OHOS__)
        (void)event; // touch-only build ignores controller events
        return;
#else
        switch (event.type)
        {
        case SDL_EVENT_GAMEPAD_ADDED:
            if (!IsConnected()) OpenDevice(event.gdevice.which);
            break;
        case SDL_EVENT_GAMEPAD_REMOVED:
            if (event.gdevice.which == m_activeDeviceId)
            {
                CloseDevice();
                RefreshDevices();
            }
            break;
        case SDL_EVENT_GAMEPAD_BUTTON_DOWN:
            if (event.gbutton.which != m_activeDeviceId)
                OpenDevice(event.gbutton.which);
            break;
        case SDL_EVENT_GAMEPAD_AXIS_MOTION:
            if (event.gaxis.which != m_activeDeviceId
                && std::abs(event.gaxis.value) >= ActivityAxisThreshold)
            {
                OpenDevice(event.gaxis.which);
            }
            break;
        default:
            break;
        }
#endif
    }

    GamepadIconFamily SdlGamepadBackend::GetIconFamily() const
    {
#if defined(__ANDROID__) || defined(__OHOS__)
        return GamepadIconFamily::Generic;
#else
        if (!m_gamepad) return GamepadIconFamily::Generic;
        switch (SDL_GetGamepadType(m_gamepad))
        {
        case SDL_GAMEPAD_TYPE_PS3:
        case SDL_GAMEPAD_TYPE_PS4:
        case SDL_GAMEPAD_TYPE_PS5:
            return GamepadIconFamily::PlayStation;
        case SDL_GAMEPAD_TYPE_NINTENDO_SWITCH_PRO:
        case SDL_GAMEPAD_TYPE_NINTENDO_SWITCH_JOYCON_LEFT:
        case SDL_GAMEPAD_TYPE_NINTENDO_SWITCH_JOYCON_RIGHT:
        case SDL_GAMEPAD_TYPE_NINTENDO_SWITCH_JOYCON_PAIR:
            return GamepadIconFamily::Nintendo;
        case SDL_GAMEPAD_TYPE_XBOX360:
        case SDL_GAMEPAD_TYPE_XBOXONE:
            return GamepadIconFamily::Xbox;
        default:
            return GamepadIconFamily::Generic;
        }
#endif
    }

    bool SdlGamepadBackend::SupportsRumble() const
    {
        const bool gamepadRumble = m_gamepad != nullptr && SDL_GetBooleanProperty(
            SDL_GetGamepadProperties(m_gamepad),
            SDL_PROP_GAMEPAD_CAP_RUMBLE_BOOLEAN,
            false);
#if defined(__ANDROID__) || defined(__OHOS__)
        return gamepadRumble || m_systemHaptic != nullptr;
#else
        return gamepadRumble;
#endif
    }

    void SdlGamepadBackend::Play(
        std::uint16_t lowFrequency,
        std::uint16_t highFrequency,
        std::uint32_t durationMs)
    {
        if (m_gamepad)
        {
            SDL_RumbleGamepad(m_gamepad, lowFrequency, highFrequency, durationMs);
#ifdef MU_ENABLE_VIRTUAL_GAMEPAD_TESTS
            RecordVirtualRumble(lowFrequency, highFrequency, durationMs);
#endif
        }
#if defined(__ANDROID__) || defined(__OHOS__)
        if (m_systemHaptic != nullptr)
        {
            const float strength = std::max(lowFrequency, highFrequency) / 65535.0f;
            SDL_PlayHapticRumble(m_systemHaptic, strength, durationMs);
        }
#endif
    }

    void SdlGamepadBackend::Stop()
    {
        if (m_gamepad)
        {
            SDL_RumbleGamepad(m_gamepad, 0, 0, 0);
#ifdef MU_ENABLE_VIRTUAL_GAMEPAD_TESTS
            RecordVirtualRumble(0, 0, 0);
#endif
        }
#if defined(__ANDROID__) || defined(__OHOS__)
        if (m_systemHaptic != nullptr)
            SDL_StopHapticRumble(m_systemHaptic);
#endif
    }

#if !defined(__ANDROID__) && !defined(__OHOS__)
    bool SdlGamepadBackend::OpenDevice(SDL_JoystickID deviceId)
    {
        if (deviceId == 0) return false;
        if (deviceId == m_activeDeviceId && IsConnected()) return true;

        SDL_Gamepad* next = SDL_OpenGamepad(deviceId);
        if (!next) return false;

        CloseDevice();
        m_gamepad = next;
        m_activeDeviceId = deviceId;
        return true;
    }

    void SdlGamepadBackend::CloseDevice()
    {
        if (!m_gamepad)
        {
            m_activeDeviceId = 0;
            return;
        }

        SDL_RumbleGamepad(m_gamepad, 0, 0, 0);
#ifdef MU_ENABLE_VIRTUAL_GAMEPAD_TESTS
        RecordVirtualRumble(0, 0, 0);
#endif
        SDL_CloseGamepad(m_gamepad);
        m_gamepad = nullptr;
        m_activeDeviceId = 0;
    }

    float SdlGamepadBackend::NormalizeStick(Sint16 value)
    {
        const float divisor = value < 0 ? AxisNegativeMaximum : AxisPositiveMaximum;
        return std::clamp(static_cast<float>(value) / divisor, -1.0f, 1.0f);
    }

    float SdlGamepadBackend::NormalizeTrigger(Sint16 value)
    {
        return std::clamp(static_cast<float>(std::max<Sint16>(value, 0)) / AxisPositiveMaximum, 0.0f, 1.0f);
    }
#endif

#ifdef MU_ENABLE_VIRTUAL_GAMEPAD_TESTS
    bool SdlGamepadBackend::AttachVirtualTestDevice()
    {
        if (m_virtualDeviceId != 0 || m_virtualStatePath.empty()) return true;

        SDL_VirtualJoystickDesc description;
        SDL_INIT_INTERFACE(&description);
        description.type = SDL_JOYSTICK_TYPE_GAMEPAD;
        description.vendor_id = 0xFFFF;
        description.product_id = 0x0001;
        description.naxes = SDL_GAMEPAD_AXIS_COUNT;
        description.nbuttons = SDL_GAMEPAD_BUTTON_COUNT;
        description.button_mask = (1u << SDL_GAMEPAD_BUTTON_COUNT) - 1u;
        description.axis_mask = (1u << SDL_GAMEPAD_AXIS_COUNT) - 1u;
        description.name = "MuMain Virtual Acceptance Gamepad";
        description.Rumble = [](void*, Uint16, Uint16) { return true; };

        m_virtualDeviceId = SDL_AttachVirtualJoystick(&description);
        if (m_virtualDeviceId == 0) return false;

        m_virtualJoystick = SDL_OpenJoystick(m_virtualDeviceId);
        if (!m_virtualJoystick)
        {
            SDL_DetachVirtualJoystick(m_virtualDeviceId);
            m_virtualDeviceId = 0;
            return false;
        }
        return true;
    }

    void SdlGamepadBackend::DetachVirtualTestDevice()
    {
        if (m_virtualDeviceId == 0) return;

        if (m_activeDeviceId == m_virtualDeviceId)
            CloseDevice();
        if (m_virtualJoystick)
        {
            SDL_CloseJoystick(m_virtualJoystick);
            m_virtualJoystick = nullptr;
        }
        SDL_DetachVirtualJoystick(m_virtualDeviceId);
        m_virtualDeviceId = 0;
    }

    void SdlGamepadBackend::UpdateVirtualTestDevice()
    {
        if (m_virtualStatePath.empty()) return;

        const auto readInt = [this](const char* narrowKey, const wchar_t* wideKey, int fallback)
        {
#ifdef _WIN32
            // Keep the acceptance input file read-only in this process and use
            // the same profile API family as the external Windows driver. The
            // W API maintains a separate cache which can retain old samples.
            return GetPrivateProfileIntA(
                "Gamepad", narrowKey, fallback, m_virtualStatePathNarrow.c_str());
#else
            return GetPrivateProfileIntW(
                L"Gamepad", wideKey, fallback, m_virtualStatePath.c_str());
#endif
        };

        const bool shouldBeConnected = readInt("Connected", L"Connected", 1) != 0;
        if (!shouldBeConnected)
        {
            DetachVirtualTestDevice();
            return;
        }
        if (!AttachVirtualTestDevice()) return;

        struct VirtualIniKey
        {
            const char* narrow;
            const wchar_t* wide;
        };
        constexpr std::array<VirtualIniKey, SDL_GAMEPAD_AXIS_COUNT> axisKeys = {
            VirtualIniKey{ "LeftX", L"LeftX" },
            VirtualIniKey{ "LeftY", L"LeftY" },
            VirtualIniKey{ "RightX", L"RightX" },
            VirtualIniKey{ "RightY", L"RightY" },
            VirtualIniKey{ "LeftTrigger", L"LeftTrigger" },
            VirtualIniKey{ "RightTrigger", L"RightTrigger" },
        };
        for (int axis = 0; axis < SDL_GAMEPAD_AXIS_COUNT; ++axis)
        {
            const int defaultValue = axis >= SDL_GAMEPAD_AXIS_LEFT_TRIGGER
                ? SDL_JOYSTICK_AXIS_MIN
                : 0;
            const int value = std::clamp(
                static_cast<int>(readInt(
                    axisKeys[axis].narrow, axisKeys[axis].wide, defaultValue)),
                static_cast<int>(SDL_JOYSTICK_AXIS_MIN),
                static_cast<int>(SDL_JOYSTICK_AXIS_MAX));
            SDL_SetJoystickVirtualAxis(m_virtualJoystick, axis, static_cast<Sint16>(value));
        }

        constexpr std::array<VirtualIniKey, SDL_GAMEPAD_BUTTON_COUNT> buttonKeys = {
            VirtualIniKey{ "South", L"South" },
            VirtualIniKey{ "East", L"East" },
            VirtualIniKey{ "West", L"West" },
            VirtualIniKey{ "North", L"North" },
            VirtualIniKey{ "Back", L"Back" },
            VirtualIniKey{ "Guide", L"Guide" },
            VirtualIniKey{ "Start", L"Start" },
            VirtualIniKey{ "LeftStick", L"LeftStick" },
            VirtualIniKey{ "RightStick", L"RightStick" },
            VirtualIniKey{ "LeftShoulder", L"LeftShoulder" },
            VirtualIniKey{ "RightShoulder", L"RightShoulder" },
            VirtualIniKey{ "DpadUp", L"DpadUp" },
            VirtualIniKey{ "DpadDown", L"DpadDown" },
            VirtualIniKey{ "DpadLeft", L"DpadLeft" },
            VirtualIniKey{ "DpadRight", L"DpadRight" },
            VirtualIniKey{ "Misc1", L"Misc1" },
            VirtualIniKey{ "RightPaddle1", L"RightPaddle1" },
            VirtualIniKey{ "LeftPaddle1", L"LeftPaddle1" },
            VirtualIniKey{ "RightPaddle2", L"RightPaddle2" },
            VirtualIniKey{ "LeftPaddle2", L"LeftPaddle2" },
            VirtualIniKey{ "Touchpad", L"Touchpad" },
            VirtualIniKey{ "Misc2", L"Misc2" },
            VirtualIniKey{ "Misc3", L"Misc3" },
            VirtualIniKey{ "Misc4", L"Misc4" },
            VirtualIniKey{ "Misc5", L"Misc5" },
            VirtualIniKey{ "Misc6", L"Misc6" },
        };
        static_assert(buttonKeys.size() == SDL_GAMEPAD_BUTTON_COUNT);
        for (int button = 0; button < SDL_GAMEPAD_BUTTON_COUNT; ++button)
        {
            const bool down = readInt(
                buttonKeys[button].narrow, buttonKeys[button].wide, 0) != 0;
            SDL_SetJoystickVirtualButton(m_virtualJoystick, button, down);
        }

        // Publish the injected sample before Poll() reads it through the
        // SDL_Gamepad handle. The unit-test path performs this explicitly too.
        SDL_UpdateJoysticks();
    }

    void SdlGamepadBackend::RecordVirtualRumble(
        std::uint16_t lowFrequency,
        std::uint16_t highFrequency,
        std::uint32_t durationMs)
    {
        if (m_virtualStatePath.empty() || m_activeDeviceId != m_virtualDeviceId)
            return;

        const std::wstring low = std::to_wstring(lowFrequency);
        const std::wstring high = std::to_wstring(highFrequency);
        const std::wstring duration = std::to_wstring(durationMs);
        const std::wstring sequence = std::to_wstring(++m_virtualRumbleSequence);
        WritePrivateProfileStringW(L"Rumble", L"Low", low.c_str(), m_virtualStatePath.c_str());
        WritePrivateProfileStringW(L"Rumble", L"High", high.c_str(), m_virtualStatePath.c_str());
        WritePrivateProfileStringW(L"Rumble", L"DurationMs", duration.c_str(), m_virtualStatePath.c_str());
        WritePrivateProfileStringW(L"Rumble", L"Sequence", sequence.c_str(), m_virtualStatePath.c_str());
    }
#endif
}
