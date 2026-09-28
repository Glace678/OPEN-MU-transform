#include <doctest.h>
#include <initializer_list>
#include "UI/Dialogs/MessageBoxPointer.h"

using Pointer = UI::Dialogs::MessageBoxPointer;
using Event = Pointer::Event;

TEST_CASE("message box first touch activates without a preceding hover frame")
{
    Pointer pointer;
    CHECK(pointer.Update(1, false, false, false, false, false) == Event::None);
    CHECK(pointer.Update(1, true, true, false, false, false) == Event::LeftDown);
    CHECK(pointer.CapturesPointer());
    CHECK(pointer.Update(1, true, true, false, false, false) == Event::None);
    CHECK(pointer.Update(1, true, false, true, false, false) == Event::LeftUp);
    CHECK_FALSE(pointer.CapturesPointer());
    CHECK(pointer.Update(1, true, false, true, false, false) == Event::None);
}

TEST_CASE("message box first secondary press and ordinary mouse hover remain supported")
{
    Pointer pointer;
    CHECK(pointer.Update(1, false, false, false, false, false) == Event::None);
    CHECK(pointer.Update(1, true, false, false, true, false) == Event::RightDown);
    CHECK(pointer.Update(1, true, false, false, false, true) == Event::RightUp);
    CHECK(pointer.Update(1, false, false, false, false, false) == Event::None);
    CHECK(pointer.Update(1, true, false, false, false, false) == Event::Hover);
    CHECK(pointer.Update(1, true, false, false, false, false) == Event::None);
}

TEST_CASE("message box focus loss or touch cancellation is not a release confirmation")
{
    for (const bool left : {true, false})
    {
        Pointer pointer;
        pointer.Update(1, false, false, false, false, false);
        pointer.Update(1, true, left, false, !left, false);
        CHECK(pointer.Update(1, true, false, false, false, false) == Event::None);
        CHECK_FALSE(pointer.CapturesPointer());
    }
}

TEST_CASE("message box release outside and dragging into a button do not confirm")
{
    Pointer pointer;
    pointer.Update(1, false, false, false, false, false);
    CHECK(pointer.Update(1, false, true, false, false, false) == Event::None);
    CHECK(pointer.Update(1, true, true, false, false, false) == Event::None);
    CHECK(pointer.Update(1, true, false, true, false, false) == Event::Hover);
    CHECK(pointer.Update(1, true, true, false, false, false) == Event::LeftDown);
    CHECK(pointer.Update(1, false, false, true, false, false) == Event::None);
    CHECK_FALSE(pointer.CapturesPointer());
}

TEST_CASE("message box replacements cannot inherit the preceding held press")
{
    Pointer pointer;
    pointer.Update(1, false, false, false, false, false);
    pointer.Update(1, true, true, false, false, false);
    CHECK(pointer.Update(2, true, true, false, false, false) == Event::None);
    CHECK(pointer.Update(2, true, false, true, false, false) == Event::None);
    CHECK(pointer.Update(2, true, true, false, false, false) == Event::LeftDown);
    CHECK(pointer.Update(2, true, false, true, false, false) == Event::LeftUp);
}

TEST_CASE("message box newly opened during a held press waits for a new contact")
{
    Pointer pointer;
    CHECK(pointer.Update(1, true, true, false, false, false) == Event::None);
    CHECK(pointer.Update(1, true, false, true, false, false) == Event::None);
    CHECK(pointer.Update(1, true, true, false, false, false) == Event::LeftDown);
    pointer.Reset();
    CHECK_FALSE(pointer.CapturesPointer());
    CHECK(pointer.Update(1, true, false, true, false, false) == Event::Hover);
}

TEST_CASE("message box creation while idle accepts the genuine first down without an idle update")
{
    Pointer pointer;
    pointer.Bind(1, false, false);
    pointer.RecordButton(true, true, true, 17, 24);
    CHECK(pointer.Update(1, true, true, false, false, false) == Event::LeftDown);
    pointer.RecordButton(true, false, true, 19, 26);
    CHECK(pointer.Update(1, true, false, true, false, false) == Event::LeftUp);
}

TEST_CASE("message box same pump down and up retain both edges and their own coordinates")
{
    for (const bool left : {true, false})
    {
        Pointer pointer;
        pointer.Bind(1, false, false);
        pointer.RecordButton(left, true, true, 17, 24);
        pointer.RecordButton(left, false, true, 31, 38);
        CHECK(pointer.Update(1, false, false, left, false, !left)
            == (left ? Event::LeftDown : Event::RightDown));
        int x = 0, y = 0;
        CHECK(pointer.EventPosition(x, y));
        CHECK(x == 17);
        CHECK(y == 24);
        CHECK(pointer.CapturesPointer());
        CHECK(pointer.Update(1, false, false, false, false, false)
            == (left ? Event::LeftUp : Event::RightUp));
        CHECK(pointer.EventPosition(x, y));
        CHECK(x == 31);
        CHECK(y == 38);
        CHECK(pointer.Update(1, false, false, false, false, false) == Event::None);
        CHECK_FALSE(pointer.CapturesPointer());
        CHECK_FALSE(pointer.EventPosition(x, y));
    }
}

TEST_CASE("message box raw bare release never confirms even after owner creation")
{
    Pointer pointer;
    pointer.Bind(1, false, false);
    pointer.RecordButton(true, false, true, 17, 24);
    CHECK(pointer.Update(1, true, false, true, false, false) == Event::None);
    CHECK_FALSE(pointer.CapturesPointer());
}

TEST_CASE("message box created by a held opener ignores its up and accepts the next contact")
{
    Pointer pointer;
    pointer.Bind(1, true, false);
    pointer.RecordButton(true, false, true, 17, 24);
    CHECK(pointer.Update(1, true, false, true, false, false) == Event::None);
    pointer.RecordButton(true, true, true, 17, 24);
    pointer.RecordButton(true, false, true, 17, 24);
    CHECK(pointer.Update(1, true, false, true, false, false) == Event::LeftDown);
    CHECK(pointer.Update(1, true, false, false, false, false) == Event::LeftUp);
}

TEST_CASE("message box an old release on creation cannot suppress a new genuine down")
{
    Pointer pointer;
    pointer.Bind(1, false, false, true);
    pointer.RecordButton(true, true, true, 17, 24);
    CHECK(pointer.Update(1, true, true, false, false, false) == Event::LeftDown);
}

TEST_CASE("message box replacement discards a pending up rather than cascading confirmation")
{
    for (const bool sameAddress : {true, false})
    {
        Pointer pointer;
        pointer.Bind(1, false, false);
        pointer.RecordButton(true, true, true, 17, 24);
        pointer.RecordButton(true, false, true, 17, 24);
        CHECK(pointer.Update(1, true, false, true, false, false) == Event::LeftDown);
        pointer.Reset();
        const auto owner = sameAddress ? 1 : 2;
        pointer.Bind(owner, false, false, true);
        CHECK(pointer.Update(owner, true, false, true, false, false) == Event::None);
        pointer.RecordButton(true, true, true, 17, 24);
        CHECK(pointer.Update(owner, true, true, false, false, false) == Event::LeftDown);
    }
}

TEST_CASE("message box focus loss or multitouch cancel discard pending raw confirmation")
{
    for (const bool afterDown : {true, false})
    {
        Pointer pointer;
        pointer.Bind(1, false, false);
        pointer.RecordButton(true, true, true, 17, 24);
        pointer.RecordButton(true, false, true, 17, 24);
        if (afterDown) CHECK(pointer.Update(1, true, false, true, false, false) == Event::LeftDown);
        pointer.Cancel();
        pointer.RecordButton(true, false, true, 17, 24);
        CHECK(pointer.Update(1, true, false, true, false, false) == Event::None);
        CHECK_FALSE(pointer.CapturesPointer());
        pointer.RecordButton(true, true, true, 17, 24);
        CHECK(pointer.Update(1, true, true, false, false, false) == Event::LeftDown);
    }
}

TEST_CASE("message box raw outside contact and release outside cannot confirm")
{
    Pointer pointer;
    pointer.Bind(1, false, false);
    pointer.RecordButton(true, true, false, 2, 4);
    pointer.RecordButton(true, false, true, 17, 24);
    CHECK(pointer.Update(1, true, false, true, false, false) == Event::None);
    pointer.RecordButton(true, true, true, 17, 24);
    pointer.RecordButton(true, false, false, 2, 4);
    CHECK(pointer.Update(1, true, false, true, false, false) == Event::LeftDown);
    CHECK(pointer.Update(1, true, false, false, false, false) == Event::None);
    CHECK_FALSE(pointer.CapturesPointer());
}

TEST_CASE("message box mobile held latch cannot replay a recorded quick tap")
{
    Pointer pointer;
    pointer.Bind(1, false, false);
    pointer.RecordButton(true, true, true, 17, 24);
    pointer.RecordButton(true, false, true, 17, 24);
    CHECK(pointer.Update(1, true, true, false, false, false) == Event::LeftDown);
    CHECK(pointer.Update(1, true, false, true, false, false) == Event::LeftUp);
    CHECK(pointer.Update(1, true, false, true, false, false) == Event::None);
    CHECK(pointer.Update(1, true, false, true, false, false) != Event::LeftUp);
    pointer.RecordButton(true, true, true, 17, 24);
    CHECK(pointer.Update(1, true, true, false, false, false) == Event::LeftDown);
}

TEST_CASE("message box bounded raw edges cannot be overwritten by another pump contact")
{
    Pointer pointer;
    pointer.Bind(1, false, false);
    pointer.RecordButton(true, true, true, 17, 24);
    pointer.RecordButton(true, false, true, 19, 26);
    pointer.RecordButton(true, true, true, 31, 38);
    pointer.RecordButton(true, false, true, 33, 40);
    CHECK(pointer.Update(1, true, false, true, false, false) == Event::LeftDown);
    CHECK(pointer.Update(1, true, false, true, false, false) == Event::LeftUp);
    int x = 0, y = 0;
    CHECK(pointer.EventPosition(x, y));
    CHECK(x == 19);
    CHECK(y == 26);
    CHECK(pointer.Update(1, true, false, false, false, false) == Event::None);
}
