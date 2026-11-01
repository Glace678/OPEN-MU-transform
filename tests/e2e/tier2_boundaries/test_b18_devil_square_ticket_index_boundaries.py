# tests/e2e/tier2_boundaries/test_b18_devil_square_ticket_index_boundaries.py
import unittest
from tests.e2e.harness.economy_engine import EconomyEngine


class TestB18DevilSquareTicketIndexBoundaries(unittest.TestCase):
    """
    Feature 18 Boundaries: Devil Square Negative Ticket Index.
    Tests boundary inventory indices: -1, 0, 107 (capacity - 1), 108 (capacity), and 255.
    """

    def test_negative_one_index_rejected(self):
        valid, msg = EconomyEngine.validate_devil_square_ticket_index(-1)
        self.assertFalse(valid)

    def test_index_zero_allowed(self):
        valid, _ = EconomyEngine.validate_devil_square_ticket_index(0)
        self.assertTrue(valid)

    def test_index_107_allowed(self):
        # 107 is INVENTORY_SIZE - 1
        valid, _ = EconomyEngine.validate_devil_square_ticket_index(EconomyEngine.INVENTORY_SIZE - 1)
        self.assertTrue(valid)

    def test_index_108_rejected(self):
        # 108 is INVENTORY_SIZE (first invalid index)
        valid, msg = EconomyEngine.validate_devil_square_ticket_index(EconomyEngine.INVENTORY_SIZE)
        self.assertFalse(valid)

    def test_index_255_rejected(self):
        # 255 was the unsigned byte representation of -1
        valid, msg = EconomyEngine.validate_devil_square_ticket_index(255)
        self.assertFalse(valid)


if __name__ == "__main__":
    unittest.main()
