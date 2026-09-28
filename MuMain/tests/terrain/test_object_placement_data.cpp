#include "doctest.h"
#include "Data/World/ObjectPlacementData.h"

#include <bit>
#include <limits>

namespace
{
    constexpr int ModelCount = 100;

    void AppendFloat(std::vector<std::uint8_t>& bytes, float value)
    {
        auto bits = std::bit_cast<std::uint32_t>(value);
        for (int index = 0; index < 4; ++index)
        {
            bytes.push_back(static_cast<std::uint8_t>(bits));
            bits >>= 8;
        }
    }

    std::vector<std::uint8_t> ValidFile(bool hasMapNumber = true)
    {
        std::vector<std::uint8_t> bytes { 0 };
        if (hasMapNumber)
            bytes.push_back(42);
        bytes.insert(bytes.end(), { 1, 0, 7, 0 }); // one object, type 7
        for (const auto value : { 100.0f, 200.0f, 300.0f, 0.0f, 90.0f, 180.0f, 1.5f })
            AppendFloat(bytes, value);
        return bytes;
    }
}

using Data::WorldObjects::ObjectPlacementData;

TEST_CASE("world object records preserve the legacy little-endian layout")
{
    for (bool hasMapNumber : { false, true })
    {
        const auto parsed = ObjectPlacementData::Parse(ValidFile(hasMapNumber), hasMapNumber, ModelCount);
        REQUIRE(parsed.has_value());
        CHECK(parsed->mapNumber == (hasMapNumber ? 42 : 0));
        REQUIRE(parsed->objects.size() == 1);
        CHECK(parsed->objects[0].type == 7);
        CHECK(parsed->objects[0].position[2] == 300.0f);
        CHECK(parsed->objects[0].angle[1] == 90.0f);
        CHECK(parsed->objects[0].scale == 1.5f);
    }
}

TEST_CASE("truncated world object files fail before any object is created")
{
    const auto bytes = ValidFile();
    for (std::size_t size = 0; size < bytes.size(); ++size)
        CHECK_FALSE(ObjectPlacementData::Parse(std::span(bytes).first(size), true, ModelCount).has_value());
}

TEST_CASE("world object files reject negative counts and skip invalid model indexes")
{
    auto bytes = ValidFile();
    bytes[2] = 0xff;
    bytes[3] = 0xff;
    CHECK_FALSE(ObjectPlacementData::Parse(bytes, true, ModelCount).has_value());

    bytes = ValidFile();
    bytes[4] = ModelCount;
    const auto parsed = ObjectPlacementData::Parse(bytes, true, ModelCount);
    REQUIRE(parsed.has_value());
    CHECK(parsed->objects.empty());
    CHECK(parsed->skippedObjects == 1);
}

TEST_CASE("nonfinite world object coordinates cannot enter the renderer")
{
    auto bytes = ValidFile();
    const auto secondRecord = ValidFile();
    bytes[2] = 2;
    bytes.insert(bytes.end(), secondRecord.begin() + 4, secondRecord.end());
    bytes.resize(bytes.size() - sizeof(float));
    AppendFloat(bytes, std::numeric_limits<float>::quiet_NaN());
    const auto parsed = ObjectPlacementData::Parse(bytes, true, ModelCount);
    REQUIRE(parsed.has_value());
    CHECK(parsed->objects.size() == 1);
    CHECK(parsed->skippedObjects == 1);
}

TEST_CASE("empty world object maps remain valid")
{
    const std::vector<std::uint8_t> bytes { 0, 42, 0, 0 };
    const auto parsed = ObjectPlacementData::Parse(bytes, true, ModelCount);
    REQUIRE(parsed.has_value());
    CHECK(parsed->objects.empty());
    CHECK(parsed->mapNumber == 42);
}

TEST_CASE("world object saves retain the binary layout and actual record count")
{
    const auto bytes = ValidFile();
    auto parsed = ObjectPlacementData::Parse(bytes, true, ModelCount);
    REQUIRE(parsed.has_value());
    REQUIRE(parsed->Serialize(ModelCount).has_value());
    CHECK(*parsed->Serialize(ModelCount) == bytes);

    parsed->objects.clear();
    const auto empty = parsed->Serialize(ModelCount);
    REQUIRE(empty.has_value());
    CHECK(*empty == std::vector<std::uint8_t> { 0, 42, 0, 0 });
}

TEST_CASE("world object save cannot wrap the signed record count")
{
    ObjectPlacementData data;
    data.objects.resize(static_cast<std::size_t>(std::numeric_limits<std::int16_t>::max()) + 1);
    CHECK_FALSE(data.Serialize(ModelCount).has_value());
}
