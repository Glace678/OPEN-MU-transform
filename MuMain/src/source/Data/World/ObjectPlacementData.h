#pragma once

#include <array>
#include <cstdint>
#include <optional>
#include <span>
#include <vector>

namespace Data::WorldObjects
{
    // Decoded world objects. The file stores little-endian scalars without
    // alignment padding; never reinterpret its bytes as native structs.
    class ObjectPlacementData
    {
    public:
        struct Placement
        {
            std::uint16_t type = 0;
            std::array<float, 3> position {};
            std::array<float, 3> angle {};
            float scale = 1.0f;
        };

        static constexpr std::size_t RecordSize = 30;

        static std::optional<ObjectPlacementData> Parse(
            std::span<const std::uint8_t> bytes, bool hasMapNumber, int modelCount);

        std::optional<std::vector<std::uint8_t>> Serialize(int modelCount) const;

        std::uint8_t mapNumber = 0;
        std::size_t skippedObjects = 0;
        std::vector<Placement> objects;
    };
}
