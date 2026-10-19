package com.naifrog.pet;

import java.util.Map;

/** Platform-independent state machine. Host supplies elapsed monotonic time. */
public final class PetEngine {
    public static final class Clip {
        public final String from, to;
        public final String[] frames;
        public final int duration;

        public Clip(String a, String b, String[] f, int ms) {
            from = a;
            to = b;
            frames = f;
            duration = ms;
        }
    }

    private final Map<String, Clip> clips;
    private final int[] durations;
    private Clip active;
    private double clipTime, frameTime;
    private int direction = 1, index, visited;
    private String stable = "idle", desired = "idle", layout = "free", previous = "free";
    public long revision;
    public int originalFrames;
    public double originalMs;

    public PetEngine(Map<String, Clip> clips, int[] durations) {
        this.clips = clips;
        this.durations = durations.clone();
    }

    public String visual() {
        if (active == null) return stable;
        int i = (int) Math.round(clipTime / active.duration * (active.frames.length - 1));
        return active.frames[Math.max(0, Math.min(active.frames.length - 1, i))];
    }

    public String desired() {
        return desired;
    }

    public String layout() {
        return layout;
    }

    public boolean animating() {
        return active != null || sway(stable);
    }

    public void tap(boolean belly) {
        if (layout.equals("keyboard")) request(desired.equals("sit_2") ? "sit_1" : "sit_2");
        else if (layout.equals("free") && belly) request(desired.equals("sway") ? "idle" : "sway");
    }

    public void request(String target) {
        desired = target;
        revision++;
        if (active != null)
            direction =
                    matches(active.to, desired)
                                    || (active.to.equals("idle") && !matches(active.from, desired))
                            ? 1
                            : -1;
        else reconcile();
    }

    public void beginDrag() {
        if (!layout.equals("keyboard")) {
            layout = "free";
            request("idle");
        }
    }

    public void keyboard(boolean visible) {
        if (visible && !layout.equals("keyboard")) {
            previous = layout;
            layout = "keyboard";
            request("sit_1");
        } else if (!visible && layout.equals("keyboard")) {
            layout = previous;
            request(base(layout));
        }
    }

    private static String base(String mode) {
        switch (mode) {
            case "bottom":
                return "bottom";
            case "left":
                return "peek_left";
            case "right":
                return "peek_right";
            case "keyboard":
                return "sit_1";
            default:
                return "idle";
        }
    }

    public void advance(double delta) {
        if (delta < 0 || Double.isNaN(delta) || Double.isInfinite(delta))
            throw new IllegalArgumentException("delta");
        int guard = 0;
        while (delta > .00001 && guard++ < 10000) {
            if (active != null) {
                double remaining = direction > 0 ? active.duration - clipTime : clipTime,
                        consume = Math.min(remaining, delta);
                clipTime += direction * consume;
                delta -= consume;
                if (remaining - consume < .00001) {
                    stable = direction > 0 ? active.to : active.from;
                    active = null;
                    if (sway(stable) && desired.equals("sway")) {
                        index = Integer.parseInt(stable.substring(5));
                        frameTime = 0;
                        visited = 0;
                        originalFrames = 1;
                        originalMs = 0;
                    }
                    reconcile();
                }
            } else if (sway(stable) && desired.equals("sway")) {
                double remaining = durations[index] - frameTime,
                        consume = Math.min(remaining, delta);
                frameTime += consume;
                originalMs += consume;
                delta -= consume;
                if (remaining - consume < .00001) {
                    frameTime = 0;
                    visited++;
                    if (visited == durations.length) {
                        desired = "idle";
                        revision++;
                        reconcile();
                    } else {
                        index = (index + 1) % durations.length;
                        stable = swayKey(index);
                        originalFrames++;
                    }
                }
            } else {
                reconcile();
                if (active == null) break;
            }
        }
    }

    private void reconcile() {
        if (active != null || matches(stable, desired)) return;
        String target = desired.equals("sway") ? swayKey(0) : desired;
        Clip next = clips.get(stable + "|" + target);
        if (next == null && !stable.equals("idle")) next = clips.get(stable + "|idle");
        if (next == null)
            throw new IllegalStateException("Missing transition " + stable + " -> " + target);
        active = next;
        clipTime = 0;
        direction = 1;
    }

    private static boolean matches(String endpoint, String intent) {
        return endpoint.equals(intent) || (intent.equals("sway") && sway(endpoint));
    }

    public static boolean sway(String key) {
        return key.startsWith("sway_");
    }

    public static String swayKey(int i) {
        return String.format(java.util.Locale.ROOT, "sway_%02d", i);
    }

    public static boolean belly(float x, float y) {
        double dx = (x - 160) / 65, dy = (y - 166) / 70;
        return dx * dx + dy * dy <= 1 && y >= 102 && y <= 235;
    }
}
