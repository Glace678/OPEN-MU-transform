//////////////////////////////////////////////////////////////////////////////
// TextListSafe.h
//
// Unified safe-append layer for the global tooltip line buffer
//   wchar_t TextList[50][100]; int TextListColor[50]; int TextBold[50];
// declared in Engine/Object/ZzzInventory.h.
//
// Every writer into TextList / TextListColor / TextBold must go through one of
// these helpers so the row index is always clamped to the 50-line capacity and
// the text is bounded to the 100-wide row. This closes the unbounded TextNum
// paths 86-01 (NewUIMasterLevel), 86-54 (NewUIBuffWindow) and 87-38
// (CSItemOption::RenderSetOptionList) in one place.
//
// Safety checks are runtime branches, never asserts.
//////////////////////////////////////////////////////////////////////////////
#pragma once

#include "Engine/Object/ZzzInventory.h"

#include <wchar.h>

#define TEXT_LIST_LINE_CAPACITY 50
#define TEXT_LIST_ROW_WIDTH     100

// Bounded append for a ready-made wide string. Copies `text` into the next
// row (truncating to the 100-wide row and NUL-terminating), records
// color/bold, and returns the advanced row index. If the buffer is already
// full (textNum >= 50) the line is dropped and textNum is returned unchanged.
inline int SafeAppendTextLine(int textNum, const wchar_t* text, int color = 0, int bold = 0)
{
    if (textNum < 0)
        textNum = 0;
    if (textNum >= TEXT_LIST_LINE_CAPACITY)
        return textNum;

    wchar_t* row = TextList[textNum];
    if (text != nullptr)
        ::wcsncpy_s(row, TEXT_LIST_ROW_WIDTH, text, _TRUNCATE);
    else
        row[0] = L'\0';

    TextListColor[textNum] = color;
    TextBold[textNum] = (bold != 0);
    return textNum + 1;
}

// Index guard for callers that fill the returned row themselves (for example
// via getExplainText(...)). Returns a writable row only when textNum is within
// [0, 50); otherwise returns nullptr and pins textNum at 0 so the caller can
// stop without ever indexing TextList out of bounds.
inline wchar_t* SafeTextRowForAppend(int& textNum)
{
    if (textNum < 0)
        textNum = 0;
    if (textNum >= TEXT_LIST_LINE_CAPACITY)
    {
        textNum = TEXT_LIST_LINE_CAPACITY;
        return nullptr;
    }
    return TextList[textNum];
}