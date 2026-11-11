# tests/e2e/harness/systemic_engine.py
"""
OpenMU & MuMain Systemic Verification Engine.
Provides reference verifications for:
- Memory bounds & buffer protection (BuffLoader, DevilSquare, QuestReward, BannerList, LetterText)
- Use-after-free lifecycle elimination
- Server configuration bounds & Web UI type unboxing safety
- Packet layout collision detection
- Attribute multiplier duplication guards
- Exception resilience, RAII lifecycle, nullability, and concurrency safety.
"""

from dataclasses import dataclass
from typing import List, Dict, Optional, Tuple
import threading
import struct


class BuffScriptValidator:
    """Feature 02: w_BuffScriptLoader.cpp & BuffScriptValidation.h"""
    MAXIMUM_BUFF_RECORDS = 10000
    STRUCT_SIZE = 128  # sizeof(_BUFFINFO)

    @classmethod
    def is_valid_record_count(cls, listsize: int, structsize: int) -> bool:
        if listsize <= 0 or listsize > cls.MAXIMUM_BUFF_RECORDS:
            return False
        # 64-bit safe multiplication check
        total_size = int(listsize) * int(structsize)
        if total_size > 50 * 1024 * 1024:  # 50 MB safety cap
            return False
        return True


class DevilSquareRankValidator:
    """Feature 03: CSEventMatch.cpp rank bounds"""
    BUFFER_CAPACITY = 11  # MatchResult m_MatchResult[11]
    MAX_RANKS = 10        # CSEventMatch.cpp:215-217 MaxMatchResults = 10 (strictly < 11)

    @classmethod
    def clamp_ranks(cls, num_devil_rank: int) -> int:
        if num_devil_rank < 0:
            return 0
        return min(num_devil_rank, cls.MAX_RANKS)


class QuestRewardValidator:
    """Feature 05: QuestMng.cpp count bounds"""
    MAX_QUEST_REQUESTS = 5  # SQuestRequestReward::m_aRequest[5]
    MAX_QUEST_REWARDS = 5   # SQuestRequestReward::m_aReward[5]

    @classmethod
    def clamp_request_count(cls, count: int) -> int:
        return max(0, min(count, cls.MAX_QUEST_REQUESTS))

    @classmethod
    def clamp_reward_count(cls, count: int) -> int:
        return max(0, min(count, cls.MAX_QUEST_REWARDS))


class BannerBufferValidator:
    """Feature 06: BannerInfoList.cpp wchar_t buffer clamp"""
    BUFFER_ELEMENTS = 1024  # wchar_t buff[1024]
    WCHAR_SIZE = 2  # sizeof(wchar_t) on Windows

    @classmethod
    def safe_getline_capacity(cls) -> int:
        # std::size(buff) == 1024 wchar_t characters, NOT sizeof(buff) == 2048
        return cls.BUFFER_ELEMENTS


class LetterTextBufferValidator:
    """Feature 07: WSclient.h / WSclient.cpp FS_LETTER_TEXT bounds"""
    SIZEOF_FS_LETTER_TEXT_HEADER = 50   # sizeof(FS_LETTER_TEXT_HEADER)
    MAX_LETTERTEXT_LENGTH = 1000        # TextLimits.h MAX_LETTERTEXT_LENGTH
    SIZEOF_FS_LETTER_TEXT = 1050        # sizeof(FS_LETTER_TEXT) == 50 + 1000

    @classmethod
    def get_safe_copy_length(cls, receive_buffer_size: int) -> int:
        return max(0, min(receive_buffer_size, cls.SIZEOF_FS_LETTER_TEXT))

    @classmethod
    def get_safe_text_length(cls, receive_buffer_size: int) -> int:
        return max(0, min(receive_buffer_size - cls.SIZEOF_FS_LETTER_TEXT_HEADER, cls.MAX_LETTERTEXT_LENGTH))


class ServerConfigIdValidator:
    """Feature 19: CreateGameServerConfig.razor.cs nextServerId overflow guard"""
    MAX_SERVER_ID = 255  # byte max

    @classmethod
    def get_next_server_id(cls, current_server_count: int) -> Tuple[bool, Optional[int]]:
        if current_server_count >= cls.MAX_SERVER_ID:
            return False, None
        return True, current_server_count + 1


class WebNumberFieldConverter:
    """Feature 20: NumberField.razor safe numeric conversion avoiding InvalidCastException"""
    @staticmethod
    def parse_number(val: str, target_type: type):
        try:
            if target_type == int:
                return int(val)
            elif target_type == float:
                return float(val)
            return target_type(val)
        except (ValueError, TypeError):
            return None


class PacketLayoutValidator:
    """Feature 21: Packet definition collisions"""
    @staticmethod
    def parse_money_dropped(data: bytes) -> Optional[int]:
        # Bytes 10..13 represent 32-bit integer Amount
        if len(data) < 14:
            return None
        # Must read 4 full contiguous bytes: data[10..13]
        return struct.unpack_from(">I", data, 10)[0]

    @staticmethod
    def validate_illusion_temple_state_offsets(remaining_seconds_offset: int, player_index_offset: int) -> bool:
        # Must not overlap
        return remaining_seconds_offset != player_index_offset


class HappyHourAttributeManager:
    """Feature 22: HappyHourPlugIn.cs duplicate multiplier prevention"""
    def __init__(self):
        self._active_multipliers: Dict[str, float] = {}

    def apply_multiplier(self, source_key: str, multiplier: float) -> bool:
        if source_key in self._active_multipliers:
            # Already active, prevent duplicate accumulation
            return False
        self._active_multipliers[source_key] = multiplier
        return True

    def remove_multiplier(self, source_key: str):
        self._active_multipliers.pop(source_key, None)

    def get_total_multiplier(self) -> float:
        total = 1.0
        for m in self._active_multipliers.values():
            total *= m
        return total


class ExceptionHardeningContainer:
    """Feature 26: Exception hardening wrapper"""
    @staticmethod
    def execute_safe(action, default=None):
        try:
            return action(), None
        except Exception as ex:
            return default, ex


class RAIIResourceTracker:
    """Feature 27: RAII lifecycle tracker"""
    def __init__(self):
        self.is_acquired = True
        self.is_disposed = False

    def close(self):
        if not self.is_disposed:
            self.is_disposed = True
            self.is_acquired = False

    def __enter__(self):
        return self

    def __exit__(self, exc_type, exc_val, exc_tb):
        self.close()


class ConcurrencySafetyGuard:
    """Feature 29: Thread safety guard"""
    def __init__(self):
        self._lock = threading.Lock()
        self._counter = 0

    def increment(self):
        with self._lock:
            self._counter += 1
            return self._counter

    def get_value(self):
        with self._lock:
            return self._counter
