#pragma once

#include <cstddef>
#include <cstdint>
#include <optional>
#include <vector>

namespace Core::IO
{
    std::optional<std::vector<std::uint8_t>> ReadBinaryFile(const wchar_t* path, std::size_t maximumSize);
}
