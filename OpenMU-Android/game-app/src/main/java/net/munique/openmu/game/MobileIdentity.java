package net.munique.openmu.game;

import java.nio.charset.StandardCharsets;
import java.security.GeneralSecurityException;
import java.util.Base64;

import javax.crypto.Mac;
import javax.crypto.spec.SecretKeySpec;

final class MobileIdentity {
    private static final String LOGIN_NAME_CONTEXT = "OpenMU-Mobile.LoginName.v1";
    private static final String LOGIN_PASSWORD_CONTEXT = "OpenMU-Mobile.LoginPassword.v1";

    private MobileIdentity() {
    }

    static Credentials derive(String packageKey) {
        if (packageKey == null || !packageKey.matches("[A-Za-z0-9_-]{43}")) {
            throw new IllegalArgumentException("Invalid mobile package key");
        }

        byte[] key = packageKey.getBytes(StandardCharsets.UTF_8);
        String usernameHex = toUpperHex(hmacSha256(key, LOGIN_NAME_CONTEXT));
        String passwordBase64 = Base64.getUrlEncoder().withoutPadding()
            .encodeToString(hmacSha256(key, LOGIN_PASSWORD_CONTEXT));
        return new Credentials("mob" + usernameHex.substring(0, 7), passwordBase64.substring(0, 20));
    }

    private static byte[] hmacSha256(byte[] key, String message) {
        try {
            Mac mac = Mac.getInstance("HmacSHA256");
            mac.init(new SecretKeySpec(key, "HmacSHA256"));
            return mac.doFinal(message.getBytes(StandardCharsets.UTF_8));
        } catch (GeneralSecurityException error) {
            throw new IllegalStateException("HmacSHA256 is unavailable", error);
        }
    }

    private static String toUpperHex(byte[] bytes) {
        char[] result = new char[bytes.length * 2];
        char[] digits = "0123456789ABCDEF".toCharArray();
        for (int index = 0; index < bytes.length; index++) {
            int value = bytes[index] & 0xff;
            result[index * 2] = digits[value >>> 4];
            result[index * 2 + 1] = digits[value & 0x0f];
        }
        return new String(result);
    }

    static final class Credentials {
        private final String username;
        private final String password;

        Credentials(String username, String password) {
            this.username = username;
            this.password = password;
        }

        String username() {
            return username;
        }

        String password() {
            return password;
        }
    }
}
