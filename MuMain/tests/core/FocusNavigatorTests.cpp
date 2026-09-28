#include <doctest.h>

#include "Core/Input/FocusNavigator.h"

#include <limits>

using namespace Core::Input;

namespace
{
    struct Owner
    {
    };
}

TEST_CASE("FocusRegistry registers valid nodes once per frame")
{
    FocusRegistry registry;
    Owner owner;

    CHECK_FALSE(registry.Register(&owner, 1, 0, 0, 10, 10));
    registry.BeginFrame();
    CHECK(registry.Register(&owner, 1, 0, 0, 10, 10));
    CHECK(registry.Register(&owner, 1, 20, 30, 40, 50));
    CHECK_FALSE(registry.Register(nullptr, 2, 0, 0, 10, 10));
    CHECK_FALSE(registry.Register(&owner, 2, 0, 0, 0, 10));
    CHECK_FALSE(registry.Register(&owner, 3, 0, 0, 10, 10, false));
    CHECK_FALSE(registry.Register(
        &owner,
        4,
        std::numeric_limits<float>::quiet_NaN(),
        0,
        10,
        10));
    registry.EndFrame();

    REQUIRE(registry.Size() == 1);
    const auto current = registry.Current();
    REQUIRE(current.has_value());
    CHECK(current->owner == reinterpret_cast<std::uintptr_t>(&owner));
    CHECK(current->id == 1);
    CHECK(current->centerX == doctest::Approx(40.0f));
    CHECK(current->centerY == doctest::Approx(55.0f));
}

TEST_CASE("FocusRegistry navigates by direction and geometric proximity")
{
    FocusRegistry registry;
    Owner center;
    Owner nearRight;
    Owner diagonalRight;
    Owner below;
    Owner left;

    registry.BeginFrame();
    registry.Register(&center, 0, 95, 95, 10, 10);
    registry.Register(&nearRight, 0, 145, 95, 10, 10);
    registry.Register(&diagonalRight, 0, 105, 155, 10, 10);
    registry.Register(&below, 0, 95, 145, 10, 10);
    registry.Register(&left, 0, 45, 95, 10, 10);
    registry.EndFrame();

    REQUIRE(registry.SetCurrent(&center, 0));
    CHECK(registry.Move(FocusDirection::Right));
    REQUIRE(registry.Current().has_value());
    CHECK(registry.Current()->owner == reinterpret_cast<std::uintptr_t>(&nearRight));

    REQUIRE(registry.SetCurrent(&center, 0));
    CHECK(registry.Move(FocusDirection::Down));
    CHECK(registry.Current()->owner == reinterpret_cast<std::uintptr_t>(&below));

    REQUIRE(registry.SetCurrent(&center, 0));
    CHECK(registry.Move(FocusDirection::Left));
    CHECK(registry.Current()->owner == reinterpret_cast<std::uintptr_t>(&left));
    CHECK_FALSE(registry.Move(FocusDirection::Left));
}

TEST_CASE("FocusRegistry preserves identity when registration order changes")
{
    FocusRegistry registry;
    Owner first;
    Owner second;

    registry.BeginFrame();
    registry.Register(&first, 10, 0, 0, 10, 10);
    registry.Register(&second, 20, 100, 0, 10, 10);
    registry.EndFrame();
    REQUIRE(registry.SetCurrent(&second, 20));

    registry.BeginFrame();
    registry.Register(&second, 20, 110, 0, 10, 10);
    registry.Register(&first, 10, 0, 0, 10, 10);
    registry.EndFrame();

    REQUIRE(registry.Current().has_value());
    CHECK(registry.Current()->owner == reinterpret_cast<std::uintptr_t>(&second));
    CHECK(registry.Current()->id == 20);
    CHECK(registry.Current()->centerX == doctest::Approx(115.0f));
}

TEST_CASE("FocusRegistry falls back near the vanished node")
{
    FocusRegistry registry;
    Owner vanished;
    Owner near;
    Owner far;

    registry.BeginFrame();
    registry.Register(&vanished, 1, 90, 90, 20, 20);
    registry.EndFrame();
    REQUIRE(registry.SetCurrent(&vanished, 1));

    registry.BeginFrame();
    registry.Register(&far, 3, 300, 300, 20, 20);
    registry.Register(&near, 2, 110, 100, 20, 20);
    registry.EndFrame();

    REQUIRE(registry.Current().has_value());
    CHECK(registry.Current()->owner == reinterpret_cast<std::uintptr_t>(&near));

    registry.BeginFrame();
    registry.EndFrame();
    CHECK_FALSE(registry.Current().has_value());
}

TEST_CASE("FocusRegistry can seed focus from the pointer position")
{
    FocusRegistry registry;
    Owner first;
    Owner second;

    registry.BeginFrame();
    registry.Register(&first, 1, 0, 0, 20, 20);
    registry.Register(&second, 2, 200, 200, 20, 20);
    registry.EndFrame(205, 205);

    REQUIRE(registry.Current().has_value());
    CHECK(registry.Current()->owner == reinterpret_cast<std::uintptr_t>(&second));
    CHECK_FALSE(registry.SetCurrent(&first, 99));
    CHECK(registry.SelectNearest(5, 5));
    CHECK(registry.Current()->owner == reinterpret_cast<std::uintptr_t>(&first));

    registry.Clear();
    CHECK(registry.Size() == 0);
    CHECK_FALSE(registry.Current().has_value());
}
