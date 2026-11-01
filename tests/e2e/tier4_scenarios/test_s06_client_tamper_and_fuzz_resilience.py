# tests/e2e/tier4_scenarios/test_s06_client_tamper_and_fuzz_resilience.py
import unittest
from tests.e2e.harness.packet_framing import PacketFramingEngine
from tests.e2e.harness.systemic_engine import BuffScriptValidator, QuestRewardValidator


class TestS06ClientTamperAndFuzzResilience(unittest.TestCase):
    """
    Tier 4: Scenario 11 & 12.
    Scenario 11: Network Fuzzing & Truncated Frame Rejection.
    Scenario 12: Malformed Buff BMD & Quest Script Injection Resilience.
    """

    def test_scenario_11_network_fuzzing_and_truncated_frame_rejection(self):
        # Suite of fuzzed and truncated payloads
        fuzz_corpus = [
            b"",                                  # Null/empty
            b"\xC1",                              # 1 byte
            b"\xC1\x02",                          # Declared size 2 < minimum
            b"\xC1\xFF\x00\x01\x02",              # Declared 255 bytes, 5 supplied
            b"\xC2\x00",                          # Truncated C2 header
            b"\xC2\x10\x00\x01\x02",              # Declared 4096 bytes, 5 supplied
            b"\x00\x10\x00\x00",                  # Corrupted header 0x00
            b"\xFF\xFF\xFF\xFF",                  # Corrupted header 0xFF
            bytes([0xC1, 14, 0x24] + [0] * 5),    # MoneyDropped declared 14, 8 supplied
        ]

        for i, payload in enumerate(fuzz_corpus):
            valid, err = PacketFramingEngine.validate_client_dispatch(payload)
            self.assertFalse(valid, f"Corpus item {i} should have been rejected")
            self.assertIsNotNone(err)

    def test_scenario_12_malformed_bmd_and_quest_injection_resilience(self):
        # 1. BMD file with listsize = 0x40000000 (32-bit overflow)
        self.assertFalse(BuffScriptValidator.is_valid_record_count(0x40000000, 128))

        # 2. BMD file with 15,000 records (> 10,000 threshold)
        self.assertFalse(BuffScriptValidator.is_valid_record_count(15000, 128))

        # 3. Quest packet injection with 250 request items
        req_clamped = QuestRewardValidator.clamp_request_count(250)
        self.assertEqual(req_clamped, 5)

        # 4. Quest packet injection with 200 reward items
        rew_clamped = QuestRewardValidator.clamp_reward_count(200)
        self.assertEqual(rew_clamped, 5)


if __name__ == "__main__":
    unittest.main()
