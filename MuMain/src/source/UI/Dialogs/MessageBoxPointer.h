#pragma once

#include <array>
#include <cstddef>
#include <cstdint>

namespace UI::Dialogs
{
    class MessageBoxPointer
    {
    public:
        enum class Event { None, Hover, LeftDown, LeftUp, RightDown, RightUp };

        bool IsOwner(std::uintptr_t owner) const { return m_owner == owner; }

        void Bind(std::uintptr_t owner, bool leftHeld, bool rightHeld, bool discardCurrentEdges = false)
        {
            Reset();
            m_owner = owner;
            m_leftWasHeld = m_rawLeftHeld = leftHeld;
            m_rightWasHeld = m_rawRightHeld = rightHeld;
            m_suppressed = leftHeld || rightHeld || discardCurrentEdges;
        }

        void RecordButton(bool left, bool down, bool inside, int x, int y)
        {
            const bool wasHeld = m_rawLeftHeld || m_rawRightHeld;
            bool& held = left ? m_rawLeftHeld : m_rawRightHeld;
            const bool changed = held != down;
            held = down;
            m_rawInput = true;
            if (m_suppressed)
            {
                if (!wasHeld && down) m_suppressed = false;
                else
                {
                    m_suppressed = m_rawLeftHeld || m_rawRightHeld;
                    return;
                }
            }
            if (!changed) return;
            const State button = left ? State::Left : State::Right;
            if (down)
            {
                // Keep only the first contact until its edges have been consumed.
                if (!inside || m_pendingCount != 0 || m_state == State::Left || m_state == State::Right)
                    return;
                m_state = button;
                Queue(left ? Event::LeftDown : Event::RightDown, x, y);
                return;
            }
            if (m_state != button) return;
            m_state = inside ? State::Hover : State::None;
            if (inside) Queue(left ? Event::LeftUp : Event::RightUp, x, y);
        }

        void Cancel()
        {
            const auto owner = m_owner;
            Bind(owner, false, false, true);
            m_rawInput = true;
        }

        bool EventPosition(int& x, int& y) const
        {
            if (!m_hasEventPosition) return false;
            x = m_eventX;
            y = m_eventY;
            return true;
        }

        Event Update(std::uintptr_t owner, bool inside, bool leftHeld, bool leftReleased,
            bool rightHeld, bool rightReleased)
        {
            m_hasEventPosition = false;
            if (owner != m_owner) Bind(owner, leftHeld, rightHeld);
            if (m_pendingCount != 0) return TakePendingEvent();
            if (m_rawInput)
            {
                m_leftWasHeld = leftHeld;
                m_rightWasHeld = rightHeld;
                if (!leftHeld && !rightHeld && m_state != State::Left && m_state != State::Right)
                {
                    m_rawInput = false;
                    m_suppressed = false;
                }
                return Event::None;
            }
            const bool leftPressed = leftHeld && !m_leftWasHeld;
            const bool rightPressed = rightHeld && !m_rightWasHeld;
            m_leftWasHeld = leftHeld;
            m_rightWasHeld = rightHeld;
            if (m_suppressed)
            {
                m_suppressed = leftHeld || rightHeld;
                return Event::None;
            }
            if (m_state == State::Left || m_state == State::Right)
                return UpdateHeld(inside, leftHeld, leftReleased, rightHeld, rightReleased);
            if (!inside)
            {
                m_state = State::None;
                return Event::None;
            }
            if (leftPressed || rightPressed)
            {
                m_state = leftPressed ? State::Left : State::Right;
                return leftPressed ? Event::LeftDown : Event::RightDown;
            }
            if (leftHeld || rightHeld) return Event::None;
            const bool entered = m_state != State::Hover;
            m_state = State::Hover;
            return entered ? Event::Hover : Event::None;
        }

        bool CapturesPointer() const
        {
            return m_pendingCount != 0 || m_suppressed || m_state == State::Left || m_state == State::Right;
        }
        void Reset()
        {
            m_owner = 0;
            m_state = State::None;
            m_suppressed = false;
            m_leftWasHeld = m_rightWasHeld = false;
            m_rawLeftHeld = m_rawRightHeld = m_rawInput = false;
            m_pendingCount = 0;
            m_hasEventPosition = false;
        }

    private:
        enum class State { None, Hover, Left, Right };

        struct PendingEvent
        {
            Event event;
            int x;
            int y;
        };

        void Queue(Event event, int x, int y)
        {
            if (m_pendingCount < m_pending.size())
                m_pending[m_pendingCount++] = {event, x, y};
        }

        Event TakePendingEvent()
        {
            const auto event = m_pending[0];
            if (--m_pendingCount != 0) m_pending[0] = m_pending[1];
            m_hasEventPosition = true;
            m_eventX = event.x;
            m_eventY = event.y;
            return event.event;
        }

        Event UpdateHeld(bool inside, bool leftHeld, bool leftReleased, bool rightHeld, bool rightReleased)
        {
            const bool left = m_state == State::Left;
            const bool held = left ? leftHeld : rightHeld;
            const bool released = left ? leftReleased : rightReleased;
            if (held && !released) return Event::None;
            m_state = inside ? State::Hover : State::None;
            if (!inside || !released) return Event::None;
            return left ? Event::LeftUp : Event::RightUp;
        }

        std::uintptr_t m_owner{};
        State m_state{State::None};
        bool m_suppressed{};
        bool m_leftWasHeld{};
        bool m_rightWasHeld{};
        bool m_rawLeftHeld{};
        bool m_rawRightHeld{};
        bool m_rawInput{};
        std::array<PendingEvent, 2> m_pending{};
        std::size_t m_pendingCount{};
        bool m_hasEventPosition{};
        int m_eventX{};
        int m_eventY{};
    };
}
