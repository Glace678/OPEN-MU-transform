#include "MerchantPriceQuoteCache.h"

#include <algorithm>
#include <limits>

namespace Network::MerchantPrices
{
    namespace
    {
        constexpr std::uint8_t LongHeader = 0xC2;
        constexpr std::uint8_t WireVersion = 1;
        constexpr std::uint8_t BalanceV1Profile = 1;
        constexpr std::uint8_t MaximumTaxRate = 3;
        constexpr std::uint32_t UnpayablePrice = std::numeric_limits<std::uint32_t>::max();
        constexpr std::uint32_t MaximumPrice = std::numeric_limits<std::int32_t>::max();

        std::uint32_t Read32(std::span<const std::uint8_t> data)
        {
            return static_cast<std::uint32_t>(data[0]) | (static_cast<std::uint32_t>(data[1]) << 8)
                | (static_cast<std::uint32_t>(data[2]) << 16) | (static_cast<std::uint32_t>(data[3]) << 24);
        }
    }

    void QuoteCache::Reset()
    {
        m_count = m_bound = 0;
        m_authoritative = m_pending = m_valid = m_ready = false;
    }

    bool QuoteCache::Receive(std::span<const std::uint8_t> packet)
    {
        if (packet.size() < 4 || packet[0] != LongHeader || packet[3] != PacketCode)
            return false;

        Reset();
        m_authoritative = m_pending = true;
        m_valid = Parse(packet);
        return true; // Recognized but malformed quotes never fall back to a guessed cheap price.
    }

    bool QuoteCache::Parse(std::span<const std::uint8_t> packet)
    {
        if (packet.size() < HeaderSize || ((static_cast<std::size_t>(packet[1]) << 8) | packet[2]) != packet.size()
            || packet[4] != 0 || packet[5] != WireVersion || packet[6] != BalanceV1Profile
            || packet[7] > MaximumTaxRate || packet[8] > MaximumItems)
            return false;

        m_count = packet[8];
        std::array<bool, MaximumItems> slots {};
        auto offset = HeaderSize;
        for (std::size_t index = 0; index < m_count; ++index)
        {
            if (packet.size() - offset < EntryOverhead)
                return false;
            const auto slot = packet[offset];
            const auto length = packet[offset + 1];
            if (slot >= MaximumItems || slots[slot] || length == 0 || length > MaximumItemBytes
                || packet.size() - offset < EntryOverhead + length)
                return false;
            slots[slot] = true;
            auto& entry = m_entries[index];
            entry = Entry {};
            entry.slot = slot;
            entry.length = length;
            std::copy_n(packet.begin() + offset + 2, length, entry.item.begin());
            entry.quote.basePrice = Read32(packet.subspan(offset + 2 + length));
            entry.quote.totalPrice = Read32(packet.subspan(offset + 6 + length));
            const auto base = entry.quote.basePrice;
            const auto total = entry.quote.totalPrice;
            entry.quote.payable = base != UnpayablePrice || total != UnpayablePrice;
            if (entry.quote.payable && (base > MaximumPrice || total > MaximumPrice
                || total != base + (static_cast<std::uint64_t>(base) * packet[7] / 100)))
                return false;
            offset += EntryOverhead + length;
        }
        return offset == packet.size();
    }

    void QuoteCache::BeginStock()
    {
        if (!m_pending)
        {
            if (m_authoritative)
            {
                // A second stock without its own quote cannot silently fall back to legacy prices.
                // A new NPC dialog, closing the merchant or a new game session calls Reset().
                m_valid = m_ready = false;
                m_bound = 0;
            }
            else
                Reset();
            return;
        }
        m_pending = false;
        m_ready = false;
        m_bound = 0;
    }

    void QuoteCache::Bind(std::uint8_t slot, std::span<const std::uint8_t> item, std::uint32_t key)
    {
        if (!m_authoritative || !m_valid)
            return;
        const auto end = m_entries.begin() + m_count;
        const auto found = std::find_if(m_entries.begin(), end, [slot](const Entry& entry) { return entry.slot == slot; });
        const auto duplicateKey = std::any_of(m_entries.begin(), end, [key](const Entry& entry) { return entry.bound && entry.key == key; });
        if (found == end || found->bound || duplicateKey || found->length != item.size()
            || !std::equal(item.begin(), item.end(), found->item.begin()))
        {
            m_valid = false;
            return;
        }
        found->bound = true;
        found->key = key;
        ++m_bound;
    }

    void QuoteCache::CompleteStock(std::size_t count)
    {
        m_ready = m_authoritative && m_valid && !m_pending && count == m_count && m_bound == m_count;
    }

    void QuoteCache::UpdateTaxRate(std::uint8_t rate)
    {
        if (!m_authoritative || !m_valid)
            return;
        if (rate > MaximumTaxRate)
        {
            m_valid = m_ready = false;
            return;
        }
        // Existing server tax notifications use the same integer charge as the versioned quote.
        for (std::size_t index = 0; index < m_count; ++index)
        {
            auto& quote = m_entries[index].quote;
            if (quote.basePrice == UnpayablePrice)
                continue;
            const auto total = quote.basePrice + static_cast<std::uint64_t>(quote.basePrice) * rate / 100;
            quote.payable = total <= MaximumPrice;
            quote.totalPrice = quote.payable ? static_cast<std::uint32_t>(total) : UnpayablePrice;
        }
    }

    bool QuoteCache::TryGet(std::uint32_t key, Quote& quote) const
    {
        if (!m_ready)
            return false;
        const auto end = m_entries.begin() + m_count;
        const auto found = std::find_if(m_entries.begin(), end, [key](const Entry& entry) { return entry.bound && entry.key == key; });
        if (found == end)
            return false;
        quote = found->quote;
        return true;
    }

    bool QuoteCache::CanBuy(std::uint32_t key, std::int64_t money) const
    {
        if (!m_authoritative)
            return true; // Preserve the existing original/Solo server-side wallet check.
        Quote quote;
        return TryGet(key, quote) && quote.payable && money >= quote.totalPrice;
    }
}
