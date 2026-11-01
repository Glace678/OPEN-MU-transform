# tests/e2e/harness/movement_engine.py
"""
OpenMU Movement Anti-Cheat and Path Validation Engine.
Implements reference movement arbitration, step distance calculation,
anti-teleport checks, speedhack detection, and terrain boundary validation.
"""

import math
from dataclasses import dataclass
from typing import Tuple, Optional, Set


@dataclass
class Point:
    x: int
    y: int

    def distance_to(self, other: 'Point') -> float:
        return math.sqrt((self.x - other.x) ** 2 + (self.y - other.y) ** 2)

    def chebyshev_distance(self, other: 'Point') -> int:
        return max(abs(self.x - other.x), abs(self.y - other.y))

    def manhattan_distance(self, other: 'Point') -> int:
        return abs(self.x - other.x) + abs(self.y - other.y)


class MovementEngine:
    MAX_STEP_DISTANCE = 2  # Standard MU walking/running step limit per tick
    MIN_TICK_INTERVAL_MS = 250  # Minimum millisecond interval between valid move packets
    MAP_SIZE = 256

    def __init__(self, start_x: int = 125, start_y: int = 125, map_id: int = 0):
        self.current_position = Point(start_x, start_y)
        self.last_valid_position = Point(start_x, start_y)
        self.map_id = map_id
        self.last_move_timestamp_ms = 0
        self.blocked_tiles: Set[Tuple[int, int]] = set()

    def set_blocked_tile(self, x: int, y: int):
        self.blocked_tiles.add((x, y))

    def is_tile_walkable(self, x: int, y: int) -> bool:
        if x < 0 or x >= self.MAP_SIZE or y < 0 or y >= self.MAP_SIZE:
            return False
        return (x, y) not in self.blocked_tiles

    def validate_and_move_step(self, target_x: int, target_y: int, timestamp_ms: int) -> Tuple[bool, str, Point]:
        """
        Validates standard player walk/run packet.
        Returns: (is_valid, reason, resulting_position)
        """
        target = Point(target_x, target_y)

        # 1. Bounds check
        if not (0 <= target.x < self.MAP_SIZE and 0 <= target.y < self.MAP_SIZE):
            return False, "Target coordinates out of map bounds", self.last_valid_position

        # 2. Walkability check
        if not self.is_tile_walkable(target.x, target.y):
            return False, "Target coordinate is non-walkable/blocked terrain", self.last_valid_position

        # 3. Teleport anti-cheat check (Chebyshev step distance must be <= 2)
        step_dist = self.current_position.chebyshev_distance(target)
        if step_dist > self.MAX_STEP_DISTANCE:
            # Teleport attempt detected! Rubberband back to last valid position
            return False, f"Teleport detected: step distance {step_dist} exceeds max {self.MAX_STEP_DISTANCE}", self.last_valid_position

        # 4. Speedhack check (rate limiting)
        if self.last_move_timestamp_ms > 0:
            time_diff = timestamp_ms - self.last_move_timestamp_ms
            if time_diff < self.MIN_TICK_INTERVAL_MS and step_dist > 0:
                return False, f"Speedhack detected: move packet received after only {time_diff}ms", self.last_valid_position

        # Move accepted
        self.current_position = target
        self.last_valid_position = target
        self.last_move_timestamp_ms = timestamp_ms
        return True, "Move accepted", self.current_position

    def validate_instant_move_request(self, target_x: int, target_y: int, debugger_attached: bool) -> Tuple[bool, str, Point]:
        """
        Validates InstantMoveRequest packet (CharacterMoveBaseHandlerPlugIn.cs).
        According to security specification, unauthenticated instant move requests must be rejected
        unless debugger is attached, preventing cheater teleportation.
        """
        if not debugger_attached:
            return False, "Instant move rejected: cheater teleport attempt (debugger not attached)", self.last_valid_position

        target = Point(target_x, target_y)
        if not (0 <= target.x < self.MAP_SIZE and 0 <= target.y < self.MAP_SIZE):
            return False, "Instant move coordinates out of map bounds", self.last_valid_position

        self.current_position = target
        self.last_valid_position = target
        return True, "Instant move accepted for debugging", self.current_position
