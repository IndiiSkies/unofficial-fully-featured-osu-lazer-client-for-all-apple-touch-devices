using System;
using System.IO;
using System.Text;
using OsuLegacy;
sealed class MeasuredResultsSurface : IUiSurface,ITextWidthSurface {
 public int Measures;public string Title;
 public double TextWidth(string value,double size){Measures++;return value.Length*10;}
 public void Text(double x,double y,string value,double size,double r,double g,double b){if(x==59&&y==7)Title=value;}
 public void Rect(double x,double y,double w,double h,double r,double g,double b,double a,double s){}
 public void Circle(double x,double y,double radius,double r,double g,double b,double a,bool fill){}
 public void Image(string name,double x,double y,double w,double h,double alpha){}
 public bool Artwork(string name,double x,double y,double w,double h,double alpha){return false;}
 public bool Background(string name,bool bundled,double dim){return false;}
 public void Polyline(double[] p,int n,double x,double y,double scale,double width,double r,double g,double b,double a){}
 public void Arc(double x,double y,double radius,double progress,double width,double r,double g,double b,double a){}
}
public static class ResultsTests {
    static int checks;
    static void Check(bool value,string name){checks++;if(!value)throw new Exception(name);}
    static Beatmap Map(string directory,string name,string objects){
        string path=Path.Combine(directory,name+".osu");File.WriteAllText(path,"osu file format v14\n[General]\nAudioFilename: song.wav\n[Metadata]\nTitle: Results test\nArtist: osu! community\nCreator: test mapper\nVersion: Hard\n[Difficulty]\nOverallDifficulty:5\nCircleSize:5\nSliderMultiplier:1.4\nSliderTickRate:1\n[TimingPoints]\n0,500,4,2,0,100,1,0\n[HitObjects]\n"+objects);return Beatmap.Load(path);
    }
    public static int Main(string[] args){
        string root=Path.Combine(args[1],"results-tests");Directory.CreateDirectory(root);File.WriteAllText(Path.Combine(root,"song.wav"),"fixture");
        var objects=new StringBuilder();for(int i=0;i<40;i++)objects.Append("256,192,"+(1000+i*500)+",1,0\n");
        Beatmap map=Map(root,"completed",objects.ToString());map.BackgroundPath=Path.GetFullPath(Path.Combine(args[0],"demo-background.jpg"));
        var session=new Session(map);var finish=new GameplayCompletion(session);
        Check(!finish.Ready(0,false,false),"Unfinished gameplay cannot transition to results");
        for(int i=0;i<40;i++)session.Tap(256,192,1000+i*500+(i<20?-20:20));
        Check(session.Finished && session.Great==40,"Perfect test play resolves all objects");
        Check(session.HitTimings.Count==32 && session.AllHitTimings.Count==40 && session.ScoreHistory.Count==40,"Results retain full play while HUD histories remain bounded");
        double finalHit=20520;Check(!finish.Ready(finalHit,false,false),"Final judgement starts the outro delay");
        Check(!finish.Ready(finalHit+999,false,false) && finish.Ready(finalHit+1000,false,false),"Results wait the source's full one-second outro");
        Check(!finish.Ready(finalHit+2000,true,false),"Paused gameplay cannot transition behind its overlay");
        var failed=new GameplayCompletion(session);Check(!failed.Ready(finalHit+10000,false,true),"A failed play cannot enter successful results");
        var score=new ScoreResult(session,"HD",null);
        Check(score.Rank=="SS" && score.Silver && score.NewBest,"Perfect plays receive SS, with a silver Hidden badge");
        Check(score.Score==session.TotalScore && score.Accuracy==100 && score.MaxCombo==40 && score.PossibleCombo==40 && score.FullCombo,"Results agree with live score and maximum combo");
        Check(score.Artist=="osu! community" && score.Creator=="test mapper" && score.Difficulty=="Hard","Result header retains beatmap metadata");
        Check(Math.Abs(score.AverageError)<1e-8 && Math.Abs(score.UnstableRate-200)<1e-8,"Whole-play timing statistics use Welford population variance");
        int histogram=0;foreach(int count in score.Histogram)histogram+=count;Check(histogram==40,"Histogram includes early and late hits from the complete play");
        Check(LocalScoreStore.Rank(95,0)=="S" && LocalScoreStore.Rank(95,1)=="A","Lazer's S threshold and standard miss restriction");
        Check(LocalScoreStore.Rank(90,0)=="A" && LocalScoreStore.Rank(80,0)=="B" && LocalScoreStore.Rank(70,0)=="C" && LocalScoreStore.Rank(69.99,0)=="D","Lazer rank boundary accuracies");
        string storePath=Path.Combine(root,"scores.tsv");if(File.Exists(storePath))File.Delete(storePath);
        var store=new LocalScoreStore(storePath);store.Record(session);
        Check(store.Scores[map.SourcePath].Score==score.Score && store.Scores[map.SourcePath].Rank=="SS" && store.Scores[map.SourcePath].Plays==1,"Completed score and grade persist consistently");
        var equal=new ScoreResult(session,"None",store.Scores[map.SourcePath]);Check(!equal.NewBest && equal.PreviousBest==score.Score,"Repeated equal scores are not labelled new best");
        store.Record(session);var reopened=new LocalScoreStore(storePath);Check(reopened.Scores[map.SourcePath].Plays==2,"One record operation adds one completed play");
        File.WriteAllText(storePath,File.ReadAllText(storePath).Replace("\tSS\t","\tS\t"));Check(new LocalScoreStore(storePath).Scores[map.SourcePath].Rank=="SS","Perfect best scores from older versions migrate from S to SS");
        var view=new ResultsScreen(score,10,true);Check(view.IsAnimating(13.64) && !view.IsAnimating(13.66),"Flair stops at the source's 3.65-second duration");
        Check(view.TakeSoundCues(11.94)==0 && view.TakeSoundCues(11.96)==1 && view.TakeSoundCues(12)==0,"Rank impact plays exactly once at rank reveal");
        Check(view.TakeSoundCues(12.50)==2 && view.TakeSoundCues(14)==0,"Applause plays once after the source's additional 545ms delay");
        Check(view.RankImpactSample=="results-rank-impact-pass-ss.wav" && view.ApplauseSample=="results-applause-s.wav","SS selects the source's rank-specific samples");
        var noAnimation=new ResultsScreen(score,0,false);Check(!noAnimation.IsAnimating(0) && noAnimation.TakeSoundCues(0)==1 && noAnimation.TakeSoundCues(.55)==2,"Disabling visual animation preserves the separate delayed applause cue");
        var surface=new SvgUiSurface(args[0]);view.Draw(surface,14);surface.Save(Path.Combine(args[1],"results-summary.svg"));
        Check(view.Tap(14,290)==ResultsAction.Back && view.Tap(405,290)==ResultsAction.Retry,"Back and retry have separate bounded controls");
        Check(view.Tap(330,290)==ResultsAction.None && view.Tap(200,100)==ResultsAction.None,"Taps outside result controls do not retry or leave");
        Check(view.Tap(200,290)==ResultsAction.Statistics && view.Statistics,"Statistics button opens the graphs");
        surface=new SvgUiSurface(args[0]);view.Draw(surface,14);surface.Save(Path.Combine(args[1],"results-statistics.svg"));
        Check(view.Tap(200,290)==ResultsAction.Statistics && !view.Statistics,"Statistics returns to summary without changing the score");
        var retry=new Session(map);Check(!retry.Finished && retry.Great==0 && retry.AllHitTimings.Count==0 && retry.ScoreHistory.Count==0,"Retry starts with clean scoring and timing state");
        Check(score.Score==12000 && score.Great==40 && score.MaxCombo==40,"Result snapshot survives resetting the shared beatmap for retry");
        store.Record(retry);Check(store.Scores[map.SourcePath].Plays==2,"Abandoned incomplete attempts do not overwrite the score or add plays");
        for(int i=0;i<35;i++)retry.Tap(256,192,1000+i*500);retry.Advance(21000);
        var lowerScore=new ScoreResult(retry,"HD",store.Scores[map.SourcePath]);
        Check(!lowerScore.NewBest && lowerScore.Rank=="B" && !lowerScore.Silver && lowerScore.Miss==5,"Lower completed plays show their own grade and misses without a false best or silver badge");
        store.Record(retry);Check(store.Scores[map.SourcePath].Score==12000 && store.Scores[map.SourcePath].Plays==3,"A lower result increments plays while preserving the local personal best");
        Check(Math.Abs(lowerScore.AccuracyGraph[63]-.875)<1e-8,"Accuracy graph ends at the result's real final accuracy");
        retry=new Session(map);
        bool rejected=false;try{new ScoreResult(retry,"None",null);}catch(InvalidOperationException){rejected=true;}Check(rejected,"Incomplete attempts cannot produce a completed score snapshot");
        Beatmap overlap=Map(root,"long-slider","100,192,1000,2,0,L|660:192,2,560\n256,192,2000,1,0\n");
        var longPlay=new Session(overlap);longPlay.Great=2;var longFinish=new GameplayCompletion(longPlay);double end=overlap.Objects[0].EndTime;
        Check(!longFinish.Ready(2000,false,false) && !longFinish.Ready(end+999,false,false) && longFinish.Ready(end+1000,false,false),"Outro waits for a long slider beyond the final circle's time");
        var parts=new ScoreResult(longPlay,"None",null);Check(parts.PossibleCombo==2+overlap.Objects[0].Events.Count,"Slider heads, ticks, repeats and tails contribute to possible combo");
        var nofail=new Session(Map(root,"misses","256,192,1000,1,0\n"));nofail.Advance(2000);nofail.Health=0;var nfFinish=new GameplayCompletion(nofail);
        Check(!nfFinish.Ready(2000,false,false) && nfFinish.Ready(3000,false,false),"Completed No Fail plays can reach results even at zero health");
        Check(new ScoreResult(nofail,"NF",null).Rank=="D","A No Fail miss is not mistaken for a perfect score");
        Check(!new ScoreResult(nofail,"HD",null).Silver,"Hidden only grants silver variants to S/SS grades");
        nofail.Map.Title=new String('界',30)+"\ud83d\ude00"+new String('界',30);
        var fitted=new ResultsScreen(new ScoreResult(nofail,"NF",null),0,false);var measured=new MeasuredResultsSurface();fitted.Draw(measured,0);
        Check(measured.Title.EndsWith("...") && measured.Title.Length*10<=343 && !Char.IsHighSurrogate(measured.Title[measured.Title.Length-4]),"Long Unicode metadata fits its measured header without splitting a surrogate pair");
        int measurements=measured.Measures;fitted.Draw(measured,1);Check(measured.Measures==measurements,"Metadata fitting is cached instead of measured every animation frame");
        var target=new LocalScore {Score=1000,Accuracy=97.5,MaxCombo=25};var competition=new SelfCompetition(target);
        competition.Update(999,1000);Check(competition.Position==2 && competition.Gap(999)==-1,"Live score stays below saved personal best");
        competition.Update(1000,1100);Check(!competition.Passed && competition.Position==2,"Ties rank below the existing score, as in lazer");
        competition.Update(1001,1200);Check(competition.Passed && competition.Position==1 && competition.PassedAt==1200,"Passing personal best promotes the live score");
        competition.Update(1200,1500);Check(competition.PassedAt==1200,"Passing notification fires only once per attempt");
        target.Score=5000;Check(competition.BestScore==1000,"Competition target remains frozen when saved data changes");
        var retryCompetition=new SelfCompetition(target);Check(!retryCompetition.Passed && retryCompetition.Position==2 && retryCompetition.BestScore==5000,"Retry starts a fresh comparison against the latest saved best");
        var firstPlay=new SelfCompetition(null);firstPlay.Update(10000,0);Check(!firstPlay.HasBest && !firstPlay.Passed && firstPlay.Position==1,"First play has no invented previous best or pass notification");
        var competitionPreview=new SvgUiSurface(args[0]);var competitionView=new GameplayView {Competition=new SelfCompetition(new LocalScore {Score=11000,Accuracy=95,MaxCombo=35})};competitionView.Draw(competitionPreview,session,22000,480,320);competitionPreview.Save(Path.Combine(args[1],"gameplay-personal-best.svg"));
        string taikoPath=Path.Combine(root,"taiko.osu");
        File.WriteAllText(taikoPath,"osu file format v14\n[General]\nAudioFilename: song.wav\nMode: 1\n[Difficulty]\nOverallDifficulty:5\nSliderMultiplier:1.4\n[TimingPoints]\n0,500,4,1,0,100,1,0\n[HitObjects]\n256,192,1000,1,0\n256,192,1500,1,2\n256,192,2000,1,4\n256,192,2500,1,8\n256,192,3000,2,0,L|536:192,1,280\n256,192,5000,8,0,6000\n");
        var taikoMap=Beatmap.Load(taikoPath);var taiko=new Session(taikoMap);
        Check(taikoMap.Mode==1 && taikoMap.Objects[4].Path==null && Math.Abs(taikoMap.Objects[4].Duration-1000)<=1,"Native taiko loads rolls without building slider meshes");
        Check(taiko.TaikoGreatWindow==34.5 && taiko.TaikoOkWindow==79.5 && taiko.TaikoMissWindow==94.5,"Taiko OD5 windows match upstream floor-minus-half constants");
        Check(taiko.TaikoTap(false,false,1000)==osu.Game.Rulesets.Scoring.HitResult.Great,"Centre pad hits centre notes");
        Check(taiko.TaikoTap(true,true,1560)==osu.Game.Rulesets.Scoring.HitResult.Ok && taiko.Accuracy==75,"Rim pad hits rim notes; Good carries half accuracy");
        taiko.TaikoTap(false,false,2000);int beforeStrong=taiko.TotalScore;taiko.TaikoTap(false,true,2020);
        Check(taiko.TotalScore==beforeStrong+300 && taiko.Combo==3,"Large note second hand earns bonus without a second combo or primary judgement");
        taiko.TaikoTap(false,false,2500);Check(taiko.Miss==1 && taiko.Combo==0,"Wrong-colour input misses and breaks combo");
        int primary=taiko.Great+taiko.Ok+taiko.Miss;taiko.TaikoTap(false,false,3100);int rollScore=taiko.TotalScore;taiko.TaikoTap(true,true,3110);
        Check(taiko.TotalScore==rollScore && taikoMap.Objects[4].PartsHit==1,"Roll tap debounce prevents duplicate tick awards");
        taiko.TaikoTap(true,true,3200);taiko.Advance(4000);
        Check(taikoMap.Objects[4].Judged && taiko.Great+taiko.Ok+taiko.Miss==primary && taiko.Combo==0,"Drum roll bonus does not affect accuracy or combo");
        taiko.TaikoTap(false,false,5000);taiko.TaikoTap(false,true,5050);Check(taikoMap.Objects[5].PartsHit==1,"Swell rejects repeated centre hits");
        int needed=taiko.SwellRequired(taikoMap.Objects[5]);Check(needed==8,"Swell required hits follow upstream OD rate and 1.65 multiplier");
        for(int i=1;i<needed;i++)taiko.TaikoTap(i%2==1,i%2==1,5000+i*100);
        Check(taiko.Finished && taiko.Great+taiko.Ok+taiko.Miss==4,"Alternating swell completes without adding accuracy judgements");
        var taikoResult=new ScoreResult(taiko,"None",null);Check(taikoResult.Taiko && taikoResult.PossibleCombo==4 && taikoResult.Meh==0 && taikoResult.HitWindow==79.5,"Taiko results count normal notes and use taiko windows");
        var taikoPreview=new SvgUiSurface(args[0]);var resetTaiko=new Session(taikoMap);new GameplayView().Draw(taikoPreview,resetTaiko,1100,480,320);taikoPreview.Save(Path.Combine(args[1],"taiko-gameplay.svg"));
        resetTaiko.Advance(6001);Check(resetTaiko.Finished && resetTaiko.Miss==4 && resetTaiko.Health<1,"Unplayed taiko notes miss, failed swell drains health, and the map finishes");
        var retryTaiko=new Session(taikoMap);Check(!retryTaiko.Finished && taikoMap.Objects[5].PartsHit==0,"Retry resets taiko roll and swell state");
        Check(new LazerUi().Screen==UiScreen.Home && new Beatmap().Mode==0,"osu! remains the default ruleset");
        var visuals=new Session(taikoMap);visuals.TaikoTap(false,false,1000);visuals.TaikoTap(true,true,1500);
        Check(visuals.TaikoPadTimes[1]==1000 && visuals.TaikoPadTimes[3]==1500,"Drum halves retain independent centre/rim flash times");
        Check(visuals.TaikoEffects.Count==2 && !visuals.TaikoEffects[0].Rim && visuals.TaikoEffects[1].Rim,"Successful hits retain their own burst colours");
        visuals.TaikoTap(true,false,2000);Check(visuals.TaikoMascotFailed && visuals.TaikoEffects.Count==2,"Miss changes mascot state without a successful burst");
        Check(GameplayView.TaikoMascotTexture(visuals,2000,false).Contains("fail"),"Mascot reacts to the last missed primary note");
        visuals.TaikoClearAt=2000;Check(GameplayView.TaikoMascotTexture(visuals,2700,false)=="taiko-pippidonclear5.png","Mascot clear animation follows upstream 100ms sequence including reverse frames");
        Check(GameplayView.TaikoMascotTexture(visuals,2700,true)=="taiko-pippidonfail0.png","Reduced effects keeps mascot on a static state frame");
        for(int i=0;i<30;i++)visuals.TaikoTap(i%2==0,i%2==1,5000+i*10);
        Check(visuals.TaikoEffects.Count<=8,"Taiko effect history is bounded during rapid drum input");
        var animatedPreview=new SvgUiSurface(args[0]);new GameplayView().Draw(animatedPreview,visuals,5150,480,320);animatedPreview.Save(Path.Combine(args[1],"taiko-effects.svg"));
        var visualRetry=new Session(taikoMap);Check(visualRetry.TaikoEffects.Count==0 && visualRetry.TaikoPadTimes[0]<0 && !visualRetry.TaikoMascotFailed,"Retry clears previous mascot and drum effects");
        string beatPath=Path.Combine(root,"taiko-beats.osu");File.WriteAllText(beatPath,File.ReadAllText(taikoPath).Replace("0,500,4,1,0,100,1,0","0,500,4,1,0,100,1,0\n2000,-100,4,1,0,100,0,1\n3000,400,4,1,0,100,1,0"));
        var beatMap=Beatmap.Load(beatPath);bool kiai;
        Check(beatMap.BeatPosition(2500,out kiai)==5 && kiai,"Mascot follows red-line beats and inherited kiai changes");
        Check(beatMap.BeatPosition(3400,out kiai)==1 && !kiai,"Mascot clock follows tempo changes and kiai ending");
        var beatSession=new Session(beatMap);Check(GameplayView.TaikoMascotTexture(beatSession,2500,false).Contains("kiai1"),"Kiai uses its original animation frames");
        var comboMap=new Beatmap {Mode=1};for(int i=0;i<50;i++)comboMap.Objects.Add(new Circle {Time=1000+i*300});
        var comboSession=new Session(comboMap);for(int i=0;i<50;i++)comboSession.TaikoTap(false,false,1000+i*300);
        Check(comboSession.Combo==50 && comboSession.TaikoClearAt==15700 && GameplayView.TaikoMascotTexture(comboSession,15700,false).Contains("clear0"),"Every 50-hit combo triggers the mascot clear celebration");
        Console.WriteLine("PASS: "+checks+" results, competition and taiko gameplay checks");return 0;
    }
}
