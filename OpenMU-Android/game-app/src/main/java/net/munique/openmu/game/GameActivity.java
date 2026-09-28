package net.munique.openmu.game;

import android.content.Intent;
import android.graphics.Color;
import android.graphics.Rect;
import android.os.Build;
import android.os.Bundle;
import android.text.InputType;
import android.view.Display;
import android.view.Gravity;
import android.view.View;
import android.view.ViewGroup;
import android.view.WindowManager;
import android.view.WindowInsets;
import android.view.inputmethod.InputMethodManager;
import android.widget.ImageButton;
import android.widget.LinearLayout;
import android.widget.RelativeLayout;
import android.util.DisplayMetrics;
import android.util.Log;

import org.libsdl.app.SDLActivity;

public final class GameActivity extends SDLActivity {
    private static final int MIN_UTILITY_BUTTON_DP = 44;
    private static final int DEFAULT_UTILITY_BUTTON_DP = 52;
    private static final int UTILITY_GAP_DP = 4;
    private int viewportHorizontalMargin;
    private int viewportVerticalMargin;
    private int viewportRenderPercent;
    private int viewportKeyboardHeight;
    private int lastRenderWidth;
    private int lastRenderHeight;
    private int lastDiagnosticPan = -1;
    private int lastInputType = -1;

    @Override
    protected String[] getLibraries() {
        return new String[] { "SDL3", "main" };
    }

    @Override
    protected String[] getArguments() {
        return MobilePreferences.nativeArguments(this);
    }

    @Override
    protected void onCreate(Bundle state) {
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.P) {
            getWindow().getAttributes().layoutInDisplayCutoutMode =
                WindowManager.LayoutParams.LAYOUT_IN_DISPLAY_CUTOUT_MODE_SHORT_EDGES;
        }
        getWindow().addFlags(WindowManager.LayoutParams.FLAG_KEEP_SCREEN_ON);
        getWindow().setSoftInputMode(WindowManager.LayoutParams.SOFT_INPUT_ADJUST_NOTHING);
        applyPreferredFrameRate();
        super.onCreate(state);
        if (SDLActivity.mBrokenLibraries) {
            return;
        }
        configureMobileIdentity();
        configureMobileViewport();
        addUtilityButtons();
    }

    private void configureMobileIdentity() {
        SDLActivity.nativeSetenv("MU_INPUT_DIAGNOSTICS",
            BuildConfig.DEBUG && getIntent().getBooleanExtra("inputDiagnostics", false) ? "1" : "0");
        String server = MobilePreferences.get(this).getString(MobilePreferences.KEY_SERVER,
            MobilePreferences.DEFAULT_SERVER);
        MobileIdentity.Credentials credentials = MobileConnectionPolicy.localCredentials(server,
            MobilePreferences.usesLocalPairing(this), BuildConfig.MOBILE_PACKAGE_KEY);
        boolean automaticLogin = credentials != null;
        SDLActivity.nativeSetenv("MU_LOCAL_AUTO_LOGIN", automaticLogin ? "1" : "0");
        SDLActivity.nativeSetenv("MU_MOBILE_LOCAL_AUTO_LOGIN", automaticLogin ? "1" : "0");
        SDLActivity.nativeSetenv("MU_LOCAL_GAME_USERNAME", automaticLogin ? credentials.username() : "");
        SDLActivity.nativeSetenv("MU_LOCAL_GAME_PASSWORD", automaticLogin ? credentials.password() : "");
        SDLActivity.nativeSetenv("MU_ACCOUNT_PORTAL_URL", MobilePreferences.accountPortalBase(this));
        SDLActivity.nativeSetenv("MU_ACCOUNT_PORTAL_CULTURE", MobilePreferences.accountPortalLocale(this));
    }

    private void configureMobileViewport() {
        if (mSurface == null) {
            return;
        }
        if (mLayout != null) {
            mLayout.setBackgroundColor(Color.BLACK);
        }
        android.content.SharedPreferences preferences = MobilePreferences.get(this);
        int safePercent = preferences.getInt(MobilePreferences.KEY_SAFE_MARGIN,
            MobilePreferences.DEFAULT_SAFE_MARGIN);
        DisplayMetrics metrics = new DisplayMetrics();
        getWindowManager().getDefaultDisplay().getRealMetrics(metrics);
        viewportHorizontalMargin = Math.round(metrics.widthPixels * safePercent / 100.0f);
        viewportVerticalMargin = Math.round(metrics.heightPixels * safePercent / 100.0f);
        RelativeLayout.LayoutParams surfaceParams = new RelativeLayout.LayoutParams(
            ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.MATCH_PARENT);
        boolean leftHanded = usesLeftHandedControls();
        int utilityMargin = MobileViewportMetrics.utilitySideMargin(viewportHorizontalMargin,
            utilityButtonSize(), dp(UTILITY_GAP_DP));
        surfaceParams.setMargins(leftHanded ? utilityMargin : viewportHorizontalMargin, viewportVerticalMargin,
            leftHanded ? viewportHorizontalMargin : utilityMargin, viewportVerticalMargin);
        mSurface.setLayoutParams(surfaceParams);

        viewportRenderPercent = preferences.getInt(MobilePreferences.KEY_RENDER_SCALE,
            MobilePreferences.DEFAULT_RENDER_SCALE);
        mSurface.addOnLayoutChangeListener((view, left, top, right, bottom,
                oldLeft, oldTop, oldRight, oldBottom) -> updateRenderBuffer());
        mSurface.post(this::updateRenderBuffer);
        mLayout.setOnApplyWindowInsetsListener((view, insets) -> {
            if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.R) {
                int keyboard = insets.isVisible(WindowInsets.Type.ime())
                    ? insets.getInsets(WindowInsets.Type.ime()).bottom : 0;
                viewportKeyboardHeight = keyboard;
                updateKeyboardViewport(keyboard);
            }
            return insets;
        });
        mLayout.getViewTreeObserver().addOnGlobalLayoutListener(() -> {
            if (mLayout.getHeight() <= 0) {
                return;
            }
            if (Build.VERSION.SDK_INT < Build.VERSION_CODES.R) {
                Rect visible = new Rect();
                mLayout.getWindowVisibleDisplayFrame(visible);
                int[] position = new int[2];
                mLayout.getLocationOnScreen(position);
                int obscured = position[1] + mLayout.getHeight() - visible.bottom;
                viewportKeyboardHeight = obscured > mLayout.getHeight() / 4 ? obscured : 0;
            }
            updateKeyboardViewport(viewportKeyboardHeight);
        });
        mLayout.requestApplyInsets();
    }

    private void updateKeyboardViewport(int keyboardHeight) {
        if (mLayout == null || mSurface == null || mLayout.getHeight() <= 0) {
            return;
        }
        View editor = mTextEdit;
        int focusBottom = 0;
        if (editor != null && editor.getHeight() > 0 && lastRenderHeight > 0) {
            float renderToView = mSurface.getHeight() / (float)lastRenderHeight;
            focusBottom = viewportVerticalMargin
                + Math.round((editor.getTop() + editor.getHeight()) * renderToView);
        }
        int pan = MobileViewportMetrics.keyboardPan(mLayout.getHeight(),
            keyboardHeight, focusBottom, dp(12));
        if (BuildConfig.DEBUG && getIntent().getBooleanExtra("inputDiagnostics", false)
                && pan != lastDiagnosticPan) {
            lastDiagnosticPan = pan;
            Log.d("OpenMUViewport", "layout=" + mLayout.getHeight()
                + " keyboard=" + keyboardHeight + " editorTop="
                + (editor == null ? 0 : editor.getTop()) + " editorHeight="
                + (editor == null ? 0 : editor.getHeight()) + " render=" + lastRenderHeight
                + " view=" + mSurface.getHeight() + " focusBottom=" + focusBottom + " pan=" + pan);
        }
        // Moving the surface preserves its aspect ratio and its local touch coordinates.
        mSurface.setTranslationY(-pan);
    }

    // SDL's Android editor does not follow changed caret areas while the IME is already visible.
    public void syncTextInputArea(int x, int y, int width, int height,
            boolean password, boolean multiline) {
        int type = InputType.TYPE_CLASS_TEXT | (password
            ? InputType.TYPE_TEXT_VARIATION_PASSWORD | InputType.TYPE_TEXT_FLAG_NO_SUGGESTIONS
            : InputType.TYPE_TEXT_VARIATION_NORMAL | InputType.TYPE_TEXT_FLAG_AUTO_CORRECT
                | InputType.TYPE_TEXT_FLAG_AUTO_COMPLETE);
        if (multiline) type |= InputType.TYPE_TEXT_FLAG_MULTI_LINE;
        final int inputType = type;
        runOnUiThread(() -> {
            boolean changed = lastInputType != inputType;
            lastInputType = inputType;
            SDLActivity.showTextInput(inputType, x, y, width, height);
            if (changed && mLayout != null) {
                mLayout.post(() -> {
                    View editor = mTextEdit;
                    if (editor == null) return;
                    InputMethodManager input = (InputMethodManager)getSystemService(INPUT_METHOD_SERVICE);
                    if (input != null) input.restartInput(editor);
                });
            }
        });
    }

    private void updateRenderBuffer() {
        if (mSurface == null || mSurface.getWidth() <= 0 || mSurface.getHeight() <= 0) {
            return;
        }
        int width = MobileViewportMetrics.renderDimension(mSurface.getWidth(), viewportRenderPercent);
        int height = MobileViewportMetrics.renderDimension(mSurface.getHeight(), viewportRenderPercent);
        if (width == lastRenderWidth && height == lastRenderHeight) {
            return;
        }
        lastRenderWidth = width;
        lastRenderHeight = height;
        if (viewportRenderPercent >= 100) {
            mSurface.getHolder().setSizeFromLayout();
        } else {
            mSurface.getHolder().setFixedSize(width, height);
        }
        updateKeyboardViewport(viewportKeyboardHeight);
    }

    private void applyPreferredFrameRate() {
        if (Build.VERSION.SDK_INT < Build.VERSION_CODES.M) {
            return;
        }
        int target = MobilePreferences.targetFrameRate(this);
        int maximum = MobilePreferences.maximumRefreshRate(this);
        boolean wantMaximum = target >= maximum;

        Display display = getWindowManager().getDefaultDisplay();
        Display.Mode current = display.getMode();
        Display.Mode best = null;
        for (Display.Mode mode : display.getSupportedModes()) {
            if (mode.getPhysicalWidth() != current.getPhysicalWidth()
                || mode.getPhysicalHeight() != current.getPhysicalHeight()) {
                continue;
            }
            if (best == null) {
                best = mode;
                continue;
            }
            // At the slider's maximum, switch the panel to its highest refresh rate;
            // otherwise pick the mode whose refresh is closest to the chosen target.
            boolean better = wantMaximum
                ? mode.getRefreshRate() > best.getRefreshRate()
                : Math.abs(mode.getRefreshRate() - target) < Math.abs(best.getRefreshRate() - target);
            if (better) {
                best = mode;
            }
        }
        if (best != null && best.getModeId() != current.getModeId()) {
            WindowManager.LayoutParams attributes = getWindow().getAttributes();
            attributes.preferredDisplayModeId = best.getModeId();
            getWindow().setAttributes(attributes);
        }
    }

    private void addUtilityButtons() {
        if (mLayout == null) {
            return;
        }
        LinearLayout utilities = new LinearLayout(this);
        utilities.setOrientation(LinearLayout.VERTICAL);
        boolean leftHanded = usesLeftHandedControls();
        utilities.setGravity(leftHanded ? Gravity.START : Gravity.END);
        utilities.addView(iconButton(android.R.drawable.ic_menu_help,
            getString(R.string.gesture_help), v -> GestureTutorial.show(this, false, null)));
        utilities.addView(iconButton(android.R.drawable.ic_menu_preferences,
            getString(R.string.open_settings), v -> startActivity(new Intent(this, SettingsActivity.class))));

        RelativeLayout.LayoutParams params = new RelativeLayout.LayoutParams(
            ViewGroup.LayoutParams.WRAP_CONTENT, ViewGroup.LayoutParams.WRAP_CONTENT);
        params.addRule(RelativeLayout.ALIGN_PARENT_TOP);
        params.addRule(leftHanded ? RelativeLayout.ALIGN_PARENT_START : RelativeLayout.ALIGN_PARENT_END);
        int safePercent = MobilePreferences.get(this).getInt(MobilePreferences.KEY_SAFE_MARGIN,
            MobilePreferences.DEFAULT_SAFE_MARGIN);
        DisplayMetrics metrics = new DisplayMetrics();
        getWindowManager().getDefaultDisplay().getRealMetrics(metrics);
        int horizontalMargin = Math.round(metrics.widthPixels * safePercent / 100.0f);
        int verticalMargin = Math.round(metrics.heightPixels * safePercent / 100.0f);
        params.topMargin = Math.max(dp(6), verticalMargin);
        if (leftHanded) {
            params.leftMargin = Math.max(dp(8), horizontalMargin);
        } else {
            params.rightMargin = Math.max(dp(8), horizontalMargin);
        }
        mLayout.addView(utilities, params);
    }

    private ImageButton iconButton(int icon, String description, View.OnClickListener listener) {
        android.content.SharedPreferences preferences = MobilePreferences.get(this);
        int opacityPercent = preferences.getInt(MobilePreferences.KEY_TOUCH_OPACITY,
            MobilePreferences.DEFAULT_TOUCH_OPACITY);
        int size = utilityButtonSize();
        int inset = Math.max(dp(8), (size - dp(26)) / 2);
        ImageButton button = new ImageButton(this);
        button.setImageResource(icon);
        button.setColorFilter(Color.WHITE);
        button.setBackgroundColor(Color.argb(Math.round(255 * opacityPercent / 100.0f), 16, 20, 24));
        button.setContentDescription(description);
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.O) {
            button.setTooltipText(description);
        }
        button.setPadding(inset, inset, inset, inset);
        button.setOnClickListener(listener);
        button.setMinimumWidth(size);
        button.setMinimumHeight(size);
        button.setLayoutParams(new LinearLayout.LayoutParams(size, size));
        return button;
    }

    private int dp(int value) {
        return Math.round(value * getResources().getDisplayMetrics().density);
    }

    private boolean usesLeftHandedControls() {
        return "left".equals(MobilePreferences.get(this).getString(MobilePreferences.KEY_HANDEDNESS, "right"));
    }

    private int utilityButtonSize() {
        int uiPercent = MobilePreferences.get(this).getInt(MobilePreferences.KEY_UI_SCALE,
            MobilePreferences.DEFAULT_UI_SCALE);
        return Math.max(dp(MIN_UTILITY_BUTTON_DP), Math.round(dp(DEFAULT_UTILITY_BUTTON_DP) * uiPercent / 100.0f));
    }
}
