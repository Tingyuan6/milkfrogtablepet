using System;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using Naifrog.Graphics;

namespace Naifrog.Windows
{
    internal static class Program
    {
        [STAThread] static int Main(string[] args)
        {
            try { Native.SetProcessDpiAwarenessContext(new IntPtr(-4)); } catch(EntryPointNotFoundException){Native.SetProcessDPIAware();}
            Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
            string home=AppDomain.CurrentDomain.BaseDirectory,data=Path.Combine(home,"data");
            for(int i=0;i<args.Length-1;i++)if(args[i]=="--data-dir")data=Path.GetFullPath(args[++i]);
            try
            {
                if(args.Length>0&&args[0]=="--monitor-self-test")return NativeProbe.Monitor(data);
                if(args.Length>0&&args[0]=="--probe-background")return NativeProbe.Background(args);
                using(var assets=new Assets(Path.Combine(home,"assets")))
                {
                    if(args.Length>0&&args[0]=="--smoke-test")return NativeProbe.Run(assets,data);
                    bool first;
                    using(var mutex=new Mutex(true,"Local\\Naifrog."+home.GetHashCode(),out first))
                    {
                        if(!first)return 0;
                        using(var window=new PetWindow(assets,data,false))Application.Run(window);
                    }
                }
                return 0;
            }
            catch(Exception error)
            {
                try{Directory.CreateDirectory(data);File.WriteAllText(Path.Combine(data,"error.log"),error.ToString());}catch{}
                if(args.Length==0||!args[0].StartsWith("--"))MessageBox.Show("奶蛙暂时无法启动。请将程序与 assets 文件夹放在一起。\n\n"+error.Message,"奶蛙",MessageBoxButtons.OK,MessageBoxIcon.Error);return 1;
            }
        }
    }
}
