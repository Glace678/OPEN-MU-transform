#include "ServerNoticeLocalization.h"
#include "I18N/All.h"

#include <cstring>
#include <cwchar>
#include <iterator>

namespace
{
constexpr std::wstring_view entranceOpened = L" entrance is open and closes in ";
constexpr std::wstring_view minutesSuffix  = L" minute(s).";
constexpr std::wstring_view entranceClosed = L" entrance closed.";
constexpr size_t            noticeCapacity = 256;

std::wstring TranslateEntrance(std::wstring_view original, std::wstring_view remaining, const wchar_t* eventName)
{
    wchar_t translated[noticeCapacity]{};
    int     length = -1;
    if (remaining == entranceClosed)
    {
        length = std::swprintf(translated, std::size(translated), I18N::Game::EventEntranceClosedNotice, eventName);
    }
    else if (remaining.starts_with(entranceOpened) && remaining.ends_with(minutesSuffix))
    {
        const auto minutes =
            remaining.substr(entranceOpened.size(), remaining.size() - entranceOpened.size() - minutesSuffix.size());
        if (minutes.empty() || minutes.find_first_not_of(L"0123456789") != std::wstring_view::npos)
            return std::wstring(original);

        const std::wstring minuteText(minutes);
        length = std::swprintf(translated, std::size(translated), I18N::Game::EventEntranceOpenedNotice, eventName,
                               minuteText.c_str());
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
    if (text == L"You don't have enough Money" || text == L"You don't have enough Money.")
        return L"你的金币不足。";
    if (text == L"Inventory is full" || text == L"Inventory is full.")
        return L"背包已满。";
    if (text == L"Inventory got not enough space.")
        return L"背包空间不足。";
    if (text == L"You can't buy anymore.")
        return L"无法再购买更多物品。";
    if (text == L"No item there to repair.")
        return L"没有找到需要修理的物品。";
    if (text == L"Item cannot be repaired.")
        return L"该物品无法修理。";
    if (text == L"You don't have enough money to repair.")
        return L"你的金币不足以修理该物品。";

    constexpr std::wstring_view enteredGameSuffix = L" entered the game.";
    if (text.ends_with(enteredGameSuffix) && text.size() > enteredGameSuffix.size())
    {
        std::wstring playerName(text.substr(0, text.size() - enteredGameSuffix.size()));
        return playerName + L" 进入了游戏。";
    }

    if (text.starts_with(L"["))
    {
        const size_t closeBracket = text.find(L']');
        if (closeBracket != std::wstring_view::npos && closeBracket > 1)
        {
            struct MapNameEntry
            {
                std::wstring_view english;
                std::wstring_view chinese;
            };
            static constexpr MapNameEntry kMapNames[] = {
                {L"Lorencia", L"勇者大陆"},
                {L"Dungeon", L"地下城"},
                {L"Devias", L"冰风谷"},
                {L"Noria", L"仙踪林"},
                {L"Lost Tower", L"失落之塔"},
                {L"LostTower", L"失落之塔"},
                {L"Exile", L"流放地"},
                {L"Arena", L"古战场"},
                {L"Stadium", L"古战场"},
                {L"Atlans", L"亚特兰蒂斯"},
                {L"Tarkan", L"死亡沙漠"},
                {L"Devil Square", L"恶魔广场"},
                {L"Icarus", L"天空之城"},
                {L"Kalima", L"卡利玛"},
                {L"Valley of Loren", L"罗兰峡谷"},
                {L"Land of Trials", L"魔炼之地"},
                {L"Aida", L"幽暗森林"},
                {L"Crywolf Fortress", L"狼魂要塞"},
                {L"Crywolf", L"狼魂要塞"},
                {L"Barracks of Balgass", L"巴卡斯兵营"},
                {L"Balgass Refuge", L"巴卡斯休息室"},
                {L"Kanturu Ruins", L"坎特鲁废墟"},
                {L"Kanturu Relics", L"坎特鲁遗址"},
                {L"Kanturu Remain", L"坎特鲁遗址"},
                {L"Kanturu", L"坎特鲁废墟"},
                {L"Elbeland", L"幻术园"},
                {L"Swamp of Calmness", L"安宁池"},
                {L"Raklion", L"冰霜之城"},
                {L"LaCleon", L"冰霜之城"},
                {L"Vulcanus", L"囚禁之岛"},
                {L"Duel Arena", L"决斗场"},
                {L"Karutan 1", L"卡伦特1"},
                {L"Karutan 2", L"卡伦特2"},
                {L"Karutan", L"卡伦特"},
            };

            std::wstring_view insideBrackets = text.substr(1, closeBracket - 1);
            std::wstring      translatedMaps;
            bool              allTokensRecognized = true;
            bool              anyTokenTranslated  = false;

            size_t pos = 0;
            while (pos < insideBrackets.size())
            {
                const size_t comma     = insideBrackets.find(L',', pos);
                const size_t tokenEnd  = (comma == std::wstring_view::npos) ? insideBrackets.size() : comma;
                auto         tokenView = insideBrackets.substr(pos, tokenEnd - pos);

                while (!tokenView.empty() && tokenView.front() == L' ')
                    tokenView.remove_prefix(1);
                while (!tokenView.empty() && tokenView.back() == L' ')
                    tokenView.remove_suffix(1);

                std::wstring_view mappedToken{};
                for (const auto& entry : kMapNames)
                {
                    if (tokenView == entry.english)
                    {
                        mappedToken        = entry.chinese;
                        anyTokenTranslated = true;
                        break;
                    }
                    if (tokenView == entry.chinese)
                    {
                        mappedToken = entry.chinese;
                        break;
                    }
                }

                if (mappedToken.empty())
                {
                    allTokensRecognized = false;
                    break;
                }

                if (!translatedMaps.empty())
                    translatedMaps += L"，";
                translatedMaps += mappedToken;

                if (comma == std::wstring_view::npos)
                    break;
                pos = comma + 1;
            }

            if (allTokensRecognized && !translatedMaps.empty())
            {
                std::wstring_view suffix = text.substr(closeBracket + 1);
                std::wstring      translatedSuffix(suffix);
                if (suffix == L" Golden invasion!" || suffix == L" Golden Invasion!")
                    translatedSuffix = L" 黄金入侵！";
                else if (suffix == L" Golden invasion has ended." || suffix == L" Golden Invasion has ended.")
                    translatedSuffix = L" 黄金入侵已结束。";
                else if (suffix == L" Red Dragon invasion!" || suffix == L" Red Dragon Invasion!")
                    translatedSuffix = L" 红龙入侵！";
                else if (suffix == L" Red Dragon invasion has ended." || suffix == L" Red Dragon Invasion has ended.")
                    translatedSuffix = L" 红龙入侵已结束。";
                else if (suffix == L" White Wizard corps invasion!" || suffix == L" White Wizard Invasion!")
                    translatedSuffix = L" 白巫师军团入侵！";
                else if (suffix == L" White Wizard corps invasion has ended." ||
                         suffix == L" White Wizard Invasion has ended.")
                    translatedSuffix = L" 白巫师军团入侵已结束。";

                if (anyTokenTranslated || translatedSuffix != suffix)
                    return L"[" + translatedMaps + L"]" + translatedSuffix;
            }
        }
    }

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
