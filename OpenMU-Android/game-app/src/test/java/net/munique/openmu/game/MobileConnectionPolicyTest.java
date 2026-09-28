package net.munique.openmu.game;

import org.junit.Test;

import static org.junit.Assert.assertFalse;
import static org.junit.Assert.assertNotNull;
import static org.junit.Assert.assertNull;
import static org.junit.Assert.assertTrue;

public final class MobileConnectionPolicyTest {
    @Test
    public void acceptsCloudAddressesOnlyForNormalLogin() {
        assertTrue(MobileConnectionPolicy.isValidServer("8.8.8.8", false));
        assertTrue(MobileConnectionPolicy.isValidServer("game.example.com", false));
        assertTrue(MobileConnectionPolicy.isValidServer("mu-1.example.com", false));
        assertFalse(MobileConnectionPolicy.isValidServer("8.8.8.8", true));
        assertFalse(MobileConnectionPolicy.isValidServer("game.example.com", true));
        assertTrue(MobileConnectionPolicy.isValidServer("10.0.2.2", true));
    }

    @Test
    public void rejectsUrlsMalformedHostsAndIniInjection() {
        String[] invalid = { "", " 10.0.2.2", "game.example.com\nPort=44405", "https://mu.example.com",
            "mu.example.com:44406", "-mu.example.com", "mu-.example.com", "mu..example.com",
            "192.168.001.1", "256.1.2.3", "0.0.0.0", "224.0.0.1", "255.255.255.255", "::1",
            "a".repeat(64) + ".com" };
        for (String address : invalid) {
            assertFalse(address, MobileConnectionPolicy.isValidServer(address, false));
        }
        assertFalse(MobileConnectionPolicy.isValidServer(null, false));
    }

    @Test
    public void neverDerivesLocalCredentialsForCloudOrManualLogin() {
        assertNull(MobileConnectionPolicy.localCredentials("8.8.8.8", true, "invalid"));
        assertNull(MobileConnectionPolicy.localCredentials("game.example.com", true, "invalid"));
        assertNull(MobileConnectionPolicy.localCredentials("10.0.2.2", false, "invalid"));
        assertNotNull(MobileConnectionPolicy.localCredentials("10.0.2.2", true, "A".repeat(43)));
    }
}
