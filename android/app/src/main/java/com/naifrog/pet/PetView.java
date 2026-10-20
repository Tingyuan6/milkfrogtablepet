package com.naifrog.pet;

import android.content.Context;
import android.graphics.Bitmap;
import android.graphics.Canvas;
import android.graphics.Paint;
import android.graphics.Rect;
import android.graphics.RectF;
import android.os.SystemClock;
import android.view.MotionEvent;
import android.view.View;

public final class PetView extends View {
    public interface Host {
        void drag(float dx, float dy, boolean start, boolean end);

        void frame(Rect bounds);

        default void presented() {}
    }

    public final PetEngine engine;
    private final AssetStore assets;
    private final Host host;
    private final FacialRenderer facial;
    private final FaceMotion faceMotion;
    private final SpeechPlayback speech;
    private final SpeechPlayback fartSound;
    private final FartReaction fart = new FartReaction();
    private boolean buttDown;
    private String mouthPoseDown;
    private final Paint paint = new Paint(Paint.ANTI_ALIAS_FLAG | Paint.FILTER_BITMAP_FLAG);
    private final boolean compact;
    public final float spriteScale;
    private Rect crop = new Rect(86, 30, 234, 290);
    private float originX, originY, downX, downY, mouseX = 160, mouseY = 53;
    private int pointer = -1;
    private boolean dragging, bellyDown, look;
    private boolean attached, tickerRunning;
    private boolean presented;
    private float lastDragX, lastDragY;
    private String drawn = "";
    private long previous;
    private Bitmap image;
    private final Runnable tick =
            new Runnable() {
                @Override
                public void run() {
                    if (!tickerRunning) return;
                    long now = SystemClock.uptimeMillis();
                    float dt = now - previous;
                    previous = now;
                    engine.advance(dt);
                    String pose = engine.visual();
                    boolean fartWasActive=fart.active();
                    fart.advance(dt);
                    if (!"bottom".equals(pose)) fart.stop();
                    if (fartWasActive && !fart.active()) fartSound.stop();
                    if (fart.active()) pose=fart.visual();
                    facial.advance(pose, mouseX, mouseY, look, dt);
                    if (!facial.has(pose)) speech.stop();
                    faceMotion.advance(
                            dt,
                            facial.has(pose) && pointer < 0,
                            speech.position(),
                            speech.playing());
                    String key =
                            pose
                                    + "|"
                                    + facial.stamp(
                                            faceMotion.blink, faceMotion.mouth, faceMotion.round);
                    if (!key.equals(drawn)) {
                        drawn = key;
                        image =
                                facial.render(
                                        pose, faceMotion.blink, faceMotion.mouth, faceMotion.round);
                        Rect next = assets.bounds(pose);
                        if (!next.equals(crop)) {
                            crop = next;
                            host.frame(new Rect(crop));
                        }
                        invalidate();
                    }
                    if (tickerRunning)
                        postDelayed(
                                this,
                                fart.active() || engine.animating()
                                                || pointer >= 0
                                                || speech.playing()
                                                || faceMotion.blink > 0
                                                || faceMotion.mouth > 0
                                        ? 16
                                        : 32);
                }
            };

    public PetView(Context context, AssetStore assets, boolean compact, Host host) {
        super(context);
        this.assets = assets;
        this.compact = compact;
        this.host = host;
        engine = new PetEngine(assets.clips, assets.durations);
        try {
            facial = new FacialRenderer(assets);
        } catch (Exception error) {
            throw new IllegalStateException("Missing facial assets", error);
        }
        faceMotion = new FaceMotion(facial.speechSamples, facial.speechStep);
        speech = new SpeechPlayback(context, facial.speechFile);
        fartSound = new SpeechPlayback(context, "audio/fart.wav");
        spriteScale = getResources().getDisplayMetrics().density * .85f;
        image = assets.get("idle");
        setWillNotDraw(false);
        setFocusable(false);
        setContentDescription("奶蛙，点击嘴巴说话，点击肚子互动，拖动移动");
    }

    public void origin(float x, float y) {
        originX = x;
        originY = y;
        invalidate();
    }

    public float originX() {
        return originX;
    }

    public float originY() {
        return originY;
    }

    public Rect crop() {
        return new Rect(crop);
    }

    public boolean hasPresented() {
        return presented;
    }

    @Override
    protected void onAttachedToWindow() {
        super.onAttachedToWindow();
        attached = true;
        syncTicker();
    }

    @Override
    protected void onDetachedFromWindow() {
        attached = false;
        presented = false;
        tickerRunning = false;
        removeCallbacks(tick);
        pointer = -1;
        dragging = false;
        if (speech != null) speech.stop();
        if (fartSound != null) { fartSound.stop(); fart.stop(); }
        super.onDetachedFromWindow();
    }

    @Override
    protected void onWindowVisibilityChanged(int visibility) {
        super.onWindowVisibilityChanged(visibility);
        syncTicker();
    }

    @Override
    protected void onVisibilityChanged(View changedView, int visibility) {
        super.onVisibilityChanged(changedView, visibility);
        syncTicker();
    }

    private void syncTicker() {
        if (tick == null
                || engine == null
                || facial == null
                || faceMotion == null
                || speech == null
                || fartSound == null
                || image == null) return;
        boolean next = attached && getWindowVisibility() == VISIBLE && isShown();
        if (next == tickerRunning) return;
        tickerRunning = next;
        removeCallbacks(tick);
        if (next) {
            previous = SystemClock.uptimeMillis();
            post(tick);
        } else {
            pointer = -1;
            dragging = false;
            look = false;
            speech.stop();
            fartSound.stop();fart.stop();
        }
    }

    @Override
    protected void onDraw(Canvas canvas) {
        super.onDraw(canvas);
        canvas.save();
        canvas.translate(
                compact ? -crop.left * spriteScale : originX,
                compact ? -crop.top * spriteScale : originY);
        canvas.scale(spriteScale, spriteScale);
        canvas.drawBitmap(
                image,
                new Rect(0, 0, image.getWidth(), image.getHeight()),
                new RectF(0, 0, 320, 320),
                paint);
        canvas.restore();
        if (!presented) {
            presented = true;
            host.presented();
        }
    }

    private float logicalX(float x) {
        return compact ? x / spriteScale + crop.left : (x - originX) / spriteScale;
    }

    private float logicalY(float y) {
        return compact ? y / spriteScale + crop.top : (y - originY) / spriteScale;
    }

    @Override
    public boolean onTouchEvent(MotionEvent event) {
        int action = event.getActionMasked();
        float lx = logicalX(event.getX()), ly = logicalY(event.getY());
        if (action == MotionEvent.ACTION_DOWN) {
            if (lx < 0
                    || ly < 0
                    || lx >= 320
                    || ly >= 320
                    || (image.getPixel((int) lx, (int) ly) >>> 24) < 96) return false;
            pointer = event.getPointerId(0);
            downX = event.getRawX();
            downY = event.getRawY();
            dragging = false;
            lastDragX = lastDragY = 0;
            bellyDown = PetEngine.belly(lx, ly);
            faceMotion.touch();
            mouthPoseDown = facial.hitMouth(engine.visual(), lx, ly) ? engine.visual() : null;
            buttDown = FartReaction.hit(engine.visual(),lx,ly);
            mouseX = lx;
            mouseY = ly;
            look = true;
            return true;
        }
        if (pointer < 0) return false;
        if (action == MotionEvent.ACTION_POINTER_UP) {
            if (event.getPointerId(event.getActionIndex()) == pointer) {
                pointer = -1;
                look = false;
                int index = event.getActionIndex();
                float rawX = event.getRawX() + event.getX(index) - event.getX(0);
                float rawY = event.getRawY() + event.getY(index) - event.getY(0);
                if (dragging) host.drag(rawX - downX, rawY - downY, false, true);
                dragging = false;
            }
            return true;
        }
        if (event.findPointerIndex(pointer) < 0) {
            pointer = -1;
            look = false;
            if (dragging) host.drag(lastDragX, lastDragY, false, true);
            dragging = false;
            return true;
        }
        if (action == MotionEvent.ACTION_MOVE) {
            float dx = event.getRawX() - downX, dy = event.getRawY() - downY;
            lastDragX = dx;
            lastDragY = dy;
            boolean start =
                    !dragging
                            && Math.hypot(dx, dy) >= 8 * getResources().getDisplayMetrics().density;
            if (start) {
                dragging = true;
                speech.stop();
                fart.stop();fartSound.stop();
                faceMotion.touch();
                engine.beginDrag();
            }
            if (dragging) host.drag(dx, dy, start, false);
            mouseX = lx;
            mouseY = ly;
            return true;
        }
        if (action == MotionEvent.ACTION_UP) {
            if (dragging) host.drag(event.getRawX() - downX, event.getRawY() - downY, false, true);
            else {
                performClick();
                faceMotion.touch();
                if (engine.visual().equals(mouthPoseDown)
                        && facial.hitMouth(engine.visual(), lx, ly)) {
                    fart.stop();fartSound.stop();speech.play();
                }
                else if (buttDown && FartReaction.hit(engine.visual(),lx,ly)) {
                    speech.stop();if(fart.start(engine.visual())) fartSound.play();
                }
                else {
                    speech.stop();
                    engine.tap(bellyDown && PetEngine.belly(lx, ly));
                }
            }
            pointer = -1;
            look = false;
            return true;
        }
        if (action == MotionEvent.ACTION_CANCEL) {
            if (dragging) host.drag(event.getRawX() - downX, event.getRawY() - downY, false, true);
            pointer = -1;
            look = false;
            return true;
        }
        return true;
    }

    @Override
    public boolean performClick() {
        super.performClick();
        return true;
    }

    @Override
    public boolean onHoverEvent(MotionEvent e) {
        look = e.getActionMasked() != MotionEvent.ACTION_HOVER_EXIT;
        mouseX = logicalX(e.getX());
        mouseY = logicalY(e.getY());
        return true;
    }
}
