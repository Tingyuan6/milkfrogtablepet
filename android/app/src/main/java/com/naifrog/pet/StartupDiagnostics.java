package com.naifrog.pet;

import android.app.Application;
import android.content.Context;
import android.os.Build;

import java.io.PrintWriter;
import java.io.StringWriter;

public final class StartupDiagnostics extends Application {
    @Override
    public void onCreate() {
        super.onCreate();
        Thread.UncaughtExceptionHandler previous = Thread.getDefaultUncaughtExceptionHandler();
        Thread.setDefaultUncaughtExceptionHandler(
                (thread, error) -> {
                    record(this, "未处理的运行错误", error);
                    if (previous != null) previous.uncaughtException(thread, error);
                    else {
                        android.os.Process.killProcess(android.os.Process.myPid());
                        System.exit(10);
                    }
                });
    }

    static void record(Context context, String stage, Throwable error) {
        try {
            StringWriter trace = new StringWriter();
            error.printStackTrace(new PrintWriter(trace));
            String report =
                    "奶蛙 0.4.0 · "
                            + stage
                            + "\n"
                            + Build.MANUFACTURER
                            + " "
                            + Build.MODEL
                            + " · Android "
                            + Build.VERSION.RELEASE
                            + " (API "
                            + Build.VERSION.SDK_INT
                            + ")\n"
                            + trace;
            if (report.length() > 12000) report = report.substring(0, 12000);
            context.getSharedPreferences("startup", 0).edit().putString("error", report).commit();
        } catch (RuntimeException ignored) {
        }
    }

    static String error(Context context) {
        return context.getSharedPreferences("startup", 0).getString("error", "");
    }

    static void clear(Context context) {
        context.getSharedPreferences("startup", 0).edit().remove("error").apply();
    }
}
