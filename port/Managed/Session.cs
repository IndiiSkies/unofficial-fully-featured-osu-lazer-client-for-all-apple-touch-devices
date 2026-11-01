using System;
using System.Collections.Generic;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Osu.Scoring;
using osu.Game.Rulesets.Scoring;

namespace OsuLegacy
{
    public sealed class GameplayStartGate {
        private uint serial;public bool Armed {get;private set;}
        public void Arm(uint presented){serial=presented;Armed=true;}
        public void Cancel(){Armed=false;}
        public bool TryCommit(uint presented){if(!Armed || unchecked(presented-serial)<2)return false;Armed=false;return true;}
        public static double LeadIn(double firstTime,double requested,double preempt){return Math.Max(0,Math.Max(requested,preempt-firstTime));}
    }
    public sealed class GameplayJudgement
    {
        public double X,Y,Time;
        public string Result;
        public GameplayJudgement(double x,double y,double time,string result){X=x;Y=y;Time=time;Result=result;}
    }
    public sealed class Session
    {
        public sealed class HitTiming {public double Offset,Time;}
        public readonly List<HitTiming> HitTimings=new List<HitTiming>();
        public readonly List<HitTiming> AllHitTimings=new List<HitTiming>();
        public readonly List<GameplayJudgement> ScoreHistory=new List<GameplayJudgement>();
        private sealed class Contact
        {
            public double X,Y,PreviousX,PreviousY,Time,PreviousTime,DownTime,UpTime=double.PositiveInfinity;
            public PathPoint At(double time)
            {
                if(Time<=PreviousTime || time>=Time)return new PathPoint(X,Y);
                double t=Math.Max(0,(time-PreviousTime)/(Time-PreviousTime));
                return new PathPoint(PreviousX+(X-PreviousX)*t,PreviousY+(Y-PreviousY)*t);
            }
        }
        private readonly Dictionary<int,int> modKeys=new Dictionary<int,int>();
        public void ModKey(int phase,int id,int action,double time){if(phase==0){if(!Mods.AcceptAction(action,time))return;modKeys[id]=action;TouchDownCore(id,Mods.AimX,Mods.AimY,time);}else if(phase>=2){modKeys.Remove(id);TouchUp(id,Mods.AimX,Mods.AimY,time);}}
        public bool IsModKey(int id){return modKeys.ContainsKey(id);}
        private readonly Dictionary<int,Contact> contacts=new Dictionary<int,Contact>();
        public readonly Beatmap Map;
        public readonly ModRuntime Mods; private int extraLives=2; public bool ModFailed;
        public bool Failed {get{return ModFailed || (Mania!=null?Mania.Failed:Catch!=null?Catch.HasFailed:Health<=0);}}
        private void CheckMods(){if(Mods.Has("AC") && Great+Ok+Meh+Miss>0 && Accuracy<Mods.Settings.AccuracyTarget)ModFailed=true;if(Health<=0 && Mods.Has("EZ") && Catch==null && Mania==null && !ModFailed && extraLives-->0)Health=1;}
        public readonly BreakTimeline BreakTimeline;
        public readonly CatchSession Catch;
        public readonly ManiaSession Mania;
        public string ScoreKey {get {return Map.SourcePath+(Map.Mode==3?"|mania":Map.Mode==2?"|catch":Map.TaikoConverted?"|taiko":"");}}
        public readonly OsuHitWindows Windows = new OsuHitWindows();
        public readonly double Radius, Preempt;
        public int Great, Ok, Meh, Miss, Combo, MaxCombo, SliderTicksHit, SliderTicksMissed, BonusScore;
        public int Misclicks;
        public double Health=1;
        public readonly List<GameplayJudgement> Judgements=new List<GameplayJudgement>();
        private double clockTime;
        private double lastHealthTime=-1;
        private readonly double lastObjectEnd;
        private Circle strongTarget;
        private double strongTime;
        private bool strongRight;
        public double TaikoGreatWindow {get {return Math.Floor(Map.OD<=5?50-Map.OD*3:35-(Map.OD-5)*3)-.5;}}
        public double TaikoOkWindow {get {return Math.Floor(Map.OD<=5?120-Map.OD*8:80-(Map.OD-5)*6)-.5;}}
        public double TaikoMissWindow {get {return Math.Floor(Map.OD<=5?135-Map.OD*8:95-(Map.OD-5)*5)-.5;}}
        public double TaikoLastTap=-10000;
        public int TaikoLastPad;
        public readonly double[] TaikoPadTimes={-10000,-10000,-10000,-10000};
        public sealed class TaikoEffect {public double Time;public bool Rim,Strong,Great;}
        public readonly List<TaikoEffect> TaikoEffects=new List<TaikoEffect>();
        public bool TaikoMascotFailed;
        public double TaikoClearAt=-10000;
        private void TaikoFeedback(bool rim,bool strong,bool great,double time){
            if(TaikoEffects.Count==8)TaikoEffects.RemoveAt(0);
            TaikoEffects.Add(new TaikoEffect {Rim=rim,Strong=strong,Great=great,Time=time});
        }
        public int SwellRequired(Circle obj){double rate=Map.OD<=5?3+Map.OD*.4:5+(Map.OD-5)*.5;return Math.Max(1,(int)(obj.Duration/1000*rate*1.65));}
        public int FirstPending { get; private set; }
        public string LastResult = "";
        public Action<Circle,string,int> Sound;
        private void Emit(Circle obj,string kind,int edge) { if(Sound!=null)Sound(obj,kind,edge); }
        private void BreakCombo(Circle obj) { if(Combo>=20)Emit(obj,"combobreak",0); Combo=0; }

        public Session(Beatmap map):this(map,false) {}
        public Session(Beatmap map,bool difficultyOnly)
        {
            BreakTimeline=new BreakTimeline(map.Breaks);
            Map = map; Mods=new ModRuntime(map.ModSettings,this); Windows.SetDifficulty(map.OD);
            Radius = (54.4 - 4.48 * map.CS) * 1.00041;
            Preempt = Math.Floor(LegacyDifficulty.DifficultyRange(map.AR, new DifficultyRange(1800, 1200, 450)));
            if(map.Mode==1)Preempt=2000;
            if(difficultyOnly)return;
            foreach (Circle obj in map.Objects)
            {
                lastObjectEnd=Math.Max(lastObjectEnd,obj.EndTime);
                obj.Judged=obj.HeadJudged=obj.HeadHit=obj.HasAngle=obj.TrackBroken=false;
                obj.NextEvent=obj.PartsHit=0;obj.SpinnerPointer=-1;obj.Rotation=obj.VisualRotation=0;obj.LastFollowTime=double.NegativeInfinity;
                obj.LastAngleTime=double.NegativeInfinity;
                foreach(SliderEvent e in obj.Events)e.Judged=e.Hit=false;
            }
            if(map.Mode==2)Catch=new CatchSession(this);
            if(map.Mode==3){Mania=new ManiaSession(this);Mania.ApplyMods(map.ManiaMods);}
        }
        public double Accuracy
        { get {if(Mania!=null)return Mania.Accuracy;if(Catch!=null)return Catch.Accuracy;int count=Great+Ok+Meh+Miss;return count==0?100:(300.0*Great+(Map.Mode==1?150:100)*Ok+50*Meh)/(300*count)*100; } }
        public bool Finished { get {if(Mania!=null)return Mania.Finished;if(Catch!=null)return Catch.Next==Catch.Objects.Count;return Map.Mode==1?FirstPending==Map.Objects.Count:Great+Ok+Meh+Miss==Map.Objects.Count; } }
        public int TotalScore { get {double raw=Mania!=null?Mania.Score:Catch!=null?Catch.Score:Great*300+Ok*(Map.Mode==1?150:100)+Meh*50+SliderTicksHit*10+BonusScore;return (int)Math.Min(int.MaxValue,Math.Round(raw*(Mods.Settings==null?1:Mods.Settings.ScoreFactor)));} }
        public double Progress(double time)
        {
            if(Map.Objects.Count==0)return 1;double start=Map.Objects[0].Time,end=lastObjectEnd;
            return end<=start?1:Math.Max(0,Math.Min(1,(time-start)/(end-start)));
        }
        public int SpinsRequired(Circle spinner)
        {
            double rps=LegacyDifficulty.DifficultyRange(Map.OD,new DifficultyRange(90,150,225))/60;
            return (int)(rps*spinner.Duration/1000+0.0001);
        }
        public double SpinnerProgress(Circle spinner)
        { int required=SpinsRequired(spinner);return required==0?1:spinner.Rotation/(Math.PI*2)/required; }
        public int BonusSpins(Circle spinner)
        {
            double maximumRps=LegacyDifficulty.DifficultyRange(Map.OD,new DifficultyRange(250,380,430))/60;
            int maxBonus=Math.Max(0,(int)(maximumRps*spinner.Duration/1000+0.0001)-SpinsRequired(spinner)-2);
            return Math.Min(maxBonus,Math.Max(0,(int)(spinner.Rotation/(Math.PI*2)+0.0000001)-SpinsRequired(spinner)-2));
        }
        public void Advance(double time)
        {
            if(Mania!=null){Mania.Advance(time);CheckMods();return;}
            if(Catch!=null){Catch.Advance(time);CheckMods();return;}
            clockTime=time;foreach(int id in modKeys.Keys){Contact key;if(contacts.TryGetValue(id,out key))Update(key,Mods.AimX,Mods.AimY,time);}
            if(lastHealthTime>=0 && Map.Objects.Count>0 && time>=Map.Objects[0].Time && time<=Map.Objects[Map.Objects.Count-1].EndTime)
                Health=Math.Max(0,Health-BreakTimeline.DrainDuration(lastHealthTime,time)*.000006);
            lastHealthTime=time;
            if(Map.Mode==1){AdvanceTaiko(time);CheckMods();return;}
            if(Mods.Has("AP") || Mods.Automation){var aim=Mods.AutoAim(time);Mods.AimX=aim.X;Mods.AimY=aim.Y;}
            for (int i=FirstPending;i<Map.Objects.Count;i++)
            {
                Circle obj=Map.Objects[i];if(obj.Time>time)break;if(obj.Judged)continue;
                if(obj.Kind==ObjectKind.Circle)
                { if(Mods.Automation || (Mods.Has("RX") && Math.Pow(Mods.AimX-Mods.Object(obj,time).X,2)+Math.Pow(Mods.AimY-Mods.Object(obj,time).Y,2)<=Radius*Radius))Apply(obj,HitResult.Great,true);else if(time-obj.Time>Windows.WindowFor(HitResult.Meh))Apply(obj,HitResult.Miss,true); }
                else if(obj.Kind==ObjectKind.Slider) AdvanceSlider(obj,time);
                else {if(Mods.Automation || Mods.Has("SO") || (Mods.Has("AP") && contacts.Count>0)){obj.Rotation=SpinsRequired(obj)*Math.PI*2*Math.Min(1,(time-obj.Time)/Math.Max(1,obj.Duration));obj.VisualRotation=obj.Rotation;obj.HasAngle=true;obj.LastAngleTime=time;}if(time>=obj.EndTime)
                {
                    if(Mods.Automation || Mods.Has("SO"))obj.Rotation=SpinsRequired(obj)*Math.PI*2;
                    double progress=SpinnerProgress(obj);
                    BonusScore+=BonusSpins(obj)*1000;
                    Apply(obj,progress+0.0000001>=1?HitResult.Great:progress>0.9?HitResult.Ok:progress>0.75?HitResult.Meh:HitResult.Miss,true);
                }}
            }
        }
        private void AdvanceTaiko(double time)
        {
            for(int i=FirstPending;i<Map.Objects.Count;i++){
                Circle obj=Map.Objects[i];if(obj.Time>time)break;if(obj.Judged)continue;
                if(Mods.Automation){if(obj.Kind==ObjectKind.Circle){Apply(obj,HitResult.Great,true);if((obj.HitSound&4)!=0)BonusScore+=300;TaikoFeedback((obj.HitSound&10)!=0,(obj.HitSound&4)!=0,true,time);}else if(obj.Kind==ObjectKind.Slider){int ticks=(int)(Math.Min(time,obj.EndTime)-obj.Time)/50;int added=Math.Max(0,ticks-obj.PartsHit);obj.PartsHit=ticks;BonusScore+=added*((obj.HitSound&4)!=0?20:10);if(added>0)Emit(obj,"hit",0);if(time>=obj.EndTime)obj.Judged=true;}else if(time>=obj.EndTime){obj.PartsHit=SwellRequired(obj);BonusScore+=obj.PartsHit*10+300;obj.Judged=true;Emit(obj,"hit",0);}continue;}
                if(obj.Kind==ObjectKind.Circle){if(time-obj.Time>TaikoOkWindow)Apply(obj,HitResult.Miss,true);}
                else if(time>=obj.EndTime){
                    if(obj.Kind==ObjectKind.Spinner && obj.PartsHit<SwellRequired(obj)){
                        Health=Math.Max(0,Health-.3);BreakCombo(obj);LastResult="Swell failed";
                        TaikoMascotFailed=true;
                        Judgements.Add(new GameplayJudgement(100,192,time,"X"));TrimJudgements();
                    }
                    obj.Judged=true;
                }
            }
            while(FirstPending<Map.Objects.Count && Map.Objects[FirstPending].Judged)FirstPending++;
        }
        public HitResult TaikoTap(bool rim,bool right,double time)
        {
            if(Map.Mode!=1 || Mods.Automation)return HitResult.None;
            Advance(time);if(!Mods.AcceptAction(right?1:0,time))return HitResult.None;TaikoLastTap=time;TaikoLastPad=right?(rim?3:2):(rim?0:1);
            TaikoPadTimes[TaikoLastPad]=time;
            if(strongTarget!=null && time>=strongTime && time-strongTime<=30 && right!=strongRight && rim==((strongTarget.HitSound&10)!=0)){
                BonusScore+=strongTarget.PartsHit;TaikoFeedback(rim,true,true,time);strongTarget=null;return HitResult.Great;
            }
            strongTarget=null;
            // Judge the earliest normal note first, including notes overlapping rolls.
            for(int i=FirstPending;i<Map.Objects.Count;i++){
                Circle obj=Map.Objects[i];if(obj.Time-time>TaikoMissWindow)break;
                if(obj.Judged || obj.Kind!=ObjectKind.Circle)continue;
                double error=Math.Abs(time-obj.Time);if(error>TaikoMissWindow)continue;
                if(Mods.Has("RX"))rim=(obj.HitSound&10)!=0;HitResult result=rim!=((obj.HitSound&10)!=0) || error>TaikoOkWindow?HitResult.Miss:error<=TaikoGreatWindow?HitResult.Great:HitResult.Ok;
                if(result!=HitResult.Miss){var timing=new HitTiming {Offset=time-obj.Time,Time=time};HitTimings.Add(timing);AllHitTimings.Add(timing);if(HitTimings.Count>32)HitTimings.RemoveAt(0);}
                Apply(obj,result,true);
                if(result!=HitResult.Miss)TaikoFeedback(rim,(obj.HitSound&4)!=0,result==HitResult.Great,time);
                if((obj.HitSound&4)!=0 && result!=HitResult.Miss){strongTarget=obj;strongTime=time;strongRight=right;obj.PartsHit=result==HitResult.Great?300:150;}
                return result;
            }
            for(int i=FirstPending;i<Map.Objects.Count;i++){
                Circle obj=Map.Objects[i];if(obj.Time>time)break;
                if(obj.Judged || obj.Kind==ObjectKind.Circle || time>=obj.EndTime)continue;
                if(obj.Kind==ObjectKind.Slider){if(time-obj.LastAngleTime<50)return HitResult.None;obj.LastAngleTime=time;obj.PartsHit++;BonusScore+=((obj.HitSound&4)!=0?20:10);Emit(obj,"hit",0);TaikoFeedback(rim,false,true,time);return HitResult.Great;}
                if(obj.PartsHit>0 && (obj.HeadHit==rim || time<=obj.LastAngleTime))return HitResult.None;
                obj.HeadHit=rim;obj.LastAngleTime=time;obj.PartsHit++;BonusScore+=10;Emit(obj,"hit",0);
                TaikoFeedback(rim,false,true,time);
                if(obj.PartsHit>=SwellRequired(obj)){BonusScore+=300;obj.Judged=true;TaikoClearAt=time;TaikoMascotFailed=false;while(FirstPending<Map.Objects.Count && Map.Objects[FirstPending].Judged)FirstPending++;}
                return HitResult.Great;
            }
            return HitResult.None;
        }
        public bool Tracking(Circle slider,double time) { return Following(Mods.Position(slider,slider.PositionAt(time),time),time); }
        private bool Following(PathPoint position,double time)
        {
            if(Mods.Automation)return true;
            if(Mods.Has("RX"))return Math.Pow(Mods.AimX-position.X,2)+Math.Pow(Mods.AimY-position.Y,2)<=Radius*Radius*2.4*2.4;
            if(Mods.Has("AP"))return contacts.Count>0;
            foreach(Contact contact in contacts.Values)
            {
                if(time<contact.DownTime || time>=contact.UpTime)continue;
                PathPoint point=contact.At(time),delta=point-position;
                if(delta.X*delta.X+delta.Y*delta.Y<=Radius*Radius*(Mods.Has("ST")?1:2.4*2.4))return true;
            }
            return false;
        }
        private void AdvanceSlider(Circle obj,double time)
        {
            if(!obj.HeadJudged && time>=obj.Time && (Mods.Automation || (Mods.Has("RX") && Math.Pow(Mods.AimX-obj.X,2)+Math.Pow(Mods.AimY-obj.Y,2)<=Radius*Radius))){obj.HeadJudged=true;obj.HeadHit=true;Nested(obj,true);Emit(obj,"hit",0);}
            if(!obj.HeadJudged && time-obj.Time>Windows.WindowFor(HitResult.Meh))
            { obj.HeadJudged=true;Nested(obj,false); }
            if(Mods.Has("ST") && obj.HeadHit && time>obj.Time && time<obj.EndTime && !Following(Mods.Position(obj,obj.PositionAt(time),time),time))obj.TrackBroken=true;
            if(time<=obj.EndTime && Following(Mods.Position(obj,obj.PositionAt(time),time),time))obj.LastFollowTime=time;
            if(obj.Duration==0 && !obj.HeadJudged)return;
            while(obj.NextEvent<obj.Events.Count && obj.Events[obj.NextEvent].Time<=time)
            {
                SliderEvent e=obj.Events[obj.NextEvent++];
                bool hit=Following(Mods.Position(obj,obj.Path.Position(e.Progress),e.Time),e.Time);
                if(e.Tail && obj.LastFollowTime>=Math.Max(obj.Time+obj.Duration/2,obj.EndTime-36))hit=true;
                if(e.Tail && obj.TrackBroken)hit=false;
                e.Judged=true;e.Hit=hit;Nested(obj,hit);
                if(hit || (e.Tail && Mods.Has("CL")))Emit(obj,e.Tail || e.Repeat?"hit":"slidertick",e.Span+1);
            }
            if(time>=obj.EndTime && obj.HeadJudged)
            {
                int total=1+obj.Events.Count;
                Apply(obj,obj.PartsHit==total?HitResult.Great:obj.PartsHit==0?HitResult.Miss:
                    (double)obj.PartsHit/total>=0.5?HitResult.Ok:HitResult.Meh,false);
            }
        }
        private void Nested(Circle obj,bool hit)
        {
            if(hit){obj.PartsHit++;SliderTicksHit++;Combo++;MaxCombo=Math.Max(MaxCombo,Combo);LastResult="Slider";}
            else {SliderTicksMissed++;BreakCombo(obj);LastResult="Slider break";if(Mods.Has("SD") || Mods.Has("PF"))ModFailed=true;}
        }
        public HitResult Tap(double x,double y,double time)
        {
            clockTime=time;
            Advance(time);if(Mods.Automation || Mods.Has("RX"))return HitResult.None;if(Mods.Has("AP")){var aim=Mods.AutoAim(time);x=aim.X;y=aim.Y;}
            for(int i=FirstPending;i<Map.Objects.Count;i++)
            {
                Circle obj=Map.Objects[i];if(obj.Judged || obj.Kind==ObjectKind.Spinner || obj.HeadJudged)continue;
                if(obj.Time-time>OsuHitWindows.MISS_WINDOW)break;
                var state=Mods.Object(obj,time);double dx=x-state.X,dy=y-state.Y;if(dx*dx+dy*dy>Radius*Radius*state.Scale*state.Scale){if(Mods.Has("CL"))break;continue;}
                HitResult result=Windows.ResultFor(time-obj.Time);if(result==HitResult.None)continue;
                if(result!=HitResult.Miss){var timing=new HitTiming {Offset=time-obj.Time,Time=time};HitTimings.Add(timing);AllHitTimings.Add(timing);if(HitTimings.Count>32)HitTimings.RemoveAt(0);}
                if(obj.Kind==ObjectKind.Slider)
                {
                    obj.HeadJudged=true;obj.HeadHit=result!=HitResult.Miss;
                    Nested(obj,obj.HeadHit);if(obj.HeadHit)Emit(obj,"hit",0);AdvanceSlider(obj,time);
                }
                else Apply(obj,result,true);
                return result;
            }
            return HitResult.None;
        }
        public HitResult TouchDown(int id,double x,double y,double time)
        {
            Mods.AimX=x;Mods.AimY=y;return TouchDownCore(id,x,y,time);
        }
        private HitResult TouchDownCore(int id,double x,double y,double time){
            // Resolve older checkpoints first; an exact-time checkpoint sees
            // the newly pressed contact when Tap advances to the event time.
            Advance(time-0.000001);
            contacts[id]=new Contact { X=x,Y=y,PreviousX=x,PreviousY=y,Time=time,PreviousTime=time,DownTime=time };
            TrackSpinners(id,x,y,time,true);
            HitResult result=Tap(x,y,time);
            if(result==HitResult.None && !TrackingObject(time))Misclick(x,y,time);
            return result;
        }
        private bool TrackingObject(double time)
        {
            for(int i=FirstPending;i<Map.Objects.Count;i++) {
                Circle obj=Map.Objects[i];if(obj.Time>time)break;
                if(!obj.Judged && (obj.Kind==ObjectKind.Spinner || (obj.Kind==ObjectKind.Slider && time<=obj.EndTime)))return true;
            }
            return false;
        }
        private void Misclick(double x,double y,double time)
        {
            Misclicks++;Health=Math.Max(0,Health-.025);Judgements.Add(new GameplayJudgement(x,y,time,"X"));TrimJudgements();
        }
        private void TrimJudgements(){while(Judgements.Count>24)Judgements.RemoveAt(0);}
        public void TouchMove(int id,double x,double y,double time)
        {
            Mods.AimX=x;Mods.AimY=y;Contact contact;if(!contacts.TryGetValue(id,out contact))return;
            Update(contact,x,y,time);TrackSpinners(id,x,y,time,false);Advance(time);
        }
        public void TouchUp(int id,double x,double y,double time)
        {
            Mods.AimX=x;Mods.AimY=y;Contact contact;if(!contacts.TryGetValue(id,out contact))return;
            Update(contact,x,y,time);contact.UpTime=time;TrackSpinners(id,x,y,time,false);Advance(time);contacts.Remove(id);
            foreach(Circle obj in Map.Objects)if(obj.SpinnerPointer==id){obj.SpinnerPointer=-1;obj.HasAngle=false;}
        }
        public void ReleaseAll(double time)
        {
            if(Mania!=null){Mania.Suspend(time);return;}
            if(Catch!=null){Catch.Advance(time);Catch.Release();return;}
            foreach(Contact contact in contacts.Values)contact.UpTime=time;
            Advance(time);contacts.Clear();modKeys.Clear();
            foreach(Circle obj in Map.Objects){obj.HasAngle=false;obj.SpinnerPointer=-1;}
        }
        private static void Update(Contact contact,double x,double y,double time)
        {
            contact.PreviousX=contact.X;contact.PreviousY=contact.Y;contact.PreviousTime=contact.Time;
            contact.X=x;contact.Y=y;contact.Time=time;
        }
        private void TrackSpinners(int id,double x,double y,double time,bool began)
        {
            for(int i=FirstPending;i<Map.Objects.Count;i++)
            {
                Circle obj=Map.Objects[i];if(obj.Time>time)break;
                if(obj.Kind!=ObjectKind.Spinner || obj.Judged || time>=obj.EndTime)continue;
                double dx=x-256,dy=y-192;
                if(dx*dx+dy*dy<32*32){if(obj.SpinnerPointer==id)obj.HasAngle=false;continue;}
                if(obj.SpinnerPointer==-1)obj.SpinnerPointer=id;
                if(obj.SpinnerPointer!=id)continue;
                double angle=Math.Atan2(dy,dx);
                if(obj.HasAngle && !began && time>=obj.LastAngleTime)
                {
                    double delta=angle-obj.LastAngle;
                    if(delta>Math.PI)delta-=2*Math.PI;if(delta<-Math.PI)delta+=2*Math.PI;
                    int previousBonus=BonusSpins(obj);
                    obj.Rotation+=Math.Abs(delta);obj.VisualRotation+=delta;
                    if(BonusSpins(obj)>previousBonus)Emit(obj,"spinnerbonus",0);
                }
                obj.LastAngle=angle;obj.LastAngleTime=time;obj.HasAngle=true;
            }
        }
        public int VisibleEnd(double time)
        {
            int low=FirstPending,high=Map.Objects.Count;
            double cutoff=Map.Mode==1?Map.TaikoScrollPosition(time)+2000:time+Preempt;
            while(low<high){int middle=low+(high-low)/2;double position=Map.Mode==1?Map.TaikoScrollPosition(Map.Objects[middle].Time):Map.Objects[middle].Time;if(position<=cutoff)low=middle+1;else high=middle;}
            if(Mods.Has("FR"))while(low<Map.Objects.Count && Map.Objects[low].ModComboStart<=time+Preempt)low++;return low;
        }
        private void Apply(Circle obj,HitResult result,bool combo)
        {
            obj.Judged=true;
            while(FirstPending<Map.Objects.Count && Map.Objects[FirstPending].Judged)FirstPending++;
            if(result==HitResult.Great)Great++;else if(result==HitResult.Ok)Ok++;else if(result==HitResult.Meh)Meh++;else Miss++;
            if(result==HitResult.Great)Health=Math.Min(1,Health+.035);else if(result==HitResult.Ok)Health=Math.Min(1,Health+.015);else if(result==HitResult.Meh)Health=Math.Max(0,Health-.025);else Health=Math.Max(0,Health-.12);
            if(combo){if(result==HitResult.Miss)BreakCombo(obj);else {Combo++;Emit(obj,"hit",obj.Kind==ObjectKind.Spinner?1:0);}MaxCombo=Math.Max(MaxCombo,Combo);}
            if(Map.Mode==1){TaikoMascotFailed=result==HitResult.Miss;if(result!=HitResult.Miss && Combo>0 && Combo%50==0)TaikoClearAt=clockTime;}
            if((Mods.Has("SD") && result==HitResult.Miss) || (Mods.Has("PF") && result!=HitResult.Great))ModFailed=true;CheckMods();
            LastResult=result.ToString();
            var judgement=new GameplayJudgement(obj.Kind==ObjectKind.Spinner?256:obj.X,obj.Kind==ObjectKind.Spinner?192:obj.Y,clockTime,result==HitResult.Great?"300":result==HitResult.Ok?(Map.Mode==1?"150":"100"):result==HitResult.Meh?"50":"X");
            Judgements.Add(judgement);ScoreHistory.Add(judgement);TrimJudgements();
        }
    }
}
