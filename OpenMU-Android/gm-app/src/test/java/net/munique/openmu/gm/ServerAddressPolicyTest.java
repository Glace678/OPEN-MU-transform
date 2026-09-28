package net.munique.openmu.gm;

import org.junit.Test;

import static org.junit.Assert.assertFalse;
import static org.junit.Assert.assertTrue;

public final class ServerAddressPolicyTest {
    @Test
    public void permitsLocalHttpAddresses() {
        assertTrue(ServerAddressPolicy.isAllowed("http://127.0.0.1:5080"));
        assertTrue(ServerAddressPolicy.isAllowed("http://10.0.2.2:5080"));
        assertTrue(ServerAddressPolicy.isAllowed("http://172.16.4.5:5080"));
        assertTrue(ServerAddressPolicy.isAllowed("http://192.168.215.56:5080"));
        assertTrue(ServerAddressPolicy.isAllowed("http://169.254.10.20:5080"));
        assertTrue(ServerAddressPolicy.isAllowed("http://[::1]:5080"));
        assertTrue(ServerAddressPolicy.isAllowed("http://[fd12::20]:5080"));
        assertTrue(ServerAddressPolicy.isAllowed("http://[fe80::20]:5080"));
    }

    @Test
    public void rejectsCleartextPublicOrNamedHosts() {
        assertFalse(ServerAddressPolicy.isAllowed("http://8.8.8.8:5080"));
        assertFalse(ServerAddressPolicy.isAllowed("http://example.com:5080"));
        assertFalse(ServerAddressPolicy.isAllowed("http://192.168.\u0661.2:5080"));
        assertFalse(ServerAddressPolicy.isAllowed("http://user@192.168.1.2:5080"));
        assertFalse(ServerAddressPolicy.isAllowed("ftp://192.168.1.2:5080"));
    }

    @Test
    public void permitsExplicitHttpsEndpoint() {
        assertTrue(ServerAddressPolicy.isAllowed("https://gm.example.com"));
    }
}
