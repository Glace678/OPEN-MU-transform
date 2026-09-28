// MultiLanguage.cpp: implementation of the CMultiLanguage class.
//////////////////////////////////////////////////////////////////////

#include "stdafx.h"

#include <cstring>

CMultiLanguage* CMultiLanguage::ms_Singleton = NULL;

CMultiLanguage::CMultiLanguage(std::wstring strSelectedML)
{
    ms_Singleton = this;

    if (wcsicmp(strSelectedML.c_str(), L"ENG") == 0)
    {
        byLanguage = 0;
    }
    else if (wcsicmp(strSelectedML.c_str(), L"POR") == 0)
    {
        byLanguage = 1;
    }
    else if (wcsicmp(strSelectedML.c_str(), L"SPN") == 0)
    {
        byLanguage = 2;
    }
    else
    {
        byLanguage = 0;
    }
}

BYTE CMultiLanguage::GetLanguage()
{
    return byLanguage;
}

/**
 * Converts a UTF-8 byte buffer to UTF-16.
 *
 * Reads up to @p maxSourceLength bytes from @p source (which may or may not
 * be null-terminated) and writes the converted UTF-16 characters to @p target.
 * The required number of UTF-16 characters is determined first using
 * MultiByteToWideChar().
 *
 * If a null terminator exists within the processed byte range, it is copied
 * by the conversion. Otherwise, the function appends one when possible.
 *
 * @param target Destination buffer for the UTF-16 output.
 * @param source UTF-8 encoded byte buffer.
 * @param maxSourceLength Maximum number of bytes to read from @p source.
 *
 * @return Number of UTF-16 characters written (excluding the null terminator
 *         when present). Returns 0 on failure.
 *
 * @note The caller must ensure @p target is large enough to hold the converted
 *       UTF-16 string plus a terminating null character.
 */
int32_t CMultiLanguage::ConvertFromUtf8(wchar_t* target, const char* source, int maxSourceLength)
{
    if (target == nullptr || source == nullptr)
    {
        return 0;
    }

    // Fixed-size on-disk text fields are byte-windowed rather than NUL-safe:
    // if the window ends in the middle of a multi-byte UTF-8 sequence, the
    // dangling lead byte converts to U+FFFD (shown as a stray diamond glyph).
    // When no NUL terminator lies inside the window, walk the end back to the
    // last complete code point boundary.
    if (maxSourceLength > 0)
    {
        bool terminatorInside = false;
        for (int i = 0; i < maxSourceLength; ++i)
        {
            if (source[i] == '\0')
            {
                terminatorInside = true;
                break;
            }
        }

        if (!terminatorInside)
        {
            int trailing = 0;
            int i = maxSourceLength - 1;
            while (i >= 0 && (static_cast<unsigned char>(source[i]) & 0xC0) == 0x80)
            {
                ++trailing;
                --i;
            }

            if (i >= 0 && trailing > 0 && trailing <= 3)
            {
                const unsigned char lead = static_cast<unsigned char>(source[i]);
                int expectedContinuation = 0;
                if ((lead & 0xE0) == 0xC0) expectedContinuation = 1;
                else if ((lead & 0xF0) == 0xE0) expectedContinuation = 2;
                else if ((lead & 0xF8) == 0xF0) expectedContinuation = 3;

                if (expectedContinuation > 0 && trailing < expectedContinuation)
                {
                    maxSourceLength = i; // drop the whole incomplete sequence
                }
            }
        }
    }

    // Determine how many UTF-16 characters are needed
    const int requiredChars = MultiByteToWideChar(CP_UTF8, 0, source, maxSourceLength, nullptr, 0);
    if (requiredChars <= 0)
    {
        target[0] = L'\0';
        return 0;
    }

    // Perform the conversion
    int written = MultiByteToWideChar(
        CP_UTF8,
        0,
        source,
        maxSourceLength,    // read at most this many bytes
        target,
        requiredChars       // assume destination large enough
    );

    if (written <= 0)
    {
        target[0] = L'\0';
        return 0;
    }

    // If the source contained a null terminator within the range,
    // MultiByteToWideChar copies it as well.
    if (written < maxSourceLength)
    {
        target[written] = L'\0';
    }

    return written;
}

int32_t CMultiLanguage::ConvertToUtf8(char* target, const wchar_t* source, int maxSourceLength)
{
    if (target == nullptr || source == nullptr)
    {
        return 0;
    }

    // In this codebase, maxSourceLength is effectively used as the destination buffer capacity.
    const int requiredBytesWithNull = WideCharToMultiByte(CP_UTF8, 0, source, -1, nullptr, 0, nullptr, nullptr);
    if (requiredBytesWithNull <= 0)
    {
        target[0] = '\0';
        return 0;
    }

    if (maxSourceLength > 0)
    {
        std::string tmp;
        tmp.resize(requiredBytesWithNull);
        const int writtenWithNull = WideCharToMultiByte(CP_UTF8, 0, source, -1, tmp.data(), requiredBytesWithNull, nullptr, nullptr);
        if (writtenWithNull <= 0)
        {
            target[0] = '\0';
            return 0;
        }

        const int available = std::max<int>(0, maxSourceLength - 1);
        const int srcLen = std::max<int>(0, writtenWithNull - 1);
        const int copyLen = std::min<int>(srcLen, available);

        if (copyLen > 0)
        {
            std::memcpy(target, tmp.data(), static_cast<size_t>(copyLen));
        }
        target[copyLen] = '\0';
        return copyLen;
    }

    const int requiredBytes = requiredBytesWithNull;
    if (requiredBytes <= 0)
    {
        target[0] = '\0';
        return 0;
    }

    const int written = WideCharToMultiByte(CP_UTF8, 0, source, -1, target, requiredBytes, nullptr, nullptr);
    if (written <= 0)
    {
        target[0] = '\0';
        return 0;
    }

    // When source length is -1, WinAPI includes the null terminator in 'written'.
    return written > 0 ? (written - 1) : 0;
}


WPARAM CMultiLanguage::ConvertFulltoHalfWidthChar(DWORD wParam)
{
    auto Char = (wchar_t)(wParam);

    if (Char >= 0xFF01 && Char <= 0xFF5A)
        wParam -= 0xFEE0;
    else if (Char == 0x3000)
        wParam = 0x0020;

    return wParam;
}