//************************************************************************
//
// Decompiled by @myheart, @synth3r
// <https://forum.ragezone.com/members/2000236254.html>
//
//
// FILE: Path.cpp
//
//

#include "stdafx.h"
#ifdef KJH_ADD_INGAMESHOP_UI_SYSTEM
#include "Path.h"

#include <cwchar>
#include <filesystem>
#include <fstream>
#include <string>
#include "Core/Platform/CrtDbg.h"
#include "Core/Platform/StrSafe.h"

#include <iterator>

TCHAR* Path::GetCurrentFullPath(TCHAR* szPath)
{
    if (!szPath)
        return 0;

    GetModuleFileName(0, szPath, MAX_PATH);

    return szPath;
}

TCHAR* Path::GetCurrentDirectory(TCHAR* szPath)
{
    if (!szPath)
        return 0;

    GetModuleFileName(0, szPath, MAX_PATH);

    wchar_t* chr = wcsrchr(szPath, '\\');

    if (!chr)
        return 0;

    (*chr) = 0;

    return szPath;
}

TCHAR* Path::GetCurrentFileName(TCHAR* szPath)
{
    if (!szPath)
        return 0;

    GetModuleFileName(0, szPath, MAX_PATH);

    return Path::GetFileName(szPath);
}

TCHAR* Path::SetDirString(TCHAR* szPath)
{
    if (!szPath)
        return 0;

    if ((*szPath))
    {
        std::size_t len = 0;
        if (FAILED(StringCchLength(szPath, MAX_PATH, &len)))
            return 0;

        if (len > 0 && szPath[len - 1] != '\\' && szPath[len - 1] != '/')
        {
            if (len + 1 >= MAX_PATH)
                return 0;

            szPath[len]     = '\\';
            szPath[len + 1] = 0;
        }
    }
    else
    {
        szPath[0] = '\\';
        szPath[1] = 0;
    }

    return szPath;
}

TCHAR* Path::ClearDirString(TCHAR* szPath)
{
    if (!szPath || !(*szPath))
        return szPath;

    std::size_t len = 0;
    if (FAILED(StringCchLength(szPath, MAX_PATH, &len)))
        return szPath;

    if (len > 0 && (szPath[len - 1] == '\\' || szPath[len - 1] == '/' || szPath[len - 1] == '"'))
    {
        szPath[len - 1] = 0;
    }

    if (len >= 2 && (szPath[len - 2] == '\\' || szPath[len - 2] == '/' || szPath[len - 2] == '"'))
    {
        szPath[len - 2] = 0;
    }

    if (szPath[0] == '"')
    {
        const std::size_t rem = wcslen(szPath + 1);
        wmemmove(szPath, szPath + 1, rem + 1);
    }

    return szPath;
}

TCHAR* Path::GetDirectory(TCHAR* szPath)
{
    if (!szPath || !(*szPath))
        return szPath;

    wchar_t* chrBs = wcsrchr(szPath, '\\');
    wchar_t* chrFs = wcsrchr(szPath, L'/');
    wchar_t* chr   = (chrBs && chrFs) ? ((chrBs > chrFs) ? chrBs : chrFs) : (chrBs ? chrBs : chrFs);

    if (chr)
        (*chr) = 0;
    else
        StringCchCopy(szPath, MAX_PATH, L".");

    return szPath;
}

TCHAR* Path::GetFileName(TCHAR* szPath)
{
    if (!szPath || !(*szPath))
        return szPath;

    wchar_t* chrBs = wcsrchr(szPath, L'\\');
    wchar_t* chrFs = wcsrchr(szPath, L'/');
    wchar_t* chr   = (chrBs && chrFs) ? ((chrBs > chrFs) ? chrBs : chrFs) : (chrBs ? chrBs : chrFs);

    if (chr)
    {
        const std::size_t rem = wcslen(chr + 1);
        wmemmove(szPath, chr + 1, rem + 1);
    }

    return szPath;
}

TCHAR* Path::ChangeSlashToBackSlash(TCHAR* szPath)
{
    if (!szPath || !(*szPath))
        return szPath;

    std::size_t len = 0;
    if (FAILED(StringCchLength(szPath, MAX_PATH, &len)))
        return szPath;

    for (std::size_t n = 0; n < len; n++)
    {
        if (szPath[n] == '\\')
            szPath[n] = '/';
    }

    return szPath;
}

TCHAR* Path::ChangeBackSlashToSlash(TCHAR* szPath)
{
    if (!szPath || !(*szPath))
        return szPath;

    std::size_t len = 0;
    if (FAILED(StringCchLength(szPath, MAX_PATH, &len)))
        return szPath;

    for (std::size_t n = 0; n < len; n++)
    {
        if (szPath[n] == '/')
            szPath[n] = '\\';
    }

    return szPath;
}

BOOL Path::ReadFileLastLine(TCHAR* szFile, TCHAR* szLastLine)
{
    if (!szFile || !szLastLine || !*szFile)
    {
        return 0;
    }

    szLastLine[0] = 0;

    std::ifstream ifs(std::filesystem::path(szFile), std::ifstream::in | std::ifstream::binary);
    if (!ifs.is_open())
    {
        return 0;
    }

    std::string line;
    std::string lastLine;
    while (std::getline(ifs, line))
    {
        while (!line.empty() && (line.back() == '\r' || line.back() == '\n'))
        {
            line.pop_back();
        }
        if (!line.empty())
        {
            lastLine = line;
        }
    }

    ifs.close();

    if (lastLine.empty())
    {
        return 0;
    }

    const char* src = lastLine.c_str();
    if (lastLine.size() >= 3 && static_cast<unsigned char>(src[0]) == 0xEF &&
        static_cast<unsigned char>(src[1]) == 0xBB && static_cast<unsigned char>(src[2]) == 0xBF)
    {
        src += 3;
    }

    int cchWide = MultiByteToWideChar(CP_UTF8, MB_ERR_INVALID_CHARS, src, -1, NULL, 0);
    UINT codePage = CP_UTF8;
    DWORD flags = MB_ERR_INVALID_CHARS;
    if (cchWide <= 0)
    {
        codePage = CP_ACP;
        flags = 0;
        cchWide = MultiByteToWideChar(codePage, flags, src, -1, NULL, 0);
    }

    if (cchWide <= 0)
    {
        return 0;
    }

    std::wstring wideLine(static_cast<std::size_t>(cchWide), L'\0');
    MultiByteToWideChar(codePage, flags, src, -1, wideLine.data(), cchWide);
    StringCchCopy(szLastLine, MAX_PATH, wideLine.c_str());

    std::size_t len = 0;
    if (FAILED(StringCchLength(szLastLine, MAX_PATH, &len)))
    {
        return 0;
    }

    return (len > 1) ? 1 : 0;
}

BOOL Path::WriteNewFile(TCHAR* szFile, TCHAR* szText, INT nTextSize)
{
    if (!szFile || !szText)
        return 0;

    std::wfstream ofs(std::filesystem::path(szFile), std::wfstream::out | std::wfstream::trunc | std::wfstream::binary);

    if (ofs.is_open())
    {
        ofs.seekp(0, std::ofstream::end);
        ofs.write(szText, nTextSize);
        ofs.close();

        return 1;
    }

    return 0;
}

BOOL Path::CreateDirectorys(TCHAR* szFilePath, BOOL bIsFile)

{
    if (!szFilePath || !(*szFilePath))
        return 0;

    wchar_t PathName[MAX_PATH] = {0};
    wchar_t buff2[MAX_PATH]    = {0};

    StringCchCopy(PathName, std::size(PathName), szFilePath);

    if (bIsFile)
        Path::GetDirectory(PathName);

    if (CreateDirectory(PathName, 0) != 0 || GetLastError() == ERROR_ALREADY_EXISTS)
    {
        return 1;
    }

    StringCchCopy(buff2, std::size(buff2), PathName);

    wchar_t* chr1 = wcsrchr(buff2, L'\\');
    wchar_t* chr2 = wcschr(buff2, L'\\');

    if (!chr1 || !chr2 || chr1 == chr2)
        return 0;

    (*chr1) = 0;

    if (!Path::CreateDirectorys(buff2, 0))
        return 0;

    return (CreateDirectory(PathName, 0) != 0 || GetLastError() == ERROR_ALREADY_EXISTS) ? 1 : 0;
}
#endif