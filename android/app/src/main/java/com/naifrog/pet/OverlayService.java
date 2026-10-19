package com.naifrog.pet;

import android.app.KeyguardManager;
import android.app.Notification;
import android.app.NotificationChannel;
import android.app.NotificationManager;
import android.app.PendingIntent;
import android.app.Service;
import android.content.BroadcastReceiver;
import android.content.Context;
import android.content.Intent;
import android.content.IntentFilter;
import android.content.pm.ServiceInfo;
import android.content.res.Configuration;
import android.graphics.PixelFormat;
import android.graphics.Rect;
import android.hardware.display.DisplayManager;
import android.os.Build;
import android.os.Handler;
import android.os.IBinder;
import android.provider.Settings;
import android.util.DisplayMetrics;
import android.view.Display;
import android.view.Gravity;
import android.view.WindowInsets;
import android.view.WindowManager;
import android.widget.Toast;

public final class OverlayService extends Service {
    public static final String ACTION_STATE = "com.naifrog.pet.OVERLAY_STATE";
    private static OverlayService instance;
    private WindowManager wm;
    private WindowManager.LayoutParams params;
    private PetView pet;
    private boolean attached, keyboard, registered, failed;
    private Context windowContext;
    private float x, y, beforeX, beforeY, dragX, dragY;
    private Rect ime;
    private AssetStore assets;
    private final Handler handler = new Handler(android.os.Looper.getMainLooper());
    private final Runnable monitor =
            new Runnable() {
                public void run() {
                    if (!Settings.canDrawOverlays(OverlayService.this)) {
                        stopSelf();
                        return;
                    }
                    syncVisibility();
                    if (!failed) handler.postDelayed(this, 1000);
                }
            };
    private final BroadcastReceiver screen =
            new BroadcastReceiver() {
                @Override
                public void onReceive(Context c, Intent i) {
                    if (Intent.ACTION_SCREEN_OFF.equals(i.getAction())) detach();
                    else syncVisibility();
                }
            };

    public static boolean running() {
        return instance != null
                && instance.attached
                && !instance.failed
                && instance.pet != null
                && instance.pet.hasPresented();
    }

    public static void geometry(Rect rect) {
        OverlayService s = instance;
        if (s != null)
            s.handler.post(
                    () -> {
                        try {
                            s.setKeyboard(rect);
                        } catch (RuntimeException error) {
                            s.fail("键盘位置更新", error);
                        }
                    });
    }

    @Override
    public IBinder onBind(Intent i) {
        return null;
    }

    @Override
    public void onCreate() {
        super.onCreate();
        instance = this;
    }

    @Override
    public int onStartCommand(Intent intent, int flags, int startId) {
        try {
            return startOverlay(intent);
        } catch (RuntimeException error) {
            fail("启动桌面悬浮", error);
            return START_NOT_STICKY;
        }
    }

    private int startOverlay(Intent intent) {
        if (intent != null && "stop".equals(intent.getAction())) {
            stopSelf();
            return START_NOT_STICKY;
        }
        if (!Settings.canDrawOverlays(this)) {
            stopSelf();
            return START_NOT_STICKY;
        }
        if (wm == null) {
            windowContext = this;
            if (Build.VERSION.SDK_INT >= 30) {
                DisplayManager displays = getSystemService(DisplayManager.class);
                Display display =
                        displays == null ? null : displays.getDisplay(Display.DEFAULT_DISPLAY);
                if (display == null) throw new IllegalStateException("找不到可用的显示屏");
                windowContext =
                        createDisplayContext(display)
                                .createWindowContext(
                                        WindowManager.LayoutParams.TYPE_APPLICATION_OVERLAY, null);
            }
            wm = windowContext.getSystemService(WindowManager.class);
            if (wm == null) throw new IllegalStateException("系统未提供窗口服务");
        }
        NotificationManager nm = getSystemService(NotificationManager.class);
        nm.createNotificationChannel(
                new NotificationChannel("companion", "奶蛙陪伴", NotificationManager.IMPORTANCE_LOW));
        PendingIntent
                open =
                        PendingIntent.getActivity(
                                this,
                                0,
                                new Intent(this, MainActivity.class),
                                PendingIntent.FLAG_IMMUTABLE | PendingIntent.FLAG_UPDATE_CURRENT),
                stop =
                        PendingIntent.getService(
                                this,
                                1,
                                new Intent(this, OverlayService.class).setAction("stop"),
                                PendingIntent.FLAG_IMMUTABLE | PendingIntent.FLAG_UPDATE_CURRENT);
        Notification notice =
                new Notification.Builder(this, "companion")
                        .setContentTitle("奶蛙在这里陪你")
                        .setContentText("点击打开设置，或随时收起奶蛙")
                        .setSmallIcon(R.drawable.ic_stat_naifrog)
                        .setContentIntent(open)
                        .setOngoing(true)
                        .addAction(
                                new Notification.Action.Builder(
                                                R.drawable.ic_stat_naifrog, "收起奶蛙", stop)
                                        .build())
                        .build();
        if (Build.VERSION.SDK_INT >= 34)
            startForeground(1, notice, ServiceInfo.FOREGROUND_SERVICE_TYPE_SPECIAL_USE);
        else startForeground(1, notice);
        if (pet == null) {
            try {
                assets = new AssetStore(this);
                pet =
                        new PetView(
                                windowContext,
                                assets,
                                true,
                                new PetView.Host() {
                                    public void drag(
                                            float dx, float dy, boolean start, boolean end) {
                                        if (start) {
                                            dragX = x;
                                            dragY = y;
                                        }
                                        x = dragX + dx;
                                        if (!keyboard) y = dragY + dy;
                                        clampPosition();
                                        updateWindow();
                                        if (end) save();
                                    }

                                    public void frame(Rect bounds) {
                                        updateWindow();
                                    }

                                    public void presented() {
                                        StartupDiagnostics.clear(OverlayService.this);
                                        publishState();
                                    }
                                });
                Rect screen = screenBounds();
                x =
                        getSharedPreferences("pet", 0)
                                .getFloat("x", screen.right - 250 * pet.spriteScale);
                y =
                        getSharedPreferences("pet", 0)
                                .getFloat("y", screen.bottom - 360 * pet.spriteScale);
                params =
                        new WindowManager.LayoutParams(
                                1,
                                1,
                                WindowManager.LayoutParams.TYPE_APPLICATION_OVERLAY,
                                WindowManager.LayoutParams.FLAG_NOT_FOCUSABLE
                                        | WindowManager.LayoutParams.FLAG_NOT_TOUCH_MODAL
                                        | WindowManager.LayoutParams.FLAG_LAYOUT_IN_SCREEN,
                                PixelFormat.TRANSLUCENT);
                params.gravity = Gravity.TOP | Gravity.LEFT;
                params.setTitle("奶蛙");
                if (Build.VERSION.SDK_INT >= 30)
                    pet.setOnApplyWindowInsetsListener(
                            (v, insets) -> {
                                if (!KeyboardGeometryService.enabled(this)) {
                                    boolean show = insets.isVisible(WindowInsets.Type.ime());
                                    int bottom = insets.getInsets(WindowInsets.Type.ime()).bottom;
                                    if (show && bottom > 0) {
                                        Rect b = screenBounds();
                                        geometry(
                                                new Rect(
                                                        b.left,
                                                        b.bottom - bottom,
                                                        b.right,
                                                        b.bottom));
                                    } else if (!show && keyboard) geometry(null);
                                }
                                return insets;
                            });
                clampPosition();
            } catch (Exception e) {
                fail("桌面角色初始化", e);
                return START_NOT_STICKY;
            }
        }
        if (!registered) {
            IntentFilter filter = new IntentFilter();
            filter.addAction(Intent.ACTION_SCREEN_OFF);
            filter.addAction(Intent.ACTION_USER_PRESENT);
            if (Build.VERSION.SDK_INT >= 33)
                registerReceiver(screen, filter, Context.RECEIVER_NOT_EXPORTED);
            else registerReceiver(screen, filter);
            registered = true;
        }
        handler.removeCallbacks(monitor);
        handler.post(monitor);
        return START_NOT_STICKY;
    }

    private Rect screenBounds() {
        if (Build.VERSION.SDK_INT >= 30) return wm.getCurrentWindowMetrics().getBounds();
        DisplayMetrics d = new DisplayMetrics();
        wm.getDefaultDisplay().getRealMetrics(d);
        return new Rect(0, 0, d.widthPixels, d.heightPixels);
    }

    private void syncVisibility() {
        try {
            updateVisibility();
        } catch (RuntimeException error) {
            fail("显示桌面奶蛙", error);
        }
    }

    private void updateVisibility() {
        if (pet == null || failed) return;
        boolean locked = ((KeyguardManager) getSystemService(KEYGUARD_SERVICE)).isKeyguardLocked();
        boolean interactive =
                ((android.os.PowerManager) getSystemService(POWER_SERVICE)).isInteractive();
        if (locked || !interactive) {
            detach();
            return;
        }
        if (!attached) {
            updateWindow();
            if (failed) return;
            try {
                wm.addView(pet, params);
                attached = true;
                publishState();
            } catch (RuntimeException e) {
                fail("添加悬浮窗口", e);
            }
        }
    }

    private void detach() {
        if (attached) {
            try {
                wm.removeView(pet);
            } catch (RuntimeException ignored) {
            }
            attached = false;
            publishState();
        }
    }

    private void clampPosition() {
        if (pet == null) return;
        Rect s = screenBounds();
        x = Math.max(s.left - 86 * pet.spriteScale, Math.min(s.right - 234 * pet.spriteScale, x));
        if (keyboard && ime != null) {
            x =
                    Math.max(
                            ime.left - 80 * pet.spriteScale,
                            Math.min(ime.right - 240 * pet.spriteScale, x));
            y = ime.top - 224 * pet.spriteScale - 6 * getResources().getDisplayMetrics().density;
        } else
            y =
                    Math.max(
                            s.top - 30 * pet.spriteScale,
                            Math.min(s.bottom - 290 * pet.spriteScale, y));
    }

    private void updateWindow() {
        if (params == null || pet == null) return;
        Rect crop = pet.crop();
        params.width = Math.max(1, Math.round(crop.width() * pet.spriteScale));
        params.height = Math.max(1, Math.round(crop.height() * pet.spriteScale));
        params.x = Math.round(x + crop.left * pet.spriteScale);
        params.y = Math.round(y + crop.top * pet.spriteScale);
        if (attached)
            try {
                wm.updateViewLayout(pet, params);
            } catch (RuntimeException e) {
                fail("更新悬浮窗口", e);
            }
    }

    private void setKeyboard(Rect rect) {
        if (pet == null) return;
        boolean shown = rect != null && !rect.isEmpty();
        if (shown && !keyboard) {
            beforeX = x;
            beforeY = y;
            pet.engine.keyboard(true);
        } else if (!shown && keyboard) {
            pet.engine.keyboard(false);
            x = beforeX;
            y = beforeY;
        }
        keyboard = shown;
        ime = shown ? new Rect(rect) : null;
        clampPosition();
        updateWindow();
    }

    private void save() {
        getSharedPreferences("pet", 0).edit().putFloat("x", x).putFloat("y", y).apply();
    }

    private void publishState() {
        sendBroadcast(new Intent(ACTION_STATE).setPackage(getPackageName()));
    }

    private void fail(String stage, Throwable error) {
        if (failed) return;
        failed = true;
        StartupDiagnostics.record(this, stage, error);
        handler.removeCallbacksAndMessages(null);
        detach();
        publishState();
        Toast.makeText(getApplicationContext(), "桌面奶蛙暂未开启，请返回应用查看错误信息。", Toast.LENGTH_LONG).show();
        stopSelf();
    }

    @Override
    public void onConfigurationChanged(Configuration c) {
        super.onConfigurationChanged(c);
        if (pet != null) {
            keyboard = false;
            ime = null;
            pet.engine.keyboard(false);
            clampPosition();
            updateWindow();
        }
    }

    @Override
    public void onDestroy() {
        handler.removeCallbacksAndMessages(null);
        if (registered) unregisterReceiver(screen);
        detach();
        if (assets != null) assets.clear();
        instance = null;
        publishState();
        stopForeground(STOP_FOREGROUND_REMOVE);
        super.onDestroy();
    }
}
