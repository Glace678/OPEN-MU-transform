#include "doctest.h"
#include "Network/Login/LocalAutoLogin.h"
#include "Network/Login/LocalLoginCredentials.h"

using Network::Login::LocalAutoLogin;
using Network::Login::LocalLoginCredentials;

TEST_CASE("First-run launcher credentials fit the normal login protocol")
{
    const auto credentials = LocalLoginCredentials::Parse("soloABC123", "AbCd0123456789_abc-Z");
    REQUIRE(credentials.IsValid());
    CHECK(credentials.Username() == L"soloABC123");
    CHECK(credentials.Password() == L"AbCd0123456789_abc-Z");
    LocalAutoLogin login;
    login.Initialize(true, L"127.0.0.1", credentials.IsValid());
    REQUIRE(login.CanSelectServer());
    login.ServerSelected();
    login.ServerAddressReceived(L"127.0.0.1");
    CHECK(login.TryBeginLogin());
    CHECK_FALSE(login.TryBeginLogin());
}

TEST_CASE("Malformed launcher credentials cannot be truncated or injected into login")
{
    for (auto username : {"", "a", "ab", "toolongname", "user name", "user\nname", "user/name"})
    {
        CHECK_FALSE(LocalLoginCredentials::Parse(username, "AbCd0123456789_abc-Z").IsValid());
    }
    for (auto password : {"", "short", "012345678901234567890", "invalid password", "invalid\npassword"})
    {
        CHECK_FALSE(LocalLoginCredentials::Parse("soloABC123", password).IsValid());
    }
}

TEST_CASE("Local automatic login requires opt-in, saved credentials and literal loopback")
{
    LocalAutoLogin disabled;
    disabled.Initialize(false, L"127.0.0.1", true);
    CHECK_FALSE(disabled.CanSelectServer());

    LocalAutoLogin missingCredentials;
    missingCredentials.Initialize(true, L"127.0.0.1", false);
    CHECK_FALSE(missingCredentials.CanSelectServer());

    for (auto host : {L"192.168.1.2", L"example.com", L"localhost", L"127.0.0.1.example.com", L""})
    {
        LocalAutoLogin remote;
        remote.Initialize(true, host, true);
        CHECK_FALSE(remote.CanSelectServer());
        CHECK_FALSE(remote.TryBeginLogin());
    }
}

TEST_CASE("Local automatic login waits for selection and a loopback game server")
{
    LocalAutoLogin login;
    login.Initialize(true, L"127.0.0.1", true);
    CHECK(login.CanSelectServer());
    CHECK_FALSE(login.TryBeginLogin());
    login.ServerSelected();
    CHECK_FALSE(login.CanSelectServer());
    CHECK_FALSE(login.TryBeginLogin());
    login.ServerAddressReceived(L"127.0.0.1");
    CHECK(login.TryBeginLogin());
    CHECK_FALSE(login.TryBeginLogin());
    login.Initialize(true, L"127.0.0.1", true);
    CHECK_FALSE(login.CanSelectServer());
}

TEST_CASE("A remote or unsolicited game server address cannot receive automatic credentials")
{
    LocalAutoLogin remote;
    remote.Initialize(true, L"127.0.0.1", true);
    remote.ServerSelected();
    remote.ServerAddressReceived(L"192.168.1.2");
    CHECK_FALSE(remote.TryBeginLogin());
    remote.ServerAddressReceived(L"127.0.0.1");
    CHECK_FALSE(remote.TryBeginLogin());

    LocalAutoLogin unsolicited;
    unsolicited.Initialize(true, L"127.0.0.1", true);
    unsolicited.ServerAddressReceived(L"127.0.0.1");
    CHECK_FALSE(unsolicited.TryBeginLogin());
}

TEST_CASE("Mobile automatic login accepts only a pinned private IPv4 game server")
{
    for (auto host : {L"10.0.0.8", L"127.0.0.2", L"172.16.5.10", L"172.31.255.254", L"192.168.1.2", L"169.254.10.20"})
    {
        LocalAutoLogin mobile;
        mobile.Initialize(true, host, true, true);
        REQUIRE(mobile.CanSelectServer());
        mobile.ServerSelected();
        mobile.ServerAddressReceived(host);
        CHECK(mobile.TryBeginLogin());
    }

    for (auto host : {L"8.8.8.8", L"172.15.1.1", L"172.32.1.1", L"example.com", L"", L"192.168.1"})
    {
        LocalAutoLogin mobile;
        mobile.Initialize(true, host, true, true);
        CHECK_FALSE(mobile.CanSelectServer());
    }

    LocalAutoLogin redirected;
    redirected.Initialize(true, L"192.168.1.2", true, true);
    REQUIRE(redirected.CanSelectServer());
    redirected.ServerSelected();
    redirected.ServerAddressReceived(L"192.168.1.3");
    CHECK_FALSE(redirected.TryBeginLogin());
}

TEST_CASE("Cancellation and repeated server addresses do not restart automatic login")
{
    LocalAutoLogin cancelled;
    cancelled.Initialize(true, L"127.0.0.1", true);
    cancelled.Cancel();
    cancelled.Initialize(true, L"127.0.0.1", true);
    cancelled.ServerSelected();
    cancelled.ServerAddressReceived(L"127.0.0.1");
    CHECK_FALSE(cancelled.TryBeginLogin());

    LocalAutoLogin duplicate;
    duplicate.Initialize(true, L"127.0.0.1", true);
    duplicate.ServerSelected();
    duplicate.ServerAddressReceived(L"127.0.0.1");
    duplicate.ServerAddressReceived(L"192.168.1.2");
    CHECK_FALSE(duplicate.TryBeginLogin());
}
