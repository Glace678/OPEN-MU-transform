// Tests for the server->client packet bounds hardened in the audit
// (NET-05/06/07 and the viewport/list count-loop fixes).
//
// Bound directly to the production guard functions in Network/Server/PacketBoundsGuards.h
// (whose capacities are static_assert-checked against CHARACTER_ATTRIBUTE::AbilityTime
// in WSclient.cpp and ND_QUEST_INDEX_MAX_COUNT in NewUINPCDialogue.cpp) and the real
// MAX_CHARACTERS_CLIENT / MAX_SKILLS headers.

#include "doctest.h"

#include "Network/Server/PacketBoundsGuards.h"

#include <cstdint>

TEST_SUITE("PacketBounds")
{
    TEST_CASE("magic list indices fit the skill table")
    {
        CHECK(MAX_SKILLS == 650);
        CHECK(MAX_SKILLS > 255);
    }

    TEST_CASE("character client roster guard rejects the -1 miss sentinel and >= capacity")
    {
        CHECK(MAX_CHARACTERS_CLIENT == 400);

        CHECK(MuPacketBounds::IsCharacterIndexValid(0));
        CHECK(MuPacketBounds::IsCharacterIndexValid(MAX_CHARACTERS_CLIENT - 1));

        CHECK_FALSE(MuPacketBounds::IsCharacterIndexValid(-1));
        CHECK_FALSE(MuPacketBounds::IsCharacterIndexValid(MAX_CHARACTERS_CLIENT));
        CHECK_FALSE(MuPacketBounds::IsCharacterIndexValid(99999));
    }

    TEST_CASE("ability helper guard rejects index beyond the 3-slot AbilityTime array")
    {
        CHECK(MuPacketBounds::AbilityTimeSlots == 3);

        for (unsigned char i = 0; i < MuPacketBounds::AbilityTimeSlots; ++i)
            CHECK(MuPacketBounds::IsAbilityIndexValid(i));

        for (int i = MuPacketBounds::AbilityTimeSlots; i <= 255; ++i)
            CHECK_FALSE(MuPacketBounds::IsAbilityIndexValid(static_cast<unsigned char>(i)));
    }

    TEST_CASE("quest index list clamps the server WORD count into the 20-DWORD buffer")
    {
        CHECK(MuPacketBounds::QuestIndexMaxCount == 20);

        uint32_t dummyBuffer[MuPacketBounds::QuestIndexMaxCount] = {};

        for (int c = 0; c <= MuPacketBounds::QuestIndexMaxCount; ++c)
            CHECK(MuPacketBounds::ClampQuestIndexCount(dummyBuffer, c) == c);

        CHECK(MuPacketBounds::ClampQuestIndexCount(nullptr, 5) == 0);
        CHECK(MuPacketBounds::ClampQuestIndexCount(dummyBuffer, -5) == 0);
        CHECK(MuPacketBounds::ClampQuestIndexCount(dummyBuffer, MuPacketBounds::QuestIndexMaxCount + 1) ==
              MuPacketBounds::QuestIndexMaxCount);
        CHECK(MuPacketBounds::ClampQuestIndexCount(dummyBuffer, 65535) == MuPacketBounds::QuestIndexMaxCount);
    }
}
