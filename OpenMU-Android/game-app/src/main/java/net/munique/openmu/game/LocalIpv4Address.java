package net.munique.openmu.game;

final class LocalIpv4Address {
    private LocalIpv4Address() {
    }

    static boolean isTrusted(String value) {
        if (value == null || !value.equals(value.trim())) {
            return false;
        }
        String[] parts = value.split("\\.", -1);
        if (parts.length != 4) {
            return false;
        }
        int[] octets = new int[4];
        for (int index = 0; index < parts.length; index++) {
            if (parts[index].isEmpty() || (parts[index].length() > 1 && parts[index].charAt(0) == '0')) {
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
            if (octets[index] > 255) {
                return false;
            }
        }
        return octets[0] == 10
            || octets[0] == 127
            || (octets[0] == 172 && octets[1] >= 16 && octets[1] <= 31)
            || (octets[0] == 192 && octets[1] == 168)
            || (octets[0] == 169 && octets[1] == 254);
    }
}
