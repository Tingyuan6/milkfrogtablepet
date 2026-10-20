using System;
namespace Naifrog.Core
{
    // A finite reaction over the existing bottom pose; never changes docking.
    public sealed class FartReaction
    {
        double time=3000;
        public bool Active {get{return time<3000;}}
        public double PositionMs {get{return time;}}
        public static bool Hit(string pose,double x,double y){return pose=="bottom"&&Math.Pow((x-78)/34,2)+Math.Pow((y-248)/28,2)<=1;}
        public bool Start(string pose){if(pose!="bottom")return false;time=0;return true;}
        public void Stop(){time=3000;}
        public void Advance(double dt){if(dt<0||Double.IsNaN(dt)||Double.IsInfinity(dt))throw new ArgumentOutOfRangeException("dt");time=Math.Min(3000,time+dt);}
        public string VisualKey {get{return Active?"effects/fart/"+Math.Min(89,(int)(time*30/1000)).ToString("000")+".png":null;}}
    }
}
