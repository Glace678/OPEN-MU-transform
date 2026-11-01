# tests/e2e/tier2_boundaries/test_b36_packet_definitions_xml_truncation_boundaries.py
import unittest
import tempfile
import os
from tests.e2e.harness.regression_models import PacketDefinitionsSaveEngine


class TestB36PacketDefinitionsXmlTruncationBoundaries(unittest.TestCase):
    """
    Feature 36 Boundaries: PacketDefinitions XML Save Truncation.
    Tests boundary conditions:
    - Overwriting large XML with empty string (0-byte file).
    - Overwriting with identical length content.
    - Overwriting with single byte content.
    - Multi-byte UTF-8 CJK text content (Chinese character serialization).
    - XML entities, CDATA sections, and special character preservation.
    """

    def setUp(self):
        self.temp_dir = tempfile.TemporaryDirectory()
        self.file_path = os.path.join(self.temp_dir.name, "DefinitionsBoundary.xml")

    def tearDown(self):
        self.temp_dir.cleanup()

    def test_zero_length_new_xml_file_truncates_to_zero(self):
        """Overwriting 2000-byte file with empty string results in a 0-byte file."""
        initial_content = "<Definitions>" + "A" * 2000 + "</Definitions>"
        PacketDefinitionsSaveEngine.save_hardened(self.file_path, initial_content)
        self.assertGreater(os.path.getsize(self.file_path), 2000)

        PacketDefinitionsSaveEngine.save_hardened(self.file_path, "")
        self.assertEqual(os.path.getsize(self.file_path), 0)

    def test_identical_length_overwrite(self):
        """Overwriting with exact same length updates content without discrepancy."""
        xml1 = "<Definitions><Packets Length='10' /></Definitions>"
        xml2 = "<Definitions><Packets Length='20' /></Definitions>"
        self.assertEqual(len(xml1), len(xml2))

        PacketDefinitionsSaveEngine.save_hardened(self.file_path, xml1)
        PacketDefinitionsSaveEngine.save_hardened(self.file_path, xml2)

        self.assertEqual(os.path.getsize(self.file_path), len(xml2.encode("utf-8")))
        valid, _ = PacketDefinitionsSaveEngine.verify_xml_integrity(self.file_path)
        self.assertTrue(valid)

    def test_single_byte_new_xml_file_truncates_cleanly(self):
        """Overwriting 4096-byte file with a single character truncates to 1 byte."""
        large_xml = "<Definitions>" + "X" * 4000 + "</Definitions>"
        PacketDefinitionsSaveEngine.save_hardened(self.file_path, large_xml)

        PacketDefinitionsSaveEngine.save_hardened(self.file_path, "A")
        self.assertEqual(os.path.getsize(self.file_path), 1)

    def test_unicode_multibyte_xml_truncation(self):
        """Multi-byte UTF-8 Chinese characters overwrite longer Latin XML cleanly."""
        long_latin = "<Defs>" + "PacketDefinitionLongEntry" * 20 + "</Defs>"
        PacketDefinitionsSaveEngine.save_hardened(self.file_path, long_latin)

        cjk_xml = (
            '<?xml version="1.0" encoding="utf-8"?>\n'
            '<PacketDefinitions>\n'
            '  <Packet Name="雷神之剑掉落" HeaderType="C1" Length="6">\n'
            '    <Description>玛雅武器与祝福宝石掉落协议</Description>\n'
            '  </Packet>\n'
            '</PacketDefinitions>\n'
        )
        PacketDefinitionsSaveEngine.save_hardened(self.file_path, cjk_xml)

        self.assertEqual(os.path.getsize(self.file_path), len(cjk_xml.encode("utf-8")))
        valid, err = PacketDefinitionsSaveEngine.verify_xml_integrity(self.file_path)
        self.assertTrue(valid, f"CJK XML parse error: {err}")

    def test_special_xml_entities_and_cdata(self):
        """XML with escaped entities (&lt;, &gt;, &amp;) and CDATA parses without truncation flaw."""
        initial = "<Root>" + "old" * 100 + "</Root>"
        PacketDefinitionsSaveEngine.save_hardened(self.file_path, initial)

        special_xml = (
            '<?xml version="1.0" encoding="utf-8"?>\n'
            '<PacketDefinitions>\n'
            '  <Packet Name="FilterPacket" Caption="&lt;Safe&amp;Clean&gt;">\n'
            '    <Regex><![CDATA[[A-Za-z0-9_]+]]></Regex>\n'
            '  </Packet>\n'
            '</PacketDefinitions>\n'
        )
        PacketDefinitionsSaveEngine.save_hardened(self.file_path, special_xml)

        valid, err = PacketDefinitionsSaveEngine.verify_xml_integrity(self.file_path)
        self.assertTrue(valid, f"Special entities parse error: {err}")


if __name__ == "__main__":
    unittest.main()
