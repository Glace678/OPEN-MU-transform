# tests/e2e/harness/economy_engine.py
"""
OpenMU Economy, Arithmetic, Durability, and Store Transaction Engine.
Implements reference logic for 64-bit jewel pricing, durability clamping [0..255],
store price bounds, rollback refunds, wing downgrade safety, and ticket validation.
"""

from typing import Tuple, Optional


class EconomyEngine:
    MAX_GOLD = 2_000_000_000
    MAX_SHOP_PRICE = 2_000_000_000
    INT32_MAX = 2_147_483_647
    BYTE_MAX_VALUE = 255
    EQUIPPABLE_SLOTS_COUNT = 12
    MAX_INVENTORY_SLOT_INDEX = 203
    INVENTORY_SIZE = 204  # InventoryConstants.GetInventorySize(4) == 12 + 64 + 4 * 32

    PACKED_JEWEL_MULTIPLIERS = {
        "PackedBless": 1_480,
        "PackedSoul": 1_120,
        "PackedChaos": 385,
        "PackedLife": 2_950,
        "PackedCreation": 2_360,
        "PackedGuardian": 3_900,
        "PackedGemstone": 18_600,
        "PackedHarmony": 18_600,
        "PackedLowerRefineStone": 18_600,
        "PackedHigherRefineStone": 18_600,
    }

    @staticmethod
    def clamp_durability_addition(current_durability: int, added_durability: int) -> int:
        """
        Feature 14: DroppedItem.cs durability stacking clamp.
        Guarantees that durability addition never wraps around byte [0..255].
        """
        if current_durability < 0 or added_durability < 0:
            raise ValueError("Durability cannot be negative")
        total = current_durability + added_durability
        return min(total, EconomyEngine.BYTE_MAX_VALUE)

    @staticmethod
    def calculate_packed_jewel_price_64bit(special_item_type: str, level: int) -> int:
        """
        Feature 16: ItemPriceCalculator.cs:106-115 64-bit integer price calculation.
        Computes jewel pack price using 64-bit arithmetic to prevent 32-bit integer overflow.
        """
        mult = EconomyEngine.PACKED_JEWEL_MULTIPLIERS.get(special_item_type)
        if mult is None:
            raise ValueError(f"Unknown packed jewel type: {special_item_type}")

        # Compute using 64-bit integer arithmetic matching (level + 1) * base * 10L
        calculated_price = (level + 1) * mult * 10
        # Result clamp to [0, MAX_GOLD]
        return min(max(0, calculated_price), EconomyEngine.MAX_GOLD)

    @staticmethod
    def calculate_packed_jewel_price_32bit_buggy(special_item_type: str, level: int) -> int:
        """
        Simulates the legacy 32-bit signed integer overflow bug on identical multipliers.
        """
        mult = EconomyEngine.PACKED_JEWEL_MULTIPLIERS.get(special_item_type)
        if mult is None:
            raise ValueError(f"Unknown packed jewel type: {special_item_type}")
        val = (level + 1) * mult * 10
        # Emulate 32-bit signed integer overflow
        val = (val + 2**31) % 2**32 - 2**31
        return val

    @staticmethod
    def validate_player_store_price(price: int, max_inventory_money: Optional[int] = None) -> Tuple[bool, str]:
        """
        Feature 15: SetItemPriceAction.cs:53 player store price validation.
        Rejects negative prices and prices exceeding MaximumInventoryMoney (defaults to int.MaxValue).
        """
        upper_limit = EconomyEngine.INT32_MAX if max_inventory_money is None else max_inventory_money
        if price < 0:
            return False, f"Price {price} is negative (integer overflow detected)"
        if price > upper_limit:
            return False, f"Price {price} exceeds maximum store price {upper_limit}"
        return True, "Price is valid"

    @staticmethod
    def process_store_purchase(buyer_gold: int, seller_gold: int, price: int,
                               inventory_has_space: bool) -> Tuple[bool, int, int, str]:
        """
        Feature 12: BuyRequestAction.cs rollback logic.
        If inventory space check fails or transaction aborts, buyer is refunded (not double deducted).
        Returns: (success, final_buyer_gold, final_seller_gold, status)
        """
        if price <= 0:
            return False, buyer_gold, seller_gold, "Invalid price"
        if buyer_gold < price:
            return False, buyer_gold, seller_gold, "Insufficient buyer funds"

        # Preliminary deduction
        buyer_temp_gold = buyer_gold - price

        # Inventory check
        if not inventory_has_space:
            # Rollback: Refund buyer! (Legacy bug deducted price AGAIN: buyer_temp_gold - price)
            final_buyer_gold = buyer_temp_gold + price
            return False, final_buyer_gold, seller_gold, "Transaction failed: inventory full, funds refunded"

        # Success: commit seller credit
        final_seller_gold = min(seller_gold + price, EconomyEngine.MAX_GOLD)
        return True, buyer_temp_gold, final_seller_gold, "Transaction successful"

    @staticmethod
    def downgrade_wing_level(current_level: int, downgrade_steps: int = 2) -> int:
        """
        Feature 17: BaseItemCraftingHandler.cs:297-312 ThirdWingsDowngradedRandom.
        Downgrades by 2 or 3 levels randomly and clamps via Math.Max(0, item.Level - downgrade).
        """
        if downgrade_steps not in (2, 3):
            raise ValueError(f"Third-wing downgrade steps must be 2 or 3, got {downgrade_steps}")
        return max(0, current_level - downgrade_steps)

    @staticmethod
    def validate_devil_square_ticket_index(ticket_index: int) -> Tuple[bool, str]:
        """
        Feature 18: DevilSquareEnterHandlerPlugIn.cs:45-53 ticket index validation.
        Rejects negative indices, equipped item slots (< 12), and out-of-bounds inventory indices (> 203).
        """
        if ticket_index < 0:
            return False, f"Negative ticket index rejected: {ticket_index}"
        if ticket_index < EconomyEngine.EQUIPPABLE_SLOTS_COUNT:
            return False, f"Equipped slot index rejected: {ticket_index} (< {EconomyEngine.EQUIPPABLE_SLOTS_COUNT})"
        if ticket_index >= EconomyEngine.INVENTORY_SIZE:
            return False, f"Ticket index {ticket_index} out of inventory bounds ({EconomyEngine.INVENTORY_SIZE})"
        return True, "Ticket index valid"
