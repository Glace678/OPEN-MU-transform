#pragma once

#include "Core/Input/MobileGestureMapper.h"

#include <cstdint>
#include <optional>

namespace UI::Items::Touch
{
    enum class ItemTouchKind { QuickSlot, InventoryItem };

    struct ItemTouchToken
    {
        ItemTouchKind kind{ItemTouchKind::InventoryItem};
        std::uintptr_t owner{};
        std::uint32_t key{};
        std::uint32_t draggedKey{};
        int index{-1};
        int quickSlot{-1};
        std::uint64_t context{};
    };

    // The controller owns touch arbitration; the target owns inventory and
    // packet operations. Coordinates are in the game's logical UI space.
    class ItemTouchTarget
    {
    public:
        virtual ~ItemTouchTarget() = default;
        virtual std::optional<ItemTouchToken> Capture(float x, float y) = 0;
        virtual bool Validate(const ItemTouchToken& token, bool dragging) = 0;
        virtual void Select(const ItemTouchToken& token) = 0;
        virtual void Use(const ItemTouchToken& token) = 0;
        virtual bool BeginDrag(ItemTouchToken& token) = 0;
        virtual bool PlaceDrag(const ItemTouchToken& token, float x, float y) = 0;
        virtual void RestoreDrag(const ItemTouchToken& token) = 0;
    };

    class ItemTouchController
    {
    public:
        explicit ItemTouchController(ItemTouchTarget& target) : m_target(target) {}
        bool Begin(const Core::Input::TouchSample& contact);
        void Handle(const Core::Input::TouchSample& contact);
        void Tick(std::uint64_t nowMs);
        void Cancel();
        void FinishContactSequence();
        bool IsCaptured() const { return m_captured; }
        bool Owns(std::uintptr_t owner) const { return m_captured && m_token.owner == owner; }

    private:
        enum class State { Pending, Dragging, Used, Cancelled };
        void Move(const Core::Input::TouchSample& contact);
        void Release(const Core::Input::TouchSample& contact);
        bool Validate();

        static constexpr std::uint64_t LongPressMs = 500;
        static constexpr std::uint64_t QuickTapMs = 300;
        static constexpr float DragThreshold = 6.0f;
        ItemTouchTarget& m_target;
        ItemTouchToken m_token{};
        Core::Input::TouchSample m_start{};
        State m_state{State::Cancelled};
        bool m_captured{};
    };
}
