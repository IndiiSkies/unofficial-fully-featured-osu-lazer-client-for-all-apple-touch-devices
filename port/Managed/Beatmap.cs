using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace OsuLegacy
{
    public enum ObjectKind { Circle, Slider, Spinner, Hold }
    public sealed class SliderEvent
    {
        public double Time, Progress;
        public int Span;
        public bool Tail, Repeat, Judged, Hit;
    }
    // Retain the original class/list API while representing all standard objects.
    public sealed class Circle
    {
        public double X, Y, Time, Duration;
        public ObjectKind Kind;
        public bool Judged, HeadJudged, HeadHit,TrackBroken;
        public bool NewCombo;
        public int ComboNumber=1,ComboColourIndex,ComboSkip;
        public HitSample Sample = new HitSample();
        public int HitSound;
        public int Column,ModIndex; public double ModComboStart;
        public bool ManiaTailSound;
        public int[] EdgeSounds;
        public HitSample[] EdgeSamples;
        public int SpanCount = 1, NextEvent, PartsHit, SpinnerPointer = -1;
        public double SpanDuration, Velocity, TickDistance, LastFollowTime, Rotation, VisualRotation, LastAngle, LastAngleTime;
        public bool HasAngle;
        public SliderPath Path;
        public readonly List<SliderEvent> Events = new List<SliderEvent>();
        internal string PathDefinition;
        internal double ExpectedLength;
        internal int SourceIndex;
        public double EndTime { get { return Time + Duration; } }
        public PathPoint PositionAt(double time)
        {
            if (Kind != ObjectKind.Slider) return new PathPoint(X,Y);
            if (SpanDuration <= 0) return Path.Position(SpanCount % 2);
            double elapsed = Math.Max(0, Math.Min(Duration,time-Time));
            int span = Math.Min(SpanCount-1,(int)(elapsed/SpanDuration));
            double progress = (elapsed-span*SpanDuration)/SpanDuration;
            return Path.Position(span%2==0 ? progress : 1-progress);
        }
    }

    public sealed class Beatmap
    {
        [ThreadStatic] public static Action LoadCheckpoint;
        public float StackLeniency=.7f;
        private sealed class TimingPoint { public double Time, BeatLength; public bool Red, Ticks,Kiai; public int Index; }
        public string Title = "Untitled", Artist="Unknown artist", Creator="Unknown mapper", Difficulty="Normal", AudioPath, BackgroundPath, VideoPath, SourcePath;
        public double VideoOffset;
        public string Source="";
        public double AudioLeadIn;
        public ModSettings ModSettings;
        public double BeatLengthAt(double time){double length=500;foreach(var point in timing){if(point.Time>time)break;if(point.Red)length=point.BeatLength;}return length;}
        public double HP=5;
        public bool CatchHardRock,CatchHidden,CatchEasy,CatchSuddenDeath,CatchPerfect;
        public double CatchScoreMultiplier=1;
        public void ApplyCatchMods(string summary){
            string mods=","+summary+",";
            if(mods.Contains(",HR,")){CatchHardRock=true;CS=Math.Min(10,(float)CS*1.3f);AR=Math.Min(10,(float)AR*1.4f);OD=Math.Min(10,(float)OD*1.4f);HP=Math.Min(10,(float)HP*1.4f);CatchScoreMultiplier*=1.12;}
            if(mods.Contains(",EZ,")){CatchEasy=true;CS*=.5;AR*=.5;OD*=.5;HP*=.5;CatchScoreMultiplier*=.5;}
            if(mods.Contains(",HD,")){CatchHidden=true;CatchScoreMultiplier*=1.06;}
            if(mods.Contains(",NF,"))CatchScoreMultiplier*=.5;
            CatchSuddenDeath=mods.Contains(",SD,");CatchPerfect=mods.Contains(",PF,");
        }
        public readonly List<double[]> Breaks=new List<double[]>();
        public readonly List<double[]> ComboColours=new List<double[]>();
        public int DefaultSampleSet = 1, DefaultSampleVolume = 100;
        public readonly List<SamplePoint> SamplePoints = new List<SamplePoint>();
        public double OD = 5, CS = 5, AR = 5, SliderMultiplier = 1.4, SliderTickRate = 1;
        public int Version = 14;
        public int Mode;
        public bool TaikoConverted;
        public bool ManiaConverted;
        public string ManiaMods="None";
        public int ManiaColumns=4;
        public List<double[]> ExportTiming(){var result=new List<double[]>();foreach(TimingPoint p in timing)result.Add(new double[]{p.Time,p.BeatLength,p.Red?1:0,p.Kiai?1:0});return result;}
        private readonly List<double[]> taikoScroll=new List<double[]>();
        public double TaikoScrollPosition(double time){
            int low=0,high=taikoScroll.Count-1,index=-1;
            while(low<=high){int mid=(low+high)/2;if(taikoScroll[mid][0]<=time){index=mid;low=mid+1;}else high=mid-1;}
            if(index<0)return time;double[] point=taikoScroll[index];return point[2]+(time-point[0])*point[1];
        }
        // Source: TaikoBeatmapConverter.shouldConvertSliderToHits. Keep the
        // separate multiplications and integer duration for legacy precision.
        public void ConvertToTaiko()
        {
            if(Mode!=0)return;
            var converted=new List<Circle>();
            foreach(Circle obj in Objects){
                if(obj.Kind!=ObjectKind.Slider){converted.Add(obj);continue;}
                double beat=500,sv=1;
                foreach(TimingPoint p in timing)if(p.Red){beat=p.BeatLength;break;}
                foreach(TimingPoint p in timing){if(p.Time>obj.Time)break;if(p.Red){beat=p.BeatLength;sv=1;}else sv=p.BeatLength<0?Math.Max(.1,Math.Min(10,-100/p.BeatLength)):1;}
                if(taikoScroll.Count==0 || taikoScroll[taikoScroll.Count-1][1]!=sv){double position=TaikoScrollPosition(obj.Time);taikoScroll.Add(new double[]{obj.Time,sv,position});}
                double adjusted=beat*(Math.Max(10,Math.Min(10000,(float)(100/sv)))/100.0);
                double distance=obj.ExpectedLength;distance*=1.4f;distance*=obj.SpanCount;
                double scoringDistance=100*(SliderMultiplier*1.4f)/SliderTickRate;
                double velocity=scoringDistance*SliderTickRate;
                int duration=(int)(distance/velocity*adjusted);
                double osuVelocity=velocity*(1000f/adjusted);
                double conversionBeat=Version>=8?beat:adjusted;
                double spacing=Math.Min(conversionBeat/SliderTickRate,(double)duration/obj.SpanCount);
                if(spacing>0 && distance/osuVelocity*1000<2*conversionBeat){
                    int edge=0;
                    for(double t=obj.Time;t<=obj.Time+duration+spacing/8;t+=spacing){
                        int count=obj.EdgeSounds==null?0:obj.EdgeSounds.Length;
                        converted.Add(new Circle {Kind=ObjectKind.Circle,Time=t,X=obj.X,Y=obj.Y,HitSound=count==0?obj.HitSound:obj.EdgeSounds[edge%count],Sample=obj.EdgeSamples==null || obj.EdgeSamples.Length==0?obj.Sample:obj.EdgeSamples[edge%obj.EdgeSamples.Length],SourceIndex=obj.SourceIndex});
                        edge++;if(converted.Count>200000)throw new FormatException("Too many converted taiko hits");
                        if(spacing<1e-7)break;
                    }
                }else {obj.Duration=duration;obj.Events.Clear();converted.Add(obj);}
            }
            converted.Sort(delegate(Circle a,Circle b){int order=a.Time.CompareTo(b.Time);return order!=0?order:a.SourceIndex.CompareTo(b.SourceIndex);});
            Circles.Clear();Circles.AddRange(converted);Mode=1;TaikoConverted=true;
        }
        public readonly List<Circle> Circles = new List<Circle>();
        public List<Circle> Objects { get { return Circles; } }
        private readonly List<TimingPoint> timing = new List<TimingPoint>();

        public static Beatmap Load(string path){return Load(path,false);}
        public static Beatmap Load(string path,bool allowEmpty){return Load(path,allowEmpty,false);}
        public static Beatmap Load(string path,bool allowEmpty,bool difficultyOnly)
        {
            var map = new Beatmap(); map.SourcePath = path;
            string section = "", audio = null;
            bool hasAR = false; int mode = 0, backgroundPriority=0;
            var comboColours=new Dictionary<int,double[]>();
            using (var reader = new StreamReader(path))
            {
                string raw;int parsedLines=0;
                while ((raw = reader.ReadLine()) != null)
                {
                    if((parsedLines++&63)==0 && LoadCheckpoint!=null)LoadCheckpoint();
                    string line = raw.Trim();
                    if (line.Length == 0 || line.StartsWith("//")) continue;
                    if (line.StartsWith("osu file format v")) { map.Version = Integer(line.Substring(17)); continue; }
                    if (line.StartsWith("[")) { section = line; continue; }
                    if (section == "[Events]") {
                        string[] fields=line.Split(',');if(fields.Length>=3 && (fields[0]=="2" || fields[0]=="Break"))map.Breaks.Add(new double[]{Number(fields[1]),Number(fields[2])});
                        string video;double offset;
                        if(map.VideoPath==null && BeatmapArtwork.ReadVideoEvent(path,line,out video,out offset)) { map.VideoPath=video;map.VideoOffset=offset; }
                        map.BackgroundPath=BeatmapArtwork.ReadEvent(path,line,ref backgroundPriority,map.BackgroundPath);continue;
                    }
                    if (section == "[HitObjects]") { map.ReadObject(line); continue; }
                    if (section == "[TimingPoints]")
                    {
                        string[] f = line.Split(','); if (f.Length < 2) throw new FormatException("Malformed timing point");
                        bool red = f.Length < 7 || Integer(f[6]) != 0;
                        bool ticks = !String.Equals(f[1].Trim(), "NaN", StringComparison.OrdinalIgnoreCase);
                        double beat = ticks ? Number(f[1]) : -100;
                        if (red && beat <= 0) throw new FormatException("Invalid beat length");
                        map.SamplePoints.Add(new SamplePoint { Order=map.SamplePoints.Count,Time=Number(f[0]), Set=f.Length>3?Integer(f[3]):0, Index=f.Length>4?Integer(f[4]):0, Volume=f.Length>5?Integer(f[5]):map.DefaultSampleVolume });
                        map.timing.Add(new TimingPoint { Time = Number(f[0]), BeatLength = beat, Red = red, Ticks = ticks, Kiai=f.Length>7 && f[7].Trim().Length>0 && (Integer(f[7])&1)!=0, Index = map.timing.Count });
                        continue;
                    }
                    int colon = line.IndexOf(':'); if (colon < 0) continue;
                    string key = line.Substring(0, colon).Trim(), value = line.Substring(colon + 1).Trim();
                    if(section=="[Colours]" && key.StartsWith("Combo")) {
                        int index;string[] rgb=value.Split(',');
                        if(int.TryParse(key.Substring(5),out index) && index>0 && rgb.Length==3)
                            comboColours[index]=new double[]{Math.Max(0,Math.Min(255,Number(rgb[0])))/255,Math.Max(0,Math.Min(255,Number(rgb[1])))/255,Math.Max(0,Math.Min(255,Number(rgb[2])))/255};
                    }
                    if (section == "[General]") { if(key=="StackLeniency")map.StackLeniency=(float)Number(value);if (key == "AudioFilename") audio = value;if(key=="AudioLeadIn")map.AudioLeadIn=Number(value); if (key == "Mode") mode = Integer(value); if(key=="SampleSet") map.DefaultSampleSet=value=="Soft"?2:value=="Drum"?3:1; if(key=="SampleVolume") map.DefaultSampleVolume=Integer(value); }
                    if (section == "[Metadata]" && key == "Title") map.Title = value;
                    if(section=="[Metadata]"){if(key=="Artist")map.Artist=value;if(key=="Creator")map.Creator=value;if(key=="Source")map.Source=value;if(key=="Version")map.Difficulty=value;}
                    if (section == "[Difficulty]")
                    {
                        if (key == "OverallDifficulty") map.OD = Number(value);
                        if (key == "HPDrainRate") map.HP = Number(value);
                        if (key == "CircleSize") map.CS = Number(value);
                        if (key == "ApproachRate") { map.AR = Number(value); hasAR = true; }
                        if (key == "SliderMultiplier") map.SliderMultiplier = Number(value);
                        if (key == "SliderTickRate") map.SliderTickRate = Number(value);
                    }
                }
            }
            if (mode < 0 || mode > 3) throw new NotSupportedException("Unknown gameplay ruleset");
            map.Mode=mode;
            var colourOrder=new List<int>(comboColours.Keys);colourOrder.Sort();
            foreach(int index in colourOrder)map.ComboColours.Add(comboColours[index]);
            if (!allowEmpty && map.Circles.Count == 0) throw new FormatException("No hit objects in beatmap");
            if (!hasAR) map.AR = map.OD;
            if (map.OD < 0 || map.OD > 10 || map.AR < 0 || map.AR > 10 || map.CS < 0 || map.CS > (mode==3?18:10))
                throw new FormatException("Difficulty outside supported 0-10 range");
            if (map.SliderMultiplier <= 0 || map.SliderMultiplier > 100 || map.SliderTickRate <= 0 || map.SliderTickRate > 100)
                throw new FormatException("Invalid slider difficulty settings");
            map.Circles.Sort(delegate(Circle a, Circle b) { int order=a.Time.CompareTo(b.Time);return order!=0?order:a.SourceIndex.CompareTo(b.SourceIndex); });
            Circle previous=null;int comboNumber=0,comboColour=-1;
            foreach(Circle obj in map.Circles) {
                if(obj.Kind!=ObjectKind.Spinner && (previous==null || previous.Kind==ObjectKind.Spinner || obj.NewCombo)) {comboNumber=0;comboColour+=1+obj.ComboSkip;}
                obj.ComboNumber=++comboNumber;obj.ComboColourIndex=Math.Max(0,comboColour);previous=obj;
            }
            map.ApplyTiming();
            if(mode==3 && map.Circles.Count>0)ManiaConversionAdapter.Prepare(map,false);
            map.SamplePoints.Sort(delegate(SamplePoint a,SamplePoint b){int order=a.Time.CompareTo(b.Time);return order!=0?order:a.Order.CompareTo(b.Order);});
            if (String.IsNullOrEmpty(audio)) throw new FormatException("No AudioFilename");
            string directory = Path.GetFullPath(Path.GetDirectoryName(path));
            map.AudioPath = Path.GetFullPath(Path.Combine(directory, audio.Replace('\\', Path.DirectorySeparatorChar)));
            if (!map.AudioPath.StartsWith(directory + Path.DirectorySeparatorChar, StringComparison.Ordinal)) throw new FormatException("AudioFilename escapes beatmap directory");
            if (!difficultyOnly && !File.Exists(map.AudioPath)) throw new FileNotFoundException("Beatmap audio missing", map.AudioPath);
            return map;
        }
        private void ReadObject(string line)
        {
            string[] f=line.Split(','); if(f.Length<5)throw new FormatException("Malformed hit object");
            int type=Integer(f[3]) & (1|2|8|128);
            var obj=new Circle { X=Number(f[0]),Y=Number(f[1]),Time=Number(f[2]),SourceIndex=Circles.Count,HitSound=Integer(f[4]) };
            int flags=Integer(f[3]);obj.NewCombo=(flags&4)!=0;obj.ComboSkip=(flags>>4)&7;
            if(type==2)
            {
                if(f.Length<8)throw new FormatException("Slider is missing its path, spans or length");
                obj.EdgeSounds=f.Length>8 && f[8].Length>0?Array.ConvertAll(f[8].Split('|'),Integer):new int[0];
                obj.EdgeSamples=f.Length>9 && f[9].Length>0?Array.ConvertAll(f[9].Split('|'),HitSample.Parse):new HitSample[0];
                if(f.Length>10)obj.Sample=HitSample.Parse(f[10]);
                obj.Kind=ObjectKind.Slider;obj.PathDefinition=f[5];obj.SpanCount=Integer(f[6]);obj.ExpectedLength=Number(f[7]);
                if(obj.SpanCount<1 || obj.SpanCount>10000 || obj.ExpectedLength<0 || obj.ExpectedLength>1000000)throw new FormatException("Invalid slider spans or length");
            }
            else if(type==8)
            {
                if(f.Length<6)throw new FormatException("Spinner is missing its end time");
                if(f.Length>6)obj.Sample=HitSample.Parse(f[6]);
                obj.Kind=ObjectKind.Spinner;obj.X=256;obj.Y=192;obj.Duration=Number(f[5])-obj.Time;
                if(obj.Duration<=0 || obj.Duration>86400000)throw new FormatException("Invalid spinner duration");
            }
            else if(type==128){
                if(f.Length<6)throw new FormatException("Hold note is missing its end time");
                int colon=f[5].IndexOf(':');obj.Duration=Number(colon<0?f[5]:f[5].Substring(0,colon))-obj.Time;
                if(obj.Duration<0 || obj.Duration>86400000)throw new FormatException("Invalid hold duration");
                obj.Kind=obj.Duration==0?ObjectKind.Circle:ObjectKind.Hold;
                if(colon>=0)obj.Sample=HitSample.Parse(f[5].Substring(colon+1));
            }
            else if(type!=1)throw new FormatException("Unknown hit object type");
            if(type==1 && f.Length>5)obj.Sample=HitSample.Parse(f[5]);
            Circles.Add(obj);
        }
        public double BeatPosition(double time,out bool kiai)
        {
            int low=0,high=timing.Count;
            while(low<high){int mid=low+(high-low)/2;if(timing[mid].Time<=time)low=mid+1;else high=mid;}
            int index=low-1;kiai=index>=0 && timing[index].Kiai;
            for(int i=index;i>=0;i--)if(timing[i].Red)return (time-timing[i].Time)/timing[i].BeatLength;
            return time/500;
        }
        private void ApplyTiming()
        {
            timing.Sort(delegate(TimingPoint a,TimingPoint b){int order=a.Time.CompareTo(b.Time);return order!=0?order:a.Index.CompareTo(b.Index);});
            int point=0; double beatLength=500, sv=1; bool ticks=true;
            foreach(TimingPoint initial in timing)if(initial.Red){beatLength=initial.BeatLength;break;}
            int preparedObjects=0;foreach(Circle obj in Circles)
            {
                if((preparedObjects++&31)==0 && LoadCheckpoint!=null)LoadCheckpoint();
                while(point<timing.Count && timing[point].Time<=obj.Time)
                {
                    TimingPoint p=timing[point++];
                    if(p.Red){beatLength=p.BeatLength;sv=1;}
                    else sv=p.BeatLength<0 ? 100/Math.Max(10,Math.Min(1000,(float)-p.BeatLength)) : 1;
                    ticks=p.Ticks;
                }
                if(obj.Kind!=ObjectKind.Slider)continue;
                if(Mode==1){
                    // Native taiko sliders are drum rolls. The converter's
                    // distance/velocity multiplier cancels; no path mesh is used.
                    double adjusted=beatLength/Math.Max(.1,sv);
                    obj.Velocity=100*SliderMultiplier/adjusted;
                    obj.SpanDuration=obj.ExpectedLength/obj.Velocity;
                    obj.Duration=Math.Floor(obj.SpanDuration*obj.SpanCount);
                    if(double.IsInfinity(obj.Duration) || obj.Duration>86400000)throw new FormatException("Invalid drum roll duration");
                    continue;
                }
                obj.Path=SliderPath.Parse(obj.PathDefinition,obj.X,obj.Y,obj.ExpectedLength);
                double adjustedBeat=beatLength*(Math.Max(10,Math.Min(1000,(float)(100/sv)))/100.0);
                obj.Velocity=100*SliderMultiplier/adjustedBeat;
                obj.SpanDuration=obj.Path.Length/obj.Velocity;
                obj.Duration=obj.SpanDuration*obj.SpanCount;
                if(double.IsInfinity(obj.Duration) || obj.Duration>86400000)throw new FormatException("Invalid slider duration");
                obj.TickDistance=ticks ? obj.Velocity*beatLength/SliderTickRate*(Version<8?1/sv:1) : double.PositiveInfinity;
                for(int span=0;span<obj.SpanCount;span++)
                {
                    double length=Math.Min(100000,obj.Path.Length);
                    if(obj.TickDistance>0)for(double distance=obj.TickDistance;distance<length-obj.Velocity*10;distance+=obj.TickDistance)
                    {
                        double progress=distance/length;
                        obj.Events.Add(new SliderEvent { Span=span,Time=obj.Time+(span+(span%2==0?progress:1-progress))*obj.SpanDuration, Progress=progress });
                        if(obj.Events.Count>100000)throw new FormatException("Too many slider ticks");
                    }
                    if(span+1<obj.SpanCount)obj.Events.Add(new SliderEvent { Span=span,Time=obj.Time+(span+1)*obj.SpanDuration,Progress=(span+1)%2,Repeat=true });
                }
                obj.Events.Add(new SliderEvent { Span=obj.SpanCount-1,Time=obj.EndTime,Progress=obj.SpanCount%2,Tail=true });
                obj.Events.Sort(delegate(SliderEvent a,SliderEvent b){return a.Time.CompareTo(b.Time);});
            }
        }
        private static int Integer(string value) { return int.Parse(value,CultureInfo.InvariantCulture); }
        public static double Number(string value)
        {
            double result = double.Parse(value, CultureInfo.InvariantCulture);
            if (double.IsNaN(result) || double.IsInfinity(result)) throw new FormatException("Non-finite beatmap value");
            return result;
        }
    }
}
