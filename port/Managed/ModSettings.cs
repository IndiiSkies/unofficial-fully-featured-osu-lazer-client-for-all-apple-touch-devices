using System;using System.Collections.Generic;
namespace OsuLegacy {
 public sealed class ModSettings {
  private readonly Dictionary<string,bool> flags=new Dictionary<string,bool>();
  public readonly string Summary;public readonly int Mode,Seed;public readonly double Rate,AccuracyTarget,CS,AR,OD,HP,Multiplier;public readonly bool PitchAudio,RecordScore;
  public bool Has(string acronym){return flags.ContainsKey(acronym);}
  public ModSettings(LegacyModState state,int mode){Mode=mode;Summary=state.SummaryFor(mode);foreach(string a in Summary.Split(','))if(a!="None")flags[a]=true;Seed=state.Seed==0?unchecked((int)DateTime.UtcNow.Ticks):state.Seed;
   Rate=Has("DT")||Has("NC")?(state.SpeedOverride>1 && state.SpeedOverride<=2?state.SpeedOverride:1.5):Has("HT")||Has("DC")?(state.SpeedOverride>=.5 && state.SpeedOverride<1?state.SpeedOverride:.75):1;
   PitchAudio=Has("NC")||Has("DC");AccuracyTarget=state.MinimumAccuracy;CS=state.DACs;AR=state.DAAr;OD=state.DAOd;HP=state.DAHp;Multiplier=state.MultiplierFor(mode);RecordScore=!(Has("AT")||Has("CN")||Has("RX")||Has("AP")||Has("TP")||Has("DA"));foreach(var mod in state.All)if(Has(mod.Acronym) && mod.Category=="fun")RecordScore=false;
  }
  public double ScoreFactor {
   get {double factor=Multiplier;if(Mode==2){if(Has("NF"))factor/=.5;if(Has("EZ"))factor/=.5;if(Has("HR"))factor/=1.12;if(Has("HD"))factor/=1.06;}if(Mode==3){if(Has("NF"))factor/=.5;if(Has("EZ"))factor/=.5;if(Has("HR"))factor/=1.06;}return factor;}
  }
  public void Apply(Beatmap map){
   map.ModSettings=this;
   if(Mode<2){if(Has("EZ")){map.CS*=.5;map.AR*=.5;map.OD*=.5;map.HP*=.5;}if(Has("HR")){map.CS=Math.Min(10,map.CS*1.3);map.AR=Math.Min(10,map.AR*1.4);map.OD=Math.Min(10,map.OD*1.4);map.HP=Math.Min(10,map.HP*1.4);}}
   if(Has("DA")){if(CS>=0)map.CS=CS;if(AR>=0)map.AR=AR;if(OD>=0)map.OD=OD;if(HP>=0)map.HP=HP;}
   if(Mode==2)map.ApplyCatchMods(Summary);if(Mode==3)map.ManiaMods=Summary;
   var random=new Random(Seed);
   if(Mode==0 && Has("TP") && map.Objects.Count>0){
    double first=map.Objects[0].Time,last=0;foreach(var obj in map.Objects)last=Math.Max(last,obj.EndTime);map.Objects.Clear();int count=0;
    for(double time=first;time<=last && count<200000;time+=map.BeatLengthAt(time)){map.Objects.Add(new Circle {Time=time,X=64+random.NextDouble()*384,Y=48+random.NextDouble()*288,ComboNumber=count%4+1,ComboColourIndex=count/4,SourceIndex=count++});}
   }
   int[] columns=new int[map.ManiaColumns];for(int i=0;i<columns.Length;i++)columns[i]=i;if(Mode==3 && Has("RD"))for(int i=columns.Length-1;i>0;i--){int j=random.Next(i+1),v=columns[i];columns[i]=columns[j];columns[j]=v;}
   double comboTime=0;for(int i=0;i<map.Objects.Count;i++){
    Circle obj=map.Objects[i];obj.ModIndex=i;if(i==0 || obj.ComboNumber==1)comboTime=obj.Time;obj.ModComboStart=comboTime;
    if(Mode==0 || (Mode==2 && Has("MR"))){double dx=0,dy=0;if(Has("RD") && Mode==0 && obj.Kind!=ObjectKind.Spinner){dx=40+random.NextDouble()*432-obj.X;dy=40+random.NextDouble()*304-obj.Y;}
     bool flipX=Has("MR"),flipY=Mode==0 && Has("HR");obj.X=flipX?512-(obj.X+dx):obj.X+dx;obj.Y=flipY?384-(obj.Y+dy):obj.Y+dy;
     if(obj.Path!=null){for(int p=0;p<obj.Path.Points.Count;p++){PathPoint old=obj.Path.Points[p];obj.Path.Points[p]=new PathPoint(flipX?512-old.X-dx:old.X+dx,flipY?384-old.Y-dy:old.Y+dy);}TransformArray(obj.Path.Coordinates,dx,dy,flipX,flipY);TransformArray(obj.Path.ReverseAtStart,dx,dy,flipX,flipY);TransformArray(obj.Path.ReverseAtEnd,dx,dy,flipX,flipY);}
    }
    if(Mode==1 && Has("RD")){if(random.Next(2)==0)obj.HitSound&=~10;else obj.HitSound|=2;}
    if(Mode==3 && Has("RD"))obj.Column=columns[obj.Column];
   }
  }
  private static void TransformArray(double[] values,double dx,double dy,bool flipX,bool flipY){if(values==null)return;for(int i=0;i<values.Length;i+=2){values[i]=flipX?512-values[i]-dx:values[i]+dx;values[i+1]=flipY?384-values[i+1]-dy:values[i+1]+dy;}}
 }
 public struct ModObjectState {public double X,Y,Scale,Rotation,Alpha,Approach;}
 public sealed class ModRuntime {
  public readonly ModSettings Settings;private readonly Session owner;private readonly PathPoint[] offsets;private readonly double[] offsetTimes;
  public double AimX=256,AimY=192,BlockedAt=-10000;private int lastAction=-1;private double previousKeyTime=-1;
  public ModRuntime(ModSettings settings,Session session){Settings=settings;owner=session;offsets=new PathPoint[session.Map.Objects.Count];offsetTimes=new double[offsets.Length];}
  public bool Has(string mod){return Settings!=null && Settings.Has(mod);}
  public double Preempt(Circle obj){return owner.Preempt+(Has("FR")?Math.Max(0,obj.Time-obj.ModComboStart):0);}
  private static double Clamp(double n,double a,double b){return Math.Max(a,Math.Min(b,n));}
  private static PathPoint WigglePoint(Circle obj,int step){uint r=unchecked((uint)((int)obj.Time+step*7919+1));r^=r<<13;r^=r>>17;r^=r<<5;double angle=(r/4294967296.0)*Math.PI*2;r^=r<<13;r^=r>>17;r^=r<<5;double distance=(r/4294967296.0)*7;return new PathPoint(Math.Cos(angle)*distance,Math.Sin(angle)*distance);}
  public ModObjectState Object(Circle obj,double time){
   double preempt=Preempt(obj),progress=Clamp((time-obj.Time+preempt)/preempt,0,1),remaining=1-progress;var state=new ModObjectState {X=obj.X,Y=obj.Y,Scale=1,Alpha=1,Approach=1+3*remaining};
   if(Has("WG")){double part=(time-obj.Time+preempt)/100;int step=(int)Math.Floor(Math.Max(0,part));PathPoint a=WigglePoint(obj,step),b=WigglePoint(obj,step+1);double t=part-Math.Floor(part);state.X+=a.X+(b.X-a.X)*t;state.Y+=a.Y+(b.Y-a.Y)*t;}
   if(Has("TR")){double theta=obj.ModIndex*.4,distance=(preempt-Math.Min(400,preempt))*.5*(1+Math.Cos(Math.PI*progress))*.5;state.X+=Math.Cos(theta)*distance;state.Y+=Math.Sin(theta)*distance;}
   if(Has("MG") || Has("RP")){int i=obj.ModIndex;if(i>=0 && i<offsets.Length){double dt=Math.Max(0,time-offsetTimes[i]);if(offsetTimes[i]==0 || time<offsetTimes[i])dt=0;offsetTimes[i]=time;double factor=1-Math.Pow(.5,dt/(Has("MG")?1520:Math.Max(1,Math.Sqrt(Math.Pow(state.X-AimX,2)+Math.Pow(state.Y-AimY,2))/.06)));
     double desiredX=Has("MG")?AimX:Clamp(2*(state.X+offsets[i].X)-AimX,0,512),desiredY=Has("MG")?AimY:Clamp(2*(state.Y+offsets[i].Y)-AimY,0,384);
     offsets[i]=new PathPoint(offsets[i].X+(desiredX-state.X-offsets[i].X)*factor,offsets[i].Y+(desiredY-state.Y-offsets[i].Y)*factor);state.X+=offsets[i].X;state.Y+=offsets[i].Y;}}
   if(Has("GR"))state.Scale=.5+.5*progress;if(Has("DF"))state.Scale=2-progress;
   if(Has("SI")){state.Scale=.5-.5*Math.Cos(Math.PI*progress);state.Rotation=2*Math.PI*(1-progress);}
   if(Has("DP")){double z=100*(1-progress);state.Scale=200/(200+z);state.X=256+(state.X-256)*state.Scale;state.Y=192+(state.Y-192)*state.Scale;}
   if(Has("HD")){state.Alpha=progress<.4?progress/.4:Clamp((.7-progress)/.3,0,1);state.Approach=0;}
   if(Has("SI"))state.Approach=0;if(Has("AD"))state.Approach=4-3*progress*progress;
   if(Has("FR"))state.Approach=1+3*Math.Max(0,(obj.Time-time)/owner.Preempt);
   if(Has("CL") && time>obj.Time)state.Alpha*=Clamp(1-(time-obj.Time)/owner.Windows.WindowFor(osu.Game.Rulesets.Scoring.HitResult.Meh),0,1);
   return state;
  }
  public PathPoint Position(Circle obj,PathPoint point,double time){var s=Object(obj,time);double dx=(point.X-obj.X)*s.Scale,dy=(point.Y-obj.Y)*s.Scale,co=Math.Cos(s.Rotation),si=Math.Sin(s.Rotation);return new PathPoint(s.X+dx*co-dy*si,s.Y+dx*si+dy*co);}
  public PathPoint AutoAim(double time){Circle previous=null;for(int index=Math.Max(0,owner.FirstPending-1);index<owner.Map.Objects.Count;index++){var obj=owner.Map.Objects[index];if(obj.Kind==ObjectKind.Spinner && time>=obj.Time && time<=obj.EndTime)return new PathPoint(256+100*Math.Cos(time/40),192+100*Math.Sin(time/40));if(obj.Kind==ObjectKind.Slider && time>=obj.Time && time<=obj.EndTime)return Position(obj,obj.PositionAt(time),time);if(obj.Time>=time){var target=Position(obj,new PathPoint(obj.X,obj.Y),time);if(previous==null)return target;var from=Position(previous,previous.PositionAt(Math.Min(time,previous.EndTime)),time);double p=Clamp((time-previous.EndTime)/Math.Max(1,obj.Time-previous.EndTime),0,1);p=p*p*(3-2*p);return from+(target-from)*p;}previous=obj;}return previous==null?new PathPoint(256,192):Position(previous,previous.PositionAt(previous.EndTime),time);}
  public bool AcceptAction(int action,double time){if(time<previousKeyTime || owner.BreakTimeline.Active(time) || (owner.Map.Objects.Count>0 && time<owner.Map.Objects[0].Time-owner.Windows.WindowFor(osu.Game.Rulesets.Scoring.HitResult.Meh)))lastAction=-1;previousKeyTime=time;
   bool allowed=lastAction<0 || (Has("AL")?lastAction!=action:!Has("SG")||lastAction==action);if(!allowed){BlockedAt=time;return false;}lastAction=action;return true;}
  public double CursorScale {get{return Has("BM")?1+9*Math.Min(1,owner.Combo/50.0):1;}}
  public double CursorAlpha(double time){if(!Has("NS"))return 1;foreach(var obj in owner.Map.Objects)if(obj.Kind==ObjectKind.Spinner && time>=obj.Time-300 && time<=obj.EndTime)return 1;return Math.Max(0,1-owner.Combo/10.0);}
  public double MusicGain {get{return Has("MU")?Math.Max(0,1-owner.Combo/100.0):1;}}
  public bool Automation {get{return Has("AT")||Has("CN");}}
 }
}
