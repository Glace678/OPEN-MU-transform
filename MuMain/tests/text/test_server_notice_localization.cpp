#include "doctest.h"
#include "I18N/All.h"
#include "UI/NewUI/HUD/ServerNoticeLocalization.h"

#include <string>

using UI::Notices::LocalizeServerNotice;

TEST_CASE("Simplified Chinese translates scheduled event countdowns")
{
    I18N::SetLocale("zh-CN");
    CHECK(LocalizeServerNotice(L"Blood Castle entrance is open and closes in 1 minute(s).")
        == L"血色城堡入口已开放，将于 1 分钟后关闭。");
    CHECK(LocalizeServerNotice(L"Chaos Castle entrance is open and closes in 5 minute(s).")
        == L"赤色要塞入口已开放，将于 5 分钟后关闭。");
    CHECK(LocalizeServerNotice(L"Devil Square entrance is open and closes in 120 minute(s).")
        == L"恶魔广场入口已开放，将于 120 分钟后关闭。");
    CHECK(LocalizeServerNotice(L"Blood Castle entrance is open and closes in 0 minute(s).")
        == L"血色城堡入口已开放，将于 0 分钟后关闭。");
}

TEST_CASE("Simplified Chinese translates event closures and happy hour")
{
    I18N::SetLocale("zh-CN");
    CHECK(LocalizeServerNotice(L"Blood Castle entrance closed.") == L"血色城堡入口已关闭。");
    CHECK(LocalizeServerNotice(L"Chaos Castle entrance closed.") == L"赤色要塞入口已关闭。");
    CHECK(LocalizeServerNotice(L"Devil Square entrance closed.") == L"恶魔广场入口已关闭。");
    CHECK(LocalizeServerNotice(L"Happy Hour event has been started!") == L"欢乐时光活动已开始！");
    CHECK(LocalizeServerNotice(L"Happy Hour event has ended!") == L"欢乐时光活动已结束！");
}

TEST_CASE("Custom and malformed notices remain verbatim")
{
    I18N::SetLocale("zh-CN");
    for (const auto* text : {
        L"", L"欢迎来到服务器！", L"Blood Castle guild recruiting!",
        L"Chaos Castle entrance is open and closes in soon minute(s).",
        L"Chaos Castle entrance is open and closes in -1 minute(s).",
        L"Chaos Castle entrance is open and closes in  minute(s).",
        L"Blood Castle entrance closed. Custom server message" })
    {
        CHECK(LocalizeServerNotice(text) == text);
    }
    const std::wstring longNotice(512, L'x');
    CHECK(LocalizeServerNotice(longNotice) == longNotice);
}

TEST_CASE("Changing UI locale does not translate English or Traditional Chinese notices")
{
    constexpr auto notice = L"Chaos Castle entrance is open and closes in 5 minute(s).";
    I18N::SetLocale("en");
    CHECK(LocalizeServerNotice(notice) == notice);
    I18N::SetLocale("zh-CN");
    CHECK(LocalizeServerNotice(notice) == L"赤色要塞入口已开放，将于 5 分钟后关闭。");
    I18N::SetLocale("zh-TW");
    CHECK(LocalizeServerNotice(notice) == notice);
}

TEST_CASE("ResxGen exposes the fifteen UI locales and new locales fall back to English")
{
    const auto locales = I18N::GetAvailableLocales();
    REQUIRE(locales.size() == 15);

    for (const auto* locale : {"fr", "ko", "vi"})
    {
        CHECK(I18N::LocaleIndex(locale) >= 0);
    }

    I18N::SetLocale("en");
    const std::wstring englishLanguage = I18N::Game::Language;
    for (const auto* locale : {"fr", "ko", "vi"})
    {
        I18N::SetLocale(locale);
        CHECK(std::wstring(I18N::Game::Language) == englishLanguage);
    }
    I18N::SetLocale("en");
}
