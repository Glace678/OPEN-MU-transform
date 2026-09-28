#pragma once

#include <array>
#include <cstdint>

class CShopList;

namespace GameShop::SoloCatalog
{
    inline constexpr int MaximumOffers = 2048;
    inline constexpr int NameBytes = 96;

    struct Offer
    {
        std::uint32_t id = 0;
        std::uint16_t itemCode = 0;
        std::uint8_t level = 0;
        std::uint8_t category = 0;
        std::int32_t price = 0;
        std::uint16_t quantity = 1;
        std::array<wchar_t, NameBytes> name {};
    };

    void Initialize(CShopList& catalog);
    bool Append(CShopList& catalog, const Offer& offer);
}
