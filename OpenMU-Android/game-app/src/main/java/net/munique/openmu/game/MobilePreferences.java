package net.munique.openmu.game;

import android.content.Context;
import android.content.SharedPreferences;
import android.os.StatFs;
import android.util.DisplayMetrics;
import android.view.Display;
import android.view.WindowManager;

import java.io.BufferedInputStream;
import java.io.BufferedOutputStream;
import java.io.File;
import java.io.FileInputStream;
import java.io.FileOutputStream;
import java.io.IOException;
import java.io.InputStream;
import java.io.InterruptedIOException;
import java.nio.charset.StandardCharsets;
import java.nio.file.AtomicMoveNotSupportedException;
import java.nio.file.Files;
import java.nio.file.StandardCopyOption;
import java.util.ArrayList;
import java.util.Arrays;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Locale;
import java.util.Map;
import java.util.zip.ZipEntry;
import java.util.zip.ZipInputStream;

final class MobilePreferences {
    static final String PREFS = "openmu-mobile";
    static final String KEY_FPS = "fps";
    static final String KEY_RENDER_SCALE = "render-scale";
    static final String KEY_EFFECTS = "effects";
    static final String KEY_UI_SCALE = "ui-scale";
    static final String KEY_SAFE_MARGIN = "safe-margin";
    static final String KEY_TOUCH_OPACITY = "touch-opacity";
    static final String KEY_VIBRATION = "vibration";
    static final String KEY_HANDEDNESS = "handedness";
    static final String KEY_SERVER = "server";
    static final String KEY_PORT = "port";
    static final String KEY_LOCAL_PAIRING = "local-pairing";
    static final String KEY_ACCOUNT_PORTAL_BASE = "account-portal-base";
    static final String KEY_ACCOUNT_PORTAL_LOCALE = "account-portal-locale";
    static final String KEY_TUTORIAL_VERSION = "tutorial-version";

    static final String DEFAULT_SERVER = BuildConfig.DEFAULT_SERVER_ADDRESS;
    static final int DEFAULT_PORT = 44406;
    static final int DEFAULT_RENDER_SCALE = 75;
    static final int DEFAULT_UI_SCALE = 100;
    static final int DEFAULT_SAFE_MARGIN = 3;
    static final int DEFAULT_TOUCH_OPACITY = 65;

    private static final String DATA_ARCHIVE = "game-data.zip";
    private static final String VERSION_MARKER = ".openmu-data-version";
    private static final String STAGING_DIRECTORY = "game.pending";
    private static final String BACKUP_DIRECTORY = "game.backup";
    private static final long EXTRA_STORAGE_ALLOWANCE = 256L * 1024L * 1024L;
    private static final Object DATA_EXTRACTION_LOCK = new Object();

    interface ProgressListener {
        void onProgress(int percent, String currentFile);
    }

    private MobilePreferences() {
    }

    static SharedPreferences get(Context context) {
        return context.getSharedPreferences(PREFS, Context.MODE_PRIVATE);
    }

    static File dataRoot(Context context) {
        return new File(context.getFilesDir(), "game");
    }

    static boolean isGameDataReady(Context context) {
        return hasCurrentDataVersion(dataRoot(context));
    }

    private static boolean hasCurrentDataVersion(File root) {
        File marker = new File(root, VERSION_MARKER);
        if (!marker.isFile()) {
            return false;
        }
        byte[] versionBytes = new byte[(int) Math.min(marker.length(), 256)];
        try (FileInputStream input = new FileInputStream(marker)) {
            int length = input.read(versionBytes);
            String value = new String(versionBytes, 0, Math.max(0, length), StandardCharsets.UTF_8);
            return BuildConfig.GAME_DATA_VERSION.equals(value.trim());
        } catch (IOException ignored) {
            return false;
        }
    }

    static void extractGameData(Context context, ProgressListener listener) throws IOException {
        synchronized (DATA_EXTRACTION_LOCK) {
            extractGameDataLocked(context.getApplicationContext(), listener);
        }
    }

    private static void extractGameDataLocked(Context context, ProgressListener listener) throws IOException {
        File root = dataRoot(context);
        File staging = new File(context.getFilesDir(), STAGING_DIRECTORY);
        File backup = new File(context.getFilesDir(), BACKUP_DIRECTORY);

        if (!root.exists() && backup.exists()) {
            moveDirectory(backup, root);
        }
        if (hasCurrentDataVersion(root)) {
            deleteTree(backup);
            listener.onProgress(100, "");
            return;
        }

        long expandedSize = calculateExpandedSize(context);
        long available = new StatFs(context.getFilesDir().getAbsolutePath()).getAvailableBytes();
        if (expandedSize > 0 && available < expandedSize + EXTRA_STORAGE_ALLOWANCE) {
            long shortfall = expandedSize + EXTRA_STORAGE_ALLOWANCE - available;
            throw new IOException(context.getString(R.string.storage_low, humanBytes(shortfall)));
        }

        deleteTree(staging);
        if (!staging.mkdirs()) {
            throw new IOException("Unable to create " + staging);
        }

        String rootPath = staging.getCanonicalPath() + File.separator;
        long completed = 0;
        boolean promoted = false;
        try {
            try (InputStream asset = context.getAssets().open(DATA_ARCHIVE);
                 ZipInputStream zip = new ZipInputStream(new BufferedInputStream(asset))) {
                ZipEntry entry;
                byte[] buffer = new byte[1024 * 1024];
                while ((entry = zip.getNextEntry()) != null) {
                    throwIfInterrupted();
                    String normalizedName = entry.getName().replace('\\', '/');
                    File output = new File(staging, normalizedName);
                    String outputPath = output.getCanonicalPath();
                    if (!outputPath.startsWith(rootPath)) {
                        throw new IOException("Unsafe path in game data: " + entry.getName());
                    }

                    if (entry.isDirectory()) {
                        if (!output.exists() && !output.mkdirs()) {
                            throw new IOException("Unable to create " + output);
                        }
                    } else {
                        File parent = output.getParentFile();
                        if (parent != null && !parent.exists() && !parent.mkdirs()) {
                            throw new IOException("Unable to create " + parent);
                        }
                        try (BufferedOutputStream destination = new BufferedOutputStream(new FileOutputStream(output))) {
                            int read;
                            while ((read = zip.read(buffer)) != -1) {
                                throwIfInterrupted();
                                destination.write(buffer, 0, read);
                                completed += read;
                                if (expandedSize > 0) {
                                    listener.onProgress((int) Math.min(99, completed * 100 / expandedSize), normalizedName);
                                }
                            }
                        }
                    }
                    zip.closeEntry();
                }
            }

            File existingConfig = new File(root, "config.ini");
            if (existingConfig.isFile()) {
                Files.copy(existingConfig.toPath(), new File(staging, "config.ini").toPath(),
                    StandardCopyOption.REPLACE_EXISTING);
            }
            writeConfig(context, staging);
            File marker = new File(staging, VERSION_MARKER);
            try (FileOutputStream output = new FileOutputStream(marker, false)) {
                output.write(BuildConfig.GAME_DATA_VERSION.getBytes(StandardCharsets.UTF_8));
            }

            deleteTree(backup);
            if (root.exists()) {
                moveDirectory(root, backup);
            }
            try {
                moveDirectory(staging, root);
                promoted = true;
            } catch (IOException promotionFailure) {
                if (!root.exists() && backup.exists()) {
                    try {
                        moveDirectory(backup, root);
                    } catch (IOException restoreFailure) {
                        promotionFailure.addSuppressed(restoreFailure);
                    }
                }
                throw promotionFailure;
            }
            deleteTree(backup);
            listener.onProgress(100, "");
        } catch (java.io.FileNotFoundException missing) {
            throw new IOException(context.getString(R.string.game_data_missing), missing);
        } finally {
            if (!promoted) {
                deleteTree(staging);
            }
        }
    }

    private static void throwIfInterrupted() throws InterruptedIOException {
        if (Thread.currentThread().isInterrupted()) {
            throw new InterruptedIOException("Game data preparation was interrupted.");
        }
    }

    private static void moveDirectory(File source, File destination) throws IOException {
        try {
            Files.move(source.toPath(), destination.toPath(), StandardCopyOption.ATOMIC_MOVE);
        } catch (AtomicMoveNotSupportedException ignored) {
            Files.move(source.toPath(), destination.toPath());
        }
    }

    private static void deleteTree(File path) throws IOException {
        if (!path.exists()) {
            return;
        }
        if (path.isDirectory()) {
            File[] children = path.listFiles();
            if (children == null) {
                throw new IOException("Unable to list " + path);
            }
            for (File child : children) {
                deleteTree(child);
            }
        }
        if (!path.delete() && path.exists()) {
            throw new IOException("Unable to delete " + path);
        }
    }

    private static long calculateExpandedSize(Context context) throws IOException {
        long total = 0;
        try (InputStream asset = context.getAssets().open(DATA_ARCHIVE);
             ZipInputStream zip = new ZipInputStream(new BufferedInputStream(asset))) {
            ZipEntry entry;
            byte[] buffer = new byte[1024 * 1024];
            while ((entry = zip.getNextEntry()) != null) {
                if (!entry.isDirectory()) {
                    int read;
                    while ((read = zip.read(buffer)) != -1) {
                        throwIfInterrupted();
                        total += read;
                    }
                }
                zip.closeEntry();
            }
        } catch (java.io.FileNotFoundException missing) {
            throw new IOException(context.getString(R.string.game_data_missing), missing);
        }
        return total;
    }

    static void writeConfig(Context context) throws IOException {
        writeConfig(context, dataRoot(context));
    }

    private static void writeConfig(Context context, File root) throws IOException {
        SharedPreferences preferences = get(context);
        int maximumRefresh = maximumRefreshRate(context);
        int targetFps = targetFrameRate(context);
        boolean useDisplayMaximum = targetFps >= maximumRefresh;
        int renderScale = preferences.getInt(KEY_RENDER_SCALE, DEFAULT_RENDER_SCALE);
        int uiScale = preferences.getInt(KEY_UI_SCALE, DEFAULT_UI_SCALE);
        int safeMargin = preferences.getInt(KEY_SAFE_MARGIN, DEFAULT_SAFE_MARGIN);
        int touchOpacity = preferences.getInt(KEY_TOUCH_OPACITY, DEFAULT_TOUCH_OPACITY);
        String effects = preferences.getString(KEY_EFFECTS, "balanced");
        String handedness = preferences.getString(KEY_HANDEDNESS, "right");
        String server = sanitizeIniValue(preferences.getString(KEY_SERVER, DEFAULT_SERVER));
        int port = preferences.getInt(KEY_PORT, DEFAULT_PORT);
        int renderLevel = "low".equals(effects) ? 1 : "high".equals(effects) ? 4 : 3;
        boolean renderAllEffects = !"low".equals(effects);

        DisplayMetrics metrics = new DisplayMetrics();
        WindowManager manager = (WindowManager) context.getSystemService(Context.WINDOW_SERVICE);
        manager.getDefaultDisplay().getRealMetrics(metrics);
        int width = Math.max(metrics.widthPixels, metrics.heightPixels);
        int height = Math.min(metrics.widthPixels, metrics.heightPixels);

        Map<String, LinkedHashMap<String, String>> updates = new LinkedHashMap<>();
        putIni(updates, "Window", "Width", Integer.toString(width));
        putIni(updates, "Window", "Height", Integer.toString(height));
        putIni(updates, "Window", "Windowed", "0");
        putIni(updates, "CONNECTION SETTINGS", "ServerIP", server);
        putIni(updates, "CONNECTION SETTINGS", "ServerPort", Integer.toString(port));
        putIni(updates, "UI", "ScalePercent", Integer.toString(uiScale));
        putIni(updates, "UI", "SafeMarginPercent", Integer.toString(safeMargin));
        putIni(updates, "Render", "FrameRateMode", useDisplayMaximum ? "DisplayMaximum" : "Fixed");
        putIni(updates, "Render", "FrameRateLimit", Integer.toString(targetFps));
        putIni(updates, "Render", "FrameRateLimitMilliHz", Integer.toString(targetFps * 1000));
        putIni(updates, "Render", "BackgroundFrameRate", "30");
        putIni(updates, "Render", "VSync", useDisplayMaximum ? "1" : "0");
        putIni(updates, "Render", "CoreProfile", "0");
        putIni(updates, "Render", "RenderScalePercent", Integer.toString(renderScale));
        putIni(updates, "Render", "EffectQuality", effects);
        putIni(updates, "Render", "RenderLevel", Integer.toString(renderLevel));
        putIni(updates, "Render", "RenderAllEffects", renderAllEffects ? "1" : "0");
        putIni(updates, "Input", "MobileTouchEnabled", "1");
        putIni(updates, "Input", "TouchOpacityPercent", Integer.toString(touchOpacity));
        putIni(updates, "Input", "Handedness", handedness);
        putIni(updates, "Haptics", "Enabled",
            preferences.getBoolean(KEY_VIBRATION, true) ? "1" : "0");

        if (!root.exists() && !root.mkdirs()) {
            throw new IOException("Unable to create " + root);
        }
        File configFile = new File(root, "config.ini");
        String existing = configFile.isFile()
            ? new String(Files.readAllBytes(configFile.toPath()), StandardCharsets.UTF_8)
            : defaultConfig();
        String merged = mergeIni(existing, updates);
        File pending = new File(root, "config.ini.pending");
        try (FileOutputStream output = new FileOutputStream(pending, false)) {
            output.write(merged.getBytes(StandardCharsets.UTF_8));
            output.getFD().sync();
        }
        try {
            Files.move(pending.toPath(), configFile.toPath(),
                StandardCopyOption.ATOMIC_MOVE, StandardCopyOption.REPLACE_EXISTING);
        } catch (AtomicMoveNotSupportedException ignored) {
            Files.move(pending.toPath(), configFile.toPath(), StandardCopyOption.REPLACE_EXISTING);
        }
    }

    private static String defaultConfig() {
        return "[LOGIN]\nRememberMe=0\nSavePassword=0\nLanguage=Chs\n"
            + "EncryptedUsername=\nEncryptedPassword=\n"
            + "[UI]\nLocale=zh-CN\n"
            + "[Audio]\nSoundVolume=5\nMusicVolume=5\n";
    }

    private static void putIni(Map<String, LinkedHashMap<String, String>> updates,
                               String section, String key, String value) {
        updates.computeIfAbsent(section, ignored -> new LinkedHashMap<>()).put(key, value);
    }

    static String mergeIni(String source, Map<String, LinkedHashMap<String, String>> updates) {
        String normalizedSource = source.replace("\r\n", "\n");
        List<String> lines = new ArrayList<>(Arrays.asList(normalizedSource.split("\n", -1)));
        if (normalizedSource.endsWith("\n")) {
            lines.remove(lines.size() - 1);
        }
        Map<String, String> canonicalSections = new LinkedHashMap<>();
        Map<String, Map<String, String>> canonicalKeys = new LinkedHashMap<>();
        for (Map.Entry<String, LinkedHashMap<String, String>> section : updates.entrySet()) {
            String normalizedSection = section.getKey().toLowerCase(Locale.ROOT);
            canonicalSections.put(normalizedSection, section.getKey());
            Map<String, String> keys = new LinkedHashMap<>();
            for (String key : section.getValue().keySet()) {
                keys.put(key.toLowerCase(Locale.ROOT), key);
            }
            canonicalKeys.put(normalizedSection, keys);
        }

        Map<String, Map<String, Boolean>> written = new LinkedHashMap<>();
        for (Map.Entry<String, LinkedHashMap<String, String>> section : updates.entrySet()) {
            Map<String, Boolean> keys = new LinkedHashMap<>();
            for (String key : section.getValue().keySet()) {
                keys.put(key.toLowerCase(Locale.ROOT), false);
            }
            written.put(section.getKey().toLowerCase(Locale.ROOT), keys);
        }

        Map<String, Boolean> seenSections = new LinkedHashMap<>();
        StringBuilder merged = new StringBuilder();
        String currentSection = "";
        for (String line : lines) {
            String trimmed = line.trim();
            if (trimmed.startsWith("[") && trimmed.endsWith("]") && trimmed.length() > 2) {
                appendMissingKeys(merged, currentSection, updates, canonicalSections, written);
                currentSection = trimmed.substring(1, trimmed.length() - 1).trim().toLowerCase(Locale.ROOT);
                seenSections.put(currentSection, true);
                merged.append(line).append('\n');
                continue;
            }
            int separator = trimmed.indexOf('=');
            Map<String, String> keys = canonicalKeys.get(currentSection);
            if (separator <= 0 || keys == null) {
                merged.append(line).append('\n');
                continue;
            }
            String normalizedKey = trimmed.substring(0, separator).trim().toLowerCase(Locale.ROOT);
            String canonicalKey = keys.get(normalizedKey);
            if (canonicalKey == null) {
                merged.append(line).append('\n');
                continue;
            }
            String canonicalSection = canonicalSections.get(currentSection);
            merged.append(canonicalKey).append('=')
                .append(updates.get(canonicalSection).get(canonicalKey)).append('\n');
            written.get(currentSection).put(normalizedKey, true);
        }
        appendMissingKeys(merged, currentSection, updates, canonicalSections, written);
        for (Map.Entry<String, LinkedHashMap<String, String>> section : updates.entrySet()) {
            String normalizedSection = section.getKey().toLowerCase(Locale.ROOT);
            if (Boolean.TRUE.equals(seenSections.get(normalizedSection))) {
                continue;
            }
            merged.append('[').append(section.getKey()).append("]\n");
            appendMissingKeys(merged, normalizedSection, updates, canonicalSections, written);
        }
        return merged.toString();
    }

    private static void appendMissingKeys(
        StringBuilder output,
        String normalizedSection,
        Map<String, LinkedHashMap<String, String>> updates,
        Map<String, String> canonicalSections,
        Map<String, Map<String, Boolean>> written) {
        String canonicalSection = canonicalSections.get(normalizedSection);
        if (canonicalSection == null) {
            return;
        }
        for (Map.Entry<String, String> key : updates.get(canonicalSection).entrySet()) {
            String normalizedKey = key.getKey().toLowerCase(Locale.ROOT);
            if (!Boolean.TRUE.equals(written.get(normalizedSection).get(normalizedKey))) {
                output.append(key.getKey()).append('=').append(key.getValue()).append('\n');
                written.get(normalizedSection).put(normalizedKey, true);
            }
        }
    }

    static String[] nativeArguments(Context context) {
        SharedPreferences preferences = get(context);
        return new String[] {
            "--data-root", dataRoot(context).getAbsolutePath(),
            "--server", preferences.getString(KEY_SERVER, DEFAULT_SERVER),
            "--port", Integer.toString(preferences.getInt(KEY_PORT, DEFAULT_PORT)),
            "--fps", Integer.toString(targetFrameRate(context)),
            "--render-scale", Integer.toString(preferences.getInt(KEY_RENDER_SCALE, DEFAULT_RENDER_SCALE)),
            "--effects", preferences.getString(KEY_EFFECTS, "balanced"),
            "--ui-scale", Integer.toString(preferences.getInt(KEY_UI_SCALE, DEFAULT_UI_SCALE)),
            "--safe-margin", Integer.toString(preferences.getInt(KEY_SAFE_MARGIN, DEFAULT_SAFE_MARGIN)),
            "--touch-opacity", Integer.toString(preferences.getInt(KEY_TOUCH_OPACITY, DEFAULT_TOUCH_OPACITY)),
            "--handedness", preferences.getString(KEY_HANDEDNESS, "right")
        };
    }

    static boolean usesLocalPairing(Context context) {
        return get(context).getBoolean(KEY_LOCAL_PAIRING,
            LocalIpv4Address.isTrusted(DEFAULT_SERVER));
    }

    static String accountPortalLocale(Context context) {
        String selected = get(context).getString(KEY_ACCOUNT_PORTAL_LOCALE, "");
        if (selected != null && !selected.isEmpty()) {
            return AccountPortalPolicy.normalizeLocale(selected);
        }
        String deviceLocale = context.getResources().getConfiguration().getLocales().get(0).toLanguageTag();
        File config = new File(dataRoot(context), "config.ini");
        if (config.isFile() && config.length() <= 1024 * 1024) {
            try {
                return AccountPortalPolicy.localeFromIni(
                    new String(Files.readAllBytes(config.toPath()), StandardCharsets.UTF_8), deviceLocale);
            } catch (IOException ignored) {
                // Unprepared data must not prevent access to the account portal.
            }
        }
        return AccountPortalPolicy.normalizeLocale(deviceLocale);
    }

    static String accountPortalBase(Context context) {
        SharedPreferences preferences = get(context);
        String base = preferences.getString(KEY_ACCOUNT_PORTAL_BASE, "");
        try {
            return AccountPortalPolicy.canonicalBase(base,
                usesLocalPairing(context), preferences.getString(KEY_SERVER, DEFAULT_SERVER));
        } catch (IllegalArgumentException ignored) {
            return "";
        }
    }

    static String accountPortalUrl(Context context, String action) {
        SharedPreferences preferences = get(context);
        try {
            return AccountPortalPolicy.actionUrl(accountPortalBase(context), action, accountPortalLocale(context),
                usesLocalPairing(context), preferences.getString(KEY_SERVER, DEFAULT_SERVER));
        } catch (IllegalArgumentException ignored) {
            return null;
        }
    }

    static boolean isValidServer(String value, boolean localPairing) {
        return MobileConnectionPolicy.isValidServer(value, localPairing);
    }

    /** Highest refresh rate (Hz) the device's display advertises. */
    static int maximumRefreshRate(Context context) {
        WindowManager manager = (WindowManager) context.getSystemService(Context.WINDOW_SERVICE);
        Display display = manager == null ? null : manager.getDefaultDisplay();
        return DisplayRefresh.maximumRefreshRate(display);
    }

    /**
     * Resolves the stored frame-rate preference to a concrete target in
     * [30, device max]. The legacy "follow" value and any out-of-range value
     * resolve to the device maximum so the engine can follow the panel.
     */
    static int targetFrameRate(Context context) {
        int maximum = maximumRefreshRate(context);
        String stored = get(context).getString(KEY_FPS,
            Integer.toString(DisplayRefresh.DEFAULT_FRAME_RATE));
        if (stored == null || "follow".equals(stored)) {
            return maximum;
        }
        int parsed = parseInt(stored, maximum);
        return Math.max(DisplayRefresh.MIN_FRAME_RATE, Math.min(parsed, maximum));
    }

    private static String sanitizeIniValue(String value) {
        return value == null ? DEFAULT_SERVER : value.replace("\r", "").replace("\n", "").trim();
    }

    private static int parseInt(String value, int fallback) {
        try {
            return Integer.parseInt(value);
        } catch (NumberFormatException ignored) {
            return fallback;
        }
    }

    private static String humanBytes(long bytes) {
        if (bytes >= 1024L * 1024L * 1024L) {
            return String.format(Locale.ROOT, "%.1f GB", bytes / (1024.0 * 1024.0 * 1024.0));
        }
        return String.format(Locale.ROOT, "%.0f MB", bytes / (1024.0 * 1024.0));
    }
}
