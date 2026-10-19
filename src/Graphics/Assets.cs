using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Web.Script.Serialization;
using Naifrog.Core;

namespace Naifrog.Graphics
{
    public sealed class PoseInfo
    {
        public string file { get; set; }
        public int[] bounds { get; set; }
        public double[] contact { get; set; }
        public double[] head { get; set; }
        public double[] dragPivot { get; set; }
    }
    public sealed class ClipInfo
    {
        public string from { get; set; }
        public string to { get; set; }
        public string[] files { get; set; }
        public int durationMs { get; set; }
    }
    public sealed class SwayInfo
    {
        public string[] frames { get; set; }
        public int[] durationsMs { get; set; }
        public int totalMs { get; set; }
    }
    public sealed class EyeInfo
    {
        public double cx { get; set; }
        public double cy { get; set; }
        public double rx { get; set; }
        public double ry { get; set; }
    }
    public sealed class Manifest
    {
        public int version { get; set; }
        public int[] canvas { get; set; }
        public Dictionary<string,PoseInfo> poses { get; set; }
        public SwayInfo sway { get; set; }
        public EyeInfo[] eyes { get; set; }
        public ClipInfo[] clips { get; set; }
    }

    public sealed class Assets : IClips, IDisposable
    {
        public readonly Manifest Manifest;
        public readonly string Root;
        readonly Dictionary<string,Bitmap> images = new Dictionary<string,Bitmap>();
        readonly Dictionary<string,Clip> clips = new Dictionary<string,Clip>();
        readonly HashSet<string> poseFiles = new HashSet<string>();
        readonly LinkedList<string> recent = new LinkedList<string>();
        readonly Dictionary<string,LinkedListNode<string>> nodes = new Dictionary<string,LinkedListNode<string>>();
        public Assets(string root)
        {
            Root=Path.GetFullPath(root);
            Manifest=new JavaScriptSerializer().Deserialize<Manifest>(File.ReadAllText(Path.Combine(Root,"manifest.json")));
            if(Manifest.version!=1 || Manifest.canvas[0]!=320 || Manifest.canvas[1]!=320) throw new InvalidDataException("Unsupported assets");
            int total=0; foreach(int ms in Manifest.sway.durationsMs) total+=ms;
            if(Manifest.sway.frames.Length!=37 || total!=4390) throw new InvalidDataException("Sway timing changed");
            foreach(ClipInfo c in Manifest.clips)
            {
                if(c.durationMs<=0 || c.files.Length<2) throw new InvalidDataException("Invalid clip");
                clips[c.from+"|"+c.to]=new Clip { From=c.from,To=c.to,Frames=c.files,DurationMs=c.durationMs };
            }
            foreach(PoseInfo p in Manifest.poses.Values) poseFiles.Add(p.file);
            foreach(PoseInfo p in Manifest.poses.Values) LoadFile(p.file);
        }
        public Clip Find(string from,string to) { Clip c; return clips.TryGetValue(from+"|"+to,out c)?c:null; }
        public Bitmap Get(string key) { PoseInfo p; return LoadFile(Manifest.poses.TryGetValue(key,out p)?p.file:key); }
        Bitmap LoadFile(string relative)
        {
            Bitmap image; if(images.TryGetValue(relative,out image)){LinkedListNode<string> n;if(nodes.TryGetValue(relative,out n)){recent.Remove(n);recent.AddLast(n);}return image;}
            string file=Path.GetFullPath(Path.Combine(Root,relative.Replace('/',Path.DirectorySeparatorChar)));
            if(!file.StartsWith(Root+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Asset path outside bundle");
            using(var source=new Bitmap(file))
            {
                image=new Bitmap(320,320,PixelFormat.Format32bppPArgb);
                using(var g=System.Drawing.Graphics.FromImage(image)) { g.CompositingMode=System.Drawing.Drawing2D.CompositingMode.SourceCopy; g.DrawImage(source,new Rectangle(0,0,320,320),0,0,320,320,GraphicsUnit.Pixel); }
            }
            images.Add(relative,image);
            if(!poseFiles.Contains(relative))nodes[relative]=recent.AddLast(relative);
            while(images.Count>80&&recent.Count>0){string first=recent.First.Value;recent.RemoveFirst();nodes.Remove(first);images[first].Dispose();images.Remove(first);}
            return image;
        }
        public void Dispose() { foreach(Bitmap b in images.Values)b.Dispose(); images.Clear(); }
    }

    public sealed class Gaze : IDisposable
    {
        readonly Bitmap original, result;
        readonly int[] pixels, rendered;
        readonly EyeInfo[] eyes;
        public double X { get; private set; }
        public double Y { get; private set; }
        double renderedX=100,renderedY=100;
        public Gaze(Bitmap source,EyeInfo[] eyes)
        {
            original=source; this.eyes=eyes;
            pixels=new int[320*320]; rendered=new int[pixels.Length];
            var data=source.LockBits(new Rectangle(0,0,320,320),ImageLockMode.ReadOnly,PixelFormat.Format32bppPArgb);
            Marshal.Copy(data.Scan0,pixels,0,pixels.Length);source.UnlockBits(data);
            result=new Bitmap(320,320,PixelFormat.Format32bppPArgb);
        }
        public bool Advance(double mouseX,double mouseY,bool enabled,double deltaMs)
        {
            double dx=mouseX-160,dy=mouseY-53;
            double outsideX=Math.Max(0,Math.Abs(mouseX-160)-74),outsideY=Math.Max(0,Math.Max(30-mouseY,mouseY-290));
            double distance=Math.Sqrt(outsideX*outsideX+outsideY*outsideY);
            double factor=enabled?Math.Max(0,Math.Min(1,(140-distance)/30)):0;
            double length=Math.Sqrt(dx*dx+dy*dy),tx=length>1?dx/length*2*factor:0,ty=length>1?dy/length*1.6*factor:0;
            double f=1-Math.Exp(-Math.Min(100,deltaMs)/90);
            X+=(tx-X)*f;Y+=(ty-Y)*f;
            if(Math.Abs(X)<.005)X=0;if(Math.Abs(Y)<.005)Y=0;
            return Math.Abs(X-renderedX)>.035 || Math.Abs(Y-renderedY)>.035;
        }
        public Bitmap Render()
        {
            if(Math.Abs(X)<.001&&Math.Abs(Y)<.001){renderedX=X;renderedY=Y;return original;}
            Array.Copy(pixels,rendered,pixels.Length);
            foreach(EyeInfo e in eyes)
                for(int y=(int)Math.Floor(e.cy-e.ry);y<=Math.Ceiling(e.cy+e.ry);y++)
                    for(int x=(int)Math.Floor(e.cx-e.rx);x<=Math.Ceiling(e.cx+e.rx);x++)
                    {
                        double r=(x-e.cx)*(x-e.cx)/(e.rx*e.rx)+(y-e.cy)*(y-e.cy)/(e.ry*e.ry);
                        if(r>=1)continue;
                        double f=Math.Pow(1-r,1.5),sx=x-X*f,sy=y-Y*f;
                        int ix=(int)sx,iy=(int)sy;double fx=sx-ix,fy=sy-iy;
                        int value=0;
                        for(int shift=0;shift<=24;shift+=8)
                        {
                            double c=((pixels[iy*320+ix]>>shift)&255)*(1-fx)*(1-fy)+((pixels[iy*320+ix+1]>>shift)&255)*fx*(1-fy)+((pixels[(iy+1)*320+ix]>>shift)&255)*(1-fx)*fy+((pixels[(iy+1)*320+ix+1]>>shift)&255)*fx*fy;
                            value|=((int)Math.Round(c))<<shift;
                        }
                        rendered[y*320+x]=value;
                    }
            var data=result.LockBits(new Rectangle(0,0,320,320),ImageLockMode.WriteOnly,PixelFormat.Format32bppPArgb);
            Marshal.Copy(rendered,0,data.Scan0,rendered.Length);result.UnlockBits(data);
            renderedX=X;renderedY=Y;return result;
        }
        public void Dispose(){result.Dispose();}
    }
}
