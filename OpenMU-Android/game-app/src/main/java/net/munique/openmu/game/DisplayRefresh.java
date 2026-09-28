package net.munique.openmu.game;

import android.os.Build;
import android.view.Display;

/**
 * Detects the device's display refresh rates so the frame-rate slider can range
 * from the floor (30 FPS) up to the highest refresh rate the phone advertises.
 */
final class DisplayRefresh {
    static final int MIN_FRAME_RATE = 30;
    static final int DEFAULT_FRAME_RATE = 60;
    static final int FALLBACK_REFRESH_RATE = 60;

    private DisplayRefresh() {
    }

    /**
     * Highest refresh rate (Hz) the display advertises at its current resolution,
     * rounded to an int. Falls back to the active refresh rate, then 60 Hz.
     */
    static int maximumRefreshRate(Display display) {
        float best = 0f;
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.M && display != null) {
            Display.Mode[] modes = display.getSupportedModes();
            if (modes != null) {
                for (Display.Mode mode : modes) {
                    float rate = mode.getRefreshRate();
                    if (rate > best) {
                        best = rate;
                    }
                }
            }
        }
        if (best <= 0f && display != null) {
            best = display.getRefreshRate();
        }

        int hertz = Math.round(best);
        if (hertz < MIN_FRAME_RATE) {
            hertz = FALLBACK_REFRESH_RATE;
        }
        return hertz;
    }
}
