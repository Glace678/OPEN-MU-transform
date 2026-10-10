package net.munique.openmu.gm;

import android.content.Context;
import android.content.SharedPreferences;

/**
 * 107-02: secure storage for the GM API bearer token.
 *
 * The build-time BuildConfig.MOBILE_GM_TOKEN is a default injected at package
 * time. On first launch it is copied into this app-private SharedPreferences
 * store so that:
 *   1. the live token is not repeatedly extracted from the APK string table;
 *   2. the token can be rotated (re-paired) without rebuilding the APK;
 *   3. a flag marks whether the operator has changed the default build-time
 *      token -- shipping the default to end users must be treated as a
 *      known-weak configuration and changed on first deploy.
 *
 * The store itself is not encrypted at rest (no AndroidX Security dependency
 * added), but SharedPreferences in /data/data/<pkg>/ is only readable by this
 * app's UID on a non-rooted device, which is the same protection level as
 * every other setting in this app. Full EncryptedSharedPreferences can be
 * layered on later if a stronger guarantee is needed.
 */
final class GmTokenStore {
    private static final String PREFS = "openmu-gm";
    private static final String KEY_GM_TOKEN = "gm-token";
    private static final String KEY_TOKEN_ROTATED = "gm-token-rotated";

    private GmTokenStore() {
    }

    /**
     * Returns the current GM bearer token. On first call the build-time
     * BuildConfig default is copied into private storage and the rotated flag
     * remains false (meaning: still the shipped default, needs operator change).
     */
    static String getToken(Context context) {
        SharedPreferences prefs = context.getSharedPreferences(PREFS, Context.MODE_PRIVATE);
        String token = prefs.getString(KEY_GM_TOKEN, null);
        if (token == null || token.isEmpty()) {
            token = BuildConfig.MOBILE_GM_TOKEN;
            prefs.edit().putString(KEY_GM_TOKEN, token).apply();
        }
        return token;
    }

    /**
     * Updates the stored GM token (re-pair / rotation). Marks the token as
     * rotated so the "change the default" warning no longer fires.
     */
    static void setToken(Context context, String newToken) {
        context.getSharedPreferences(PREFS, Context.MODE_PRIVATE)
            .edit()
            .putString(KEY_GM_TOKEN, newToken)
            .putBoolean(KEY_TOKEN_ROTATED, true)
            .apply();
    }

    /**
     * True once the operator has explicitly set a token different from the
     * build-time default. A false return means the app is still using the
     * shipped build-time bearer and the deployment should be treated as
     * insecure until re-paired.
     */
    static boolean isRotated(Context context) {
        return context.getSharedPreferences(PREFS, Context.MODE_PRIVATE)
            .getBoolean(KEY_TOKEN_ROTATED, false);
    }
}