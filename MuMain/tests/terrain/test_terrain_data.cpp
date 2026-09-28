#include "doctest.h"
#include "Data/World/TerrainData.h"

#include <vector>

using namespace Data::Terrain;

TEST_CASE("terrain mapping preserves both layers and alpha endpoints")
{
    std::vector<std::uint8_t> bytes(MappingFileSize);
    bytes[1] = 42;
    bytes[MappingHeaderSize] = 7;
    bytes[MappingHeaderSize + TileCount - 1] = 9;
    bytes[MappingHeaderSize + TileCount] = 255;
    bytes[MappingHeaderSize + 2 * TileCount] = 128;
    bytes.back() = 255;
    std::vector<std::uint8_t> first(TileCount), second(TileCount);
    std::vector<float> alpha(TileCount);

    REQUIRE(ParseMapping(bytes, first, second, alpha) == 42);
    CHECK(first.front() == 7);
    CHECK(first.back() == 9);
    CHECK(second.front() == 255);
    CHECK(alpha.front() == doctest::Approx(128.f / 255.f));
    CHECK(alpha[1] == 0.f);
    CHECK(alpha.back() == 1.f);
}

TEST_CASE("every truncated terrain mapping is rejected before output changes")
{
    std::vector<std::uint8_t> bytes(MappingFileSize);
    std::vector<std::uint8_t> first(TileCount, 17), second(TileCount, 23);
    std::vector<float> alpha(TileCount, 0.5f);
    for (std::size_t length = 0; length < MappingFileSize; ++length)
        REQUIRE(ParseMapping(std::span(bytes).first(length), first, second, alpha) == -1);
    CHECK(first.front() == 17);
    CHECK(first.back() == 17);
    CHECK(second.front() == 23);
    CHECK(second.back() == 23);
    CHECK(alpha.front() == 0.5f);
    CHECK(alpha.back() == 0.5f);
}

TEST_CASE("terrain mapping checks destination capacity and permits legacy padding")
{
    std::vector<std::uint8_t> bytes(MappingFileSize + 8);
    bytes[1] = 5;
    std::vector<std::uint8_t> first(TileCount), second(TileCount);
    std::vector<float> alpha(TileCount);
    CHECK(ParseMapping(bytes, std::span(first).first(TileCount - 1), second, alpha) == -1);
    CHECK(ParseMapping(bytes, first, std::span(second).first(TileCount - 1), alpha) == -1);
    CHECK(ParseMapping(bytes, first, second, std::span(alpha).first(TileCount - 1)) == -1);
    CHECK(ParseMapping(bytes, first, second, alpha) == 5);
}

TEST_CASE("terrain attributes support byte and little-endian word formats")
{
    std::vector<std::uint16_t> walls(TileCount);
    std::vector<std::uint8_t> bytes(ByteAttributeFileSize);
    bytes[1] = 17;
    bytes[2] = bytes[3] = 255;
    bytes[AttributeHeaderSize] = 5;
    bytes.back() = 127;
    REQUIRE(ParseAttributes(bytes, walls) == 17);
    CHECK(walls.front() == 5);
    CHECK(walls.back() == 127);

    bytes.assign(WordAttributeFileSize, 0);
    bytes[1] = 51;
    bytes[2] = bytes[3] = 255;
    bytes[AttributeHeaderSize] = 0x34;
    bytes[AttributeHeaderSize + 1] = 0x12;
    bytes[WordAttributeFileSize - 2] = 0xCD;
    bytes.back() = 0xAB;
    REQUIRE(ParseAttributes(bytes, walls) == 51);
    CHECK(walls.front() == 0x1234);
    CHECK(walls.back() == 0xABCD);
}

TEST_CASE("terrain attributes reject bad headers and incomplete payloads atomically")
{
    std::vector<std::uint16_t> walls(TileCount, 0x4321);
    std::vector<std::uint8_t> bytes(ByteAttributeFileSize);
    bytes[2] = bytes[3] = 255;
    for (std::size_t length = 0; length < ByteAttributeFileSize; ++length)
        REQUIRE(ParseAttributes(std::span(bytes).first(length), walls) == -1);
    for (const auto headerIndex : { 0, 2, 3 })
    {
        const auto original = bytes[headerIndex];
        bytes[headerIndex] = 1;
        CHECK(ParseAttributes(bytes, walls) == -1);
        bytes[headerIndex] = original;
    }
    CHECK(ParseAttributes(bytes, std::span(walls).first(TileCount - 1)) == -1);
    bytes.push_back(0);
    CHECK(ParseAttributes(bytes, walls) == -1);
    CHECK(walls.front() == 0x4321);
    CHECK(walls.back() == 0x4321);
}
