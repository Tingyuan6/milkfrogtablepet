import com.naifrog.pet.MouthSurface;

public final class MouthSurfaceTests {
    public static void main(String[] args) {
        int teeth = 0, tongue = 0, cavity = 0, checks = 0;
        for (int y = -100; y <= 100; y++)
            for (int x = -100; x <= 100; x++) {
                if (x * x + y * y > 10000) continue;
                int color = MouthSurface.colorAt(x / 100.0, y / 100.0),
                        r = (color >>> 16) & 255,
                        g = (color >>> 8) & 255,
                        b = color & 255;
                if ((color >>> 24) != 255) throw new AssertionError("mouth material alpha");
                if (r > 210 && g > 200 && b > 175) teeth++;
                if (r > 120 && g < 120 && b > g && b > 40) tongue++;
                if (r < 100 && g < 50) cavity++;
                checks++;
            }
        if (teeth < 500 || tongue < 500 || cavity < 2000)
            throw new AssertionError("Missing teeth, tongue or cavity");
        System.out.println(
                "PASS "
                        + checks
                        + " Java mouth material checks; teeth="
                        + teeth
                        + ", tongue="
                        + tongue
                        + ", cavity="
                        + cavity);
    }
}
