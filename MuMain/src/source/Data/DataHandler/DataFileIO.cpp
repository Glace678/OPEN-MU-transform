#include "stdafx.h"
#include "DataFileIO.h"
#include "Engine/Object/ZzzInfomation.h"
#include "Core/Platform/WinCompat.h"
#include <limits>

extern HWND g_hWnd;

namespace DataFileIO
{
namespace
{
constexpr size_t MaxAllowedDataFileBytes = 256u * 1024u * 1024u;

bool TryGetBufferSize(const IOConfig& config, size_t& outSize)
{
    if (config.itemSize <= 0 || config.itemCount <= 0)
    {
        return false;
    }

    const size_t itemSize  = static_cast<size_t>(config.itemSize);
    const size_t itemCount = static_cast<size_t>(config.itemCount);
    if (itemCount > MaxAllowedDataFileBytes / itemSize ||
        itemSize * itemCount > static_cast<size_t>((std::numeric_limits<int>::max)()))
    {
        return false;
    }

    outSize = itemSize * itemCount;
    return true;
}
}  // namespace

std::unique_ptr<BYTE[]> ReadBuffer(FILE* fp, const IOConfig& config, DWORD* outChecksum)
{
    if (fp == nullptr)
    {
        ShowErrorAndExit(L"Invalid file handle");
        return nullptr;
    }

    size_t bufferSize = 0;
    if (!TryGetBufferSize(config, bufferSize))
    {
        ShowErrorAndExit(L"Invalid data file buffer configuration");
        return nullptr;
    }

    auto buffer = std::make_unique<BYTE[]>(bufferSize);

    // Read buffer
    size_t bytesRead = fread(buffer.get(), bufferSize, 1, fp);
    if (bytesRead != 1)
    {
        ShowErrorAndExit(L"Failed to read data from file");
        return nullptr;
    }

    // Read checksum if requested
    if (outChecksum)
    {
        bytesRead = fread(outChecksum, sizeof(DWORD), 1, fp);
        if (bytesRead != 1)
        {
            ShowErrorAndExit(L"Failed to read checksum from file");
            return nullptr;
        }
    }

    return buffer;
}

bool VerifyChecksum(const BYTE* buffer, const IOConfig& config, DWORD expectedChecksum)
{
    size_t bufferSize = 0;
    if (buffer == nullptr || !TryGetBufferSize(config, bufferSize))
    {
        return false;
    }

    DWORD calculatedChecksum = GenerateCheckSum2(buffer, static_cast<int>(bufferSize), config.checksumKey);
    return (calculatedChecksum == expectedChecksum);
}

void DecryptBuffer(BYTE* buffer, const IOConfig& config)
{
    if (!config.decryptRecord || buffer == nullptr)
    {
        return;  // No decryption needed
    }

    size_t bufferSize = 0;
    if (!TryGetBufferSize(config, bufferSize))
    {
        return;
    }

    BYTE* pSeek = buffer;
    for (int i = 0; i < config.itemCount; i++)
    {
        config.decryptRecord(pSeek, config.itemSize);
        pSeek += config.itemSize;
    }
}

void ShowErrorAndExit(const wchar_t* message)
{
    const wchar_t* safeMessage = message ? message : L"Unknown data file error";
    g_ErrorReport.Write(L"%ls\r\n", safeMessage);
    MessageBox(g_hWnd, safeMessage, L"Data File Error", MB_OK | MB_ICONERROR);
    // Note: Application continues running - error handling left to caller
}

#ifdef _EDITOR
bool WriteBuffer(FILE* fp, BYTE* buffer, const IOConfig& config)
{
    size_t bufferSize = 0;
    if (fp == nullptr || buffer == nullptr || !TryGetBufferSize(config, bufferSize))
    {
        return false;
    }

    // Write buffer
    size_t bytesWritten = fwrite(buffer, bufferSize, 1, fp);
    if (bytesWritten != 1)
    {
        return false;
    }

    // Write checksum
    DWORD dwCheckSum = GenerateCheckSum2(buffer, static_cast<int>(bufferSize), config.checksumKey);
    bytesWritten     = fwrite(&dwCheckSum, sizeof(DWORD), 1, fp);
    if (bytesWritten != 1)
    {
        return false;
    }

    return true;
}

void EncryptBuffer(BYTE* buffer, const IOConfig& config)
{
    if (!config.encryptRecord || buffer == nullptr)
    {
        return;  // No encryption needed
    }

    size_t bufferSize = 0;
    if (!TryGetBufferSize(config, bufferSize))
    {
        return;
    }

    BYTE* pSeek = buffer;
    for (int i = 0; i < config.itemCount; i++)
    {
        config.encryptRecord(pSeek, config.itemSize);
        pSeek += config.itemSize;
    }
}
#endif
}
