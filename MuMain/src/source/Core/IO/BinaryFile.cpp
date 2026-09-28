#include "stdafx.h"
#include "Core/IO/BinaryFile.h"

#include <cstdio>
#include <memory>

namespace Core::IO
{
    std::optional<std::vector<std::uint8_t>> ReadBinaryFile(const wchar_t* path, std::size_t maximumSize)
    {
        if (path == nullptr)
            return std::nullopt;

        using FileHandle = std::unique_ptr<FILE, decltype(&fclose)>;
        FileHandle file(_wfopen(path, L"rb"), &fclose);
        if (!file || fseek(file.get(), 0, SEEK_END) != 0)
            return std::nullopt;

        const auto size = ftell(file.get());
        if (size < 0 || static_cast<std::size_t>(size) > maximumSize ||
            fseek(file.get(), 0, SEEK_SET) != 0)
            return std::nullopt;

        std::vector<std::uint8_t> bytes(static_cast<std::size_t>(size));
        if (fread(bytes.data(), 1, bytes.size(), file.get()) != bytes.size())
            return std::nullopt;
        return bytes;
    }
}
