#include <doctest.h>
#include "UI/Items/Touch/ItemTouchController.h"
#include "UI/Items/Touch/QuickItemTouchLayout.h"

using namespace UI::Items::Touch;
using Core::Input::TouchPhase;

namespace
{
    struct InventoryDouble final : ItemTouchTarget
    {
        ItemTouchToken identity;
        bool available{true}, itemPresent{true}, picked{}, placeAllowed{true};
        int selections{}, uses{}, pickups{}, placements{}, restores{};
        InventoryDouble() { identity.owner = 1; identity.key = 20; identity.index = 12; identity.context = 1; }
        std::optional<ItemTouchToken> Capture(float, float) override { return available && itemPresent ? std::optional{identity} : std::nullopt; }
        bool Validate(const ItemTouchToken& token, bool dragging) override
        {
            return available && token.context == identity.context && token.index == identity.index
                && (dragging ? picked && token.draggedKey == 1020 : itemPresent && token.key == identity.key);
        }
        void Select(const ItemTouchToken&) override { ++selections; }
        void Use(const ItemTouchToken&) override { ++uses; }
        bool BeginDrag(ItemTouchToken& token) override
        {
            if (!itemPresent) return false;
            itemPresent = false; picked = true; token.draggedKey = 1020; ++pickups; return true;
        }
        bool PlaceDrag(const ItemTouchToken&, float, float) override
        {
            if (!placeAllowed) return false;
            picked = false; itemPresent = true; ++placements; return true;
        }
        void RestoreDrag(const ItemTouchToken&) override { picked = false; itemPresent = true; ++restores; }
    };
}

TEST_CASE("item touch first contact selects without mouse hover or picking on release")
{
    InventoryDouble target; ItemTouchController controller(target);
    CHECK(controller.Begin({1, TouchPhase::Down, 10, 20, 100}));
    CHECK(target.selections == 1);
    controller.Handle({1, TouchPhase::Up, 10, 20, 200});
    controller.FinishContactSequence();
    CHECK(target.pickups == 0); CHECK(target.uses == 0); CHECK(target.itemPresent);
    CHECK_FALSE(controller.IsCaptured());
}

TEST_CASE("item touch 500ms consumption is once only and suppresses primary release")
{
    InventoryDouble target; ItemTouchController controller(target);
    controller.Begin({1, TouchPhase::Down, 10, 20, 100});
    controller.Tick(599); CHECK(target.uses == 0);
    controller.Tick(600); CHECK(target.uses == 1);
    controller.Tick(1600); controller.Handle({1, TouchPhase::Up, 10, 20, 1800});
    controller.FinishContactSequence();
    CHECK(target.uses == 1); CHECK(target.pickups == 0); CHECK(target.placements == 0);
}

TEST_CASE("item touch long release works even when no move event or timer frame was delivered")
{
    InventoryDouble target; ItemTouchController controller(target);
    controller.Begin({1, TouchPhase::Down, 10, 20, 100});
    controller.Handle({1, TouchPhase::Up, 10, 20, 600});
    CHECK(target.uses == 1); CHECK(target.pickups == 0);
}

TEST_CASE("item touch drag threshold begins a real pick before release and cancels consumption")
{
    InventoryDouble target; ItemTouchController controller(target);
    controller.Begin({1, TouchPhase::Down, 10, 20, 100});
    controller.Handle({1, TouchPhase::Move, 15.9f, 20, 200}); CHECK(target.pickups == 0);
    controller.Handle({1, TouchPhase::Move, 16, 20, 210}); CHECK(target.pickups == 1); CHECK(target.picked);
    controller.Tick(1000); CHECK(target.uses == 0);
    controller.Handle({1, TouchPhase::Up, 40, 20, 1200});
    CHECK(target.placements == 1); CHECK(target.restores == 0); CHECK_FALSE(target.picked);
}

TEST_CASE("item touch second finger restores a drag without placing or using")
{
    InventoryDouble target; ItemTouchController controller(target);
    controller.Begin({1, TouchPhase::Down, 10, 20, 100});
    controller.Handle({1, TouchPhase::Move, 20, 20, 200});
    controller.Handle({2, TouchPhase::Down, 25, 20, 220});
    controller.Handle({1, TouchPhase::Up, 40, 20, 800}); controller.Tick(900);
    controller.FinishContactSequence();
    CHECK(target.restores == 1); CHECK(target.placements == 0); CHECK(target.uses == 0); CHECK(target.itemPresent);
}

TEST_CASE("item touch release-only movement starts a drag instead of long-press use")
{
    InventoryDouble target; ItemTouchController controller(target);
    controller.Begin({1, TouchPhase::Down, 10, 20, 100});
    controller.Handle({1, TouchPhase::Up, 30, 20, 600});
    CHECK(target.pickups == 1); CHECK(target.placements == 1); CHECK(target.uses == 0);

    InventoryDouble quick; quick.identity.kind = ItemTouchKind::QuickSlot;
    ItemTouchController quickController(quick);
    quickController.Begin({1, TouchPhase::Down, 20, 455, 100});
    quickController.Handle({1, TouchPhase::Up, 30, 455, 200});
    CHECK(quick.uses == 0); CHECK(quick.pickups == 0);
}

TEST_CASE("item touch cancelled pending gesture never uses or picks an item")
{
    InventoryDouble target; ItemTouchController controller(target);
    controller.Begin({1, TouchPhase::Down, 10, 20, 100});
    controller.Handle({2, TouchPhase::Down, 20, 20, 200});
    controller.Tick(600); controller.Handle({1, TouchPhase::Up, 10, 20, 800});
    CHECK(target.uses == 0); CHECK(target.pickups == 0); CHECK(target.restores == 0);
}

TEST_CASE("item touch stale item key or slot blocks deferred use")
{
    for (bool changeKey : {false, true})
    {
        InventoryDouble target; ItemTouchController controller(target);
        controller.Begin({1, TouchPhase::Down, 10, 20, 100});
        if (changeKey) ++target.identity.key; else ++target.identity.index;
        controller.Tick(600); controller.Handle({1, TouchPhase::Up, 10, 20, 800});
        CHECK(target.uses == 0); CHECK(target.pickups == 0);
    }
}

TEST_CASE("item touch popup or interface switch restores a held drag")
{
    for (bool changeContext : {false, true})
    {
        InventoryDouble target; ItemTouchController controller(target);
        controller.Begin({1, TouchPhase::Down, 10, 20, 100});
        controller.Handle({1, TouchPhase::Move, 20, 20, 200});
        if (changeContext) ++target.identity.context; else target.available = false;
        controller.Tick(250); controller.Handle({1, TouchPhase::Up, 50, 20, 300});
        CHECK(target.restores == 1); CHECK(target.placements == 0); CHECK(target.itemPresent);
    }
}

TEST_CASE("item touch system cancellation and failed placement restore once")
{
    for (bool systemCancel : {false, true})
    {
        InventoryDouble target; ItemTouchController controller(target);
        controller.Begin({1, TouchPhase::Down, 10, 20, 100});
        controller.Handle({1, TouchPhase::Move, 20, 20, 200});
        if (systemCancel) controller.Handle({1, TouchPhase::Cancel, 20, 20, 250});
        else { target.placeAllowed = false; controller.Handle({1, TouchPhase::Up, 50, 20, 300}); }
        controller.Cancel(); controller.FinishContactSequence();
        CHECK(target.restores == 1); CHECK(target.placements == 0); CHECK(target.uses == 0);
    }
}

TEST_CASE("item touch quick slot taps use exactly once without press-and-hold repeats")
{
    InventoryDouble target; target.identity.kind = ItemTouchKind::QuickSlot;
    ItemTouchController controller(target);
    controller.Begin({1, TouchPhase::Down, 20, 455, 100});
    controller.Handle({1, TouchPhase::Up, 20, 455, 400}); controller.FinishContactSequence();
    CHECK(target.uses == 1);
    controller.Begin({2, TouchPhase::Down, 20, 455, 500});
    controller.Tick(1000); controller.Tick(1500);
    controller.Handle({2, TouchPhase::Up, 20, 455, 1600});
    CHECK(target.uses == 1); CHECK(target.pickups == 0);
}

TEST_CASE("item touch quick slot movement cancels rather than dragging or casting")
{
    InventoryDouble target; target.identity.kind = ItemTouchKind::QuickSlot;
    ItemTouchController controller(target);
    controller.Begin({1, TouchPhase::Down, 20, 455, 100});
    controller.Handle({1, TouchPhase::Move, 26, 455, 200});
    controller.Handle({1, TouchPhase::Up, 26, 455, 250});
    CHECK(target.uses == 0); CHECK(target.pickups == 0);
}

TEST_CASE("item touch long press is elapsed-time based at 30 60 and 120 frames per second")
{
    for (const int frameStep : {33, 16, 8})
    {
        InventoryDouble target; ItemTouchController controller(target);
        controller.Begin({1, TouchPhase::Down, 10, 20, 100});
        for (int time = 100; time < 1000; time += frameStep) controller.Tick(time);
        CHECK(target.uses == 1);
    }
}

TEST_CASE("item touch quick slot geometry has four non-overlapping enlarged targets")
{
    for (int slot = 0; slot < 4; ++slot)
    {
        CHECK(QuickItemSlotAt(20 + slot * 38, 455) == slot);
        CHECK(QuickItemSlotAt(1 + slot * 38, 434) == slot);
    }
    CHECK(QuickItemSlotAt(39, 455) == 1);
    CHECK(QuickItemSlotAt(0, 455) == -1);
    CHECK(QuickItemSlotAt(153, 455) == -1);
    CHECK(QuickItemSlotAt(20, 433.9f) == -1);
    CHECK(QuickItemSlotAt(20, 476) == -1);
}
