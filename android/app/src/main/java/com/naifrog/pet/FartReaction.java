package com.naifrog.pet;

public final class FartReaction {
    private double time = 3000;
    public boolean active() { return time < 3000; }
    public static boolean hit(String pose, double x, double y) {
        return "bottom".equals(pose) && Math.pow((x-78)/34,2)+Math.pow((y-248)/28,2)<=1;
    }
    public boolean start(String pose) { if (!"bottom".equals(pose)) return false; time=0; return true; }
    public void stop() { time=3000; }
    public void advance(double dt) {
        if (dt<0 || Double.isNaN(dt) || Double.isInfinite(dt)) throw new IllegalArgumentException("dt");
        time=Math.min(3000,time+dt);
    }
    public String visual() {
        if (!active()) return null;
        int frame=Math.min(89,(int)(time*30/1000));
        return "effects/fart/"+(frame<10?"00":frame<100?"0":"")+frame+".png";
    }
}
