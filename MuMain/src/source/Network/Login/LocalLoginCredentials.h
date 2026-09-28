#pragma once

#include <string>
#include <string_view>

namespace Network::Login
{
    // Launcher credentials are used only when no saved account was selected.
    class LocalLoginCredentials
    {
    public:
        static LocalLoginCredentials FromEnvironment();
        static LocalLoginCredentials Parse(std::string_view username, std::string_view password);

        bool IsValid() const { return !m_username.empty() && !m_password.empty(); }
        const std::wstring& Username() const { return m_username; }
        const std::wstring& Password() const { return m_password; }

    private:
        std::wstring m_username;
        std::wstring m_password;
    };

    // Older bundled desktop launchers (and a direct double-click on Main.exe)
    // start the client without the MU_* launch environment. The installer writes
    // a "local-client.env" sidecar next to the per-installation config; this loads
    // it before GameConfig is constructed so every launch path gets the same
    // config path, solo profile and auto-login. Variables already present in the
    // real environment always win. Must run once, as early as possible in WinMain.
    void ApplyLaunchProfile();
}
