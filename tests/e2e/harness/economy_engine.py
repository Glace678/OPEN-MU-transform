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
    BYTE_MAX_VALUE = 255
    INVENTORY_SIZE = 108

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
        Feature 16: ItemPriceCalculator.cs 64-bit integer price calculation.
        Computes jewel pack price using 64-bit arithmetic to prevent 32-bit integer overflow.
        """
        multipliers = {
            "PackedBless": 9_000_000,
            "PackedSoul": 6_000_000,
            "PackedChaos": 810_000,
            "PackedLife": 45_000_000,
            "PackedCreation": 36_000_000,
            "PackedGuardian": 60_000_000,
            "PackedGemstone": 18_600,
            "PackedHarmony": 18_600,
        }
        mult = multipliers.get(special_item_type)
        if mult is None:
            raise ValueError(f"Unknown packed jewel type: {special_item_type}")

        # Compute using 64-bit integer arithmetic
        calculated_price = (level + 1) * mult * 10
        # Result clamp to [0, MAX_GOLD]
        return min(max(0, calculated_price), EconomyEngine.MAX_GOLD)

    @staticmethod
    def calculate_packed_jewel_price_32bit_buggy(special_item_type: str, level: int) -> int:
        """
        Simulates the legacy 32-bit integer overflow bug for comparison/demonstration.
        """
        multipliers = {
            "PackedGuardian": 60_000_000,
            "PackedLife": 45_000_000,
        }
        mult = multipliers.get(special_item_type, 1)
        val = (level + 1) * mult * 10
        # Emulate 32-bit signed integer overflow
        val = (val + 2**31) % 2**32 - 2**31
        return val

    @staticmethod
    def validate_player_store_price(price: int) -> Tuple[bool, str]:
        """
        Feature 15: Player store price validation.
        Rejects negative prices and prices exceeding MaxShopPrice.
        """
        if price < 0:
            return False, f"Price {price} is negative (integer overflow detected)"
        if price > EconomyEngine.MAX_SHOP_PRICE:
            return False, f"Price {price} exceeds maximum store price {EconomyEngine.MAX_SHOP_PRICE}"
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
    def downgrade_wing_level(current_level: int) -> int:
        """
        Feature 17: BaseItemCraftingHandler.cs wing level downgrade.
        Guarantees that 0 level wing does not underflow to byte 255.
        """
        return max(0, current_level - 1)

    @staticmethod
    def validate_devil_square_ticket_index(ticket_index: int) -> Tuple[bool, str]:
        """
        Feature 18: DevilSquareEnterHandlerPlugIn.cs ticket index validation.
        Rejects negative ticket index and out-of-bounds inventory index.
        """
        if ticket_index < 0:
            return False, f"Negative ticket index rejected: {ticket_index}"
        if ticket_index >= EconomyEngine.INVENTORY_SIZE:
            return False, f"Ticket index {ticket_index} out of inventory bounds ({EconomyEngine.INVENTORY_SIZE})"
        return True, "Ticket index valid"
