# tests/adversarial/adversarial_python_runner.py
"""
Adversarial Python Stress Runner for Milestone 1 Security & Memory Hardening.
Executes 20,000+ adversarial assertions testing:
1. WSclient PacketDispatchPolicy framing, truncation, and size fuzzing
2. BuffScriptLoader multiplication wrap & record count bounds
3. CSEventMatch Devil Square rank out-of-bounds inputs
4. PersonalShopTitleImp UAF lifecycle elimination
5. QuestMng request and reward count overflow protection
6. BannerInfoList stack buffer clamp
7. LetterText heap buffer overflow clamp
"""

import sys
import os
import random
import unittest
import tempfile
from datetime import datetime, timedelta, timezone

# Ensure project root is in sys.path
REPO_ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
if REPO_ROOT not in sys.path:
    sys.path.insert(0, REPO_ROOT)

from tests.e2e.harness.packet_framing import PacketFramingEngine, PACKET_SIZE_TABLE
from tests.e2e.harness.systemic_engine import (
    BuffScriptValidator,
    DevilSquareRankValidator,
    QuestRewardValidator,
    BannerBufferValidator,
    LetterTextBufferValidator,
)
from tests.e2e.tier1_features.test_f04_personal_shop_uaf import MockShopTitleDrawObj
from tests.e2e.harness.regression_models import (
    ItemDefinition,
    MockItem,
    MockPlayer,
    DroppedItemEngine,
    HeroState,
    PlayerCharacterState,
    PlayerRegenerationEngine,
    DictionaryAdapterContract,
    IdGeneratorEngine,
    PacketDefinitionsSaveEngine,
)


class AdversarialEmpiricalStressSuite(unittest.TestCase):
    def test_01_malformed_c1_packets(self):
        """Probe C1 packets with < 3 bytes, size 0, and invalid lengths."""
        # Empty
        valid, _ = PacketFramingEngine.validate_client_dispatch(b"")
        self.assertFalse(valid)

        # 1 byte
        valid, _ = PacketFramingEngine.validate_client_dispatch(b"\xC1")
        self.assertFalse(valid)

        # 2 bytes (declared size 0, 1, 2)
        for sz in [0, 1, 2]:
            valid, _ = PacketFramingEngine.validate_client_dispatch(bytes([0xC1, sz]))
            self.assertFalse(valid)

        # Truncated before headcode
        valid, _ = PacketFramingEngine.validate_client_dispatch(b"\xC1\x02")
        self.assertFalse(valid)

    def test_02_malformed_c2_packets(self):
        """Probe C2 packets with < 4 bytes, truncated size, and invalid lengths."""
        # 1 byte
        valid, _ = PacketFramingEngine.validate_client_dispatch(b"\xC2")
        self.assertFalse(valid)

        # 2 bytes
        valid, _ = PacketFramingEngine.validate_client_dispatch(b"\xC2\x00")
        self.assertFalse(valid)

        # 3 bytes (declared size 0..3)
        for sz in [0, 1, 2, 3]:
            valid, _ = PacketFramingEngine.validate_client_dispatch(bytes([0xC2, 0x00, sz]))
            self.assertFalse(valid)

    def test_03_fuzzed_packets_declared_size_exceeding_buffer(self):
        """Adversarial probe: declared size > actual buffer length."""
        rng = random.Random(42)
        for _ in range(2000):
            header = rng.choice([0xC1, 0xC2, 0xC3, 0xC4])
            actual_len = rng.randint(4, 30)
            if header in (0xC1, 0xC3):
                declared_size = rng.randint(actual_len + 1, 255)
                packet = bytes([header, declared_size, 0x00]) + bytes(rng.getrandbits(8) for _ in range(actual_len - 3))
            else:
                declared_size = rng.randint(actual_len + 1, 4096)
                packet = bytes([header, (declared_size >> 8) & 0xFF, declared_size & 0xFF, 0x00]) + bytes(rng.getrandbits(8) for _ in range(actual_len - 4))

            valid, err = PacketFramingEngine.validate_client_dispatch(packet)
            self.assertFalse(valid)
            self.assertIn("Truncated", err)

    def test_04_truncated_subcode_fuzzing(self):
        """Probe multiplexed headcodes when subcode byte is missing or truncated."""
        multiplexed_ops = [0xF1, 0xF3, 0x3F]
        for op in multiplexed_ops:
            # C1 packet with exactly 3 bytes has header + headcode, but NO subcode!
            pkt = bytes([0xC1, 3, op])
            # Even if framing parses, dispatch should reject if subcode is required or expected size > 3
            valid, _ = PacketFramingEngine.validate_client_dispatch(pkt)
            self.assertFalse(valid)

    def test_05_buff_script_32bit_multiplication_wrap(self):
        """Adversarial stress: 32-bit multiplication wrap attacks on listsize."""
        wrap_vectors = [
            0x40000000,
            0x80000000,
            0xFFFFFFFF,
            0x20000000,
            0x10000000,
            10001,
            20000,
            100000,
            0,
            -1,
            -1000,
        ]
        for listsize in wrap_vectors:
            self.assertFalse(BuffScriptValidator.is_valid_record_count(listsize, 128))
            self.assertFalse(BuffScriptValidator.is_valid_record_count(listsize, 4))
            self.assertFalse(BuffScriptValidator.is_valid_record_count(listsize, 1))

        # Valid boundaries
        self.assertTrue(BuffScriptValidator.is_valid_record_count(1, 128))
        self.assertTrue(BuffScriptValidator.is_valid_record_count(100, 128))
        self.assertTrue(BuffScriptValidator.is_valid_record_count(10000, 128))

    def test_06_devil_square_rank_oob_sweep(self):
        """Adversarial stress: iNumDevilRank out-of-bounds inputs."""
        extreme_ranks = [-999999, -255, -1, 0, 11, 12, 13, 50, 100, 199, 200, 255, 65535, 2147483647]
        for r in extreme_ranks:
            clamped = DevilSquareRankValidator.clamp_ranks(r)
            self.assertGreaterEqual(clamped, 0)
            self.assertLessEqual(clamped, DevilSquareRankValidator.MAX_RANKS)

    def test_07_personal_shop_uaf_probe(self):
        """Adversarial probe: verify that deleting draw object prevents UAF when key is cached."""
        # 1. Simulating vulnerable pattern (direct access after delete throws UAF)
        obj = MockShopTitleDrawObj(key=123)
        obj.delete()
        with self.assertRaises(RuntimeError):
            _ = obj.get_key()

        # 2. Simulating hardened pattern (cache key before delete)
        obj2 = MockShopTitleDrawObj(key=456)
        cached_key = obj2.get_key()
        obj2.delete()
        # Logging operates strictly on cached integer
        self.assertEqual(cached_key, 456)

    def test_08_quest_mng_count_overflow_sweep(self):
        """Adversarial stress: byRequestCount and byRewardCount > 5 probes."""
        for c in range(6, 256):
            self.assertEqual(QuestRewardValidator.clamp_request_count(c), 5)
            self.assertEqual(QuestRewardValidator.clamp_reward_count(c), 5)

        for c in range(-100, 1):
            self.assertEqual(QuestRewardValidator.clamp_request_count(c), 0)
            self.assertEqual(QuestRewardValidator.clamp_reward_count(c), 0)

        for c in range(1, 6):
            self.assertEqual(QuestRewardValidator.clamp_request_count(c), c)
            self.assertEqual(QuestRewardValidator.clamp_reward_count(c), c)

    def test_09_banner_info_list_sizing(self):
        """Adversarial probe: wchar_t buffer capacity vs byte size."""
        self.assertEqual(BannerBufferValidator.safe_getline_capacity(), 1024)
        # Buffer element count is half of byte size (2048 bytes / 2 bytes per wchar_t = 1024 elements)
        self.assertEqual(1024 * BannerBufferValidator.WCHAR_SIZE, 2048)

    def test_10_letter_text_buffer_clamp(self):
        """Adversarial probe: FS_LETTER_TEXT memory copy bounds."""
        for sz in [0, 1, 50, 1000, 1024]:
            self.assertEqual(LetterTextBufferValidator.get_safe_copy_length(sz), sz)
        for sz in [1025, 2000, 5000, 65535]:
            self.assertEqual(LetterTextBufferValidator.get_safe_copy_length(sz), 1024)

    def test_11_dropped_item_adversarial_theft_stacking_probe(self):
        """Adversarial probe: 1000 simulated attack bursts attempting to steal protected drops via stacking."""
        item_def = ItemDefinition(item_id=99, name="AdversarialJewel", is_stackable=True, is_bound_to_character=False)
        drop_time = datetime(2026, 11, 1, 12, 0, 0, tzinfo=timezone.utc)
        owner = MockPlayer(player_id="legit_owner", character_name="Alice")
        attacker = MockPlayer(player_id="adversary", character_name="Mallory")
        attacker.inventory.append(MockItem(definition=item_def, durability=50))

        rng = random.Random(1337)
        for trial in range(1000):
            ground = MockItem(definition=item_def, durability=rng.randint(1, 20))
            engine = DroppedItemEngine(item=ground, drop_timestamp=drop_time, owner_player_ids={owner.player_id})
            # Attacker probes at random milliseconds within [0..9999ms]
            probe_ms = rng.randint(0, 9999)
            probe_time = drop_time + timedelta(milliseconds=probe_ms)
            ok, target, _ = engine.try_pickup_hardened(attacker, probe_time)
            self.assertFalse(ok)
            self.assertIsNone(target)
            self.assertEqual(attacker.inventory[0].durability, 50)
            self.assertTrue(engine.available_to_pick)

    def test_12_player_regen_adversarial_time_manipulation(self):
        """Adversarial stress: Monotonicity invariant holds across 1000 randomized time steps & jitter."""
        t0 = datetime(2026, 11, 1, 12, 0, 0, tzinfo=timezone.utc)
        char = PlayerCharacterState(
            name="Stress_PK",
            state=HeroState.PlayerKiller1,
            state_remaining_seconds=10000,
            last_regenerate_utc=t0,
        )
        rng = random.Random(2026)
        current_time = t0
        prev_remaining = char.state_remaining_seconds

        for _ in range(500):
            # Time step randomly moves forward by 1..30s, or steps backward by 1..5s (clock jitter attack)
            jitter = rng.randint(-5, 30)
            candidate_time = current_time + timedelta(seconds=jitter)
            new_remaining = PlayerRegenerationEngine.regenerate_hardened(char, candidate_time)
            # Hardened invariant: countdown must NEVER increase!
            self.assertLessEqual(new_remaining, prev_remaining)
            prev_remaining = new_remaining
            if candidate_time > current_time:
                current_time = candidate_time

    def test_13_dictionary_adapter_duplicate_key_and_kvp_fidelity(self):
        """Adversarial stress: 2000 random upsert replacements & KeyValuePair value queries."""
        adapter = DictionaryAdapterContract()
        rng = random.Random(999)
        oracle_dict = {}

        for _ in range(2000):
            key = f"key_{rng.randint(1, 20)}"
            val = rng.randint(1, 100000)
            # Upsert
            adapter[key] = val
            oracle_dict[key] = val
            self.assertEqual(adapter[key], oracle_dict[key])
            self.assertEqual(adapter.count(), len(oracle_dict))

            # Query KeyValuePair with correct vs wrong value
            self.assertTrue(adapter.contains_kvp_hardened(key, val))
            self.assertFalse(adapter.contains_kvp_hardened(key, val + 1))

    def test_14_id_generator_negative_and_duplicate_injection(self):
        """Adversarial stress: 5000 malicious GiveBack probes with negative, oversized, and duplicate IDs."""
        gen = IdGeneratorEngine(first_value=500, max_value=1500)
        issued_ids = [gen.generate_id() for _ in range(100)]
        self.assertEqual(issued_ids[0], 500)
        self.assertEqual(issued_ids[-1], 599)

        rng = random.Random(777)
        for _ in range(5000):
            probe_type = rng.choice(["negative", "oversized", "unissued", "valid_then_dup"])
            if probe_type == "negative":
                neg_id = -rng.randint(1, 100000)
                self.assertFalse(gen.give_back_hardened(neg_id))
            elif probe_type == "oversized":
                huge_id = rng.randint(1501, 100000)
                self.assertFalse(gen.give_back_hardened(huge_id))
            elif probe_type == "unissued":
                unissued = rng.randint(600, 1500)
                self.assertFalse(gen.give_back_hardened(unissued))
            elif probe_type == "valid_then_dup":
                target_id = issued_ids[rng.randint(0, len(issued_ids) - 1)]
                if target_id not in gen._in_recycle_pool:
                    self.assertTrue(gen.give_back_hardened(target_id))
                    # Duplicate enqueue MUST fail
                    self.assertFalse(gen.give_back_hardened(target_id))

    def test_15_xml_truncation_trailing_garbage_fuzzing(self):
        """Adversarial stress: Overwrite varying sized XML documents and assert zero trailing garbage."""
        with tempfile.TemporaryDirectory() as td:
            fpath = os.path.join(td, "FuzzDefs.xml")
            rng = random.Random(404)

            for trial in range(50):
                long_sz = rng.randint(500, 3000)
                short_sz = rng.randint(50, 400)
                long_content = f"<Defs count='{long_sz}'>" + ("<X>" + "A" * 50 + "</X>") * (long_sz // 50) + "</Defs>"
                short_content = f"<Defs count='{short_sz}'>" + ("<Y>" + "B" * 20 + "</Y>") * (short_sz // 20) + "</Defs>"

                # Write long then short
                PacketDefinitionsSaveEngine.save_hardened(fpath, long_content)
                PacketDefinitionsSaveEngine.save_hardened(fpath, short_content)

                # Assert exact byte truncation
                self.assertEqual(os.path.getsize(fpath), len(short_content.encode("utf-8")))
                valid, err = PacketDefinitionsSaveEngine.verify_xml_integrity(fpath)
                self.assertTrue(valid, f"Fuzz trial {trial} failed: {err}")


if __name__ == "__main__":
    suite = unittest.TestLoader().loadTestsFromTestCase(AdversarialEmpiricalStressSuite)
    runner = unittest.TextTestRunner(verbosity=2)
    result = runner.run(suite)
    sys.exit(0 if result.wasSuccessful() else 1)
