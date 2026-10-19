package com.naifrog.pet;

public final class FaceMotion {
    private final double[][] samples;
    private final double step;
    private double idleMs;
    public double blink, mouth, round;

    public FaceMotion(double[][] samples, double step) {
        this.samples = samples;
        this.step = step;
    }

    public void touch() {
        idleMs = 0;
        blink = 0;
    }

    public void advance(double dt, boolean settled, double mediaMs, boolean playing) {
        if (!settled) {
            idleMs = 0;
            blink = 0;
        } else {
            idleMs += Math.max(0, dt);
            blink = idleMs < 3000 ? 0 : blinkAt(idleMs % 3000);
        }
        double target = 0, r = 0;
        if (playing && mediaMs >= 0 && mediaMs < step * (samples.length - 1)) {
            double position = mediaMs / step;
            int i = (int) position;
            double t = position - i;
            target = samples[i][0] * (1 - t) + samples[i + 1][0] * t;
            r = samples[i][1] * (1 - t) + samples[i + 1][1] * t;
        }
        double ease = 1 - Math.exp(-Math.max(0, dt) / (target > mouth ? 22 : 42));
        mouth += (target - mouth) * ease;
        round += (r - round) * ease;
        if (mouth < .003) mouth = 0;
    }

    public static double blinkAt(double phase) {
        if (phase < 0 || phase >= 240) return 0;
        if (phase < 80) return smooth(phase / 80);
        if (phase < 105) return 1;
        return 1 - smooth((phase - 105) / 135);
    }

    private static double smooth(double t) {
        return t * t * (3 - 2 * t);
    }
}
