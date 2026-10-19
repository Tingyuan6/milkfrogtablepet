using System;

namespace Naifrog.Graphics
{
    // Continuous mouth material, inspired by Naiwa.mp4: shaded cavity, rounded ivory
    // teeth on both jaws, and a distinct pink tongue with a central crease.
    public static class MouthSurface
    {
        public static int ColorAt(double x,double y)
        {
            double lower=Clamp((y+1)/2);
            int color=Rgb(38+25*lower,9+8*lower,12+12*lower);
            double tongue=x*x/(.60*.60)+(y-.55)*(y-.55)/(.38*.38);
            double tongueMask=Clamp((1-tongue)*8);
            if(tongueMask>0)
            {
                double light=Clamp(1-Math.Abs(x)/.7)*Clamp((.95-y)*2);
                int pink=Rgb(166+42*light,49+30*light,65+29*light);
                double crease=Clamp(1-Math.Abs(x)/.06)*Clamp((y-.27)*6)*Clamp((.79-y)*7);
                color=Mix(color,Mix(pink,Rgb(121,30,43),crease*.5),tongueMask);
            }
            double rim=Math.Sqrt(Math.Max(0,1-x*x));
            double toothPosition=(x+.88)/1.76*7,cell=toothPosition-Math.Floor(toothPosition);
            double tip=.22+.11*Math.Sqrt(Math.Max(0,1-Math.Pow((cell-.5)*2,2)));
            double upper=Clamp(((-rim+tip)-y)*18)*Clamp((y+rim-.025)*22)*Clamp((.9-Math.Abs(x))*16);
            double bottom=Clamp((y-(rim-.18))*24)*Clamp((rim-y-.015)*26)*Clamp((.82-Math.Abs(x))*16);
            double teeth=Math.Max(upper,bottom);
            if(teeth>0)
            {
                double separation=Clamp((.045-Math.Min(cell,1-cell))/.045)*.18;
                color=Mix(color,Rgb(247-30*separation,239-32*separation,216-35*separation),teeth);
            }
            return color;
        }
        static int Rgb(double r,double g,double b){return unchecked((int)0xff000000)|((int)r<<16)|((int)g<<8)|(int)b;}
        static int Mix(int a,int b,double t){int v=unchecked((int)0xff000000);for(int sh=0;sh<24;sh+=8)v|=(int)Math.Round(((a>>sh)&255)*(1-t)+((b>>sh)&255)*t)<<sh;return v;}
        static double Clamp(double x){return Math.Max(0,Math.Min(1,x));}
    }
}
