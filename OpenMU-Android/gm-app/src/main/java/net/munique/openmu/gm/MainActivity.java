package net.munique.openmu.gm;

import android.app.Activity;
import android.app.AlertDialog;
import android.content.Context;
import android.content.SharedPreferences;
import android.graphics.Color;
import android.graphics.Typeface;
import android.os.Build;
import android.os.Bundle;
import android.text.InputType;
import android.view.Gravity;
import android.view.View;
import android.view.ViewGroup;
import android.widget.AdapterView;
import android.widget.ArrayAdapter;
import android.widget.Button;
import android.widget.CheckBox;
import android.widget.EditText;
import android.widget.FrameLayout;
import android.widget.ImageButton;
import android.widget.LinearLayout;
import android.widget.ProgressBar;
import android.widget.ScrollView;
import android.widget.SeekBar;
import android.widget.Spinner;
import android.widget.TextView;

import java.util.ArrayList;
import java.util.List;
import java.util.UUID;
import java.util.concurrent.ExecutorService;
import java.util.concurrent.Executors;

public final class MainActivity extends Activity {
    private static final String PREFS = "openmu-gm";
    private static final String KEY_SERVER_URL = "server-url";

    private final ExecutorService networkExecutor = Executors.newSingleThreadExecutor();
    private final List<MobileGmApiClient.CharacterOption> characters = new ArrayList<>();
    private final List<MobileGmApiClient.ItemOption> items = new ArrayList<>();
    private String serverUrl;
    private int requestGeneration;
    private boolean destroyed;
    private boolean grantInFlight;
    private ProgressBar progress;
    private ImageButton refreshButton;
    private TextView connectionStatus;
    private TextView accountValue;
    private Spinner characterSpinner;
    private ArrayAdapter<MobileGmApiClient.CharacterOption> characterAdapter;
    private EditText searchField;
    private Button searchButton;
    private Spinner itemSpinner;
    private ArrayAdapter<MobileGmApiClient.ItemOption> itemAdapter;
    private TextView levelValue;
    private SeekBar levelSeek;
    private Spinner quantitySpinner;
    private CheckBox skillCheck;
    private CheckBox luckCheck;
    private Spinner additionalSpinner;
    private LinearLayout excellentGroup;
    private final List<CheckBox> excellentChecks = new ArrayList<>();
    private Button grantButton;
    private TextView resultText;
    private TextView zenBalance;
    private EditText zenAmountField;
    private Button grantZenButton;

    @Override
    protected void onCreate(Bundle state) {
        super.onCreate(state);
        getWindow().setStatusBarColor(Color.rgb(21, 25, 30));
        getWindow().setNavigationBarColor(Color.rgb(21, 25, 30));
        SharedPreferences preferences = getSharedPreferences(PREFS, Context.MODE_PRIVATE);
        serverUrl = normalizeUrl(preferences.getString(KEY_SERVER_URL, BuildConfig.DEFAULT_SERVER_URL));
        if (!ServerAddressPolicy.isAllowed(serverUrl)) {
            serverUrl = BuildConfig.DEFAULT_SERVER_URL;
            preferences.edit().putString(KEY_SERVER_URL, serverUrl).apply();
        }
        setContentView(createContent());
        loadStatus();
    }

    private View createContent() {
        LinearLayout root = new LinearLayout(this);
        root.setOrientation(LinearLayout.VERTICAL);
        root.setBackgroundColor(Color.rgb(246, 247, 249));

        LinearLayout toolbar = new LinearLayout(this);
        toolbar.setGravity(Gravity.CENTER_VERTICAL);
        toolbar.setPadding(dp(16), 0, dp(4), 0);
        toolbar.setBackgroundColor(Color.rgb(21, 25, 30));
        toolbar.setMinimumHeight(dp(56));
        TextView title = new TextView(this);
        title.setText(R.string.app_name);
        title.setTextColor(Color.WHITE);
        title.setTextSize(18);
        title.setTypeface(Typeface.DEFAULT, Typeface.BOLD);
        title.setGravity(Gravity.CENTER_VERTICAL);
        toolbar.addView(title, new LinearLayout.LayoutParams(0, dp(56), 1));
        refreshButton = iconButton(android.R.drawable.ic_popup_sync,
            getString(R.string.menu_refresh), view -> loadStatus());
        toolbar.addView(refreshButton);
        toolbar.addView(iconButton(android.R.drawable.ic_menu_preferences,
            getString(R.string.menu_server), view -> showServerDialog()));
        root.addView(toolbar, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, dp(56)));

        progress = new ProgressBar(this, null, android.R.attr.progressBarStyleHorizontal);
        progress.setIndeterminate(true);
        progress.setVisibility(View.GONE);
        root.addView(progress, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, dp(3)));

        ScrollView scroll = new ScrollView(this);
        scroll.setFillViewport(true);
        LinearLayout form = new LinearLayout(this);
        form.setOrientation(LinearLayout.VERTICAL);
        form.setPadding(dp(16), dp(14), dp(16), dp(28));
        scroll.addView(form, new ScrollView.LayoutParams(
            ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT));

        connectionStatus = bodyText(R.string.status_connecting);
        connectionStatus.setTextColor(Color.rgb(92, 99, 108));
        form.addView(connectionStatus, matchWrap());
        form.addView(sectionTitle(R.string.section_account), topMargin(18));
        accountValue = bodyText(R.string.account_unknown);
        form.addView(accountValue, matchMinHeight(44));
        form.addView(fieldLabel(R.string.character_label), topMargin(10));
        characterSpinner = new Spinner(this);
        characterSpinner.setMinimumHeight(dp(48));
        characterAdapter = new ArrayAdapter<>(this, android.R.layout.simple_spinner_item, characters);
        characterAdapter.setDropDownViewResource(android.R.layout.simple_spinner_dropdown_item);
        characterSpinner.setAdapter(characterAdapter);
        characterSpinner.setOnItemSelectedListener(new AdapterView.OnItemSelectedListener() {
            @Override
            public void onItemSelected(AdapterView<?> parent, View view, int position, long id) {
                updateZenBalance();
            }

            @Override
            public void onNothingSelected(AdapterView<?> parent) {
                updateZenBalance();
            }
        });
        form.addView(characterSpinner, matchMinHeight(48));

        form.addView(sectionTitle(R.string.section_item), topMargin(20));
        searchField = new EditText(this);
        searchField.setSingleLine(true);
        searchField.setTextSize(16);
        searchField.setHint(R.string.item_search_hint);
        searchField.setInputType(InputType.TYPE_CLASS_TEXT);
        searchField.setMinimumHeight(dp(48));
        form.addView(searchField, matchMinHeight(48));
        searchButton = commandButton(R.string.item_search_button, view -> searchItems());
        form.addView(searchButton, topMarginWithHeight(8, 48));

        form.addView(fieldLabel(R.string.item_result_label), topMargin(12));
        itemSpinner = new Spinner(this);
        itemSpinner.setMinimumHeight(dp(48));
        itemAdapter = new ArrayAdapter<>(this, android.R.layout.simple_spinner_item, items);
        itemAdapter.setDropDownViewResource(android.R.layout.simple_spinner_dropdown_item);
        itemSpinner.setAdapter(itemAdapter);
        itemSpinner.setOnItemSelectedListener(new AdapterView.OnItemSelectedListener() {
            @Override
            public void onItemSelected(AdapterView<?> parent, View view, int position, long id) {
                applySelectedItem();
            }

            @Override
            public void onNothingSelected(AdapterView<?> parent) {
                applySelectedItem();
            }
        });
        form.addView(itemSpinner, matchMinHeight(48));

        levelValue = fieldLabel(R.string.level_default);
        form.addView(levelValue, topMargin(12));
        levelSeek = new SeekBar(this);
        levelSeek.setMin(0);
        levelSeek.setMax(0);
        levelSeek.setMinimumHeight(dp(44));
        levelSeek.setOnSeekBarChangeListener(new SeekBar.OnSeekBarChangeListener() {
            @Override
            public void onProgressChanged(SeekBar seekBar, int value, boolean fromUser) {
                updateLevelText();
            }

            @Override public void onStartTrackingTouch(SeekBar seekBar) { }
            @Override public void onStopTrackingTouch(SeekBar seekBar) { }
        });
        form.addView(levelSeek, matchMinHeight(44));

        form.addView(fieldLabel(R.string.quantity_label), topMargin(8));
        quantitySpinner = new Spinner(this);
        quantitySpinner.setMinimumHeight(dp(48));
        List<Integer> quantities = new ArrayList<>();
        for (int value = 1; value <= 10; value++) {
            quantities.add(value);
        }
        ArrayAdapter<Integer> quantityAdapter = new ArrayAdapter<>(
            this, android.R.layout.simple_spinner_item, quantities);
        quantityAdapter.setDropDownViewResource(android.R.layout.simple_spinner_dropdown_item);
        quantitySpinner.setAdapter(quantityAdapter);
        form.addView(quantitySpinner, matchMinHeight(48));

        skillCheck = new CheckBox(this);
        skillCheck.setText(R.string.has_skill);
        skillCheck.setTextSize(16);
        skillCheck.setMinimumHeight(dp(48));
        form.addView(skillCheck, topMarginWithHeight(8, 48));

        luckCheck = new CheckBox(this);
        luckCheck.setText(R.string.has_luck);
        luckCheck.setTextSize(16);
        luckCheck.setMinimumHeight(dp(48));
        form.addView(luckCheck, topMarginWithHeight(4, 48));

        form.addView(fieldLabel(R.string.additional_option_label), topMargin(8));
        additionalSpinner = new Spinner(this);
        additionalSpinner.setMinimumHeight(dp(48));
        ArrayAdapter<String> additionalAdapter = new ArrayAdapter<>(this,
            android.R.layout.simple_spinner_item, new String[] {
                getString(R.string.additional_none), "+4", "+8", "+12", "+16"
            });
        additionalAdapter.setDropDownViewResource(android.R.layout.simple_spinner_dropdown_item);
        additionalSpinner.setAdapter(additionalAdapter);
        form.addView(additionalSpinner, matchMinHeight(48));

        form.addView(fieldLabel(R.string.excellent_label), topMargin(12));
        excellentGroup = new LinearLayout(this);
        excellentGroup.setOrientation(LinearLayout.VERTICAL);
        form.addView(excellentGroup, matchWrap());

        grantButton = commandButton(R.string.grant_button, view -> grantSelectedItem());
        form.addView(grantButton, topMarginWithHeight(16, 52));
        resultText = bodyText(R.string.result_ready);
        resultText.setMinHeight(dp(44));
        resultText.setGravity(Gravity.CENTER_VERTICAL);
        resultText.setAccessibilityLiveRegion(View.ACCESSIBILITY_LIVE_REGION_POLITE);
        form.addView(resultText, topMargin(8));

        form.addView(sectionTitle(R.string.section_zen), topMargin(24));
        zenBalance = bodyText(R.string.zen_balance_unknown);
        form.addView(zenBalance, matchMinHeight(40));
        zenAmountField = new EditText(this);
        zenAmountField.setSingleLine(true);
        zenAmountField.setTextSize(16);
        zenAmountField.setHint(R.string.zen_amount_hint);
        zenAmountField.setInputType(InputType.TYPE_CLASS_NUMBER);
        zenAmountField.setMinimumHeight(dp(48));
        zenAmountField.setText("1000000");
        form.addView(zenAmountField, matchMinHeight(48));
        LinearLayout zenPresets = new LinearLayout(this);
        zenPresets.setOrientation(LinearLayout.HORIZONTAL);
        zenPresets.setWeightSum(4);
        addZenPreset(zenPresets, R.string.zen_preset_1m, 1_000_000L);
        addZenPreset(zenPresets, R.string.zen_preset_10m, 10_000_000L);
        addZenPreset(zenPresets, R.string.zen_preset_100m, 100_000_000L);
        addZenPreset(zenPresets, R.string.zen_preset_max, 2_000_000_000L);
        form.addView(zenPresets, topMargin(6));
        grantZenButton = commandButton(R.string.zen_grant_button, view -> grantZen());
        form.addView(grantZenButton, topMarginWithHeight(10, 52));

        root.addView(scroll, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, 0, 1));
        SafeArea.apply(root);
        updateGrantAvailability();
        return root;
    }

    private void loadStatus() {
        int generation = ++requestGeneration;
        grantInFlight = false;
        searchButton.setEnabled(true);
        setBusy(true);
        connectionStatus.setText(R.string.status_connecting);
        accountValue.setText(R.string.account_unknown);
        characters.clear();
        characterAdapter.notifyDataSetChanged();
        updateGrantAvailability();
        MobileGmApiClient client = client();
        networkExecutor.execute(() -> {
            try {
                MobileGmApiClient.Status status = client.loadStatus();
                runOnUiThread(() -> {
                    if (!accept(generation)) return;
                    setBusy(false);
                    connectionStatus.setText(getString(R.string.status_connected, serverUrl));
                    accountValue.setText(status.accountName.isEmpty()
                        ? getString(R.string.account_unknown)
                        : getString(R.string.account_value, status.accountName));
                    characters.addAll(status.characters);
                    characterAdapter.notifyDataSetChanged();
                    if (characters.isEmpty()) resultText.setText(R.string.no_online_characters);
                    updateZenBalance();
                    updateGrantAvailability();
                });
            } catch (MobileGmApiClient.ApiException error) {
                showFailure(generation, error, true);
            }
        });
    }

    private void searchItems() {
        final int generation = requestGeneration;
        final String query = searchField.getText().toString();
        searchButton.setEnabled(false);
        resultText.setText(R.string.searching_items);
        MobileGmApiClient client = client();
        networkExecutor.execute(() -> {
            try {
                List<MobileGmApiClient.ItemOption> found = client.searchItems(query);
                runOnUiThread(() -> {
                    if (!accept(generation)) return;
                    searchButton.setEnabled(true);
                    items.clear();
                    items.addAll(found);
                    itemAdapter.notifyDataSetChanged();
                    applySelectedItem();
                    resultText.setText(found.isEmpty() ? getString(R.string.no_items_found)
                        : getResources().getQuantityString(R.plurals.items_found, found.size(), found.size()));
                });
            } catch (MobileGmApiClient.ApiException error) {
                showFailure(generation, error, false);
            }
        });
    }

    private void grantSelectedItem() {
        int characterIndex = characterSpinner.getSelectedItemPosition();
        int itemIndex = itemSpinner.getSelectedItemPosition();
        if (grantInFlight || characterIndex < 0 || itemIndex < 0) return;
        MobileGmApiClient.CharacterOption character = characters.get(characterIndex);
        MobileGmApiClient.ItemOption item = items.get(itemIndex);
        int quantity = (Integer) quantitySpinner.getSelectedItem();
        MobileGmApiClient.GrantRequest request = new MobileGmApiClient.GrantRequest(
            UUID.randomUUID().toString(), character.id, item.group, item.number,
            levelSeek.getProgress(), quantity,
            skillCheck.isEnabled() && skillCheck.isChecked(),
            luckCheck.isEnabled() && luckCheck.isChecked(),
            additionalSpinner.isEnabled() ? additionalSpinner.getSelectedItemPosition() : 0,
            excellentMask());
        final int generation = requestGeneration;
        grantInFlight = true;
        updateGrantAvailability();
        resultText.setText(R.string.granting_item);
        MobileGmApiClient client = client();
        networkExecutor.execute(() -> {
            try {
                MobileGmApiClient.GrantResult result = client.grantItem(request);
                runOnUiThread(() -> {
                    if (!accept(generation)) return;
                    grantInFlight = false;
                    updateGrantAvailability();
                    resultText.setText(result.message);
                    loadStatus();
                });
            } catch (MobileGmApiClient.ApiException error) {
                runOnUiThread(() -> {
                    if (!accept(generation)) return;
                    grantInFlight = false;
                    updateGrantAvailability();
                    resultText.setText(getString(R.string.request_failed, error.getMessage()));
                });
            }
        });
    }

    private void grantZen() {
        int characterIndex = characterSpinner.getSelectedItemPosition();
        if (grantInFlight || characterIndex < 0) return;
        MobileGmApiClient.CharacterOption character = characters.get(characterIndex);
        long amount;
        try {
            amount = Long.parseLong(zenAmountField.getText().toString().trim());
        } catch (NumberFormatException error) {
            resultText.setText(R.string.zen_amount_invalid);
            return;
        }
        if (amount <= 0 || amount > 2_000_000_000L) {
            resultText.setText(R.string.zen_amount_invalid);
            return;
        }
        MobileGmApiClient.ZenRequest request = new MobileGmApiClient.ZenRequest(
            UUID.randomUUID().toString(), character.id, amount);
        final int generation = requestGeneration;
        grantInFlight = true;
        updateGrantAvailability();
        resultText.setText(R.string.granting_zen);
        MobileGmApiClient client = client();
        networkExecutor.execute(() -> {
            try {
                MobileGmApiClient.GrantResult result = client.grantZen(request);
                runOnUiThread(() -> {
                    if (!accept(generation)) return;
                    grantInFlight = false;
                    updateGrantAvailability();
                    resultText.setText(result.message);
                    loadStatus();
                });
            } catch (MobileGmApiClient.ApiException error) {
                runOnUiThread(() -> {
                    if (!accept(generation)) return;
                    grantInFlight = false;
                    updateGrantAvailability();
                    resultText.setText(getString(R.string.request_failed, error.getMessage()));
                });
            }
        });
    }

    private void showFailure(int generation, Exception error, boolean statusRequest) {
        runOnUiThread(() -> {
            if (!accept(generation)) return;
            if (statusRequest) {
                setBusy(false);
                connectionStatus.setText(getString(R.string.status_failed, error.getMessage()));
            } else {
                searchButton.setEnabled(true);
            }
            resultText.setText(getString(R.string.request_failed, error.getMessage()));
            updateGrantAvailability();
        });
    }

    private void applySelectedItem() {
        int position = itemSpinner.getSelectedItemPosition();
        MobileGmApiClient.ItemOption item =
            (position >= 0 && position < items.size()) ? items.get(position) : null;
        if (item == null) {
            levelSeek.setMax(0);
            levelSeek.setProgress(0);
            skillCheck.setChecked(false);
            skillCheck.setEnabled(false);
            luckCheck.setChecked(false);
            luckCheck.setEnabled(false);
            additionalSpinner.setSelection(0);
            additionalSpinner.setEnabled(false);
            rebuildExcellentChecks(0);
        } else {
            levelSeek.setMax(item.maxLevel);
            levelSeek.setProgress(Math.min(levelSeek.getProgress(), item.maxLevel));
            skillCheck.setEnabled(item.canHaveSkill);
            if (!item.canHaveSkill) skillCheck.setChecked(false);
            luckCheck.setEnabled(item.canHaveLuck);
            if (!item.canHaveLuck) luckCheck.setChecked(false);
            additionalSpinner.setEnabled(item.canHaveAdditional);
            if (!item.canHaveAdditional) additionalSpinner.setSelection(0);
            rebuildExcellentChecks(item.excellentCount);
        }
        updateLevelText();
        updateGrantAvailability();
    }

    private void rebuildExcellentChecks(int count) {
        excellentGroup.removeAllViews();
        excellentChecks.clear();
        if (count <= 0) {
            TextView none = bodyText(R.string.excellent_none);
            none.setTextColor(Color.rgb(140, 146, 154));
            none.setMinHeight(dp(40));
            excellentGroup.addView(none, matchWrap());
            return;
        }
        int options = Math.min(count, 12);
        CheckBox all = new CheckBox(this);
        all.setText(R.string.excellent_all);
        all.setTextSize(16);
        all.setMinimumHeight(dp(44));
        excellentGroup.addView(all, matchMinHeight(44));
        for (int i = 1; i <= options; i++) {
            final int optionNumber = i;
            CheckBox box = new CheckBox(this);
            box.setText(getString(R.string.excellent_option, optionNumber));
            box.setTextSize(16);
            box.setMinimumHeight(dp(44));
            excellentChecks.add(box);
            excellentGroup.addView(box, matchMinHeight(44));
        }
        all.setOnCheckedChangeListener((button, checked) -> {
            for (CheckBox box : excellentChecks) box.setChecked(checked);
        });
    }

    private int excellentMask() {
        int mask = 0;
        for (int i = 0; i < excellentChecks.size(); i++) {
            if (excellentChecks.get(i).isChecked()) {
                mask |= (1 << i);
            }
        }
        return mask;
    }

    private void addZenPreset(LinearLayout row, int labelRes, long amount) {
        Button preset = commandButton(labelRes, view -> zenAmountField.setText(String.valueOf(amount)));
        preset.setTextSize(13);
        LinearLayout.LayoutParams params = new LinearLayout.LayoutParams(0, dp(44), 1);
        params.setMarginEnd(dp(6));
        row.addView(preset, params);
    }

    private void updateLevelText() {
        levelValue.setText(getString(R.string.level_value, levelSeek.getProgress(), levelSeek.getMax()));
    }

    private void updateGrantAvailability() {
        if (grantButton != null) {
            grantButton.setEnabled(!grantInFlight && !characters.isEmpty() && !items.isEmpty());
        }
        if (grantZenButton != null) {
            grantZenButton.setEnabled(!grantInFlight && !characters.isEmpty());
        }
    }

    private void updateZenBalance() {
        if (zenBalance == null) return;
        int index = characterSpinner != null ? characterSpinner.getSelectedItemPosition() : -1;
        if (index < 0 || index >= characters.size() || characters.get(index).money < 0) {
            zenBalance.setText(R.string.zen_balance_unknown);
        } else {
            zenBalance.setText(getString(R.string.zen_balance_label,
                String.format(java.util.Locale.US, "%,d", characters.get(index).money)));
        }
    }

    private void setBusy(boolean busy) {
        progress.setVisibility(busy ? View.VISIBLE : View.GONE);
        refreshButton.setEnabled(!busy);
    }

    private MobileGmApiClient client() {
        return new MobileGmApiClient(serverUrl, BuildConfig.MOBILE_PACKAGE_KEY);
    }

    private boolean accept(int generation) {
        return !destroyed && generation == requestGeneration;
    }

    private void showServerDialog() {
        EditText field = new EditText(this);
        field.setSingleLine(true);
        field.setInputType(InputType.TYPE_CLASS_TEXT | InputType.TYPE_TEXT_VARIATION_URI);
        field.setHint(R.string.server_hint);
        field.setText(serverUrl);
        field.setSelectAllOnFocus(true);
        field.setMinimumHeight(dp(48));
        FrameLayout holder = new FrameLayout(this);
        holder.setPadding(dp(20), 0, dp(20), 0);
        holder.addView(field, new FrameLayout.LayoutParams(
            ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT));
        AlertDialog dialog = new AlertDialog.Builder(this)
            .setTitle(R.string.server_title).setView(holder)
            .setNegativeButton(R.string.server_cancel, null)
            .setPositiveButton(R.string.server_connect, null).create();
        dialog.setOnShowListener(ignored -> dialog.getButton(AlertDialog.BUTTON_POSITIVE)
            .setOnClickListener(view -> {
                final String candidate = normalizeUrl(field.getText().toString());
                if (!ServerAddressPolicy.isAllowed(candidate)) {
                    field.setError(getString(R.string.invalid_server));
                    return;
                }
                // https to a public host passes the policy, but the pairing key
                // is then sent there on every request. Make that explicit before
                // saving the address, so a mistyped URL cannot exfiltrate it.
                if (!ServerAddressPolicy.isLocal(candidate)) {
                    confirmPublicServer(candidate, () -> applyServerUrl(candidate, dialog));
                    return;
                }
                applyServerUrl(candidate, dialog);
            }));
        dialog.show();
    }

    private void applyServerUrl(String candidate, AlertDialog dialog) {
        serverUrl = candidate;
        getSharedPreferences(PREFS, Context.MODE_PRIVATE).edit()
            .putString(KEY_SERVER_URL, serverUrl).apply();
        dialog.dismiss();
        loadStatus();
    }

    private void confirmPublicServer(String candidate, Runnable onConfirm) {
        new AlertDialog.Builder(this)
            .setTitle(R.string.server_public_title)
            .setMessage(getString(R.string.server_public_warning, candidate))
            .setNegativeButton(R.string.server_cancel, null)
            .setPositiveButton(R.string.server_public_confirm,
                (warningDialog, which) -> onConfirm.run())
            .show();
    }

    private ImageButton iconButton(int icon, String description, View.OnClickListener listener) {
        ImageButton button = new ImageButton(this);
        button.setImageResource(icon);
        button.setContentDescription(description);
        button.setColorFilter(Color.WHITE);
        button.setBackgroundColor(Color.TRANSPARENT);
        button.setPadding(dp(14), dp(14), dp(14), dp(14));
        button.setOnClickListener(listener);
        button.setTooltipText(description);
        button.setMinimumWidth(dp(52));
        button.setMinimumHeight(dp(52));
        button.setLayoutParams(new LinearLayout.LayoutParams(dp(52), dp(52)));
        return button;
    }

    private Button commandButton(int text, View.OnClickListener listener) {
        Button button = new Button(this);
        button.setText(text);
        button.setTextSize(16);
        button.setAllCaps(false);
        button.setMinHeight(dp(48));
        button.setOnClickListener(listener);
        return button;
    }

    private TextView sectionTitle(int text) {
        TextView view = bodyText(text);
        view.setTextSize(17);
        view.setTypeface(Typeface.DEFAULT, Typeface.BOLD);
        view.setGravity(Gravity.CENTER_VERTICAL);
        view.setMinHeight(dp(36));
        return view;
    }

    private TextView fieldLabel(int text) {
        TextView view = bodyText(text);
        view.setTextColor(Color.rgb(65, 71, 80));
        view.setGravity(Gravity.CENTER_VERTICAL);
        view.setMinHeight(dp(32));
        return view;
    }

    private TextView bodyText(int text) {
        TextView view = new TextView(this);
        view.setText(text);
        view.setTextSize(16);
        view.setTextColor(Color.rgb(32, 36, 42));
        view.setLineSpacing(0, 1.15f);
        return view;
    }

    private LinearLayout.LayoutParams matchWrap() {
        return new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT,
            ViewGroup.LayoutParams.WRAP_CONTENT);
    }

    private LinearLayout.LayoutParams matchMinHeight(int height) {
        return new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, dp(height));
    }

    private LinearLayout.LayoutParams topMargin(int margin) {
        LinearLayout.LayoutParams params = matchWrap();
        params.topMargin = dp(margin);
        return params;
    }

    private LinearLayout.LayoutParams topMarginWithHeight(int margin, int height) {
        LinearLayout.LayoutParams params = matchMinHeight(height);
        params.topMargin = dp(margin);
        return params;
    }

    private static String normalizeUrl(String raw) {
        String value = raw == null ? "" : raw.trim();
        while (value.endsWith("/")) value = value.substring(0, value.length() - 1);
        return value;
    }

    private int dp(int value) {
        return Math.round(value * getResources().getDisplayMetrics().density);
    }

    @Override
    protected void onDestroy() {
        destroyed = true;
        requestGeneration++;
        networkExecutor.shutdownNow();
        super.onDestroy();
    }
}
