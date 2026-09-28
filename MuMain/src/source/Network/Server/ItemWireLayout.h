#pragma once

#include <cstdint>
#include <optional>
#include <span>

namespace Network::ItemWire
{
    inline std::optional<std::size_t> ExtendedLength(std::span<const std::uint8_t> item)
    {
        constexpr std::size_t BaseSize = 5;
        constexpr std::uint8_t ExtraFields[] = { 0x01, 0x08, 0x10, 0x20 };
        constexpr std::uint8_t SocketFlag = 0x80;
        constexpr std::uint8_t SocketCountMask = 0x0F;
        constexpr std::uint8_t MaximumSockets = 5;
        if (item.size() < BaseSize)
            return std::nullopt;
        auto size = BaseSize;
        for (const auto flag : ExtraFields)
            size += (item[BaseSize - 1] & flag) != 0;
        if (item[BaseSize - 1] & SocketFlag)
        {
            if (size >= item.size() || (item[size] & SocketCountMask) > MaximumSockets)
                return std::nullopt;
            size += 1 + (item[size] & SocketCountMask);
        }
        return size <= item.size() ? std::optional(size) : std::nullopt;
    }
}
