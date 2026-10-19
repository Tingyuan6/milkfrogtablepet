using System;
using System.Collections.Generic;

namespace Naifrog.Core
{
    public enum LaughPhase { Off, Preparing, Entry, Loop, FinishFrame, Exit, ReverseEntry, Recover, RecoverForward }
    public sealed class LaughSequence
    {
        public string[] frames {get;set;}
        public string[] entry {get;set;}
        public Dictionary<string,string[]> exits {get;set;}
        public string audio {get;set;}
        public int fps {get;set;}
        public int durationMs {get;set;}
        public int entryDurationMs {get;set;}
        public int exitDurationMs {get;set;}
        public int exitStride {get;set;}
        public int recoveryFirstFrame {get;set;}
        public int recoveryLastFrame {get;set;}
        public int recoveryExitFrame {get;set;}
        public int recoveryForwardFirstFrame {get;set;}
        public int recoveryForwardExitFrame {get;set;}
    }
    // Separate from the normal posture controller: reaction cancellation never changes
    // the user's saved layout. Every 100ms of video has a prepared path back to idle.
    public sealed class LaughTimeline
    {
        readonly LaughSequence sequence;
        double time;
        int exitIndex;
        public LaughPhase Phase {get;private set;}
        public bool Active {get{return Phase!=LaughPhase.Off;}}
        public int AudioEpoch {get;private set;}
        public int FrameIndex {get{return Math.Min(sequence.frames.Length-1,(int)(time*sequence.fps/1000));}}
        public double PositionMs {get{return time;}}
        public LaughTimeline(LaughSequence sequence)
        {
            if(sequence==null||sequence.frames==null||sequence.frames.Length<2||sequence.fps<=0||sequence.exitStride<=0||sequence.entryDurationMs<=0||sequence.exitDurationMs<=0||sequence.durationMs<=0||sequence.entry==null||sequence.entry.Length<2)throw new ArgumentException("Invalid laugh sequence");
            for(int i=0;i<sequence.frames.Length;i+=sequence.exitStride)if(!sequence.exits.ContainsKey(i.ToString()))throw new ArgumentException("Missing laugh exit "+i);
            if(!sequence.exits.ContainsKey((sequence.frames.Length-1).ToString()))throw new ArgumentException("Missing final laugh exit");
            this.sequence=sequence;
        }
        public void Start(){if(Active)return;Phase=LaughPhase.Preparing;time=0;}
        public void Ready(){if(Phase==LaughPhase.Preparing){Phase=LaughPhase.Entry;time=0;}}
        public void Stop()
        {
            if(Phase==LaughPhase.Preparing){Phase=LaughPhase.Off;return;}
            if(Phase==LaughPhase.Entry){Phase=LaughPhase.ReverseEntry;return;}
            if(Phase!=LaughPhase.Loop)return;
            if(sequence.recoveryForwardFirstFrame>0&&FrameIndex>=sequence.recoveryForwardFirstFrame&&FrameIndex<=sequence.recoveryForwardExitFrame){Phase=LaughPhase.RecoverForward;return;}
            if(FrameIndex>=sequence.recoveryFirstFrame&&FrameIndex<=sequence.recoveryLastFrame&&sequence.recoveryFirstFrame>0){Phase=LaughPhase.Recover;return;}
            exitIndex=Math.Min(sequence.frames.Length-1,(int)Math.Ceiling(time*sequence.fps/1000/sequence.exitStride)*sequence.exitStride);
            Phase=LaughPhase.FinishFrame;
        }
        public void Advance(double dt,double audioPosition)
        {
            if(dt<0||Double.IsNaN(dt)||Double.IsInfinity(dt))throw new ArgumentOutOfRangeException("dt");
            if(Phase==LaughPhase.Off||Phase==LaughPhase.Preparing)return;
            if(Phase==LaughPhase.ReverseEntry){time=Math.Max(0,time-dt);if(time==0)Phase=LaughPhase.Off;return;}
            if(Phase==LaughPhase.RecoverForward)
            {
                time+=dt;double target=sequence.recoveryForwardExitFrame*1000.0/sequence.fps;
                if(time<target)return;
                exitIndex=sequence.recoveryForwardExitFrame;time=0;Phase=LaughPhase.Exit;dt=0;
            }
            if(Phase==LaughPhase.Recover)
            {
                time-=dt*2;double target=sequence.recoveryExitFrame*1000.0/sequence.fps;
                if(time>target)return;
                exitIndex=sequence.recoveryExitFrame;time=0;Phase=LaughPhase.Exit;dt=0;
            }
            if(Phase==LaughPhase.Entry)
            {
                time+=dt;if(time>=sequence.entryDurationMs){dt=time-sequence.entryDurationMs;time=0;Phase=LaughPhase.Loop;AudioEpoch++;}else return;
            }
            if(Phase==LaughPhase.Loop)
            {
                time=audioPosition>=0?audioPosition:time+dt;
                if(time>=sequence.durationMs){time%=sequence.durationMs;AudioEpoch++;}return;
            }
            if(Phase==LaughPhase.FinishFrame)
            {
                time+=dt;double target=exitIndex*1000.0/sequence.fps;
                if(time<target)return;
                dt=time-target;time=0;Phase=LaughPhase.Exit;
            }
            if(Phase==LaughPhase.Exit){time+=dt;if(time>=sequence.exitDurationMs){time=0;Phase=LaughPhase.Off;}}
        }
        public string VisualKey
        {
            get
            {
                if(Phase==LaughPhase.Entry||Phase==LaughPhase.ReverseEntry)return Sample(sequence.entry,time/sequence.entryDurationMs);
                if(Phase==LaughPhase.Loop||Phase==LaughPhase.FinishFrame||Phase==LaughPhase.Recover||Phase==LaughPhase.RecoverForward)return sequence.frames[FrameIndex];
                if(Phase==LaughPhase.Exit)return Sample(sequence.exits[exitIndex.ToString()],time/sequence.exitDurationMs);
                return null;
            }
        }
        static string Sample(string[] files,double t){return files[Math.Max(0,Math.Min(files.Length-1,(int)Math.Round(t*(files.Length-1))))];}
    }
}
