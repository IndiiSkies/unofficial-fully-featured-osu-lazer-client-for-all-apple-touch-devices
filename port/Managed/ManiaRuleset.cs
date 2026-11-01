using System;using System.Collections.Generic;using osu.Game.Rulesets.Scoring;
namespace OsuLegacy {
 public sealed class ManiaNote {
  public Circle Source;public bool HeadJudged,TailJudged,Holding,Broken,HeadHit,Suspended;public HitResult HeadResult,TailResult;
  public double ResumeUntil;public bool Hold{get{return Source.Kind==ObjectKind.Hold;}}public bool Complete{get{return HeadJudged && (!Hold || TailJudged);}}
 }
 // ManiaHitWindows, DrawableHoldNote, OrderedHitPolicy and ManiaScoreProcessor adaptations.
 public sealed class ManiaSession {
  public readonly Session Owner;public readonly List<ManiaNote> Notes=new List<ManiaNote>();
  public readonly List<ManiaNote>[] Columns;public readonly int[] Held;public readonly double[] PressedAt;
  private readonly int[] first;private readonly Dictionary<int,int> touches=new Dictionary<int,int>();
  public readonly double[] Windows=new double[7];public readonly int[] Counts=new int[7];
  public int Judged,PossibleCombo,HoldBreaks,Lives;public bool Failed,Mirror;
  public double LastJudgementAt=-10000;public string LastJudgement="";public double ScrollTime=1500;
  private double points,comboPortion,maximumComboPortion,scoreMultiplier=1;
  private bool suddenDeath,perfect;private double hp;
  public double HpMultiplier=1;
  public ManiaSession(Session owner){
   Owner=owner;int count=owner.Map.ManiaColumns;Columns=new List<ManiaNote>[count];Held=new int[count];PressedAt=new double[count];first=new int[count];hp=owner.Map.HP;
   for(int i=0;i<count;i++){Columns[i]=new List<ManiaNote>();PressedAt[i]=-10000;}
   foreach(Circle o in owner.Map.Objects){var n=new ManiaNote {Source=o};Notes.Add(n);Columns[o.Column].Add(n);PossibleCombo+=n.Hold?2:1;}
   for(int i=1;i<=PossibleCombo;i++)maximumComboPortion+=ComboValue(300,i);
   SetWindows(1);
   PrepareHealth();
  }
  private void SetWindows(double multiplier){double od=Owner.Map.OD;double rate=Owner.Mods.Settings==null?1:Owner.Mods.Settings.Rate;multiplier/=rate;
   if(Owner.Mods.Has("CL")){bool convert=Owner.Map.ManiaConverted;double inverse=Math.Max(0,Math.Min(10,10-od));double[] classic={0,convert?158:158+3*inverse,convert?121:121+3*inverse,convert?97:97+3*inverse,convert?(Math.Round(od)>4?67:77):67+3*inverse,convert?(Math.Round(od)>4?34:47):34+3*inverse,16};for(int i=1;i<=6;i++)Windows[i]=Math.Floor(classic[i]/multiplier)+.5;return;}
   Windows[(int)HitResult.Perfect]=Window(od,22.4,19.4,13.9,multiplier);
   Windows[(int)HitResult.Great]=Window(od,64,49,34,multiplier);Windows[(int)HitResult.Good]=Window(od,97,82,67,multiplier);
   Windows[(int)HitResult.Ok]=Window(od,127,112,97,multiplier);Windows[(int)HitResult.Meh]=Window(od,151,136,121,multiplier);Windows[(int)HitResult.Miss]=Window(od,188,173,158,multiplier);
  }
  public void ApplyMods(string summary){string mods=","+summary+",";Mirror=mods.Contains(",MR,");suddenDeath=mods.Contains(",SD,");perfect=mods.Contains(",PF,");
   if(mods.Contains(",NF,"))scoreMultiplier*=.5;
   if(mods.Contains(",HR,")){SetWindows(1.4);hp=Math.Min(10,hp*1.4);scoreMultiplier*=1.06;}
   if(mods.Contains(",EZ,")){SetWindows(1/1.4);hp*=.5;Lives=2;scoreMultiplier*=.5;}
   PrepareHealth();
  }
  private static double Range(double value,double min,double mid,double max){return value<=5?min+(mid-min)*value/5:mid+(max-mid)*(value-5)/5;}
  // ManiaHealthProcessor runs the legacy calibration to recover HP gain, then
  // returns zero passive drain. Hold parents/body have zero maximum HP gain.
  private void PrepareHealth(){
   HpMultiplier=1;if(Notes.Count==0)return;
   double drop=.00025,low=Range(hp,.975,.8,.3),end=Range(hp,.99,.9,.4),recovery=Range(hp,.04,.02,0),gain=.0055-hp*.0005;
   for(int iteration=0;iteration<10000;iteration++){
    double health=1,uncapped=1,last=Notes[0].Source.Time;bool fail=false;int gap=0;
    foreach(ManiaNote n in Notes){Circle o=n.Source;
     while(gap<Owner.Map.Breaks.Count && Owner.Map.Breaks[gap][1]<=o.Time){last=o.Time;gap++;}
     double loss=drop*(o.Time-last);health=Math.Max(0,health-loss);uncapped=Math.Max(0,uncapped-loss);last=o.EndTime;
     if(health<=low){fail=true;drop*=.96;break;}
     loss=drop*o.Duration;double overkill=Math.Max(0,loss-health);health=Math.Max(0,health-loss);uncapped=Math.Max(0,uncapped-loss);
     int nested=n.Hold?2:0;for(int j=0;j<nested;j++){health=Math.Min(1,health+gain*HpMultiplier);uncapped+=gain*HpMultiplier;}
     if(overkill>0 && health-overkill<=low){fail=true;drop*=.96;break;}
     if(!n.Hold){health=Math.Min(1,health+gain*HpMultiplier);uncapped+=gain*HpMultiplier;}
    }
    if(!fail && health<end){fail=true;drop*=.94;HpMultiplier*=1.01;}
    if(!fail && (uncapped-1)/Notes.Count<recovery){fail=true;drop*=.96;HpMultiplier*=1.01;}
    if(!fail)return;
   }
   throw new FormatException("Mania health calibration did not converge");
  }
  private static double Window(double od,double min,double mid,double max,double multiplier){return Math.Floor((od<=5?min+(mid-min)*od/5:mid+(max-mid)*(od-5)/5)/multiplier)+.5;}
  public HitResult ResultFor(double offset){double value=Math.Abs(offset);for(int i=6;i>=1;i--)if(value<=Windows[i])return (HitResult)i;return HitResult.None;}
  public static int Value(HitResult result){return result==HitResult.Perfect?305:result==HitResult.Great?300:result==HitResult.Good?200:result==HitResult.Ok?100:result==HitResult.Meh?50:0;}
  private static double ComboValue(int value,int combo){return value*Math.Min(Math.Max(.5,Math.Log(Math.Max(1,combo),4)),Math.Log(400,4));}
  public double Accuracy{get{return Judged==0?100:points/(305*Judged)*100;}}
  public int Score{get{double a=Accuracy/100;return (int)Math.Round(Math.Round(150000*(maximumComboPortion==0?1:comboPortion/maximumComboPortion)+850000*Math.Pow(a,2+2*a)*(PossibleCombo==0?1:(double)Judged/PossibleCombo))*scoreMultiplier);}}
  public bool Finished{get{return Judged==PossibleCombo;}}
  public int FirstInColumn(int col){return first[col];}
  public string Rank{get{if(Counts[1]+Counts[2]+Counts[3]+Counts[4]==0 && Judged>0)return "SS";return Accuracy>=95?"S":Accuracy>=90?"A":Accuracy>=80?"B":Accuracy>=70?"C":"D";}}
  public static string Label(HitResult result){return result==HitResult.Perfect?"MAX":result==HitResult.Great?"300":result==HitResult.Good?"200":result==HitResult.Ok?"100":result==HitResult.Meh?"50":"MISS";}
  private void Break(ManiaNote note,double time){if(note.Broken)return;note.Broken=true;HoldBreaks++;Owner.Combo=0;LastJudgement="HOLD BREAK";LastJudgementAt=time;if(suddenDeath || perfect)Failed=true;}
  private void Judge(ManiaNote note,bool tail,HitResult result,double time,double offset){
   if(tail){if(note.TailJudged)return;note.TailJudged=true;note.TailResult=result;note.Holding=false;}
   else {if(note.HeadJudged)return;note.HeadJudged=true;note.HeadResult=result;note.HeadHit=result>HitResult.Miss;}
   Counts[(int)result]++;Judged++;int value=Value(result);points+=value;
   if(result==HitResult.Miss){if(Owner.Combo>=20 && Owner.Sound!=null)Owner.Sound(note.Source,"combobreak",0);Owner.Combo=0;Owner.Miss++;}
   else {Owner.Combo++;Owner.MaxCombo=Math.Max(Owner.MaxCombo,Owner.Combo);if(result>=HitResult.Great)Owner.Great++;else if(result==HitResult.Good || result==HitResult.Ok)Owner.Ok++;else Owner.Meh++;comboPortion+=ComboValue(Math.Min(300,value),Owner.Combo);
    var timing=new Session.HitTiming {Time=time,Offset=offset};Owner.HitTimings.Add(timing);Owner.AllHitTimings.Add(timing);if(Owner.HitTimings.Count>32)Owner.HitTimings.RemoveAt(0);
    if(!tail && Owner.Sound!=null)Owner.Sound(note.Source,"hit",0);
    if(tail && note.Source.ManiaTailSound && Owner.Sound!=null)Owner.Sound(note.Source,"hit",1);
   }
   double gain=result==HitResult.Miss?-(hp+1)*(note.Hold?.00375:.0075):result==HitResult.Meh?-(hp+1)*.0016:result==HitResult.Good?.004-hp*.0004:result==HitResult.Great?.005-hp*.0005:result==HitResult.Perfect?.0055-hp*.0005:0;
   if(gain>0)gain*=HpMultiplier;Owner.Health=Math.Max(0,Math.Min(1,Owner.Health+gain));
   if(Owner.Health<=0){if(Lives>0){Lives--;Owner.Health=1;}else Failed=true;}
   if((suddenDeath && result==HitResult.Miss) || (perfect && result!=HitResult.Perfect))Failed=true;
   LastJudgement=Label(result);LastJudgementAt=time;Owner.LastResult=LastJudgement;
   string history=result==HitResult.Perfect?"305":value.ToString();var j=new GameplayJudgement(note.Source.Column,0,time,history);Owner.ScoreHistory.Add(j);
   note.Source.Judged=note.Complete;
  }
  private void ReleaseColumn(int col,double time){foreach(ManiaNote note in Columns[col]){
    if(note.Complete || !note.Hold || !note.Holding)continue;
    HitResult result=ResultFor((time-note.Source.EndTime)/1.5);
    if(result!=HitResult.None){if((!note.HeadHit || note.Broken) && result>HitResult.Meh)result=HitResult.Meh;Judge(note,true,result,time,time-note.Source.EndTime);}
    else Break(note,time);
    note.Holding=false;note.Suspended=false;
   }}
  public void Advance(double time){
   for(int col=0;col<Columns.Length;col++){
    var list=Columns[col];while(first[col]<list.Count && list[first[col]].Complete)first[col]++;
    for(int i=first[col];i<list.Count;i++){var n=list[i];Circle o=n.Source;if(o.Time>time+Windows[1])break;if(n.Complete)continue;
     if(Owner.Mods.Automation){if(!n.HeadJudged && time>=o.Time){Judge(n,false,HitResult.Perfect,o.Time,0);n.Holding=n.Hold;}if(n.Hold && !n.TailJudged && time>=o.EndTime){Judge(n,true,HitResult.Perfect,o.EndTime,0);n.Holding=false;}continue;}
     if(!n.HeadJudged && time-o.Time>Windows[1])Judge(n,false,HitResult.Miss,time,time-o.Time);
     if(!n.Hold)continue;
     if(n.Suspended && Held[col]>0){n.Suspended=false;n.Holding=true;}
     if(n.Suspended && time>n.ResumeUntil){n.Suspended=false;n.Holding=false;Break(n,time);}
     if(!n.TailJudged && time-o.EndTime>Windows[1]*1.5){Judge(n,true,HitResult.Miss,time,time-o.EndTime);n.Holding=false;}
    }
   }
  }
  private void PressColumn(int col,double time){PressedAt[col]=time;var list=Columns[col];ManiaNote target=null;
   for(int i=first[col];i<list.Count;i++){var n=list[i];if(n.Complete)continue;if(n.Source.Time-time>Windows[1])break;
    if(n.Hold && n.HeadJudged && time<=n.Source.EndTime+Windows[1]){n.Holding=true;n.Suspended=false;}
    if(!n.HeadJudged && (target==null || n.Source.Time<=time))target=n;
    if(n.Source.Time>time)break;
   }
   if(target==null)return;
   foreach(var n in list){if(n.Source.EndTime>=target.Source.Time)break;if(!n.HeadJudged)Judge(n,false,HitResult.Miss,time,time-n.Source.Time);if(n.Hold && !n.TailJudged)Judge(n,true,HitResult.Miss,time,time-n.Source.EndTime);}
   HitResult result=ResultFor(time-target.Source.Time);if(result==HitResult.None)return;
   if(target.Hold){target.Holding=true;target.Suspended=false;}Judge(target,false,result,time,time-target.Source.Time);
  }
  public int ColumnAt(double x){int column=Math.Max(0,Math.Min(Columns.Length-1,(int)(x/480*Columns.Length)));return Mirror?Columns.Length-1-column:column;}
  public void Touch(int phase,int id,double x,double y,double time,bool paused){
   if(Owner.Mods.Automation)return;if(!paused)Advance(time);int old;bool had=touches.TryGetValue(id,out old);
   int next=phase>=2 || y<42?-1:ColumnAt(x);
   if(had && old==next)return;
   if(had){touches.Remove(id);Held[old]--;if(Held[old]==0 && !paused)ReleaseColumn(old,time);}
   if(next>=0){touches[id]=next;Held[next]++;if(Held[next]==1 && !paused)PressColumn(next,time);}
  }
  public void Suspend(double time){Advance(time);touches.Clear();for(int i=0;i<Held.Length;i++)Held[i]=0;foreach(var n in Notes)if(n.Holding && !n.Complete){n.Suspended=true;n.ResumeUntil=time+500;}}
 }
}
