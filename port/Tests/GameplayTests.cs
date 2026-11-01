using System;
using System.IO;
using OsuLegacy;
using osu.Game.Rulesets.Scoring;

public static class GameplayTests
{
    private static int count;
    private static void Check(bool result,string message){count++;if(!result)throw new Exception(message);}
    private static bool Near(double a,double b,double tolerance){return Math.Abs(a-b)<tolerance;}
    private static Beatmap Load(string directory,string objects,string timing,string extraDifficulty)
    {
        string header="osu file format v14\n[General]\nAudioFilename: audio.wav\nMode: 0\n[Difficulty]\nOverallDifficulty: 5\nCircleSize: 5\nSliderMultiplier: 1.4\nSliderTickRate: 1\n"+extraDifficulty+"\n[TimingPoints]\n"+timing+"\n[HitObjects]\n";
        string path=Path.Combine(directory,"game.osu");File.WriteAllText(path,header+objects);return Beatmap.Load(path);
    }
    private static Session SpinnerSession()
    {
        var map=new Beatmap();map.Objects.Add(new Circle { Kind=ObjectKind.Spinner,Time=1000,Duration=2000,X=256,Y=192 });
        return new Session(map);
    }
    public static int Main()
    {
        string directory=Path.Combine(Path.GetTempPath(),"osu-gameplay-"+Guid.NewGuid());Directory.CreateDirectory(directory);
        try
        {
            File.WriteAllText(Path.Combine(directory,"audio.wav"),"fixture");
            const string red="0,500,4,1,0,100,1,0";
            Beatmap combos=Load(directory,"100,192,1000,1,0\n100,192,2000,1,0\n100,192,3000,21,0\n256,192,4000,12,0,5000\n100,192,6000,1,0\n",red,"");
            Check(combos.Objects[0].ComboNumber==1 && combos.Objects[1].ComboNumber==2 && combos.Objects[2].ComboNumber==1 && combos.Objects[4].ComboNumber==1,"Object numbers reset on a new combo and after spinners");
            Check(combos.Objects[2].ComboColourIndex==2 && combos.Objects[3].ComboColourIndex==2 && combos.Objects[4].ComboColourIndex==3,"Combo colour skips apply once and spinner new-combo flags are ignored");
            var timingSession=new Session(combos);timingSession.TouchDown(1,100,192,985);timingSession.TouchUp(1,100,192,990);timingSession.TouchDown(2,100,192,2020);
            Check(timingSession.HitTimings.Count==2 && timingSession.HitTimings[0].Offset==-15 && timingSession.HitTimings[1].Offset==20,"Hit-error meter receives signed early and late offsets");
            const string slider="100,192,1000,6,0,L|380:192,2,280\n";
            Beatmap map=Load(directory,slider,red,"");Circle obj=map.Objects[0];
            Check(obj.Kind==ObjectKind.Slider && obj.SpanCount==2,"Slider flags and span count");
            Check(Near(obj.SpanDuration,1000,0.001) && Near(obj.EndTime,3000,0.001),"BPM and multiplier determine duration");
            Check(obj.Events.Count==4 && Near(obj.Events[0].Time,1500,0.001) && obj.Events[1].Repeat && obj.Events[3].Tail,"Ticks, repeat and tail are generated");
            Check(Near(obj.PositionAt(1500).X,240,0.001) && Near(obj.PositionAt(2000).X,380,0.001) && Near(obj.PositionAt(2500).X,240,0.001) && Near(obj.PositionAt(3000).X,100,0.001),"Ball follows forward and reverse spans");
            var session=new Session(map);
            Check(session.TouchDown(1,100,192,1000)==HitResult.Great && !obj.Judged,"Head hit does not prematurely finish slider");
            session.TouchMove(1,240,192,1500);session.TouchMove(1,380,192,2000);session.TouchMove(1,240,192,2500);
            session.TouchMove(1,105.6,192,2980);session.TouchUp(1,105.6,192,2980);session.Advance(3000);
            Check(session.Great==1 && session.Finished && session.Combo==5,"Following ticks/repeats and lenient tail produces full slider result");
            Check(session.SliderTicksHit==5 && session.SliderTicksMissed==0,"Each slider nested part is counted once");
            session.Advance(4000);Check(session.Great==1 && session.Combo==5,"Slider is not judged twice");
            session=new Session(map);session.TouchDown(1,100,192,1000);session.TouchUp(1,100,192,1010);session.Advance(3200);
            Check(session.Meh==1 && session.Combo==0 && session.SliderTicksMissed==4,"Releasing slider loses nested parts and combo");
            session=new Session(map);session.Advance(3200);Check(session.Miss==1 && session.Finished,"Untouched slider expires");
            session=new Session(map);session.TouchDown(1,100,192,1000);session.TouchMove(1,0,0,1500);session.TouchMove(1,380,192,2000);session.TouchMove(1,240,192,2500);session.TouchMove(1,100,192,3000);
            Check(session.Ok==1 && session.SliderTicksMissed==1,"Slider tracking can recover after a break");
            Beatmap velocity=Load(directory,"100,100,2500,2,0,L|380:100,1,280\n100,100,3500,2,0,L|380:100,1,280\n",red+"\n2000,-50,4,1,0,100,0,0\n3000,1000,4,1,0,100,1,0","");
            Check(Near(velocity.Objects[0].Duration,500,0.001) && Near(velocity.Objects[0].TickDistance,280,0.001),"Inherited velocity affects duration and tick spacing");
            Check(Near(velocity.Objects[1].Duration,2000,0.001) && Near(velocity.Objects[1].TickDistance,140,0.001),"Red timing point resets inherited velocity");
            Beatmap noTicks=Load(directory,slider,red+"\n500,NaN,4,1,0,100,0,0","");
            Check(noTicks.Objects[0].Events.Count==2,"NaN difficulty point disables ticks but retains repeat/tail");
            Beatmap fasterTicks=Load(directory,slider,red,"SliderTickRate: 2");
            Check(fasterTicks.Objects[0].Events.Count==8,"Tick rate controls tick density");
            var line=SliderPath.Parse("L|200:0",0,0,100);Check(Near(line.Position(1).X,100,0.001),"Declared length trims paths");
            line=SliderPath.Parse("L|200:0",0,0,300);Check(Near(line.Position(1).X,300,0.001),"Declared length extends paths");
            foreach(double epsilon in new double[]{0.000000000001,0.0000001,0.000001,0.00001})
            {
                var precise=SliderPath.Parse("L|100:0",0,0,100+epsilon);
                Check(Near(precise.Length,100+epsilon,0.000000001) && Near(precise.Position(1).X,100+epsilon,0.000002),"Near-equal length endpoint lookup remains valid");
                Check(precise.ReverseAtEnd.Length==6 && precise.Coordinates.Length==precise.Points.Count*2,"Precision correction preserves repeat-arrow and vertex tables");
            }
            line=SliderPath.Parse("L|100:0|100:0",0,0,300);Check(Near(line.Length,100,0.001),"Repeated end point prevents path extension");
            var bezier=SliderPath.Parse("B|100:100|200:0",0,0,230);
            Check(bezier.Points.Count>10 && bezier.Position(0.5).Y>45,"Bezier curve is tessellated rather than treated as a line");
            var segmented=SliderPath.Parse("B|100:0|100:0|100:100",0,0,200);
            Check(Near(segmented.Position(0.5).X,100,0.001) && Near(segmented.Position(0.5).Y,0,0.001) && Near(segmented.Position(1).Y,100,0.001),"Repeated Bezier points separate segments");
            var arc=SliderPath.Parse("P|100:100|200:0",0,0,Math.PI*100);
            Check((arc.Position(0.5)-new PathPoint(100,100)).Length<0.3,"Perfect-circle arc passes through middle point");
            var collinear=SliderPath.Parse("P|100:0|200:0",0,0,200);
            Check(Near(collinear.Position(0.5).X,100,0.001),"Degenerate circle falls back to Bezier");
            var catmull=SliderPath.Parse("C|100:100|200:0",0,0,280);
            Check(catmull.Points.Count>50 && catmull.Position(0.5).Y>90,"Catmull interpolation");
            var bspline=SliderPath.Parse("B3|100:0|200:100|300:100",0,0,320);
            Check(bspline.Points.Count>100 && bspline.Position(0.5).Y>30,"B-spline curve type");
            Beatmap spinner=Load(directory,"100,50,1000,12,0,3000\n",red,"");
            Check(spinner.Objects[0].Kind==ObjectKind.Spinner && spinner.Objects[0].Duration==2000 && spinner.Objects[0].X==256,"Spinner end time, flags and fixed centre");
            session=new Session(spinner);Check(session.SpinsRequired(spinner.Objects[0])==5,"Lazer OD5 spinner clear RPM");
            session.Map.OD=0;Check(session.SpinsRequired(spinner.Objects[0])==3,"OD0 spinner clear RPM");session.Map.OD=10;Check(session.SpinsRequired(spinner.Objects[0])==7,"OD10 spinner clear RPM");
            session=SpinnerSession();session.TouchDown(7,356,192,1000);
            for(int i=1;i<=160;i++){double angle=i*Math.PI*2*5/160;session.TouchMove(7,256+100*Math.Cos(angle),192+100*Math.Sin(angle),1000+i*1990.0/160);}
            session.Advance(3000);Check(session.Great==1 && session.Finished,"Held full rotations clear spinner");
            session=SpinnerSession();session.TouchDown(7,356,192,1000);
            for(int i=1;i<=160;i++){double angle=-i*Math.PI*2*5/160;session.TouchMove(7,256+100*Math.Cos(angle),192+100*Math.Sin(angle),1000+i*1990.0/160);}
            session.Advance(3000);Check(session.Great==1,"Counterclockwise spins are supported");
            session=SpinnerSession();session.TouchDown(1,356,192,1000);session.TouchMove(1,356,192,2500);session.Advance(3000);
            Check(session.Miss==1,"Stationary hold does not clear spinner");
            session=SpinnerSession();session.TouchMove(1,256,292,2000);session.Advance(3000);Check(session.Miss==1,"Movement without a held contact is ignored");
            session=SpinnerSession();session.TouchDown(1,356,192,1000);session.TouchUp(1,356,192,1100);session.TouchDown(2,156,192,1200);
            Check(session.Map.Objects[0].Rotation==0,"Finger repositioning does not add artificial rotation");
            session.TouchMove(2,256,192,1300);session.TouchMove(2,356,192,1400);Check(session.Map.Objects[0].Rotation==0,"Centre crossing resets the angular baseline");
            session=SpinnerSession();session.TouchDown(1,356,192,1000);session.TouchDown(2,156,192,1000);session.TouchMove(2,256,292,1200);
            Check(session.Map.Objects[0].Rotation==0,"A second finger cannot double-count spinner rotation");
            session=SpinnerSession();session.TouchDown(1,356,192,1000);
            for(int i=1;i<=320;i++){double angle=i*Math.PI*2*10/320;session.TouchMove(1,256+100*Math.Cos(angle),192+100*Math.Sin(angle),1000+i*1990.0/320);}
            session.Advance(3000);Check(session.BonusScore==3000,"Bonus spins award capped bonus score");
            session=new Session(map);session.TouchDown(1,100,192,1000);session.ReleaseAll(1100);session.Advance(3200);
            Check(session.SliderTicksMissed==4,"Focus loss releases slider tracking");
            bool rejected=false;try{Load(directory,"256,192,1000,8,0,900\n",red,"");}catch(FormatException){rejected=true;}
            Check(rejected,"Invalid spinner interval is rejected");
            Beatmap zero=Load(directory,"100,192,1000,2,0,L|100:192,1,0\n",red,"");
            session=new Session(zero);session.TouchDown(1,100,192,1000);Check(session.Great==1 && session.Finished,"Zero-length slider head and tail resolve without hanging");
            Beatmap overlap=Load(directory,slider+"50,50,1500,1,0\n",red,"");
            session=new Session(overlap);session.TouchDown(1,100,192,1000);session.TouchMove(1,240,192,1500);session.TouchDown(2,50,50,1500);
            Check(session.Great==1 && overlap.Objects[0].PartsHit==2,"A second finger hits circles while the first follows a slider");
            session=new Session(map);session.TouchDown(1,100,192,1000);session.TouchMove(1,240,192,1500);session.TouchUp(1,240,192,1600);session.TouchDown(2,380,192,2000);
            session.TouchMove(2,240,192,2500);session.TouchMove(2,100,192,3000);Check(session.Great==1,"Slider tracking transfers to a new held contact");
            session=SpinnerSession();session.TouchDown(1,356,192,1000);session.TouchMove(1,256,292,1000);session.TouchMove(1,156,192,1000);
            Check(Near(session.Map.Objects[0].Rotation,Math.PI,0.000001),"Coarse audio-clock timestamps do not discard distinct spinner movement");
            Console.WriteLine("PASS: "+count+" slider/spinner parsing, geometry, input and scoring checks");return 0;
        }
        finally{Directory.Delete(directory,true);}
    }
}
