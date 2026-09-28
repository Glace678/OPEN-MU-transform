package net.munique.openmu.gm;

import java.net.URI;
import java.net.URISyntaxException;
import java.util.Locale;

final class ServerAddressPolicy {
    private ServerAddressPolicy() {
    }

    static boolean isAllowed(String value) {
        try {
            URI uri = new URI(value);
            String scheme = uri.getScheme();
            String host = uri.getHost();
            if (scheme == null || host == null || uri.getUserInfo() != null) {
                return false;
            }
            if ("https".equalsIgnoreCase(scheme)) {
                return true;
            }
            return "http".equalsIgnoreCase(scheme) && isLocalAddress(host);
        } catch (URISyntaxException error) {
            return false;
        }
    }

    // Separate from isAllowed(): https is permitted to any host (public
    // deployment is a documented use case), but the pairing key is sent to
    // whatever host the user types, so callers must confirm before letting a
    // non-local host receive it. A mistyped or hostile URL otherwise exfiltrates
    // the key, which is what authenticates every GM write operation.
    static boolean isLocal(String value) {
        try {
            URI uri = new URI(value);
            if (uri.getHost() == null) {
                return false;
            }
            return isLocalAddress(uri.getHost());
        } catch (URISyntaxException error) {
            return false;
        }
    }

    private static boolean isLocalAddress(String rawHost) {
        String host = rawHost.toLowerCase(Locale.ROOT);
        if (host.startsWith("[") && host.endsWith("]")) {
            host = host.substring(1, host.length() - 1);
        }
        int zoneIndex = host.indexOf('%');
        if (zoneIndex >= 0) {
            host = host.substring(0, zoneIndex);
        }
        if ("localhost".equals(host) || "::1".equals(host)) {
            return true;
        }
        if (host.indexOf(':') >= 0) {
            return isPrivateIpv6(host);
        }
        return isPrivateIpv4(host);
    }

    private static boolean isPrivateIpv4(String host) {
        String[] parts = host.split("\\.", -1);
        if (parts.length != 4) {
            return false;
        }
        int[] octets = new int[4];
        for (int index = 0; index < parts.length; index++) {
            if (parts[index].isEmpty()) {
                return false;
            }
            for (int character = 0; character < parts[index].length(); character++) {
                char digit = parts[index].charAt(character);
                if (digit < '0' || digit > '9') {
                    return false;
                }
            }
            try {
                octets[index] = Integer.parseInt(parts[index]);
            } catch (NumberFormatException error) {
                return false;
            }
            if (octets[index] < 0 || octets[index] > 255) {
                return false;
            }
        }
        return octets[0] == 10
            || octets[0] == 127
            || (octets[0] == 172 && octets[1] >= 16 && octets[1] <= 31)
            || (octets[0] == 192 && octets[1] == 168)
            || (octets[0] == 169 && octets[1] == 254);
    }

    private static boolean isPrivateIpv6(String host) {
        String firstPart = host.split(":", 2)[0];
        if (firstPart.isEmpty()) {
            return false;
        }
        try {
            int firstHextet = Integer.parseInt(firstPart, 16);
            return (firstHextet & 0xfe00) == 0xfc00
                || (firstHextet & 0xffc0) == 0xfe80;
        } catch (NumberFormatException error) {
            return false;
        }
    }
}
