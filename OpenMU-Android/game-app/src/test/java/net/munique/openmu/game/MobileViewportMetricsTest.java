package net.munique.openmu.game;

import org.junit.Test;

import static org.junit.Assert.assertEquals;

public class MobileViewportMetricsTest {
    @Test public void renderBufferTracksEveryViewportSizeAtTheSelectedScale() {
        assertEquals(1918, MobileViewportMetrics.renderDimension(2256, 85));
        assertEquals(864, MobileViewportMetrics.renderDimension(1016, 85));
        assertEquals(413, MobileViewportMetrics.renderDimension(486, 85));
        assertEquals(1016, MobileViewportMetrics.renderDimension(1016, 100));
    }

    @Test public void invalidScalesAndDimensionsStayWithinSupportedBounds() {
        assertEquals(500, MobileViewportMetrics.renderDimension(1000, -1));
        assertEquals(1000, MobileViewportMetrics.renderDimension(1000, 150));
        assertEquals(1, MobileViewportMetrics.renderDimension(0, 75));
        assertEquals(1, MobileViewportMetrics.renderDimension(-20, 75));
    }

    @Test public void keyboardPansOnlyEnoughToExposeTheCurrentField() {
        assertEquals(182, MobileViewportMetrics.keyboardPan(1080, 690, 540, 32));
        assertEquals(0, MobileViewportMetrics.keyboardPan(1080, 690, 300, 32));
        assertEquals(242, MobileViewportMetrics.keyboardPan(1080, 690, 600, 32));
    }

    @Test public void keyboardHideAndInvalidBoundsRestoreTheOriginalViewport() {
        assertEquals(0, MobileViewportMetrics.keyboardPan(1080, 0, 600, 32));
        assertEquals(0, MobileViewportMetrics.keyboardPan(1080, -1, 600, 32));
        assertEquals(0, MobileViewportMetrics.keyboardPan(0, 5000, 600, 32));
        assertEquals(0, MobileViewportMetrics.keyboardPan(1080, 690, 0, 32));
        assertEquals(1079, MobileViewportMetrics.keyboardPan(1080, 5000, 5000, 32));
    }

    @Test public void utilityRailDoesNotCoverTheNativeSurfaceAtAnySupportedScale() {
        for (int safeMargin : new int[] {0, 24, 72, 192}) {
            for (int buttonSize : new int[] {44, 52, 136, 180}) {
                int rail = MobileViewportMetrics.utilitySideMargin(safeMargin, buttonSize, 12);
                assertEquals(safeMargin + buttonSize + 12, rail);
                assertEquals(12, rail - safeMargin - buttonSize);
            }
        }
        assertEquals(0, MobileViewportMetrics.utilitySideMargin(-1, -1, -1));
    }
}
