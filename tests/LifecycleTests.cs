using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using Naifrog.Core;
using Naifrog.Graphics;
using Naifrog.Windows;

internal static class LifecycleTests
{
    static readonly List<object> observations=new List<object>();
    static int checks;
    static void Check(bool ok,string name){checks++;if(!ok)throw new Exception(name);}
    static void Pump(int ms){var clock=Stopwatch.StartNew();while(clock.ElapsedMilliseconds<ms){Application.DoEvents();Thread.Sleep(5);}}
    static void MouthClick(PetWindow pet,FaceRig rig)
    {
        double[] m=rig.faces[pet.Controller.VisualKey].mouth;
        int px=(int)Math.Round(m[0]*pet.LogicalScale),py=(int)Math.Round(m[1]*pet.LogicalScale);
        foreach(string method in new[]{"PointerDown","PointerUp"})
            typeof(PetWindow).GetMethod(method,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(pet,new object[]{pet,new MouseEventArgs(MouseButtons.Left,1,px,py,0)});
    }
    static void Speech(PetWindow pet,FaceRig rig,string scenario)
    {
        MouthClick(pet,rig);double maximum=0,first=pet.SpeechPosition;int levels=0;
        for(int i=0;i<20;i++){Pump(50);maximum=Math.Max(maximum,pet.FaceMouth);if(pet.FaceMouth>.05)levels++;}
        observations.Add(new {scenario=scenario,voice=pet.SpeechPlaying,firstPosition=first,position=pet.SpeechPosition,maxMouth=maximum,levels=levels,phase=pet.LaughterPhase.ToString(),pose=pet.Controller.VisualKey});
        Check(pet.SpeechPlaying&&pet.SpeechPosition>first+300,scenario+" voice advances");
        Check(maximum>.35&&levels>3,scenario+" lips still animate");Pump(1200);
    }
    static void RapidReopen(PetWindow pet)
    {
        pet.StartLaughterForProbe();Pump(1200);pet.StopLaughterForProbe();
        pet.StartLaughterForProbe();Pump(2200);
        Check(pet.LaughterPhase==LaughPhase.Loop&&pet.LaughSoundPlaying,"opening again during recovery is not dropped");
        pet.StopLaughterForProbe();Pump(2300);
        pet.StartLaughterForProbe();Pump(1200);pet.StopLaughterForProbe();
        pet.StartLaughterForProbe();pet.StopLaughterForProbe();Pump(2300);
        Check(pet.LaughterPhase==LaughPhase.Off&&!pet.LaughSoundPlaying,"later click cancels pending restart");
    }
    [STAThread] static int Main(string[] args)
    {
        string data=Path.GetFullPath(args[1]);Directory.CreateDirectory(data);
        try
        {
            try{Native.SetProcessDpiAwarenessContext(new IntPtr(-4));}catch(EntryPointNotFoundException){}
            Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
            using(var assets=new Assets(args[0]))using(var renderer=new FaceRenderer(assets))using(var pet=new PetWindow(assets,data,true))
            {
                pet.Show();pet.GazeForProbe(false);Pump(300);
                RapidReopen(pet);
                if(args.Length<3||args[2]!="--rapid")
                {
                    Speech(pet,renderer.Rig,"after rapid reopen");
                    foreach(LayoutMode layout in new[]{LayoutMode.Free,LayoutMode.Left,LayoutMode.Right,LayoutMode.Bottom})
                    {
                        pet.Controller.SetLayout(layout);Pump(700);
                        foreach(int wait in new[]{0,100,1100,9200})
                        {
                            pet.StartLaughterForProbe();Pump(wait);
                            Check(pet.LaughterPhase!=LaughPhase.Off,"repeat starts "+layout+" "+wait);
                            pet.StopLaughterForProbe();Pump(2300);
                            Check(pet.LaughterPhase==LaughPhase.Off,"stop settles "+layout+" "+wait);
                            Check(pet.Controller.VisualKey==PetController.BasePose(layout),"restored pose");
                            Speech(pet,renderer.Rig,layout+" stop at "+wait);
                            File.WriteAllText(Path.Combine(data,"progress.json"),new JavaScriptSerializer().Serialize(observations));
                        }
                    }
                    pet.Controller.SetLayout(LayoutMode.Free);Pump(700);pet.StartLaughterForProbe();Pump(17100);
                    Check(pet.LaughterPhase==LaughPhase.Loop&&pet.LaughSoundPlaying,"full cycle repeats");
                    pet.StopLaughterForProbe();Pump(2300);Speech(pet,renderer.Rig,"after full video cycle");
                    using(var monitor=new ExplorerFileMonitor(pet,delegate{},delegate{}))
                    {
                        var pump=(Control)typeof(ExplorerFileMonitor).GetField("hookPump",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(monitor);
                        using(var signal=new ManualResetEvent(false))
                        {
                            pump.BeginInvoke((Action)delegate{signal.Set();});Thread.Sleep(1200);
                            Check(signal.WaitOne(0),"input hooks pump independently while render thread is busy");
                        }
                        for(int i=0;i<50&&!monitor.ShellProbeFinished;i++)Pump(50);
                        Check(monitor.Available&&monitor.ShellMetadataAvailable,"desktop listener and Shell available");
                        monitor.Dispose();Check(!monitor.Available,"listener removed on disposal");
                    }
                }
                pet.Close();
            }
            File.WriteAllText(Path.Combine(data,"result.json"),new JavaScriptSerializer().Serialize(new {passed=true,checks=checks,observations=observations}));return 0;
        }
        catch(Exception error){File.WriteAllText(Path.Combine(data,"result.json"),new JavaScriptSerializer().Serialize(new {passed=false,checks=checks,error=error.ToString(),observations=observations}));return 1;}
    }
}
