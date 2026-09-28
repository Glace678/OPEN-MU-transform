#pragma once

#include <string>
#include <string_view>

namespace Network::Login
{
    enum class AccountPortalView { Register, ChangePassword, ResetPassword };

    bool IsLoopbackAccountHost(std::wstring_view host);
    std::string BuildAccountPortalUrl(std::string_view origin, AccountPortalView view,
        std::string_view culture, bool allowPrivateHttp = false);
    std::string BuildAccountPortalUrl(std::wstring_view origin, AccountPortalView view,
        std::wstring_view culture, bool allowPrivateHttp = false);
}
