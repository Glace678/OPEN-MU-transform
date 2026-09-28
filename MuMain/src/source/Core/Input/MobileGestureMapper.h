#pragma once

#include <array>
#include <cstddef>
#include <cstdint>

namespace Core::Input
{
    enum class TouchPhase
    {
        Down,
        Move,
        Up,
        Cancel,
    };

    struct TouchSample
    {
        std::int64_t fingerId{};
        TouchPhase phase{TouchPhase::Move};
        float x{};
        float y{};
        std::uint64_t timestampMs{};
    };

    enum class MobileGestureActionType
    {
        PointerMove,
        LeftButtonDown,
        LeftButtonUp,
        RightButtonDown,
        RightButtonUp,
        CancelLeftButton,
        CancelRightButton,
        PreviousSkill,
        NextSkill,
        ZoomIn,
        ZoomOut,
        OpenMap,
        OpenSettings,
    };

    struct MobileGestureAction
    {
        MobileGestureActionType type{MobileGestureActionType::PointerMove};
        float x{};
        float y{};
    };

    class MobileGestureActions
    {
    public:
        using const_iterator = const MobileGestureAction*;

        void push_back(const MobileGestureAction& action)
        {
            if (m_size >= Capacity)
                return;
            m_actions[m_size++] = action;
        }
        bool empty() const { return m_size == 0; }
        std::size_t size() const { return m_size; }
        const MobileGestureAction& front() const { return m_actions.front(); }
        const_iterator begin() const { return m_actions.data(); }
        const_iterator end() const { return m_actions.data() + m_size; }

    private:
        static constexpr std::size_t Capacity = 3;
        std::array<MobileGestureAction, Capacity> m_actions{};
        std::size_t m_size{};
    };

    class MobileGestureMapper
    {
    public:
        MobileGestureActions Handle(const TouchSample& sample);
        void SetLeftHanded(bool leftHanded) { m_leftHanded = leftHanded; }
        void SetWorldGesturesEnabled(bool enabled);
        void CaptureNextContact();
        bool HasActiveContacts() const { return ActiveCount() != 0; }
        void Reset();

    private:
        struct Finger;
        enum class Mode;

        Finger* Find(std::int64_t fingerId);
        const Finger* Find(std::int64_t fingerId) const;
        std::size_t ActiveCount() const;
        void StartMultiGesture();
        void StartThreeFingerGesture();
        MobileGestureActions HandleDown(const TouchSample& sample);
        MobileGestureActions HandleMove(Finger& finger);
        MobileGestureActions HandleUp(Finger& finger, const TouchSample& sample);
        void AppendMultiGestureAction(MobileGestureActions& actions);
        void AppendThreeFingerAction(MobileGestureActions& actions);
        void ReleaseActiveButton(MobileGestureActions& actions, float x, float y);
        void CancelActiveButton(MobileGestureActions& actions, float x, float y);
        bool IsActionRegion(float x) const;

        static constexpr std::size_t MaxFingers = 10;
        Finger* FirstFree();

        struct Finger
        {
            bool active{};
            std::int64_t id{};
            float startX{};
            float startY{};
            float x{};
            float y{};
            std::uint64_t downAtMs{};
        };

        enum class Mode
        {
            Idle,
            SingleLeft,
            SingleRight,
            Captured,
            Multi,
            Three,
            Suppressed,
        };

        Finger m_fingers[MaxFingers]{};
        Mode m_mode{Mode::Idle};
        float m_anchorX{};
        float m_anchorY{};
        float m_anchorDistance{};
        float m_threeStartX{};
        float m_threeStartY{};
        bool m_threeActionSent{};
        std::uint64_t m_lastRightTapAtMs{};
        float m_lastRightTapX{};
        float m_lastRightTapY{};
        bool m_leftHanded{};
        bool m_worldGesturesEnabled{true};
        bool m_captureNextContact{};
        bool m_capturedSequence{};
    };
}
