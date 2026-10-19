using System;
using System.Collections.Generic;
using System.IO;
using Naifrog.Core;
using Naifrog.Graphics;

internal static class CoreTests
{
    static int checks;
    static void Check(bool value,string message){checks++;if(!value)throw new Exception(message);}
    static PetController New(Assets a){return new PetController(a,a.Manifest.sway.durationsMs);}
    static void Settle(PetController p){p.Advance(2000);}
    static int Main(string[] args)
    {
        try
        {
            using(var a=new Assets(args[0]))
            {
                var p=New(a);Check(p.VisualKey=="idle","idle on startup");p.Tap(false);Check(p.Desired=="idle","head tap ignored");
                Check(a.Get("idle").GetPixel(160,160).A>200,"PNG pixels survive DPI-independent import");Check(a.Get("idle").GetPixel(0,0).A==0,"transparent pixel remains transparent");
                p.Tap(true);p.Advance(220);Check(p.VisualKey=="sway_00","entry endpoint");
                var seen=new HashSet<string>();for(int i=0;i<4390;i++){seen.Add(p.VisualKey);p.Advance(1);}
                Check(seen.Count==37,"all 37 originals");Check(p.OriginalFramesPresented==37,"original frames presented");Check(p.OriginalTimePlayed==4390,"original duration 4390ms");
                Check(p.InTransition&&p.Desired=="idle","automatic exit");p.Advance(220);Check(p.VisualKey=="idle","single cycle returns idle");
                p.Advance(20000);Check(p.VisualKey=="idle","no hidden looping");
                // Every original frame has an exit, and every exit can be interrupted.
                int elapsed=0;
                for(int frame=0;frame<37;frame++)
                {
                    p=New(a);p.Tap(true);p.Advance(220+elapsed+1);string visible=p.VisualKey;
                    Check(visible==PetController.SwayKey(frame),"frame timing "+frame);p.Tap(true);Check(p.VisualKey==a.Manifest.poses[visible].file,"no discontinuity on interrupt "+frame);
                    p.Advance(90);string exitFrame=p.VisualKey;p.Tap(true);Check(p.VisualKey==exitFrame,"no discontinuity reversing exit "+frame);Check(p.Desired=="sway","last click wins "+frame);
                    p.Advance(90);Check(p.VisualKey==visible,"exit reverses to current original "+frame);
                    p.Tap(true);Settle(p);Check(p.VisualKey=="idle","cancel after resumed sway "+frame);
                    elapsed+=a.Manifest.sway.durationsMs[frame];
                }
                p=New(a);p.Tap(true);p.Advance(80);string start=p.VisualKey;p.Tap(true);Check(start==p.VisualKey,"entry interrupted without jump");Settle(p);Check(p.VisualKey=="idle","entry cancel");
                p=New(a);for(int i=0;i<10;i++){p.Tap(true);p.Advance(23);}Settle(p);Check(p.Desired=="idle"&&p.VisualKey=="idle","10 clicks converge");
                p=New(a);p.Tap(true);p.Advance(600);p.BeginDrag();Check(p.Desired=="idle","drag cancels action");Settle(p);Check(p.VisualKey=="idle","drag exits action");
                p=New(a);p.SetLayout(LayoutMode.Bottom);Settle(p);p.BeginDrag();p.Advance(120);p.SetLayout(LayoutMode.Right);p.Advance(240);Check(p.Status=="idle -> peek_right","changed dock continues through idle without backtracking");p.Advance(360);Check(p.VisualKey=="peek_right","dock transfer finishes within two clips");
                foreach(LayoutMode mode in new[]{LayoutMode.Bottom,LayoutMode.Left,LayoutMode.Right})
                {p=New(a);p.SetLayout(mode);Settle(p);Check(p.Stable==PetController.BasePose(mode),"dock "+mode);p.Tap(true);Check(p.Desired==PetController.BasePose(mode),"docked belly ignored");p.BeginDrag();Settle(p);Check(p.VisualKey=="idle","undock "+mode);}
                p=New(a);p.SetLayout(LayoutMode.Left);Settle(p);p.SetKeyboard(true);Settle(p);Check(p.VisualKey=="sit_1","keyboard overrides");
                p.Tap(false);p.Advance(100);string sit=p.VisualKey;p.Tap(false);Check(p.VisualKey==sit,"seat reversal continuous");Settle(p);Check(p.VisualKey=="sit_1","seat latest intent");
                p.Tap(false);Settle(p);Check(p.VisualKey=="sit_2","seat two");p.SetKeyboard(false);Settle(p);Check(p.Layout==LayoutMode.Left&&p.VisualKey=="peek_left","keyboard restores prior dock");
                p=New(a);for(int i=0;i<5;i++){p.SetKeyboard(true);p.Advance(47);p.SetKeyboard(false);p.Advance(31);}Settle(p);Check(p.VisualKey=="idle","fast keyboard churn");
                var g=new Gesture();g.Begin(0,0,4);g.Move(3,0);Check(g.End(),"small movement remains click");g.Begin(0,0,4);Check(g.Move(4,0),"drag threshold");Check(!g.End(),"drag is not click");g.Begin(0,0,8);g.Cancel();Check(!g.End(),"lost capture not click");
                Check(HitRegions.Belly(160,166)&&HitRegions.Belly(160,220),"abdomen hit");Check(!HitRegions.Belly(160,53)&&!HitRegions.Belly(90,170)&&!HitRegions.Belly(160,280),"exclude head hands feet");
                var d=new DockResolver();var work=new Box(-1920,0,0,1040);var corner=new Box(-1910,800,-1760,1030);
                Check(d.Evaluate(corner,work,1,0)==LayoutMode.Free,"dock waits");Check(d.Evaluate(corner,work,1,121)==LayoutMode.Left,"side wins corner negative monitor");
                Check(d.Evaluate(new Box(-1880,800,-1730,1030),work,1,150)==LayoutMode.Left,"dock hysteresis");
                d.Evaluate(new Box(-1800,500,-1650,760),work,1,170);Check(d.Evaluate(new Box(-1800,500,-1650,760),work,1,291)==LayoutMode.Free,"dock leaves");
                d.Reset();Check(d.Drop(corner,work,1)==LayoutMode.Left,"quick edge release commits without preview delay");
                // Random interleavings exercise routing and cancellation without real timers.
                var random=new Random(22);p=New(a);for(int i=0;i<10000;i++){int op=random.Next(8);if(op<3)p.Tap(true);else if(op==3)p.SetKeyboard(true);else if(op==4)p.SetKeyboard(false);else if(op==5)p.BeginDrag();else if(op==6)p.SetLayout((LayoutMode)random.Next(4));p.Advance(random.Next(1,90));a.Get(p.VisualKey);}p.SetKeyboard(false);p.SetLayout(LayoutMode.Free);p.Request("idle");Settle(p);Check(p.VisualKey=="idle","random sequence converges");
                Console.WriteLine("PASS "+checks+" checks; 37 interruption positions; 10,000 mixed events");
            }
            return 0;
        }
        catch(Exception e){Console.Error.WriteLine(e);return 1;}
    }
}
