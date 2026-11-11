#pragma once

#include "Character/CharacterConstants.h"
#include "GameLogic/SystemLimits.h"

namespace MuPacketBounds
{
constexpr int AbilityTimeSlots   = 3;
constexpr int QuestIndexMaxCount = 20;

constexpr bool IsCharacterIndexValid(int wireIndex) noexcept
{
    return wireIndex >= 0 && wireIndex < MAX_CHARACTERS_CLIENT;
}

constexpr bool IsAbilityIndexValid(unsigned char wireIndex) noexcept
{
    return wireIndex < AbilityTimeSlots;
}

constexpr int ClampQuestIndexCount(const void* srcBuffer, int wireCount) noexcept
{
    if (srcBuffer == nullptr || wireCount < 0)
        return 0;
    if (wireCount > QuestIndexMaxCount)
        return QuestIndexMaxCount;
    return wireCount;
}
}  // namespace MuPacketBounds
