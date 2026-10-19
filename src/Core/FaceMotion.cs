using System;

namespace Naifrog.Core
{
    // Uses media time for lips and independent inactivity time for the three-second blink.
    public sealed class FaceMotion
    {
        readonly double[][] samples;
        readonly double step;
        double idleMs;
        public double Blink { get; private set; }
        public double Mouth { get; private set; }
        public double Round { get; private set; }
        public FaceMotion(double[][] samples,double step){this.samples=samples;this.step=step;}
        public void Touch(){idleMs=0;Blink=0;}
        public void Advance(double dt,bool settled,double mediaMs,bool playing)
        {
            if(!settled){idleMs=0;Blink=0;}else{idleMs+=Math.Max(0,dt);Blink=idleMs<3000?0:BlinkAt(idleMs%3000);}
            double target=0,round=0;
            if(playing&&mediaMs>=0&&mediaMs<step*(samples.Length-1))
            {
                double position=mediaMs/step;int i=(int)position;double t=position-i;
                target=samples[i][0]*(1-t)+samples[i+1][0]*t;
                round=samples[i][1]*(1-t)+samples[i+1][1]*t;
            }
            double ease=1-Math.Exp(-Math.Max(0,dt)/(target>Mouth?22:42));
            Mouth+=(target-Mouth)*ease;Round+=(round-Round)*ease;
            if(Mouth<.003)Mouth=0;
        }
        public static double BlinkAt(double phase)
        {
            if(phase<0||phase>=240)return 0;
            if(phase<80)return Smooth(phase/80);
            if(phase<105)return 1;
            return 1-Smooth((phase-105)/135);
        }
        static double Smooth(double t){return t*t*(3-2*t);}
    }
}
