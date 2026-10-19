using System;
using System.IO;
using System.Web.Script.Serialization;
using Naifrog.Core;

internal static class LaughTests
{
    static int checks;
    static void Check(bool ok,string name){checks++;if(!ok)throw new Exception(name);}
    static LaughTimeline Playing(LaughSequence sequence,double ms)
    {
        var t=new LaughTimeline(sequence);t.Start();t.Ready();t.Advance(sequence.entryDurationMs,-1);t.Advance(0,ms);return t;
    }
    static int Main(string[] args)
    {
        try
        {
            var sequence=new JavaScriptSerializer().Deserialize<LaughSequence>(File.ReadAllText(Path.Combine(args[0],"laugh.json")));
            Check(sequence.frames.Length==449&&sequence.durationMs==14967,"video timing");
            Check(sequence.entry[0]=="poses/idle.png"&&sequence.entry[sequence.entry.Length-1]==sequence.frames[0],"entry endpoints");
            foreach(var pair in sequence.exits)
            {
                Check(pair.Value[0]==sequence.frames[Int32.Parse(pair.Key)]&&pair.Value[pair.Value.Length-1]=="poses/idle.png","exit endpoints "+pair.Key);
                foreach(string file in pair.Value)Check(File.Exists(Path.Combine(args[0],file)),"missing exit file "+file);
            }
            int slowest=0;
            for(int frame=0;frame<sequence.frames.Length;frame++)
            {
                var t=Playing(sequence,frame*1000.0/sequence.fps+.001);Check(t.FrameIndex==frame,"media frame "+frame);
                string visual=t.VisualKey;t.Stop();int epoch=t.AudioEpoch;
                Check(t.VisualKey==visual,"stop begins on current video frame "+frame);
                int elapsed=0;
                while(t.Active&&elapsed<1600)
                {
                    Check(t.VisualKey!=null&&File.Exists(Path.Combine(args[0],t.VisualKey)),"return frame available");
                    t.Advance(16,-1);elapsed+=16;
                }
                Check(!t.Active,"every video frame can stop "+frame);Check(t.AudioEpoch==epoch,"no audio restart after stop");
                slowest=Math.Max(slowest,elapsed);
            }
            var timeline=new LaughTimeline(sequence);timeline.Start();timeline.Stop();Check(!timeline.Active,"cancel before entry");
            timeline.Start();timeline.Ready();timeline.Advance(120,-1);string halfway=timeline.VisualKey;timeline.Stop();Check(timeline.VisualKey==halfway,"entry cancellation keeps frame");timeline.Advance(120,-1);Check(!timeline.Active,"entry reversal finishes");
            timeline=Playing(sequence,2500);Check(timeline.FrameIndex==75,"audio clock controls video");timeline.Advance(sequence.durationMs,-1);Check(timeline.Active&&timeline.AudioEpoch==2,"repeat video and audio epoch");
            var gesture=new FileActivationGesture(500,4,4);
            Check(gesture.Complete(1,10,10,1)==null,"single click never opens");
            gesture.Remember(1,10,10,1,"a.txt");Check(gesture.Complete(100,12,12,1)=="a.txt","verified same-item double click");Check(gesture.Complete(120,12,12,1)==null,"one activation per pair");
            gesture.Remember(1,10,10,1,null);Check(gesture.Complete(100,10,10,1)==null,"folder or blank item cannot trigger");
            gesture.Remember(1,10,10,1,"a.txt");Check(gesture.Complete(600,10,10,1)==null,"slow pair");
            gesture.Remember(1,10,10,1,"a.txt");Check(gesture.Complete(100,20,10,1)==null,"different item region");
            gesture.Remember(1,10,10,1,"a.txt");Check(gesture.Complete(100,10,10,2)==null,"different window");
            gesture.Remember(UInt32.MaxValue-10,10,10,1,"a.txt");Check(gesture.Complete(5,10,10,1)=="a.txt","system timestamp wrap");
            gesture.Remember(1,10,10,1,"a.txt");gesture.Reset();Check(gesture.Complete(100,10,10,1)==null,"other clicks cancel pending pair");
            Console.WriteLine("PASS "+checks+" laugh/activation checks; all 449 interruption positions; longest visual recovery "+slowest+"ms");return 0;
        }
        catch(Exception e){Console.Error.WriteLine(e);return 1;}
    }
}
