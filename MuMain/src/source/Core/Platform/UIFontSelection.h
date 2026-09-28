#pragma once

#include <string_view>

namespace Core::Platform::Fonts
{
enum class UIFontPlatform
{
    Windows,
    Portable,
};

inline constexpr std::wstring_view SelectUIFontFamily(
    std::wstring_view configuredFamily,
    std::wstring_view uiLocale,
    std::wstring_view dataLanguage,
    UIFontPlatform platform)
{
    if (!configuredFamily.empty())
        return configuredFamily;

    if (uiLocale == L"ja" || dataLanguage == L"Jpn")
        return platform == UIFontPlatform::Windows ? L"Yu Gothic" : L"Noto Sans CJK JP";

    if (uiLocale == L"zh-TW" || dataLanguage == L"Cht")
        return platform == UIFontPlatform::Windows ? L"Microsoft JhengHei" : L"Noto Sans CJK TC";

    if (uiLocale == L"zh-CN" || dataLanguage == L"Chs")
        return L"Noto Sans CJK SC";

    return platform == UIFontPlatform::Windows ? L"Tahoma" : L"DejaVu Sans";
}
} // namespace Core::Platform::Fonts
