using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using Naifrog.Graphics;

namespace Naifrog.Windows
{
    internal static class NativeProbe
    {
        [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr window,int message,IntPtr w,IntPtr l);
        [DllImport("user32.dll")] static extern bool ScreenToClient(IntPtr window,ref Native.Point p);
        sealed class Backdrop : Form
        {
            public int Clicks;
            protected override bool ShowWithoutActivation{get{return true;}}
            protected override CreateParams CreateParams{get{var c=base.CreateParams;c.ExStyle|=0x08000000|0x80;return c;}}
            protected override void WndProc(ref Message m){if(m.Msg==0x21){m.Result=new IntPtr(3);return;}base.WndProc(ref m);}
        }
        public static int Background(string[] args)
        {
            string file=args[1];int px=Int32.Parse(args[2]),py=Int32.Parse(args[3]),side=Int32.Parse(args[4]);
            using(var form=new Backdrop { AutoScaleMode=AutoScaleMode.None,FormBorderStyle=FormBorderStyle.None,ShowInTaskbar=false,TopMost=true,StartPosition=FormStartPosition.Manual,Location=new Point(px,py),Size=new Size(side,side),BackColor=Color.FromArgb(177,195,209) })
            {
                Action write=delegate { File.WriteAllText(file,new JavaScriptSerializer().Serialize(new {handle=form.Handle.ToInt64(),clicks=form.Clicks})); };
                form.Shown+=delegate {write();};form.MouseUp+=delegate {form.Clicks++;write();};var timeout=new System.Windows.Forms.Timer {Interval=45000};timeout.Tick+=delegate{form.Close();};timeout.Start();Application.Run(form);timeout.Dispose();
            }
            return 0;
        }
        static void Pump(int milliseconds){var w=Stopwatch.StartNew();while(w.ElapsedMilliseconds<milliseconds){Application.DoEvents();Thread.Sleep(5);}}
        static void ClickAt(int x,int y)
        {
            var p=new Native.Point(x,y);IntPtr target=Native.WindowFromPoint(p);ScreenToClient(target,ref p);IntPtr packed=new IntPtr((p.Y<<16)|(p.X&65535));var pet=Control.FromHandle(target) as PetWindow;
            try{if(pet!=null)pet.ProbeExpectedMessage=0x201;SendMessage(target,0x201,new IntPtr(1),packed);if(pet!=null)pet.ProbeExpectedMessage=0x202;SendMessage(target,0x202,IntPtr.Zero,packed);}
            finally{if(pet!=null)pet.ProbeExpectedMessage=0;}
            Pump(70);
        }
        static Native.Point BodyPoint(PetWindow pet,int logicalX,int logicalY)
        {
            Point origin=pet.CanvasOrigin;return new Native.Point(origin.X+(int)Math.Round(logicalX*pet.LogicalScale),origin.Y+(int)Math.Round(logicalY*pet.LogicalScale));
        }
        static void Route(PetWindow pet,Native.Point global,int message)
        {
            pet.ProbeCursor=global;Native.Point local=global;ScreenToClient(pet.Handle,ref local);
            pet.ProbeExpectedMessage=message;
            try{SendMessage(pet.Handle,message,message==0x202?IntPtr.Zero:new IntPtr(1),new IntPtr((local.Y<<16)|(local.X&65535)));}
            finally{pet.ProbeExpectedMessage=0;}
        }
        static void DragTo(PetWindow pet,Native.Point destination,int grabX,int grabY)
        {
            Route(pet,BodyPoint(pet,grabX,grabY),0x201);Route(pet,destination,0x200);Pump(150);Route(pet,destination,0x202);Pump(750);
        }
        public static int Monitor(string data)
        {
            Directory.CreateDirectory(data);
            using(var form=new Form())
            {
                IntPtr handle=form.Handle;
                using(var monitor=new ExplorerFileMonitor(form,delegate{},delegate{}))
                {
                    for(int n=0;n<60&&!monitor.ShellProbeFinished;n++)Pump(50);
                    var report=new {hooks=monitor.Available,shell=monitor.ShellMetadataAvailable,finished=monitor.ShellProbeFinished,diagnostic=monitor.Diagnostic};
                    File.WriteAllText(Path.Combine(data,"monitor.json"),new JavaScriptSerializer().Serialize(report));
                    Console.WriteLine(new JavaScriptSerializer().Serialize(report));
                    return monitor.Available&&monitor.ShellMetadataAvailable?0:1;
                }
            }
        }
        public static int Run(Assets assets,string data)
        {
            Directory.CreateDirectory(data);string childFile=Path.Combine(data,"backdrop.json");if(File.Exists(childFile))File.Delete(childFile);
            Native.Point original;Native.GetCursorPos(out original);IntPtr focused=Native.GetForegroundWindow();Process child=null;
            try
            {
                Rectangle work=Screen.PrimaryScreen.WorkingArea;double ratio=Native.DpiAt(work.Left+work.Width/2,work.Top+work.Height/2);int side=(int)Math.Round(400*ratio),px=work.Left+(work.Width-side)/2,py=work.Top+(work.Height-side)/2;Func<int,int> sx=v=>px+(int)Math.Round(v*ratio),sy=v=>py+(int)Math.Round(v*ratio);
                child=Process.Start(new ProcessStartInfo(Application.ExecutablePath,"--probe-background \""+childFile+"\" "+px+" "+py+" "+side) {UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden});
                var limit=Stopwatch.StartNew();while(!File.Exists(childFile)&&limit.ElapsedMilliseconds<5000)Thread.Sleep(20);
                if(!File.Exists(childFile))throw new Exception("Probe backdrop not ready");
                using(var pet=new PetWindow(assets,data,true))
                {
                    pet.Show();pet.PositionForProbe(sx(40),sy(40));Pump(150);
                    pet.SaveSurface(Path.Combine(data,"surface.png"));Native.Rect actualRect;Native.GetWindowRect(pet.Handle,out actualRect);
                    File.WriteAllText(Path.Combine(data,"debug.json"),new JavaScriptSerializer().Serialize(new {pet=pet.Handle.ToInt64(),rect=actualRect,visible=Native.IsWindowVisible(pet.Handle),style=Native.GetWindowLongPtr(pet.Handle,-20).ToInt64(),origin=pet.CanvasOrigin,background=child.Id}));
                    IntPtr transparentTarget=Native.WindowFromPoint(new Native.Point(sx(50),sy(50)));
                    IntPtr bodyTarget=Native.WindowFromPoint(new Native.Point(sx(200),sy(206)));
                    uint targetPid;Native.GetWindowThreadProcessId(transparentTarget,out targetPid);
                    bool passesOtherProcess=targetPid==(uint)child.Id&&transparentTarget!=pet.Handle,bodyHits=bodyTarget==pet.Handle;
                    ClickAt(sx(50),sy(50));Pump(100);
                    var backdrop=new JavaScriptSerializer().Deserialize<Dictionary<string,object>>(File.ReadAllText(childFile));
                    bool actualPassThrough=Convert.ToInt32(backdrop["clicks"])==1;
                    ClickAt(sx(200),sy(206));bool clickStarts=pet.Controller.Desired=="sway";Pump(5100);
                    bool returns=pet.Controller.VisualKey=="idle";int originals=pet.PresentedSway.Count;
                    Pump(250);
                    using(var capture=new Bitmap(side,side)){using(var g=System.Drawing.Graphics.FromImage(capture))g.CopyFromScreen(px,py,0,0,capture.Size);capture.Save(Path.Combine(data,"windows-native.png"));}
                    pet.GazeForProbe(false);Pump(1300);pet.ResetFaceForProbe();Pump(100);int settled=pet.PresentCount;Pump(1000);int idleDraws=pet.PresentCount-settled;
                    ClickAt(sx(200),sy(206));Pump(900);ClickAt(sx(200),sy(206));Pump(650);bool cancels=pet.Controller.VisualKey=="idle";
                    Native.Point mouth=BodyPoint(pet,160,69);ClickAt(mouth.X,mouth.Y);Pump(200);
                    bool speechStarts=pet.SpeechPlaying,mediaAdvances=pet.SpeechPosition>80;
                    ClickAt(mouth.X,mouth.Y);bool replayRestarts=pet.SpeechPlaying&&pet.SpeechPosition<180;
                    double maxMouth=0;var lipLevels=new HashSet<int>();
                    for(int i=0;i<45;i++){Pump(50);maxMouth=Math.Max(maxMouth,pet.FaceMouth);lipLevels.Add((int)Math.Round(pet.FaceMouth*20));}
                    Pump(250);bool mouthCloses=!pet.SpeechPlaying&&pet.FaceMouth<.01;
                    pet.ResetFaceForProbe();Pump(2900);double maxBlink=0;var blinkLevels=new HashSet<int>();
                    for(int i=0;i<40;i++){Pump(15);maxBlink=Math.Max(maxBlink,pet.FaceBlink);if(pet.FaceBlink>0&&pet.FaceBlink<1)blinkLevels.Add((int)Math.Round(pet.FaceBlink*20));}
                    bool blinkSmooth=maxBlink>.95&&blinkLevels.Count>=3;
                    Point freeStart=pet.CanvasOrigin;Native.Point grab=BodyPoint(pet,160,166);
                    DragTo(pet,new Native.Point(grab.X+90,grab.Y-60),160,166);
                    bool dragWithoutClick=pet.Controller.Desired=="idle"&&pet.Controller.Layout==Core.LayoutMode.Free&&Math.Abs(pet.CanvasOrigin.X-freeStart.X-90)<=1&&Math.Abs(pet.CanvasOrigin.Y-freeStart.Y+60)<=1;
                    DragTo(pet,new Native.Point(work.Left+20,work.Top+work.Height/2),160,166);
                    bool leftDock=pet.Controller.VisualKey=="peek_left"&&Math.Abs(pet.CanvasOrigin.X+114*pet.LogicalScale-work.Left)<=1;
                    pet.GazeForProbe(true);pet.ProbeCursor=BodyPoint(pet,215,50);Pump(500);bool leftGaze=pet.FaceGazeX>1;
                    DragTo(pet,new Native.Point(work.Left+work.Width/2,work.Top+work.Height/2),175,110);
                    bool undocks=pet.Controller.VisualKey=="idle"&&pet.Controller.Layout==Core.LayoutMode.Free;
                    DragTo(pet,new Native.Point(work.Left+(int)(work.Width*.65),work.Bottom-20),160,166);
                    bool bottomDock=pet.Controller.VisualKey=="bottom"&&Math.Abs(pet.CanvasOrigin.Y+290*pet.LogicalScale-work.Bottom)<=1;
                    pet.ProbeCursor=BodyPoint(pet,222,190);Pump(500);bool bottomGaze=pet.FaceGazeX< -1;
                    DragTo(pet,new Native.Point(work.Right-20,work.Top+work.Height/2),210,240);
                    bool rightDock=pet.Controller.VisualKey=="peek_right"&&Math.Abs(pet.CanvasOrigin.X+206*pet.LogicalScale-work.Right)<=1;
                    pet.ProbeCursor=BodyPoint(pet,100,50);Pump(500);bool rightGaze=pet.FaceGazeX< -1;
                    Point laughHome=pet.CanvasOrigin;
                    pet.StartLaughterForProbe();Pump(1300);
                    bool laughStarts=pet.LaughterPhase==Core.LaughPhase.Loop&&pet.LaughterVisual!=null&&pet.LaughSoundPlaying;
                    pet.StopLaughterForProbe();bool laughAudioStops=!pet.LaughSoundPlaying;Pump(1200);
                    bool laughRestores=pet.LaughterPhase==Core.LaughPhase.Off&&pet.Controller.VisualKey=="peek_right"&&Math.Abs(pet.CanvasOrigin.X-laughHome.X)<=1&&Math.Abs(pet.CanvasOrigin.Y-laughHome.Y)<=1;
                    bool monitorAvailable,shellMetadataAvailable;
                    using(var monitor=new ExplorerFileMonitor(pet,delegate{},delegate{})){monitorAvailable=monitor.Available;for(int n=0;n<40&&!monitor.ShellMetadataAvailable;n++)Pump(50);shellMetadataAvailable=monitor.ShellMetadataAvailable;}
                    File.WriteAllText(Path.Combine(data,"laugh-native.json"),new JavaScriptSerializer().Serialize(new {reactionStartsFromRightDock=laughStarts,clickStopAudioImmediate=laughAudioStops,restoresOriginalLayoutAndPosition=laughRestores,globalHooksInstallAndDispose=monitorAvailable,shellMetadataApiAvailable=shellMetadataAvailable,triggerMethod="Controlled host activation and cancellation; actual Explorer user double-click is still a manual acceptance item. No hardware input injection."}));
                    bool focusKept=Native.GetForegroundWindow()==focused;double[] sorted=pet.PaintTimes.OrderBy(v=>v).ToArray();
                    var report=new {platform=Environment.OSVersion.VersionString,process64=Environment.Is64BitProcess,dpi=Native.DpiAt(work.Left+100,work.Top+100),inputMethod="Win32 messages routed by alpha hit target, controlled cursor source for dragging; no physical pointer injection",excludedProbeInputMessages=pet.ProbeIgnoredInputs,alphaHitOtherProcess=passesOtherProcess,transparentClickRouted=actualPassThrough,bodyHit=bodyHits,bellyStartsSway=clickStarts,originalFramesObserved=originals,originalFramesExpected=37,returnsIdle=returns,secondClickCancels=cancels,dragDoesNotClick=dragWithoutClick,leftDock=leftDock,undocks=undocks,bottomDock=bottomDock,rightDock=rightDock,foregroundPreserved=focusKept,nonActivatingStyle=(Native.GetWindowLongPtr(pet.Handle,-20).ToInt64()&0x08000000)!=0,mouseActivateResponse=SendMessage(pet.Handle,0x21,IntPtr.Zero,IntPtr.Zero).ToInt64(),settledIdleRedrawsPerSecond=idleDraws,paintP95Ms=sorted[(int)(sorted.Length*.95)],presentations=pet.PresentCount,workingSetMB=Process.GetCurrentProcess().WorkingSet64/1048576.0,gdiObjects=Native.GetGuiResources(Process.GetCurrentProcess().Handle,0)};
                    File.WriteAllText(Path.Combine(data,"windows-native.json"),new JavaScriptSerializer().Serialize(report));
                    File.WriteAllText(Path.Combine(data,"facial-native.json"),new JavaScriptSerializer().Serialize(new {mouthStartsSpeech=speechStarts,voicePositionAdvances=mediaAdvances,speechReplayRestarts=replayRestarts,lipsFollowMedia=lipLevels.Count>=5,maxMouth= maxMouth,mouthCloses=mouthCloses,autoBlinkHasInbetweens=blinkSmooth,blinkLevels=blinkLevels.Count,leftPoseGaze=leftGaze,bottomPoseGaze=bottomGaze,rightPoseGaze=rightGaze,sourceDurationMs=2000}));pet.Close();
                    File.WriteAllLines(Path.Combine(data,"events.txt"),pet.ProbeTrace);
                    return passesOtherProcess&&actualPassThrough&&bodyHits&&clickStarts&&originals==37&&returns&&cancels&&idleDraws==0&&dragWithoutClick&&leftDock&&undocks&&bottomDock&&rightDock&&speechStarts&&mediaAdvances&&replayRestarts&&lipLevels.Count>=5&&mouthCloses&&blinkSmooth&&leftGaze&&bottomGaze&&rightGaze&&laughStarts&&laughAudioStops&&laughRestores&&monitorAvailable&&shellMetadataAvailable?0:1;
                }
            }
            finally {if(child!=null&&!child.HasExited){child.CloseMainWindow();if(!child.WaitForExit(1000))child.Kill();}if(child!=null)child.Dispose();}
        }
    }
}
