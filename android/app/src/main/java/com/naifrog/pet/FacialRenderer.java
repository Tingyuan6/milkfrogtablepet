package com.naifrog.pet;

import android.graphics.Bitmap;

import org.json.JSONArray;
import org.json.JSONObject;

import java.util.HashMap;
import java.util.Iterator;
import java.util.Map;

public final class FacialRenderer {
    static final class Face {
        double[][] eyes;
        double[] mouth;
    }

    private final AssetStore assets;
    private final Map<String, Face> faces = new HashMap<>();
    private final Map<String, int[]> sources = new HashMap<>();
    private final int[] output = new int[320 * 320];
    private final Bitmap result = Bitmap.createBitmap(320, 320, Bitmap.Config.ARGB_8888);
    public final double[][] speechSamples;
    public final int speechStep;
    public final String speechFile;
    public double x, y;
    private String previous = "";

    public FacialRenderer(AssetStore assets) throws Exception {
        this.assets = assets;
        result.setDensity(Bitmap.DENSITY_NONE);
        JSONObject rig = new JSONObject(assets.text("face.json")),
                data = rig.getJSONObject("faces");
        for (Iterator<String> it = data.keys(); it.hasNext(); ) {
            String key = it.next();
            JSONObject f = data.getJSONObject(key);
            Face face = new Face();
            face.eyes = matrix(f.getJSONArray("eyes"));
            face.mouth = row(f.getJSONArray("mouth"));
            faces.put(key, face);
        }
        JSONObject speech = rig.getJSONObject("speech");
        speechSamples = matrix(speech.getJSONArray("samples"));
        speechStep = speech.getInt("stepMs");
        speechFile = speech.getString("file");
    }

    private static double[] row(JSONArray a) throws Exception {
        double[] r = new double[a.length()];
        for (int i = 0; i < r.length; i++) r[i] = a.getDouble(i);
        return r;
    }

    private static double[][] matrix(JSONArray a) throws Exception {
        double[][] r = new double[a.length()][];
        for (int i = 0; i < r.length; i++) r[i] = row(a.getJSONArray(i));
        return r;
    }

    public boolean has(String pose) {
        return faces.containsKey(pose);
    }

    public boolean hitMouth(String pose, double px, double py) {
        Face f = faces.get(pose);
        if (f == null) return false;
        double[] m = f.mouth;
        double a = m[3] * Math.PI / 180,
                dx = px - m[0],
                dy = py - m[1],
                u = dx * Math.cos(a) + dy * Math.sin(a),
                v = -dx * Math.sin(a) + dy * Math.cos(a);
        return u * u / Math.pow(m[2] + 5, 2) + v * v / 81 <= 1;
    }

    public void advance(String pose, double px, double py, boolean enabled, double dt) {
        if (!previous.equals(pose)) {
            x = y = 0;
            previous = pose;
        }
        Face f = faces.get(pose);
        double tx = 0, ty = 0;
        if (enabled && f != null) {
            double hx = 0, hy = 0;
            for (double[] e : f.eyes) {
                hx += e[0];
                hy += e[1];
            }
            hx /= f.eyes.length;
            hy /= f.eyes.length;
            double dx = px - hx,
                    dy = py - hy,
                    len = Math.hypot(dx, dy),
                    factor = clamp((160 - len) / 50);
            if (len > 1) {
                tx = dx / len * 2 * factor;
                ty = dy / len * 1.6 * factor;
            }
        }
        double ease = 1 - Math.exp(-Math.min(100, dt) / 90);
        x += (tx - x) * ease;
        y += (ty - y) * ease;
        if (Math.abs(x) < .01) x = 0;
        if (Math.abs(y) < .01) y = 0;
    }

    public String stamp(double blink, double mouth, double round) {
        return Math.round(x * 25)
                + ":"
                + Math.round(y * 25)
                + ":"
                + Math.round(blink * 100)
                + ":"
                + Math.round(mouth * 50)
                + ":"
                + Math.round(round * 20);
    }

    private int[] pixels(String pose) {
        int[] p = sources.get(pose);
        if (p == null) {
            p = new int[320 * 320];
            assets.get(pose).getPixels(p, 0, 320, 0, 0, 320, 320);
            sources.put(pose, p);
        }
        return p;
    }

    public Bitmap render(String pose, double blink, double mouth, double round) {
        Face face = faces.get(pose);
        if (face == null || (blink == 0 && mouth == 0 && x == 0 && y == 0)) return assets.get(pose);
        int[] src = pixels(pose);
        System.arraycopy(src, 0, output, 0, src.length);
        for (double[] e : face.eyes) {
            double cx = e[0], cy = e[1], rx = e[2], ry = e[3];
            for (int py = (int) Math.floor(cy - ry - 1); py <= Math.ceil(cy + ry + 1); py++)
                for (int px = (int) Math.floor(cx - rx - 1); px <= Math.ceil(cx + rx + 1); px++) {
                    double u = (px - cx) / rx, v = (py - cy) / ry, r = u * u + v * v;
                    if (r >= 1.2) continue;
                    int p = py * 320 + px;
                    if ((src[p] >>> 24) < 230) continue;
                    double fall = Math.pow(Math.max(0, 1 - r), 1.5);
                    output[p] = sample(src, px - x * rx / 7.3 * fall, py - y * ry / 9 * fall);
                    if (blink > 0) {
                        double lid = -1 + 2.12 * blink + .12 * u * u,
                                cover = clamp((lid - v) * ry + .5) * clamp((1.2 - r) * rx);
                        int skin = skin(src, cx + u * rx, cy - ry - 3, cx + u * rx, cy + ry + 3);
                        output[p] = mix(output[p], skin, cover);
                        if (blink > .15 && Math.abs(v - lid) < .12 && Math.abs(u) < .86)
                            output[p] =
                                    mix(
                                            output[p],
                                            shade(skin, .65),
                                            .45 * clamp((blink - .15) * 3));
                        double crease =
                                clamp(1 - Math.abs(v - (.12 + .22 * u * u)) * ry / .75)
                                        * clamp((blink - .65) / .35)
                                        * clamp((.95 - Math.abs(u)) * 7);
                        output[p] = mix(output[p], shade(skin, .52), crease * .8);
                    }
                }
        }
        if (mouth > 0) mouth(src, face.mouth, mouth, round);
        for (int p = 0; p < output.length; p++)
            output[p] = (output[p] & 0x00ffffff) | (src[p] & 0xff000000);
        result.setPixels(output, 0, 320, 0, 0, 320, 320);
        return result;
    }

    private void mouth(int[] src, double[] m, double opening, double rounding) {
        double a = m[3] * Math.PI / 180,
                c = Math.cos(a),
                s = Math.sin(a),
                rx = m[2] * (1 - .2 * rounding),
                ry = .6 + m[2] * .72 * opening,
                center = ry * .35;
        int radius = (int) Math.ceil(m[2] + 12);
        for (int py = Math.max(0, (int) m[1] - radius); py < Math.min(320, m[1] + radius); py++)
            for (int px = Math.max(0, (int) m[0] - radius);
                    px < Math.min(320, m[0] + radius);
                    px++) {
                double dx = px - m[0],
                        dy = py - m[1],
                        u = dx * c + dy * s,
                        v = -dx * s + dy * c,
                        edge =
                                clamp((m[2] * 1.25 - Math.abs(u)) / 2)
                                        * clamp((Math.max(6, ry + 3) - Math.abs(v)) / 2);
                if (edge <= 0) continue;
                int p = py * 320 + px;
                if ((src[p] >>> 24) < 230) continue;
                int skin =
                        skin(
                                src,
                                m[0] + u * c + 7 * s,
                                m[1] + u * s - 7 * c,
                                m[0] + u * c - 11 * s,
                                m[1] + u * s + 11 * c);
                output[p] = mix(output[p], skin, edge * clamp(opening * 9));
                double oval = u * u / (rx * rx) + (v - center) * (v - center) / (ry * ry),
                        inside = clamp((1 - oval) * Math.min(rx, ry));
                if (inside > 0) {
                    int cavity = MouthSurface.colorAt(u / rx, (v - center) / ry);
                    output[p] = mix(output[p], cavity, inside * clamp(opening * 10));
                } else if (oval < 1.45)
                    output[p] =
                            mix(
                                    output[p],
                                    shade(skin, v < center ? .72 : 1.08),
                                    clamp((1.45 - oval) * 2) * clamp(opening * 5));
            }
    }

    private static int skin(int[] src, double ax, double ay, double bx, double by) {
        int a = sample(src, ax, ay), b = sample(src, bx, by);
        boolean av = yellow(a), bv = yellow(b);
        if (av && bv) return mix(a, b, .35);
        if (av) return a;
        if (bv) return b;
        double cx = (ax + bx) / 2, cy = (ay + by) / 2;
        for (int r = 6; r <= 24; r += 6)
            for (int[] d : new int[][] {{-r, 0}, {r, 0}, {0, -r}, {0, r}}) {
                int c = sample(src, cx + d[0], cy + d[1]);
                if (yellow(c)) return c;
            }
        return a;
    }

    private static boolean yellow(int c) {
        int r = (c >>> 16) & 255, g = (c >>> 8) & 255, b = c & 255;
        return (c >>> 24) > 230 && r > 130 && r > g * 1.05 && g > b * 1.2;
    }

    private static int shade(int color, double factor) {
        int v = color & 0xff000000;
        for (int sh = 0; sh < 24; sh += 8)
            v |= Math.min(255, (int) (((color >>> sh) & 255) * factor)) << sh;
        return v;
    }

    private static int mix(int a, int b, double t) {
        int v = 0;
        for (int sh = 0; sh <= 24; sh += 8)
            v |= (int) Math.round(((a >>> sh) & 255) * (1 - t) + ((b >>> sh) & 255) * t) << sh;
        return v;
    }

    private static int sample(int[] p, double x, double y) {
        x = Math.max(0, Math.min(318.99, x));
        y = Math.max(0, Math.min(318.99, y));
        int ix = (int) x, iy = (int) y;
        double fx = x - ix, fy = y - iy;
        return mix(
                mix(p[iy * 320 + ix], p[iy * 320 + ix + 1], fx),
                mix(p[(iy + 1) * 320 + ix], p[(iy + 1) * 320 + ix + 1], fx),
                fy);
    }

    private static double clamp(double x) {
        return Math.max(0, Math.min(1, x));
    }
}
