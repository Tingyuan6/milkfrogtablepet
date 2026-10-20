using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Automation;
using System.Windows.Forms;
using Naifrog.Core;

namespace Naifrog.Windows
{
    // Observe activation intent in Explorer/desktop only. Never open files, swallow
    // input, read their contents, or retain a history of paths/keys.
    internal sealed class ExplorerFileMonitor : IDisposable
    {
        delegate IntPtr HookProc(int code,IntPtr message,IntPtr data);
        [StructLayout(LayoutKind.Sequential)] struct MouseData {public Native.Point point;public uint mouseData,flags,time;public IntPtr extra;}
        sealed class Input {public int kind,x,y;public uint time;public long serial;public IntPtr root;}
        [DllImport("user32.dll",SetLastError=true)] static extern IntPtr SetWindowsHookEx(int id,HookProc callback,IntPtr module,uint thread);
        [DllImport("user32.dll")] static extern bool UnhookWindowsHookEx(IntPtr hook);
        [DllImport("user32.dll")] static extern IntPtr CallNextHookEx(IntPtr hook,int code,IntPtr message,IntPtr data);
        [DllImport("kernel32.dll",CharSet=CharSet.Unicode)] static extern IntPtr GetModuleHandle(string name);
        [DllImport("user32.dll")] static extern IntPtr GetAncestor(IntPtr window,uint flags);
        [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern int GetClassName(IntPtr window,StringBuilder name,int count);
        [DllImport("user32.dll")] static extern uint GetDoubleClickTime();
        [DllImport("user32.dll")] static extern int GetSystemMetrics(int index);
        [DllImport("user32.dll")] static extern short GetAsyncKeyState(int key);
        readonly Control owner;
        readonly Action clicked,opened;
        readonly BlockingCollection<Input> queue=new BlockingCollection<Input>(32);
        readonly HookProc mouseProc,keyProc;
        readonly Thread worker,hookThread;
        readonly ManualResetEvent hookReady=new ManualResetEvent(false);
        Control hookPump;
        Exception hookError;
        IntPtr mouseHook,keyHook;
        long serial;
        volatile bool disposed;
        bool enterHeld;
        FileActivationGesture gesture;
        readonly int doubleTime=(int)GetDoubleClickTime(),doubleX=GetSystemMetrics(36),doubleY=GetSystemMetrics(37);
        public bool Available {get{return mouseHook!=IntPtr.Zero&&keyHook!=IntPtr.Zero;}}
        public volatile bool ShellMetadataAvailable,ShellProbeFinished;
        public string Diagnostic;
        public ExplorerFileMonitor(Control owner,Action clicked,Action opened)
        {
            this.owner=owner;this.clicked=clicked;this.opened=opened;
            gesture=new FileActivationGesture((uint)doubleTime,doubleX,doubleY);
            mouseProc=Mouse;keyProc=Key;
            // Low-level hooks need their own prompt message pump. PNG decoding and
            // layered-window rendering must not delay them until Windows removes them.
            hookThread=new Thread(HookLoop){IsBackground=true,Name="Naifrog desktop input"};hookThread.SetApartmentState(ApartmentState.STA);hookThread.Start();
            hookReady.WaitOne();hookReady.Dispose();
            if(hookError!=null)throw new System.ComponentModel.Win32Exception("Desktop input listener unavailable",hookError);
            worker=new Thread(delegate(){try{Work();}catch(Exception error){Diagnostic=error.ToString();ShellProbeFinished=true;}}){IsBackground=true,Name="Naifrog Explorer activations"};worker.SetApartmentState(ApartmentState.MTA);worker.Start();
        }
        void HookLoop()
        {
            bool signaled=false;
            try
            {
                using(var pump=new Control())
                {
                    IntPtr handle=pump.Handle;hookPump=pump;IntPtr module=GetModuleHandle(null);
                    mouseHook=SetWindowsHookEx(14,mouseProc,module,0);keyHook=SetWindowsHookEx(13,keyProc,module,0);
                    if(!Available)throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
                    signaled=true;hookReady.Set();Application.Run();
                }
            }
            catch(Exception error){hookError=error;if(!signaled)hookReady.Set();}
            finally
            {
                if(mouseHook!=IntPtr.Zero)UnhookWindowsHookEx(mouseHook);if(keyHook!=IntPtr.Zero)UnhookWindowsHookEx(keyHook);
                mouseHook=keyHook=IntPtr.Zero;hookPump=null;
            }
        }
        void Post(Action action){if(disposed||owner.IsDisposed||!owner.IsHandleCreated)return;try{owner.BeginInvoke(action);}catch(InvalidOperationException){} }
        void Enqueue(Input input){if(!disposed&&!queue.IsAddingCompleted)try{queue.TryAdd(input);}catch(InvalidOperationException){} }
        IntPtr Mouse(int code,IntPtr message,IntPtr data)
        {
            if(code>=0)
            {
                int m=message.ToInt32();
                if(m==0x201||m==0x204||m==0x207||m==0x20B)
                {
                    var p=(MouseData)Marshal.PtrToStructure(data,typeof(MouseData));
                    if((p.flags&1)==0)
                    {
                        long epoch=Interlocked.Increment(ref serial);Post(clicked);
                        if(m==0x201)Enqueue(new Input{kind=1,x=p.point.X,y=p.point.Y,time=p.time,serial=epoch,root=GetAncestor(Native.WindowFromPoint(p.point),2)});
                        else Enqueue(new Input{kind=0,serial=epoch});
                    }
                }
            }
            return CallNextHookEx(mouseHook,code,message,data);
        }
        IntPtr Key(int code,IntPtr message,IntPtr data)
        {
            if(code>=0&&Marshal.ReadInt32(data)==13)
            {
                int m=message.ToInt32();
                if(m==0x101||m==0x105)enterHeld=false;
                else if((m==0x100||m==0x104)&&!enterHeld)
                {
                    enterHeld=true;
                    if((GetAsyncKeyState(17)&0x8000)==0&&(GetAsyncKeyState(18)&0x8000)==0)
                        Enqueue(new Input{kind=2,serial=Interlocked.Read(ref serial),root=GetAncestor(Native.GetForegroundWindow(),2)});
                }
            }
            return CallNextHookEx(keyHook,code,message,data);
        }
        static string ClassName(IntPtr root){var text=new StringBuilder(128);GetClassName(root,text,text.Capacity);return text.ToString();}
        static bool IsShell(IntPtr root)
        {
            string cls=ClassName(root);if(cls!="CabinetWClass"&&cls!="ExploreWClass"&&cls!="Progman"&&cls!="WorkerW")return false;
            uint pid;Native.GetWindowThreadProcessId(root,out pid);try{using(var p=Process.GetProcessById((int)pid))return p.ProcessName.Equals("explorer",StringComparison.OrdinalIgnoreCase);}catch(ArgumentException){return false;}
        }
        static AutomationElement Item(AutomationElement element)
        {
            for(int n=0;element!=null&&n<10;n++,element=TreeWalker.ControlViewWalker.GetParent(element))
            {
                ControlType type=element.Current.ControlType;
                if(type==ControlType.Edit)return null;
                if(type==ControlType.ListItem||type==ControlType.DataItem)return element;
            }
            return null;
        }
        static object Get(object target,string name,params object[] args){return target.GetType().InvokeMember(name,BindingFlags.GetProperty|BindingFlags.InvokeMethod,null,target,args);}
        static void Release(object value){if(value!=null&&Marshal.IsComObject(value))Marshal.ReleaseComObject(value);}
        static bool SameName(string shellName,string accessibleName,string path)
        {
            return shellName==accessibleName||Path.GetFileName(path)==accessibleName||Path.GetFileNameWithoutExtension(path)==accessibleName;
        }
        static string SelectedItem(IntPtr root,AutomationElement element)
        {
            if(!IsShell(root))return null;
            element=Item(element);if(element==null)return null;
            string name=element.Current.Name;if(String.IsNullOrEmpty(name))return null;
            // A real desktop icon is enough, including folders, shortcuts and
            // virtual icons (This PC / Recycle Bin). It need not be a disk file.
            string cls=ClassName(root);
            if(cls=="Progman"||cls=="WorkerW")return "desktop:"+name;
            object shell=null,windows=null;
            try
            {
                shell=Activator.CreateInstance(Type.GetTypeFromProgID("Shell.Application"));windows=Get(shell,"Windows");
                int count=Convert.ToInt32(Get(windows,"Count"));
                for(int i=0;i<count;i++)
                {
                    object window=null,document=null,items=null;
                    try
                    {
                        window=Get(windows,"Item",i);if(new IntPtr(Convert.ToInt64(Get(window,"HWND")))!=root)continue;
                        document=Get(window,"Document");items=Get(document,"SelectedItems");
                        int selected=Convert.ToInt32(Get(items,"Count"));
                        for(int j=0;j<Math.Min(selected,256);j++)
                        {
                            object item=null;
                            try
                            {
                                item=Get(items,"Item",j);
                                string path=Convert.ToString(Get(item,"Path"));string shellName=Convert.ToString(Get(item,"Name"));
                                if(SameName(shellName,name,path)&&!String.IsNullOrEmpty(path))return path;
                            }
                            finally{Release(item);}
                        }
                        // The first mouse-down may precede Explorer updating selection.
                        // Resolve the actual item under the pointer in that folder too.
                        object folder=null,itemByName=null;
                        try
                        {
                            folder=Get(document,"Folder");itemByName=Get(folder,"ParseName",name);
                            if(itemByName!=null)
                            {
                                string path=Convert.ToString(Get(itemByName,"Path"));if(!String.IsNullOrEmpty(path))return path;
                            }
                        }
                        finally{Release(itemByName);Release(folder);}
                    }
                    finally{Release(items);Release(document);Release(window);}
                }
            }
            finally{Release(windows);Release(shell);}
            return null;
        }
        void Activate(string file,long epoch)
        {
            if(file==null||epoch!=Interlocked.Read(ref serial))return;
            Post(delegate{if(epoch==Interlocked.Read(ref serial))opened();});
        }
        void Work()
        {
            object shellProbe=null,windowsProbe=null;
            try
            {
                shellProbe=Activator.CreateInstance(Type.GetTypeFromProgID("Shell.Application"));windowsProbe=Get(shellProbe,"Windows");
                ShellMetadataAvailable=Convert.ToInt32(Get(windowsProbe,"Count"))>=0;
            }
            catch(Exception error){Diagnostic=error.ToString();}
            finally{Release(windowsProbe);Release(shellProbe);ShellProbeFinished=true;}
            foreach(Input input in queue.GetConsumingEnumerable())
            {
                if(disposed)break;
                try
                {
                    if(input.kind==0){gesture.Reset();continue;}
                    if(input.kind==1)
                    {
                        string activated=gesture.Complete(input.time,input.x,input.y,input.root.ToInt64());
                        if(activated!=null)Activate(activated,input.serial);
                        else
                        {
                            string candidate=null;
                            if(IsShell(input.root))candidate=SelectedItem(input.root,AutomationElement.FromPoint(new System.Windows.Point(input.x,input.y)));
                            gesture.Remember(input.time,input.x,input.y,input.root.ToInt64(),candidate);
                        }
                    }
                    else if(input.kind==2&&IsShell(input.root))Activate(SelectedItem(input.root,AutomationElement.FocusedElement),input.serial);
                }
                catch(Exception error){if(!(error is SystemException||error is TargetInvocationException))throw;}
            }
        }
        public void Dispose()
        {
            if(disposed)return;disposed=true;
            Control pump=hookPump;
            if(pump!=null)try{pump.BeginInvoke((Action)Application.ExitThread);}catch(InvalidOperationException){}
            hookThread.Join(1000);
            queue.CompleteAdding();if(worker!=null)worker.Join(200);
        }
    }
}
