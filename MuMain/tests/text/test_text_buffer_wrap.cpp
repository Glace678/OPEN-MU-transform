#include "doctest.h"
#include "Core/Text/TextLineWrap.h"
#include <algorithm>
#include <cwchar>

namespace
{
    int MeasureCharacters(const wchar_t*, std::size_t count) { return static_cast<int>(count); }
}

TEST_CASE("shop notices terminate every row in a previously uninitialized buffer")
{
    wchar_t output[4][64];
    std::fill_n(output[0], 4 * 64, L'Z');
    const auto count = WrapTextToBuffer(L"购买成功#物品已放入背包", output[0], 4, 64, 50, MeasureCharacters, false, L'#');
    REQUIRE(count == 2);
    CHECK(std::wstring(output[0]) == L"购买成功");
    CHECK(std::wstring(output[1]) == L"物品已放入背包");
    CHECK(output[2][0] == L'\0');
    CHECK(output[3][63] == L'\0');
}

TEST_CASE("paragraphs cannot write past the remaining rows")
{
    struct GuardedBuffer
    {
        wchar_t before = L'A';
        wchar_t rows[2][8] {};
        wchar_t after = L'B';
    } output;
    const auto count = WrapTextToBuffer(L"one#two#three#four", output.rows[0], 2, 8, 50, MeasureCharacters, false, L'#');
    CHECK(count == 2);
    CHECK(output.before == L'A');
    CHECK(output.after == L'B');
    CHECK(std::wstring(output.rows[1]) == L"two");
}

TEST_CASE("pixel wrapping also respects the capacity of each destination row")
{
    wchar_t output[3][5] {};
    const auto count = WrapTextToBuffer(L"abcdefghijk", output[0], 3, 5, 100, MeasureCharacters);
    REQUIRE(count == 3);
    CHECK(std::wstring(output[0]) == L"abcd");
    CHECK(std::wstring(output[1]) == L"efgh");
    CHECK(std::wstring(output[2]) == L"ijk");
}

TEST_CASE("paragraph delimiters and measured wrapping coexist")
{
    wchar_t output[4][16] {};
    const auto count = WrapTextToBuffer(L"one two#three", output[0], 4, 16, 5, MeasureCharacters, false, L'#');
    REQUIRE(count == 3);
    CHECK(std::wstring(output[0]) == L"one");
    CHECK(std::wstring(output[1]) == L"two");
    CHECK(std::wstring(output[2]) == L"three");
}

TEST_CASE("empty notices clear the previous contents")
{
    wchar_t output[2][8];
    std::fill_n(output[0], 16, L'Z');
    CHECK(WrapTextToBuffer(L"", output[0], 2, 8, 30, MeasureCharacters) == 0);
    CHECK(output[0][0] == L'\0');
    CHECK(output[1][7] == L'\0');
}

TEST_CASE("an indented long word does not waste a row on an empty prefix")
{
    wchar_t output[3][8] {};
    const auto count = WrapTextToBuffer(L"abcdef", output[0], 3, 8, 4, MeasureCharacters, true);
    REQUIRE(count == 2);
    CHECK(std::wstring(output[0]) == L"abcd");
    CHECK(std::wstring(output[1]) == L"ef");
}

TEST_CASE("legacy chat indentation only reduces the first line width")
{
    wchar_t output[3][16] {};
    const auto count = WrapTextToBuffer(L"one two three", output[0], 3, 16, 9, MeasureCharacters, false, L'\n', 6);
    REQUIRE(count == 2);
    CHECK(std::wstring(output[0]) == L"one");
    CHECK(std::wstring(output[1]) == L"two three");
}

TEST_CASE("a name occupying the first line leaves the message for the next line")
{
    wchar_t output[3][16] {};
    const auto count = WrapTextToBuffer(L"hello", output[0], 3, 16, 9, MeasureCharacters, false, L'\n', 12);
    REQUIRE(count == 2);
    CHECK(output[0][0] == L'\0');
    CHECK(std::wstring(output[1]) == L"hello");
}
