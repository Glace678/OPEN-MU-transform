#include "ServerNoticeLocalization.h"
#include "I18N/All.h"

#include <cstring>
#include <cwchar>
#include <iterator>

namespace
{
    constexpr std::wstring_view entranceOpened = L" entrance is open and closes in ";
    constexpr std::wstring_view minutesSuffix = L" minute(s).";
    constexpr std::wstring_view entranceClosed = L" entrance closed.";
    constexpr size_t noticeCapacity = 256;

    std::wstring TranslateEntrance(std::wstring_view original, std::wstring_view remaining,
        const wchar_t* eventName)
    {
        wchar_t translated[noticeCapacity]{};
        int length = -1;
        if (remaining == entranceClosed)
        {
            length = std::swprintf(translated, std::size(translated),
                I18N::Game::EventEntranceClosedNotice, eventName);
        }
        else if (remaining.starts_with(entranceOpened) && remaining.ends_with(minutesSuffix))
        {
            const auto minutes = remaining.substr(entranceOpened.size(),
                remaining.size() - entranceOpened.size() - minutesSuffix.size());
            if (minutes.empty() || minutes.find_first_not_of(L"0123456789") != std::wstring_view::npos)
                return std::wstring(original);

            const std::wstring minuteText(minutes);
            length = std::swprintf(translated, std::size(translated),
                I18N::Game::EventEntranceOpenedNotice, eventName, minuteText.c_str());
        }
        if (length < 0 || static_cast<size_t>(length) >= std::size(translated))
            return std::wstring(original);
        return std::wstring(translated, length);
    }
}

namespace UI::Notices
{
    std::wstring LocalizeServerNotice(std::wstring_view text)
    {
        if (std::strcmp(I18N::GetCurrentLocale(), "zh-CN") != 0 || text.size() >= noticeCapacity)
            return std::wstring(text);

        if (text == L"Happy Hour event has been started!")
            return I18N::Game::HappyHourStartedNotice;
        if (text == L"Happy Hour event has ended!")
            return I18N::Game::HappyHourEndedNotice;

        constexpr std::wstring_view bloodCastle = L"Blood Castle";
        constexpr std::wstring_view chaosCastle = L"Chaos Castle";
        constexpr std::wstring_view devilSquare = L"Devil Square";
        if (text.starts_with(bloodCastle))
            return TranslateEntrance(text, text.substr(bloodCastle.size()), I18N::Game::BloodCastle);
        if (text.starts_with(chaosCastle))
            return TranslateEntrance(text, text.substr(chaosCastle.size()), I18N::Game::ChaosCastle);
        if (text.starts_with(devilSquare))
            return TranslateEntrance(text, text.substr(devilSquare.size()), I18N::Game::DevilSquare);
        return std::wstring(text);
    }
}
