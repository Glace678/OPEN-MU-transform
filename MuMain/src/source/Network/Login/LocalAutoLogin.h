#pragma once

#include <string>
#include <string_view>

namespace Network::Login
{
    inline constexpr char LocalAutoLoginEnvironment[] = "MU_LOCAL_AUTO_LOGIN";
    inline constexpr char MobileLocalAutoLoginEnvironment[] = "MU_MOBILE_LOCAL_AUTO_LOGIN";

    // One startup attempt. Desktop launchers stay loopback-only; the Android
    // package may opt into a private IPv4 endpoint and pins the redirect to it.
    class LocalAutoLogin
    {
    public:
        static LocalAutoLogin& Instance();

        void Initialize(bool enabled, std::wstring_view host, bool hasSavedCredentials,
            bool allowTrustedRemote = false);
        bool CanSelectServer() const;
        void ServerSelected();
        void ServerAddressReceived(std::wstring_view host);
        bool TryBeginLogin();
        void Cancel();

    private:
        enum class Stage { Disabled, SelectingServer, WaitingForAddress, WaitingForHello, Finished };

        bool m_initialized = false;
        Stage m_stage = Stage::Disabled;
        std::wstring m_expectedHost;
    };
}
