# tests/e2e/tier1_features/test_f36_packet_definitions_xml_truncation.py
import unittest
import tempfile
import os
from tests.e2e.harness.regression_models import PacketDefinitionsSaveEngine


class TestF36PacketDefinitionsXmlTruncation(unittest.TestCase):
    """
    Feature 36: PacketDefinitions XML Save Truncation (NET-04).
    Confirms that saving PacketDefinitions XML truncates existing files (FileMode.Create)
    so that overwriting longer files with shorter content leaves zero stale trailing bytes.
    """

    def setUp(self):
        self.temp_dir = tempfile.TemporaryDirectory()
        self.file_path = os.path.join(self.temp_dir.name, "PacketDefinitions.xml")

        self.long_xml = (
            '<?xml version="1.0" encoding="utf-8"?>\n'
            '<PacketDefinitions xmlns="http://www.munique.net/OpenMU/PacketDefinitions">\n'
            '  <Packets>\n'
            '    <Packet Name="LegacyLongPacketStructure" HeaderType="C1" Length="120">\n'
            '      <Field Index="0" Type="Byte" Name="SubCode" />\n'
            '      <Field Index="1" Type="String" Length="100" Name="VeryLongTrailingDataPayload" />\n'
            '    </Packet>\n'
            '  </Packets>\n'
            '</PacketDefinitions>\n'
        )

        self.short_xml = (
            '<?xml version="1.0" encoding="utf-8"?>\n'
            '<PacketDefinitions xmlns="http://www.munique.net/OpenMU/PacketDefinitions">\n'
            '  <Packets>\n'
            '    <Packet Name="ShortPacket" HeaderType="C1" Length="4" />\n'
            '  </Packets>\n'
            '</PacketDefinitions>\n'
        )

    def tearDown(self):
        self.temp_dir.cleanup()

    def test_filemode_create_overwrites_longer_file_without_stale_bytes(self):
        """Writing shorter XML over longer initial file truncates file completely."""
        # 1. First write longer XML
        PacketDefinitionsSaveEngine.save_hardened(self.file_path, self.long_xml)
        long_size = os.path.getsize(self.file_path)

        # 2. Overwrite with shorter XML
        new_size = PacketDefinitionsSaveEngine.save_hardened(self.file_path, self.short_xml)
        self.assertLess(new_size, long_size)
        self.assertEqual(os.path.getsize(self.file_path), len(self.short_xml.encode("utf-8")))

    def test_truncated_xml_is_well_formed(self):
        """Parsed XML after truncation is 100% valid with no syntax error."""
        PacketDefinitionsSaveEngine.save_hardened(self.file_path, self.long_xml)
        PacketDefinitionsSaveEngine.save_hardened(self.file_path, self.short_xml)

        valid, err = PacketDefinitionsSaveEngine.verify_xml_integrity(self.file_path)
        self.assertTrue(valid, f"Truncated XML parsing failed: {err}")
        self.assertIsNone(err)

    def test_vulnerable_openwrite_leaves_trailing_stale_bytes(self):
        """Vulnerable OpenWrite does not truncate, preserving old long file size."""
        # 1. Write long XML
        PacketDefinitionsSaveEngine.save_hardened(self.file_path, self.long_xml)
        long_size = os.path.getsize(self.file_path)

        # 2. Overwrite using vulnerable OpenWrite
        vulnerable_size = PacketDefinitionsSaveEngine.save_vulnerable_openwrite(self.file_path, self.short_xml)
        # File size was NOT truncated; remains long_size!
        self.assertEqual(vulnerable_size, long_size)
        self.assertGreater(vulnerable_size, len(self.short_xml.encode("utf-8")))

    def test_vulnerable_file_causes_parse_error(self):
        """Trailing stale bytes cause standard XML parsers to fail."""
        PacketDefinitionsSaveEngine.save_hardened(self.file_path, self.long_xml)
        PacketDefinitionsSaveEngine.save_vulnerable_openwrite(self.file_path, self.short_xml)

        valid, err = PacketDefinitionsSaveEngine.verify_xml_integrity(self.file_path)
        self.assertFalse(valid)
        self.assertIn("XML ParseError", err)

    def test_exact_file_size_equals_serialized_length(self):
        """Asserts exact byte length matches payload byte count."""
        size = PacketDefinitionsSaveEngine.save_hardened(self.file_path, self.short_xml)
        expected_bytes = len(self.short_xml.encode("utf-8"))
        self.assertEqual(size, expected_bytes)


if __name__ == "__main__":
    unittest.main()
