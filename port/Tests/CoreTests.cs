using System;
using System.IO;
using OsuLegacy;
using osu.Game.Rulesets.Osu.Scoring;
using osu.Game.Rulesets.Scoring;

public static class CoreTests
{
    private static int assertions;
    private static void Check(bool condition, string name)
    { assertions++; if (!condition) throw new Exception(name); }

    public static int Main()
    {
        var windows = new OsuHitWindows();
        windows.SetDifficulty(5);
        Check(windows.ResultFor(49.5) == HitResult.Great, "OD5 inclusive Great boundary");
        Check(windows.ResultFor(-49.5) == HitResult.Great, "Early/late symmetry");
        Check(windows.ResultFor(50) == HitResult.Ok, "Outside Great boundary");
        Check(windows.ResultFor(99.5) == HitResult.Ok, "OK boundary");
        Check(windows.ResultFor(149.5) == HitResult.Meh, "Meh boundary");
        Check(windows.ResultFor(150) == HitResult.Miss, "Miss window");
        Check(windows.ResultFor(401) == HitResult.None, "Too-early ignore window");
        windows.SetDifficulty(0);
        Check(windows.WindowFor(HitResult.Great) == 79.5, "OD0 timing");
        windows.SetDifficulty(10);
        Check(windows.WindowFor(HitResult.Great) == 19.5, "OD10 timing");

        var map = new Beatmap();
        map.Circles.Add(new Circle { X = 256, Y = 192, Time = 1000 });
        map.Circles.Add(new Circle { X = 128, Y = 192, Time = 2000 });
        var session = new Session(map);
        Check(session.VisibleEnd(-500) == 0 && session.VisibleEnd(0) == 1, "Cull objects outside approach window");
        Check(session.Tap(0, 0, 1000) == HitResult.None, "Aim required");
        Check(session.Tap(256, 192, 1000) == HitResult.Great, "Hit judgement");
        Check(session.FirstPending == 1, "Retire judged object from render/input scans");
        Check(session.Tap(256, 192, 1000) == HitResult.None, "Cannot hit twice");
        session.Advance(2150);
        Check(session.Miss == 1 && session.Combo == 0 && session.MaxCombo == 1, "Late miss resets combo");
        Check(session.Finished && session.Accuracy == 50, "Results and accuracy");

        string directory = Path.Combine(Path.GetTempPath(), "osu-legacy-test-" + Guid.NewGuid());
        Directory.CreateDirectory(directory);
        try
        {
            File.WriteAllText(Path.Combine(directory, "song.wav"), "fixture");
            string path = Path.Combine(directory, "map.osu");
            string header = "osu file format v14\n[General]\nAudioFilename: song.wav\nMode: 0\n[Difficulty]\nOverallDifficulty: 6\n[HitObjects]\n";
            File.WriteAllText(path, header + "256,192,1000,5,0\n");
            Beatmap parsed = Beatmap.Load(path);
            Check(parsed.Circles.Count == 1 && parsed.AR == 6, "Circle flags and missing AR fallback");
            File.WriteAllText(path, header + "256,192,1000,2,0\n");
            bool rejected = false;
            try { Beatmap.Load(path); } catch (FormatException) { rejected = true; }
            Check(rejected, "Reject malformed slider data");
            File.WriteAllText(path, header.Replace("song.wav", "../outside.wav") + "256,192,1000,1,0\n");
            rejected = false;
            try { Beatmap.Load(path); } catch (FormatException) { rejected = true; }
            Check(rejected, "Reject audio path escape");
        }
        finally { Directory.Delete(directory, true); }
        var startGate=new GameplayStartGate();startGate.Arm(100);
        Check(!startGate.TryCommit(100) && !startGate.TryCommit(101),"Gameplay waits for two completed presentations, not loading duration");
        Check(startGate.TryCommit(102) && !startGate.TryCommit(103),"Presentation gate releases audio exactly once");
        startGate.Arm(uint.MaxValue);Check(!startGate.TryCommit(0) && startGate.TryCommit(1),"Presentation counter wrap is safe");
        startGate.Arm(20);startGate.Cancel();Check(!startGate.TryCommit(99),"Cancelled start cannot release playback");
        var earlyMap=new Beatmap();earlyMap.Circles.Add(new Circle {X=256,Y=192,Time=100});var earlySession=new Session(earlyMap);
        double lead=GameplayStartGate.LeadIn(100,0,earlySession.Preempt);
        Check(lead+100>=earlySession.Preempt,"First-second circle receives its full approach time");
        for(int frame=0;frame<120;frame++)earlySession.Advance(-lead);
        Check(earlySession.Miss==0 && !earlyMap.Objects[0].Judged,"Arbitrarily slow warm-up frames cannot miss the first note");
        earlySession.Tap(256,192,100);Check(earlySession.Great==1,"Early note remains hittable after warm-up");
        Check(GameplayStartGate.LeadIn(5000,2000,1200)==2000,"Explicit map audio lead-in is respected");
        var breaks=new BreakTimeline(new double[][]{new double[]{3000,15000},new double[]{16000,16649},new double[]{17000,17650},new double[]{Double.NaN,30000},new double[]{20000,19000}});
        double breakStart,breakEnd;
        Check(!breaks.TryGet(2999,out breakStart,out breakEnd) && breaks.TryGet(3000,out breakStart,out breakEnd),"Break overlay starts at authored event");
        Check(breaks.Active(14675) && !breaks.Active(14676) && breaks.TryGet(14999,out breakStart,out breakEnd) && !breaks.TryGet(15000,out breakStart,out breakEnd),"Break state ends 325ms early while overlay fades through authored end");
        Check(!breaks.TryGet(16200,out breakStart,out breakEnd) && breaks.TryGet(17100,out breakStart,out breakEnd),"650ms minimum break duration matches upstream");
        Check(BreakTimeline.Seconds(15000,5000)==10 && BreakTimeline.Seconds(15000,5000.1)==10 && BreakTimeline.Seconds(15000,4999.9)==11,"Countdown uses ceil of remaining song-clock seconds");
        Check(BreakTimeline.Alpha(3000,15000,3162.5)==.5 && BreakTimeline.Alpha(3000,15000,14837.5)==.5,"Break uses upstream 325ms entry and exit fades");
        Check(BreakTimeline.RemainingBar(3000,15000,14350)==0,"Remaining bar completes 650ms before authored break end");
        Check(breaks.DrainDuration(2000,16000)==2325,"Health drain excludes active break across a long frame");
        Check(breaks.TryGet(9000,out breakStart,out breakEnd) && breaks.TryGet(5000,out breakStart,out breakEnd) && breakEnd==15000,"Seek and paused clocks resolve break without wall-clock timers");
        Check(!new BreakTimeline(new double[0][]).TryGet(9000,out breakStart,out breakEnd),"Unmarked empty gaps do not invent a countdown");
        var breakMap=new Beatmap();breakMap.Circles.Add(new Circle {X=256,Y=192,Time=1000});breakMap.Circles.Add(new Circle {X=256,Y=192,Time=20000});breakMap.Breaks.Add(new double[]{3000,15000});var breakSession=new Session(breakMap);breakSession.Tap(256,192,1000);breakSession.Advance(3000);double breakHealth=breakSession.Health;breakSession.Advance(10000);Check(breakSession.Health==breakHealth,"Authored break does not drain osu health");
        var hpPath=new ArgonHealthPath();var hpRange=new double[64];int hpPoints=hpPath.CopyRange(0,1,hpRange);
        Check(hpPoints>16 && hpRange[0]==10 && hpRange[1]==10 && hpRange[(hpPoints-1)*2]==290 && hpRange[(hpPoints-1)*2+1]==40,"Argon health path follows upstream top line, curved slash and lower line endpoints");
        Check(hpPath.CopyRange(0,0,hpRange)==0 && hpPath.CopyRange(.8,.2,hpRange)==0,"Zero/reversed health ranges emit no fill");
        hpPoints=hpPath.CopyRange(.25,.75,hpRange);Check(hpPoints>=2 && hpRange[0]<hpRange[(hpPoints-1)*2],"Partial health/damage ranges clip along path length");
        var hpAnimation=new ArgonHealthAnimation();hpAnimation.Update(1,false,0);hpAnimation.Update(.5,true,100);
        Check(Math.Abs(hpAnimation.Value-.625)<.000001 && hpAnimation.Trail==1 && hpAnimation.Damage,"Health uses upstream 50ms half-time and holds red lost-health range");
        hpAnimation.Update(.5,false,599);Check(hpAnimation.Trail==1,"Damage range holds for 500ms after miss");hpAnimation.Update(.5,false,750);Check(Math.Abs(hpAnimation.Trail-.515625)<.000001,"Damage range returns with 300ms OutQuint easing");hpAnimation.Update(.5,false,900);Check(!hpAnimation.Damage && hpAnimation.Trail==.5,"Damage transition finishes without retaining red range");
        hpAnimation.Update(.3,false,0);Check(hpAnimation.Value==.3 && hpAnimation.Trail==.3,"Health animation resets on rewind/retry");
        hpAnimation.Update(.2,false,50);Check(!hpAnimation.Damage,"Passive drain does not create miss feedback");double frozenHealth=hpAnimation.Value;hpAnimation.Update(.2,false,50);Check(hpAnimation.Value==frozenHealth,"Paused song clock freezes health animation");
        Console.WriteLine("PASS: " + assertions + " core assertions (host only)");
        return 0;
    }
}
