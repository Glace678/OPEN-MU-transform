#ifndef _IEXPLORER_H_
#define _IEXPLORER_H_

#pragma once

#include <string>
#include <string_view>

namespace leaf
{
/* Open URL for default web-browser    */
/* This function return process handle */
inline bool OpenExplorer(const std::wstring& url)
{
    std::wstring_view address(url);
    constexpr std::wstring_view https = L"https://";
    constexpr std::wstring_view http = L"http://";
    if (address.starts_with(https))
        address.remove_prefix(https.size());
    else if (address.starts_with(http))
        address.remove_prefix(http.size());
    else
        return false;

    const auto host = address.substr(0, address.find_first_of(L"/?#"));
    if (host.empty() || host.find_first_of(L"@\\") != std::wstring_view::npos ||
        address.find_first_of(L" \r\n\t") != std::wstring_view::npos)
        return false;

    // SW_SHOW
    INT_PTR result = (INT_PTR)ShellExecute(NULL, L"open", url.c_str(), NULL, NULL, SW_NORMAL);
    if (result > 32)
        return true;
    return false;
}
}

#endif  // _IEXPLORER_H_
