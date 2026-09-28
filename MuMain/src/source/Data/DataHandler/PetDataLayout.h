#pragma once

#include <cstddef>
#include <cstdint>
#include <limits>
#include <optional>

namespace Data::Pets
{
    struct PetDataLayout
    {
        static constexpr std::size_t HeaderSize = 3 * sizeof(std::int32_t);
        static constexpr std::size_t ChecksumSize = sizeof(std::uint32_t);

        int actionSlots;
        int recordCount;
        int recordSize;
        int payloadSize;

        static std::optional<PetDataLayout> Validate(int slots, int count, std::size_t fileSize)
        {
            constexpr int FixedRecordSize = 4 * sizeof(std::int32_t);
            constexpr int ActionSize = sizeof(std::int32_t) + sizeof(float);
            if (slots < 0 || count < 0 || (count > 0 && slots == 0)
                || slots > (std::numeric_limits<int>::max() - FixedRecordSize) / ActionSize)
                return std::nullopt;

            const int recordSize = FixedRecordSize + slots * ActionSize;
            if (count > std::numeric_limits<int>::max() / recordSize)
                return std::nullopt;
            const int payloadSize = count * recordSize;
            if (fileSize < HeaderSize + static_cast<std::size_t>(payloadSize) + ChecksumSize)
                return std::nullopt;
            return PetDataLayout { slots, count, recordSize, payloadSize };
        }
    };
}
