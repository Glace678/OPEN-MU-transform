#include <doctest.h>

#include <algorithm>

#include "Core/Input/MobileGestureMapper.h"

using Core::Input::MobileGestureActionType;
using Core::Input::MobileGestureMapper;
using Core::Input::TouchPhase;
using Core::Input::TouchSample;

namespace
{
    bool Has(const Core::Input::MobileGestureActions& actions,
        MobileGestureActionType type)
    {
        return std::any_of(actions.begin(), actions.end(),
            [type](const auto& action) { return action.type == type; });
    }
}

TEST_CASE("single-finger drag preserves the legacy left mouse contract")
{
    MobileGestureMapper mapper;
    auto down = mapper.Handle({1, TouchPhase::Down, 0.2f, 0.6f, 100});
    CHECK(Has(down, MobileGestureActionType::PointerMove));
    CHECK(Has(down, MobileGestureActionType::LeftButtonDown));

    auto move = mapper.Handle({1, TouchPhase::Move, 0.35f, 0.55f, 150});
    CHECK(Has(move, MobileGestureActionType::PointerMove));

    auto up = mapper.Handle({1, TouchPhase::Up, 0.35f, 0.55f, 200});
    CHECK(Has(up, MobileGestureActionType::LeftButtonUp));
}

TEST_CASE("gesture action batches remain bounded when a caller appends too many actions")
{
    Core::Input::MobileGestureActions actions;
    actions.push_back({MobileGestureActionType::PointerMove, 0.0f, 0.0f});
    actions.push_back({MobileGestureActionType::LeftButtonDown, 0.0f, 0.0f});
    actions.push_back({MobileGestureActionType::LeftButtonUp, 0.0f, 0.0f});
    actions.push_back({MobileGestureActionType::RightButtonDown, 0.0f, 0.0f});

    CHECK(actions.size() == 3);
    CHECK(std::count_if(actions.begin(), actions.end(),
        [](const auto& action) { return action.type == MobileGestureActionType::RightButtonDown; }) == 0);
}

TEST_CASE("right-half double tap casts with the legacy right mouse button")
{
    MobileGestureMapper mapper;
    mapper.Handle({1, TouchPhase::Down, 0.75f, 0.5f, 100});
    mapper.Handle({1, TouchPhase::Up, 0.75f, 0.5f, 160});

    auto secondDown = mapper.Handle({2, TouchPhase::Down, 0.76f, 0.51f, 300});
    CHECK(Has(secondDown, MobileGestureActionType::RightButtonDown));
    CHECK_FALSE(Has(secondDown, MobileGestureActionType::LeftButtonDown));
    auto secondUp = mapper.Handle({2, TouchPhase::Up, 0.76f, 0.51f, 350});
    CHECK(Has(secondUp, MobileGestureActionType::RightButtonUp));
}

TEST_CASE("rapid menu taps remain left clicks and cannot cast a skill")
{
    MobileGestureMapper mapper;
    mapper.SetWorldGesturesEnabled(false);
    mapper.Handle({1, TouchPhase::Down, 0.75f, 0.5f, 100});
    mapper.Handle({1, TouchPhase::Up, 0.75f, 0.5f, 160});
    const auto next = mapper.Handle({2, TouchPhase::Down, 0.75f, 0.5f, 200});
    CHECK(Has(next, MobileGestureActionType::LeftButtonDown));
    CHECK_FALSE(Has(next, MobileGestureActionType::RightButtonDown));
    mapper.Handle({2, TouchPhase::Up, 0.75f, 0.5f, 250});
    mapper.SetWorldGesturesEnabled(true);
    const auto world = mapper.Handle({3, TouchPhase::Down, 0.75f, 0.5f, 300});
    CHECK(Has(world, MobileGestureActionType::LeftButtonDown));
    CHECK_FALSE(Has(world, MobileGestureActionType::RightButtonDown));
}

TEST_CASE("multi-touch in a menu cancels its press without world commands")
{
    MobileGestureMapper mapper;
    mapper.SetWorldGesturesEnabled(false);
    mapper.Handle({1, TouchPhase::Down, 0.3f, 0.5f, 100});
    const auto second = mapper.Handle({2, TouchPhase::Down, 0.5f, 0.5f, 120});
    CHECK(Has(second, MobileGestureActionType::CancelLeftButton));
    CHECK_FALSE(Has(second, MobileGestureActionType::LeftButtonUp));
    CHECK(mapper.Handle({1, TouchPhase::Move, 0.1f, 0.5f, 150}).empty());
    CHECK(mapper.Handle({2, TouchPhase::Move, 0.9f, 0.5f, 170}).empty());
    CHECK(mapper.Handle({2, TouchPhase::Up, 0.9f, 0.5f, 190}).empty());
}

TEST_CASE("double-tap recognition includes exact time and distance boundaries")
{
    MobileGestureMapper mapper;
    mapper.Handle({1, TouchPhase::Down, 0.70f, 0.50f, 100});
    mapper.Handle({1, TouchPhase::Up, 0.70f, 0.50f, 170});

    const auto boundary = mapper.Handle({2, TouchPhase::Down, 0.765f, 0.50f, 500});
    CHECK(Has(boundary, MobileGestureActionType::RightButtonDown));

    MobileGestureMapper lateMapper;
    lateMapper.Handle({1, TouchPhase::Down, 0.70f, 0.50f, 100});
    lateMapper.Handle({1, TouchPhase::Up, 0.70f, 0.50f, 170});
    const auto outsideWindow = lateMapper.Handle({2, TouchPhase::Down, 0.70f, 0.50f, 501});
    CHECK(Has(outsideWindow, MobileGestureActionType::LeftButtonDown));
    CHECK_FALSE(Has(outsideWindow, MobileGestureActionType::RightButtonDown));
}

TEST_CASE("touch coordinates are clamped after contact begins")
{
    MobileGestureMapper mapper;
    mapper.Handle({1, TouchPhase::Down, 0.2f, 0.5f, 100});

    const auto move = mapper.Handle({1, TouchPhase::Move, -0.25f, 1.25f, 120});
    REQUIRE(move.size() == 1);
    CHECK(move.front().type == MobileGestureActionType::PointerMove);
    CHECK(move.front().x == 0.0f);
    CHECK(move.front().y == 1.0f);
}

TEST_CASE("left-handed mode swaps action zones without mirroring pointer coordinates")
{
    MobileGestureMapper mapper;
    mapper.SetLeftHanded(true);

    mapper.Handle({1, TouchPhase::Down, 0.2f, 0.4f, 100});
    mapper.Handle({1, TouchPhase::Up, 0.2f, 0.4f, 180});
    const auto secondDown = mapper.Handle({2, TouchPhase::Down, 0.2f, 0.4f, 260});

    CHECK(Has(secondDown, MobileGestureActionType::RightButtonDown));
    REQUIRE_FALSE(secondDown.empty());
    CHECK(secondDown.front().x == doctest::Approx(0.2f));
}

TEST_CASE("two-finger gestures cancel held movement without producing a click")
{
    MobileGestureMapper mapper;
    mapper.Handle({1, TouchPhase::Down, 0.3f, 0.5f, 100});
    auto second = mapper.Handle({2, TouchPhase::Down, 0.5f, 0.5f, 120});
    CHECK(Has(second, MobileGestureActionType::CancelLeftButton));
    CHECK_FALSE(Has(second, MobileGestureActionType::LeftButtonUp));

    mapper.Handle({1, TouchPhase::Move, 0.42f, 0.5f, 150});
    auto swipe = mapper.Handle({2, TouchPhase::Move, 0.62f, 0.5f, 150});
    CHECK(Has(swipe, MobileGestureActionType::NextSkill));
}

TEST_CASE("system cancellation clears a held touch without clicking")
{
    MobileGestureMapper mapper;
    mapper.Handle({1, TouchPhase::Down, 0.2f, 0.5f, 100});
    const auto cancelled = mapper.Handle({1, TouchPhase::Cancel, 0.2f, 0.5f, 120});
    CHECK(Has(cancelled, MobileGestureActionType::CancelLeftButton));
    CHECK_FALSE(Has(cancelled, MobileGestureActionType::LeftButtonUp));
}

TEST_CASE("pinch and vertical two-finger motion control camera zoom")
{
    MobileGestureMapper mapper;
    mapper.Handle({1, TouchPhase::Down, 0.4f, 0.5f, 100});
    mapper.Handle({2, TouchPhase::Down, 0.6f, 0.5f, 110});
    auto pinch = mapper.Handle({1, TouchPhase::Move, 0.32f, 0.5f, 150});
    CHECK(Has(pinch, MobileGestureActionType::ZoomIn));

    mapper.Reset();
    mapper.Handle({1, TouchPhase::Down, 0.4f, 0.6f, 200});
    mapper.Handle({2, TouchPhase::Down, 0.6f, 0.6f, 210});
    mapper.Handle({1, TouchPhase::Move, 0.4f, 0.48f, 250});
    auto vertical = mapper.Handle({2, TouchPhase::Move, 0.6f, 0.48f, 250});
    CHECK(Has(vertical, MobileGestureActionType::ZoomIn));
}

TEST_CASE("three-finger vertical swipes open map and settings")
{
    MobileGestureMapper mapper;
    mapper.Handle({1, TouchPhase::Down, 0.3f, 0.7f, 100});
    mapper.Handle({2, TouchPhase::Down, 0.5f, 0.7f, 110});
    mapper.Handle({3, TouchPhase::Down, 0.7f, 0.7f, 120});
    mapper.Handle({1, TouchPhase::Move, 0.3f, 0.45f, 170});
    mapper.Handle({2, TouchPhase::Move, 0.5f, 0.45f, 170});
    mapper.Handle({3, TouchPhase::Move, 0.7f, 0.45f, 170});
    auto up = mapper.Handle({3, TouchPhase::Up, 0.7f, 0.45f, 190});
    CHECK(Has(up, MobileGestureActionType::OpenMap));

    mapper.Reset();
    mapper.Handle({4, TouchPhase::Down, 0.3f, 0.3f, 300});
    mapper.Handle({5, TouchPhase::Down, 0.5f, 0.3f, 310});
    mapper.Handle({6, TouchPhase::Down, 0.7f, 0.3f, 320});
    mapper.Handle({4, TouchPhase::Move, 0.3f, 0.55f, 370});
    mapper.Handle({5, TouchPhase::Move, 0.5f, 0.55f, 370});
    mapper.Handle({6, TouchPhase::Move, 0.7f, 0.55f, 370});
    auto down = mapper.Handle({6, TouchPhase::Up, 0.7f, 0.55f, 390});
    CHECK(Has(down, MobileGestureActionType::OpenSettings));
}

TEST_CASE("captured item contact never creates primary or secondary mouse edges")
{
    MobileGestureMapper mapper;
    mapper.CaptureNextContact();
    const auto down = mapper.Handle({1, TouchPhase::Down, 0.75f, 0.50f, 100});
    CHECK(mapper.HasActiveContacts());
    REQUIRE(down.size() == 1);
    CHECK(down.front().type == MobileGestureActionType::PointerMove);
    const auto up = mapper.Handle({1, TouchPhase::Up, 0.75f, 0.50f, 800});
    REQUIRE(up.size() == 1);
    CHECK(up.front().type == MobileGestureActionType::PointerMove);
    CHECK_FALSE(mapper.HasActiveContacts());
    CHECK(Has(mapper.Handle({2, TouchPhase::Down, 0.75f, 0.50f, 850}), MobileGestureActionType::LeftButtonDown));
}

TEST_CASE("second and third contacts cannot turn an item drag into world gestures")
{
    MobileGestureMapper mapper;
    mapper.CaptureNextContact();
    mapper.Handle({1, TouchPhase::Down, 0.3f, 0.5f, 100});
    CHECK(mapper.Handle({2, TouchPhase::Down, 0.6f, 0.5f, 120}).empty());
    mapper.SetWorldGesturesEnabled(true);
    CHECK(mapper.Handle({3, TouchPhase::Down, 0.9f, 0.5f, 140}).empty());
    CHECK(mapper.Handle({1, TouchPhase::Move, 0.3f, 0.1f, 180}).empty());
    CHECK(mapper.Handle({2, TouchPhase::Move, 0.6f, 0.1f, 190}).empty());
    CHECK(mapper.Handle({3, TouchPhase::Up, 0.9f, 0.1f, 200}).empty());
    CHECK(mapper.Handle({2, TouchPhase::Up, 0.6f, 0.1f, 210}).empty());
    CHECK(mapper.Handle({1, TouchPhase::Up, 0.3f, 0.1f, 220}).empty());
    CHECK_FALSE(mapper.HasActiveContacts());
}

TEST_CASE("captured contact cancellation clears ownership and double tap history")
{
    MobileGestureMapper mapper;
    mapper.Handle({1, TouchPhase::Down, 0.75f, 0.5f, 100});
    mapper.Handle({1, TouchPhase::Up, 0.75f, 0.5f, 150});
    mapper.CaptureNextContact();
    mapper.Handle({2, TouchPhase::Down, 0.75f, 0.5f, 200});
    CHECK(mapper.Handle({2, TouchPhase::Cancel, 0.75f, 0.5f, 220}).empty());
    CHECK_FALSE(mapper.HasActiveContacts());
    const auto next = mapper.Handle({3, TouchPhase::Down, 0.75f, 0.5f, 250});
    CHECK(Has(next, MobileGestureActionType::LeftButtonDown));
    CHECK_FALSE(Has(next, MobileGestureActionType::RightButtonDown));
}
