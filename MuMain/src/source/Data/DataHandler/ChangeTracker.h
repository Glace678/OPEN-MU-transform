#pragma once

#ifdef _EDITOR

#include <cwchar>
#include <sstream>
#include <string>
#include <type_traits>

/**
 * Simple change tracking for data structures
 *
 * Provides easy-to-use functions for comparing old vs new data
 * and logging all changes.
 */

namespace ChangeTracker
{
/**
     * Extract UTF-8 name from a struct with wchar_t Name field
     */
template <typename TStruct> inline std::string GetNameUtf8(int index, const TStruct& data, int maxNameLen)
{
    (void)index;
    using NameFieldType       = std::remove_cv_t<std::remove_reference_t<decltype(data.Name)>>;
    constexpr size_t arrayCap = std::is_array_v<NameFieldType> ? std::extent_v<NameFieldType> : 0;

    size_t limit = (maxNameLen > 0) ? static_cast<size_t>(maxNameLen) : arrayCap;
    if (arrayCap > 0 && (limit == 0 || limit > arrayCap))
    {
        limit = arrayCap;
    }

    const wchar_t* namePtr = data.Name;
    if (namePtr == nullptr)
    {
        return std::string();
    }

    const size_t wcharLen = (limit > 0) ? wcsnlen(namePtr, limit) : wcslen(namePtr);
    if (wcharLen == 0)
    {
        return std::string();
    }

    char      nameBuf[1024] = {0};
    const int result        = WideCharToMultiByte(CP_UTF8, 0, namePtr, static_cast<int>(wcharLen), nameBuf,
                                                  static_cast<int>(sizeof(nameBuf) - 1), NULL, NULL);
    if (result <= 0)
    {
        return std::string("[UTF-8 conversion failed]");
    }
    return std::string(nameBuf, static_cast<size_t>(result));
}

/**
     * Compare any two values and log the difference
     */
template <typename T>
inline void CompareField(const char* fieldName, const T& oldVal, const T& newVal, std::stringstream& changes,
                         bool& hasChanged)
{
    if (oldVal != newVal)
    {
        changes << "  " << fieldName << ": " << oldVal << " -> " << newVal << "\n";
        hasChanged = true;
    }
}

/**
     * Compare two bool values and log as true/false
     */
inline void CompareField(const char* fieldName, const bool& oldVal, const bool& newVal, std::stringstream& changes,
                         bool& hasChanged)
{
    if (oldVal != newVal)
    {
        changes << "  " << fieldName << ": " << (oldVal ? "true" : "false") << " -> " << (newVal ? "true" : "false")
                << "\n";
        hasChanged = true;
    }
}

/**
     * Compare two BYTE values and display as integers
     */
inline void CompareField(const char* fieldName, const BYTE& oldVal, const BYTE& newVal, std::stringstream& changes,
                         bool& hasChanged)
{
    if (oldVal != newVal)
    {
        changes << "  " << fieldName << ": " << (int)oldVal << " -> " << (int)newVal << "\n";
        hasChanged = true;
    }
}

/**
     * Compare two wide-character strings
     */
inline void CompareWideString(const char* fieldName, const wchar_t* oldVal, const wchar_t* newVal,
                              std::stringstream& changes, bool& hasChanged, int maxLen)
{
    const wchar_t* safeOld      = oldVal ? oldVal : L"";
    const wchar_t* safeNew      = newVal ? newVal : L"";
    const size_t   oldLen       = (maxLen > 0) ? wcsnlen(safeOld, static_cast<size_t>(maxLen)) : wcslen(safeOld);
    const size_t   newLen       = (maxLen > 0) ? wcsnlen(safeNew, static_cast<size_t>(maxLen)) : wcslen(safeNew);
    const bool     stringsEqual = (oldLen == newLen) && (oldLen == 0 || wmemcmp(safeOld, safeNew, oldLen) == 0);

    if (!stringsEqual)
    {
        char oldUtf8[1024] = {0};
        char newUtf8[1024] = {0};
        int  result1       = 1;
        int  result2       = 1;

        if (oldLen > 0)
        {
            result1 = WideCharToMultiByte(CP_UTF8, 0, safeOld, static_cast<int>(oldLen), oldUtf8,
                                          static_cast<int>(sizeof(oldUtf8) - 1), NULL, NULL);
        }
        if (newLen > 0)
        {
            result2 = WideCharToMultiByte(CP_UTF8, 0, safeNew, static_cast<int>(newLen), newUtf8,
                                          static_cast<int>(sizeof(newUtf8) - 1), NULL, NULL);
        }

        if (result1 <= 0 || result2 <= 0)
        {
            changes << "  " << fieldName << ": [UTF-8 conversion failed]\n";
        }
        else
        {
            changes << "  " << fieldName << ": \"" << oldUtf8 << "\" -> \"" << newUtf8 << "\"\n";
        }
        hasChanged = true;
    }
}
}

// ===========================================================================
// OLD MACRO-BASED COMPARISON (DEPRECATED - Use FieldMetadata.h instead!)
// ===========================================================================
// These macros are kept for backward compatibility only.
// New code should use CompareAllFieldsByMetadata() from FieldMetadata.h
// with C++20 constexpr field arrays instead.
//
// Example migration:
//   OLD: CompareAllFields(oldItem, newItem, changes, changed, MAX_ITEM_NAME, ITEM_FIELDS_SIMPLE, ITEM_FIELDS_ARRAYS);
//   NEW: CompareAllFieldsByMetadata(oldItem, newItem, ItemComparisonMetadata::SIMPLE_FIELDS, changes, changed);
//        CompareAllFieldsByMetadata(oldItem, newItem, ItemComparisonMetadata::ARRAY_FIELDS, changes, changed);
// ===========================================================================

#endif  // _EDITOR
