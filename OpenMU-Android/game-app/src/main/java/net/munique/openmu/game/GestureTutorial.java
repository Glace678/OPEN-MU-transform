package net.munique.openmu.game;

import android.app.Activity;
import android.app.AlertDialog;

final class GestureTutorial {
    private GestureTutorial() {
    }

    static void show(Activity activity, boolean enteringGame, Runnable afterConfirm) {
        AlertDialog.Builder builder = new AlertDialog.Builder(activity, android.R.style.Theme_Material_Dialog_Alert)
            .setTitle(R.string.tutorial_title)
            .setMessage(R.string.tutorial_body);

        if (enteringGame) {
            builder
                .setCancelable(false)
                .setNegativeButton(R.string.settings, null)
                .setPositiveButton(R.string.understood, null);
        } else {
            builder.setPositiveButton(R.string.help_close, null);
        }

        AlertDialog dialog = builder.create();

        dialog.setOnShowListener(ignored -> {
            if (enteringGame) {
                dialog.getButton(AlertDialog.BUTTON_NEGATIVE).setOnClickListener(v ->
                    activity.startActivity(new android.content.Intent(activity, SettingsActivity.class)));
                dialog.getButton(AlertDialog.BUTTON_POSITIVE).setOnClickListener(v -> {
                    dialog.dismiss();
                    if (afterConfirm != null) {
                        afterConfirm.run();
                    }
                });
            }
        });
        dialog.show();
    }
}
