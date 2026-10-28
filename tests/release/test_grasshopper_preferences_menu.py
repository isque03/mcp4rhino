"""Cross-file contract: the shipped Rhino menu invokes the implemented preferences command."""
from pathlib import Path
import unittest
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[2]


class GrasshopperPreferencesMenuTests(unittest.TestCase):
    def test_plugin_preferences_menu_resolves_to_registered_command(self):
        root = ET.parse(ROOT / 'src/MCP4Rhino/MCP4Rhino.rui').getroot()
        items = [item for item in root.findall('.//menu_item')
                 if item.findtext('text/locale_1033') == 'Grasshopper plugins…']
        self.assertEqual(len(items), 1)
        macro_id = items[0].findtext('macro_id')
        macro = next(m for m in root.findall('macros/macro_item') if m.attrib['guid'] == macro_id)
        self.assertEqual(macro.findtext('script'), '! _MCP4RhinoGrasshopper')
        source = (ROOT / 'src/MCP4Rhino/Commands/MCP4RhinoGrasshopperCommand.cs').read_text()
        self.assertIn('EnglishName => "MCP4RhinoGrasshopper"', source)
        self.assertIn('ShowPreferences()', source)


if __name__ == '__main__':
    unittest.main()
