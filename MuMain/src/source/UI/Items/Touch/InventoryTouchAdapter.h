#pragma once

#include "Core/Input/MobileGestureMapper.h"

#include <cstdint>

namespace SEASON3B { class CNewUIInventoryCtrl; }

namespace UI::Items::Touch
{
    bool BeginItemContact(const Core::Input::TouchSample& contact);
    void HandleItemContact(const Core::Input::TouchSample& contact);
    void TickItemContact(std::uint64_t nowMs);
    void FinishItemContactSequence();
    void CancelItemContact();
    void CancelItemContactForOwner(SEASON3B::CNewUIInventoryCtrl* owner);
}
