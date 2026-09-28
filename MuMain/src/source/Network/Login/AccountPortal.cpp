#include "Network/Login/AccountPortal.h"

#include <algorithm>
#include <array>
#include <charconv>
#include <optional>

namespace Network::Login
{
    namespace
    {
        std::optional<std::string> NarrowAscii(std::wstring_view value)
        {
            std::string result;
            for (const wchar_t character : value)
            {
                if (static_cast<unsigned int>(character) > 127) return std::nullopt;
                result.push_back(static_cast<char>(character));
            }
            return result;
        }

        bool ParseNumber(std::string_view value, unsigned int maximum, unsigned int& number)
        {
            if (value.empty()) return false;
            const auto [end, error] = std::from_chars(value.data(), value.data() + value.size(), number);
            return error == std::errc{} && end == value.data() + value.size() && number <= maximum;
        }

        bool ParseIpv4(std::string_view host, std::array<unsigned int, 4>& octets)
        {
            for (std::size_t index = 0; index < octets.size(); ++index)
            {
                const auto dot = host.find('.');
                const auto part = host.substr(0, dot);
                if ((part.size() > 1 && part.front() == '0') || !ParseNumber(part, 255, octets[index]))
                    return false;
                if (index == 3) return dot == std::string_view::npos;
                if (dot == std::string_view::npos) return false;
                host.remove_prefix(dot + 1);
            }
            return false;
        }

        bool IsDnsHost(std::string_view host)
        {
            if (host.empty() || host.size() > 253) return false;
            bool hasLetter = false;
            while (!host.empty())
            {
                const auto dot = host.find('.');
                const auto label = host.substr(0, dot);
                if (label.empty() || label.size() > 63 || label.front() == '-' || label.back() == '-')
                    return false;
                for (const char value : label)
                {
                    const bool letter = (value >= 'a' && value <= 'z') || (value >= 'A' && value <= 'Z');
                    hasLetter = hasLetter || letter;
                    if (!letter && !(value >= '0' && value <= '9') && value != '-') return false;
                }
                if (dot == std::string_view::npos) return hasLetter;
                host.remove_prefix(dot + 1);
                if (host.empty()) return false;
            }
            return false;
        }

        bool IsAllowedAuthority(std::string_view authority, bool https, bool allowPrivateHttp)
        {
            std::string_view host = authority;
            std::string_view port;
            if (host.starts_with("[::1]"))
            {
                if (host.size() > 5 && host[5] != ':') return false;
                port = host.size() > 5 ? host.substr(6) : std::string_view{};
                host = host.substr(0, 5);
            }
            else if (const auto colon = host.find(':'); colon != std::string_view::npos)
            {
                port = host.substr(colon + 1);
                host = host.substr(0, colon);
            }
            if (authority.ends_with(':')) return false;
            if (!port.empty())
            {
                unsigned int number = 0;
                if (!ParseNumber(port, 65535, number) || number == 0) return false;
            }
            if (host == "localhost" || host == "[::1]") return true;
            std::array<unsigned int, 4> octets{};
            if (ParseIpv4(host, octets))
            {
                const bool loopback = octets[0] == 127;
                const bool privateAddress = octets[0] == 10
                    || (octets[0] == 172 && octets[1] >= 16 && octets[1] <= 31)
                    || (octets[0] == 192 && octets[1] == 168);
                return https || loopback || (allowPrivateHttp && privateAddress);
            }
            return https && IsDnsHost(host);
        }

        std::string_view PortalViewName(AccountPortalView view)
        {
            switch (view)
            {
            case AccountPortalView::Register: return "register";
            case AccountPortalView::ChangePassword: return "change-password";
            case AccountPortalView::ResetPassword: return "reset-password";
            }
            return {};
        }
    }

    bool IsLoopbackAccountHost(std::wstring_view host)
    {
        if (host == L"localhost" || host == L"::1" || host == L"[::1]") return true;
        std::string ascii;
        for (const wchar_t value : host)
        {
            if (static_cast<unsigned int>(value) > 127) return false;
            ascii.push_back(static_cast<char>(value));
        }
        std::array<unsigned int, 4> octets{};
        return ParseIpv4(ascii, octets) && octets[0] == 127;
    }

    std::string BuildAccountPortalUrl(std::string_view origin, AccountPortalView view,
        std::string_view culture, bool allowPrivateHttp)
    {
        if (origin.empty() || origin.size() > 300) return {};
        if (origin.ends_with('/')) origin.remove_suffix(1);
        const bool https = origin.starts_with("https://");
        const bool http = origin.starts_with("http://");
        if (!https && !http) return {};
        if (!IsAllowedAuthority(origin.substr(https ? 8 : 7), https, allowPrivateHttp)) return {};
        const auto name = PortalViewName(view);
        if (name.empty()) return {};
        constexpr std::array<std::string_view, 15> cultures {
            "en", "zh-CN", "zh-TW", "ja", "ko", "de", "es", "fr", "pt", "ru", "uk", "pl", "id", "vi", "tl" };
        if (std::find(cultures.begin(), cultures.end(), culture) == cultures.end()) culture = "en";
        std::string url(origin);
        if (https) url += "/" + std::string(name) + "?culture=";
        else url += "/_content/MUnique.OpenMU.Web.AdminPanel/player-portal/index.html?view="
            + std::string(name) + "&culture=";
        return url + std::string(culture);
    }

    std::string BuildAccountPortalUrl(std::wstring_view origin, AccountPortalView view,
        std::wstring_view culture, bool allowPrivateHttp)
    {
        const auto asciiOrigin = NarrowAscii(origin);
        const auto asciiCulture = NarrowAscii(culture);
        if (!asciiOrigin || !asciiCulture) return {};
        return BuildAccountPortalUrl(*asciiOrigin, view, *asciiCulture, allowPrivateHttp);
    }
}
