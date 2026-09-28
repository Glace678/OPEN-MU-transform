//*****************************************************************************
// File: UsefulDef.cpp
//
// Desc: ������ ���� ����.
//
// producer: Ahn Sang-Kyu
//*****************************************************************************

#include "stdafx.h"
#include "Core/Utilities/UsefulDef.h"
#include "Core/Text/TextLineWrap.h"
#include "UI/Legacy/UIControls.h"



bool ReduceStringByPixel(LPTSTR lpszDst, int nDstSize, LPCTSTR lpszSrc, int nPixel)
{
    if (lpszDst == nullptr || lpszSrc == nullptr || nDstSize <= 0)
        return false;
    SIZE size;
    GetTextExtentPoint32(g_pRenderText->GetFontDC(), lpszSrc, lstrlen(lpszSrc), &size);
    const float screenScale = g_fScreenRate_x > 0 ? g_fScreenRate_x : 1.0f;
    int nSrcWidth = int(size.cx / screenScale);

    if (nSrcWidth <= nPixel)
    {
        ::wcsncpy(lpszDst, lpszSrc, nDstSize - 1);
        lpszDst[nDstSize - 1] = '\0';
        return false;
    }

    ::wmemset(lpszDst, L'\0', nDstSize);
    constexpr int EllipsisLength = 3;
    if (nDstSize <= EllipsisLength)
    {
        std::fill_n(lpszDst, nDstSize - 1, L'.');
        return true;
    }
    ::CutText3(lpszSrc, lpszDst, nPixel - 6, 1, nDstSize);
    lpszDst[nDstSize - 4] = L'\0'; // reserve room for L"..."
    ::wcscat(lpszDst, L"...");
    return true;
}

int DivideStringByPixel(wchar_t* alpszDst, int nDstRow, int nDstColumn, const wchar_t* lpszSrc, int nPixelPerLine, bool bSpaceInsert, const wchar_t szNewlineChar)
{
    const auto measure = [](const wchar_t* text, std::size_t length)
    {
        SIZE size {};
        GetTextExtentPoint32(g_pRenderText->GetFontDC(), text, static_cast<int>(length), &size);
        const float screenScale = g_fScreenRate_x > 0 ? g_fScreenRate_x : 1.0f;
        return static_cast<int>(std::ceil(size.cx / screenScale));
    };
    return WrapTextToBuffer(lpszSrc, alpszDst, nDstRow, nDstColumn,
        nPixelPerLine, measure, bSpaceInsert, szNewlineChar);
}

int DivideString(LPTSTR alpszDst, int nDstRow, int nDstColumn, LPCTSTR lpszSrc)
{
    if (NULL == lpszSrc)
        return 0;

    int nSrcLen = ::wcslen(lpszSrc);
    if (0 == nSrcLen)
        return 0;

    int nSrcPos = 0;
    int nDstStart = 0;
    int nDstLen = 1;
    int nLineCount = 0;

    while (TRUE)
    {
        if (0x80 & lpszSrc[nSrcPos])
        {
            ++nSrcPos;
            ++nDstLen;
        }

        if ('/' == lpszSrc[nSrcPos])
        {
            ::wcsncpy(alpszDst + nLineCount * nDstColumn, lpszSrc + nDstStart, nDstLen - 1);
            ++nLineCount;
            nDstStart = nSrcPos + 1;
            nDstLen = 0;
        }
        else if (nDstLen >= nDstColumn)
        {
            nSrcPos -= 2;
            nDstLen -= 2;
            ::wcsncpy(alpszDst + nLineCount * nDstColumn, lpszSrc + nDstStart, nDstLen);
            ++nLineCount;
            nDstStart = nSrcPos + 1;
            nDstLen = 0;
        }
        else if (nSrcPos == nSrcLen - 1)
        {
            ::wcsncpy(alpszDst + nLineCount * nDstColumn, lpszSrc + nDstStart, nDstLen);
            break;
        }
        else if (nDstLen == nDstColumn - 1)
        {
            ::wcsncpy(alpszDst + nLineCount * nDstColumn, lpszSrc + nDstStart, nDstLen);
            ++nLineCount;
            nDstStart = nSrcPos + 1;
            nDstLen = 0;
        }

        if (nDstRow == nLineCount)
            break;

        ++nSrcPos;
        ++nDstLen;
    }

    return nLineCount + 1;
}

BOOL CheckErrString(LPTSTR lpszTarget)
{
    int i = 0;
    int nLen = ::wcslen(lpszTarget);
    while (i < nLen)
    {
        if (0x80 & lpszTarget[i])
        {
            if (i == nLen - 1)
            {
                lpszTarget[i] = 0;
                return FALSE;
            }
            else
                ++i;
        }
        ++i;
    }

    return TRUE;
}
