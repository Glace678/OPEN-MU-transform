# tests/e2e/harness/regression_models.py
"""
OpenMU Defect Remediation & Regression Test Models.
Authoritative reference implementations and verification engines for:
1. DroppedItem stack ownership defense & character-bound protection (GL-01).
2. Player PK/Hero regeneration calculation monotonicity (GL-03).
3. DictionaryAdapter / CollectionToDictionaryAdapter indexer replacement & KeyValuePair contract (PER-01 / PER-12).
4. IdGenerator lower-bound validation, issuance checks, and duplicate enqueue rejection (PER-02 / PER-13).
5. PacketDefinitions XML save truncation & FileMode.Create overwrite fidelity (NET-04).
"""

from dataclasses import dataclass, field
from datetime import datetime, timedelta, timezone
from enum import IntEnum
from typing import Dict, List, Optional, Set, Tuple, Any
from collections import deque
import xml.etree.ElementTree as ET
import os


# ==============================================================================
# Area 1: DroppedItem Stack Ownership Defense (GL-01)
# ==============================================================================

@dataclass
class ItemDefinition:
    item_id: int
    name: str
    is_stackable: bool = False
    is_bound_to_character: bool = False
    max_durability: int = 255


@dataclass
class MockItem:
    definition: ItemDefinition
    durability: int = 1
    bound_character_id: Optional[str] = None

    def is_stackable(self) -> bool:
        return self.definition.is_stackable

    def can_completely_stack_on(self, other: "MockItem") -> bool:
        if not self.is_stackable() or not other.is_stackable():
            return False
        if self.definition.item_id != other.definition.item_id:
            return False
        # Can stack if target has room
        return self.durability < self.definition.max_durability


@dataclass
class MockPlayer:
    player_id: str
    character_name: str
    inventory: List[MockItem] = field(default_factory=list)
    max_inventory_slots: int = 64

    def has_inventory_space(self) -> bool:
        return len(self.inventory) < self.max_inventory_slots

    def find_stack_target(self, item: MockItem) -> Optional[MockItem]:
        for inv_item in self.inventory:
            if inv_item.can_completely_stack_on(item):
                return inv_item
        return None


class DroppedItemEngine:
    TIME_UNTIL_DROP_IS_FREE = timedelta(seconds=10)

    def __init__(
        self,
        item: MockItem,
        drop_timestamp: datetime,
        owner_player_ids: Optional[Set[str]] = None,
    ):
        self.item = item
        self.drop_timestamp = drop_timestamp
        self.owners: Set[str] = owner_player_ids or set()
        self.available_to_pick: bool = True

    def is_player_an_owner(self, player: MockPlayer) -> bool:
        if not self.owners:
            return True
        return player.player_id in self.owners

    def try_pickup_hardened(
        self,
        player: MockPlayer,
        current_time: datetime,
    ) -> Tuple[bool, Optional[MockItem], str]:
        """
        Hardened DroppedItem.TryPickUpByAsync implementation (GL-01 remediation).
        Enforces character binding and drop ownership protection BEFORE any stacking or inventory pickup.
        """
        if not self.available_to_pick:
            return False, None, "Item is already picked up or unavailable"

        # Guard 1: Character-bound item restriction
        if self.item.definition.is_bound_to_character and not self.is_player_an_owner(player):
            return False, None, "Ownership bypass rejected: Item is bound to character"

        # Guard 2: 10-second drop protection window
        protection_end = self.drop_timestamp + self.TIME_UNTIL_DROP_IS_FREE
        if not self.is_player_an_owner(player) and current_time < protection_end:
            return False, None, "Ownership bypass rejected: Item is under owner protection"

        # Stacking branch (only reached if ownership/binding check passed!)
        stack_target = player.find_stack_target(self.item) if self.item.is_stackable() else None
        if stack_target is not None:
            # Clamp durability to max_durability [0..255]
            new_dur = min(
                stack_target.durability + self.item.durability,
                stack_target.definition.max_durability,
            )
            stack_target.durability = new_dur
            self.available_to_pick = False
            return True, stack_target, "Stacked successfully on existing inventory item"

        # Normal inventory pickup
        if player.has_inventory_space():
            player.inventory.append(self.item)
            self.available_to_pick = False
            return True, None, "Picked up successfully to inventory slot"

        return False, None, "Inventory full"

    def try_pickup_vulnerable(
        self,
        player: MockPlayer,
        current_time: datetime,
    ) -> Tuple[bool, Optional[MockItem], str]:
        """
        Vulnerable DroppedItem implementation (reproducing GL-01 defect in e3988eb7).
        Stacks onto existing item BEFORE checking ownership or character binding.
        """
        if not self.available_to_pick:
            return False, None, "Item is already picked up or unavailable"

        # BUGGY ORDER: Stacking branch evaluated first!
        stack_target = player.find_stack_target(self.item) if self.item.is_stackable() else None
        if stack_target is not None:
            new_dur = min(
                stack_target.durability + self.item.durability,
                stack_target.definition.max_durability,
            )
            stack_target.durability = new_dur
            self.available_to_pick = False
            return True, stack_target, "Stacked successfully (UNAUTHORIZED THEFT Bypassed Ownership)"

        # Ownership checks only performed if not stacked
        if self.item.definition.is_bound_to_character and not self.is_player_an_owner(player):
            return False, None, "Rejected: Bound to character"

        protection_end = self.drop_timestamp + self.TIME_UNTIL_DROP_IS_FREE
        if not self.is_player_an_owner(player) and current_time < protection_end:
            return False, None, "Rejected: Under owner protection"

        if player.has_inventory_space():
            player.inventory.append(self.item)
            self.available_to_pick = False
            return True, None, "Picked up successfully"

        return False, None, "Inventory full"


# ==============================================================================
# Area 2: Player PK / Hero Regeneration Monotonicity (GL-03)
# ==============================================================================

class HeroState(IntEnum):
    PlayerKiller2 = -2
    PlayerKiller1 = -1
    Normal = 0
    Hero1 = 1
    Hero2 = 2


@dataclass
class PlayerCharacterState:
    name: str
    state: HeroState
    state_remaining_seconds: int
    last_regenerate_utc: datetime


class PlayerRegenerationEngine:
    @staticmethod
    def regenerate_hardened(
        character: PlayerCharacterState,
        current_time_utc: datetime,
        stage_duration_seconds: int = 1800,
    ) -> int:
        """
        Hardened RegenerateHeroStateAsync calculation (GL-03 remediation).
        Elapsed seconds = current_time_utc - last_regenerate_utc (positive).
        Monotonically reduces StateRemainingSeconds over time.
        """
        if character.state_remaining_seconds <= 0:
            return 0

        # Defensive guard: if time traveled backwards, no decrement
        if current_time_utc <= character.last_regenerate_utc:
            return character.state_remaining_seconds

        elapsed = (current_time_utc - character.last_regenerate_utc).total_seconds()
        seconds_deducted = int(round(elapsed))
        if seconds_deducted <= 0:
            return character.state_remaining_seconds

        character.state_remaining_seconds -= seconds_deducted
        character.last_regenerate_utc = current_time_utc

        if character.state_remaining_seconds <= 0:
            character.state_remaining_seconds = 0
            # Transition state towards Normal
            if character.state > HeroState.Normal:
                character.state = HeroState(character.state - 1)
                if character.state != HeroState.Normal:
                    character.state_remaining_seconds = stage_duration_seconds
            elif character.state < HeroState.Normal:
                character.state = HeroState(character.state + 1)
                if character.state != HeroState.Normal:
                    character.state_remaining_seconds = stage_duration_seconds

        return character.state_remaining_seconds

    @staticmethod
    def regenerate_vulnerable(
        character: PlayerCharacterState,
        current_time_utc: datetime,
    ) -> int:
        """
        Vulnerable calculation reproducing GL-03 defect:
        Subtract(UtcNow) yields negative number, so -= adds time.
        """
        if character.state_remaining_seconds <= 0:
            return 0

        # Legacy inverted subtraction: _lastRegenerate.Subtract(DateTime.UtcNow) -> negative
        inverted_diff = (character.last_regenerate_utc - current_time_utc).total_seconds()
        character.state_remaining_seconds -= int(round(inverted_diff))
        character.last_regenerate_utc = current_time_utc
        return character.state_remaining_seconds


# ==============================================================================
# Area 3: DictionaryAdapter / CollectionToDictionaryAdapter Contract (PER-01 / PER-12)
# ==============================================================================

class DictionaryAdapterContract:
    """
    Reference model of OpenMU CollectionToDictionaryAdapter & DictionaryAdapter.
    Enforces IDictionary<TKey, TValue> indexer setter upsert replacement contract
    and KeyValuePair Contains / Remove fidelity.
    """
    def __init__(self):
        # Underlying raw list storing join tuples (key, value)
        self._raw_collection: List[Tuple[Any, Any]] = []

    def __getitem__(self, key: Any) -> Any:
        for k, v in self._raw_collection:
            if k == key:
                return v
        raise KeyError(f"No value found for key {key}.")

    def __setitem__(self, key: Any, value: Any) -> None:
        """
        Hardened indexer setter contract:
        If key exists, REPLACES the existing entry. If not, adds it.
        """
        for i, (k, _) in enumerate(self._raw_collection):
            if k == key:
                self._raw_collection[i] = (key, value)
                return
        self._raw_collection.append((key, value))

    def set_vulnerable(self, key: Any, value: Any) -> None:
        """
        Vulnerable indexer setter reproducing PER-12: calls Add, throwing on duplicate key.
        """
        self.add(key, value)

    def add(self, key: Any, value: Any) -> None:
        if self.contains_key(key):
            raise ValueError(f"An item with key {key} already exists.")
        self._raw_collection.append((key, value))

    def contains_key(self, key: Any) -> bool:
        return any(k == key for k, _ in self._raw_collection)

    def contains_kvp_hardened(self, key: Any, value: Any) -> bool:
        """Checks both key AND value."""
        return any(k == key and v == value for k, v in self._raw_collection)

    def contains_kvp_vulnerable(self, key: Any, value: Any) -> bool:
        """Vulnerable implementation checking ONLY key."""
        return self.contains_key(key)

    def remove_key(self, key: Any) -> bool:
        for i, (k, _) in enumerate(self._raw_collection):
            if k == key:
                del self._raw_collection[i]
                return True
        return False

    def remove_kvp_hardened(self, key: Any, value: Any) -> bool:
        """Removes ONLY if both key AND value match."""
        for i, (k, v) in enumerate(self._raw_collection):
            if k == key and v == value:
                del self._raw_collection[i]
                return True
        return False

    def remove_kvp_vulnerable(self, key: Any, value: Any) -> bool:
        """Vulnerable implementation removing if ONLY key matches, ignoring value."""
        return self.remove_key(key)

    def count(self) -> int:
        return len(self._raw_collection)


# ==============================================================================
# Area 4: IdGenerator Lower Bound Validation & Duplicate Rejection (PER-02 / PER-13)
# ==============================================================================

class IdGeneratorEngine:
    """
    Reference model of OpenMU IdGenerator.
    Enforces lower-bound (>= first_value), upper-bound (<= max_value),
    issued boundary (<= current_value), and duplicate enqueue rejection.
    """
    def __init__(self, first_value: int, max_value: int):
        if first_value > max_value:
            raise ValueError("first_value cannot be greater than max_value")
        self.first_value = first_value
        self.max_value = max_value
        self.current_value = first_value - 1
        self._given_back: deque = deque()
        self._in_recycle_pool: Set[int] = set()

    def generate_id(self) -> int:
        if self._given_back:
            recycled = self._given_back.popleft()
            self._in_recycle_pool.remove(recycled)
            return recycled

        if self.current_value >= self.max_value:
            raise OverflowError("Maximum object id exceeded")

        self.current_value += 1
        return self.current_value

    def give_back_hardened(self, id_val: int) -> bool:
        """
        Hardened GiveBack implementation (PER-13 remediation).
        Validates:
        1. id >= first_value (lower bound guard)
        2. id <= max_value (upper bound guard)
        3. id <= current_value (has actually been issued)
        4. id not already in recycle pool (duplicate enqueue prevention)
        """
        if id_val < self.first_value:
            return False
        if id_val > self.max_value:
            return False
        if id_val > self.current_value:
            return False
        if id_val in self._in_recycle_pool:
            return False

        self._given_back.append(id_val)
        self._in_recycle_pool.add(id_val)
        return True

    def give_back_vulnerable(self, id_val: int) -> bool:
        """
        Vulnerable implementation reproducing PER-13:
        Only checks id <= max_value, accepting negative IDs and duplicates.
        """
        if id_val <= self.max_value:
            self._given_back.append(id_val)
            return True
        return False

    @property
    def recycled_count(self) -> int:
        return len(self._given_back)


# ==============================================================================
# Area 5: PacketDefinitions XML Save Truncation (NET-04)
# ==============================================================================

class PacketDefinitionsSaveEngine:
    """
    Reference engine verifying PacketDefinitions XML serialization.
    Validates that FileMode.Create (truncating file) prevents corrupted trailing XML
    when overwriting longer legacy files.
    """
    @staticmethod
    def save_hardened(file_path: str, xml_content: str) -> int:
        """
        Hardened PacketDefinitions.Save:
        Uses FileMode.Create equivalent (opening in write mode with truncate).
        """
        # Ensure parent dir exists
        os.makedirs(os.path.dirname(os.path.abspath(file_path)), exist_ok=True)
        data = xml_content.encode("utf-8")
        with open(file_path, "wb") as f:
            f.write(data)
        return len(data)

    @staticmethod
    def save_vulnerable_openwrite(file_path: str, xml_content: str) -> int:
        """
        Vulnerable PacketDefinitions.Save (fileInfo.OpenWrite()):
        Uses FileMode.OpenOrCreate without truncation.
        """
        os.makedirs(os.path.dirname(os.path.abspath(file_path)), exist_ok=True)
        # Open in r+b mode if file exists (mirroring OpenWrite without truncate)
        if os.path.exists(file_path):
            with open(file_path, "r+b") as f:
                f.seek(0)
                f.write(xml_content.encode("utf-8"))
        else:
            with open(file_path, "wb") as f:
                f.write(xml_content.encode("utf-8"))
        return os.path.getsize(file_path)

    @staticmethod
    def verify_xml_integrity(file_path: str) -> Tuple[bool, Optional[str]]:
        """Parses the XML file and returns whether it is well-formed with no trailing garbage."""
        try:
            tree = ET.parse(file_path)
            root = tree.getroot()
            if root is None:
                return False, "Root element is None"
            return True, None
        except ET.ParseError as e:
            return False, f"XML ParseError: {str(e)}"
        except Exception as e:
            return False, f"General Error: {str(e)}"
