# tests/e2e/tier2_boundaries/test_b18_devil_square_ticket_index_boundaries.py
import unittest
from tests.e2e.harness.economy_engine import EconomyEngine


class TestB18DevilSquareTicketIndexBoundaries(unittest.TestCase):
    """
    Feature 18 Boundaries: Devil Square Ticket Index Validation.
    Tests boundary inventory indices: -1, 0..11 (equipped), 12 (first backpack slot), 203 (max), 204, and 255.
    """

    def test_negative_one_index_rejected(self):
        valid, msg = EconomyEngine.validate_devil_square_ticket_index(-1)
        self.assertFalse(valid)

    def test_equipped_slots_0_and_11_rejected_and_12_allowed(self):
        self.assertFalse(EconomyEngine.validate_devil_square_ticket_index(0)[0])
        self.assertFalse(EconomyEngine.validate_devil_square_ticket_index(11)[0])
        self.assertTrue(EconomyEngine.validate_devil_square_ticket_index(12)[0])

    def test_max_inventory_slot_203_allowed(self):
        # 203 is INVENTORY_SIZE - 1 (12 + 64 + 4*32 - 1)
        valid, _ = EconomyEngine.validate_devil_square_ticket_index(EconomyEngine.INVENTORY_SIZE - 1)
        self.assertTrue(valid)

    def test_index_204_rejected(self):
        # 204 is INVENTORY_SIZE (first invalid index)
        valid, msg = EconomyEngine.validate_devil_square_ticket_index(EconomyEngine.INVENTORY_SIZE)
        self.assertFalse(valid)

    def test_index_255_rejected(self):
        # 255 was the unsigned byte representation of -1
        valid, msg = EconomyEngine.validate_devil_square_ticket_index(255)
        self.assertFalse(valid)


if __name__ == "__main__":
    unittest.main()
