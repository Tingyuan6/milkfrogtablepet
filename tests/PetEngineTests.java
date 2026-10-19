import com.naifrog.pet.PetEngine;

import java.io.FileInputStream;
import java.util.HashMap;
import java.util.HashSet;
import java.util.Map;
import java.util.Properties;
import java.util.Random;
import java.util.Set;

public final class PetEngineTests {
    private static int checks;

    private static void check(boolean value, String message) {
        checks++;
        if (!value) throw new AssertionError(message);
    }

    public static void main(String[] args) throws Exception {
        Properties fixture = new Properties();
        try (FileInputStream in = new FileInputStream(args[0])) {
            fixture.load(in);
        }
        int[] durations = new int[37];
        for (int i = 0; i < 37; i++)
            durations[i] = Integer.parseInt(fixture.getProperty("duration." + i));
        Map<String, PetEngine.Clip> clips = new HashMap<>();
        for (String key : fixture.stringPropertyNames()) {
            if (key.startsWith("clip.")) {
                String[] pair = key.substring(5).split("\\|"),
                        value = fixture.getProperty(key).split(";", 2);
                clips.put(
                        pair[0] + "|" + pair[1],
                        new PetEngine.Clip(
                                pair[0], pair[1], value[1].split(","), Integer.parseInt(value[0])));
            }
        }
        PetEngine p = new PetEngine(clips, durations);
        p.tap(true);
        p.advance(220);
        Set<String> seen = new HashSet<>();
        for (int i = 0; i < 4390; i++) {
            seen.add(p.visual());
            p.advance(1);
        }
        check(seen.size() == 37, "37 original frames");
        check(p.originalMs == 4390, "4390 milliseconds");
        check(p.originalFrames == 37, "37 presented");
        p.advance(220);
        check(p.visual().equals("idle"), "one cycle");
        p.advance(20000);
        check(p.visual().equals("idle"), "no loop");
        p = new PetEngine(clips, durations);
        p.request("bottom");
        p.advance(2000);
        p.request("idle");
        p.advance(120);
        p.request("peek_right");
        p.advance(240);
        check(
                p.visual().equals(clips.get("idle|peek_right").frames[0]),
                "changed target keeps route through idle");
        p.advance(360);
        check(p.visual().equals("peek_right"), "two-clip dock route has no detour");
        int elapsed = 0;
        for (int frame = 0; frame < 37; frame++) {
            p = new PetEngine(clips, durations);
            p.tap(true);
            p.advance(220 + elapsed + 1);
            check(p.visual().equals(PetEngine.swayKey(frame)), "timing " + frame);
            p.tap(true);
            p.advance(85);
            String visible = p.visual();
            p.tap(true);
            check(visible.equals(p.visual()), "continuous reversal " + frame);
            p.advance(85);
            check(p.visual().equals(PetEngine.swayKey(frame)), "resume origin " + frame);
            p.tap(true);
            p.advance(500);
            check(p.visual().equals("idle"), "exit " + frame);
            elapsed += durations[frame];
        }
        p = new PetEngine(clips, durations);
        for (int i = 0; i < 10; i++) {
            p.tap(true);
            p.advance(23);
        }
        p.advance(2000);
        check(p.visual().equals("idle"), "10 tap convergence");
        p = new PetEngine(clips, durations);
        p.tap(true);
        p.advance(900);
        p.keyboard(true);
        p.advance(2000);
        check(p.visual().equals("sit_1"), "IME overrides sway");
        p.tap(false);
        p.advance(300);
        check(p.visual().equals("sit_2"), "seat toggle");
        p.tap(false);
        p.advance(100);
        String midway = p.visual();
        p.tap(false);
        check(midway.equals(p.visual()), "seat reversal preserves image");
        p.advance(1000);
        check(p.visual().equals("sit_2"), "seat latest intent");
        p.keyboard(false);
        p.advance(2000);
        check(p.visual().equals("idle"), "IME restores idle");
        for (int i = 0; i < 5; i++) {
            p.keyboard(true);
            p.advance(47);
            p.keyboard(false);
            p.advance(31);
        }
        p.advance(2000);
        check(p.visual().equals("idle"), "IME churn");
        Random random = new Random(33);
        for (int i = 0; i < 10000; i++) {
            int op = random.nextInt(6);
            if (op < 3) p.tap(true);
            else if (op == 3) p.keyboard(true);
            else if (op == 4) p.keyboard(false);
            else p.beginDrag();
            p.advance(1 + random.nextInt(80));
        }
        p.keyboard(false);
        p.request("idle");
        p.advance(2000);
        check(p.visual().equals("idle"), "random events converge");
        check(PetEngine.belly(160, 160) && !PetEngine.belly(160, 53), "belly mask");
        System.out.println(
                "PASS " + checks + " Java checks; 37 interrupt positions; 10,000 mixed events");
    }
}
