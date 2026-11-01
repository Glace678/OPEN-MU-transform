# tests/e2e/harness/packet_framing.py
"""
OpenMU & MuMain Packet Framing and Validation Engine.
Implements reference protocol framing (C1, C2, C3, C4), size checks,
dispatch table verification, and packet truncation detection.
"""

from dataclasses import dataclass
from typing import Optional, Tuple, Dict

# Minimum protocol frame headers
MIN_C1_HEADER_SIZE = 2
MIN_C2_HEADER_SIZE = 3
MIN_C1_PACKET_SIZE = 4
MIN_C2_PACKET_SIZE = 4

# Reference table of expected minimum packet sizes (HeadCode, SubCode) -> min_size
# Modeled after WSclient.cpp and OpenMU Network Packet definitions
PACKET_SIZE_TABLE: Dict[Tuple[int, Optional[int]], int] = {
    (0xF1, 0x00): 12,   # JoinServerResponse
    (0xF1, 0x01): 8,    # LoginResponse
    (0xF1, None): 8,    # Any 0xF1 packet requires at least 8 bytes
    (0xF3, 0x00): 6,    # CharacterList
    (0xF3, 0x03): 32,   # CharacterCreateResponse
    (0x00, None): 5,    # ChatMessage (header + sender + text)
    (0x19, None): 7,    # SkillHit (C1, length, 0x19, animCount, targetH, targetL, skill)
    (0x1E, None): 6,    # SkillAnimation
    (0xD4, None): 7,    # InstantMoveRequest / Move
    (0x1D, None): 7,    # WalkRequest
    (0x22, None): 9,    # ItemDrop
    (0x24, None): 14,   # MoneyDropped (14 bytes with 4-byte amount)
    (0x26, None): 32,   # LetterHeader / Text
    (0x3F, 0x00): 16,   # QuestExpInfo (NPC_QUESTEXP_INFO)
    (0x40, None): 8,    # BuffEffect
    (0x86, None): 12,   # DevilSquareScore
    (0x9E, None): 10,   # IllusionTempleState (RemainingSeconds + PlayerIndex non-overlapping)
}


@dataclass
class PacketFrame:
    header: int
    size: int
    head_code: int
    sub_code: Optional[int]
    payload: bytes
    raw: bytes
    is_valid: bool
    error_message: Optional[str] = None


class PacketFramingEngine:
    @staticmethod
    def parse_header(raw_data: bytes) -> Tuple[int, int, int]:
        """
        Parses packet header.
        Returns: (header_byte, declared_size, header_len)
        """
        if not raw_data or len(raw_data) < 2:
            raise ValueError("Data buffer is too short to parse header")

        header = raw_data[0]
        if header in (0xC1, 0xC3):
            size = raw_data[1]
            return header, size, 2
        elif header in (0xC2, 0xC4):
            if len(raw_data) < 3:
                raise ValueError("C2/C4 packet requires at least 3 bytes for header")
            size = (raw_data[1] << 8) | raw_data[2]
            return header, size, 3
        else:
            raise ValueError(f"Invalid packet header: 0x{header:02X}")

    @classmethod
    def frame_packet(cls, raw_data: bytes) -> PacketFrame:
        """
        Validates raw packet bytes against C1-C4 rules and extracts head/sub codes.
        """
        if not raw_data:
            return PacketFrame(0, 0, 0, None, b"", b"", False, "Buffer is empty or null")

        header = raw_data[0]
        if header not in (0xC1, 0xC2, 0xC3, 0xC4):
            return PacketFrame(header, 0, 0, None, b"", raw_data, False, f"Invalid packet header byte: 0x{header:02X}")

        is_c1_c3 = (header in (0xC1, 0xC3))
        min_header_size = 2 if is_c1_c3 else 3

        if len(raw_data) < min_header_size:
            return PacketFrame(header, len(raw_data), 0, None, b"", raw_data, False, "Packet shorter than minimum header size")

        if is_c1_c3:
            declared_size = raw_data[1]
            head_code_offset = 2
        else:
            declared_size = (raw_data[1] << 8) | raw_data[2]
            head_code_offset = 3

        if declared_size < min_header_size:
            return PacketFrame(header, declared_size, 0, None, b"", raw_data, False, f"Declared size {declared_size} less than header size {min_header_size}")

        if len(raw_data) < declared_size:
            return PacketFrame(header, declared_size, 0, None, b"", raw_data, False, f"Truncated packet: declared {declared_size} bytes, received {len(raw_data)} bytes")

        if len(raw_data) < head_code_offset + 1:
            return PacketFrame(header, declared_size, 0, None, b"", raw_data, False, "Packet truncated before HeadCode")

        head_code = raw_data[head_code_offset]
        sub_code = None
        if len(raw_data) > head_code_offset + 1 and head_code in (0xF1, 0xF3, 0x3F):
            sub_code = raw_data[head_code_offset + 1]

        payload = raw_data[head_code_offset + 1:]
        return PacketFrame(header, declared_size, head_code, sub_code, payload, raw_data[:declared_size], True, None)

    @classmethod
    def validate_client_dispatch(cls, raw_data: bytes) -> Tuple[bool, Optional[str]]:
        """
        Reference implementation of WSclient.cpp ProcessPacket validation guard.
        Validates buffer size against PACKET_SIZE_TABLE to eliminate buffer overflow / OOB reads.
        """
        frame = cls.frame_packet(raw_data)
        if not frame.is_valid:
            return False, frame.error_message

        # Dispatch gate check: minimum header + subcode bytes (matching PacketDispatchPolicy.h)
        multiplexed_codes = {
            0xF1, 0xF3, 0xF4, 0xEB, 0xBC, 0x8E, 0xF5, 0xF6, 0xF8, 0xF9,
            0xAA, 0xF7, 0x3F, 0xAF, 0xB1, 0xB2, 0xB7, 0xB8, 0xB9, 0xBD,
            0xD1, 0xBF, 0xDE, 0xD2, 0xB3, 0xB4, 0xB5
        }
        is_c1_c3 = (frame.header in (0xC1, 0xC3))
        min_dispatch_bytes = 3 if is_c1_c3 else 4
        if frame.head_code in multiplexed_codes:
            min_dispatch_bytes += 1
        if frame.head_code == 0xF1:
            min_dispatch_bytes += 1

        if frame.size < min_dispatch_bytes:
            return False, f"Packet 0x{frame.head_code:02X} size {frame.size} is less than required {min_dispatch_bytes} dispatch bytes"

        # Check against size table
        expected_size = PACKET_SIZE_TABLE.get((frame.head_code, frame.sub_code))
        if expected_size is None:
            expected_size = PACKET_SIZE_TABLE.get((frame.head_code, None))

        if expected_size is not None and frame.size < expected_size:
            return False, f"Packet 0x{frame.head_code:02X} size {frame.size} is less than required {expected_size} bytes"

        return True, None

    @staticmethod
    def build_c1_packet(head_code: int, payload: bytes = b"", sub_code: Optional[int] = None) -> bytes:
        """Helper to construct a valid C1 packet."""
        data = bytearray([0xC1, 0, head_code])
        if sub_code is not None:
            data.append(sub_code)
        data.extend(payload)
        data[1] = len(data)
        return bytes(data)

    @staticmethod
    def build_c2_packet(head_code: int, payload: bytes = b"", sub_code: Optional[int] = None) -> bytes:
        """Helper to construct a valid C2 packet."""
        data = bytearray([0xC2, 0, 0, head_code])
        if sub_code is not None:
            data.append(sub_code)
        data.extend(payload)
        length = len(data)
        data[1] = (length >> 8) & 0xFF
        data[2] = length & 0xFF
        return bytes(data)

    @staticmethod
    def build_c3_packet(head_code: int, payload: bytes = b"", sub_code: Optional[int] = None) -> bytes:
        """Helper to construct a valid C3 packet."""
        data = bytearray([0xC3, 0, head_code])
        if sub_code is not None:
            data.append(sub_code)
        data.extend(payload)
        data[1] = len(data)
        return bytes(data)
