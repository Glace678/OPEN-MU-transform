package net.munique.openmu.game;

import org.junit.Test;

import static org.junit.Assert.assertEquals;
import static org.junit.Assert.assertThrows;

public final class MobileIdentityTest {
    @Test
    public void derivesExpectedCredentialsFromUtf8PackageKey() {
        MobileIdentity.Credentials credentials = MobileIdentity.derive("A".repeat(43));

        assertEquals("mobB92499A", credentials.username());
        assertEquals("bjFzBKJhNZcNXgbgkx5i", credentials.password());
    }

    @Test
    public void rejectsMalformedPackageKey() {
        assertThrows(IllegalArgumentException.class,
            () -> MobileIdentity.derive("A".repeat(42)));
        assertThrows(IllegalArgumentException.class,
            () -> MobileIdentity.derive("A".repeat(42) + "+"));
    }

    @Test
    public void acceptsOnlyTrustedIpv4GameServers() {
        org.junit.Assert.assertTrue(LocalIpv4Address.isTrusted("127.0.0.1"));
        org.junit.Assert.assertTrue(LocalIpv4Address.isTrusted("10.0.2.2"));
        org.junit.Assert.assertTrue(LocalIpv4Address.isTrusted("172.31.255.255"));
        org.junit.Assert.assertTrue(LocalIpv4Address.isTrusted("192.168.215.56"));
        org.junit.Assert.assertTrue(LocalIpv4Address.isTrusted("169.254.4.8"));
        org.junit.Assert.assertFalse(LocalIpv4Address.isTrusted("8.8.8.8"));
        org.junit.Assert.assertFalse(LocalIpv4Address.isTrusted("example.com"));
        org.junit.Assert.assertFalse(LocalIpv4Address.isTrusted("192.168.001.2"));
        org.junit.Assert.assertFalse(LocalIpv4Address.isTrusted("192.168.\u0661.2"));
        org.junit.Assert.assertFalse(LocalIpv4Address.isTrusted("::1"));
    }
}
