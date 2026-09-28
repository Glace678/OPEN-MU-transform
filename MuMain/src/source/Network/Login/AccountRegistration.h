#pragma once

#include <string>

namespace Network::Login
{
    // Result of an account self-service POST against the embedded admin panel.
    struct ServiceResult
    {
        bool transportOk = false;  // false: the HTTP request itself failed (server down / timeout)
        bool success = false;      // server answered and performed the action
        std::string code;          // stable server result code
    };

    // Back-compat alias for the registration call sites.
    using RegistrationResult = ServiceResult;

    // Posts the registration to http://<serverHost>:<adminPanelPort>/api/registration/create.
    // Synchronous (short timeouts); the caller runs it directly on a button click.
    ServiceResult PostAccountRegistration(const wchar_t* serverHost,
        const std::wstring& loginName, const std::wstring& password);

    // Posts a password change (/api/registration/change-password). The old password
    // proves account ownership, so no login session is required.
    ServiceResult PostPasswordChange(const wchar_t* serverHost,
        const std::wstring& loginName, const std::wstring& oldPassword,
        const std::wstring& newPassword);

    // Posts a loopback password reset (/api/registration/reset-password).
    // The server rejects callers that are not on the same machine.
    ServiceResult PostPasswordReset(const wchar_t* serverHost,
        const std::wstring& loginName, const std::wstring& newPassword);

    // Port the embedded admin panel listens on (LocalStackSettings default 5080).
    inline constexpr int AdminPanelPort = 5080;
}
