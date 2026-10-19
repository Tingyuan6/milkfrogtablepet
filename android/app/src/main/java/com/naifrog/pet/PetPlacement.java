package com.naifrog.pet;

/** Pixel geometry shared by the initial placement and the recovery action. */
public final class PetPlacement {
    private PetPlacement() {}

    public static float[] center(
            int width, int height, float scale, int controlsBottom, int inputTop) {
        if (width <= 0 || height <= 0 || scale <= 0)
            throw new IllegalArgumentException("Layout must be measured");
        float top = Math.max(0, controlsBottom),
                bottom = inputTop > top ? Math.min(height, inputTop) : height;
        if (bottom - top < 260 * scale) {
            top = 0;
            bottom = height;
        }
        float x = width / 2f - 160 * scale, y = (top + bottom) / 2f - 160 * scale;
        x = Math.max(-86 * scale, Math.min(width - 234 * scale, x));
        y = Math.max(-30 * scale, Math.min(height - 290 * scale, y));
        return new float[] {x, y};
    }
}
