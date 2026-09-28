#include "Network/Login/LocalAutoLogin.h"

#include <array>

namespace Network::Login
{
    namespace
    {
        constexpr std::wstring_view LocalHost = L"127.0.0.1";

        bool IsPrivateIpv4(std::wstring_view host)
        {
            std::array<unsigned int, 4> octets{};
            std::size_t octetIndex = 0;
            unsigned int value = 0;
            bool hasDigit = false;

            for (const auto character : host)
            {
                if (character == L'.')
                {
                    if (!hasDigit || value > 255 || octetIndex >= octets.size() - 1)
                    {
                        return false;
                    }

                    octets[octetIndex++] = value;
                    value = 0;
                    hasDigit = false;
                    continue;
                }

                if (character < L'0' || character > L'9')
                {
                    return false;
                }

                hasDigit = true;
                value = (value * 10) + static_cast<unsigned int>(character - L'0');
                if (value > 255)
                {
                    return false;
                }
            }

            if (!hasDigit || value > 255 || octetIndex != octets.size() - 1)
            {
                return false;
            }

            octets[octetIndex] = value;
            return octets[0] == 10
                || octets[0] == 127
                || (octets[0] == 172 && octets[1] >= 16 && octets[1] <= 31)
                || (octets[0] == 192 && octets[1] == 168)
                || (octets[0] == 169 && octets[1] == 254);
        }
    }

    LocalAutoLogin& LocalAutoLogin::Instance()
    {
        static LocalAutoLogin instance;
        return instance;
    }

    void LocalAutoLogin::Initialize(bool enabled, std::wstring_view host, bool hasSavedCredentials,
        bool allowTrustedRemote)
    {
        if (m_initialized)
        {
            return;
        }

        m_initialized = true;
        const bool endpointAllowed = host == LocalHost || (allowTrustedRemote && IsPrivateIpv4(host));
        if (enabled && endpointAllowed && hasSavedCredentials)
        {
            m_expectedHost.assign(host);
            m_stage = Stage::SelectingServer;
        }
    }

    bool LocalAutoLogin::CanSelectServer() const
    {
        return m_stage == Stage::SelectingServer;
    }

    void LocalAutoLogin::ServerSelected()
    {
        if (CanSelectServer())
        {
            m_stage = Stage::WaitingForAddress;
        }
    }

    void LocalAutoLogin::ServerAddressReceived(std::wstring_view host)
    {
        if (m_stage == Stage::WaitingForAddress)
        {
            m_stage = host == m_expectedHost ? Stage::WaitingForHello : Stage::Finished;
        }
        else
        {
            Cancel();
        }
    }

    bool LocalAutoLogin::TryBeginLogin()
    {
        if (m_stage != Stage::WaitingForHello)
        {
            return false;
        }

        m_stage = Stage::Finished;
        return true;
    }

    void LocalAutoLogin::Cancel()
    {
        m_stage = Stage::Finished;
    }
}
