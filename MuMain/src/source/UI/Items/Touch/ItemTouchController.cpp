#include "UI/Items/Touch/ItemTouchController.h"

#include <cmath>

namespace UI::Items::Touch
{
    bool ItemTouchController::Begin(const Core::Input::TouchSample& contact)
    {
        if (m_captured || contact.phase != Core::Input::TouchPhase::Down) return false;
        const auto token = m_target.Capture(contact.x, contact.y);
        if (!token) return false;
        m_token = *token;
        m_start = contact;
        m_state = State::Pending;
        m_captured = true;
        m_target.Select(m_token);
        return true;
    }

    bool ItemTouchController::Validate()
    {
        if (m_state == State::Used || m_state == State::Cancelled) return false;
        if (m_target.Validate(m_token, m_state == State::Dragging)) return true;
        Cancel();
        return false;
    }

    void ItemTouchController::Handle(const Core::Input::TouchSample& contact)
    {
        if (!m_captured) return;
        if (contact.phase == Core::Input::TouchPhase::Cancel
            || (contact.phase == Core::Input::TouchPhase::Down && contact.fingerId != m_start.fingerId))
        {
            Cancel();
            return;
        }
        if (contact.fingerId != m_start.fingerId || !Validate()) return;
        if (contact.phase == Core::Input::TouchPhase::Move) Move(contact);
        else if (contact.phase == Core::Input::TouchPhase::Up) Release(contact);
    }

    void ItemTouchController::Move(const Core::Input::TouchSample& contact)
    {
        if (m_state != State::Pending) return;
        if (std::hypot(contact.x - m_start.x, contact.y - m_start.y) < DragThreshold) return;
        if (m_token.kind == ItemTouchKind::QuickSlot || !m_target.BeginDrag(m_token))
        {
            Cancel();
            return;
        }
        m_state = State::Dragging;
    }

    void ItemTouchController::Tick(std::uint64_t nowMs)
    {
        if (!m_captured || !Validate() || m_state != State::Pending) return;
        if (m_token.kind != ItemTouchKind::InventoryItem || nowMs < m_start.timestampMs
            || nowMs - m_start.timestampMs < LongPressMs) return;
        // A rejected/non-usable item is still a completed gesture, not a
        // primary release or a fallback equip/sell/drop command.
        m_state = State::Used;
        m_target.Use(m_token);
    }

    void ItemTouchController::Release(const Core::Input::TouchSample& contact)
    {
        // A coalesced stream may deliver the final position only on release.
        Move(contact);
        if (m_state == State::Cancelled) return;
        if (m_state == State::Dragging)
        {
            if (!m_target.PlaceDrag(m_token, contact.x, contact.y)) m_target.RestoreDrag(m_token);
        }
        else if (m_state == State::Pending)
        {
            Tick(contact.timestampMs);
            if (m_state == State::Pending && m_token.kind == ItemTouchKind::QuickSlot
                && contact.timestampMs >= m_start.timestampMs
                && contact.timestampMs - m_start.timestampMs <= QuickTapMs
                && std::hypot(contact.x - m_start.x, contact.y - m_start.y) < DragThreshold)
                m_target.Use(m_token);
        }
        m_state = State::Used;
    }

    void ItemTouchController::Cancel()
    {
        if (m_captured && m_state == State::Dragging) m_target.RestoreDrag(m_token);
        m_state = State::Cancelled;
    }

    void ItemTouchController::FinishContactSequence()
    {
        Cancel();
        m_captured = false;
        m_token = {};
    }
}
