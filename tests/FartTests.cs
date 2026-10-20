using System;
using System.IO;
using Naifrog.Core;
internal static class FartTests
{
    static int checks;
    static void Check(bool ok,string message){checks++;if(!ok)throw new Exception(message);}
    static int Main(string[] args)
    {
        try
        {
            var f=new FartReaction();Check(!f.Active&&f.VisualKey==null,"initially inactive");
            foreach(string pose in new[]{"idle","sit_1","sit_2","peek_left","peek_right","sway_00"})
            {Check(!f.Start(pose),"only bottom can start");Check(!FartReaction.Hit(pose,78,248),"only bottom hit");}
            Check(FartReaction.Hit("bottom",78,248),"butt hit");Check(!FartReaction.Hit("bottom",273,204),"mouth is separate");Check(!FartReaction.Hit("bottom",160,250),"belly is separate");
            for(int repeat=0;repeat<5;repeat++)
            {
                Check(f.Start("bottom"),"can restart");
                for(int i=0;i<3000;i+=10){Check(f.Active,"all three seconds active");Check(File.Exists(Path.Combine(args[0],f.VisualKey)),"frame exists");f.Advance(10);}
                Check(!f.Active&&f.VisualKey==null,"finishes after exactly three seconds");
            }
            f.Start("bottom");f.Advance(500);f.Start("bottom");Check(f.PositionMs==0,"repeat tap restarts");f.Stop();Check(!f.Active,"drag cancels");
            Console.WriteLine("PASS "+checks+" fart interaction checks");return 0;
        }
        catch(Exception e){Console.Error.WriteLine(e);return 1;}
    }
}
