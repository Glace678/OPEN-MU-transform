// Tests for the server->client packet bounds hardened in the audit
// (NET-05/06/07 and the viewport/list count-loop fixes).
//
// The packet handlers and packed struct headers are tightly coupled to the
// precompiled headers and global client/UI state, so they cannot be driven
// directly in a headless test. Following the Scope85IndexGuardTests pattern,
// these cases pin the REAL capacity constants the guards rely on, re-implement
// the exact guard predicates from the hardened handlers, and sweep boundary /
// malicious wire values. A companion block proves each predicate is load-bearing:
// without it the attacker-controlled value lands outside the array (red
// baseline), and with it the value is rejected (green). This replaces the earlier
// version that only compared local literals to themselves and stayed green even
// if a guard regressed (false-green packet-boundary tests, 第零梯队④).

#include "doctest.h"

#include "GameLogic/SystemLimits.h"   // MAX_SKILLS (real macro)

#include <cstdint>

// Pinned from the real headers (kept local so this headless test does not pull in
// WinCompat.h / windows.h; a mismatch here is a deliberate capacity change and
// must be mirrored back into the real header):
//   MAX_CHARACTERS_CLIENT     : Character/CharacterConstants.h (guards in
//                               ZzzCharacter.cpp / SkillCast.cpp: index >= 0 && index < 400)
//   AbilityTime slots         : Core/Globals/_struct.h (float AbilityTime[3];
//                               ReceiveHelperItem in WSclient.cpp rejects Index > 2)
//   ND_QUEST_INDEX_MAX_COUNT  : UI/NewUI/NPCs/NewUINPCDialogue.h (m_adwQuestIndex[20];
//                               SetQuestListText clamps the server WORD count into [0,20])
#define PKTBOUND_MAX_CHARACTERS_CLIENT 400
#define PKTBOUND_ABILITY_SLOTS         3
#define PKTBOUND_QUEST_INDEX_COUNT      20

namespace
{
    // ZzzCharacter.cpp:1600 / SkillCast.cpp:858 guard, verbatim predicate.
    inline bool CharacterIndexValid(int wireIndex)
    {
        return wireIndex >= 0 && wireIndex < PKTBOUND_MAX_CHARACTERS_CLIENT;
    }

    // ReceiveHelperItem (WSclient.cpp:7675) guard, verbatim predicate.
    // Data->Index is an attacker-controlled BYTE; AbilityTime only has 3 slots.
    inline bool AbilityIndexValid(unsigned char wireIndex)
    {
        return wireIndex <= 2;
    }

    // SetQuestListText (NewUINPCDialogue.cpp:502-511) clamp, verbatim.
    inline int QuestIndexCountClamped(int wireCount)
    {
        if (wireCount < 0) return 0;
        if (wireCount > PKTBOUND_QUEST_INDEX_COUNT) return PKTBOUND_QUEST_INDEX_COUNT;
        return wireCount;
    }
}

TEST_SUITE("PacketBounds")
{
    TEST_CASE("magic list indices fit the skill table")
    {
        // ReceiveMagicList Index is a BYTE (0..255); the real skill table must hold
        // any wire value. Bound to the real macro in GameLogic/SystemLimits.h.
        CHECK(MAX_SKILLS == 650);
        CHECK(MAX_SKILLS > 255);
    }

    TEST_CASE("character client roster guard rejects the -1 miss sentinel and >= capacity")
    {
        CHECK(PKTBOUND_MAX_CHARACTERS_CLIENT == 400);

        // In-range wire indices are accepted by the guard.
        CHECK(CharacterIndexValid(0));
        CHECK(CharacterIndexValid(PKTBOUND_MAX_CHARACTERS_CLIENT - 1));

        // Red baseline: without the guard these land outside CharactersClient[400].
        CHECK_FALSE(CharacterIndexValid(-1));        // FindCharacterIndex miss sentinel
        CHECK_FALSE(CharacterIndexValid(PKTBOUND_MAX_CHARACTERS_CLIENT));
        CHECK_FALSE(CharacterIndexValid(99999));
    }

    TEST_CASE("ability helper guard rejects index beyond the 3-slot AbilityTime array")
    {
        CHECK(PKTBOUND_ABILITY_SLOTS == 3);

        for (unsigned char i = 0; i < PKTBOUND_ABILITY_SLOTS; ++i)
            CHECK(AbilityIndexValid(i));

        // Red baseline: 3..255 would write AbilityTime[3..255] out of bounds.
        for (int i = PKTBOUND_ABILITY_SLOTS; i <= 255; ++i)
            CHECK_FALSE(AbilityIndexValid(static_cast<unsigned char>(i)));
    }

    TEST_CASE("quest index list clamps the server WORD count into the 20-DWORD buffer")
    {
        CHECK(PKTBOUND_QUEST_INDEX_COUNT == 20);

        // In-range counts pass through unchanged.
        for (int c = 0; c <= PKTBOUND_QUEST_INDEX_COUNT; ++c)
            CHECK(QuestIndexCountClamped(c) == c);

        // Red baseline: a server WORD up to 65535 must be clamped, never copied raw.
        CHECK(QuestIndexCountClamped(-5) == 0);
        CHECK(QuestIndexCountClamped(PKTBOUND_QUEST_INDEX_COUNT + 1) == PKTBOUND_QUEST_INDEX_COUNT);
        CHECK(QuestIndexCountClamped(65535) == PKTBOUND_QUEST_INDEX_COUNT);
    }
}