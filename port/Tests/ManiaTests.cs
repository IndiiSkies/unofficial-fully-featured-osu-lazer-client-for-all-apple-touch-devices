using System;using System.IO;using OsuLegacy;using osu.Game.Rulesets.Scoring;
class ManiaTests {
 static int checks;static void Check(bool ok,string name){checks++;if(!ok)throw new Exception(name);}
 static Beatmap Map(){return new Beatmap {Mode=3,ManiaColumns=4,CS=4,SourcePath="mania-test.osu",Title="Mania first steps",Artist="Generated click track"};}
 static Circle Note(int col,double start,double end){return new Circle {Kind=end>start?ObjectKind.Hold:ObjectKind.Circle,Time=start,Duration=end-start,Column=col};}
 static Session Single(bool hold){var m=Map();m.Objects.Add(Note(0,1000,hold?2000:1000));return new Session(m);}
 static void Touch(Session s,int phase,int id,int col,double t){s.Mania.Touch(phase,id,60+120*col,290,t,false);}
 static void Main(string[] args){
  var s=Single(false);Check(s.Mania.Windows[6]==19.5 && s.Mania.Windows[5]==49.5 && s.Mania.Windows[4]==82.5 && s.Mania.Windows[3]==112.5 && s.Mania.Windows[2]==136.5 && s.Mania.Windows[1]==173.5,"OD5 uses all six source windows and half-ms rounding");
  for(int i=6;i>=1;i--){Check(s.Mania.ResultFor(s.Mania.Windows[i])==(HitResult)i,"Inclusive judgement window "+i);Check(s.Mania.ResultFor(-s.Mania.Windows[i])==(HitResult)i,"Symmetric early window "+i);}
  Check(s.Mania.ResultFor(174)==HitResult.None,"Outside miss window does not consume a note");
  Touch(s,0,1,0,1000);Check(s.Finished && s.Mania.Counts[6]==1 && s.TotalScore==1000000 && s.Accuracy==100 && s.MaxCombo==1,"Perfect tap reaches normalized one million");
  Check(s.ScoreKey=="mania-test.osu|mania","Mania scores are isolated from standard, taiko and catch");
  s=Single(false);Touch(s,0,1,1,1000);s.Advance(1200);Check(s.Miss==1 && s.TotalScore==0,"Wrong-column touch cannot hit a note");
  s=Single(false);Touch(s,0,1,0,800);s.Advance(1200);Check(s.Miss==1,"Holding a key before the miss window cannot auto-hit later notes");
  s=Single(true);Touch(s,0,1,0,1000);Check(!s.Finished && s.Combo==1,"Hold head scores separately and waits for release");Touch(s,2,1,0,2000);Check(s.Finished && s.Mania.Counts[6]==2 && s.MaxCombo==2 && s.TotalScore==1000000,"Hold press and exact release both award MAX");
  s=Single(true);Touch(s,0,1,0,1000);Touch(s,2,1,0,2029);Check(s.Mania.Counts[6]==2,"Tail uses source 1.5 release lenience");
  s=Single(true);Touch(s,0,1,0,1000);s.Advance(2400);Check(s.Finished && s.Miss==1,"Never releasing a hold misses its tail");
  s=Single(true);Touch(s,0,1,0,1000);Touch(s,2,1,0,1300);Check(s.Mania.HoldBreaks==1 && s.Combo==0 && !s.Finished,"Early release breaks body and combo without finishing tail");Touch(s,0,2,0,1700);Touch(s,2,2,0,2000);Check(s.Finished && s.Mania.Counts[2]==1 && s.Mania.Counts[6]==1,"Recovered broken hold tail is capped to 50");
  s=Single(true);s.Advance(1200);Touch(s,0,1,0,1300);Touch(s,2,1,0,2000);Check(s.Finished && s.Miss==1 && s.Mania.Counts[2]==1,"Missed hold head can be recovered but tail is capped");
  var map=Map();map.Objects.Add(Note(0,1000,1000));map.Objects.Add(Note(3,1000,1000));s=new Session(map);Touch(s,0,1,0,1000);Touch(s,0,2,3,1000);Check(s.Finished && s.MaxCombo==2,"Independent touches hit simultaneous chords");
  map=Map();map.Objects.Add(Note(0,1000,1000));map.Objects.Add(Note(0,1100,1100));s=new Session(map);Touch(s,0,1,0,1100);Check(s.Miss==1 && s.Mania.Counts[6]==1,"Ordered note policy chooses the newest started note and misses older notes");
  map=Map();map.Objects.Add(Note(0,1000,1000));map.Objects.Add(Note(0,1500,1500));s=new Session(map);Touch(s,0,1,0,1000);s.Advance(1700);Check(s.Mania.Counts[6]==1 && s.Miss==1,"A held touch does not automatically retrigger subsequent notes");
  s=Single(true);Touch(s,0,1,0,1000);Touch(s,0,2,0,1100);Touch(s,2,1,0,1300);Check(s.Mania.Held[0]==1 && s.Mania.HoldBreaks==0,"Multiple touches in one column only release on the last finger");Touch(s,2,2,0,2000);Check(s.TotalScore==1000000,"Last finger releases hold exactly once");
  s=Single(true);Touch(s,0,1,0,1000);s.ReleaseAll(1400);Check(s.Mania.Held[0]==0 && s.Mania.HoldBreaks==0,"Pausing clears touches without breaking an active hold");s.Mania.Touch(0,2,60,290,1400,true);s.Advance(1500);Touch(s,2,2,0,2000);Check(s.TotalScore==1000000,"Preholding during resume countdown restores a hold");
  s=Single(true);Touch(s,0,1,0,1000);s.ReleaseAll(1400);s.Advance(1950);Check(s.Mania.HoldBreaks==1,"An unreacquired hold breaks after resume grace");
  s=Single(false);double hp=s.Health;s.Advance(800);Check(s.Health==hp,"Mania has no passive health drain");s.Advance(1200);Check(Math.Abs(s.Health-(1-6*.0075))<1e-9,"Normal miss uses source mania health loss");
  s=Single(true);s.Advance(1200);Check(Math.Abs(s.Health-(1-6*.00375))<1e-9,"Hold head has half normal-note miss HP loss");
  s=Single(false);s.Health=.001;s.Advance(1200);Check(s.Mania.Failed,"Depleted HP fails the play");
  s=Single(false);s.Mania.ApplyMods("MR");Touch(s,0,1,3,1000);Check(s.TotalScore==1000000,"Mirror reverses touch-to-column mapping");
  s=Single(false);s.Mania.ApplyMods("HR");Check(s.Mania.Windows[6]==13.5 && s.Mania.Windows[5]==35.5,"Hard Rock uses the source 1.4 timing multiplier");
  s=Single(false);s.Mania.ApplyMods("EZ");Check(s.Mania.Windows[6]==27.5 && s.Mania.Lives==2,"Easy widens windows and grants two extra lives");
  s=Single(false);s.Mania.ApplyMods("PF");Touch(s,0,1,0,1030);Check(s.Mania.Failed,"Perfect fails on a 300 rather than MAX");
  s=Single(false);s.Mania.ApplyMods("SD");s.Advance(1200);Check(s.Mania.Failed,"Sudden Death fails on miss");
  s=Single(false);Touch(s,0,1,0,1030);Check(s.Mania.Rank=="SS" && Math.Abs(s.Accuracy-300.0/305*100)<1e-8,"Mania 300-only play receives SS while accuracy distinguishes MAX");
  var result=new ScoreResult(s,"None",null);Check(result.Mania && result.Perfect==0 && result.Great==1 && result.Good==0 && result.PossibleCombo==1 && result.FullCombo,"Results preserve six-grade counts and mania combo maximum");
  var store=new LocalScoreStore(Path.Combine(args[1],"mania-scores.tsv"));store.Record(s);Check(store.Scores[s.ScoreKey].Rank=="SS" && !store.Scores.ContainsKey(s.Map.SourcePath),"Mania SS and mode-separated best persist");
  var retry=new Session(s.Map);Check(!retry.Finished && retry.Mania.Judged==0 && retry.Mania.Held[0]==0,"Retry resets notes, judgements and fingers");
  s.Map.ManiaMods="MR,HR";retry=new Session(s.Map);Check(retry.Mania.Mirror && retry.Mania.Windows[6]==13.5,"Retry reapplies the captured mod snapshot");
  Check(new LocalScoreStore(Path.Combine(args[1],"mania-scores.tsv")).Scores[s.ScoreKey].Great==1,"Saved mania grade counts survive reloading");
  map=Map();map.Objects.Add(Note(0,1000,2300));map.Objects.Add(Note(1,1800,1800));map.Objects.Add(Note(2,1900,1900));map.Objects.Add(Note(3,2100,2900));s=new Session(map);Touch(s,0,1,0,1000);
  var surface=new SvgUiSurface(args[0]);new GameplayView().Draw(surface,s,1400,480,320);surface.Save(Path.Combine(args[1],"mania-gameplay.svg"));
  s.Advance(5000);surface=new SvgUiSurface(args[0]);new ResultsScreen(new ScoreResult(s,"None",null),0,false).Draw(surface,4);surface.Save(Path.Combine(args[1],"mania-results.svg"));
  Console.WriteLine("PASS: "+checks+" mania timing, chords, holds, pause, score, health, mods and results checks");
 }
}
