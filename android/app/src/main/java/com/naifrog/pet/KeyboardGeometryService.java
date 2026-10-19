package com.naifrog.pet;

import android.accessibilityservice.AccessibilityService;
import android.content.ComponentName;
import android.content.Context;
import android.content.Intent;
import android.graphics.Rect;
import android.provider.Settings;
import android.view.accessibility.AccessibilityEvent;
import android.view.accessibility.AccessibilityWindowInfo;

/** Optional, geometry only. Never accesses a node tree, event text or typed characters. */
public final class KeyboardGeometryService extends AccessibilityService {
    public static boolean enabled(Context context) {
        String value =
                Settings.Secure.getString(
                        context.getContentResolver(),
                        Settings.Secure.ENABLED_ACCESSIBILITY_SERVICES);
        if (value == null) return false;
        ComponentName wanted = new ComponentName(context, KeyboardGeometryService.class);
        for (String item : value.split(":")) {
            if (wanted.equals(ComponentName.unflattenFromString(item))) return true;
        }
        return false;
    }

    @Override
    protected void onServiceConnected() {
        publish();
    }

    @Override
    public void onAccessibilityEvent(AccessibilityEvent ignored) {
        publish();
    }

    private void publish() {
        if (!OverlayService.running()) return;
        Rect keyboard = null;
        for (AccessibilityWindowInfo window : getWindows()) {
            if (window.getType() == AccessibilityWindowInfo.TYPE_INPUT_METHOD) {
                Rect bounds = new Rect();
                window.getBoundsInScreen(bounds);
                if (!bounds.isEmpty()) {
                    if (keyboard == null) keyboard = bounds;
                    else keyboard.union(bounds);
                }
            }
        }
        OverlayService.geometry(keyboard);
    }

    @Override
    public void onInterrupt() {
        OverlayService.geometry(null);
    }

    @Override
    public boolean onUnbind(Intent intent) {
        OverlayService.geometry(null);
        return super.onUnbind(intent);
    }
}
