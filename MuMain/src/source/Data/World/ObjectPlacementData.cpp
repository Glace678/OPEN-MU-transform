#include "ObjectPlacementData.h"

#include <algorithm>
#include <bit>
#include <cmath>
#include <limits>

namespace Data::WorldObjects
{
    namespace
    {
        std::uint16_t ReadWord(std::span<const std::uint8_t> bytes)
        {
            return static_cast<std::uint16_t>(bytes[0] | (static_cast<unsigned>(bytes[1]) << 8));
        }

        float ReadFloat(std::span<const std::uint8_t> bytes)
        {
            const auto bits = static_cast<std::uint32_t>(ReadWord(bytes))
                | (static_cast<std::uint32_t>(ReadWord(bytes.subspan(2))) << 16);
            return std::bit_cast<float>(bits);
        }

        void AppendWord(std::vector<std::uint8_t>& bytes, std::uint16_t value)
        {
            bytes.push_back(static_cast<std::uint8_t>(value));
            bytes.push_back(static_cast<std::uint8_t>(value >> 8));
        }

        void AppendFloat(std::vector<std::uint8_t>& bytes, float value)
        {
            const auto bits = std::bit_cast<std::uint32_t>(value);
            AppendWord(bytes, static_cast<std::uint16_t>(bits));
            AppendWord(bytes, static_cast<std::uint16_t>(bits >> 16));
        }

        ObjectPlacementData::Placement ReadPlacement(std::span<const std::uint8_t> record)
        {
            ObjectPlacementData::Placement placement;
            placement.type = ReadWord(record);
            std::size_t offset = sizeof(std::uint16_t);
            for (auto& coordinate : placement.position)
            {
                coordinate = ReadFloat(record.subspan(offset));
                offset += sizeof(float);
            }
            for (auto& angle : placement.angle)
            {
                angle = ReadFloat(record.subspan(offset));
                offset += sizeof(float);
            }
            placement.scale = ReadFloat(record.subspan(offset));
            return placement;
        }

        bool IsValid(const ObjectPlacementData::Placement& placement, int modelCount)
        {
            const auto isFinite = [](float value) { return std::isfinite(value); };
            return placement.type < modelCount
                && std::all_of(placement.position.begin(), placement.position.end(), isFinite)
                && std::all_of(placement.angle.begin(), placement.angle.end(), isFinite)
                && isFinite(placement.scale);
        }
    }

    std::optional<ObjectPlacementData> ObjectPlacementData::Parse(
        std::span<const std::uint8_t> bytes, bool hasMapNumber, int modelCount)
    {
        constexpr std::size_t VersionSize = 1;
        const std::size_t countOffset = VersionSize + (hasMapNumber ? 1 : 0);
        const std::size_t headerSize = countOffset + sizeof(std::uint16_t);
        if (bytes.size() < headerSize)
            return std::nullopt;

        const auto count = ReadWord(bytes.subspan(countOffset));
        if (count > std::numeric_limits<std::int16_t>::max()
            || count > (bytes.size() - headerSize) / RecordSize)
            return std::nullopt;

        ObjectPlacementData result;
        result.mapNumber = hasMapNumber ? bytes[VersionSize] : 0;
        result.objects.reserve(count);
        for (std::size_t index = 0; index < count; ++index)
        {
            auto placement = ReadPlacement(bytes.subspan(headerSize + index * RecordSize, RecordSize));
            if (!IsValid(placement, modelCount))
            {
                // Existing maps can contain isolated corrupt decorative records.
                // Keep the rest of the map playable without passing invalid
                // indexes or NaNs into the renderer.
                ++result.skippedObjects;
                continue;
            }
            result.objects.push_back(placement);
        }
        return result;
    }

    std::optional<std::vector<std::uint8_t>> ObjectPlacementData::Serialize(int modelCount) const
    {
        if (objects.size() > std::numeric_limits<std::int16_t>::max())
            return std::nullopt;

        constexpr std::size_t HeaderSize = 4;
        std::vector<std::uint8_t> bytes;
        bytes.reserve(HeaderSize + objects.size() * RecordSize);
        bytes.push_back(0); // Legacy world-object format version.
        bytes.push_back(mapNumber);
        AppendWord(bytes, static_cast<std::uint16_t>(objects.size()));
        for (const auto& placement : objects)
        {
            if (!IsValid(placement, modelCount))
                return std::nullopt;
            AppendWord(bytes, placement.type);
            for (auto coordinate : placement.position)
                AppendFloat(bytes, coordinate);
            for (auto angle : placement.angle)
                AppendFloat(bytes, angle);
            AppendFloat(bytes, placement.scale);
        }
        return bytes;
    }
}
