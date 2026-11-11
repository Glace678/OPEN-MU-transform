// MultiLanguage.h: interface for the CMultiLanguage class.
//////////////////////////////////////////////////////////////////////

#pragma once

#include <cstddef>
#include <cstdint>
#include <string>
#include <type_traits>

class CMultiLanguage
{
private:
    static CMultiLanguage* ms_Singleton;

    BYTE byLanguage;

    CMultiLanguage() { ms_Singleton = this; };

public:
    CMultiLanguage(std::wstring strSelectedML);
    ~CMultiLanguage() { ms_Singleton = 0; };

    BYTE GetLanguage();  // Getters

    WPARAM ConvertFulltoHalfWidthChar(DWORD wParam);

    static int32_t ConvertFromUtf8Buffer(wchar_t* target, size_t targetCapacity, const char* source,
                                         int maxSourceLength = -1);

    template <size_t N>
    static int32_t ConvertFromUtf8(wchar_t (&target)[N], const char* source, int maxSourceLength = -1)
    {
        return ConvertFromUtf8Buffer(target, N, source, maxSourceLength);
    }

    template <typename TPtr,
              std::enable_if_t<!std::is_array<std::remove_reference_t<TPtr>>::value &&
                                   std::is_convertible<TPtr, wchar_t*>::value,
                               int> = 0>
    static int32_t ConvertFromUtf8(TPtr&& target, const char* source, int maxSourceLength = -1)
    {
        return ConvertFromUtf8Buffer(target, 0, source, maxSourceLength);
    }

    static int32_t ConvertToUtf8(char* target, const wchar_t* source, int maxSourceLength = -1);

    static CMultiLanguage* GetSingletonPtr() { return ms_Singleton; };
};
