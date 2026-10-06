#pragma once

// Pure validation rules for the dconfig ID-history table. Extracted from
// CheckID_HistoryDay (XC-6) so the off-by-one-sensitive append bound can be
// unit tested on its own (XC-17).

#include <cstdint>

namespace DayHistoryValidation
{
    // The table stores exactly this many records (days[0..MaximumHistoryEntries-1]).
    constexpr std::uint16_t MaximumHistoryEntries = 100;

    // True when the record count read from a file header is readable in place.
    // A full table (num == MaximumHistoryEntries) is still valid here; only
    // appending to it is rejected (see IsAppendPositionValid).
    constexpr bool IsStoredCountValid(std::uint32_t num)
    {
        return num <= MaximumHistoryEntries;
    }

    // True when a new entry can be appended at index num. The old code only
    // rejected num > 100, writing days[100] out of bounds when a full file
    // (100 entries) met a new ID.
    constexpr bool IsAppendPositionValid(std::uint32_t num)
    {
        return num < MaximumHistoryEntries;
    }
}
