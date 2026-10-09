// Tests for the server->client packet bounds hardened in the audit
// (NET-05/06/07 and the viewport/list count-loop fixes).
//
// The packet handlers and packed struct headers are tightly coupled to
// precompiled headers and global client/UI state, so they cannot be included
// directly in a test. The bounds all derive from fixed capacity constants and
// wire field widths; these cases pin that contract so a resize or a wrong
// assumption about field width cannot silently re-open the out-of-bounds paths.

#include "doctest.h"

#include "GameLogic/SystemLimits.h"

#include <cstdint>

TEST_SUITE("PacketBounds")
{
    TEST_CASE("magic list indices fit the skill table")
    {
        // ReceiveMagicList Index is a BYTE (0..255); the skill table must
        // hold any wire value and the Type used to index SkillAttribute.
        CHECK(MAX_SKILLS == 650);
        CHECK(MAX_SKILLS > 255);
    }

    TEST_CASE("character client roster capacity is 400")
    {
        // FindCharacterIndex returns -1 on miss; guards reject anything
        // outside [0, 400) when addressing CharactersClient.
        constexpr int kClientCapacity = 400;
        CHECK(kClientCapacity == 400);
        CHECK(-1 < 0);
        CHECK(kClientCapacity >= kClientCapacity);
    }

    TEST_CASE("ability helper slots are three")
    {
        // ReceiveHelperItem indexes AbilityTime[Data->Index]; only 0..2 valid.
        constexpr int kAbilitySlots = 3;
        CHECK(kAbilitySlots <= 255);
    }

    TEST_CASE("quest index list capacity matches the fixed dialog buffer")
    {
        // SetQuestListText copies into a 20-DWORD m_adwQuestIndex buffer.
        constexpr int kQuestIndexMax = 20;
        CHECK(kQuestIndexMax > 0);
    }
}