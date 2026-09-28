package net.munique.openmu.game;

import org.junit.Test;

import static org.junit.Assert.assertEquals;
import static org.junit.Assert.fail;

public final class AccountPortalPolicyTest {
    @Test
    public void acceptsExplicitHttpsOriginsWithoutPairing() {
        assertEquals("https://accounts.example.com",
            AccountPortalPolicy.canonicalBase("HTTPS://ACCOUNTS.example.com:443/", false, "8.8.8.8"));
        assertEquals("https://accounts.example.com:8443",
            AccountPortalPolicy.canonicalBase("https://accounts.example.com:8443", false, ""));
        assertEquals("https://8.8.8.8", AccountPortalPolicy.canonicalBase("https://8.8.8.8", false, ""));
        assertEquals("https://192.168.1.3", AccountPortalPolicy.canonicalBase("https://192.168.1.3", false, ""));
        assertEquals("https://[2001:db8::1]:8443",
            AccountPortalPolicy.canonicalBase("https://[2001:db8::1]:8443", false, ""));
        assertEquals("https://localhost", AccountPortalPolicy.canonicalBase("https://localhost", false, ""));
    }

    @Test
    public void permitsDirectLoopbackHttpWithoutPairingButNeverLocalhostDns() {
        assertEquals("http://127.0.0.1", AccountPortalPolicy.canonicalBase("http://127.0.0.1:80/", false, ""));
        assertEquals("http://127.0.0.2:5080",
            AccountPortalPolicy.canonicalBase("http://127.0.0.2:5080", false, "8.8.8.8"));
        assertEquals("http://[::1]:5080", AccountPortalPolicy.canonicalBase("http://[::1]:5080", false, ""));
        rejects("http://localhost:5080", true, "10.0.2.2");
        rejects("http://localhost.", false, "");
        rejects("http://[::ffff:127.0.0.1]", false, "");
    }

    @Test
    public void requiresTrustedPairingForPrivateHttpEvenWhenStoredSettingsChange() {
        String[] trusted = { "10.0.2.2", "192.168.1.3", "172.16.1.2", "172.31.255.254", "169.254.1.2" };
        for (String host : trusted) {
            assertEquals("http://" + host + ":5080",
                AccountPortalPolicy.canonicalBase("http://" + host + ":5080", true, "10.0.2.2"));
            rejects("http://" + host + ":5080", false, "10.0.2.2");
            rejects("http://" + host + ":5080", true, "game.example.com");
            rejects("http://" + host + ":5080", true, "8.8.8.8");
        }
        String[] publicHosts = { "8.8.8.8", "game.example.com", "172.15.1.2", "172.32.1.2", "192.169.1.2" };
        for (String host : publicHosts) {
            rejects("http://" + host, false, "10.0.2.2");
            rejects("http://" + host, true, "10.0.2.2");
        }
    }

    @Test
    public void rejectsCredentialsControlsPathsQueriesFragmentsAndNonWebSchemes() {
        String[] invalid = { null, "", "https://user:secret@accounts.example.com", "https://@accounts.example.com",
            "https://accounts.example.com/username", "https://accounts.example.com//", "https://accounts.example.com/..",
            "https://accounts.example.com?", "https://accounts.example.com?password=secret",
            "https://accounts.example.com#", "https://accounts.example.com#recovery=secret",
            "https://accounts.example.com%2f@evil.example.com", "https://accounts.example.com/%0a",
            "https://accounts.example.com\\@evil.example.com", "https://accounts.example.com\n",
            "https://accounts.example.com\r\n", "https://accounts.example.com\t", "https://accounts.example.com\u0000",
            " https://accounts.example.com", "https://accounts.example.com ", "https://账户.example.com",
            "https://accounts.example.com/é", "intent://accounts.example.com", "file:///register",
            "content://accounts.example.com", "javascript:alert(1)", "//accounts.example.com", "accounts.example.com",
            "https:accounts.example.com", "https:////accounts.example.com", "https:///accounts.example.com" };
        for (String value : invalid) rejects(value, true, "10.0.2.2");
        rejects("https://" + "a".repeat(301), true, "10.0.2.2");
    }

    @Test
    public void rejectsMalformedPortsHostsAndIpAliases() {
        String[] invalid = { "https://accounts.example.com:0", "https://accounts.example.com:65536",
            "https://accounts.example.com:-1", "https://accounts.example.com:abc", "https://accounts.example.com:",
            "https://accounts.example.com:2147483648", "https://-accounts.example.com", "https://accounts-.example.com",
            "https://accounts..example.com", "https://accounts.example.com.", "https://192.168.001.1",
            "https://256.1.1.1", "https://224.0.0.1", "https://0.0.0.0", "https://127.1",
            "https://2130706433", "https://0x7f000001", "https://[::1", "https://::1", "https://[garbage]", "https://[fe80::1%25wlan0]",
            "http://0x7f000001", "http://0177.0.0.1", "http://0x7f.0.0.1", "http://[fc00::1]" };
        for (String value : invalid) rejects(value, true, "10.0.2.2");
        rejects("https://" + "a".repeat(64) + ".example.com", false, "");
        assertEquals("https://accounts.example.com:65535",
            AccountPortalPolicy.canonicalBase("https://accounts.example.com:65535", false, ""));
    }

    @Test
    public void cloudActionsUseTheIndependentHttpsPortalAndSelectedLocale() {
        for (String action : new String[] { "register", "change-password", "reset-password" }) {
            for (String locale : AccountPortalPolicy.LOCALES) {
                assertEquals("https://accounts.example.com/" + action + "?culture=" + locale,
                    AccountPortalPolicy.actionUrl("https://accounts.example.com", action, locale, false, "8.8.8.8"));
            }
        }
    }

    @Test
    public void localActionsUseStaticPortalRatherThanTheAdminBlazorRoutes() {
        for (String action : new String[] { "register", "change-password", "reset-password" }) {
            assertEquals("http://10.0.2.2:5080/_content/MUnique.OpenMU.Web.AdminPanel/player-portal/index.html?view="
                + action + "&culture=zh-TW",
                AccountPortalPolicy.actionUrl("http://10.0.2.2:5080/", action, "zh-Hant-HK", true, "10.0.2.2"));
            assertEquals("http://127.0.0.1/_content/MUnique.OpenMU.Web.AdminPanel/player-portal/index.html?view="
                + action + "&culture=en",
                AccountPortalPolicy.actionUrl("http://127.0.0.1", action, "en", false, ""));
        }
    }

    @Test
    public void onlyKnownActionsAndWhitelistedCultureCanEnterBrowserUrls() {
        for (String action : new String[] { null, "", "register?password=secret", "../admin", "file://reset", "REGISTER" }) {
            try {
                AccountPortalPolicy.actionUrl("https://accounts.example.com", action, "en", false, "");
                fail("Invalid action accepted");
            } catch (IllegalArgumentException expected) { }
        }
        assertEquals("https://accounts.example.com/register?culture=en",
                AccountPortalPolicy.actionUrl("https://accounts.example.com", "register", "en&password=secret", false, ""));
    }

    @Test
    public void noPortalIsDerivedFromTheGameAddressWhenTheExplicitOriginIsMissing() {
        for (String origin : new String[] { "", null }) {
            try {
                AccountPortalPolicy.actionUrl(origin, "register", "zh-CN", true, "10.0.2.2");
                fail("Missing portal origin accepted");
            } catch (IllegalArgumentException expected) { }
        }
    }

    @Test
    public void revalidatesPrivateHttpOnEveryActionAfterPairingOrGameAddressChanges() {
        for (String action : new String[] { "register", "change-password", "reset-password" }) {
            for (String server : new String[] { "10.0.2.2", "8.8.8.8", "game.example.com" }) {
                try {
                    AccountPortalPolicy.actionUrl("http://10.0.2.2:5080", action, "zh-CN", false, server);
                    fail("Unpaired HTTP origin accepted");
                } catch (IllegalArgumentException expected) { }
            }
            try {
                AccountPortalPolicy.actionUrl("http://10.0.2.2:5080", action, "zh-CN", true, "8.8.8.8");
                fail("Cloud game mode accepted private HTTP origin");
            } catch (IllegalArgumentException expected) { }
        }
    }

    @Test
    public void mapsGameAndDeviceLocaleAliasesToTheFifteenPortalCultures() {
        String[][] aliases = { { "CHS", "zh-CN" }, { "cht", "zh-TW" }, { "zh", "zh-CN" },
            { "zh-Hans-SG", "zh-CN" }, { "zh_Hant", "zh-TW" }, { "zh-HK", "zh-TW" }, { "zh-MO", "zh-TW" },
            { "pt-BR", "pt" }, { "de-DE", "de" }, { "en-US", "en" }, { "id-ID", "id" },
            { "in", "id" }, { "in-ID", "id" }, { "in_ID", "id" },
            { "fil-PH", "tl" }, { "tl-PH", "tl" }, { "unsupported", "en" }, { "", "en" }, { null, "en" } };
        for (String[] alias : aliases) assertEquals(alias[1], AccountPortalPolicy.normalizeLocale(alias[0]));
        for (String locale : AccountPortalPolicy.LOCALES) assertEquals(locale, AccountPortalPolicy.normalizeLocale(locale));
        assertEquals(15, AccountPortalPolicy.LOCALES.length);
        assertEquals(AccountPortalPolicy.LOCALES.length, AccountPortalPolicy.LOCALE_LABELS.length);
    }

    @Test
    public void readsOnlyTheUiLocaleFromTheExistingIniAndOtherwiseUsesDeviceLocale() {
        assertEquals("zh-TW", AccountPortalPolicy.localeFromIni(
            "[LOGIN]\nLanguage=Chs\nUserName=not-a-locale\n[UI]\nLocale = zh-TW\n", "en-US"));
        assertEquals("fr", AccountPortalPolicy.localeFromIni("[UI]\r\nlocale=fr-FR\r\n", "en-US"));
        assertEquals("de", AccountPortalPolicy.localeFromIni("[LOGIN]\nLanguage=Chs\n", "de-DE"));
        assertEquals("en", AccountPortalPolicy.localeFromIni("[UI]\nLocale=not-supported\n", "de-DE"));
    }

    private static void rejects(String value, boolean pairing, String gameServer) {
        try {
            AccountPortalPolicy.canonicalBase(value, pairing, gameServer);
            fail("Invalid portal origin accepted");
        } catch (IllegalArgumentException expected) { }
    }
}
