#include "Data/World/TerrainData.h"

#include <algorithm>

namespace Data::Terrain
{
    int ParseMapping(std::span<const std::uint8_t> bytes, std::span<std::uint8_t> layer1,
        std::span<std::uint8_t> layer2, std::span<float> alpha)
    {
        if (bytes.size() < MappingFileSize || layer1.size() < TileCount ||
            layer2.size() < TileCount || alpha.size() < TileCount)
            return -1;

        const auto firstLayer = bytes.subspan(MappingHeaderSize, TileCount);
        const auto secondLayer = bytes.subspan(MappingHeaderSize + TileCount, TileCount);
        const auto blend = bytes.subspan(MappingHeaderSize + TileCount * 2, TileCount);
        std::copy(firstLayer.begin(), firstLayer.end(), layer1.begin());
        std::copy(secondLayer.begin(), secondLayer.end(), layer2.begin());
        constexpr float MaximumAlpha = 255.f;
        std::transform(blend.begin(), blend.end(), alpha.begin(),
            [](std::uint8_t value) { return static_cast<float>(value) / MaximumAlpha; });
        return bytes[1];
    }

    int ParseAttributes(std::span<const std::uint8_t> bytes, std::span<std::uint16_t> walls)
    {
        if ((bytes.size() != ByteAttributeFileSize && bytes.size() != WordAttributeFileSize) ||
            walls.size() < TileCount)
            return -1;
        constexpr std::uint8_t SupportedVersion = 0;
        constexpr std::uint8_t LastCoordinate = Width - 1;
        if (bytes[0] != SupportedVersion || bytes[2] != LastCoordinate || bytes[3] != LastCoordinate)
            return -1;

        const bool extended = bytes.size() == WordAttributeFileSize;
        for (std::size_t index = 0; index < TileCount; ++index)
        {
            const auto offset = AttributeHeaderSize + index * (extended ? 2 : 1);
            walls[index] = bytes[offset];
            if (extended)
                walls[index] |= static_cast<std::uint16_t>(bytes[offset + 1]) << 8;
        }
        return bytes[1];
    }
}
