# tests/e2e/harness/skill_engine.py
"""
OpenMU Skill Hit Validation and Combat Anti-Cheat Engine.
Implements reference SkillHitValidator logic, animation counter synchronization,
hit delay checks, range validation, and skill list index safety.
"""

from dataclasses import dataclass
from typing import Dict, Tuple, Optional
import math


@dataclass
class SkillDefinition:
    skill_id: int
    name: str
    max_range: float
    max_hits_per_cast: int
    cooldown_ms: int


# Standard OpenMU Skill definitions
KNOWN_SKILLS: Dict[int, SkillDefinition] = {
    1: SkillDefinition(1, "Poison", 6.0, 1, 500),
    2: SkillDefinition(2, "Meteorite", 6.0, 1, 500),
    3: SkillDefinition(3, "Lighting", 6.0, 1, 500),
    4: SkillDefinition(4, "FireBall", 6.0, 1, 500),
    5: SkillDefinition(5, "Flame", 6.0, 1, 500),
    6: SkillDefinition(6, "Teleport", 0.0, 0, 1000),
    7: SkillDefinition(7, "Ice", 6.0, 1, 500),
    8: SkillDefinition(8, "Twister", 6.0, 2, 500),
    9: SkillDefinition(9, "EvilSpirit", 8.0, 5, 400),
    10: SkillDefinition(10, "Hellfire", 3.0, 1, 800),
    11: SkillDefinition(11, "PowerWave", 6.0, 1, 500),
    12: SkillDefinition(12, "AquaBeam", 6.0, 1, 500),
    13: SkillDefinition(13, "Cometfall", 6.0, 1, 500),
    14: SkillDefinition(14, "Inferno", 3.0, 1, 800),
    18: SkillDefinition(18, "Defense", 0.0, 0, 1000),
    19: SkillDefinition(19, "FallingSlash", 2.0, 1, 500),
    20: SkillDefinition(20, "Lunge", 2.0, 1, 500),
    21: SkillDefinition(21, "Uppercut", 2.0, 1, 500),
    22: SkillDefinition(22, "Cyclone", 2.0, 1, 500),
    23: SkillDefinition(23, "Slash", 2.0, 1, 500),
    41: SkillDefinition(41, "TwistingSlash", 3.0, 3, 400),
    42: SkillDefinition(42, "RagefulBlow", 4.0, 1, 1000),
    43: SkillDefinition(43, "DeathStab", 3.0, 1, 500),
    48: SkillDefinition(48, "SwellLife", 0.0, 0, 1500),
}


@dataclass
class HitEntry:
    skill_id: int
    timestamp_ms: int
    is_animation: bool
    hit_count: int


class SkillEngine:
    MAX_ANIMATION_TO_HIT_DELAY_MS = 1500
    MAXIMUM_COUNTER_VALUE = 255

    def __init__(self):
        self._hits: Dict[int, HitEntry] = {}
        self._learned_skills: list[int] = []

    def learn_skill(self, skill_id: int):
        if skill_id not in self._learned_skills:
            self._learned_skills.append(skill_id)

    def remove_skill_by_index(self, index: int) -> Tuple[bool, Optional[int]]:
        """
        Safely removes skill by index (Feature 11: SkillListViewPlugIn Index Safety).
        Must guard against index < 0 or index >= len(skills) returning 255 underflow.
        """
        if index < 0 or index >= len(self._learned_skills):
            return False, None
        removed = self._learned_skills.pop(index)
        return True, removed

    def register_animation(self, animation_counter: int, skill_id: int, timestamp_ms: int) -> bool:
        """Registers a client animation event."""
        if not (0 <= animation_counter <= self.MAXIMUM_COUNTER_VALUE):
            return False
        self._hits[animation_counter] = HitEntry(
            skill_id=skill_id,
            timestamp_ms=timestamp_ms,
            is_animation=True,
            hit_count=0
        )
        return True

    def validate_skill_hit(self, animation_counter: int, hit_counter: int, skill_id: int,
                           player_pos: Tuple[int, int], target_pos: Tuple[int, int],
                           timestamp_ms: int) -> Tuple[bool, bool, str]:
        """
        Validates skill hit request against registered animation and constraints.
        Returns: (is_valid, is_hacker_flagged, reason)
        """
        if not (0 <= animation_counter <= self.MAXIMUM_COUNTER_VALUE):
            return False, True, f"Animation counter out of range: {animation_counter}"

        animation_entry = self._hits.get(animation_counter)
        if animation_entry is None or not animation_entry.is_animation:
            return False, True, "Skill hit rejected: given animation counter was not registered as animation"

        if animation_entry.skill_id != skill_id:
            return False, True, f"Wrong skill in referenced animation: expected {animation_entry.skill_id}, got {skill_id}"

        # Delay check
        delay = timestamp_ms - animation_entry.timestamp_ms
        if delay > self.MAX_ANIMATION_TO_HIT_DELAY_MS:
            return False, True, f"Skill hit rejected: delay {delay}ms exceeds max {self.MAX_ANIMATION_TO_HIT_DELAY_MS}ms"

        # Range check
        skill_def = KNOWN_SKILLS.get(skill_id)
        if skill_def:
            dist = math.sqrt((player_pos[0] - target_pos[0]) ** 2 + (player_pos[1] - target_pos[1]) ** 2)
            if dist > skill_def.max_range:
                return False, True, f"Target out of skill range: distance {dist:.2f} > max range {skill_def.max_range}"

            # Hit count check
            if animation_entry.hit_count >= skill_def.max_hits_per_cast:
                return False, True, f"Hit count exceeded for single animation cast: {animation_entry.hit_count} >= {skill_def.max_hits_per_cast}"

        # Update entry
        animation_entry.hit_count += 1
        self._hits[animation_counter] = animation_entry
        self._hits[hit_counter] = HitEntry(skill_id, timestamp_ms, False, 0)

        return True, False, "Skill hit valid"
