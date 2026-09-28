#pragma once

#include "Core/Haptics/Haptics.h"
#include "Core/Input/GamepadTypes.h"

#include <SDL3/SDL.h>

namespace Core::Input
{
    enum class GamepadIconFamily
    {
        Xbox,
        PlayStation,
        Nintendo,
        Generic,
    };

    class SdlGamepadBackend final
        : public IGamepadBackend
        , public Core::Haptics::IHapticOutput
    {
    public:
        ~SdlGamepadBackend() override;

        bool Initialize() override;
        void Shutdown() override;
        void RefreshDevices() override;
        GamepadSnapshot Poll() override;
        bool IsConnected() const override;
        std::string GetDeviceName() const override;

        void HandleEvent(const SDL_Event& event);
        SDL_JoystickID GetActiveDeviceId() const { return m_activeDeviceId; }
        SDL_Gamepad* GetActiveGamepad() const { return m_gamepad; }
        GamepadIconFamily GetIconFamily() const;

        bool SupportsRumble() const override;
        void Play(std::uint16_t lowFrequency, std::uint16_t highFrequency, std::uint32_t durationMs) override;
        void Stop() override;

    private:
        bool OpenDevice(SDL_JoystickID deviceId);
        void CloseDevice();
        static float NormalizeStick(Sint16 value);
        static float NormalizeTrigger(Sint16 value);

#ifdef MU_ENABLE_VIRTUAL_GAMEPAD_TESTS
        bool AttachVirtualTestDevice();
        void DetachVirtualTestDevice();
        void UpdateVirtualTestDevice();
        void RecordVirtualRumble(
            std::uint16_t lowFrequency,
            std::uint16_t highFrequency,
            std::uint32_t durationMs);

        SDL_JoystickID m_virtualDeviceId = 0;
        SDL_Joystick* m_virtualJoystick = nullptr;
        std::string m_virtualStatePathNarrow;
        std::wstring m_virtualStatePath;
        std::uint64_t m_virtualRumbleSequence = 0;
#endif

        SDL_Gamepad* m_gamepad = nullptr;
#if defined(__ANDROID__) || defined(__OHOS__)
        SDL_Haptic* m_systemHaptic = nullptr;
        bool m_hapticSubsystemInitialized = false;
#endif
        SDL_JoystickID m_activeDeviceId = 0;
        std::uint64_t m_sequence = 0;
        bool m_initialized = false;
    };
}
