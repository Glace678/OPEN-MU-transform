#pragma once

#include <cstddef>
#include <cstdint>
#include <span>

namespace Data::Terrain
{
    inline constexpr std::size_t Width = 256;
    inline constexpr std::size_t TileCount = Width * Width;
    inline constexpr std::size_t MappingHeaderSize = 2;
    inline constexpr std::size_t MappingFileSize = MappingHeaderSize + TileCount * 3;
    inline constexpr std::size_t AttributeHeaderSize = 4;
    inline constexpr std::size_t ByteAttributeFileSize = AttributeHeaderSize + TileCount;
    inline constexpr std::size_t WordAttributeFileSize = AttributeHeaderSize + TileCount * 2;

    // Returns the map number, or -1 without changing outputs for invalid input.
    int ParseMapping(std::span<const std::uint8_t> bytes, std::span<std::uint8_t> layer1,
        std::span<std::uint8_t> layer2, std::span<float> alpha);
    int ParseAttributes(std::span<const std::uint8_t> bytes, std::span<std::uint16_t> walls);
}
