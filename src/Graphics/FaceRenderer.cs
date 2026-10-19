using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Web.Script.Serialization;

namespace Naifrog.Graphics
{
    public sealed class FaceInfo { public double[][] eyes {get;set;} public double[] mouth {get;set;} }
    public sealed class SpeechInfo { public string file {get;set;} public int durationMs {get;set;} public int stepMs {get;set;} public double[][] samples {get;set;} }
    public sealed class FaceRig { public Dictionary<string,FaceInfo> faces {get;set;} public SpeechInfo speech {get;set;} }
    public sealed class FaceRenderer : IDisposable
    {
        readonly Assets assets;
        readonly Dictionary<string,int[]> sources=new Dictionary<string,int[]>();
        readonly int[] output=new int[320*320];
        readonly Bitmap result=new Bitmap(320,320,PixelFormat.Format32bppPArgb);
        public readonly FaceRig Rig;
        public double X {get;private set;} public double Y {get;private set;}
        string previous="";
        public FaceRenderer(Assets assets){this.assets=assets;Rig=new JavaScriptSerializer().Deserialize<FaceRig>(File.ReadAllText(Path.Combine(assets.Root,"face.json")));}
        public bool Has(string pose){return Rig.faces.ContainsKey(pose);}
        public bool HitMouth(string pose,double x,double y)
        {
            FaceInfo face;if(!Rig.faces.TryGetValue(pose,out face))return false;
            var m=face.mouth;double a=m[3]*Math.PI/180,dx=x-m[0],dy=y-m[1],u=dx*Math.Cos(a)+dy*Math.Sin(a),v=-dx*Math.Sin(a)+dy*Math.Cos(a);
            return u*u/Math.Pow(m[2]+5,2)+v*v/81<=1;
        }
        public void Advance(string pose,double px,double py,bool enabled,double dt)
        {
            if(previous!=pose){X=Y=0;previous=pose;}
            FaceInfo f;double tx=0,ty=0;
            if(enabled&&Rig.faces.TryGetValue(pose,out f))
            {
                double hx=0,hy=0;foreach(var e in f.eyes){hx+=e[0];hy+=e[1];}hx/=f.eyes.Length;hy/=f.eyes.Length;
                double dx=px-hx,dy=py-hy,len=Math.Sqrt(dx*dx+dy*dy),factor=Clamp((160-len)/50);
                if(len>1){tx=dx/len*2*factor;ty=dy/len*1.6*factor;}
            }
            double ease=1-Math.Exp(-Math.Min(100,dt)/90);X+=(tx-X)*ease;Y+=(ty-Y)*ease;
            if(Math.Abs(X)<.01)X=0;if(Math.Abs(Y)<.01)Y=0;
        }
        public string Stamp(double blink,double mouth,double round){return Math.Round(X*25)+":"+Math.Round(Y*25)+":"+Math.Round(blink*100)+":"+Math.Round(mouth*50)+":"+Math.Round(round*20);}
        int[] Pixels(string pose)
        {
            int[] p;if(sources.TryGetValue(pose,out p))return p;
            p=new int[320*320];var b=assets.Get(pose);var bits=b.LockBits(new Rectangle(0,0,320,320),ImageLockMode.ReadOnly,PixelFormat.Format32bppPArgb);Marshal.Copy(bits.Scan0,p,0,p.Length);b.UnlockBits(bits);sources.Add(pose,p);return p;
        }
        public Bitmap Render(string pose,double blink,double mouth,double round)
        {
            FaceInfo face;if(!Rig.faces.TryGetValue(pose,out face)||(blink==0&&mouth==0&&X==0&&Y==0))return assets.Get(pose);
            int[] src=Pixels(pose);Array.Copy(src,output,src.Length);
            foreach(var e in face.eyes)
            {
                double cx=e[0],cy=e[1],rx=e[2],ry=e[3];
                for(int y=(int)Math.Floor(cy-ry-1);y<=Math.Ceiling(cy+ry+1);y++)for(int x=(int)Math.Floor(cx-rx-1);x<=Math.Ceiling(cx+rx+1);x++)
                {
                    double u=(x-cx)/rx,v=(y-cy)/ry,r=u*u+v*v;if(r>=1.2)continue;
                    int p=y*320+x;if(((uint)src[p]>>24)<230)continue;double fall=Math.Pow(Math.Max(0,1-r),1.5);
                    output[p]=Sample(src,x-X*rx/7.3*fall,y-Y*ry/9*fall);
                    if(blink>0)
                    {
                        double lid=-1+2.12*blink+.12*u*u;
                        double cover=Clamp((lid-v)*ry+.5)*Clamp((1.2-r)*rx);
                        int skin=Skin(src,cx+u*rx,cy-ry-3,cx+u*rx,cy+ry+3);
                        output[p]=Mix(output[p],skin,cover);
                        if(blink>.15&&Math.Abs(v-lid)<.12&&Math.Abs(u)<.86)output[p]=Mix(output[p],Shade(skin,.65),.45*Clamp((blink-.15)*3));
                        double crease=Clamp(1-Math.Abs(v-(.12+.22*u*u))*ry/.75)*Clamp((blink-.65)/.35)*Clamp((.95-Math.Abs(u))*7);
                        output[p]=Mix(output[p],Shade(skin,.52),crease*.8);
                    }
                }
            }
            if(mouth>0)Mouth(src,face.mouth,mouth,round);
            for(int p=0;p<output.Length;p++)output[p]=(output[p]&0x00ffffff)|(src[p]&unchecked((int)0xff000000));
            var data=result.LockBits(new Rectangle(0,0,320,320),ImageLockMode.WriteOnly,PixelFormat.Format32bppPArgb);Marshal.Copy(output,0,data.Scan0,output.Length);result.UnlockBits(data);return result;
        }
        void Mouth(int[] src,double[] m,double opening,double rounding)
        {
            double a=m[3]*Math.PI/180,c=Math.Cos(a),s=Math.Sin(a),rx=m[2]*(1-.2*rounding),ry=.6+m[2]*.58*opening,center=ry*.35;
            int radius=(int)Math.Ceiling(m[2]+12);
            for(int y=Math.Max(0,(int)m[1]-radius);y<Math.Min(320,m[1]+radius);y++)for(int x=Math.Max(0,(int)m[0]-radius);x<Math.Min(320,m[0]+radius);x++)
            {
                double dx=x-m[0],dy=y-m[1],u=dx*c+dy*s,v=-dx*s+dy*c;
                double edge=Clamp((m[2]*1.25-Math.Abs(u))/2)*Clamp((Math.Max(6,ry+3)-Math.Abs(v))/2);
                if(edge<=0)continue;
                int p=y*320+x;if(((uint)src[p]>>24)<230)continue;int skin=Skin(src,m[0]+u*c+7*s,m[1]+u*s-7*c,m[0]+u*c-11*s,m[1]+u*s+11*c);
                output[p]=Mix(output[p],skin,edge*Clamp(opening*9));
                double oval=u*u/(rx*rx)+(v-center)*(v-center)/(ry*ry),inside=Clamp((1-oval)*Math.Min(rx,ry));
                if(inside>0)
                {
                    double lower=Clamp((v-center)/ry),tongue=Clamp((lower-.3)*2)*(1-Clamp(Math.Abs(u)/rx));
                    int cavity=Color.FromArgb(255,(int)(48+33*lower+38*tongue),(int)(25+11*lower+12*tongue),(int)(15+7*lower+7*tongue)).ToArgb();
                    output[p]=Mix(output[p],cavity,inside*Clamp(opening*10));
                }
                else if(oval<1.45)output[p]=Mix(output[p],Shade(skin,v<center?.72:1.08),Clamp((1.45-oval)*2)*Clamp(opening*5));
            }
        }
        static int Skin(int[] src,double ax,double ay,double bx,double by)
        {
            int a=Sample(src,ax,ay),b=Sample(src,bx,by);bool av=Yellow(a),bv=Yellow(b);if(av&&bv)return Mix(a,b,.35);if(av)return a;if(bv)return b;
            double cx=(ax+bx)/2,cy=(ay+by)/2;
            for(int r=6;r<=24;r+=6)foreach(var d in new[]{new[]{-r,0},new[]{r,0},new[]{0,-r},new[]{0,r}}){int c=Sample(src,cx+d[0],cy+d[1]);if(Yellow(c))return c;}
            return a;
        }
        static bool Yellow(int c){int r=(c>>16)&255,g=(c>>8)&255,b=c&255;return ((uint)c>>24)>230&&r>130&&r>g*1.05&&g>b*1.2;}
        static int Shade(int color,double factor){int v=color&unchecked((int)0xff000000);for(int sh=0;sh<24;sh+=8)v|=Math.Min(255,(int)(((color>>sh)&255)*factor))<<sh;return v;}
        static int Mix(int a,int b,double t){int v=0;for(int sh=0;sh<=24;sh+=8)v|=(int)Math.Round(((a>>sh)&255)*(1-t)+((b>>sh)&255)*t)<<sh;return v;}
        static int Sample(int[] p,double x,double y){x=Math.Max(0,Math.Min(318.99,x));y=Math.Max(0,Math.Min(318.99,y));int ix=(int)x,iy=(int)y;double fx=x-ix,fy=y-iy;return Mix(Mix(p[iy*320+ix],p[iy*320+ix+1],fx),Mix(p[(iy+1)*320+ix],p[(iy+1)*320+ix+1],fx),fy);}
        static double Clamp(double x){return Math.Max(0,Math.Min(1,x));}
        public void Dispose(){result.Dispose();}
    }
}
