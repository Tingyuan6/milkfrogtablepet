package com.naifrog.pet;

/** Shared mouth material: a shaded cavity, ivory teeth and a pink tongue. */
public final class MouthSurface {
    private MouthSurface() {}

    public static int colorAt(double x, double y) {
        double lower = clamp((y + 1) / 2);
        int color = rgb(38 + 25 * lower, 9 + 8 * lower, 12 + 12 * lower);
        double tongue = x * x / (.60 * .60) + (y - .55) * (y - .55) / (.38 * .38);
        double tongueMask = clamp((1 - tongue) * 8);
        if (tongueMask > 0) {
            double light = clamp(1 - Math.abs(x) / .7) * clamp((.95 - y) * 2);
            int pink = rgb(166 + 42 * light, 49 + 30 * light, 65 + 29 * light);
            double crease =
                    clamp(1 - Math.abs(x) / .06) * clamp((y - .27) * 6) * clamp((.79 - y) * 7);
            color = mix(color, mix(pink, rgb(121, 30, 43), crease * .5), tongueMask);
        }
        double rim = Math.sqrt(Math.max(0, 1 - x * x));
        double toothPosition = (x + .88) / 1.76 * 7,
                cell = toothPosition - Math.floor(toothPosition);
        double tip = .22 + .11 * Math.sqrt(Math.max(0, 1 - Math.pow((cell - .5) * 2, 2)));
        double upper =
                clamp(((-rim + tip) - y) * 18)
                        * clamp((y + rim - .025) * 22)
                        * clamp((.9 - Math.abs(x)) * 16);
        double bottom =
                clamp((y - (rim - .18)) * 24)
                        * clamp((rim - y - .015) * 26)
                        * clamp((.82 - Math.abs(x)) * 16);
        double teeth = Math.max(upper, bottom);
        if (teeth > 0) {
            double separation = clamp((.045 - Math.min(cell, 1 - cell)) / .045) * .18;
            color =
                    mix(
                            color,
                            rgb(
                                    247 - 30 * separation,
                                    239 - 32 * separation,
                                    216 - 35 * separation),
                            teeth);
        }
        return color;
    }

    private static int rgb(double r, double g, double b) {
        return 0xff000000 | ((int) r << 16) | ((int) g << 8) | (int) b;
    }

    private static int mix(int a, int b, double t) {
        int v = 0xff000000;
        for (int sh = 0; sh < 24; sh += 8)
            v |= (int) Math.round(((a >>> sh) & 255) * (1 - t) + ((b >>> sh) & 255) * t) << sh;
        return v;
    }

    private static double clamp(double x) {
        return Math.max(0, Math.min(1, x));
    }
}
