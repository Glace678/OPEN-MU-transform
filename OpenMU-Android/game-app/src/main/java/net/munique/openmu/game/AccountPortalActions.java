package net.munique.openmu.game;

import android.app.Activity;
import android.app.AlertDialog;
import android.content.ActivityNotFoundException;
import android.content.Context;
import android.content.Intent;
import android.content.res.Configuration;
import android.net.Uri;
import android.widget.Toast;

import java.util.Locale;

final class AccountPortalActions {
    private AccountPortalActions() {
    }

    static void show(Activity activity) {
        Context text = localized(activity);
        String[] labels = {
            text.getString(R.string.account_register), text.getString(R.string.account_change_password),
            text.getString(R.string.account_reset_password), text.getString(R.string.account_portal_settings)
        };
        String[] actions = { "register", "change-password", "reset-password" };
        new AlertDialog.Builder(activity, android.R.style.Theme_Material_Dialog_Alert)
            .setTitle(text.getString(R.string.account_portal))
            .setItems(labels, (dialog, selected) -> {
                if (selected == actions.length) {
                    activity.startActivity(new Intent(activity, SettingsActivity.class));
                } else {
                    open(activity, actions[selected]);
                }
            }).show();
    }

    static void open(Activity activity, String action) {
        String url = MobilePreferences.accountPortalUrl(activity, action);
        if (url == null) {
            Toast.makeText(activity, localized(activity).getString(R.string.account_portal_not_configured),
                Toast.LENGTH_LONG).show();
            activity.startActivity(new Intent(activity, SettingsActivity.class));
            return;
        }
        Intent browser = new Intent(Intent.ACTION_VIEW, Uri.parse(url));
        browser.addCategory(Intent.CATEGORY_BROWSABLE);
        try {
            activity.startActivity(browser);
        } catch (ActivityNotFoundException | SecurityException ignored) {
            Toast.makeText(activity, localized(activity).getString(R.string.account_portal_no_browser),
                Toast.LENGTH_LONG).show();
        }
    }

    private static Context localized(Activity activity) {
        Configuration configuration = new Configuration(activity.getResources().getConfiguration());
        configuration.setLocale(Locale.forLanguageTag(MobilePreferences.accountPortalLocale(activity)));
        return activity.createConfigurationContext(configuration);
    }
}
