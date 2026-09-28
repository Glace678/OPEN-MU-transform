#pragma once

#include "Core/Haptics/Haptics.h"

#include <cstdint>

namespace Network::Server
{
    enum class ResultOperation
    {
        JoinServer,
        CreateCharacter,
        DeleteCharacter,
        Party,
        GuildJoin,
        GuildLeave,
        GuildCreate,
        LegacyQuest,
        QuestComplete,
    };

    constexpr bool IsSuccessfulResult(ResultOperation operation, std::uint8_t result)
    {
        switch (operation)
        {
        case ResultOperation::Party:
            // This response only reports errors plus 5 = successfully left party.
            return result == 5;
        case ResultOperation::GuildLeave:
            // Left, dissolved, or removed a member respectively.
            return result == 1 || result == 4 || result == 5;
        case ResultOperation::LegacyQuest:
            return result == 0;
        case ResultOperation::JoinServer:
        case ResultOperation::CreateCharacter:
        case ResultOperation::DeleteCharacter:
        case ResultOperation::GuildJoin:
        case ResultOperation::GuildCreate:
        case ResultOperation::QuestComplete:
            return result == 1;
        }

        return false;
    }

    constexpr Core::Haptics::HapticEvent HapticForResult(
        ResultOperation operation,
        std::uint8_t result)
    {
        return IsSuccessfulResult(operation, result)
            ? Core::Haptics::HapticEvent::Confirmed
            : Core::Haptics::HapticEvent::OperationFailed;
    }
}
