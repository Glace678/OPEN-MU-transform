#include "Core/Input/MobileGestureMapper.h"

#include <algorithm>
#include <cmath>
#include <iterator>

namespace Core::Input
{
    namespace
    {
        constexpr std::uint64_t TapDurationMs = 300;
        constexpr std::uint64_t DoubleTapWindowMs = 330;
        constexpr float TapTravel = 0.04f;
        constexpr float DoubleTapDistance = 0.065f;
        constexpr float SwipeStep = 0.075f;
        constexpr float PinchStep = 0.055f;
        constexpr float ThreeFingerSwipe = 0.12f;

        float Distance(float x1, float y1, float x2, float y2)
        {
            return std::hypot(x2 - x1, y2 - y1);
        }
    }

    MobileGestureMapper::Finger* MobileGestureMapper::Find(std::int64_t fingerId)
    {
        for (auto& finger : m_fingers)
        {
            if (finger.active && finger.id == fingerId)
                return &finger;
        }
        return nullptr;
    }

    const MobileGestureMapper::Finger* MobileGestureMapper::Find(std::int64_t fingerId) const
    {
        for (const auto& finger : m_fingers)
        {
            if (finger.active && finger.id == fingerId)
                return &finger;
        }
        return nullptr;
    }

    MobileGestureMapper::Finger* MobileGestureMapper::FirstFree()
    {
        for (auto& finger : m_fingers)
        {
            if (!finger.active)
                return &finger;
        }
        return nullptr;
    }

    std::size_t MobileGestureMapper::ActiveCount() const
    {
        return static_cast<std::size_t>(std::count_if(
            std::begin(m_fingers), std::end(m_fingers),
            [](const Finger& finger) { return finger.active; }));
    }

    bool MobileGestureMapper::IsActionRegion(float x) const
    {
        return m_leftHanded ? x <= 0.55f : x >= 0.45f;
    }

    void MobileGestureMapper::SetWorldGesturesEnabled(bool enabled)
    {
        if (m_worldGesturesEnabled != enabled)
            m_lastRightTapAtMs = 0;
        m_worldGesturesEnabled = enabled;
    }

    void MobileGestureMapper::CaptureNextContact()
    {
        if (ActiveCount() != 0) return;
        m_captureNextContact = true;
        m_lastRightTapAtMs = 0;
    }

    void MobileGestureMapper::ReleaseActiveButton(
        MobileGestureActions& actions, float x, float y)
    {
        if (m_mode == Mode::SingleLeft)
            actions.push_back({MobileGestureActionType::LeftButtonUp, x, y});
        else if (m_mode == Mode::SingleRight)
            actions.push_back({MobileGestureActionType::RightButtonUp, x, y});
    }

    void MobileGestureMapper::CancelActiveButton(
        MobileGestureActions& actions, float x, float y)
    {
        if (m_mode == Mode::SingleLeft)
            actions.push_back({MobileGestureActionType::CancelLeftButton, x, y});
        else if (m_mode == Mode::SingleRight)
            actions.push_back({MobileGestureActionType::CancelRightButton, x, y});
    }

    void MobileGestureMapper::StartMultiGesture()
    {
        const Finger* first = nullptr;
        const Finger* second = nullptr;
        for (const auto& finger : m_fingers)
        {
            if (!finger.active)
                continue;
            if (first == nullptr)
                first = &finger;
            else
            {
                second = &finger;
                break;
            }
        }
        if (first == nullptr || second == nullptr)
            return;
        m_anchorX = (first->x + second->x) * 0.5f;
        m_anchorY = (first->y + second->y) * 0.5f;
        m_anchorDistance = Distance(first->x, first->y, second->x, second->y);
        m_mode = Mode::Multi;
    }

    void MobileGestureMapper::StartThreeFingerGesture()
    {
        float x = 0.0f;
        float y = 0.0f;
        int count = 0;
        for (const auto& finger : m_fingers)
        {
            if (!finger.active)
                continue;
            x += finger.x;
            y += finger.y;
            ++count;
        }
        if (count == 0)
            return;
        m_threeStartX = x / static_cast<float>(count);
        m_threeStartY = y / static_cast<float>(count);
        m_threeActionSent = false;
        m_mode = Mode::Three;
    }

    MobileGestureActions MobileGestureMapper::Handle(const TouchSample& sample)
    {
        if (sample.phase == TouchPhase::Down)
            return HandleDown(sample);

        MobileGestureActions actions;
        if (sample.phase == TouchPhase::Cancel)
        {
            CancelActiveButton(actions, sample.x, sample.y);
            Reset();
            return actions;
        }

        Finger* finger = Find(sample.fingerId);
        if (finger == nullptr)
            return actions;
        finger->x = std::clamp(sample.x, 0.0f, 1.0f);
        finger->y = std::clamp(sample.y, 0.0f, 1.0f);

        if (sample.phase == TouchPhase::Move)
            return HandleMove(*finger);
        if (sample.phase == TouchPhase::Up)
            return HandleUp(*finger, sample);
        return actions;
    }

    MobileGestureActions MobileGestureMapper::HandleDown(const TouchSample& sample)
    {
        MobileGestureActions actions;
        Finger* finger = FirstFree();
        if (finger == nullptr)
            return actions;
        *finger = {true, sample.fingerId, sample.x, sample.y,
            sample.x, sample.y, sample.timestampMs};

        const std::size_t count = ActiveCount();
        if (count == 1)
        {
            actions.push_back({MobileGestureActionType::PointerMove, sample.x, sample.y});
            m_capturedSequence = m_captureNextContact;
            m_captureNextContact = false;
            if (m_capturedSequence)
            {
                m_mode = Mode::Captured;
                return actions;
            }
            const bool closeInTime = m_lastRightTapAtMs > 0
                && sample.timestampMs >= m_lastRightTapAtMs
                && sample.timestampMs - m_lastRightTapAtMs <= DoubleTapWindowMs;
            const bool closeInSpace = Distance(sample.x, sample.y,
                m_lastRightTapX, m_lastRightTapY) <= DoubleTapDistance;
            if (m_worldGesturesEnabled && IsActionRegion(sample.x) && closeInTime && closeInSpace)
            {
                m_mode = Mode::SingleRight;
                m_lastRightTapAtMs = 0;
                actions.push_back({MobileGestureActionType::RightButtonDown, sample.x, sample.y});
            }
            else
            {
                m_mode = Mode::SingleLeft;
                actions.push_back({MobileGestureActionType::LeftButtonDown, sample.x, sample.y});
            }
        }
        else if (count == 2)
        {
            CancelActiveButton(actions, sample.x, sample.y);
            if (m_worldGesturesEnabled && !m_capturedSequence)
                StartMultiGesture();
            else
                m_mode = Mode::Suppressed;
        }
        else if (count == 3)
        {
            CancelActiveButton(actions, sample.x, sample.y);
            if (m_worldGesturesEnabled && !m_capturedSequence)
                StartThreeFingerGesture();
            else
                m_mode = Mode::Suppressed;
        }
        else
        {
            m_mode = Mode::Suppressed;
        }
        return actions;
    }

    MobileGestureActions MobileGestureMapper::HandleMove(Finger& finger)
    {
        MobileGestureActions actions;
        if (m_mode == Mode::SingleLeft || m_mode == Mode::SingleRight || m_mode == Mode::Captured)
            actions.push_back({MobileGestureActionType::PointerMove, finger.x, finger.y});
        else if (m_mode == Mode::Multi && ActiveCount() == 2)
            AppendMultiGestureAction(actions);
        return actions;
    }

    MobileGestureActions MobileGestureMapper::HandleUp(
        Finger& finger,
        const TouchSample& sample)
    {
        MobileGestureActions actions;
        if (m_mode == Mode::Captured)
            actions.push_back({MobileGestureActionType::PointerMove, finger.x, finger.y});
        if (m_mode == Mode::SingleLeft || m_mode == Mode::SingleRight)
        {
            actions.push_back({MobileGestureActionType::PointerMove, finger.x, finger.y});
            ReleaseActiveButton(actions, finger.x, finger.y);
            const bool wasLeft = m_mode == Mode::SingleLeft;
            const bool wasTap = sample.timestampMs >= finger.downAtMs
                && sample.timestampMs - finger.downAtMs <= TapDurationMs
                && Distance(finger.startX, finger.startY, finger.x, finger.y) <= TapTravel;
            if (m_worldGesturesEnabled && wasLeft && wasTap && IsActionRegion(finger.x))
            {
                m_lastRightTapAtMs = sample.timestampMs;
                m_lastRightTapX = finger.x;
                m_lastRightTapY = finger.y;
            }
        }
        else if (m_mode == Mode::Three && !m_threeActionSent)
        {
            AppendThreeFingerAction(actions);
        }

        finger.active = false;
        if (ActiveCount() == 0)
        {
            m_mode = Mode::Idle;
            m_capturedSequence = false;
        }
        else if (m_mode != Mode::Three)
            m_mode = Mode::Suppressed;
        return actions;
    }

    void MobileGestureMapper::AppendMultiGestureAction(
        MobileGestureActions& actions)
    {
        const Finger* first = nullptr;
        const Finger* second = nullptr;
        for (const auto& active : m_fingers)
        {
            if (!active.active)
                continue;
            if (first == nullptr)
                first = &active;
            else
            {
                second = &active;
                break;
            }
        }
        if (first == nullptr || second == nullptr)
            return;

        const float centerX = (first->x + second->x) * 0.5f;
        const float centerY = (first->y + second->y) * 0.5f;
        const float distance = Distance(first->x, first->y, second->x, second->y);
        const float pinch = distance - m_anchorDistance;
        const float dx = centerX - m_anchorX;
        const float dy = centerY - m_anchorY;

        if (std::abs(pinch) >= PinchStep && std::hypot(dx, dy) <= PinchStep)
        {
            actions.push_back({pinch > 0
                ? MobileGestureActionType::ZoomIn
                : MobileGestureActionType::ZoomOut, centerX, centerY});
            m_anchorDistance = distance;
            m_anchorX = centerX;
            m_anchorY = centerY;
        }
        else if (std::abs(dx) >= SwipeStep && std::abs(dx) > std::abs(dy))
        {
            actions.push_back({dx > 0
                ? MobileGestureActionType::NextSkill
                : MobileGestureActionType::PreviousSkill, centerX, centerY});
            m_anchorX = centerX;
            m_anchorY = centerY;
        }
        else if (std::abs(dy) >= SwipeStep)
        {
            actions.push_back({dy < 0
                ? MobileGestureActionType::ZoomIn
                : MobileGestureActionType::ZoomOut, centerX, centerY});
            m_anchorX = centerX;
            m_anchorY = centerY;
        }
    }

    void MobileGestureMapper::AppendThreeFingerAction(
        MobileGestureActions& actions)
    {
        float x = 0.0f;
        float y = 0.0f;
        int count = 0;
        for (const auto& active : m_fingers)
        {
            if (!active.active)
                continue;
            x += active.x;
            y += active.y;
            ++count;
        }
        if (count == 0)
            return;

        const float centerX = x / static_cast<float>(count);
        const float centerY = y / static_cast<float>(count);
        const float dy = centerY - m_threeStartY;
        const float dx = centerX - m_threeStartX;
        if (std::abs(dy) < ThreeFingerSwipe || std::abs(dy) <= std::abs(dx))
            return;

        actions.push_back({dy < 0
            ? MobileGestureActionType::OpenMap
            : MobileGestureActionType::OpenSettings, centerX, centerY});
        m_threeActionSent = true;
    }

    void MobileGestureMapper::Reset()
    {
        for (auto& finger : m_fingers)
            finger = {};
        m_mode = Mode::Idle;
        m_anchorX = m_anchorY = m_anchorDistance = 0.0f;
        m_threeStartX = m_threeStartY = 0.0f;
        m_threeActionSent = false;
        m_captureNextContact = false;
        m_capturedSequence = false;
        m_lastRightTapAtMs = 0;
    }
}
