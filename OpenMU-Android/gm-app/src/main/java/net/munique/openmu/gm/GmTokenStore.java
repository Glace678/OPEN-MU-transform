package net.munique.openmu.gm;

import android.content.Context;
import android.content.SharedPreferences;

import java.net.URI;
import java.net.URISyntaxException;
import java.util.Locale;

/**
 * F1 (M1-01 / M2-H1): persists the short-lived admin-panel cookie session issued
 * by POST /api/mobile-gm/login. There is no static, APK-distributed bearer any
 * more: the GM app authenticates with the administrator account (password +
 * optional TOTP) and stores only the resulting server-issued session cookie.
 *
 * The cookie is kept in app-private SharedPreferences (readable only by this
 * app's UID on a non-rooted device), the same protection level as every other
 * setting in this app, and bound strictly to the canonical server origin.
 */
final class GmTokenStore {
    private static final String PREFS = "openmu-gm";
    private static final String KEY_SESSION_COOKIE = "session-cookie";
    private static final String KEY_SESSION_ORIGIN = "session-origin";

    private GmTokenStore() {
    }

    static String canonicalOrigin(String serverUrl) {
        if (serverUrl == null || serverUrl.trim().isEmpty()) {
            return "";
        }
        try {
            URI uri = new URI(serverUrl.trim());
            String scheme = uri.getScheme();
            String host = uri.getHost();
            if (scheme == null || host == null) {
                return "";
            }
            String lowerScheme = scheme.toLowerCase(Locale.ROOT);
            String lowerHost = host.toLowerCase(Locale.ROOT);
            int port = uri.getPort();
            int defaultPort = "https".equals(lowerScheme) ? 443 : "http".equals(lowerScheme) ? 80 : -1;
            int effectivePort = port > 0 ? port : defaultPort;
            return lowerScheme + "://" + lowerHost + (effectivePort > 0 ? ":" + effectivePort : "");
        } catch (URISyntaxException error) {
            return "";
        }
    }

    /** Returns the stored admin session cookie for the given server origin, or an empty string. */
    static String getSessionCookie(Context context, String serverUrl) {
        String requestedOrigin = canonicalOrigin(serverUrl);
        if (requestedOrigin.isEmpty()) {
            return "";
        }
        SharedPreferences prefs = context.getSharedPreferences(PREFS, Context.MODE_PRIVATE);
        String storedOrigin = prefs.getString(KEY_SESSION_ORIGIN, "");
        if (!requestedOrigin.equals(storedOrigin)) {
            return "";
        }
        return prefs.getString(KEY_SESSION_COOKIE, "");
    }

    /** Stores the session cookie returned by a successful /api/mobile-gm/login bound to serverUrl. */
    static void setSessionCookie(Context context, String serverUrl, String cookie) {
        String origin = canonicalOrigin(serverUrl);
        if (origin.isEmpty() || cookie == null || cookie.isEmpty()) {
            clear(context);
            return;
        }
        context.getSharedPreferences(PREFS, Context.MODE_PRIVATE)
            .edit()
            .putString(KEY_SESSION_ORIGIN, origin)
            .putString(KEY_SESSION_COOKIE, cookie)
            .apply();
    }

    /** Clears the stored session (sign-out / server switch / re-pair). */
    static void clear(Context context) {
        context.getSharedPreferences(PREFS, Context.MODE_PRIVATE)
            .edit()
            .remove(KEY_SESSION_COOKIE)
            .remove(KEY_SESSION_ORIGIN)
            .apply();
    }
}
