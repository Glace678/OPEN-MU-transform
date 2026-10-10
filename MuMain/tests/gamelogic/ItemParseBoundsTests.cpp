// 86-69 (P0 #17) / 86-72 regression tests.
//
// Exercises the REAL production functions ParseItemData() and
// CNewUIItemMng::CreateItemByParameters() compiled from
// src/source/UI/NewUI/Inventory/NewUIItemMng.cpp against a headless stub
// include tree (item_parse_stubs/). The item-type catalog (MAX_SOCKETS,
// MAX_ITEM, MAX_ITEM_INDEX) and the packet wire layout are the real ones.
//
// These cases pin the hardening:
//   * a packet shorter than the flags imply must not read past its span;
//   * SocketCount (a 0..15 server nibble) is clamped to the fixed
//     SocketOptions[MAX_SOCKETS] array capacity and excess sockets dropped;
//   * a socket payload the packet does not carry is truncated, not read OOB;
//   * an out-of-range built item Type is rejected at the creation entry.
#include "doctest.h"

#include "stdafx.h"
#include "UI/NewUI/Inventory/NewUIItemMng.h"

#include <vector>
#include <span>

namespace
{
    std::span<const BYTE> sp(const std::vector<BYTE>& v) { return std::span<const BYTE>(v); }
}

TEST_SUITE("ParseItemDataBounds")
{
    TEST_CASE("base 5-byte packet with no options parses and has no sockets")
    {
        // [0]=Group/NumberHi [1]=NumberLo [2]=Level [3]=Durability [4]=flags(0)
        std::vector<BYTE> pkt = { 0x10, 0x05, 0x01, 0xFF, 0x00 };
        auto p = ParseItemData(sp(pkt));
        CHECK(p.Group == 1);
        CHECK(p.Number == 5);
        CHECK(p.Level == 1);
        CHECK(p.Durability == 0xFF);
        CHECK(p.SocketCount == 0);
    }

    TEST_CASE("86-69: flags claim sockets on a 5-byte packet -> no OOB read, sockets dropped")
    {
        // Exactly 5 bytes, but flags[4]=0x80 (HasSockets). The socket header
        // byte would live at index 5, i.e. past the span. Pre-fix this read
        // itemData[5] out of bounds; post-fix the block is skipped entirely.
        std::vector<BYTE> pkt = { 0x10, 0x05, 0x01, 0xFF, 0x80 };
        auto p = ParseItemData(sp(pkt));
        CHECK(p.Group == 1);
        CHECK(p.SocketCount == 0);
        CHECK(p.SocketBonusOption == 0);
    }

    TEST_CASE("86-69: SocketCount nibble of 15 is clamped to MAX_SOCKETS, valid payload consumed")
    {
        // base(5) + socket header(1) + 5 payload bytes = 11 bytes.
        // header[5]=0xF5 -> bonus=0xF, SocketCount nibble=0x5... use 0xF5? we
        // want SocketCount nibble = 0xF (15): header = 0xF0 | 0xF = 0xFF.
        std::vector<BYTE> pkt = {
            0x10, 0x05, 0x01, 0xFF, 0x80,   // flags=HasSockets
            0xFF,                            // bonus=0xF, SocketCount=15
            10, 20, 30, 40, 50               // exactly 5 socket payload bytes
        };
        auto p = ParseItemData(sp(pkt));
        CHECK(p.SocketCount == MAX_SOCKETS);          // clamped from 15 -> 5
        CHECK(p.SocketBonusOption == 0xF);
        for (int i = 0; i < MAX_SOCKETS; ++i)
            CHECK(p.SocketOptions[i] == static_cast<BYTE>(10 + i * 10));
    }

    TEST_CASE("86-69: socket payload shorter than claimed count is truncated, not read OOB")
    {
        // base(5) + header(1) + only 1 payload byte = 7 bytes.
        // header low nibble claims SocketCount=5, but only one payload byte
        // ships. Pre-fix this read itemData[7..10] out of bounds.
        std::vector<BYTE> pkt = {
            0x10, 0x05, 0x01, 0xFF, 0x80,
            0x05,   // bonus=0, SocketCount=5
            77      // one and only payload byte
        };
        auto p = ParseItemData(sp(pkt));
        CHECK(p.SocketCount == MAX_SOCKETS);   // 5 (within cap)
        CHECK(p.SocketOptions[0] == 77);
        CHECK(p.SocketOptions[1] == 0);        // not read from OOB
        CHECK(p.SocketOptions[4] == 0);
    }
}

TEST_SUITE("CreateItemTypeBounds")
{
    TEST_CASE("86-72: out-of-range built Type is rejected, valid Type is accepted")
    {
        SEASON3B::CNewUIItemMng mgr;

        // Group=15 (max 4-bit), Number=4095 (max 12-bit) -> Type = 15*512+4095
        // = 11775 >= MAX_ITEM (8192). Pre-fix this created an item and then
        // indexed ItemAttribute[11775] out of bounds.
        ItemCreationParams bad = {};
        bad.Group = 15;
        bad.Number = 4095;
        ITEM* pBad = mgr.CreateItemByParameters(&bad);
        CHECK(pBad == nullptr);

        // Group=1, Number=5 -> Type = 517, within [0, MAX_ITEM). Must succeed.
        ItemCreationParams good = {};
        good.Group = 1;
        good.Number = 5;
        ITEM* pGood = mgr.CreateItemByParameters(&good);
        REQUIRE(pGood != nullptr);
        CHECK(pGood->Type == 1 * MAX_ITEM_INDEX + 5);
    }
}