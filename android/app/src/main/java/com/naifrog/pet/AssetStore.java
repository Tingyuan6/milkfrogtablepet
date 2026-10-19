package com.naifrog.pet;

import android.content.Context;
import android.graphics.Bitmap;
import android.graphics.BitmapFactory;
import android.graphics.Rect;
import android.util.LruCache;

import org.json.JSONArray;
import org.json.JSONObject;

import java.io.ByteArrayOutputStream;
import java.io.InputStream;
import java.util.HashMap;
import java.util.Map;

public final class AssetStore {
    private final Context context;
    private final LruCache<String, Bitmap> cache =
            new LruCache<String, Bitmap>(32 * 1024 * 1024) {
                @Override
                protected int sizeOf(String k, Bitmap b) {
                    return b.getAllocationByteCount();
                }
            };
    private final Map<String, String> poseFiles = new HashMap<>();
    private final Map<String, Rect> bounds = new HashMap<>();
    public final Map<String, PetEngine.Clip> clips = new HashMap<>();
    public final int[] durations;

    public AssetStore(Context context) throws Exception {
        this.context = context.getApplicationContext();
        JSONObject manifest = new JSONObject(text("manifest.json"));
        JSONArray times = manifest.getJSONObject("sway").getJSONArray("durationsMs");
        durations = new int[times.length()];
        int total = 0;
        for (int i = 0; i < times.length(); i++) {
            durations[i] = times.getInt(i);
            total += durations[i];
        }
        if (times.length() != 37 || total != 4390)
            throw new IllegalStateException("Sway timing changed");
        JSONObject poses = manifest.getJSONObject("poses");
        java.util.Iterator<String> ids = poses.keys();
        while (ids.hasNext()) {
            String id = ids.next();
            JSONObject p = poses.getJSONObject(id);
            String file = p.getString("file");
            poseFiles.put(id, file);
            JSONArray b = p.getJSONArray("bounds");
            bounds.put(file, new Rect(b.getInt(0), b.getInt(1), b.getInt(2), b.getInt(3)));
        }
        JSONArray array = manifest.getJSONArray("clips");
        for (int i = 0; i < array.length(); i++) {
            JSONObject c = array.getJSONObject(i);
            JSONArray files = c.getJSONArray("files");
            String[] f = new String[files.length()];
            for (int j = 0; j < f.length; j++) f[j] = files.getString(j);
            PetEngine.Clip clip =
                    new PetEngine.Clip(
                            c.getString("from"), c.getString("to"), f, c.getInt("durationMs"));
            clips.put(clip.from + "|" + clip.to, clip);
        }
    }

    String text(String file) throws Exception {
        try (InputStream in = context.getAssets().open(file)) {
            ByteArrayOutputStream out = new ByteArrayOutputStream();
            byte[] buf = new byte[8192];
            int n;
            while ((n = in.read(buf)) > 0) out.write(buf, 0, n);
            return out.toString("UTF-8");
        }
    }

    public Bitmap get(String key) {
        String file = poseFiles.containsKey(key) ? poseFiles.get(key) : key;
        Bitmap b = cache.get(file);
        if (b != null) return b;
        try (InputStream in = context.getAssets().open(file)) {
            BitmapFactory.Options opts = new BitmapFactory.Options();
            opts.inScaled = false;
            b = BitmapFactory.decodeStream(in, null, opts);
            if (b == null) throw new IllegalStateException("Invalid image " + file);
            b.setDensity(Bitmap.DENSITY_NONE);
            cache.put(file, b);
            return b;
        } catch (Exception e) {
            throw new IllegalStateException("Missing asset " + file, e);
        }
    }

    public Rect bounds(String key) {
        String file = poseFiles.containsKey(key) ? poseFiles.get(key) : key;
        Rect r = bounds.get(file);
        if (r == null) {
            Bitmap b = get(key);
            int[] pixels = new int[320 * 320];
            b.getPixels(pixels, 0, 320, 0, 0, 320, 320);
            r = new Rect(320, 320, 0, 0);
            for (int y = 0; y < 320; y++)
                for (int x = 0; x < 320; x++)
                    if ((pixels[y * 320 + x] >>> 24) >= 64) {
                        r.left = Math.min(r.left, x);
                        r.top = Math.min(r.top, y);
                        r.right = Math.max(r.right, x + 1);
                        r.bottom = Math.max(r.bottom, y + 1);
                    }
            r.inset(-2, -2);
            r.intersect(0, 0, 320, 320);
            bounds.put(file, r);
        }
        return new Rect(r);
    }

    public void clear() {
        cache.evictAll();
    }
}
