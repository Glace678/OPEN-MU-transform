package net.munique.openmu.game;

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
}
