#include "Core/Input/GamepadService.h"

#include <algorithm>
#include <cmath>

namespace Core::Input
{
    namespace
    {
        // Per-control pressed state of a raw snapshot, used by the remap
        // capture. GamepadButton::Guide has no remappable GamepadControl.
        std::array<bool, static_cast<std::size_t>(GamepadControl::Count)>
            SnapshotControlStates(const GamepadSnapshot& snapshot, const GamepadSettings& settings)
        {
            std::array<bool, static_cast<std::size_t>(GamepadControl::Count)> active{};
            auto set = [&active](GamepadControl control, bool down)
            {
                active[static_cast<std::size_t>(control)] = down;
            };

            set(GamepadControl::South, snapshot.buttons[static_cast<std::size_t>(GamepadButton::South)]);
            set(GamepadControl::East, snapshot.buttons[static_cast<std::size_t>(GamepadButton::East)]);
            set(GamepadControl::West, snapshot.buttons[static_cast<std::size_t>(GamepadButton::West)]);
            set(GamepadControl::North, snapshot.buttons[static_cast<std::size_t>(GamepadButton::North)]);
            set(GamepadControl::Back, snapshot.buttons[static_cast<std::size_t>(GamepadButton::Back)]);
            set(GamepadControl::Start, snapshot.buttons[static_cast<std::size_t>(GamepadButton::Start)]);
            set(GamepadControl::LeftStick, snapshot.buttons[static_cast<std::size_t>(GamepadButton::LeftStick)]
                || std::hypot(snapshot.leftX, snapshot.leftY) > settings.stickDeadZone);
            set(GamepadControl::RightStick, snapshot.buttons[static_cast<std::size_t>(GamepadButton::RightStick)]
                || std::hypot(snapshot.rightX, snapshot.rightY) > settings.stickDeadZone);
            set(GamepadControl::LeftShoulder, snapshot.buttons[static_cast<std::size_t>(GamepadButton::LeftShoulder)]);
            set(GamepadControl::RightShoulder, snapshot.buttons[static_cast<std::size_t>(GamepadButton::RightShoulder)]);
            set(GamepadControl::DpadUp, snapshot.buttons[static_cast<std::size_t>(GamepadButton::DpadUp)]);
            set(GamepadControl::DpadDown, snapshot.buttons[static_cast<std::size_t>(GamepadButton::DpadDown)]);
            set(GamepadControl::DpadLeft, snapshot.buttons[static_cast<std::size_t>(GamepadButton::DpadLeft)]);
            set(GamepadControl::DpadRight, snapshot.buttons[static_cast<std::size_t>(GamepadButton::DpadRight)]);
            // Require a deliberate half-pull of an analog trigger rather than
            // the tiny dead-zone threshold used while playing.
            set(GamepadControl::LeftTrigger, snapshot.leftTrigger > std::max(0.5f, settings.triggerDeadZone));
            set(GamepadControl::RightTrigger, snapshot.rightTrigger > std::max(0.5f, settings.triggerDeadZone));
            return active;
        }
    }

    GamepadService& GamepadService::Instance()
    {
        static GamepadService instance;
        return instance;
    }

    GamepadService::GamepadService()
        : m_haptics(m_backend)
    {
    }

    bool GamepadService::Initialize(
        const GamepadSettings& gamepadSettings,
        const Core::Haptics::HapticSettings& hapticSettings)
    {
#if defined(__ANDROID__) || defined(__OHOS__)
        // Touch-only builds: controllers are disabled by construction. The
        // backend still initializes SDL haptics for phone vibration.
        (void)gamepadSettings;
        m_gamepadEnabled = false;
#else
        m_gamepadEnabled = gamepadSettings.enabled;
        m_mapper.SetSettings(gamepadSettings);
#endif
        m_haptics.SetSettings(hapticSettings);
        m_initialized = m_backend.Initialize();
        m_lastDeviceId = m_backend.GetActiveDeviceId();
        m_inputOwnershipChanged = false;
        return m_initialized;
    }

    void GamepadService::Shutdown()
    {
        if (!m_initialized) return;
        m_haptics.Stop();
        m_backend.Shutdown();
        m_initialized = false;
        m_previousUpdateMs = 0.0;
        m_lastDeviceId = 0;
        m_inputOwnershipChanged = false;
        m_frame = {};
    }

    void GamepadService::HandleEvent(const SDL_Event& event)
    {
#if defined(__ANDROID__) || defined(__OHOS__)
        (void)event; // touch-only build: no controller events are consumed
        return;
#else
        if (!m_initialized) return;

        const SDL_JoystickID previousDeviceId = m_backend.GetActiveDeviceId();
        m_backend.HandleEvent(event);
        m_lastDeviceId = m_backend.GetActiveDeviceId();
        if (m_lastDeviceId != previousDeviceId)
        {
            m_inputOwnershipChanged = true;
            m_haptics.Stop();
            m_mapper.Reset(m_frame.pointer.x, m_frame.pointer.y);
        }
#endif
    }

    const GamepadFrameState& GamepadService::Update(
        InputContext context,
        double nowMs,
        float pointerWidth,
        float pointerHeight,
        bool acceptInput)
    {
#if defined(__ANDROID__) || defined(__OHOS__)
        (void)context;
        (void)pointerWidth;
        (void)pointerHeight;
        (void)acceptInput;
        // Touch-only build: keep the frame permanently neutral (no virtual
        // pointer or keys are ever produced) and pump the haptic scheduler
        // so phone vibration from combat/UI/touch still plays.
        m_haptics.Update(nowMs);
        return m_frame;
#else
        const double deltaSeconds = m_previousUpdateMs > 0.0
            ? std::clamp((nowMs - m_previousUpdateMs) / 1000.0, 0.0, 0.1)
            : 0.0;
        m_previousUpdateMs = nowMs;

        const GamepadSnapshot snapshot = m_initialized
            ? m_backend.Poll()
            : GamepadSnapshot{};
        const SDL_JoystickID deviceId = m_backend.GetActiveDeviceId();
        if (deviceId != m_lastDeviceId)
        {
            m_inputOwnershipChanged = true;
            m_haptics.Stop();
            m_mapper.Reset(m_frame.pointer.x, m_frame.pointer.y);
            m_lastDeviceId = deviceId;
        }
        const GamepadFrameState previousFrame = m_frame;
        m_frame = m_mapper.Update(
            snapshot,
            context,
            deltaSeconds,
            pointerWidth,
            pointerHeight,
            m_focused && acceptInput);
        if (m_focused && acceptInput && m_gamepadEnabled)
        {
            PublishInputFeedback(previousFrame, nowMs);
            UpdateControlCapture(snapshot);
        }
        m_haptics.Update(nowMs);
        return m_frame;
#endif
    }

    void GamepadService::PublishInputFeedback(const GamepadFrameState& previous, double nowMs)
    {
        // A low-priority input acknowledgement is distinct from a confirmed
        // hit or successful transaction, which still comes from the server.
        for (std::size_t i = 0; i < m_frame.actions.size(); ++i)
        {
            const auto& action = m_frame.actions[i];
            if (action.pressed || (action.down && !previous.actions[i].down))
            {
                m_haptics.Publish(Core::Haptics::HapticEvent::InputAcknowledged, nowMs);
                return;
            }
        }
    }

    void GamepadService::OnFocusChanged(bool focused)
    {
        m_focused = focused;
        if (!focused)
        {
            m_haptics.Stop();
            m_mapper.Reset(m_frame.pointer.x, m_frame.pointer.y);
        }
    }

    void GamepadService::SetPointerPosition(float pointerX, float pointerY)
    {
        m_mapper.SetPointerPosition(pointerX, pointerY);
        m_frame.pointer.x = pointerX;
        m_frame.pointer.y = pointerY;
    }

    void GamepadService::OnPhysicalPointerInput(float pointerX, float pointerY)
    {
        // Physical pointer activity owns the cursor until every controller
        // control returns neutral, preventing a held A/RT from reclaiming it.
        CancelControlCapture();
        m_frame.pointer.x = pointerX;
        m_frame.pointer.y = pointerY;
        m_mapper.Reset(pointerX, pointerY);
    }

    void GamepadService::RequireNeutralInput()
    {
        m_mapper.Reset(m_frame.pointer.x, m_frame.pointer.y);
    }

    bool GamepadService::PublishHaptic(Core::Haptics::HapticEvent event, double nowMs)
    {
#if defined(__ANDROID__) || defined(__OHOS__)
        return m_focused && m_haptics.Publish(event, nowMs);
#else
        return m_gamepadEnabled && m_focused && m_haptics.Publish(event, nowMs);
#endif
    }

    void GamepadService::SetGamepadSettings(const GamepadSettings& settings)
    {
#if defined(__ANDROID__) || defined(__OHOS__)
        (void)settings; // controllers do not exist on touch-only builds
        return;
#else
        const bool ownershipChanged = settings.enabled != m_gamepadEnabled
            || settings.bindings != m_mapper.GetSettings().bindings;
        m_gamepadEnabled = settings.enabled;
        m_mapper.SetSettings(settings);
        if (ownershipChanged)
        {
            m_mapper.Reset(m_frame.pointer.x, m_frame.pointer.y);
            m_inputOwnershipChanged = true;
        }
        if (!m_gamepadEnabled)
            m_haptics.Stop();
#endif
    }

    void GamepadService::SetHapticSettings(const Core::Haptics::HapticSettings& settings)
    {
        m_haptics.SetSettings(settings);
    }

    bool GamepadService::ConsumeInputOwnershipChanged()
    {
        const bool changed = m_inputOwnershipChanged;
        m_inputOwnershipChanged = false;
        return changed;
    }

    void GamepadService::BeginControlCapture(GamepadControl cancelControl)
    {
        // The Confirm press that armed capture is still held: require a fully
        // neutral sample before listening so it cannot immediately re-bind the
        // same control that opened the capture.
        m_controlCaptureCancel = cancelControl;
        m_capturedControl.reset();
        m_controlCaptureActive.fill(false);
        m_controlCaptureState = ControlCaptureState::WaitingNeutral;
    }

    void GamepadService::CancelControlCapture()
    {
        m_controlCaptureState = ControlCaptureState::Inactive;
        m_capturedControl.reset();
        m_controlCaptureActive.fill(false);
    }

    std::optional<GamepadControl> GamepadService::ConsumeCapturedControl()
    {
        if (!m_capturedControl.has_value())
            return std::nullopt;
        const GamepadControl captured = *m_capturedControl;
        CancelControlCapture();
        return captured;
    }

    void GamepadService::UpdateControlCapture(const GamepadSnapshot& snapshot)
    {
        if (m_controlCaptureState == ControlCaptureState::Inactive)
            return;

        if (!snapshot.connected)
            return;

        const auto active = SnapshotControlStates(snapshot, m_mapper.GetSettings());
        const std::size_t cancelIndex = static_cast<std::size_t>(m_controlCaptureCancel);
        const bool anythingActive = std::any_of(
            active.begin(), active.end(), [](bool down) { return down; });

        if (m_controlCaptureState == ControlCaptureState::WaitingNeutral)
        {
            if (!anythingActive)
            {
                m_controlCaptureActive = active;
                m_controlCaptureState = ControlCaptureState::Armed;
            }
            return;
        }

        // Armed: the cancel control (typically B/East) aborts, any other
        // control newly pressed completes the capture.
        if (active[cancelIndex] && !m_controlCaptureActive[cancelIndex])
        {
            CancelControlCapture();
            return;
        }

        for (std::size_t i = 0; i < active.size(); ++i)
        {
            if (active[i] && !m_controlCaptureActive[i] && i != cancelIndex)
            {
                m_capturedControl = static_cast<GamepadControl>(i);
                m_controlCaptureState = ControlCaptureState::Inactive;
                return;
            }
        }
        m_controlCaptureActive = active;
    }
}
