#include <doctest.h>

#include "Network/Login/AccountPortal.h"

using Network::Login::AccountPortalView;
using Network::Login::BuildAccountPortalUrl;
using Network::Login::IsLoopbackAccountHost;

TEST_CASE("account portal uses explicit HTTPS origins and bounded view and culture")
{
    CHECK(BuildAccountPortalUrl("https://accounts.example.com/", AccountPortalView::Register, "zh-CN")
        == "https://accounts.example.com/register?culture=zh-CN");
    CHECK(BuildAccountPortalUrl("https://accounts.example.com:8443", AccountPortalView::ChangePassword, "ja")
        == "https://accounts.example.com:8443/change-password?culture=ja");
    CHECK(BuildAccountPortalUrl("https://accounts.example.com", AccountPortalView::ResetPassword, "en&bad=1")
        == "https://accounts.example.com/reset-password?culture=en");
    CHECK(BuildAccountPortalUrl("https://accounts.example.com", static_cast<AccountPortalView>(99), "en").empty());
}

TEST_CASE("account portal rejects unsafe and ambiguous origins before opening a browser")
{
    for (const auto origin : { "", "file:///accounts", "javascript:alert(1)", "https://user:pass@example.com",
        "https://example.com/path", "https://example.com?redirect=evil", "https://example.com#fragment",
        "https://example.com\\evil", "https://example.com\n", "https://example.com:0",
        "https://example.com:65536", "https://example.com:", "https://example..com", "https://-example.com",
        "https://example.com.", "https://0127.0.0.1", "http://2130706433", "http://127.1",
        "http://example.com", "http://192.168.1.20:5080", "http://localhost.example.com" })
    {
        INFO(origin);
        CHECK(BuildAccountPortalUrl(origin, AccountPortalView::Register, "en").empty());
    }
}

TEST_CASE("only explicit loopback or paired private HTTP portals can carry credentials")
{
    CHECK_FALSE(BuildAccountPortalUrl("http://127.0.0.1:5080", AccountPortalView::Register, "en").empty());
    CHECK_FALSE(BuildAccountPortalUrl("http://[::1]:5080", AccountPortalView::Register, "en").empty());
    CHECK_FALSE(BuildAccountPortalUrl("http://localhost:5080", AccountPortalView::Register, "en").empty());
    CHECK_FALSE(BuildAccountPortalUrl("http://192.168.1.20:5080", AccountPortalView::Register, "en", true).empty());
    CHECK(BuildAccountPortalUrl("http://203.0.113.20:5080", AccountPortalView::Register, "en", true).empty());
    CHECK(IsLoopbackAccountHost(L"127.0.0.2"));
    CHECK(IsLoopbackAccountHost(L"localhost"));
    CHECK(IsLoopbackAccountHost(L"::1"));
    CHECK_FALSE(IsLoopbackAccountHost(L"127.0.0.1.evil"));
    CHECK_FALSE(IsLoopbackAccountHost(L"192.168.1.20"));
    CHECK_FALSE(IsLoopbackAccountHost(L"0127.0.0.1"));
    CHECK_FALSE(IsLoopbackAccountHost(L"2130706433"));
}

TEST_CASE("wide configuration text is validated without lossy character conversion")
{
    CHECK(BuildAccountPortalUrl(L"https://accounts.example.com", AccountPortalView::Register, L"zh-TW")
        == "https://accounts.example.com/register?culture=zh-TW");
    CHECK(BuildAccountPortalUrl(L"https://accounts.example.com\u0120", AccountPortalView::Register, L"en").empty());
    CHECK(BuildAccountPortalUrl(L"https://accounts.example.com", AccountPortalView::Register, L"\u4e2d").empty());
}
