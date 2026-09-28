#pragma once

#include <array>
#include <cstddef>
#include <cstdint>
#include <span>

namespace Network::MerchantPrices
{
    struct Quote
    {
        std::uint32_t basePrice = 0;
        std::uint32_t totalPrice = 0;
        bool payable = false;
    };

    // Main-thread network state. A quote applies only to its next identical NPC inventory.
    class QuoteCache
    {
    public:
        static constexpr std::uint8_t PacketCode = 0xFA;
        static constexpr std::size_t HeaderSize = 9;
        static constexpr std::size_t EntryOverhead = 10;
        static constexpr std::size_t MaximumItems = 120;
        static constexpr std::size_t MaximumItemBytes = 32;

        void Reset();
        bool Receive(std::span<const std::uint8_t> packet);
        void BeginStock();
        void Bind(std::uint8_t slot, std::span<const std::uint8_t> item, std::uint32_t key);
        void CompleteStock(std::size_t count);
        void UpdateTaxRate(std::uint8_t rate);
        bool IsAuthoritative() const { return m_authoritative; }
        bool TryGet(std::uint32_t key, Quote& quote) const;
        bool CanBuy(std::uint32_t key, std::int64_t money) const;

    private:
        struct Entry
        {
            Quote quote;
            std::array<std::uint8_t, MaximumItemBytes> item {};
            std::uint32_t key = 0;
            std::uint8_t length = 0;
            std::uint8_t slot = 0;
            bool bound = false;
        };

        bool Parse(std::span<const std::uint8_t> packet);
        std::array<Entry, MaximumItems> m_entries {};
        std::size_t m_count = 0;
        std::size_t m_bound = 0;
        bool m_authoritative = false;
        bool m_pending = false;
        bool m_valid = false;
        bool m_ready = false;
    };
}
