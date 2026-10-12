package net.munique.openmu.gm;

import android.content.Context;
import android.content.SharedPreferences;

/**
 * F1 (M1-01 / M2-H1): persists the short-lived admin-panel cookie session issued
 * by POST /api/mobile-gm/login. There is no static, APK-distributed bearer any
 * more: the GM app authenticates with the administrator account (password +
 * optional TOTP) and stores only the resulting server-issued session cookie.
 *
 * The cookie is kept in app-private SharedPreferences (readable only by this
 * app's UID on a non-rooted device), the same protection level as every other
 * setting in this app.
 */
final class GmTokenStore {
    private static final String PREFS = "openmu-gm";
    private static final String KEY_SESSION_COOKIE = "session-cookie";

    private GmTokenStore() {
    }

    /**Returns the stored admin session cookie, or an empty string when not signed in. */
    static String getSessionCookie(Context context) {
        return context.getSharedPreferences(PREFS, Context.MODE_PRIVATE)
            .getString(KEY_SESSION_COOKIE, "");
    }

    /**Stores the session cookie returned by a successful /api/mobile-gm/login. */
    static void setSessionCookie(Context context, String cookie) {
        context.getSharedPreferences(PREFS, Context.MODE_PRIVATE)
            .edit()
            .putString(KEY_SESSION_COOKIE, cookie == null ? "" : cookie)
            .apply();
    }

    /**Clears the stored session (sign-out / re-pair). */
    static void clear(Context context) {
        context.getSharedPreferences(PREFS, Context.MODE_PRIVATE)
            .edit()
            .remove(KEY_SESSION_COOKIE)
            .apply();
    }
}
