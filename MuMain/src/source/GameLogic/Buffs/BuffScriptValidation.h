#pragma once

// Pure validation rules for the binary buff script format. Extracted from
// BuffScriptLoader::Load (XC-12) so the bounds on the file-supplied record
// count can be unit tested without dragging in Win32/UI dependencies
// (XC-17). The loader MUST call these instead of carrying its own copies.

#include <cstdint>
#include <limits>

namespace BuffScriptValidation
{
    // Upper bound on records a buff file may declare. Generous against the
    // real data (a few hundred records) while bounding allocations.
    constexpr std::uint32_t MaximumBuffRecords = 10000;

    // True when the declared record count and the per-record size are safe to
    // allocate and multiply (also guards the multiplication against
    // overflow).
    constexpr bool IsValidRecordCount(std::uint32_t listSize, std::uint32_t structSize)
    {
        return listSize > 0
            && listSize <= MaximumBuffRecords
            && structSize > 0
            && structSize <= std::numeric_limits<std::uint32_t>::max() / listSize;
    }
}
