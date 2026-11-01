///////////////////////////////////////////////////////////////////////////////
// PacketDispatchPolicy.h
//
// [EN] Centralised dispatch-layer minimum-length contract for the client packet
// receiver (NET-01 / P0 #10 / 89-01).
// [ZH] 客户端数据包接收器集中式分发层最小长度契约模块 (NET-01 / P0 #10 / 89-01)。
//
// The receiver's ProcessPacket used to only reject packets shorter than 4
// bytes and then hand the buffer straight to per-opcode handlers, most of which
// cast it to a fixed struct and read fields without checking the length again.
// A 4-byte packet from a hostile/misbehaving server could therefore read past
// the receive buffer.
//
// 契约说明 / Contract Specification:
// 本模块拥有纯函数的安全裁定：给定 HeadCode 与帧格式类型，判断分发器在进入 opcode switch
// 与读取 SubCode/Value 之前必须验证的最小有效字节数。
// 彻底杜绝无校验强转结构体引发的堆栈越界读取 (Heap/Stack Out-Of-Bounds Read)。
//
// Wire layout / 网络报文物理结构 (see WSclient.h):
//   C1/C3 packet: [ Code(1)  Size(1)   HeadCode(1) ]                 = 3 bytes header (定长短报文)
//   C2/C4 packet: [ Code(1)  SizeH(1)  SizeL(1)    HeadCode(1) ]     = 4 bytes header (大容量长报文)
//   SubCode byte follows the fixed header; a trailing Value byte may follow.
//   子操作码 (SubCode) 紧随固定包头之后；特定协议后继 Value 状态值。
///////////////////////////////////////////////////////////////////////////////
#pragma once

#include <cstdint>

namespace PacketDispatchPolicy
{
// Fixed header sizes by framing.
// [EN] Fixed header sizes: C1/C3 is 3 bytes, C2/C4 is 4 bytes.
// [ZH] 协议帧固定头部大小：C1/C3 为 3 字节，C2/C4 为 4 字节。
inline constexpr int C1_HEADER_BYTES = 3;
inline constexpr int C2_HEADER_BYTES = 4;

// [EN] HeadCodes whose dispatcher path immediately casts the buffer to read a
// SubCode byte right after the fixed header. For these, a packet that does
// not even contain the SubCode byte must be dropped before the switch.
// [ZH] 复用型主操作码：调度分发器在固定包头后紧接着读取 SubCode 字段。
// 若报文长度连 SubCode 字节都不足，必须在进入 switch 前直接丢弃，防止越界访问。
inline constexpr bool IsMultiplexedHeadCode(std::uint8_t headCode)
{
    switch (headCode)
    {
        case 0xF1:  // login / account, subcode + value
        case 0xF3:  // game server opcode family
        case 0xF4:  // server list / connect
        case 0xEB:  // union guild result
        case 0xBC:  // gem mix result
        case 0x8E:  // move map checksum
        case 0xF5:  // chat command catalog (reads buf[3]/buf[4] inline)
        case 0xF6:  // quest subcodes
        case 0xF8:  // gens system (config-gated)
        case 0xF9:  // NPC dialog
        case 0xAA:  // duel
        case 0xF7:  // empire guardian event
        case 0x3F:  // personal shop
        case 0xAF:  // event match result 2
        case 0xB1:  // change map server
        case 0xB2:  // blood castle / gate / crown
        case 0xB7:  // catapult
        case 0xB8:  // kill count
        case 0xB9:  // castle siege info
        case 0xBD:  // crywolf
        case 0xD1:  // kanturu / raklion
        case 0xBF:  // cursed temple / doppelganger
        case 0xDE:  // character card
        case 0xD2:  // in-game shop (config-gated)
        case 0xB3:  // castle siege NPC DB list (fixed header + iCount; handler bounds records by size)
        case 0xB4:  // castle siege registered guild list
        case 0xB5:  // castle siege attacking guild list
            return true;
        default:
            return false;
    }
}

// [EN] HeadCodes for which the dispatcher itself reads the trailing Value byte
// (switch(Data->Value)) before delegating to a handler. Packets of these
// opcodes need one more byte even beyond SubCode.
// [ZH] 分发器在派发给具体处理器前需要读取尾部 Value 字段的主操作码（如 0xF1 登录结果）。
// 该类报文需要的最小长度必须覆盖 SubCode 之后的 Value 字节。
inline constexpr bool DispatcherReadsValue(std::uint8_t headCode)
{
    switch (headCode)
    {
        case 0xF1:  // case 0xF1/0x01 reads Data->Value to branch the login result
            return true;
        default:
            return false;
    }
}

// [EN] Minimum number of bytes the dispatcher must have validated for a packet
// of the given framing + HeadCode before it may safely read SubCode/Value.
//   isC1C3            : framing, ReceiveBuffer[0] % 2 == 1
// [ZH] 计算给定帧类型与主操作码的分发层最小字节阈值：
//   HeaderBytes (C1:3 / C2:4) + (IsMultiplexed ? 1 : 0) + (ReadsValue ? 1 : 0)
inline constexpr int MinimumDispatchBytes(bool isC1C3, std::uint8_t headCode)
{
    int n = isC1C3 ? C1_HEADER_BYTES : C2_HEADER_BYTES;
    if (IsMultiplexedHeadCode(headCode))
        n += 1;  // SubCode
    if (DispatcherReadsValue(headCode))
        n += 1;  // Value
    return n;
}

// [EN] True when a packet of `size` bytes for the given framing + HeadCode passes
// the dispatch-layer gate and may enter the opcode switch. False means the
// whole packet must be dropped (and counted) before any handler runs.
// This is a runtime branch, never an assert.
// [ZH] 分发层安全门禁判断：校验实际接收字节是否满足最小分发长度，不满足则安全丢弃。
inline constexpr bool PassesDispatchGate(bool isC1C3, std::uint8_t headCode, int size)
{
    return size >= MinimumDispatchBytes(isC1C3, headCode);
}

// [EN] Expected minimum structure sizes for specific (HeadCode, SubCode) pairs.
// Packets shorter than this minimum are truncated and must be rejected
// before handler raw structure casting to prevent heap/stack out-of-bounds reads.
// [ZH] 特定 (HeadCode, SubCode) 协议对应的精确最小结构体字节长度。
// 小于该长度的报文属于截断畸形包，在强转原生 C++ 结构体前予以拦截，彻底阻断堆栈越界。
inline constexpr int ExpectedPacketMinSize(bool isC1C3, std::uint8_t headCode, std::uint8_t subCode = 0)
{
    const int baseHeader  = isC1C3 ? C1_HEADER_BYTES : C2_HEADER_BYTES;
    const int headerDelta = isC1C3 ? 0 : 1;

    const int minBytes = MinimumDispatchBytes(isC1C3, headCode);

    switch (headCode)
    {
        case 0xF1:
            switch (subCode)
            {
                case 0x00:  // PRECEIVE_JOIN_SERVER (12 bytes C1)
                    return 12 + headerDelta;
                case 0x01:  // PRECEIVE_LOG_IN / PHEADER_DEFAULT_SUBCODE (5 bytes C1)
                    return 5 + headerDelta;
                case 0x02:  // PRECEIVE_LOG_OUT (5 bytes C1)
                    return 5 + headerDelta;
                case 0x03:  // PRECEIVE_CONFIRM_PASSWORD (35 bytes C1)
                    return 35 + headerDelta;
                case 0x04:  // PRECEIVE_CONFIRM_PASSWORD2 (5 bytes C1)
                    return 5 + headerDelta;
                case 0x05:  // PRECEIVE_CHANGE_PASSWORD (5 bytes C1)
                    return 5 + headerDelta;
                case 0x12:  // PRECEIVE_CREATE_ACCOUNT (5 bytes C1)
                    return 5 + headerDelta;
                default:
                    return minBytes;
            }

        case 0xF3:
            switch (subCode)
            {
                case 0x00:  // PHEADER_DEFAULT_CHARACTER_LIST (8 bytes C1)
                    return 8 + headerDelta;
                case 0x01:  // PRECEIVE_CREATE_CHARACTER (5 bytes C1)
                    return 5 + headerDelta;
                case 0x02:  // PRECEIVE_DELETE_CHARACTER (5 bytes C1)
                    return 5 + headerDelta;
                case 0x03:  // PRECEIVE_JOIN_MAP_SERVER (13 bytes C1)
                    return 13 + headerDelta;
                case 0x04:  // PRECEIVE_REVIVAL (10 bytes C1)
                    return 10 + headerDelta;
                case 0x05:  // PRECEIVE_LEVEL_UP (17 bytes C1)
                    return 17 + headerDelta;
                case 0x06:  // PRECEIVE_ADD_POINT (11 bytes C1, 24 bytes Extended)
                    return 11 + headerDelta;
                case 0x07:  // PRECEIVE_DAMAGE / PoisonDamage (8 bytes C1)
                    return 8 + headerDelta;
                case 0x08:  // PRECEIVE_PK / HeroStateChanged (7 bytes C1)
                    return 6 + headerDelta;
                case 0x10:  // PRECEIVE_INVENTORY (5 bytes C1 / 6 bytes C2)
                    return 5 + headerDelta;
                case 0x30:  // PRECEIVE_OPTION (19+ bytes C1)
                    return 19 + headerDelta;
                default:
                    return minBytes;
            }

        case 0xF4:
            switch (subCode)
            {
                case 0x03:  // PRECEIVE_SERVER_ADDRESS / ConnectionInfo (22 bytes C1)
                    return 13 + headerDelta;
                case 0x06:  // PRECEIVE_SERVER_LIST / ServerListResponse (6 bytes C1 / 7 bytes C2)
                    return 6 + headerDelta;
                default:
                    return minBytes;
            }

        case 0x00:  // Chat
        case 0x01:  // Chat
        case 0x02:  // Whisper
        case 0x0D:  // Notice
            return baseHeader + 1;

        case 0x11:  // Attack (7 bytes C1)
            return 7 + headerDelta;
        case 0x15:  // Position (5 bytes C1)
            return 5 + headerDelta;
        case 0x16:  // Die character (7 bytes C1)
            return 7 + headerDelta;
        case 0x17:  // Die character (6 bytes C1)
            return 6 + headerDelta;
        case 0x18:  // Action character (7 bytes C1)
            return 7 + headerDelta;
        case 0x19:  // Magic (7 bytes C1)
            return 7 + headerDelta;
        case 0x20:  // Create item (10 bytes C1)
            return 10 + headerDelta;
        case 0x21:  // Delete item (4 bytes C1)
            return 4 + headerDelta;
        case 0x22:  // Get item / ItemPickUpRequestFailed (4 bytes C1)
            return 4 + headerDelta;
        case 0x23:  // Drop item (5 bytes C1)
            return 5 + headerDelta;
        case 0x24:  // Equipment item (7 bytes C1)
            return 7 + headerDelta;
        case 0x26:  // Inventory item use (6 bytes C1)
            return 6 + headerDelta;
        case 0x71:  // Ping
            return baseHeader;
        case 0x86:  // Devil square rank (6 bytes C1)
            return 6 + headerDelta;
        case 0xD4:  // Move (7 bytes C1)
            return 7 + headerDelta;
        case 0xDB:  // Magic attack (8 bytes C1)
            return 8 + headerDelta;

        default:
            return minBytes;
    }
}

// [EN] Evaluates whether packet meets the expected minimum size for its specific sub-command.
// [ZH] 校验报文是否满足其具体子指令 (SubCode) 所需的最小有效负载长度。
inline constexpr bool PassesSubCodeDispatchGate(bool isC1C3, std::uint8_t headCode, std::uint8_t subCode, int size)
{
    return size >= ExpectedPacketMinSize(isC1C3, headCode, subCode);
}
}