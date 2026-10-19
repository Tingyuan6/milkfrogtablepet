using System;
using System.ComponentModel;
using System.Drawing;
using System.Runtime.InteropServices;

namespace Naifrog.Windows
{
    internal static class Native
    {
        [StructLayout(LayoutKind.Sequential)] public struct Point { public int X,Y; public Point(int x,int y){X=x;Y=y;} }
        [StructLayout(LayoutKind.Sequential)] public struct Size { public int W,H; public Size(int w,int h){W=w;H=h;} }
        [StructLayout(LayoutKind.Sequential,Pack=1)] public struct Blend { public byte Op,Flags,Alpha,Format; }
        [StructLayout(LayoutKind.Sequential)] public struct Rect { public int Left,Top,Right,Bottom; }
        [StructLayout(LayoutKind.Sequential)] public struct MonitorInfo { public int Size;public Rect Monitor,Work;public uint Flags; }
        [DllImport("user32.dll")] public static extern bool GetCursorPos(out Point p);
        [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] public static extern IntPtr WindowFromPoint(Point p);
        [DllImport("user32.dll")] public static extern IntPtr MonitorFromPoint(Point p,uint flags);
        [DllImport("user32.dll",CharSet=CharSet.Auto)] public static extern bool GetMonitorInfo(IntPtr monitor,ref MonitorInfo info);
        [DllImport("shcore.dll")] static extern int GetDpiForMonitor(IntPtr monitor,int type,out uint x,out uint y);
        [DllImport("user32.dll")] public static extern bool SetProcessDpiAwarenessContext(IntPtr context);
        [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
        [DllImport("user32.dll")] public static extern IntPtr GetDC(IntPtr window);
        [DllImport("user32.dll")] public static extern int ReleaseDC(IntPtr window,IntPtr dc);
        [DllImport("gdi32.dll")] static extern IntPtr CreateCompatibleDC(IntPtr dc);
        [DllImport("gdi32.dll")] static extern bool DeleteDC(IntPtr dc);
        [DllImport("gdi32.dll")] static extern IntPtr SelectObject(IntPtr dc,IntPtr bitmap);
        [DllImport("gdi32.dll")] static extern bool DeleteObject(IntPtr bitmap);
        [DllImport("user32.dll",SetLastError=true)] static extern bool UpdateLayeredWindow(IntPtr window,IntPtr dst,ref Point point,ref Size size,IntPtr src,ref Point origin,int color,ref Blend blend,int flags);
        [DllImport("user32.dll",SetLastError=true)] public static extern bool SetWindowPos(IntPtr window,IntPtr after,int x,int y,int cx,int cy,uint flags);
        [DllImport("user32.dll")] public static extern uint GetGuiResources(IntPtr process,uint flags);
        [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr handle,out uint process);
        [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr window,out Rect rect);
        [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr window);
        [DllImport("user32.dll",EntryPoint="GetWindowLongPtrW")] public static extern IntPtr GetWindowLongPtr(IntPtr window,int index);
        public static float DpiAt(int x,int y)
        {
            try { uint dx,dy;if(GetDpiForMonitor(MonitorFromPoint(new Point(x,y),2),0,out dx,out dy)==0)return dx/96f; } catch(DllNotFoundException){}catch(EntryPointNotFoundException){}
            return 1;
        }
        public static void Present(IntPtr handle,Bitmap frame,int x,int y)
        {
            IntPtr screen=GetDC(IntPtr.Zero),memory=IntPtr.Zero,bitmap=IntPtr.Zero,old=IntPtr.Zero;
            try
            {
                memory=CreateCompatibleDC(screen);bitmap=frame.GetHbitmap(Color.FromArgb(0));old=SelectObject(memory,bitmap);
                Point location=new Point(x,y),origin=new Point(0,0);Size size=new Size(frame.Width,frame.Height);Blend blend=new Blend { Alpha=255,Format=1 };
                if(!UpdateLayeredWindow(handle,screen,ref location,ref size,memory,ref origin,0,ref blend,2))throw new Win32Exception(Marshal.GetLastWin32Error());
            }
            finally { if(old!=IntPtr.Zero)SelectObject(memory,old);if(bitmap!=IntPtr.Zero)DeleteObject(bitmap);if(memory!=IntPtr.Zero)DeleteDC(memory);ReleaseDC(IntPtr.Zero,screen); }
        }
    }
}
