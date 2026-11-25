//************************************************************************
//
// Decompiled by @myheart, @synth3r
// <https://forum.ragezone.com/members/2000236254.html>
//
//
// FILE: ShopList.cpp
//
//

#include "stdafx.h"
#ifdef KJH_ADD_INGAMESHOP_UI_SYSTEM
#include "ShopList.h"

#include <filesystem>
#include <fstream>

CShopList::CShopList()  // OK
{
    this->m_CategoryListPtr = new CShopCategoryList;
    this->m_PackageListPtr  = new CShopPackageList;
    this->m_ProductListPtr  = new CShopProductList;
}

CShopList::~CShopList()  // OK
{
    SafeDelete(m_CategoryListPtr);
    SafeDelete(m_PackageListPtr);
    SafeDelete(m_ProductListPtr);
}

WZResult CShopList::LoadCategroy(const wchar_t* szFilePath)  // OK
{
    WZResult result;

    if (!szFilePath || !*szFilePath)
    {
        result.SetResult(PT_LOADLIBRARY, ERROR_INVALID_PARAMETER, L"package file open fail");
        return result;
    }

    FILE_ENCODE enc = this->IsFileEncodingUtf8(szFilePath);

    std::ifstream ifs;

    ifs.open(std::filesystem::path(szFilePath), std::ifstream::in);

    DWORD LastError = GetLastError();

    for (int n = 0; !ifs.is_open() && n < 10; ++n)
    {
        Sleep(0x64);
        ifs.open(std::filesystem::path(szFilePath), std::ifstream::in);
        LastError = GetLastError();
    }

    if (ifs.is_open())
    {
        this->GetCategoryListPtr()->Clear();

        const std::vector<std::wstring> lines = this->ReadShopListLines(szFilePath, enc);
        const int linesRead  = static_cast<int>(lines.size());
        int rowsParsed = 0;

        for (const std::wstring& data : lines)
        {
            CShopCategory cat;

            if (cat.SetCategory(data))
            {
                this->GetCategoryListPtr()->Append(cat);
                ++rowsParsed;
            }
        }

        ifs.close();

        // Make a zero-row decode impossible to mistake for success.
        // If the decoder ever mangles the catalog again, rowsParsed=0 (with a
        // non-zero linesRead) will be obvious in muConsoleDebug instead of
        // silently producing an empty shop grid with no error dialog.
        g_ConsoleDebug->Write(MCD_NORMAL,
                              L"[XShop] LoadCategroy: parsed %d category rows from %d lines (encode=%d) <%ls>",
                              rowsParsed, linesRead, (int)enc, szFilePath);
    }
    else
    {
        result.SetResult(PT_LOADLIBRARY, LastError, L"package file open fail");
    }

    return result;
}

WZResult CShopList::LoadPackage(const wchar_t* szFilePath)  // OK
{
    WZResult result;

    if (!szFilePath || !*szFilePath)
    {
        result.SetResult(4, ERROR_INVALID_PARAMETER, L"package file open fail");
        return result;
    }

    FILE_ENCODE enc = this->IsFileEncodingUtf8(szFilePath);

    std::ifstream ifs;

    ifs.open(std::filesystem::path(szFilePath), std::ifstream::in);

    DWORD LastError = GetLastError();

    for (int n = 0; !ifs.is_open() && n < 10; ++n)
    {
        Sleep(0x64);
        ifs.open(std::filesystem::path(szFilePath), std::ifstream::in);
        LastError = GetLastError();
    }

    if (ifs.is_open())
    {
        this->GetPackageListPtr()->Clear();

        for (const std::wstring& data : this->ReadShopListLines(szFilePath, enc))
        {
            CShopPackage pack;

            if (pack.SetPackage(data))
            {
                this->GetPackageListPtr()->Append(pack);
                this->GetCategoryListPtr()->InsertPackage(pack.ProductDisplaySeq, pack.PackageProductSeq);
            }
        }

        ifs.close();
    }
    else
    {
        result.SetResult(4, LastError, L"package file open fail");
    }

    return result;
}

WZResult CShopList::LoadProduct(const wchar_t* szFilePath)  // OK
{
    WZResult result;

    result.BuildSuccessResult();

    if (!szFilePath || !*szFilePath)
    {
        result.SetResult(4, ERROR_INVALID_PARAMETER, L"package file open fail");
        return result;
    }

    FILE_ENCODE enc = this->IsFileEncodingUtf8(szFilePath);

    std::ifstream ifs;

    ifs.open(std::filesystem::path(szFilePath), std::ifstream::in);

    DWORD LastError = GetLastError();

    for (int n = 0; !ifs.is_open() && n < 10; ++n)
    {
        Sleep(0x64);
        ifs.open(std::filesystem::path(szFilePath), std::ifstream::in);
        LastError = GetLastError();
    }

    if (ifs.is_open())
    {
        this->GetProductListPtr()->Clear();

        for (const std::wstring& data : this->ReadShopListLines(szFilePath, enc))
        {
            CShopProduct product;

            if (product.SetProduct(data))
            {
                this->GetProductListPtr()->Append(product);
            }
        }

        ifs.close();
    }
    else
    {
        result.SetResult(4, LastError, L"package file open fail");
    }

    return result;
}

void CShopList::SetCategoryListPtr(CShopCategoryList* CategoryListPtr)  // OK
{
    m_CategoryListPtr = CategoryListPtr;
}

void CShopList::SetPackageListPtr(CShopPackageList* PackagePtr)  // OK
{
    m_PackageListPtr = PackagePtr;
}

void CShopList::SetProductListPtr(CShopProductList* ProductListPtr)  // OK
{
    m_ProductListPtr = ProductListPtr;
}

FILE_ENCODE CShopList::IsFileEncodingUtf8(const wchar_t* szFilePath)  // OK
{
    if (!szFilePath || !*szFilePath)
    {
        return FE_ANSI;
    }

    std::ifstream ifs;

    ifs.open(std::filesystem::path(szFilePath), std::ifstream::in | std::ifstream::binary);

    if (!ifs.is_open())
    {
        return FE_ANSI;
    }

    unsigned char bom[3] = {0};

    ifs.read(reinterpret_cast<char*>(bom), sizeof(bom));
    const std::streamsize bytesRead = ifs.gcount();

    ifs.close();

    if (bytesRead >= 3 && bom[0] == 0xEF && bom[1] == 0xBB && bom[2] == 0xBF)
    {
        return FE_UTF8;
    }

    if (bytesRead >= 2 && bom[0] == 0xFF && bom[1] == 0xFE)
    {
        return FE_UNICODE;
    }

    return FE_ANSI;
}

std::wstring CShopList::DecodeShopListLine(const std::string& line, FILE_ENCODE encode)
{
    std::wstring result;

    if (line.empty())
    {
        return result;
    }

    if (encode == FE_UNICODE)
    {
        // The bytes are UTF-16LE code units. The line splitter already removed the BOM and
        // the wide line breaks, so decode each little-endian pair into a wide character.
        for (size_t i = 0; i + 1 < line.size(); i += 2)
        {
            const wchar_t ch = static_cast<wchar_t>(
                static_cast<unsigned char>(line[i]) |
                (static_cast<unsigned char>(line[i + 1]) << 8));
            if (ch == L'\0')
            {
                break;
            }

            result.push_back(ch);
        }

        return result;
    }

    std::string bytes = line;
    const UINT codePage = (encode == FE_UTF8) ? CP_UTF8 : CP_ACP;

    const int cchWideChar = MultiByteToWideChar(codePage, 0, bytes.data(), static_cast<int>(bytes.size()), nullptr, 0);
    if (cchWideChar <= 0)
    {
        return result;
    }

    result.resize(static_cast<size_t>(cchWideChar));
    MultiByteToWideChar(codePage, 0, bytes.data(), static_cast<int>(bytes.size()), &result[0], cchWideChar);

    while (!result.empty() && (result.back() == L'\r' || result.back() == L'\n'))
    {
        result.pop_back();
    }

    return result;
}

std::vector<std::wstring> CShopList::ReadShopListLines(const wchar_t* szFilePath, FILE_ENCODE encode)
{
    std::vector<std::wstring> lines;

    if (!szFilePath || !*szFilePath)
    {
        return lines;
    }

    std::ifstream ifs(std::filesystem::path(szFilePath), std::ifstream::in | std::ifstream::binary);
    if (!ifs.is_open())
    {
        return lines;
    }

    const std::string content((std::istreambuf_iterator<char>(ifs)), std::istreambuf_iterator<char>());
    ifs.close();

    if (encode == FE_UNICODE)
    {
        size_t pos = 0;
        if (content.size() >= 2 &&
            static_cast<unsigned char>(content[0]) == 0xFF &&
            static_cast<unsigned char>(content[1]) == 0xFE)
        {
            pos = 2;
        }

        std::wstring current;
        for (; pos + 1 < content.size(); pos += 2)
        {
            const wchar_t ch = static_cast<wchar_t>(
                static_cast<unsigned char>(content[pos]) |
                (static_cast<unsigned char>(content[pos + 1]) << 8));

            if (ch == L'\0')
            {
                break;
            }

            if (ch == L'\r')
            {
                const bool followedByLineFeed =
                    (pos + 3 < content.size()) &&
                    static_cast<unsigned char>(content[pos + 3]) == 0x0A;
                if (!followedByLineFeed)
                {
                    lines.push_back(current);
                    current.clear();
                }

                continue;
            }

            if (ch == L'\n')
            {
                lines.push_back(current);
                current.clear();
                continue;
            }

            current.push_back(ch);
        }

        if (!current.empty())
        {
            lines.push_back(current);
        }

        return lines;
    }

    size_t pos = 0;
    if (encode == FE_UTF8 && content.size() >= 3 &&
        static_cast<unsigned char>(content[0]) == 0xEF &&
        static_cast<unsigned char>(content[1]) == 0xBB &&
        static_cast<unsigned char>(content[2]) == 0xBF)
    {
        pos = 3;
    }

    std::string current;
    for (; pos < content.size(); ++pos)
    {
        const char ch = content[pos];
        if (ch == '\n' || ch == '\r')
        {
            if (!current.empty())
            {
                lines.push_back(this->DecodeShopListLine(current, encode));
                current.clear();
            }

            continue;
        }

        current.push_back(ch);
    }

    if (!current.empty())
    {
        lines.push_back(this->DecodeShopListLine(current, encode));
    }

    return lines;
}
