package net.munique.openmu.game;

final class MobileConnectionPolicy {
    private MobileConnectionPolicy() {
    }

    static boolean isValidServer(String value, boolean localPairing) {
        if (localPairing) {
            return LocalIpv4Address.isTrusted(value);
        }
        if (value == null || value.isEmpty() || !value.equals(value.trim()) || value.length() > 253) {
            return false;
        }
        if (value.matches("[0-9.]+")) {
            return isUnicastIpv4(value);
        }
        String[] labels = value.split("\\.", -1);
        for (String label : labels) {
            if (label.isEmpty() || label.length() > 63
                || !label.matches("[A-Za-z0-9](?:[A-Za-z0-9-]*[A-Za-z0-9])?")) {
                return false;
            }
        }
        return true;
    }

    static MobileIdentity.Credentials localCredentials(String server, boolean localPairing,
                                                       String packageKey) {
        // A saved local identity must never be sent to a public server.
        return localPairing && LocalIpv4Address.isTrusted(server)
            ? MobileIdentity.derive(packageKey) : null;
    }

    private static boolean isUnicastIpv4(String value) {
        String[] parts = value.split("\\.", -1);
        if (parts.length != 4) {
            return false;
        }
        int[] octets = new int[4];
        for (int index = 0; index < parts.length; index++) {
            if (parts[index].isEmpty() || parts[index].length() > 3
                || (parts[index].length() > 1 && parts[index].charAt(0) == '0')) {
                return false;
            }
            octets[index] = Integer.parseInt(parts[index]);
            if (octets[index] > 255) {
                return false;
            }
        }
        return octets[0] > 0 && octets[0] < 224;
    }
}
