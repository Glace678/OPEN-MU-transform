#include "Network/Login/LocalLoginCredentials.h"

#include <algorithm>
#include <array>
#include <cctype>
#include <cstdlib>
#include <fstream>
#include <string>
#include <string_view>
#include <vector>

#include <SDL3/SDL_filesystem.h>
#include <SDL3/SDL_stdinc.h>

namespace Network::Login
{
    namespace
    {
        // Only the launch variables the single-player installer owns may be
        // provisioned through the sidecar; never let it touch the rest of the
        // process environment.
        constexpr std::array<std::string_view, 5> AllowedVariables {
            "MU_LOCAL_AUTO_LOGIN",
            "MU_LOCAL_GAME_USERNAME",
            "MU_LOCAL_GAME_PASSWORD",
            "MU_CONFIG_FILE",
            "MU_SOLO_BALANCE",
        };

        bool IsAllowedVariable(std::string_view name)
        {
            return std::find(AllowedVariables.begin(), AllowedVariables.end(), name)
                != AllowedVariables.end();
        }

        std::string Trim(std::string value)
        {
            const auto notSpace = [](unsigned char c) { return !std::isspace(c); };
            value.erase(value.begin(), std::find_if(value.begin(), value.end(), notSpace));
            value.erase(std::find_if(value.rbegin(), value.rend(), notSpace).base(), value.end());
            return value;
        }

        std::string ParentDirectory(std::string path)
        {
            while (!path.empty() && (path.back() == '/' || path.back() == '\\'))
                path.pop_back();
            const auto pos = path.find_last_of("/\\");
            return pos == std::string::npos ? std::string {} : path.substr(0, pos);
        }

        // Candidate sidecar locations, most specific first: next to an
        // environment-provided config, then the installed data directory and
        // the executable directory.
        std::vector<std::string> SidecarCandidates()
        {
            constexpr const char* kSidecarFileName = "local-client.env";
            std::vector<std::string> candidates;

            if (const char* configured = std::getenv("MU_CONFIG_FILE");
                configured != nullptr && configured[0] != '\0')
            {
                const std::string parent = ParentDirectory(configured);
                if (!parent.empty())
                    candidates.push_back(parent + "/" + kSidecarFileName);
            }

            if (const char* basePath = SDL_GetBasePath(); basePath != nullptr)
            {
                std::string base = basePath;
                candidates.push_back(base + "../../Data/Keys/" + kSidecarFileName);
                candidates.push_back(base + kSidecarFileName);
            }

            return candidates;
        }

        // std::getenv (used by the login/config code) reads the UCRT's cached
        // environment table, which SetEnvironmentVariableA does NOT update.
        // _putenv_s updates both that table and the Win32 process block, so the
        // variables become visible to getenv and to a later SDL environment
        // snapshot alike.
        int SetLaunchEnv(const char* name, const char* value)
        {
            // A real launcher-provided value always wins.
            if (std::getenv(name) != nullptr)
                return 0;
#ifdef _WIN32
            return _putenv_s(name, value) == 0 ? 0 : -1;
#else
            return SDL_setenv_unsafe(name, value, 1);
#endif
        }

        void ApplySidecar(const std::string& path)
        {
            std::ifstream file(path, std::ios::binary);
            if (!file.is_open())
                return;

            std::string line;
            while (std::getline(file, line))
            {
                if (!line.empty() && line.back() == '\r')
                    line.pop_back();
                const std::string trimmed = Trim(line);
                if (trimmed.empty() || trimmed[0] == '#')
                    continue;

                const auto equals = trimmed.find('=');
                if (equals == std::string::npos)
                    continue;

                const std::string name = Trim(trimmed.substr(0, equals));
                const std::string value = Trim(trimmed.substr(equals + 1));
                if (name.empty() || value.empty() || !IsAllowedVariable(name))
                    continue;

                SetLaunchEnv(name.c_str(), value.c_str());
            }
        }
    }

    void ApplyLaunchProfile()
    {
        for (const auto& candidate : SidecarCandidates())
        {
            std::ifstream probe(candidate, std::ios::binary);
            if (probe.good())
            {
                ApplySidecar(candidate);
                break;
            }
        }
    }

    LocalLoginCredentials LocalLoginCredentials::FromEnvironment()
    {
        const char* enabled = std::getenv("MU_LOCAL_AUTO_LOGIN");
        const char* username = std::getenv("MU_LOCAL_GAME_USERNAME");
        const char* password = std::getenv("MU_LOCAL_GAME_PASSWORD");
        if (!enabled || std::string_view(enabled) != "1" || !username || !password)
        {
            return {};
        }

        return Parse(username, password);
    }

    LocalLoginCredentials LocalLoginCredentials::Parse(std::string_view username, std::string_view password)
    {
        constexpr size_t MinimumUsernameLength = 3;
        constexpr size_t MaximumUsernameLength = 10;
        constexpr size_t MinimumPasswordLength = 12;
        constexpr size_t MaximumPasswordLength = 20;
        const auto isAlphanumeric = [](char c) {
            return (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9');
        };
        if (username.size() < MinimumUsernameLength || username.size() > MaximumUsernameLength
            || password.size() < MinimumPasswordLength || password.size() > MaximumPasswordLength
            || !std::all_of(username.begin(), username.end(), isAlphanumeric)
            || !std::all_of(password.begin(), password.end(),
                [&](char c) { return isAlphanumeric(c) || c == '-' || c == '_'; }))
        {
            return {};
        }

        LocalLoginCredentials credentials;
        credentials.m_username.assign(username.begin(), username.end());
        credentials.m_password.assign(password.begin(), password.end());
        return credentials;
    }
}
