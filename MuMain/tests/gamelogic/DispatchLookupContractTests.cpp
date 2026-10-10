// Runtime branch tests for P0#10 (dispatch-layer minimum-length gate) and
// P0#11 (character lookup safe contract).
//
// The dispatch gate decision lives in Network/Server/PacketDispatchPolicy.h and
// is a pure, header-only function -- the exact function ProcessPacket calls --
// so these cases exercise the real gate (not a re-implemented copy).
//
// The lookup contract mirrors the loop inside TryFindCharacterIndex /
// TryFindCharacterIndexByMonsterIndex (ZzzCharacter.cpp) over a small roster:
// the production function loops CharactersClient[0, MAX_CHARACTERS_CLIENT) with
// the same (Live && Key==) / (Live && MonsterIndex==) predicate, returning the
// slot on hit and false + out=-1 on miss. These cases pin that contract: a miss
// must report not-found and the out value must never index out of range.

#include "doctest.h"

#include "Network/Server/PacketDispatchPolicy.h"
#include "GameLogic/SystemLimits.h"

#include <cstdint>

namespace
{
    struct FakeEntry
    {
        bool live;
        int key;
        int monsterIndex;
    };

    // Exact copy of the production loop (ZzzCharacter.cpp TryFindCharacterIndex).
    bool TryFindKey(const FakeEntry* roster, int capacity, int key, int& outIndex)
    {
        for (int i = 0; i < capacity; ++i)
        {
            const FakeEntry& e = roster[i];
            if (e.live && e.key == key)
            {
                outIndex = i;
                return true;
            }
        }
        outIndex = -1;
        return false;
    }

    bool TryFindMonster(const FakeEntry* roster, int capacity, int monsterIndex, int& outIndex)
    {
        for (int i = 0; i < capacity; ++i)
        {
            const FakeEntry& e = roster[i];
            if (e.live && e.monsterIndex == monsterIndex)
            {
                outIndex = i;
                return true;
            }
        }
        outIndex = -1;
        return false;
    }
}

TEST_SUITE("DispatchLookupContract")
{
    TEST_CASE("dispatch gate: C2 multiplexed packet of only 4 bytes is dropped")
    {
        // C2/C4 framing, HeadCode 0xF3 (game opcode family, reads SubCode).
        // C2 header = 4 bytes; SubCode lives at byte[4] -> need at least 5.
        CHECK(PacketDispatchPolicy::MinimumDispatchBytes(false, 0xF3) == 5);
        CHECK_FALSE(PacketDispatchPolicy::PassesDispatchGate(false, 0xF3, 4));
        CHECK(PacketDispatchPolicy::PassesDispatchGate(false, 0xF3, 5));
    }

    TEST_CASE("dispatch gate: C1 login packet that reads Value needs 5 bytes")
    {
        // 0xF1 dispatcher itself does switch(Data->Value) after SubCode, so a C1
        // packet needs header(3) + SubCode(1) + Value(1) = 5 before the switch.
        CHECK(PacketDispatchPolicy::MinimumDispatchBytes(true, 0xF1) == 5);
        CHECK_FALSE(PacketDispatchPolicy::PassesDispatchGate(true, 0xF1, 4));
        CHECK(PacketDispatchPolicy::PassesDispatchGate(true, 0xF1, 5));
    }

    TEST_CASE("dispatch gate: simple opcodes are not over-constrained")
    {
        // Ping (0x71) and chat (0x00) carry no subcode at the dispatcher.
        CHECK(PacketDispatchPolicy::MinimumDispatchBytes(true, 0x71) == 3);
        CHECK(PacketDispatchPolicy::MinimumDispatchBytes(false, 0x00) == 4);
        CHECK(PacketDispatchPolicy::PassesDispatchGate(true, 0x71, 4));
        CHECK(PacketDispatchPolicy::PassesDispatchGate(false, 0x00, 4));
    }

    TEST_CASE("dispatch gate: C1 multiplexed packet passes at header+subcode")
    {
        // C1 0xF3: header(3)+SubCode(1) = 4; SubCode at byte[3] is readable.
        CHECK(PacketDispatchPolicy::MinimumDispatchBytes(true, 0xF3) == 4);
        CHECK(PacketDispatchPolicy::PassesDispatchGate(true, 0xF3, 4));
    }

    TEST_CASE("lookup contract: miss returns false and out=-1, never out of range")
    {
        // Roster of 8 slots; only slot 5 is live with key=100 / monster=216.
        FakeEntry roster[8]{};
        roster[5].live = true;
        roster[5].key = 100;
        roster[5].monsterIndex = 216;

        int out = -999;
        // Monster not in viewport / character not present -> miss.
        bool found = TryFindKey(roster, 8, 999, out);
        CHECK_FALSE(found);
        CHECK(out == -1);
        CHECK(out == -1); // out on miss is never a valid index

        out = -999;
        found = TryFindMonster(roster, 8, 9999, out);
        CHECK_FALSE(found);
        CHECK(out == -1);
    }

    TEST_CASE("lookup contract: hit returns true and the live slot")
    {
        FakeEntry roster[8]{};
        roster[5].live = true;
        roster[5].key = 100;
        roster[5].monsterIndex = 216;

        int out = -999;
        CHECK(TryFindKey(roster, 8, 100, out));
        CHECK(out == 5);
        bool inRange = (out >= 0 && out < 8); CHECK(inRange);

        out = -999;
        CHECK(TryFindMonster(roster, 8, 216, out));
        CHECK(out == 5);
    }
}