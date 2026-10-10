///////////////////////////////////////////////////////////////////////////////
// PacketDispatchPolicy.h
//
// Centralised dispatch-layer minimum-length contract for the client packet
// receiver (NET-01 / P0 #10 / 89-01).
//
// The receiver's ProcessPacket used to only reject packets shorter than 4
// bytes and then hand the buffer straight to per-opcode handlers, most of which
// cast it to a fixed struct and read fields without checking the length again.
// A 4-byte packet from a hostile/misbehaving server could therefore read past
// the receive buffer.
//
// This header owns the *pure* decision: given a HeadCode and the framing style,
// how many bytes must the dispatcher have validated before it may safely read
// SubCode/Value and enter the opcode switch? It deliberately has no dependency
// on client globals so the exact same function the dispatcher calls can be
// exercised by the unit tests.
//
// Wire layout (see WSclient.h):
//   C1/C3 packet: [ Code(1)  Size(1)  HeadCode(1) ]                 = 3 bytes header
//   C2/C4 packet: [ Code(1)  SizeH(1) SizeL(1) HeadCode(1) ]        = 4 bytes header
//   SubCode byte follows the fixed header; a trailing Value byte may follow.
///////////////////////////////////////////////////////////////////////////////
#pragma once

#include <cstdint>

namespace PacketDispatchPolicy
{
    // Fixed header sizes by framing.
    inline constexpr int C1_HEADER_BYTES = 3;
    inline constexpr int C2_HEADER_BYTES = 4;

    // HeadCodes whose dispatcher path immediately casts the buffer to read a
    // SubCode byte right after the fixed header. For these, a packet that does
    // not even contain the SubCode byte must be dropped before the switch.
    inline constexpr bool IsMultiplexedHeadCode(std::uint8_t headCode)
    {
        switch (headCode)
        {
        case 0xF1: // login / account, subcode + value
        case 0xF3: // game server opcode family
        case 0xF4: // server list / connect
        case 0xEB: // union guild result
        case 0xBC: // gem mix result
        case 0x8E: // move map checksum
        case 0xF5: // chat command catalog (reads buf[3]/buf[4] inline)
        case 0xF6: // quest subcodes
        case 0xF8: // gens system (config-gated)
        case 0xF9: // NPC dialog
        case 0xAA: // duel
        case 0xF7: // empire guardian event
        case 0x3F: // personal shop
        case 0xAF: // event match result 2
        case 0xB1: // change map server
        case 0xB2: // blood castle / gate / crown
        case 0xB7: // catapult
        case 0xB8: // kill count
        case 0xB9: // castle siege info
        case 0xBD: // crywolf
        case 0xD1: // kanturu / raklion
        case 0xBF: // cursed temple / doppelganger
        case 0xDE: // character card
        case 0xD2: // in-game shop (config-gated)
            return true;
        default:
            return false;
        }
    }

    // HeadCodes for which the dispatcher itself reads the trailing Value byte
    // (switch(Data->Value)) before delegating to a handler. Packets of these
    // opcodes need one more byte even beyond SubCode.
    inline constexpr bool DispatcherReadsValue(std::uint8_t headCode)
    {
        switch (headCode)
        {
        case 0xF1: // case 0xF1/0x01 reads Data->Value to branch the login result
            return true;
        default:
            return false;
        }
    }

    // Minimum number of bytes the dispatcher must have validated for a packet
    // of the given framing + HeadCode before it may safely read SubCode/Value.
    //   isC1C3            : framing, ReceiveBuffer[0] % 2 == 1
    inline constexpr int MinimumDispatchBytes(bool isC1C3, std::uint8_t headCode)
    {
        int n = isC1C3 ? C1_HEADER_BYTES : C2_HEADER_BYTES;
        if (IsMultiplexedHeadCode(headCode))
            n += 1; // SubCode
        if (DispatcherReadsValue(headCode))
            n += 1; // Value
        return n;
    }

    // True when a packet of `size` bytes for the given framing + HeadCode passes
    // the dispatch-layer gate and may enter the opcode switch. False means the
    // whole packet must be dropped (and counted) before any handler runs.
    // This is a runtime branch, never an assert.
    inline constexpr bool PassesDispatchGate(bool isC1C3, std::uint8_t headCode, int size)
    {
        return size >= MinimumDispatchBytes(isC1C3, headCode);
    }
}