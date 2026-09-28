package net.munique.openmu.game;

import org.junit.Test;

import java.util.LinkedHashMap;
import java.util.Map;

import static org.junit.Assert.assertEquals;
import static org.junit.Assert.assertTrue;

public final class MobilePreferencesTest {
    @Test
    public void displayChangesPreservePlayerLocaleAndLoginSettings() {
        String source = "[UI]\r\nLocale=ja\r\nFont=Default\r\nScalePercent=100\r\n"
            + "[LOGIN]\r\nLanguage=Jpn\r\nRememberMe=1\r\n";
        Map<String, LinkedHashMap<String, String>> updates = new LinkedHashMap<>();
        LinkedHashMap<String, String> ui = new LinkedHashMap<>();
        ui.put("ScalePercent", "110");
        ui.put("SafeMarginPercent", "3");
        updates.put("UI", ui);

        String merged = MobilePreferences.mergeIni(source, updates);

        assertTrue(merged.contains("Locale=ja\n"));
        assertTrue(merged.contains("Font=Default\n"));
        assertTrue(merged.contains("Language=Jpn\n"));
        assertTrue(merged.contains("RememberMe=1\n"));
        assertTrue(merged.contains("ScalePercent=110\n"));
        assertTrue(merged.contains("SafeMarginPercent=3\n"));
        assertEquals(merged, MobilePreferences.mergeIni(merged, updates));
    }

    @Test
    public void repeatedSavesDoNotGrowTrailingBlankLines() {
        Map<String, LinkedHashMap<String, String>> updates = new LinkedHashMap<>();
        for (String ending : new String[] { "", "\n", "\n\n", "\r\n\r\n" }) {
            String once = MobilePreferences.mergeIni("[UI]\nLocale=ja" + ending, updates);
            String current = once;
            for (int save = 0; save < 20; save++) {
                current = MobilePreferences.mergeIni(current, updates);
            }
            assertEquals(once, current);
        }
    }
}
