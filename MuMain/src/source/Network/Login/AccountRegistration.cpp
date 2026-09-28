// Account self-service (register / change password / reset password): WinINet
// Legacy loopback-only POSTs to the embedded admin panel. Portable account
// buttons use the separately configured browser portal on every platform.

#include "stdafx.h"
#include "Network/Login/AccountRegistration.h"
#include "Network/Login/AccountPortal.h"

#if defined(_WIN32)

#include <wininet.h>
#include <string>

#pragma comment(lib, "wininet.lib")

namespace
{
    // UTF-16 -> UTF-8 for the JSON body.
    std::string ToUtf8(const std::wstring& value)
    {
        if (value.empty())
            return {};

        const int length = WideCharToMultiByte(CP_UTF8, 0, value.c_str(),
            static_cast<int>(value.size()), nullptr, 0, nullptr, nullptr);
        std::string result(length, '\0');
        if (length > 0)
        {
            WideCharToMultiByte(CP_UTF8, 0, value.c_str(), static_cast<int>(value.size()),
                result.data(), length, nullptr, nullptr);
        }

        return result;
    }

    // Minimal JSON string escaper. Inputs are restricted to printable ASCII,
    // but quote/backslash/newline are handled defensively anyway.
    std::string JsonEscape(const std::string& value)
    {
        std::string result;
        result.reserve(value.size() + 4);
        for (const unsigned char ch : value)
        {
            switch (ch)
            {
            case '"':  result += "\\\""; break;
            case '\\': result += "\\\\"; break;
            case '\b': result += "\\b"; break;
            case '\f': result += "\\f"; break;
            case '\n': result += "\\n"; break;
            case '\r': result += "\\r"; break;
            case '\t': result += "\\t"; break;
            default:
                if (ch < 0x20)
                {
                    char buffer[8];
                    sprintf_s(buffer, "\\u%04x", static_cast<unsigned int>(ch));
                    result += buffer;
                }
                else
                {
                    result += static_cast<char>(ch);
                }
                break;
            }
        }

        return result;
    }

    std::string JsonField(const char* name, const std::wstring& value)
    {
        return std::string("\"") + name + "\":\"" + JsonEscape(ToUtf8(value)) + "\"";
    }

    // Extracts the string value of "code":"xxx" from a small JSON response.
    std::string ExtractJsonCode(const std::string& body)
    {
        const std::string key = "\"code\":\"";
        const size_t start = body.find(key);
        if (start == std::string::npos)
            return {};

        const size_t valueStart = start + key.size();
        const size_t end = body.find('"', valueStart);
        if (end == std::string::npos)
            return {};

        return body.substr(valueStart, end - valueStart);
    }

    // POSTs a JSON body and returns the response body when the server answers 200.
    bool PostJson(const wchar_t* serverHost, const wchar_t* path,
                  const std::string& body, std::string& response)
    {
        if (serverHost == nullptr || !Network::Login::IsLoopbackAccountHost(serverHost))
            return false;

        HINTERNET hInternet = InternetOpenW(L"OpenMUClient/1.0",
            INTERNET_OPEN_TYPE_PRECONFIG, nullptr, nullptr, 0);
        if (hInternet == nullptr)
            return false;

        bool ok = false;
        HINTERNET hConnect = InternetConnectW(hInternet, serverHost,
            static_cast<INTERNET_PORT>(Network::Login::AdminPanelPort), nullptr, nullptr,
            INTERNET_SERVICE_HTTP, 0, 0);
        if (hConnect != nullptr)
        {
            HINTERNET hRequest = HttpOpenRequestW(hConnect, L"POST",
                path, nullptr, nullptr, nullptr, 0, 0);
            if (hRequest != nullptr)
            {
                // Keep a dead server from freezing the game for longer than a few seconds.
                const DWORD timeoutMs = 6000;
                InternetSetOptionW(hRequest, INTERNET_OPTION_CONNECT_TIMEOUT,
                    const_cast<DWORD*>(&timeoutMs), sizeof(timeoutMs));
                InternetSetOptionW(hRequest, INTERNET_OPTION_SEND_TIMEOUT,
                    const_cast<DWORD*>(&timeoutMs), sizeof(timeoutMs));
                InternetSetOptionW(hRequest, INTERNET_OPTION_RECEIVE_TIMEOUT,
                    const_cast<DWORD*>(&timeoutMs), sizeof(timeoutMs));

                static const wchar_t kHeaders[] = L"Content-Type: application/json; charset=utf-8";
                const BOOL sent = HttpSendRequestW(hRequest, kHeaders,
                    static_cast<DWORD>(wcslen(kHeaders)),
                    const_cast<char*>(body.data()), static_cast<DWORD>(body.size()));

                if (sent)
                {
                    DWORD statusCode = 0;
                    DWORD statusSize = sizeof(statusCode);
                    if (HttpQueryInfoW(hRequest,
                        HTTP_QUERY_STATUS_CODE | HTTP_QUERY_FLAG_NUMBER,
                        &statusCode, &statusSize, nullptr) && statusCode == 200)
                    {
                        char readBuffer[1024];
                        DWORD bytesRead = 0;
                        for (;;)
                        {
                            bytesRead = 0;
                            if (!InternetReadFile(hRequest, readBuffer,
                                static_cast<DWORD>(sizeof(readBuffer)), &bytesRead)
                                || bytesRead == 0)
                            {
                                break;
                            }

                            response.append(readBuffer, bytesRead);
                        }

                        ok = true;
                    }
                }

                InternetCloseHandle(hRequest);
            }

            InternetCloseHandle(hConnect);
        }

        InternetCloseHandle(hInternet);
        return ok;
    }

    Network::Login::ServiceResult RunServicePost(const wchar_t* serverHost,
        const wchar_t* path, const std::string& body)
    {
        Network::Login::ServiceResult result;
        std::string response;
        if (PostJson(serverHost, path, body, response))
        {
            result.transportOk = true;
            result.success = response.find("\"success\":true") != std::string::npos;
            result.code = ExtractJsonCode(response);
        }

        return result;
    }
}

namespace Network::Login
{
    ServiceResult PostAccountRegistration(const wchar_t* serverHost,
        const std::wstring& loginName, const std::wstring& password)
    {
        const std::string body =
            "{" + JsonField("loginName", loginName)
            + "," + JsonField("password", password)
            + "," + JsonField("confirmPassword", password) + "}";
        return RunServicePost(serverHost, L"/api/registration/create", body);
    }

    ServiceResult PostPasswordChange(const wchar_t* serverHost,
        const std::wstring& loginName, const std::wstring& oldPassword,
        const std::wstring& newPassword)
    {
        const std::string body =
            "{" + JsonField("loginName", loginName)
            + "," + JsonField("oldPassword", oldPassword)
            + "," + JsonField("newPassword", newPassword)
            + "," + JsonField("confirmNewPassword", newPassword) + "}";
        return RunServicePost(serverHost, L"/api/registration/change-password", body);
    }

    ServiceResult PostPasswordReset(const wchar_t* serverHost,
        const std::wstring& loginName, const std::wstring& newPassword)
    {
        const std::string body =
            "{" + JsonField("loginName", loginName)
            + "," + JsonField("newPassword", newPassword)
            + "," + JsonField("confirmNewPassword", newPassword) + "}";
        return RunServicePost(serverHost, L"/api/registration/reset-password", body);
    }
}

#else

namespace Network::Login
{
    ServiceResult PostAccountRegistration(const wchar_t* /*serverHost*/,
        const std::wstring& /*loginName*/, const std::wstring& /*password*/)
    {
        return ServiceResult {};
    }

    ServiceResult PostPasswordChange(const wchar_t* /*serverHost*/,
        const std::wstring& /*loginName*/, const std::wstring& /*oldPassword*/,
        const std::wstring& /*newPassword*/)
    {
        return ServiceResult {};
    }

    ServiceResult PostPasswordReset(const wchar_t* /*serverHost*/,
        const std::wstring& /*loginName*/, const std::wstring& /*newPassword*/)
    {
        return ServiceResult {};
    }
}

#endif
