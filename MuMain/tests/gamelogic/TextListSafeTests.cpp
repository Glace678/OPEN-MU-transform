// Tests for the unified TextList[50] safe-append layer (closes 86-01, 86-54,
// 87-38 against one wrapper): the row index is clamped to capacity and an
// over-long row is truncated/NUL-terminated.
#include "doctest.h"

#include "Engine/Object/TextListSafe.h"

#include <string>

// Definitions matching the (real) extern declarations.
wchar_t TextList[50][100];
int TextListColor[50];
int TextBold[50];

TEST_SUITE("SafeAppendTextLine")
{
    TEST_CASE("51st line is dropped instead of writing past TextList[50]")
    {
        ::memset(TextList, 0, sizeof(TextList));
        ::memset(TextListColor, 0, sizeof(TextListColor));
        ::memset(TextBold, 0, sizeof(TextBold));

        int n = 0;
        for (int i = 0; i < 60; ++i)
            n = SafeAppendTextLine(n, L"x", i, i & 1);

        CHECK(n == 50);                 // capped at capacity, not 60
        CHECK(TextListColor[49] == 49); // last accepted row
    }

    TEST_CASE("over-long row is truncated to 100 and NUL-terminated")
    {
        ::memset(TextList, 0, sizeof(TextList));

        std::wstring longText(200, L'A');
        int n = SafeAppendTextLine(0, longText.c_str(), 7, 1);

        CHECK(n == 1);
        CHECK(TextList[0][0] == L'A');
        CHECK(TextList[0][99] == L'\0');   // NUL-terminated within the 100-wide row
        CHECK(TextListColor[0] == 7);
        CHECK(TextBold[0] == 1);
    }

    TEST_CASE("SafeTextRowForAppend guards the index and returns nullptr at capacity")
    {
        int atCap = 50;
        CHECK(SafeTextRowForAppend(atCap) == nullptr);
        CHECK(atCap == 50);

        int neg = -3;
        CHECK(SafeTextRowForAppend(neg) == TextList[0]);
        CHECK(neg == 0);   // negative clamped
    }
}