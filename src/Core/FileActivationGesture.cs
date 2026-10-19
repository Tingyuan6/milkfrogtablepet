using System;

namespace Naifrog.Core
{
    // The first click only caches a verified file item. Opening intent needs the
    // second click on the same window/item region within the user's system limits.
    public sealed class FileActivationGesture
    {
        readonly uint interval;
        readonly int width,height;
        bool pending;
        uint time;
        int x,y;
        long window;
        string file;
        public FileActivationGesture(uint interval,int width,int height){this.interval=interval;this.width=width;this.height=height;}
        public void Remember(uint at,int px,int py,long handle,string verifiedFile){pending=true;time=at;x=px;y=py;window=handle;file=verifiedFile;}
        public string Complete(uint at,int px,int py,long handle)
        {
            if(!pending||handle!=window||unchecked(at-time)>=interval||Math.Abs(px-x)>width||Math.Abs(py-y)>height)return null;
            string result=file;Reset();return result;
        }
        public void Reset(){pending=false;file=null;}
    }
}
