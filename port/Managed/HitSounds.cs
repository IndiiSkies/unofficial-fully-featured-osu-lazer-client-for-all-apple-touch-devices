using System;
using System.IO;
using System.Collections.Generic;
using System.Globalization;

namespace OsuLegacy
{
    public sealed class HitSample
    {
        public int Set, AdditionSet, Index, Volume;
        public string Filename="";
        public static HitSample Parse(string text)
        {
            string[] f=text.Split(':');var sample=new HitSample();
            if(f.Length>0 && f[0].Length>0)sample.Set=int.Parse(f[0],CultureInfo.InvariantCulture);
            if(f.Length>1 && f[1].Length>0)sample.AdditionSet=int.Parse(f[1],CultureInfo.InvariantCulture);
            if(f.Length>2 && f[2].Length>0)sample.Index=int.Parse(f[2],CultureInfo.InvariantCulture);
            if(f.Length>3 && f[3].Length>0)sample.Volume=int.Parse(f[3],CultureInfo.InvariantCulture);
            if(f.Length>4)sample.Filename=f[4];return sample;
        }
    }
    public sealed class SamplePoint { public double Time; public int Set,Index,Volume,Order; }
    public interface ISoundOutput
    {
        int Load(string path);
        void Play(int sample,double volume);
        void Loop(int slot,int sample,double volume);
    }
    public interface IMetronomeSoundOutput {void Metronome(int sample,double gain,double pitch);}
    public interface IPitchedSoundOutput {void PlayPitched(int id,double volume,double pitch);}
    public sealed class HitSounds
    {
        private readonly Beatmap map;
        private readonly ISoundOutput output;
        private readonly string bundle;
        private readonly Dictionary<string,int> loaded=new Dictionary<string,int>();
        private readonly Dictionary<string,int> defaults=new Dictionary<string,int>();
        private readonly Dictionary<string,int> resolved=new Dictionary<string,int>();
        private static readonly string[] banks={"normal","soft","drum"};
        private static readonly string[] names={"hitnormal","hitwhistle","hitfinish","hitclap","slidertick","sliderslide","sliderwhistle"};
        public HitSounds(Beatmap map,ISoundOutput output,string bundle) { this.map=map;this.output=output;this.bundle=bundle; }
        private static int Bank(int value) { return value>=1 && value<=3?value:1; }
        private static double Gain(int value) { return Math.Max(0,Math.Min(100,value))/100.0; }
        public SamplePoint PointAt(double time)
        {
            int low=0,high=map.SamplePoints.Count;
            while(low<high){int mid=low+(high-low)/2;if(map.SamplePoints[mid].Time<=time)low=mid+1;else high=mid;}
            return low==0?new SamplePoint { Set=map.DefaultSampleSet,Volume=map.DefaultSampleVolume }:map.SamplePoints[low-1];
        }
        private int Load(string path)
        { int id;if(loaded.TryGetValue(path,out id))return id;id=output.Load(path);loaded[path]=id;return id; }
        public void Prepare()
        { Prepare(null); }
        public bool Prepare(Func<bool> cancelled)
        {
            foreach(string bank in banks)foreach(string name in names){if(cancelled!=null && cancelled())return false;Default(bank+"-"+name);}
            Default("spinnerbonus");Default("spinnerspin");Default("combobreak");
            if(map.Mode==2 || (map.ModSettings!=null && (map.ModSettings.Has("MU") || map.ModSettings.Has("TP"))))Default("catch-banana");if(map.Mode==2 || map.Mode==3)Default("catch-resume-countdown");
            foreach(Circle obj in map.Objects)
            {
                if(cancelled!=null && cancelled())return false;
                Resolve(obj,"hit",0,obj.Time,true);
                if(obj.Kind==ObjectKind.Hold && obj.ManiaTailSound)Resolve(obj,"hit",1,obj.EndTime,true);
                if(obj.Kind==ObjectKind.Spinner)Resolve(obj,"hit",1,obj.EndTime,true);
                if(obj.Kind==ObjectKind.Slider)
                {
                    foreach(SliderEvent e in obj.Events)Resolve(obj,e.Tail || e.Repeat?"hit":"slidertick",e.Span+1,e.Time,true);
                    Resolve(obj,"sliderslide",0,obj.Time,true);Resolve(obj,"sliderwhistle",0,obj.Time,true);
                    foreach(SamplePoint point in map.SamplePoints)if(point.Time>obj.Time && point.Time<=obj.EndTime)
                    { Resolve(obj,"sliderslide",0,point.Time,true);Resolve(obj,"sliderwhistle",0,point.Time,true); }
                }
            }
            return cancelled==null || !cancelled();
        }
        private int Default(string name)
        { int id;if(!defaults.TryGetValue(name,out id)){id=Load(Path.Combine(bundle,name+".wav"));defaults[name]=id;}return id; }
        private struct Request { public string Name,Filename; public int Index,Volume; }
        private Request Get(Circle obj,string name,int edge,double time,bool addition)
        {
            SamplePoint point=PointAt(time);HitSample sample=obj.Sample;
            int set=sample.Set,extra=sample.AdditionSet;
            if(name.StartsWith("hit") && obj.EdgeSamples!=null && edge<obj.EdgeSamples.Length)
            { set=obj.EdgeSamples[edge].Set;extra=obj.EdgeSamples[edge].AdditionSet; }
            set=Bank(set!=0?set:point.Set!=0?point.Set:map.DefaultSampleSet);
            if(addition && extra!=0)set=Bank(extra);
            return new Request { Name=banks[set-1]+"-"+name,Index=sample.Index!=0?sample.Index:point.Index,
                Volume=sample.Volume!=0?sample.Volume:point.Volume,Filename=name=="hitnormal"?sample.Filename:"" };
        }
        private int Sample(Request request,bool prepare)
        {
            string key=request.Name+":"+request.Index+":"+request.Filename;int id;
            if(resolved.TryGetValue(key,out id))return id;
            if(!prepare)return defaults[request.Name];
            id=0;
            if(request.Filename.Length>0 || request.Index>0)
            {
                string filename=request.Filename;
                if(filename.Length>0)
                { string path=BeatmapArtwork.Resolve(map.SourcePath,filename);if(path!=null)id=Load(path); }
                else foreach(string extension in new[]{".wav",".ogg",".mp3",".aiff",".m4a"})
                {
                    string path=BeatmapArtwork.Resolve(map.SourcePath,request.Name+(request.Index>1?request.Index.ToString(CultureInfo.InvariantCulture):"")+extension);
                    if(path!=null){id=Load(path);if(id!=0)break;}
                }
            }
            if(id==0)id=defaults[request.Name];resolved[key]=id;return id;
        }
        // Prepare and playback share this exact resolver; the audio callback
        // never decodes files or searches the beatmap directory.
        private void Resolve(Circle obj,string kind,int edge,double time,bool prepare)
        {
            if(kind=="hit")
            {
                int flags=obj.HitSound;
                if(obj.EdgeSounds!=null)flags=edge<obj.EdgeSounds.Length?obj.EdgeSounds[edge]:0;
                One(obj,"hitnormal",edge,time,false,prepare);
                if((flags&2)!=0)One(obj,"hitwhistle",edge,time,true,prepare);
                if((flags&4)!=0)One(obj,"hitfinish",edge,time,true,prepare);
                if((flags&8)!=0)One(obj,"hitclap",edge,time,true,prepare);
            }
            else One(obj,kind,edge,time,kind=="sliderwhistle",prepare);
        }
        private void One(Circle obj,string name,int edge,double time,bool addition,bool prepare)
        {
            Request request=Get(obj,name,edge,time,addition);int id=Sample(request,prepare);
            if(!prepare)output.Play(id,Gain(request.Volume));
        }
        public void Play(Circle obj,string kind,int edge)
        {
            if(kind=="catch-banana" || kind=="catch-resume-countdown"){
                int sample=defaults[kind];double pitch=kind=="catch-banana"?.77+edge*.006:edge==0?.5:1;
                IPitchedSoundOutput pitched=output as IPitchedSoundOutput;if(pitched!=null)pitched.PlayPitched(sample,1,pitch);else output.Play(sample,1);return;
            }
            if(kind=="catch-tick"){Resolve(obj,"slidertick",0,obj.LastFollowTime,false);return;}
            if(kind=="spinnerbonus" || kind=="combobreak") { output.Play(defaults[kind],1);return; }
            double time=obj.Time;
            if(obj.Kind==ObjectKind.Slider && edge>0 && kind=="hit")time=obj.Time+edge*obj.SpanDuration;
            else if(obj.Kind==ObjectKind.Slider && kind=="slidertick")time=obj.Events[obj.NextEvent-1].Time;
            else if(obj.Kind==ObjectKind.Spinner)time=obj.EndTime;
            else if(obj.Kind==ObjectKind.Hold && edge>0)time=obj.EndTime;
            Resolve(obj,kind,edge,time,false);
        }
        private int lastModBeat=int.MinValue;
        public void Update(Session session,double time)
        {
            if((session.Mods.Has("MU") || session.Mods.Has("TP")) && map.Objects.Count>0){bool kiai;int beat=(int)Math.Floor(map.BeatPosition(time,out kiai));if(beat!=lastModBeat && time>=map.Objects[0].Time-map.BeatLengthAt(time)*4){lastModBeat=beat;double gain=session.Mods.Has("MU")?1-session.Mods.MusicGain:1;IMetronomeSoundOutput click=output as IMetronomeSoundOutput;if(click!=null)click.Metronome(defaults["catch-banana"],gain,beat%4==0?1:.5);}}
            // Taiko rolls/swells use tap samples, never standard follow loops.
            if(map.Mode!=0){for(int slot=0;slot<4;slot++)output.Loop(slot,0,0);return;}
            Circle slider=null,spinner=null;
            for(int i=session.FirstPending;i<map.Objects.Count;i++)
            {
                Circle obj=map.Objects[i];if(obj.Time>time)break;if(obj.Judged || time>=obj.EndTime)continue;
                if(obj.Kind==ObjectKind.Slider && session.Tracking(obj,time))slider=obj;
                if(obj.Kind==ObjectKind.Spinner && obj.HasAngle && obj.Rotation>0 && time-obj.LastAngleTime<150)spinner=obj;
            }
            for(int slot=0;slot<2;slot++)
            {
                string name=slot==0?"sliderslide":"sliderwhistle";
                if(slider==null || (slot==1 && (slider.HitSound&2)==0)){output.Loop(slot,0,0);continue;}
                Request request=Get(slider,name,0,time,slot==1);output.Loop(slot,Sample(request,false),Gain(request.Volume));
            }
            output.Loop(2,spinner==null?0:defaults["spinnerspin"],.5);
        }
    }
}
