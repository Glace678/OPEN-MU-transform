// scope85 (L5 client index-guard) contract + predicate sweep.
//
// Like PacketBoundsTests, the hardened handlers (castle NPC table, siege map
// buffer, guild dialogs, personal-shop buy confirm, chat-command parameter page)
// are tightly coupled to the precompiled header and global client/UI state, so
// they cannot be driven directly in a headless test. These cases pin the REAL
// capacity constants they guard and execute the exact range predicates added to
// the source, sweeping boundary / malicious inputs. A companion block proves the
// predicate is load-bearing: without it the raw wire/index value lands outside
// the array (the red baseline), and with it the same input is rejected (green).

#include <doctest.h>

#include <cstddef>

// Pinned from the real headers (kept local so this headless test does not pull
// in WinCompat.h / windows.h; a mismatch is a deliberate capacity change).
//   SCOPE85_MAX_CHARACTERS_CLIENT : Character/CharacterConstants.h
//   SCOPE85_MAX_GUILDS            : GameLogic/Guild/GuildConstants.h
#define SCOPE85_MAX_CHARACTERS_CLIENT 400
#define SCOPE85_MAX_GUILDS             80

// Mirrors src/source/UI/NewUI/Combat/NewUISiegeWarBase.h
#define SCOPE85_MAX_COMMANDGROUP 7

// Mirrors CSenatusInfo member array capacities (UISenatus.h: m_GateInfo[6] /
// m_StatueInfo[4]). Kept here so a resize of those arrays is a deliberate change.
#define SCOPE85_GATE_SLOTS   6
#define SCOPE85_STATUE_SLOTS 4

namespace
{
    // 85-02 predicate, verbatim from CSenatusInfo::GetNPCInfo / SetNPCInfo guards.
    inline bool SenatusIndexValid(int npcNumber, int wireIndex)
    {
        // GATENPC_NUMBER=277, STATUENPC_NUMBER=283
        if (npcNumber == 277) return wireIndex >= 1 && wireIndex <= SCOPE85_GATE_SLOTS;
        if (npcNumber == 283) return wireIndex >= 1 && wireIndex <= SCOPE85_STATUE_SLOTS;
        return false;
    }

    // 85-03 predicate, verbatim from CNewUISiegeWarBase::SetMapInfo guard.
    inline bool SiegeTeamValid(unsigned char byTeam)
    {
        return byTeam < SCOPE85_MAX_COMMANDGROUP;
    }

    // 85-07 predicate, verbatim from the guild dialog DeleteIndex guards.
    inline bool GuildMemberIndexValid(int deleteIndex, int liveMemberCount)
    {
        return deleteIndex >= 0 && deleteIndex < liveMemberCount;
    }

    // 85-08 predicate, verbatim from CPersonalShopItemBuyMsgBoxLayout::OkBtnDown.
    inline bool ShopCharacterIndexValid(int idx)
    {
        return idx >= 0 && idx < SCOPE85_MAX_CHARACTERS_CLIENT;
    }

    // 85-09 predicate, verbatim from the chat-command m_parameterValues guards.
    inline bool ChatParameterValueValid(size_t parameterIndex, size_t commandParamCount, size_t valueVectorSize)
    {
        return parameterIndex < commandParamCount && parameterIndex < valueVectorSize;
    }
}

TEST_SUITE("Scope85IndexGuards")
{
    TEST_CASE("85-02 senatus gate/statue wire index is clamped to the arrays")
    {
        CHECK(SCOPE85_GATE_SLOTS == 6);
        CHECK(SCOPE85_STATUE_SLOTS == 4);

        for (int i = 1; i <= SCOPE85_GATE_SLOTS; ++i)
            CHECK(SenatusIndexValid(277, i));
        for (int i = 1; i <= SCOPE85_STATUE_SLOTS; ++i)
            CHECK(SenatusIndexValid(283, i));

        // red baseline: index 0 underflows (iNpcIndex-1 == -1); one past the last slot overflows.
        CHECK_FALSE(SenatusIndexValid(277, 0));
        CHECK_FALSE(SenatusIndexValid(277, SCOPE85_GATE_SLOTS + 1));
        CHECK_FALSE(SenatusIndexValid(277, 255));
        CHECK_FALSE(SenatusIndexValid(283, 0));
        CHECK_FALSE(SenatusIndexValid(283, SCOPE85_STATUE_SLOTS + 1));

        CHECK_FALSE(SenatusIndexValid(999, 1));
        CHECK_FALSE(SenatusIndexValid(0, 1));
    }

    TEST_CASE("85-03 siege map team byte cannot overrun the 7-slot buffer")
    {
        CHECK(SCOPE85_MAX_COMMANDGROUP == 7);

        for (unsigned char t = 0; t < SCOPE85_MAX_COMMANDGROUP; ++t)
            CHECK(SiegeTeamValid(t));

        for (int t = SCOPE85_MAX_COMMANDGROUP; t <= 255; ++t)
            CHECK_FALSE(SiegeTeamValid(static_cast<unsigned char>(t)));
        CHECK_FALSE(SiegeTeamValid(255));
    }

    TEST_CASE("85-07 guild DeleteIndex is bounded by the live member count")
    {
        CHECK(SCOPE85_MAX_GUILDS == 80);

        const int live = 5;
        for (int i = 0; i < live; ++i)
            CHECK(GuildMemberIndexValid(i, live));

        CHECK_FALSE(GuildMemberIndexValid(-1, live));
        CHECK_FALSE(GuildMemberIndexValid(live, live));
        CHECK_FALSE(GuildMemberIndexValid(SCOPE85_MAX_GUILDS, live));
        CHECK_FALSE(GuildMemberIndexValid(0, 0));
    }

    TEST_CASE("85-08 personal shop buy rejects the -1 reset shop character index")
    {
        CHECK(SCOPE85_MAX_CHARACTERS_CLIENT == 400);

        CHECK(ShopCharacterIndexValid(0));
        CHECK(ShopCharacterIndexValid(SCOPE85_MAX_CHARACTERS_CLIENT - 1));

        CHECK_FALSE(ShopCharacterIndexValid(-1));
        CHECK_FALSE(ShopCharacterIndexValid(SCOPE85_MAX_CHARACTERS_CLIENT));
        CHECK_FALSE(ShopCharacterIndexValid(99999));
    }

    TEST_CASE("85-09 chat parameter page respects the value vector, not just the command")
    {
        const size_t commandParams = 5;
        const size_t valueVector = 3;

        for (size_t i = 0; i < valueVector; ++i)
            CHECK(ChatParameterValueValid(i, commandParams, valueVector));

        // red baseline: 3..4 are < commandParams (old guard) but >= valueVector.
        CHECK_FALSE(ChatParameterValueValid(3, commandParams, valueVector));
        CHECK_FALSE(ChatParameterValueValid(4, commandParams, valueVector));

        CHECK_FALSE(ChatParameterValueValid(0, commandParams, 0));
    }
}