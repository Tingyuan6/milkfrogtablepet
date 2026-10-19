using System;
using System.IO;
using System.Runtime.InteropServices;

namespace Naifrog.Windows
{
    internal sealed class SpeechPlayer : IDisposable
    {
        [StructLayout(LayoutKind.Sequential,Pack=2)] struct Format {public ushort tag,channels;public uint rate,bytesPerSecond;public ushort alignment,bits,extra;}
        [StructLayout(LayoutKind.Sequential)] struct Header {public IntPtr data;public uint length,recorded;public IntPtr user;public uint flags,loops;public IntPtr next,reserved;}
        [StructLayout(LayoutKind.Sequential)] struct Time {public uint type,value,rest;}
        [DllImport("winmm.dll")] static extern int waveOutOpen(out IntPtr h,uint device,ref Format format,IntPtr callback,IntPtr instance,uint flags);
        [DllImport("winmm.dll")] static extern int waveOutPrepareHeader(IntPtr h,IntPtr header,uint size);
        [DllImport("winmm.dll")] static extern int waveOutWrite(IntPtr h,IntPtr header,uint size);
        [DllImport("winmm.dll")] static extern int waveOutGetPosition(IntPtr h,ref Time time,uint size);
        [DllImport("winmm.dll")] static extern int waveOutReset(IntPtr h);
        [DllImport("winmm.dll")] static extern int waveOutUnprepareHeader(IntPtr h,IntPtr header,uint size);
        [DllImport("winmm.dll")] static extern int waveOutClose(IntPtr h);
        readonly string file;IntPtr handle,header,data;Format format;bool queued;
        uint HeaderSize {get{return (uint)Marshal.SizeOf(typeof(Header));}}
        public SpeechPlayer(string file){this.file=file;}
        public bool Playing {get{return queued&&header!=IntPtr.Zero&&(Marshal.PtrToStructure<Header>(header).flags&1)==0;}}
        public double PositionMs
        {
            get{if(!queued||handle==IntPtr.Zero)return 0;var t=new Time{type=2};if(waveOutGetPosition(handle,ref t,12)!=0)return 0;return t.type==2?t.value*1000.0/format.rate:t.type==4?t.value*1000.0/format.bytesPerSecond:t.type==1?t.value:0;}
        }
        void Open()
        {
            byte[] pcm=null;
            using(var reader=new BinaryReader(File.OpenRead(file)))
            {
                if(new string(reader.ReadChars(4))!="RIFF")throw new InvalidDataException("WAV required");reader.ReadUInt32();if(new string(reader.ReadChars(4))!="WAVE")throw new InvalidDataException("WAV required");
                while(reader.BaseStream.Position+8<=reader.BaseStream.Length)
                {
                    string id=new string(reader.ReadChars(4));uint length=reader.ReadUInt32();long end=reader.BaseStream.Position+length;
                    if(id=="fmt "){format.tag=reader.ReadUInt16();format.channels=reader.ReadUInt16();format.rate=reader.ReadUInt32();format.bytesPerSecond=reader.ReadUInt32();format.alignment=reader.ReadUInt16();format.bits=reader.ReadUInt16();}
                    if(id=="data")pcm=reader.ReadBytes(checked((int)length));reader.BaseStream.Position=end+(length&1);
                }
            }
            if(format.tag!=1||pcm==null)throw new InvalidDataException("PCM WAV required");
            if(waveOutOpen(out handle,unchecked((uint)-1),ref format,IntPtr.Zero,IntPtr.Zero,0)!=0)throw new IOException("Audio device unavailable");
            data=Marshal.AllocHGlobal(pcm.Length);Marshal.Copy(pcm,0,data,pcm.Length);header=Marshal.AllocHGlobal((int)HeaderSize);
            Marshal.StructureToPtr(new Header{data=data,length=(uint)pcm.Length},header,false);
            if(waveOutPrepareHeader(handle,header,HeaderSize)!=0)throw new IOException("Audio buffer unavailable");
        }
        public bool Play()
        {
            try{if(handle==IntPtr.Zero)Open();Stop();if(waveOutWrite(handle,header,HeaderSize)!=0)throw new IOException("Audio playback failed");queued=true;return true;}
            catch(Exception e){if(!(e is IOException||e is InvalidDataException||e is UnauthorizedAccessException))throw;Dispose();return false;}
        }
        public void Stop(){if(handle!=IntPtr.Zero&&queued)waveOutReset(handle);queued=false;}
        public void Dispose(){Stop();if(handle!=IntPtr.Zero){if(header!=IntPtr.Zero)waveOutUnprepareHeader(handle,header,HeaderSize);waveOutClose(handle);handle=IntPtr.Zero;}if(header!=IntPtr.Zero){Marshal.FreeHGlobal(header);header=IntPtr.Zero;}if(data!=IntPtr.Zero){Marshal.FreeHGlobal(data);data=IntPtr.Zero;}}
    }
}
