#include "stdafx.h"
#include <doctest.h>
#include <array>
#include <cstddef>
#include "UI/NewUI/Dialogs/NewUIMessageBox.h"
#include "UI/NewUI/NewUIManager.h"

HWND g_hWnd = nullptr;
int MouseX = 0, MouseY = 0;
bool MouseLButton = false, MouseLButtonPush = false, MouseLButtonPop = false;
bool MouseRButton = false, MouseRButtonPush = false, MouseRButtonPop = false;

namespace
{
    using namespace SEASON3B;
    struct ObservedEvent { bool down; int x; int y; };
    std::vector<ObservedEvent> events;

    CALLBACK_RESULT Down(CNewUIMessageBoxBase*, const leaf::xstreambuf&)
    {
        events.push_back({true, MouseX, MouseY});
        return CALLBACK_CONTINUE;
    }
    CALLBACK_RESULT Up(CNewUIMessageBoxBase*, const leaf::xstreambuf&)
    {
        events.push_back({false, MouseX, MouseY});
        return CALLBACK_CONTINUE;
    }

    class TestMessage : public CNewUIMessageBoxBase
    {
    public:
        TestMessage()
        {
            Create(10, 20, 100, 80);
            AddCallbackFunc(Down, MSGBOX_EVENT_MOUSE_LBUTTON_DOWN);
            AddCallbackFunc(Up, MSGBOX_EVENT_MOUSE_LBUTTON_UP);
            AddCallbackFunc(Down, MSGBOX_EVENT_MOUSE_RBUTTON_DOWN);
            AddCallbackFunc(Up, MSGBOX_EVENT_MOUSE_RBUTTON_UP);
        }
        bool Update() override { return true; }
        bool Render() override { return true; }
    };

    // Force allocator address reuse so queue identity is tested deterministically.
    class ReusedMessage : public TestMessage
    {
    public:
        static void* operator new(std::size_t size)
        {
            alignas(std::max_align_t) static std::array<std::byte, 1024> memory{};
            REQUIRE(size <= memory.size());
            return memory.data();
        }
        static void operator delete(void*) noexcept {}
    };

    struct Fixture
    {
        CNewUIManager ui;
        CNewUIMessageBoxMng& manager = *CNewUIMessageBoxMng::GetInstance();
        Fixture()
        {
            events.clear();
            MouseLButton = MouseLButtonPush = MouseLButtonPop = false;
            MouseRButton = MouseRButtonPush = MouseRButtonPop = false;
            REQUIRE(manager.Create(&ui));
        }
        ~Fixture() { manager.Release(); }
        template<class T = TestMessage> T* Add()
        {
            return manager.NewMessageBox(CNewUIMessageBoxFactory::TContainer<T>());
        }
        void Frame()
        {
            manager.UpdateMouseEvent();
            manager.Update();
        }
    };
}

TEST_CASE("message box manager delivers a coalesced first tap using captured edge coordinates")
{
    Fixture fixture;
    fixture.Add();
    fixture.manager.RecordPointerButton(true, true, 17, 24);
    fixture.manager.RecordPointerButton(true, false, 31, 38);
    MouseX = 600;
    MouseY = 450;
    fixture.Frame();
    REQUIRE(events.size() == 1);
    CHECK(events[0].down);
    CHECK(events[0].x == 17);
    CHECK(events[0].y == 24);
    fixture.Frame();
    REQUIRE(events.size() == 2);
    CHECK_FALSE(events[1].down);
    CHECK(events[1].x == 31);
    CHECK(events[1].y == 38);
    CHECK(MouseX == 600);
    CHECK(MouseY == 450);
    fixture.Frame();
    CHECK(events.size() == 2);
}

TEST_CASE("message box manager does not transfer pending contact to a replacement")
{
    Fixture fixture;
    auto* first = fixture.Add();
    fixture.manager.RecordPointerButton(true, true, 17, 24);
    fixture.manager.RecordPointerButton(true, false, 17, 24);
    fixture.Frame();
    fixture.manager.DeleteMessageBox(first);
    fixture.Add();
    fixture.Frame();
    REQUIRE(events.size() == 1);
    CHECK(events[0].down);
}

TEST_CASE("message box manager stale queued events cannot target an allocator reused popup")
{
    Fixture fixture;
    auto* first = fixture.Add<ReusedMessage>();
    fixture.manager.RecordPointerButton(true, true, 17, 24);
    fixture.manager.RecordPointerButton(true, false, 17, 24);
    fixture.manager.UpdateMouseEvent();
    fixture.manager.UpdateMouseEvent();
    fixture.manager.DeleteMessageBox(first);
    auto* second = fixture.Add<ReusedMessage>();
    REQUIRE(second == first);
    fixture.manager.Update();
    CHECK(events.empty());
    fixture.manager.RecordPointerButton(true, true, 17, 24);
    fixture.manager.RecordPointerButton(true, false, 17, 24);
    fixture.Frame();
    fixture.Frame();
    CHECK(events.size() == 2);
}

TEST_CASE("message box manager focus and touch cancellation discard pending confirmation")
{
    Fixture fixture;
    fixture.Add();
    fixture.manager.RecordPointerButton(true, true, 17, 24);
    fixture.Frame();
    fixture.manager.CancelPointerInput();
    fixture.manager.RecordPointerButton(true, false, 17, 24);
    fixture.Frame();
    REQUIRE(events.size() == 1);
    CHECK(events[0].down);
}

TEST_CASE("message box manager ignores a bare release and a held popup opener")
{
    Fixture fixture;
    MouseLButton = MouseLButtonPush = true;
    fixture.Add();
    fixture.manager.RecordPointerButton(true, false, 17, 24);
    MouseLButton = MouseLButtonPush = false;
    MouseLButtonPop = true;
    fixture.Frame();
    CHECK(events.empty());
    fixture.manager.RecordPointerButton(true, false, 17, 24);
    fixture.Frame();
    CHECK(events.empty());
    fixture.manager.RecordPointerButton(true, true, 17, 24);
    fixture.manager.RecordPointerButton(true, false, 17, 24);
    fixture.Frame();
    fixture.Frame();
    CHECK(events.size() == 2);
}

TEST_CASE("message box manager mobile latch and secondary button do not duplicate recorded edges")
{
    Fixture fixture;
    fixture.Add();
    fixture.manager.RecordPointerButton(false, true, 17, 24);
    fixture.manager.RecordPointerButton(false, false, 17, 24);
    MouseRButton = MouseRButtonPush = true;
    fixture.Frame();
    MouseRButton = MouseRButtonPush = false;
    MouseRButtonPop = true;
    fixture.Frame();
    fixture.Frame();
    fixture.Frame();
    CHECK(events.size() == 2);
}

TEST_CASE("message box manager cancellation invalidates already queued pointer callbacks")
{
    Fixture fixture;
    fixture.Add();
    fixture.manager.RecordPointerButton(true, true, 17, 24);
    fixture.manager.RecordPointerButton(true, false, 17, 24);
    fixture.manager.UpdateMouseEvent();
    fixture.manager.UpdateMouseEvent();
    fixture.manager.CancelPointerInput();
    fixture.manager.Update();
    CHECK(events.empty());
}

TEST_CASE("message box manager idle creation with a stale push accepts a new real down")
{
    Fixture fixture;
    MouseLButtonPush = true;
    fixture.Add();
    fixture.manager.RecordPointerButton(true, true, 17, 24);
    fixture.manager.RecordPointerButton(true, false, 17, 24);
    fixture.Frame();
    fixture.Frame();
    CHECK(events.size() == 2);
}

TEST_CASE("message box manager callback replacement cannot cascade to the same address")
{
    Fixture fixture;
    auto* first = fixture.Add<ReusedMessage>();
    first->AddCallbackFunc([](CNewUIMessageBoxBase* owner, const leaf::xstreambuf&) {
        auto& manager = *CNewUIMessageBoxMng::GetInstance();
        manager.DeleteMessageBox(owner);
        manager.NewMessageBox(CNewUIMessageBoxFactory::TContainer<ReusedMessage>());
        return CALLBACK_CONTINUE;
    }, MSGBOX_EVENT_MOUSE_LBUTTON_DOWN);
    fixture.manager.RecordPointerButton(true, true, 17, 24);
    fixture.manager.RecordPointerButton(true, false, 17, 24);
    fixture.manager.UpdateMouseEvent();
    fixture.manager.UpdateMouseEvent();
    fixture.manager.Update();
    fixture.Frame();
    CHECK(events.empty());
}
