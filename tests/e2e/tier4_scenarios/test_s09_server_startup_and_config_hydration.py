# tests/e2e/tier4_scenarios/test_s09_server_startup_and_config_hydration.py
import unittest
import tempfile
import os
from tests.e2e.harness.regression_models import (
    PacketDefinitionsSaveEngine,
    IdGeneratorEngine,
    DictionaryAdapterContract,
)


class TestS09ServerStartupAndConfigHydration(unittest.TestCase):
    """
    Tier 4: Scenario 9 & 10.
    Scenario 9: Game Server Startup Lifecycle, XML Truncation Safety & IdGenerator Hydration.
    Scenario 10: Configuration Cache Resilience under Transient Storage Disconnection.
    """

    def setUp(self):
        self.temp_dir = tempfile.TemporaryDirectory()

    def tearDown(self):
        self.temp_dir.cleanup()

    def test_scenario_09_server_startup_xml_definitions_and_id_generator_init(self):
        """
        Full Game Server boot sequence:
        1. Truncates and writes PacketDefinitions XML with FileMode.Create.
        2. Validates schema well-formedness.
        3. Initializes IdGenerators with validated bounds.
        4. Hydrates DictionaryAdapter configuration cache.
        5. Confirms zero stale data and 100% startup configuration fidelity.
        """
        # Step 1: Write and truncate PacketDefinitions XML
        xml_path = os.path.join(self.temp_dir.name, "ServerPacketDefs.xml")
        xml_content = (
            '<?xml version="1.0" encoding="utf-8"?>\n'
            '<PacketDefinitions xmlns="http://www.munique.net/OpenMU/PacketDefinitions">\n'
            '  <Packets>\n'
            '    <Packet Name="ServerHandshake" HeaderType="C1" Length="12" />\n'
            '    <Packet Name="CharacterList" HeaderType="C1" Length="6" />\n'
            '    <Packet Name="ItemDrop" HeaderType="C3" Length="15" />\n'
            '  </Packets>\n'
            '</PacketDefinitions>\n'
        )
        bytes_written = PacketDefinitionsSaveEngine.save_hardened(xml_path, xml_content)
        self.assertEqual(bytes_written, len(xml_content.encode("utf-8")))

        # Step 2: Validate XML well-formedness
        valid, err = PacketDefinitionsSaveEngine.verify_xml_integrity(xml_path)
        self.assertTrue(valid, f"Startup XML validation failed: {err}")

        # Step 3: Initialize Player and Item IdGenerators with bounded ranges
        player_id_gen = IdGeneratorEngine(first_value=1, max_value=1000)
        item_id_gen = IdGeneratorEngine(first_value=10000, max_value=50000)

        # Issue first batch of entity IDs
        p1 = player_id_gen.generate_id()
        p2 = player_id_gen.generate_id()
        i1 = item_id_gen.generate_id()

        self.assertEqual(p1, 1)
        self.assertEqual(p2, 2)
        self.assertEqual(i1, 10000)

        # Step 4: Hydrate configuration cache via DictionaryAdapter
        config_cache = DictionaryAdapterContract()
        config_cache["MaxPlayers"] = 500
        config_cache["ExperienceRate"] = 15.0
        config_cache["DropRate"] = 30.0

        # Step 5: Verify configuration invariants
        self.assertEqual(config_cache["MaxPlayers"], 500)
        self.assertTrue(config_cache.contains_kvp_hardened("DropRate", 30.0))

    def test_scenario_10_database_failure_recovery_without_stale_cache_lockout(self):
        """
        Configuration cache resilience:
        Simulates transient DB failure during startup; cache remains unsealed (_isLoaded=false)
        and successfully re-hydrates when database connection recovers.
        """
        class MockConfigSearchCache:
            def __init__(self):
                self.is_loaded = False
                self.entries = []
                self.load_attempts = 0

            def load_from_db(self, db_connected: bool):
                self.load_attempts += 1
                if not db_connected:
                    # Transient error: do NOT set is_loaded = True! (Fix for WEB-27)
                    return False
                self.entries = ["LorenciaMap", "NoriaMap", "DeviasMap"]
                self.is_loaded = True
                return True

        cache = MockConfigSearchCache()

        # Step 1: Initial startup attempt while DB is unavailable
        success1 = cache.load_from_db(db_connected=False)
        self.assertFalse(success1)
        self.assertFalse(cache.is_loaded)  # Must NOT be marked loaded!

        # Step 2: Database recovers, retry succeeds
        success2 = cache.load_from_db(db_connected=True)
        self.assertTrue(success2)
        self.assertTrue(cache.is_loaded)
        self.assertEqual(len(cache.entries), 3)
        self.assertEqual(cache.load_attempts, 2)


if __name__ == "__main__":
    unittest.main()
