using System;
using System.Collections.Generic;

namespace OsuLegacy
{
    // Backport of CatchBeatmapConverter, JuiceStream, BananaShower and the
    // normal (non-HR) CatchBeatmapProcessor. See ThirdParty/catch-provenance.md.
    public enum CatchKind { Fruit, Droplet, Tiny, Banana }
    public sealed class CatchObject
    {
        public double Time,X,Progress,StackX,StackY,JudgedAt,HyperDistance;
        public CatchKind Kind;
        public Circle Source;
        public int Index,Node,Order,HyperTarget=-1;
        public bool Judged,Caught,LastInCombo;
        private bool visualReady;
        public float VisualA,VisualB,VisualC;public int VisualVariant;
        public void PrepareVisual(){if(visualReady)return;VisualA=CatchVisualRandom.Single((int)Time,1);VisualB=CatchVisualRandom.Single((int)Time,2);VisualC=CatchVisualRandom.Single((int)Time,3);VisualVariant=(int)(CatchVisualRandom.Value((int)Time,0)%3);visualReady=true;}
    }
    public sealed class CatchRandom
    {
        private uint x,y=842502087,z=3579807591,w=273326509;
        public CatchRandom(int seed){x=(uint)seed;}
        public int Next(){uint t=x^(x<<11);x=y;y=z;z=w;w=w^(w>>19)^t^(t>>8);return (int)(w&0x7fffffff);}
        public double NextDouble(){return Next()/2147483648.0;}
        public int Next(int min,int max){return (int)(min+NextDouble()*(max-min));}
        public int Next(double min,double max){return (int)(min+NextDouble()*(max-min));}
        private uint bitBuffer;private int bitIndex=32;
        public bool NextBool(){if(bitIndex==32){uint t=x^(x<<11);x=y;y=z;z=w;w=w^(w>>19)^t^(t>>8);bitBuffer=w;bitIndex=1;return (bitBuffer&1)==1;}bitIndex++;return ((bitBuffer>>=1)&1)==1;}
    }
    public static class CatchVisualRandom
    {
        public static ulong Value(int seed,int series){unchecked{ulong x=(((ulong)(uint)series<<32)|(uint)seed)^0x12345678;x^=x>>33;x*=0xff51afd7ed558ccd;x^=x>>33;x*=0xc4ceb9fe1a85ec53;x^=x>>33;return x;}}
        public static float Single(int seed,int series){return (float)(Value(seed,series)&((1<<24)-1))/(1<<24);}
    }
    public sealed class CatchSession
    {
        public readonly List<CatchObject> Objects=new List<CatchObject>();
        public readonly Session Owner;
        public readonly double Scale,CatchWidth,Preempt;
        public double X=256,Time,HyperMultiplier=1,HyperTarget=-1;
        public bool Dashing,FailedState;
        public int Direction,Facing=1,Next,TinyMiss,BananaHit,BananaMiss,FruitMiss,DropletMiss;
        public int FruitTotal,DropletTotal,TinyTotal,PossibleCombo;
        public double DrainRate,HpMultiplier=1;
        public int Lives=2;
        public bool HasFailed;
        private double comboPortion,maximumComboPortion;
        private bool started;
        private double lastHyperAt=double.NegativeInfinity;
        private readonly Dictionary<int,int> touches=new Dictionary<int,int>();
        public readonly List<CatchObject> Plate=new List<CatchObject>();
        public readonly List<CatchObject> Recent=new List<CatchObject>();
        public sealed class Burst {public CatchObject Object;public double Time,X,Y;public bool Explode;}
        public readonly List<Burst> Bursts=new List<Burst>();
        private readonly CatchRandom stackRandom=new CatchRandom(1337);
        private readonly List<double[]> drainGaps=new List<double[]>();
        public CatchSession(Session owner):this(owner,false){}
        public CatchSession(Session owner,bool difficultyOnly){
            Owner=owner;Scale=(1-.7*((owner.Map.CS-5)/5.0))/2;
            CatchWidth=106.75*(Scale*2)*.8;Preempt=owner.Preempt;
            Build();PrepareHyper();if(difficultyOnly)return;PrepareHealth();
            int combo=0;foreach(CatchObject obj in Objects){if(obj.Kind==CatchKind.Fruit || obj.Kind==CatchKind.Droplet){combo++;maximumComboPortion+=ComboValue(obj.Kind,combo);}}
        }
        private static double Clamp(double x,double min,double max){return Math.Max(min,Math.Min(max,x));}
        private void Add(Circle source,int index,CatchKind kind,double time,double progress,int node){
            double x=source.Kind==ObjectKind.Slider?source.Path.Position(progress).X:source.X;
            Objects.Add(new CatchObject {Source=source,Index=index,Kind=kind,Time=time,X=x,Progress=progress,Node=node,Order=Objects.Count});
            if(Objects.Count>1000000)throw new FormatException("Catch beatmap has too many nested objects");
        }
        private sealed class Event {public double Time,Progress;public int Type,Node;}
        private sealed class ByTime : IComparer<CatchObject>{public int Compare(CatchObject a,CatchObject b){return a.Time.CompareTo(b.Time);}}
        private List<Event> SliderEvents(Circle obj){
            var events=new List<Event>();events.Add(new Event {Time=obj.Time,Progress=0,Type=1});
            double length=Math.Min(100000,obj.Path.Length),distance=Clamp(obj.TickDistance,0,length);int node=1;
            for(int span=0;span<obj.SpanCount;span++){
                var ticks=new List<Event>();
                if(distance>0 && !double.IsInfinity(distance))for(double d=distance;d<=length;d+=distance){
                    if(d>=length-obj.Velocity*10)break;
                    double progress=d/length;ticks.Add(new Event {Time=obj.Time+(span+(span%2==0?progress:1-progress))*obj.SpanDuration,Progress=progress});
                    if(ticks.Count>100000)throw new FormatException("Too many catch droplets");
                }
                if(span%2==1)ticks.Reverse();events.AddRange(ticks);
                if(span<obj.SpanCount-1)events.Add(new Event {Time=obj.Time+(span+1)*obj.SpanDuration,Progress=(span+1)%2,Type=1,Node=node++});
            }
            double finalStart=obj.Time+(obj.SpanCount-1)*obj.SpanDuration;
            double last=Math.Max(obj.Time+obj.Duration/2,finalStart+obj.SpanDuration-36);
            double lastProgress=obj.SpanDuration<=0?obj.SpanCount%2:(last-finalStart)/obj.SpanDuration;
            if(obj.SpanCount%2==0)lastProgress=1-lastProgress;
            events.Add(new Event {Time=last,Progress=lastProgress,Type=2});
            events.Add(new Event {Time=obj.EndTime,Progress=obj.SpanCount%2,Type=1,Node=node});return events;
        }
        private void Build(){
            var rng=new CatchRandom(1337);Beatmap map=Owner.Map;
            float? lastPosition=null;double lastTime=0;
            for(int i=0;i<map.Objects.Count;i++){
                if((i&31)==0 && Beatmap.LoadCheckpoint!=null)Beatmap.LoadCheckpoint();Circle obj=map.Objects[i];int before=Objects.Count;
                if(obj.Kind==ObjectKind.Circle){Add(obj,i,CatchKind.Fruit,obj.Time,0,0);if(map.CatchHardRock)Objects[Objects.Count-1].X=HardRockPosition((float)obj.X,obj.Time,ref lastPosition,ref lastTime,rng);}
                else if(obj.Kind==ObjectKind.Spinner){
                    float spacing=(float)obj.Duration;while(spacing>100)spacing/=2;
                    for(float t=(int)obj.Time;t<=(int)obj.EndTime;t+=spacing){
                        Add(obj,i,CatchKind.Banana,t,0,Objects.Count-before);Objects[Objects.Count-1].X=(float)(rng.NextDouble()*512);rng.Next();rng.Next();rng.Next();
                    }
                }else{
                    Event previous=null;
                    foreach(Event e in SliderEvents(obj)){
                        if(previous!=null){double gap=(int)e.Time-(int)previous.Time;if(gap>80){
                            double spacing=gap;while(spacing>100)spacing/=2;
                            for(double t=spacing;t<gap;t+=spacing){
                                Add(obj,i,CatchKind.Tiny,previous.Time+t,previous.Progress+t/gap*(e.Progress-previous.Progress),0);
                            }
                        }}
                        previous=e;
                        if(e.Type!=2)Add(obj,i,e.Type==1?CatchKind.Fruit:CatchKind.Droplet,e.Time,e.Progress,e.Node);
                    }
                    Objects.Sort(before,Objects.Count-before,new ByTime());
                    // HitObject.ApplyDefaults sorts nested objects before offsets.
                    for(int n=before;n<Objects.Count;n++){
                        CatchObject item=Objects[n];if(item.Kind==CatchKind.Tiny)item.X+=Clamp(rng.Next(-20,20),-item.X,512-item.X);
                        else if(item.Kind==CatchKind.Droplet)rng.Next();
                    }
                    string[] points=obj.PathDefinition==null?new string[0]:obj.PathDefinition.Split('|');
                    lastPosition=points.Length>1?(float)Beatmap.Number(points[points.Length-1].Split(':')[0]):(float)obj.X;lastTime=obj.Time;
                }
                bool last=i+1==map.Objects.Count || map.Objects[i+1].NewCombo || obj.Kind==ObjectKind.Spinner || map.Objects[i+1].Kind==ObjectKind.Spinner;
                if(last && Objects.Count>before)Objects[Objects.Count-1].LastInCombo=true;
            }
            Objects.Sort(delegate(CatchObject a,CatchObject b){int order=a.Time.CompareTo(b.Time);return order!=0?order:a.Order.CompareTo(b.Order);});
            foreach(CatchObject obj in Objects){obj.X=Clamp(obj.X,0,512);if(obj.Kind==CatchKind.Fruit)FruitTotal++;else if(obj.Kind==CatchKind.Droplet)DropletTotal++;else if(obj.Kind==CatchKind.Tiny)TinyTotal++;}
            PossibleCombo=FruitTotal+DropletTotal;
        }
        private static float HardRockPosition(float position,double time,ref float? lastPosition,ref double lastTime,CatchRandom rng){
            if(lastPosition==null || lastPosition==0){lastPosition=position;lastTime=time;return position;}
            float difference=position-lastPosition.Value;int elapsed=(int)(time-lastTime);
            if(elapsed>1000){lastPosition=position;lastTime=time;return position;}
            if(difference==0){bool right=rng.NextBool();float offset=Math.Min(20,(float)rng.Next(0d,Math.Max(0,elapsed/4d)));if(right)position+=position+offset<=512?offset:-offset;else position+=position-offset>=0?-offset:offset;return position;}
            if(Math.Abs(difference)<elapsed/3){if(difference>0){if(position+difference<512)position+=difference;}else if(position+difference>0)position+=difference;}
            lastPosition=position;lastTime=time;return position;
        }
        private void PrepareHyper(){
            var palpable=new List<int>();for(int i=0;i<Objects.Count;i++)if(Objects[i].Kind==CatchKind.Fruit || Objects[i].Kind==CatchKind.Droplet)palpable.Add(i);
            double half=CatchWidth/2/.8,lastExcess=half;int lastDirection=0;
            for(int i=0;i<palpable.Count-1;i++){
                CatchObject current=Objects[palpable[i]],next=Objects[palpable[i+1]];int direction=next.X>current.X?1:-1;
                double time=(int)next.Time-(int)current.Time-1000f/60f/4;
                double distance=Math.Abs(next.X-current.X)-(lastDirection==direction?lastExcess:half);
                float hyperDistance=(float)(time-distance);current.HyperDistance=hyperDistance;
                if(hyperDistance<0){current.HyperTarget=palpable[i+1];lastExcess=half;}else lastExcess=Clamp(hyperDistance,0,half);
                lastDirection=direction;
            }
        }
        private static double Range(double od,double min,double mid,double max){return od<=5?min+(mid-min)*od/5:mid+(max-mid)*(od-5)/5;}
        private double Gain(CatchKind kind){return kind==CatchKind.Fruit?.03:kind==CatchKind.Droplet?.015:kind==CatchKind.Tiny?.0015:.0025;}
        private void PrepareHealth(){
            foreach(double[] period in Owner.Map.Breaks){double start=period[0],end=period[1];foreach(CatchObject o in Objects){if(o.Kind==CatchKind.Tiny)continue;if(o.Time<=period[0])start=o.Time;if(o.Time>=period[1]){end=o.Time;break;}}drainGaps.Add(new double[]{start,end});}
            double drop=.00025,lowest=Range(Owner.Map.HP,.975,.8,.3),endTarget=Range(Owner.Map.HP,.99,.9,.4),recovery=Range(Owner.Map.HP,.04,.02,0);
            for(int iteration=0;iteration<10000;iteration++){
                double hp=1,uncapped=1,last=Objects.Count==0?0:Objects[0].Time;bool fail=false;int count=0;
                foreach(CatchObject obj in Objects){count++;double loss=drop*DrainDuration(last,obj.Time);hp=Math.Max(0,hp-loss);uncapped=Math.Max(0,uncapped-loss);last=obj.Time;
                    if(hp<=lowest){fail=true;drop*=.96;break;}double gain=Gain(obj.Kind)*HpMultiplier;hp=Math.Min(1,hp+gain);uncapped+=gain;
                }
                if(!fail && hp<endTarget){fail=true;drop*=.94;HpMultiplier*=1.01;}
                if(!fail && (uncapped-1)/Math.Max(1,count)<recovery){fail=true;drop*=.96;HpMultiplier*=1.01;}
                if(!fail){DrainRate=drop;return;}
            }
            throw new FormatException("Catch health calibration did not converge");
        }
        private double DrainDuration(double from,double to){double duration=Math.Max(0,to-from);foreach(double[] gap in drainGaps)duration-=Math.Max(0,Math.Min(to,gap[1])-Math.Max(from,gap[0]));return Math.Max(0,duration);}
        private static double ComboValue(CatchKind kind,int combo){return (kind==CatchKind.Fruit?300:100)*Math.Min(Math.Max(.5,Math.Log(Math.Max(1,combo),4)),Math.Log(200,4));}
        public double Accuracy {get {int count=Owner.Great+Owner.Ok+Owner.Meh+Owner.Miss+TinyMiss;return count==0?100:(Owner.Great+Owner.Ok+Owner.Meh)*100.0/count;}}
        public int Score {get {
            double scale=TinyTotal+FruitTotal==0?0:(double)TinyTotal/(TinyTotal+FruitTotal);
            double comboMaximum=1000000-400000+400000*(1-scale),tinyMaximum=400000*scale;
            double tinyProgress=TinyTotal==0?0:(double)Owner.Meh/TinyTotal;
            double withoutMods=Math.Round(comboMaximum*(maximumComboPortion>0?comboPortion/maximumComboPortion:1)+tinyMaximum*tinyProgress+BananaHit*200);
            return (int)Math.Round(withoutMods*Owner.Map.CatchScoreMultiplier);
        }}
        public void Touch(int phase,int id,double x,double y,double time){
            if(Owner.Mods.Has("RX")){Advance(time);if(phase<2)X=Clamp((x-88.2962962962963)/.5925925925925926,0,512);Direction=0;Dashing=false;return;}
            Advance(time);if(phase>=2)touches.Remove(id);else {int action=x<72?(y<176? -2:-1):x>=408?(y<176?2:1):0;if(action==0)touches.Remove(id);else touches[id]=action;}
            bool left=false,right=false,dash=false;foreach(int action in touches.Values){left|=action<0;right|=action>0;dash|=Math.Abs(action)==2;}
            Direction=(right?1:0)-(left?1:0);Dashing=dash;
        }
        public void Release(){touches.Clear();Direction=0;Dashing=false;}
        private void MoveTo(double time){
            double elapsed=Math.Max(0,time-Time),old=X;X=Clamp(X+Direction*(Dashing?1:.5)*HyperMultiplier*elapsed,0,512);
            if(HyperTarget>=0 && ((old<=HyperTarget && X>=HyperTarget) || (old>=HyperTarget && X<=HyperTarget))){X=HyperTarget;HyperTarget=-1;HyperMultiplier=1;}
            if(X!=old)Facing=X>old?1:-1;
            if(Objects.Count>0){double from=Math.Max(Time,Objects[0].Time),to=Math.Min(time,Objects[Objects.Count-1].Time),before=Owner.Health;Owner.Health=Math.Max(0,Owner.Health-DrainRate*DrainDuration(from,to));if(before>0 && Owner.Health<=0)HasFailed=true;UseExtraLife();}
            Time=time;
        }
        public void Advance(double time){
            if(!started){Time=Math.Min(time,Objects.Count==0?time:Objects[0].Time);started=true;}
            if(time<Time)return;
            MoveTo(time);
            // CatcherArea.Update moves once before HitObjectContainer judges
            // this frame's due objects. Preserve that ordering and hyper guard.
            while(Next<Objects.Count && Objects[Next].Time<=time){CatchObject obj=Objects[Next++];if(Owner.Mods.Automation)X=obj.X;Judge(obj);}
            Recent.RemoveAll(delegate(CatchObject obj){return obj.JudgedAt<time-750;});Bursts.RemoveAll(delegate(Burst burst){return burst.Time<time-750;});
        }
        private void Judge(CatchObject obj){
            bool caught=obj.X>=X-CatchWidth/2 && obj.X<=X+CatchWidth/2;obj.Judged=true;obj.Caught=caught;obj.JudgedAt=Time;
            if(obj.Kind==CatchKind.Banana){if(caught){BananaHit++;Owner.BonusScore+=200;}else BananaMiss++;}
            else if(obj.Kind==CatchKind.Tiny){if(caught)Owner.Meh++;else TinyMiss++;}
            else {
                if(caught){Owner.Combo++;Owner.MaxCombo=Math.Max(Owner.MaxCombo,Owner.Combo);if(obj.Kind==CatchKind.Fruit)Owner.Great++;else Owner.Ok++;comboPortion+=ComboValue(obj.Kind,Owner.Combo);}
                else{if(Owner.Combo>=20 && Owner.Sound!=null)Owner.Sound(obj.Source,"combobreak",0);Owner.Combo=0;Owner.Miss++;if(obj.Kind==CatchKind.Fruit)FruitMiss++;else DropletMiss++;}
            }
            if(caught || obj.Kind!=CatchKind.Banana)FailedState=!caught;
            if(caught){Owner.Health=Math.Min(1,Owner.Health+Gain(obj.Kind)*HpMultiplier);obj.Source.LastFollowTime=obj.Time;if(obj.Kind!=CatchKind.Tiny && Owner.Sound!=null)Owner.Sound(obj.Source,obj.Kind==CatchKind.Droplet?"catch-tick":obj.Kind==CatchKind.Banana?"catch-banana":"hit",obj.Node);}
            else if(obj.Kind==CatchKind.Fruit || obj.Kind==CatchKind.Droplet)Owner.Health=Math.Max(0,Owner.Health+Range(Owner.Map.HP,-.03,-.125,-.2));
            if(!caught && (obj.Kind==CatchKind.Fruit || obj.Kind==CatchKind.Droplet) && Owner.Health<=0)HasFailed=true;
            if(!caught && (Owner.Map.CatchPerfect || (Owner.Map.CatchSuddenDeath && (obj.Kind==CatchKind.Fruit || obj.Kind==CatchKind.Droplet)))){Owner.Health=0;HasFailed=true;}
            UseExtraLife();
            if(obj.Kind!=CatchKind.Banana){var judgement=new GameplayJudgement(obj.X,384,Time,caught?"300":"X");Owner.Judgements.Add(judgement);Owner.ScoreHistory.Add(judgement);while(Owner.Judgements.Count>32)Owner.Judgements.RemoveAt(0);}
            Owner.LastResult=caught?"Caught":"Miss";
            if(obj.Kind!=CatchKind.Tiny && lastHyperAt!=Time){
                if(caught && obj.HyperTarget>=0){CatchObject target=Objects[obj.HyperTarget];HyperMultiplier=Math.Max(1,Math.Abs((target.X-X)/Math.Max(1,target.Time-obj.Time-1000.0/60)));HyperTarget=HyperMultiplier>1?target.X:-1;if(HyperMultiplier>1)lastHyperAt=Time;}
                else {HyperMultiplier=1;HyperTarget=-1;}
            }
            if(caught && (obj.Kind==CatchKind.Fruit || obj.Kind==CatchKind.Banana) && !Owner.Map.CatchHidden){
                obj.StackX=obj.X-X;obj.StackY=0;double distance=128*Scale*10/64;
                for(int attempt=0;attempt<1000;attempt++){
                    bool collision=false;foreach(CatchObject old in Plate)if(Math.Pow(old.StackX-obj.StackX,2)+Math.Pow(old.StackY-obj.StackY,2)<distance*distance){collision=true;break;}
                    if(!collision)break;obj.StackX+=(stackRandom.NextDouble()*2-1)*distance;obj.StackY-=stackRandom.NextDouble()*5;
                }
                Plate.Add(obj);
            }
            if(obj.LastInCombo){foreach(CatchObject old in Plate)Bursts.Add(new Burst {Object=old,Time=Time,X=X+old.StackX,Y=old.StackY,Explode=caught || obj.Kind==CatchKind.Banana});Plate.Clear();}
            Recent.Add(obj);
        }
        private void UseExtraLife(){if(HasFailed && Owner.Map.CatchEasy && Lives>0){Lives--;Owner.Health=1;HasFailed=false;}}
    }
}
