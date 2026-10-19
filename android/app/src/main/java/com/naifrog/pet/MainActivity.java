package com.naifrog.pet;

import android.Manifest;
import android.app.Activity;
import android.app.AlertDialog;
import android.content.Intent;
import android.content.pm.PackageManager;
import android.graphics.Color;
import android.graphics.Rect;
import android.net.Uri;
import android.os.Build;
import android.os.Bundle;
import android.provider.Settings;
import android.view.Gravity;
import android.view.View;
import android.view.WindowInsets;
import android.view.WindowManager;
import android.widget.Button;
import android.widget.EditText;
import android.widget.FrameLayout;
import android.widget.ImageView;
import android.widget.LinearLayout;
import android.widget.TextView;

public final class MainActivity extends Activity {
    private FrameLayout root;
    private PetView pet;
    private TextView status;
    private boolean keyboard;
    private float beforeX, beforeY, dragX, dragY;
    private Rect keyboardRect;
    private int legacyVisibleBottom;

    private int dp(float v) {
        return Math.round(v * getResources().getDisplayMetrics().density);
    }

    @Override
    public void onCreate(Bundle state) {
        super.onCreate(state);
        getWindow().setSoftInputMode(WindowManager.LayoutParams.SOFT_INPUT_ADJUST_RESIZE);
        if (Build.VERSION.SDK_INT >= 30) getWindow().setDecorFitsSystemWindows(true);
        root = new FrameLayout(this);
        root.setBackgroundColor(Color.rgb(255, 249, 232));
        setContentView(root);
        LinearLayout controls = new LinearLayout(this);
        controls.setOrientation(LinearLayout.VERTICAL);
        controls.setPadding(dp(24), dp(24), dp(24), 0);
        root.addView(controls, new FrameLayout.LayoutParams(-1, -2, Gravity.TOP));
        LinearLayout brand = new LinearLayout(this);
        brand.setGravity(Gravity.CENTER_VERTICAL);
        ImageView cover = new ImageView(this);
        cover.setImageResource(R.drawable.naifrog);
        cover.setContentDescription("奶蛙应用封面");
        brand.addView(cover, new LinearLayout.LayoutParams(dp(56), dp(56)));
        TextView title = new TextView(this);
        title.setText("奶蛙");
        title.setTextSize(32);
        title.setTextColor(Color.rgb(70, 57, 28));
        title.setPadding(dp(12), 0, 0, 0);
        brand.addView(title);
        controls.addView(brand);
        TextView description = new TextView(this);
        description.setText("点点肚子，拖一拖。打开键盘，奶蛙就坐在上面陪你。");
        description.setTextSize(15);
        description.setPadding(0, dp(8), 0, dp(14));
        controls.addView(description);
        button(controls, "开启桌面奶蛙", v -> startOverlay());
        button(
                controls,
                "收起桌面奶蛙",
                v -> {
                    stopService(new Intent(this, OverlayService.class));
                    pet.setVisibility(View.VISIBLE);
                    updateStatus();
                });
        button(
                controls,
                "跟随其他应用的键盘",
                v ->
                        new AlertDialog.Builder(this)
                                .setTitle("让奶蛙找到键盘的位置")
                                .setMessage(
                                        "开启可选的“奶蛙键盘跟随”后，奶蛙会读取输入法窗口的边界。不读取输入文字或界面内容。关闭此功能仍可在本应用体验键盘互动。")
                                .setPositiveButton(
                                        "前往设置",
                                        (d, w) ->
                                                startActivity(
                                                        new Intent(
                                                                Settings
                                                                        .ACTION_ACCESSIBILITY_SETTINGS)))
                                .setNegativeButton("暂不开启", null)
                                .show());
        status = new TextView(this);
        status.setTextSize(12);
        status.setPadding(0, dp(10), 0, 0);
        controls.addView(status);
        EditText input = new EditText(this);
        input.setSingleLine(true);
        input.setHint("点这里，试试键盘上的奶蛙");
        input.setTextSize(14);
        FrameLayout.LayoutParams entry = new FrameLayout.LayoutParams(-1, dp(56), Gravity.BOTTOM);
        entry.setMargins(dp(20), 0, dp(20), dp(12));
        root.addView(input, entry);
        try {
            AssetStore assets = new AssetStore(this);
            pet =
                    new PetView(
                            this,
                            assets,
                            false,
                            new PetView.Host() {
                                public void drag(float dx, float dy, boolean start, boolean end) {
                                    if (start) {
                                        dragX = pet.originX();
                                        dragY = pet.originY();
                                    }
                                    float x = dragX + dx, y = dragY + dy;
                                    if (keyboard) {
                                        y = pet.originY();
                                        x =
                                                clamp(
                                                        x,
                                                        -80 * pet.spriteScale,
                                                        root.getWidth() - 240 * pet.spriteScale);
                                    } else {
                                        x =
                                                clamp(
                                                        x,
                                                        -86 * pet.spriteScale,
                                                        root.getWidth() - 234 * pet.spriteScale);
                                        y =
                                                clamp(
                                                        y,
                                                        -30 * pet.spriteScale,
                                                        root.getHeight() - 290 * pet.spriteScale);
                                    }
                                    pet.origin(x, y);
                                }

                                public void frame(Rect bounds) {}
                            });
            root.addView(pet, new FrameLayout.LayoutParams(-1, -1));
            root.post(
                    () ->
                            pet.origin(
                                    root.getWidth() / 2f - 160 * pet.spriteScale,
                                    Math.max(dp(270), root.getHeight() - 330 * pet.spriteScale)));
        } catch (Exception e) {
            status.setText("奶蛙素材暂时无法打开：" + e.getMessage());
            return;
        }
        root.setOnApplyWindowInsetsListener(
                (v, insets) -> {
                    int top =
                            Build.VERSION.SDK_INT >= 30
                                    ? insets.getInsets(WindowInsets.Type.systemBars()).top
                                    : insets.getSystemWindowInsetTop();
                    controls.setPadding(dp(24), top + dp(12), dp(24), 0);
                    v.post(() -> readKeyboard(insets));
                    return insets;
                });
        root.getViewTreeObserver()
                .addOnGlobalLayoutListener(
                        () -> {
                            if (Build.VERSION.SDK_INT < 30) readKeyboard(null);
                            else if (keyboard) placeKeyboard();
                        });
    }

    private void button(LinearLayout parent, String label, View.OnClickListener action) {
        Button b = new Button(this);
        b.setText(label);
        b.setAllCaps(false);
        b.setOnClickListener(action);
        parent.addView(b, new LinearLayout.LayoutParams(-1, dp(48)));
    }

    private static float clamp(float v, float a, float b) {
        return Math.max(a, Math.min(b, v));
    }

    private void readKeyboard(WindowInsets insets) {
        if (pet == null) return;
        Rect visible = new Rect();
        root.getWindowVisibleDisplayFrame(visible);
        boolean shown;
        if (Build.VERSION.SDK_INT >= 30 && insets != null)
            shown = insets.isVisible(WindowInsets.Type.ime());
        else {
            legacyVisibleBottom = Math.max(legacyVisibleBottom, visible.bottom);
            shown = legacyVisibleBottom - visible.bottom > dp(160);
        }
        if (shown && !keyboard) {
            beforeX = pet.originX();
            beforeY = pet.originY();
            pet.engine.keyboard(true);
        }
        if (!shown && keyboard) {
            pet.engine.keyboard(false);
            pet.origin(beforeX, beforeY);
        }
        keyboard = shown;
        keyboardRect = visible;
        if (shown) placeKeyboard();
    }

    private void placeKeyboard() {
        if (pet == null || keyboardRect == null) return;
        int[] location = new int[2];
        root.getLocationOnScreen(location);
        float top = keyboardRect.bottom - location[1];
        pet.origin(
                clamp(
                        pet.originX(),
                        -80 * pet.spriteScale,
                        root.getWidth() - 240 * pet.spriteScale),
                top - 224 * pet.spriteScale - dp(6));
    }

    private void startOverlay() {
        if (!Settings.canDrawOverlays(this)) {
            startActivity(
                    new Intent(
                            Settings.ACTION_MANAGE_OVERLAY_PERMISSION,
                            Uri.parse("package:" + getPackageName())));
            return;
        }
        if (Build.VERSION.SDK_INT >= 33
                && checkSelfPermission(Manifest.permission.POST_NOTIFICATIONS)
                        != PackageManager.PERMISSION_GRANTED)
            requestPermissions(new String[] {Manifest.permission.POST_NOTIFICATIONS}, 12);
        startForegroundService(new Intent(this, OverlayService.class));
        pet.setVisibility(View.INVISIBLE);
        root.postDelayed(this::updateStatus, 400);
    }

    @Override
    protected void onResume() {
        super.onResume();
        if (status != null) updateStatus();
        if (pet != null)
            pet.setVisibility(OverlayService.running() ? View.INVISIBLE : View.VISIBLE);
    }

    private void updateStatus() {
        status.setText(
                (Settings.canDrawOverlays(this) ? "悬浮显示已授权" : "悬浮显示未授权")
                        + " · "
                        + (KeyboardGeometryService.enabled(this) ? "跨应用键盘跟随已开启" : "跨应用键盘跟随未开启"));
    }
}
