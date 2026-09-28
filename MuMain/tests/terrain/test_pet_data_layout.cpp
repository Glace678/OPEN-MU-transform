#include "doctest.h"
#include "Data/DataHandler/PetDataLayout.h"

using Data::Pets::PetDataLayout;

TEST_CASE("pet data layout retains the 32-bit file scalars on 64-bit clients")
{
    const auto layout = PetDataLayout::Validate(50, 6, 2512);
    REQUIRE(layout.has_value());
    CHECK(layout->recordSize == 416);
    CHECK(layout->payloadSize == 2496);
    CHECK(layout->recordCount == 6);
}

TEST_CASE("truncated pet data is rejected before allocating or decoding records")
{
    for (std::size_t size = 0; size < 2512; ++size)
        CHECK_FALSE(PetDataLayout::Validate(50, 6, size).has_value());
}

TEST_CASE("invalid pet counts cannot overflow record or payload sizes")
{
    constexpr auto largestFile = std::numeric_limits<std::size_t>::max();
    CHECK_FALSE(PetDataLayout::Validate(-1, 6, largestFile).has_value());
    CHECK_FALSE(PetDataLayout::Validate(50, -1, largestFile).has_value());
    CHECK_FALSE(PetDataLayout::Validate(0, 1, largestFile).has_value());
    CHECK_FALSE(PetDataLayout::Validate(std::numeric_limits<int>::max(), 1, largestFile).has_value());
    CHECK_FALSE(PetDataLayout::Validate(50, std::numeric_limits<int>::max(), largestFile).has_value());
    CHECK(PetDataLayout::Validate(0, 0, 16).has_value());
}
