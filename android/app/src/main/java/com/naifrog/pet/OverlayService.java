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
import android.os.Build;
import android.os.Handler;
import android.os.IBinder;
import android.provider.Settings;
import android.util.DisplayMetrics;
import android.view.Gravity;
import android.view.WindowInsets;
import android.view.WindowManager;
import android.widget.Toast;

public final class OverlayService extends Service {
    private static OverlayService instance;
    private WindowManager wm;
    private WindowManager.LayoutParams params;
    private PetView pet;
    private boolean attached, keyboard, registered;
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
                    handler.postDelayed(this, 1000);
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
        return instance != null;
    }

    public static void geometry(Rect rect) {
        OverlayService s = instance;
        if (s != null) s.handler.post(() -> s.setKeyboard(rect));
    }

    @Override
    public IBinder onBind(Intent i) {
        return null;
    }

    @Override
    public void onCreate() {
        super.onCreate();
        instance = this;
        wm = (WindowManager) getSystemService(WINDOW_SERVICE);
    }

    @Override
    public int onStartCommand(Intent intent, int flags, int startId) {
        if (intent != null && "stop".equals(intent.getAction())) {
            stopSelf();
            return START_NOT_STICKY;
        }
        if (!Settings.canDrawOverlays(this)) {
            stopSelf();
            return START_NOT_STICKY;
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
                        .setSmallIcon(R.drawable.naifrog)
                        .setContentIntent(open)
                        .setOngoing(true)
                        .addAction(new Notification.Action.Builder(null, "收起奶蛙", stop).build())
                        .build();
        if (Build.VERSION.SDK_INT >= 34)
            startForeground(1, notice, ServiceInfo.FOREGROUND_SERVICE_TYPE_SPECIAL_USE);
        else startForeground(1, notice);
        if (pet == null) {
            try {
                assets = new AssetStore(this);
                pet =
                        new PetView(
                                this,
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
                Toast.makeText(this, "奶蛙暂时无法打开，请重新开启。", Toast.LENGTH_LONG).show();
                stopSelf();
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
        if (pet == null) return;
        boolean locked = ((KeyguardManager) getSystemService(KEYGUARD_SERVICE)).isKeyguardLocked();
        boolean interactive =
                ((android.os.PowerManager) getSystemService(POWER_SERVICE)).isInteractive();
        if (locked || !interactive) {
            detach();
            return;
        }
        if (!attached) {
            updateWindow();
            try {
                wm.addView(pet, params);
                attached = true;
            } catch (SecurityException | WindowManager.BadTokenException e) {
                stopSelf();
            }
        }
    }

    private void detach() {
        if (attached) {
            try {
                wm.removeView(pet);
            } catch (IllegalArgumentException ignored) {
            }
            attached = false;
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
            } catch (IllegalArgumentException | SecurityException e) {
                stopSelf();
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
        stopForeground(STOP_FOREGROUND_REMOVE);
        super.onDestroy();
    }
}
