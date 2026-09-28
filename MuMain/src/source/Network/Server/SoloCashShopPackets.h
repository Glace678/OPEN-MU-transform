#pragma once

#include <cstdint>
#include <span>

namespace Network::SoloCashShop
{
    bool HandlePacket(std::span<const std::uint8_t> packet);
    void RequestExchange();
}
