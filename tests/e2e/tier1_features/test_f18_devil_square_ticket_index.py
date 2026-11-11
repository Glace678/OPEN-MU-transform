# tests/e2e/tier1_features/test_f18_devil_square_ticket_index.py
import unittest
from tests.e2e.harness.economy_engine import EconomyEngine


class TestF18DevilSquareTicketIndex(unittest.TestCase):
    """
    Feature 18: Devil Square Negative Ticket Index (DevilSquareEnterHandlerPlugIn.cs).
    Rejects negative ticket index and out-of-bounds inventory index.
    """

    def test_valid_ticket_index_acceptance(self):
        valid, msg = EconomyEngine.validate_devil_square_ticket_index(12)
        self.assertTrue(valid)

    def test_negative_ticket_index_rejected(self):
        valid, msg = EconomyEngine.validate_devil_square_ticket_index(-1)
        self.assertFalse(valid)
        self.assertIn("Negative ticket index rejected", msg)

    def test_extreme_negative_ticket_index_rejected(self):
        valid, msg = EconomyEngine.validate_devil_square_ticket_index(-100)
        self.assertFalse(valid)

    def test_upper_inventory_bounds_rejected(self):
        valid, msg = EconomyEngine.validate_devil_square_ticket_index(EconomyEngine.INVENTORY_SIZE)
        self.assertFalse(valid)
        self.assertIn("out of inventory bounds", msg)

    def test_equipped_slot_zero_index_rejected(self):
        # DevilSquareEnterHandlerPlugIn.cs:45-47 rejects indices < EquippableSlotsCount (12)
        valid, msg = EconomyEngine.validate_devil_square_ticket_index(0)
        self.assertFalse(valid)
        self.assertIn("Equipped slot index rejected", msg)


if __name__ == "__main__":
    unittest.main()
