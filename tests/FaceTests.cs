using System;
using System.Drawing;
using System.IO;
using Naifrog.Core;
using Naifrog.Graphics;

internal static class FaceTests
{
    static int checks;
    static void Check(bool ok,string message){checks++;if(!ok)throw new Exception(message);}
    static int Main(string[] args)
    {
        try
        {
            Directory.CreateDirectory(args[1]);
            using(var assets=new Assets(args[0]))using(var face=new FaceRenderer(assets))
            {
                var rig=face.Rig;Check(rig.speech.durationMs==2000&&rig.speech.samples.Length==201,"two-second voice timeline");
                var motion=new FaceMotion(rig.speech.samples,rig.speech.stepMs);
                motion.Advance(2999,true,0,false);Check(motion.Blink==0,"no blink before three seconds");
                motion.Advance(41,true,0,false);Check(motion.Blink>.4&&motion.Blink<.6,"interpolated closing eyelid");
                motion.Advance(45,true,0,false);Check(motion.Blink==1,"fully closed frame");
                motion.Advance(95,true,0,false);Check(motion.Blink>0&&motion.Blink<1,"interpolated opening eyelid");
                motion.Advance(60,true,0,false);Check(motion.Blink==0,"eyes reopen");
                motion.Advance(2845,true,0,false);Check(motion.Blink==1,"next blink starts three seconds after previous");
                motion.Touch();Check(motion.Blink==0,"interaction resets inactivity");motion.Advance(10000,false,0,false);Check(motion.Blink==0,"no detached transition blink");
                double maximum=0;for(int ms=0;ms<2000;ms+=10){motion.Advance(10,true,ms,true);maximum=Math.Max(maximum,motion.Mouth);}
                Check(maximum>.6,"voice drives opening");motion.Advance(500,true,2000,false);Check(motion.Mouth==0,"mouth closes after playback");
                foreach(var pair in rig.faces)
                {
                    string pose=pair.Key;var info=pair.Value;Bitmap original=assets.Get(pose);
                    Check(face.HitMouth(pose,info.mouth[0],info.mouth[1]),"mouth hit "+pose);Check(!face.HitMouth(pose,160,166),"belly distinct "+pose);
                    var neutral=face.Render(pose,0,0,0);Check(Object.ReferenceEquals(neutral,original),"neutral pixels unchanged "+pose);
                    for(int k=0;k<7;k++)
                    {
                        double blink=new[]{0,.25,.65,1,.65,.25,0}[k];var frame=face.Render(pose,blink,0,0);
                        frame.Save(Path.Combine(args[1],pose+"-blink-"+k+".png"));
                        for(int y=0;y<320;y++)for(int x=0;x<320;x++)if(frame.GetPixel(x,y).A!=original.GetPixel(x,y).A)throw new Exception("blink silhouette changed "+pose);
                    }
                    Check(true,"blink alpha preserved "+pose);
                    for(int k=0;k<5;k++)face.Render(pose,0,k/4.0,.4).Save(Path.Combine(args[1],pose+"-mouth-"+k+".png"));
                    double hx=0,hy=0;foreach(var e in info.eyes){hx+=e[0];hy+=e[1];}hx/=info.eyes.Length;hy/=info.eyes.Length;
                    face.Advance(pose,hx-40,hy,true,1000);Check(face.X<-.9,"left pupil follow "+pose);
                    Bitmap look=face.Render(pose,0,0,0);int changes=0;
                    for(int y=0;y<320;y++)for(int x=0;x<320;x++){if(look.GetPixel(x,y)!=original.GetPixel(x,y))changes++;Check(look.GetPixel(x,y).A==original.GetPixel(x,y).A,"gaze alpha");}
                    Check(changes>3,"gaze visible "+pose);face.Advance(pose,hx,hy,false,1000);for(int n=0;n<10;n++)face.Advance(pose,hx,hy,false,100);
                    Check(face.X==0&&face.Y==0,"gaze returns "+pose);
                }
                Console.WriteLine("PASS "+checks+" facial checks; six poses, three-second blink, speech envelope, silhouettes");
            }
            return 0;
        }
        catch(Exception e){Console.Error.WriteLine(e);return 1;}
    }
}
