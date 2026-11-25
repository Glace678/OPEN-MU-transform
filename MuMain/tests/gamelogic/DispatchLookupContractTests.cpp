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
    int  key;
    int  monsterIndex;
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

    TEST_CASE("dispatch gate: subcode expected min sizes protect against truncation")
    {
        // 0xF1 subcode 0x00 (JoinServer) requires 12 bytes for C1
        CHECK(PacketDispatchPolicy::ExpectedPacketMinSize(true, 0xF1, 0x00) == 12);
        CHECK_FALSE(PacketDispatchPolicy::PassesSubCodeDispatchGate(true, 0xF1, 0x00, 4));
        CHECK_FALSE(PacketDispatchPolicy::PassesSubCodeDispatchGate(true, 0xF1, 0x00, 11));
        CHECK(PacketDispatchPolicy::PassesSubCodeDispatchGate(true, 0xF1, 0x00, 12));

        // 0xF3 subcode 0x00 (CharacterList) requires 8 bytes for C1
        CHECK(PacketDispatchPolicy::ExpectedPacketMinSize(true, 0xF3, 0x00) == 8);
        CHECK_FALSE(PacketDispatchPolicy::PassesSubCodeDispatchGate(true, 0xF3, 0x00, 7));
        CHECK(PacketDispatchPolicy::PassesSubCodeDispatchGate(true, 0xF3, 0x00, 8));

        // 0xF3 subcode 0x03 (JoinMapServer) requires 13 bytes for C1
        CHECK(PacketDispatchPolicy::ExpectedPacketMinSize(true, 0xF3, 0x03) == 13);
        CHECK_FALSE(PacketDispatchPolicy::PassesSubCodeDispatchGate(true, 0xF3, 0x03, 12));
        CHECK(PacketDispatchPolicy::PassesSubCodeDispatchGate(true, 0xF3, 0x03, 13));

        // ObjectHitExtended (0x11) requires the full 16 byte extended layout for C1. The handler
        // casts LPPRECEIVE_ATTACK_EXTENDED unconditionally, so the older 7 byte minimum let a
        // truncated packet read HealthDamage/ShieldDamage beyond the received buffer.
        CHECK(PacketDispatchPolicy::ExpectedPacketMinSize(true, 0x11) == 16);
        CHECK_FALSE(PacketDispatchPolicy::PassesSubCodeDispatchGate(true, 0x11, 0x00, 15));
        CHECK(PacketDispatchPolicy::PassesSubCodeDispatchGate(true, 0x11, 0x00, 16));

        // BaseStatsExtended (0xF3/0x32) requires the full 24 byte layout for C1
        CHECK(PacketDispatchPolicy::ExpectedPacketMinSize(true, 0xF3, 0x32) == 24);
        CHECK_FALSE(PacketDispatchPolicy::PassesSubCodeDispatchGate(true, 0xF3, 0x32, 23));
        CHECK(PacketDispatchPolicy::PassesSubCodeDispatchGate(true, 0xF3, 0x32, 24));

        // TradeRequest (0x36) requires header + 10 byte name = 13 bytes
        CHECK(PacketDispatchPolicy::ExpectedPacketMinSize(true, 0x36) == 13);
        CHECK_FALSE(PacketDispatchPolicy::PassesSubCodeDispatchGate(true, 0x36, 0x00, 12));
        CHECK(PacketDispatchPolicy::PassesSubCodeDispatchGate(true, 0x36, 0x00, 13));
    }

    TEST_CASE("dispatch gate: fixed-layout by-server packets require their documented size")
    {
        // The receive buffer is allocated with the exact packet size, but the gate only required
        // the 3 byte header for these opcodes while the handlers read further fields. A short
        // packet therefore read past the buffer end. Every minimum below is the language
        // independent length from OpenMU/docs/Packets (the client contract), cross-checked
        // against the fields the handlers actually read.
        static const struct { std::uint8_t code; int min; } cases[] = {
        {0x07, 7},
        {0x0B, 5},
        {0x0F, 4},
        {0x1B, 7},
        {0x28, 5},
        {0x2A, 6},
        {0x2C, 7},
        {0x33, 8},
        {0x38, 4},
        {0x3B, 8},
        {0x3C, 4},
        {0x3D, 4},
        {0x40, 5},
        {0x50, 5},
        {0x51, 4},
        {0x53, 4},
        {0x56, 5},
        {0x5D, 5},
        {0x60, 4},
        {0x61, 12},
        {0x62, 13},
        {0x63, 12},
        {0x64, 6},
        {0x81, 12},
        {0x83, 4},
        {0x91, 7},
        {0x92, 4},
        {0x94, 11},
        {0x9A, 4},
        {0xA0, 54},
        {0xA1, 5},
        {0xA2, 6},
        {0xA3, 7},
        {0xA7, 7},
        {0xA8, 9},
        {0xA9, 13},
        {0xBA, 7},        };

        for (const auto& c : cases)
        {
            CHECK_EQ(PacketDispatchPolicy::ExpectedPacketMinSize(true, c.code), c.min);
            CHECK_FALSE(PacketDispatchPolicy::PassesSubCodeDispatchGate(true, c.code, 0, c.min - 1));
            CHECK(PacketDispatchPolicy::PassesSubCodeDispatchGate(true, c.code, 0, c.min));
            // Same packet with the C2/C4 framing needs exactly one byte more.
            CHECK_EQ(PacketDispatchPolicy::ExpectedPacketMinSize(false, c.code), c.min + 1);
        }
    }

    TEST_CASE("lookup contract: miss returns false and out=-1, never out of range")
    {
        // Roster of 8 slots; only slot 5 is live with key=100 / monster=216.
        FakeEntry roster[8]{};
        roster[5].live         = true;
        roster[5].key          = 100;
        roster[5].monsterIndex = 216;

        int out = -999;
        // Monster not in viewport / character not present -> miss.
        bool found = TryFindKey(roster, 8, 999, out);
        CHECK_FALSE(found);
        CHECK(out == -1);
        CHECK(out == -1);  // out on miss is never a valid index

        out   = -999;
        found = TryFindMonster(roster, 8, 9999, out);
        CHECK_FALSE(found);
        CHECK(out == -1);
    }

    TEST_CASE("lookup contract: hit returns true and the live slot")
    {
        FakeEntry roster[8]{};
        roster[5].live         = true;
        roster[5].key          = 100;
        roster[5].monsterIndex = 216;

        int out = -999;
        CHECK(TryFindKey(roster, 8, 100, out));
        CHECK(out == 5);
        bool inRange = (out >= 0 && out < 8);
        CHECK(inRange);

        out = -999;
        CHECK(TryFindMonster(roster, 8, 216, out));
        CHECK(out == 5);
    }
}