package net.munique.openmu.game;

import android.app.Activity;
import android.content.Intent;
import android.graphics.Color;
import android.graphics.Typeface;
import android.graphics.drawable.GradientDrawable;
import android.os.Bundle;
import android.os.Handler;
import android.os.Looper;
import android.view.Gravity;
import android.view.View;
import android.view.ViewGroup;
import android.widget.Button;
import android.widget.LinearLayout;
import android.widget.ImageButton;
import android.widget.ProgressBar;
import android.widget.TextView;

import java.io.IOException;
import java.util.concurrent.ExecutorService;
import java.util.concurrent.Executors;

public final class BootstrapActivity extends Activity {
    private static final ExecutorService GAME_DATA_WORKER = Executors.newSingleThreadExecutor();

    private final Handler main = new Handler(Looper.getMainLooper());
    private boolean preparing;
    private boolean destroyed;
    private TextView status;
    private TextView detail;
    private ProgressBar progress;
    private Button play;
    private Button retry;

    @Override
    protected void onCreate(Bundle state) {
        super.onCreate(state);
        getWindow().setStatusBarColor(Color.rgb(8, 9, 12));
        getWindow().setNavigationBarColor(Color.rgb(8, 9, 12));
        setContentView(createContent());
        if (MobilePreferences.isGameDataReady(this)) {
            showReady();
        } else {
            prepareGameData();
        }
    }

    private View createContent() {
        LinearLayout root = new LinearLayout(this);
        root.setOrientation(LinearLayout.HORIZONTAL);
        root.setPadding(dp(28), dp(20), dp(28), dp(20));
        root.setGravity(Gravity.CENTER_VERTICAL);
        GradientDrawable backdrop = new GradientDrawable(
            GradientDrawable.Orientation.TL_BR,
            new int[] { Color.rgb(8, 9, 12), Color.rgb(20, 15, 19), Color.rgb(38, 13, 13) });
        root.setBackground(backdrop);

        LinearLayout copy = new LinearLayout(this);
        copy.setOrientation(LinearLayout.VERTICAL);
        copy.setGravity(Gravity.CENTER_VERTICAL);
        root.addView(copy, new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.MATCH_PARENT, 3));

        TextView brand = new TextView(this);
        brand.setText(R.string.app_name);
        brand.setTextColor(Color.rgb(201, 162, 75));
        brand.setTextSize(18);
        brand.setTypeface(Typeface.DEFAULT_BOLD);
        brand.setLetterSpacing(0.16f);
        copy.addView(brand);

        status = new TextView(this);
        status.setText(R.string.preparing_title);
        status.setTextColor(Color.WHITE);
        status.setTextSize(28);
        status.setTypeface(Typeface.DEFAULT_BOLD);
        status.setShadowLayer(14f, 0f, 3f, Color.argb(170, 0, 0, 0));
        status.setPadding(0, dp(10), 0, dp(8));
        copy.addView(status);

        detail = new TextView(this);
        detail.setText(R.string.preparing_detail);
        detail.setTextColor(Color.rgb(190, 197, 204));
        detail.setTextSize(15);
        detail.setMaxLines(3);
        copy.addView(detail);

        progress = new ProgressBar(this, null, android.R.attr.progressBarStyleHorizontal);
        progress.setMax(100);
        progress.setProgressTintList(android.content.res.ColorStateList.valueOf(Color.rgb(194, 58, 40)));
        progress.setProgressBackgroundTintList(android.content.res.ColorStateList.valueOf(Color.rgb(42, 46, 53)));
        LinearLayout.LayoutParams progressParams = new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, dp(6));
        progressParams.topMargin = dp(22);
        copy.addView(progress, progressParams);

        LinearLayout actions = new LinearLayout(this);
        actions.setOrientation(LinearLayout.VERTICAL);
        actions.setGravity(Gravity.CENTER);
        LinearLayout.LayoutParams actionsParams = new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.MATCH_PARENT, 2);
        actionsParams.leftMargin = dp(30);
        root.addView(actions, actionsParams);

        play = actionButton(R.string.start_game, true, v -> startGame());
        play.setEnabled(false);
        actions.addView(play);
        LinearLayout accountRow = new LinearLayout(this);
        accountRow.setGravity(Gravity.CENTER_VERTICAL);
        Button settings = actionButton(R.string.settings, false,
            v -> startActivity(new Intent(this, SettingsActivity.class)));
        accountRow.addView(settings, new LinearLayout.LayoutParams(0, dp(54), 1));
        ImageButton account = new ImageButton(this);
        account.setImageResource(android.R.drawable.ic_menu_myplaces);
        account.setColorFilter(Color.WHITE);
        GradientDrawable accountBg = new GradientDrawable(
            GradientDrawable.Orientation.TOP_BOTTOM,
            new int[] { Color.rgb(45, 51, 59), Color.rgb(29, 33, 39) });
        accountBg.setCornerRadius(dp(14));
        accountBg.setStroke(dp(1), Color.rgb(76, 84, 95));
        account.setBackground(accountBg);
        account.setContentDescription(getString(R.string.account_portal));
        account.setTooltipText(getString(R.string.account_portal));
        account.setPadding(dp(12), dp(12), dp(12), dp(12));
        account.setOnClickListener(v -> AccountPortalActions.show(this));
        LinearLayout.LayoutParams accountParams = new LinearLayout.LayoutParams(dp(54), dp(54));
        accountParams.leftMargin = dp(8);
        accountRow.addView(account, accountParams);
        LinearLayout.LayoutParams rowParams = new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, dp(54));
        rowParams.bottomMargin = dp(10);
        actions.addView(accountRow, rowParams);
        actions.addView(actionButton(R.string.gesture_help, false,
            v -> GestureTutorial.show(this, false, null)));
        retry = actionButton(R.string.retry, false, v -> prepareGameData());
        retry.setVisibility(View.GONE);
        actions.addView(retry);
        SafeArea.apply(root);
        return root;
    }

    private Button actionButton(int label, boolean primary, View.OnClickListener listener) {
        Button button = new Button(this);
        button.setText(label);
        button.setTextSize(16);
        button.setAllCaps(false);
        button.setMinWidth(0);
        button.setMinHeight(dp(50));
        button.setTextColor(primary ? Color.rgb(255, 236, 214) : Color.rgb(224, 229, 235));
        button.setTypeface(Typeface.DEFAULT_BOLD);
        GradientDrawable buttonBg = new GradientDrawable(
            GradientDrawable.Orientation.TOP_BOTTOM,
            primary
                ? new int[] { Color.rgb(184, 54, 38), Color.rgb(118, 22, 18) }
                : new int[] { Color.rgb(45, 51, 59), Color.rgb(29, 33, 39) });
        buttonBg.setCornerRadius(dp(14));
        buttonBg.setStroke(dp(1), primary ? Color.rgb(201, 162, 75) : Color.rgb(76, 84, 95));
        button.setBackground(buttonBg);
        button.setStateListAnimator(null);
        button.setElevation(0f);
        button.setOnClickListener(listener);
        LinearLayout.LayoutParams params = new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, dp(54));
        params.bottomMargin = dp(10);
        button.setLayoutParams(params);
        return button;
    }

    private void prepareGameData() {
        if (preparing) {
            return;
        }
        preparing = true;
        play.setEnabled(false);
        retry.setVisibility(View.GONE);
        progress.setVisibility(View.VISIBLE);
        progress.setProgress(0);
        status.setText(R.string.preparing_title);
        detail.setText(R.string.preparing_detail);
        GAME_DATA_WORKER.execute(() -> {
            try {
                MobilePreferences.extractGameData(getApplicationContext(), (percent, file) -> postToUi(() -> {
                    progress.setProgress(percent);
                    if (!file.isEmpty()) {
                        detail.setText(file);
                    }
                }));
                postToUi(() -> {
                    preparing = false;
                    showReady();
                });
            } catch (IOException error) {
                postToUi(() -> {
                    preparing = false;
                    showFailure(error.getMessage());
                });
            }
        });
    }

    private void postToUi(Runnable action) {
        main.post(() -> {
            if (!destroyed && !isFinishing() && !isDestroyed()) {
                action.run();
            }
        });
    }

    private void showReady() {
        status.setText(R.string.ready);
        detail.setText(getString(R.string.current_display,
            getResources().getDisplayMetrics().widthPixels,
            getResources().getDisplayMetrics().heightPixels));
        progress.setProgress(100);
        progress.setVisibility(View.INVISIBLE);
        play.setEnabled(true);
        retry.setVisibility(View.GONE);
    }

    private void showFailure(String message) {
        status.setText(R.string.extract_failed);
        detail.setText(message == null ? getString(R.string.extract_failed) : message);
        progress.setVisibility(View.INVISIBLE);
        retry.setVisibility(View.VISIBLE);
    }

    private void startGame() {
        try {
            MobilePreferences.writeConfig(this);
        } catch (IOException error) {
            showFailure(error.getMessage());
            return;
        }

        String shownFor = MobilePreferences.get(this).getString(MobilePreferences.KEY_TUTORIAL_VERSION, "");
        if (!BuildConfig.GAME_DATA_VERSION.equals(shownFor)) {
            GestureTutorial.show(this, true, () -> {
                MobilePreferences.get(this).edit()
                    .putString(MobilePreferences.KEY_TUTORIAL_VERSION, BuildConfig.GAME_DATA_VERSION)
                    .apply();
                launchNativeGame();
            });
        } else {
            launchNativeGame();
        }
    }

    private void launchNativeGame() {
        Intent game = new Intent(this, GameActivity.class);
        if (BuildConfig.DEBUG && getIntent().getBooleanExtra("inputDiagnostics", false)) {
            game.putExtra("inputDiagnostics", true);
        }
        startActivity(game);
    }

    @Override
    protected void onResume() {
        super.onResume();
        if (play != null && MobilePreferences.isGameDataReady(this)) {
            showReady();
        }
    }

    @Override
    protected void onDestroy() {
        destroyed = true;
        main.removeCallbacksAndMessages(null);
        super.onDestroy();
    }

    private int dp(int value) {
        return Math.round(value * getResources().getDisplayMetrics().density);
    }
}
