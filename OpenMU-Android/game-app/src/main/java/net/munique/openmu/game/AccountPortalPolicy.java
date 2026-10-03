package net.munique.openmu.game;

import java.net.URI;
import java.net.URISyntaxException;
import java.util.Locale;

/** Pure URL policy: no DNS lookup, account credentials, or Android dependencies. */
final class AccountPortalPolicy {
    static final String[] LOCALES = {
        "en", "zh-CN", "zh-TW", "ja", "ko", "de", "es", "fr", "pt", "ru", "uk", "pl", "id", "vi", "tl"
    };
    static final String[] LOCALE_LABELS = {
        "English", "简体中文", "繁體中文", "日本語", "한국어", "Deutsch", "Español", "Français",
        "Português", "Русский", "Українська", "Polski", "Bahasa Indonesia", "Tiếng Việt", "Filipino"
    };
    private static final String LOCAL_PAGE = "/_content/MUnique.OpenMU.Web.AdminPanel/player-portal/index.html";

    private AccountPortalPolicy() {
    }

    static String canonicalBase(String value, boolean localPairing, String gameServer) {
        if (value == null || value.isEmpty() || value.length() > 300) {
            throw new IllegalArgumentException("Invalid account portal origin");
        }
        for (int index = 0; index < value.length(); index++) {
            char character = value.charAt(index);
            if (character <= 0x20 || character >= 0x7f || character == '%' || character == '\\') {
                throw new IllegalArgumentException("Invalid account portal origin");
            }
        }
        try {
            URI uri = new URI(value).parseServerAuthority();
            String scheme = uri.getScheme() == null ? "" : uri.getScheme().toLowerCase(Locale.ROOT);
            String host = uri.getHost() == null ? "" : uri.getHost().toLowerCase(Locale.ROOT);
            int port = uri.getPort();
            String path = uri.getRawPath();
            if (!("https".equals(scheme) || "http".equals(scheme)) || host.isEmpty()
                || uri.getRawUserInfo() != null || uri.getRawQuery() != null || uri.getRawFragment() != null
                || !(path == null || path.isEmpty() || "/".equals(path)) || port == 0 || port > 65535
                || java.util.Objects.toString(uri.getRawAuthority(), "").endsWith(":")) {
                throw new IllegalArgumentException("Invalid account portal origin");
            }
            boolean ipv6 = host.startsWith("[") && host.endsWith("]") && host.indexOf(':') >= 0;
            if (!ipv6 && (!MobileConnectionPolicy.isValidServer(host, false) || host.matches("0x[0-9a-f]+"))) {
                throw new IllegalArgumentException("Invalid account portal host");
            }
            boolean loopback = "[::1]".equals(host)
                || (host.startsWith("127.") && LocalIpv4Address.isTrusted(host));
            boolean pairedPrivate = localPairing && LocalIpv4Address.isTrusted(gameServer)
                && LocalIpv4Address.isTrusted(host);
            if ("http".equals(scheme) && !loopback && !pairedPrivate) {
                throw new IllegalArgumentException("Insecure account portal origin");
            }
            int defaultPort = "https".equals(scheme) ? 443 : 80;
            return scheme + "://" + host + (port < 0 || port == defaultPort ? "" : ":" + port);
        } catch (URISyntaxException error) {
            throw new IllegalArgumentException("Invalid account portal origin");
        }
    }

    static String actionUrl(String base, String action, String locale, boolean localPairing, String gameServer) {
        if (!("register".equals(action) || "change-password".equals(action) || "reset-password".equals(action))) {
            throw new IllegalArgumentException("Unknown account portal action");
        }
        String origin = canonicalBase(base, localPairing, gameServer);
        String culture = normalizeLocale(locale);
        return origin.startsWith("http://")
            ? origin + LOCAL_PAGE + "?view=" + action + "&culture=" + culture
            : origin + "/" + action + "?culture=" + culture;
    }

    static String normalizeLocale(String value) {
        if (value == null) {
            return "en";
        }
        String tag = value.replace('_', '-').toLowerCase(Locale.ROOT);
        if ("chs".equals(tag)) return "zh-CN";
        if ("cht".equals(tag)) return "zh-TW";
        if (tag.equals("zh") || tag.startsWith("zh-")) {
            return tag.contains("hant") || tag.endsWith("-tw") || tag.endsWith("-hk") || tag.endsWith("-mo")
                ? "zh-TW" : "zh-CN";
        }
        for (String supported : LOCALES) {
            if (supported.equalsIgnoreCase(tag)) return supported;
        }
        String language = tag.split("-", -1)[0];
        if ("in".equals(language)) return "id";
        if ("fil".equals(language)) return "tl";
        for (String supported : LOCALES) {
            if (supported.equals(language)) return supported;
        }
        return "en";
    }

    static String localeFromIni(String source, String deviceLocale) {
        String section = "";
        for (String line : source.split("\r?\n")) {
            String trimmed = line.trim();
            if (trimmed.startsWith("[") && trimmed.endsWith("]")) {
                section = trimmed.substring(1, trimmed.length() - 1).trim();
            } else if ("UI".equalsIgnoreCase(section)) {
                int separator = trimmed.indexOf('=');
                if (separator > 0 && "Locale".equalsIgnoreCase(trimmed.substring(0, separator).trim())) {
                    return normalizeLocale(trimmed.substring(separator + 1).trim());
                }
            }
        }
        return normalizeLocale(deviceLocale);
    }
}
