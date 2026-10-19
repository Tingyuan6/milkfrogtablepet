using System;
using System.IO;
using System.Web.Script.Serialization;

namespace Naifrog.Windows
{
    public sealed class Settings
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double Scale { get; set; }
        public string Layout { get; set; }
        public bool Gaze { get; set; }
        public bool HasPosition { get; set; }
        public Settings(){Scale=1;Layout="Free";Gaze=true;}
        public static Settings Read(string directory)
        {
            try { var s=new JavaScriptSerializer().Deserialize<Settings>(File.ReadAllText(Path.Combine(directory,"settings.json")));if(s==null||s.Scale<.5||s.Scale>2||Double.IsNaN(s.X)||Double.IsNaN(s.Y)||Double.IsInfinity(s.X)||Double.IsInfinity(s.Y))return new Settings();return s; }
            catch(IOException){return new Settings();}catch(ArgumentException){return new Settings();}catch(InvalidOperationException){return new Settings();}
        }
        public void Save(string directory)
        {
            Directory.CreateDirectory(directory);string file=Path.Combine(directory,"settings.json"),temporary=file+".tmp";
            File.WriteAllText(temporary,new JavaScriptSerializer().Serialize(this));File.Copy(temporary,file,true);File.Delete(temporary);
        }
    }
}
