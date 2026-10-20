using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Naifrog.Core;
using Naifrog.Graphics;

namespace Naifrog.Windows
{
    public sealed class PetWindow : Form
    {
        readonly Assets assets;
        readonly FaceRenderer face;
        readonly FaceMotion faceMotion;
        readonly SpeechPlayer speech;
        readonly SpeechPlayer laughSound;
        readonly SpeechPlayer fartSound;
        readonly FartReaction fart=new FartReaction();
        bool buttDown;
        readonly LaughTimeline laughter;
        ExplorerFileMonitor fileMonitor;
        int laughAudioEpoch;
        bool restoringLaughter;
        bool pendingLaughter;
        LayoutMode laughHomeLayout;
        string laughHomePose;
        double laughHomeX,laughHomeY;

        readonly Settings settings;
        readonly string dataDirectory;
        readonly Timer timer=new Timer();
        readonly Stopwatch clock=Stopwatch.StartNew();
        readonly Gesture gesture=new Gesture();
        readonly DockResolver dock=new DockResolver();
        readonly ContextMenuStrip menu=new ContextMenuStrip();
        readonly NotifyIcon tray;
        double lastTick,x,y,scale,dpi,grabX,grabY,startX,startY,moveAt,moveX,moveY;
        bool moving,bellyDown,gazeEnabled,loading=true,closing;
        readonly bool probeMode;
        long lastProbeRevision=-1;
        string lastProbeVisual;
        public readonly List<string> ProbeTrace=new List<string>();
        internal int ProbeExpectedMessage;
        internal Native.Point? ProbeCursor;
        public int ProbeIgnoredInputs { get; private set; }
        LayoutMode candidate;
        string drawnKey;
        string drawnFace="",mouthPoseDown;
        double drawnScale=-1;
        Bitmap surface;
        public readonly PetController Controller;
        public readonly HashSet<string> PresentedSway=new HashSet<string>();
        public readonly List<double> PaintTimes=new List<double>();
        public int PresentCount { get; private set; }
        public double LogicalScale { get { return scale; } }
        public Point CanvasOrigin { get { return new Point((int)Math.Round(x),(int)Math.Round(y)); } }
        public PetWindow(Assets assets,string dataDirectory,bool probe)
        {
            this.assets=assets;this.dataDirectory=dataDirectory;probeMode=probe;settings=probe?new Settings():Settings.Read(dataDirectory);
            gazeEnabled=settings.Gaze;Controller=new PetController(assets,assets.Manifest.sway.durationsMs);face=new FaceRenderer(assets);faceMotion=new FaceMotion(face.Rig.speech.samples,face.Rig.speech.stepMs);speech=new SpeechPlayer(Path.Combine(assets.Root,face.Rig.speech.file));
            var laughDefinition=new System.Web.Script.Serialization.JavaScriptSerializer().Deserialize<LaughSequence>(File.ReadAllText(Path.Combine(assets.Root,"laugh.json")));
            laughter=new LaughTimeline(laughDefinition);laughSound=new SpeechPlayer(Path.Combine(assets.Root,laughDefinition.audio));
            fartSound=new SpeechPlayer(Path.Combine(assets.Root,"audio/fart.wav"));
            AutoScaleMode=AutoScaleMode.None;FormBorderStyle=FormBorderStyle.None;ShowInTaskbar=false;TopMost=true;StartPosition=FormStartPosition.Manual;Text="奶蛙";
            SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint,true);
            var work=Screen.PrimaryScreen.WorkingArea;dpi=Native.DpiAt(work.Left+work.Width/2,work.Top+work.Height/2);scale=settings.Scale*dpi;
            x=settings.HasPosition?settings.X:work.Left+work.Width*.72-160*scale;
            y=settings.HasPosition?settings.Y:work.Bottom-320*scale;
            LayoutMode restored;if(!Enum.TryParse(settings.Layout,out restored)||!Enum.IsDefined(typeof(LayoutMode),restored)||restored==LayoutMode.Keyboard)restored=LayoutMode.Free;
            Controller.SetLayout(restored);RecoverPosition(false);
            menu.Items.Add("恢复站立",null,delegate { Controller.SetLayout(LayoutMode.Free);RecoverPosition(true); });
            var sizes=new ToolStripMenuItem("显示大小");foreach(double value in new[]{.8,1,1.25,1.5}){double captured=value;sizes.DropDownItems.Add((int)(value*100)+"%",null,delegate { ResizePet(captured); });}menu.Items.Add(sizes);
            var eyeItem=new ToolStripMenuItem("眼睛跟随") { Checked=gazeEnabled,CheckOnClick=true };eyeItem.CheckedChanged+=delegate { gazeEnabled=eyeItem.Checked;settings.Gaze=gazeEnabled;Save(); };menu.Items.Add(eyeItem);
            var laughItem=new ToolStripMenuItem("打开文件时大笑") { Checked=settings.FileLaughter,CheckOnClick=true };
            laughItem.CheckedChanged+=delegate { settings.FileLaughter=laughItem.Checked;if(IsHandleCreated)ConfigureFileMonitor();Save(); };menu.Items.Add(laughItem);
            menu.Items.Add("停止大笑",null,delegate { StopLaughter(); });
            menu.Items.Add(new ToolStripSeparator());menu.Items.Add("退出奶蛙",null,delegate { Close(); });
            tray=new NotifyIcon { Text="奶蛙 · 拖动移动，点击肚子互动",ContextMenuStrip=menu,Visible=!probe };
            string iconFile=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"naifrog.ico");tray.Icon=File.Exists(iconFile)?new Icon(iconFile):SystemIcons.Application;
            tray.DoubleClick+=delegate { Controller.SetLayout(LayoutMode.Free);RecoverPosition(true); };
            timer.Interval=16;timer.Tick+=Tick;MouseDown+=PointerDown;MouseUp+=PointerUp;MouseMove+=PointerMove;
            MouseCaptureChanged+=delegate { if(!Capture&&gesture.Down){gesture.Cancel();dock.Reset();Save();} };
            Deactivate+=delegate { /* A non-activating pet must leave the user's input focus alone. */ };
            SystemEventsSubscribe();
        }
        protected override bool ShowWithoutActivation { get { return true; } }
        protected override CreateParams CreateParams { get { var cp=base.CreateParams;cp.ExStyle|=0x80000|0x80|0x08000000;return cp; } }
        protected override void OnLoad(EventArgs e){base.OnLoad(e);loading=false;lastTick=clock.Elapsed.TotalMilliseconds;Draw(true);timer.Start();if(!probeMode)ConfigureFileMonitor();}
        protected override void OnPaint(PaintEventArgs e){}
        protected override void OnPaintBackground(PaintEventArgs e){}
        protected override void WndProc(ref Message message)
        {
            // A probe routes its own messages without moving the user's pointer.
            // Real hover/capture messages must not turn that synthetic click into a drag.
            if(probeMode&&message.Msg>=0x200&&message.Msg<=0x20E&&message.Msg!=ProbeExpectedMessage){ProbeIgnoredInputs++;message.Result=IntPtr.Zero;return;}
            if(message.Msg==0x21){message.Result=new IntPtr(3);return;}
            if(message.Msg==0x2E0)
            {
                if(!loading)
                {
                    double next=(message.WParam.ToInt64()&65535)/96.0;
                    if(next>0)
                    {
                        dpi=next;scale=settings.Scale*dpi;drawnScale=-1;
                        if(gesture.Down){Native.Point cursor;Native.GetCursorPos(out cursor);x=cursor.X-grabX*scale;y=cursor.Y-grabY*scale;}
                        else if(message.LParam!=IntPtr.Zero){var suggested=(Native.Rect)Marshal.PtrToStructure(message.LParam,typeof(Native.Rect));x=suggested.Left;y=suggested.Top;RecoverPosition(false);}
                        Draw(true);
                    }
                }
                message.Result=IntPtr.Zero;return;
            }
            if((message.Msg==0x7E||message.Msg==0x1A)&&!loading)BeginInvoke((Action)delegate {RecoverPosition(true);});
            base.WndProc(ref message);
        }
        void Tick(object sender,EventArgs args)
        {
            double now=clock.Elapsed.TotalMilliseconds,dt=now-lastTick;lastTick=now;Native.Point cursor;ReadCursor(out cursor);
            if(gesture.Down)Drag(cursor,now);
            bool reactionWasActive=laughter.Active;
            Controller.Advance(dt);
            bool fartWasActive=fart.Active;fart.Advance(dt);
            if(Controller.VisualKey!="bottom")fart.Stop();
            if(fartWasActive&&!fart.Active)fartSound.Stop();
            if(laughter.Phase==LaughPhase.Preparing&&Controller.VisualKey=="idle"&&!moving)laughter.Ready();
            laughter.Advance(dt,laughter.Phase==LaughPhase.Loop&&laughSound.Playing?laughSound.PositionMs:-1);
            if(laughter.AudioEpoch!=laughAudioEpoch){laughAudioEpoch=laughter.AudioEpoch;if(laughter.Phase==LaughPhase.Loop)laughSound.Play();}
            if(reactionWasActive&&!laughter.Active)RestoreAfterLaughter();
            if(probeMode&&(lastProbeRevision!=Controller.Revision||lastProbeVisual!=Controller.VisualKey)){Trace("tick dt="+dt.ToString("F1"));lastProbeRevision=Controller.Revision;lastProbeVisual=Controller.VisualKey;}
            string visual=fart.VisualKey??laughter.VisualKey??Controller.VisualKey;
            face.Advance(visual,(cursor.X-x)/scale,(cursor.Y-y)/scale,gazeEnabled,dt);
            if(!face.Has(visual))speech.Stop();
            faceMotion.Advance(dt,!laughter.Active&&face.Has(visual)&&!gesture.Down&&!moving,speech.PositionMs,speech.Playing);
            bool eyeChanged=face.Stamp(faceMotion.Blink,faceMotion.Mouth,faceMotion.Round)!=drawnFace;
            if(moving&&!gesture.Down)
            {
                double t=Math.Min(1,(now-moveAt)/180),smooth=t*t*(3-2*t);x=startX+(moveX-startX)*smooth;y=startY+(moveY-startY)*smooth;
                if(t>=1){moving=false;restoringLaughter=false;Save();}MoveNative();
            }
            if(pendingLaughter&&!laughter.Active&&!restoringLaughter&&!gesture.Down&&!moving){pendingLaughter=false;StartLaughter();}
            Draw(eyeChanged);
            timer.Interval=fart.Active||laughter.Active||Controller.InTransition||Controller.IsSwaying||gesture.Down||moving||eyeChanged||speech.Playing||faceMotion.Blink>0||faceMotion.Mouth>0?16:32;
        }
        void Draw(bool force)
        {
            string key=fart.VisualKey??laughter.VisualKey??Controller.VisualKey,stamp=face.Stamp(faceMotion.Blink,faceMotion.Mouth,faceMotion.Round);if(!force&&key==drawnKey&&stamp==drawnFace&&Math.Abs(scale-drawnScale)<.001)return;
            var watch=Stopwatch.StartNew();Bitmap frame=face.Render(key,faceMotion.Blink,faceMotion.Mouth,faceMotion.Round);
            int side=Math.Max(80,(int)Math.Round(320*scale));if(surface==null||surface.Width!=side){if(surface!=null)surface.Dispose();surface=new Bitmap(side,side,PixelFormat.Format32bppPArgb);}
            using(var g=System.Drawing.Graphics.FromImage(surface))
            {
                g.CompositingMode=CompositingMode.SourceCopy;g.Clear(Color.Transparent);g.InterpolationMode=InterpolationMode.HighQualityBicubic;g.PixelOffsetMode=PixelOffsetMode.HighQuality;
                g.DrawImage(frame,new Rectangle(0,0,side,side),0,0,320,320,GraphicsUnit.Pixel);
            }
            Native.Present(Handle,surface,(int)Math.Round(x),(int)Math.Round(y));drawnKey=key;drawnFace=stamp;drawnScale=scale;PresentCount++;
            if(PetController.IsSway(key))PresentedSway.Add(key);watch.Stop();if(PaintTimes.Count<10000)PaintTimes.Add(watch.Elapsed.TotalMilliseconds);
        }
        void PointerDown(object sender,MouseEventArgs e)
        {
            Trace("down "+e.Button+" "+e.X+","+e.Y);
            if(laughter.Active){StopLaughter();return;}
            if(e.Button==MouseButtons.Right){menu.Show(Cursor.Position);return;}if(e.Button!=MouseButtons.Left)return;
            Native.Point p=new Native.Point((int)Math.Round(x)+e.X,(int)Math.Round(y)+e.Y);gesture.Begin(p.X,p.Y,4*dpi);grabX=(p.X-x)/scale;grabY=(p.Y-y)/scale;bellyDown=HitRegions.Belly(grabX,grabY);moving=false;restoringLaughter=false;Capture=true;dock.Reset();
            faceMotion.Touch();mouthPoseDown=face.HitMouth(Controller.VisualKey,grabX,grabY)?Controller.VisualKey:null;
            buttDown=FartReaction.Hit(Controller.VisualKey,grabX,grabY);
        }
        void PointerMove(object sender,MouseEventArgs e){if(gesture.Down){Native.Point p;ReadCursor(out p);Drag(p,clock.Elapsed.TotalMilliseconds);}}
        bool ReadCursor(out Native.Point p){if(probeMode&&ProbeCursor.HasValue){p=ProbeCursor.Value;return true;}return Native.GetCursorPos(out p);}
        void Drag(Native.Point p,double now)
        {
            if(gesture.Move(p.X,p.Y)){StopFart();speech.Stop();faceMotion.Touch();Controller.BeginDrag();}if(!gesture.Dragging)return;
            UpdateDpi(p.X,p.Y);x=p.X-grabX*scale;y=p.Y-grabY*scale;MoveNative();
            var work=Screen.FromPoint(new Point(p.X,p.Y)).WorkingArea;
            var body=new Box(x+86*scale,y+30*scale,x+234*scale,y+290*scale);
            candidate=dock.Evaluate(body,new Box(work.Left,work.Top,work.Right,work.Bottom),dpi,now);
            if(candidate!=Controller.Layout)Controller.SetLayout(candidate);
        }
        void PointerUp(object sender,MouseEventArgs e)
        {
            Trace("up "+e.Button+" "+e.X+","+e.Y+" dragging="+gesture.Dragging);
            if(e.Button!=MouseButtons.Left||!gesture.Down)return;
            bool dragged=gesture.Dragging;bool click=gesture.End();Capture=false;
            Native.Point p=new Native.Point((int)Math.Round(x)+e.X,(int)Math.Round(y)+e.Y);
            if(dragged){var work=Screen.FromPoint(new Point(p.X,p.Y)).WorkingArea;candidate=dock.Drop(new Box(x+86*scale,y+30*scale,x+234*scale,y+290*scale),new Box(work.Left,work.Top,work.Right,work.Bottom),dpi);Controller.SetLayout(candidate);RecoverPosition(true);Save();}
            else if(click)
            {
                faceMotion.Touch();
                if(mouthPoseDown==Controller.VisualKey&&face.HitMouth(Controller.VisualKey,(p.X-x)/scale,(p.Y-y)/scale))
                {
                    StopFart();
                    if(!speech.Play())tray.ShowBalloonTip(2500,"奶蛙","暂时无法播放语音，请检查声音输出设备。",ToolTipIcon.Info);
                }
                else if(buttDown&&FartReaction.Hit(Controller.VisualKey,(p.X-x)/scale,(p.Y-y)/scale))
                {
                    speech.Stop();if(fart.Start(Controller.VisualKey))fartSound.Play();
                }
                else{speech.Stop();Controller.Tap(bellyDown&&HitRegions.Belly((p.X-x)/scale,(p.Y-y)/scale));}
            }
        }
        void ConfigureFileMonitor()
        {
            if(fileMonitor!=null){fileMonitor.Dispose();fileMonitor=null;}
            if(!settings.FileLaughter||probeMode){StopLaughter();return;}
            try { fileMonitor=new ExplorerFileMonitor(this,StopLaughter,StartLaughter); }
            catch(System.ComponentModel.Win32Exception){tray.ShowBalloonTip(3000,"奶蛙","系统暂时不允许文件互动监听，其他互动可以继续使用。",ToolTipIcon.Info);}
        }
        void StartLaughter()
        {
            if(closing||gesture.Down)return;
            StopFart();
            // A newly opened item can arrive while the preceding click is returning
            // the frog home. Keep that request instead of dropping it for the session.
            if(laughter.Active||restoringLaughter)
            {
                pendingLaughter=true;
                StopLaughterCore();return;
            }
            laughHomeLayout=Controller.Layout;laughHomePose=PetController.BasePose(Controller.Layout);
            laughHomeX=x;laughHomeY=y;speech.Stop();faceMotion.Touch();laughter.Start();
            Controller.Request("idle");
            var work=Screen.FromPoint(new Point((int)(x+160*scale),(int)(y+160*scale))).WorkingArea;
            startX=x;startY=y;moveX=Clamp(x,work.Left-48*scale,work.Right-272*scale);moveY=Clamp(y,work.Top-15*scale,work.Bottom-290*scale);moveAt=clock.Elapsed.TotalMilliseconds;moving=true;
        }
        void StopLaughter()
        {
            pendingLaughter=false;StopLaughterCore();
        }
        void StopLaughterCore()
        {
            if(!laughter.Active)return;
            laughSound.Stop();laughter.Stop();
            if(!laughter.Active)RestoreAfterLaughter();
        }
        void RestoreAfterLaughter()
        {
            laughSound.Dispose();speech.Dispose();faceMotion.Touch();Controller.SetLayout(laughHomeLayout);Controller.Request(laughHomePose);
            drawnKey=null;drawnFace="";
            startX=x;startY=y;moveX=laughHomeX;moveY=laughHomeY;moveAt=clock.Elapsed.TotalMilliseconds;moving=true;restoringLaughter=true;
        }
        public void StartLaughterForProbe(){StartLaughter();}
        public void StopLaughterForProbe(){StopLaughter();}
        public LaughPhase LaughterPhase {get{return laughter.Phase;}}
        public string LaughterVisual {get{return laughter.VisualKey;}}
        public bool LaughSoundPlaying {get{return laughSound.Playing;}}
        void StopFart(){fart.Stop();fartSound.Stop();}
        public bool FartActive {get{return fart.Active;}}
        public bool FartSoundPlaying {get{return fartSound.Playing;}}
        void MoveNative(){if(IsHandleCreated)Native.SetWindowPos(Handle,IntPtr.Zero,(int)Math.Round(x),(int)Math.Round(y),0,0,0x1|0x4|0x10);}
        void UpdateDpi(int px,int py)
        {
            double next=Native.DpiAt(px,py);if(Math.Abs(next-dpi)<.001)return;
            if(gesture.Down){dpi=next;scale=settings.Scale*dpi;}
            else { double cx=x+160*scale,cy=y+160*scale;dpi=next;scale=settings.Scale*dpi;x=cx-160*scale;y=cy-160*scale; }
            drawnScale=-1;
        }
        void ResizePet(double value)
        {
            double cx=x+160*scale,cy=y+160*scale;settings.Scale=value;scale=value*dpi;x=cx-160*scale;y=cy-160*scale;RecoverPosition(true);Draw(true);Save();
        }
        public void RecoverPosition(bool animate)
        {
            var work=Screen.FromPoint(new Point((int)(x+160*scale),(int)(y+160*scale))).WorkingArea;
            double targetX=x,targetY=y;
            switch(Controller.Layout)
            {
                case LayoutMode.Left:targetX=work.Left-114*scale;targetY=Clamp(y,work.Top-30*scale,work.Bottom-224*scale);break;
                case LayoutMode.Right:targetX=work.Right-206*scale;targetY=Clamp(y,work.Top-30*scale,work.Bottom-224*scale);break;
                case LayoutMode.Bottom:targetX=Clamp(x,work.Left-31*scale,work.Right-289*scale);targetY=work.Bottom-290*scale;break;
                default:targetX=Clamp(x,work.Left-86*scale,work.Right-234*scale);targetY=Clamp(y,work.Top-30*scale,work.Bottom-290*scale);break;
            }
            if(animate){startX=x;startY=y;moveX=targetX;moveY=targetY;moveAt=clock.Elapsed.TotalMilliseconds;moving=true;}
            else{x=targetX;y=targetY;MoveNative();}
        }
        static double Clamp(double value,double min,double max){return Math.Max(min,Math.Min(max,value));}
        public void PositionForProbe(int px,int py){x=px;y=py;dpi=Native.DpiAt(px,py);scale=dpi;Draw(true);}
        public void SaveSurface(string file){surface.Save(file);}
        public void GazeForProbe(bool enabled){gazeEnabled=enabled;}
        public void ResetFaceForProbe(){faceMotion.Touch();}
        public double FaceBlink {get{return faceMotion.Blink;}}
        public double FaceMouth {get{return faceMotion.Mouth;}}
        public double FaceGazeX {get{return face.X;}}
        public bool SpeechPlaying {get{return speech.Playing;}}
        public double SpeechPosition {get{return speech.PositionMs;}}
        void Trace(string action){if(probeMode)ProbeTrace.Add(clock.Elapsed.TotalMilliseconds.ToString("F1")+" "+action+" state="+Controller.Status+" desired="+Controller.Desired+" rev="+Controller.Revision+" originalMs="+Controller.OriginalTimePlayed.ToString("F1"));}
        void Save()
        {
            settings.X=laughter.Active||restoringLaughter?laughHomeX:x;settings.Y=laughter.Active||restoringLaughter?laughHomeY:y;settings.Layout=(laughter.Active||restoringLaughter?laughHomeLayout:Controller.Layout).ToString();settings.HasPosition=true;settings.Gaze=gazeEnabled;
            try{settings.Save(dataDirectory);}catch(IOException){}catch(UnauthorizedAccessException){}
        }
        void SystemEventsSubscribe(){Microsoft.Win32.SystemEvents.DisplaySettingsChanged+=DisplayChanged;}
        void DisplayChanged(object sender,EventArgs e){if(!closing&&IsHandleCreated)BeginInvoke((Action)delegate{RecoverPosition(true);});}
        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            closing=true;if(fileMonitor!=null)fileMonitor.Dispose();fartSound.Dispose();laughSound.Dispose();timer.Stop();timer.Dispose();gesture.Cancel();Save();Microsoft.Win32.SystemEvents.DisplaySettingsChanged-=DisplayChanged;
            tray.Visible=false;tray.Dispose();menu.Dispose();speech.Dispose();face.Dispose();if(surface!=null)surface.Dispose();base.OnFormClosed(e);
        }
    }
}
