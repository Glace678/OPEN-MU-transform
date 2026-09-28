#include "doctest.h"
#include "Core/Platform/BundledFonts.h"
#include "Core/Platform/UIFontSelection.h"

#include <initializer_list>
#include <ostream>
#include <string>
#include <string_view>

using Core::Platform::Fonts::SelectUIFontFamily;
using Core::Platform::Fonts::UIFontPlatform;

TEST_CASE("portable Latin and Cyrillic defaults use a bundled font")
{
    for (const auto locale : { L"en", L"de", L"es", L"fr", L"id", L"pl", L"pt", L"ru", L"tl", L"uk", L"vi" })
        CHECK(SelectUIFontFamily({}, locale, L"Eng", UIFontPlatform::Portable) == L"DejaVu Sans");
}

TEST_CASE("portable Japanese defaults select JP rather than SC glyphs")
{
    CHECK(SelectUIFontFamily({}, L"ja", L"Eng", UIFontPlatform::Portable) == L"Noto Sans CJK JP");
    CHECK(SelectUIFontFamily({}, L"en", L"Jpn", UIFontPlatform::Portable) == L"Noto Sans CJK JP");
}

TEST_CASE("portable Traditional Chinese defaults select TC rather than SC glyphs")
{
    CHECK(SelectUIFontFamily({}, L"zh-TW", L"Eng", UIFontPlatform::Portable) == L"Noto Sans CJK TC");
    CHECK(SelectUIFontFamily({}, L"en", L"Cht", UIFontPlatform::Portable) == L"Noto Sans CJK TC");
}

TEST_CASE("Simplified Chinese defaults retain the bundled SC font on both platforms")
{
    for (const auto platform : { UIFontPlatform::Windows, UIFontPlatform::Portable })
    {
        CHECK(SelectUIFontFamily({}, L"zh-CN", L"Eng", platform) == L"Noto Sans CJK SC");
        CHECK(SelectUIFontFamily({}, L"en", L"Chs", platform) == L"Noto Sans CJK SC");
    }
}

TEST_CASE("Windows retains its historical Latin Japanese and Traditional Chinese defaults")
{
    CHECK(SelectUIFontFamily({}, L"en", L"Eng", UIFontPlatform::Windows) == L"Tahoma");
    CHECK(SelectUIFontFamily({}, L"ru", L"Eng", UIFontPlatform::Windows) == L"Tahoma");
    CHECK(SelectUIFontFamily({}, L"ja", L"Eng", UIFontPlatform::Windows) == L"Yu Gothic");
    CHECK(SelectUIFontFamily({}, L"en", L"Jpn", UIFontPlatform::Windows) == L"Yu Gothic");
    CHECK(SelectUIFontFamily({}, L"zh-TW", L"Eng", UIFontPlatform::Windows) == L"Microsoft JhengHei");
    CHECK(SelectUIFontFamily({}, L"en", L"Cht", UIFontPlatform::Windows) == L"Microsoft JhengHei");
}

TEST_CASE("explicit font configuration is never replaced by a locale default")
{
    for (const auto platform : { UIFontPlatform::Windows, UIFontPlatform::Portable })
    {
        for (const auto locale : { L"ja", L"zh-TW", L"zh-CN", L"en", L"uk" })
        {
            CHECK(SelectUIFontFamily(L"User Custom Font", locale, L"Jpn", platform) == L"User Custom Font");
            CHECK(SelectUIFontFamily(L"Noto Sans CJK SC", locale, L"Cht", platform) == L"Noto Sans CJK SC");
        }
    }
}

TEST_CASE("empty language selection still has a usable platform default")
{
    CHECK(SelectUIFontFamily({}, {}, {}, UIFontPlatform::Portable) == L"DejaVu Sans");
    CHECK(SelectUIFontFamily({}, {}, {}, UIFontPlatform::Windows) == L"Tahoma");
}

TEST_CASE("default selection preserves the existing legacy data language priority")
{
    CHECK(SelectUIFontFamily({}, L"zh-TW", L"Jpn", UIFontPlatform::Windows) == L"Yu Gothic");
    CHECK(SelectUIFontFamily({}, L"zh-TW", L"Jpn", UIFontPlatform::Portable) == L"Noto Sans CJK JP");
}

TEST_CASE("selected font views are owned before crossing the CreateFont boundary")
{
    std::wstring family;
    family = SelectUIFontFamily(family, L"ja", L"Eng", UIFontPlatform::Portable);
    CHECK(family == L"Noto Sans CJK JP");
    CHECK(family.c_str()[family.size()] == L'\0');
}

TEST_CASE("portable CJK defaults have both bundled weight paths")
{
    constexpr BundledFont expectedFonts[] = {
        { "Noto Sans CJK SC", "fonts/NotoSansCJKsc-Regular.otf", "fonts/NotoSansCJKsc-Bold.otf" },
        { "Noto Sans CJK JP", "fonts/NotoSansCJKjp-Regular.otf", "fonts/NotoSansCJKjp-Bold.otf" },
        { "Noto Sans CJK TC", "fonts/NotoSansCJKtc-Regular.otf", "fonts/NotoSansCJKtc-Bold.otf" },
    };
    for (const auto& expected : expectedFonts)
    {
        bool found = false;
        for (const auto& font : kBundledFonts)
        {
            if (std::string_view(font.family) != expected.family)
                continue;
            found = true;
            CHECK(std::string_view(font.regular) == expected.regular);
            CHECK(std::string_view(font.bold) == expected.bold);
        }
        CHECK(found);
    }
}
