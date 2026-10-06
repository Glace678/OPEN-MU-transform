package net.munique.openmu.game;

import android.graphics.Rect;
import android.os.Build;
import android.util.DisplayMetrics;
import android.view.WindowManager;

final class MobileViewportMetrics {
    private MobileViewportMetrics() { }

    static int renderDimension(int viewDimension, int renderPercent) {
        int percent = Math.max(50, Math.min(100, renderPercent));
        return Math.max(1, Math.round(Math.max(1, viewDimension) * percent / 100.0f));
    }

    static int keyboardPan(int layoutHeight, int keyboardHeight, int focusBottom, int gap) {
        int keyboard = Math.max(0, Math.min(Math.max(0, layoutHeight - 1), keyboardHeight));
        if (keyboard == 0 || focusBottom <= 0) return 0;
        int visibleBottom = layoutHeight - keyboard;
        int overlap = focusBottom + Math.max(0, gap) - visibleBottom;
        return Math.min(keyboard, Math.max(0, overlap));
    }

    static int utilitySideMargin(int safeMargin, int buttonSize, int gap) {
        return Math.max(0, safeMargin) + Math.max(0, buttonSize) + Math.max(0, gap);
    }
    @SuppressWarnings("deprecation")
    static void populateRealMetrics(WindowManager windowManager, DisplayMetrics outMetrics) {
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.R) {
            Rect bounds = windowManager.getCurrentWindowMetrics().getBounds();
            outMetrics.widthPixels = bounds.width();
            outMetrics.heightPixels = bounds.height();
        } else {
            windowManager.getDefaultDisplay().getRealMetrics(outMetrics);
        }
    }
}

