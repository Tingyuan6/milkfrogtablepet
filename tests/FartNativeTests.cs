using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using Naifrog.Core;
using Naifrog.Graphics;
using Naifrog.Windows;
internal static class FartNativeTests
{
    static int checks;
    static void Check(bool ok,string name){checks++;if(!ok)throw new Exception(name);}
    static void Pump(int ms){var w=Stopwatch.StartNew();while(w.ElapsedMilliseconds<ms){Application.DoEvents();Thread.Sleep(5);}}
    static void Click(PetWindow p,int x,int y){foreach(string name in new[]{"PointerDown","PointerUp"})typeof(PetWindow).GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(p,new object[]{p,new MouseEventArgs(MouseButtons.Left,1,(int)(x*p.LogicalScale),(int)(y*p.LogicalScale),0)});}
    [STAThread]static int Main(string[] args)
    {
        Directory.CreateDirectory(args[1]);
        try
        {
            Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
            using(var a=new Assets(args[0]))using(var p=new PetWindow(a,args[1],true))
            {
                p.Show();Pump(150);Click(p,78,248);Check(!p.FartActive,"standing does not fart");
                p.Controller.SetLayout(LayoutMode.Bottom);Pump(700);var home=p.CanvasOrigin;
                Click(p,78,248);Pump(300);Check(p.FartActive&&p.FartSoundPlaying,"butt starts effect and real audio");
                p.SaveSurface(Path.Combine(args[1],"fart-native.png"));Pump(800);Click(p,78,248);Pump(2300);
                Check(p.FartActive,"repeat click restarts three seconds");Pump(900);
                Check(!p.FartActive&&!p.FartSoundPlaying&&p.Controller.VisualKey=="bottom","natural completion restores bottom");
                Check(p.CanvasOrigin==home,"reaction preserves docking position");
                Click(p,78,248);Pump(200);p.Controller.BeginDrag();Pump(80);Check(!p.FartActive&&!p.FartSoundPlaying,"drag cancels both");
                p.Controller.SetLayout(LayoutMode.Bottom);Pump(700);Click(p,78,248);Pump(200);Click(p,273,204);Pump(500);
                Check(!p.FartActive&&p.SpeechPlaying&&p.FaceMouth>.05,"mouth interrupts fart and lips animate");
                p.Close();
            }
            File.WriteAllText(Path.Combine(args[1],"result.json"),new JavaScriptSerializer().Serialize(new {passed=true,checks=checks,actualAudioDevice=true,physicalInputInjected=false}));return 0;
        }
        catch(Exception e){File.WriteAllText(Path.Combine(args[1],"result.json"),new JavaScriptSerializer().Serialize(new {passed=false,checks=checks,error=e.ToString()}));return 1;}
    }
}
