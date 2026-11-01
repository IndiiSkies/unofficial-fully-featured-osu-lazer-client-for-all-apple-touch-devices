using System;using System.Collections.Generic;
using MC=OsuLegacy.ManiaConversion;
namespace OsuLegacy {
 public static class ManiaConversionAdapter {
  private static IList<MC.HitSampleInfo> Samples(Circle o,int edge){
   int flags=o.EdgeSounds!=null && edge>=0 && edge<o.EdgeSounds.Length?o.EdgeSounds[edge]:o.HitSound;
   var list=new List<MC.HitSampleInfo>();
   // File-only samples do not acquire a normal/finish/clap type in lazer.
   HitSample sample=o.EdgeSamples!=null && edge>=0 && edge<o.EdgeSamples.Length?o.EdgeSamples[edge]:o.Sample;
   if(!String.IsNullOrEmpty(sample.Filename)){list.Add(new MC.HitSampleInfo {Name="file",Flags=0,Source=o.SourceIndex,Edge=edge});return list;}
   foreach(int bit in new int[]{1,2,4,8})if(bit==1 || (flags&bit)!=0)list.Add(new MC.HitSampleInfo {Name=bit==1?"hitnormal":bit==2?"hitwhistle":bit==4?"hitfinish":"hitclap",Flags=flags,Source=o.SourceIndex,Edge=edge});
   return list;
  }
  public static void Prepare(Beatmap map,bool convert){
   if(convert && map.Mode!=0)return;
   var original=new List<Circle>(map.Objects);var byIndex=new Dictionary<int,Circle>();
   var input=new MC.IBeatmap();input.Difficulty=new MC.IBeatmapDifficultyInfo {DrainRate=map.HP,CircleSize=map.CS,OverallDifficulty=map.OD,ApproachRate=map.AR,SliderMultiplier=map.SliderMultiplier};
   input.ControlPointInfo.Points.AddRange(map.ExportTiming());foreach(double[] gap in map.Breaks)input.TotalBreakTime+=Math.Max(0,gap[1]-gap[0]);
   foreach(Circle o in original){byIndex[o.SourceIndex]=o;MC.SourceNote note;
    if(o.Kind==ObjectKind.Slider){var slider=new MC.SourceSlider {Path=new MC.PathData(),Repeats=o.SpanCount-1,SliderVelocityMultiplier=input.ControlPointInfo.VelocityAt(o.Time),NodeSamples=new List<IList<MC.HitSampleInfo>>()};slider.Path.ExpectedDistance.Value=o.ExpectedLength;
     for(int i=0;i<=o.SpanCount;i++)slider.NodeSamples.Add(Samples(o,i));note=slider;
    }else if(o.Kind==ObjectKind.Spinner)note=new MC.SourceSpinner {EndTime=o.EndTime};
    else note=new MC.SourceNote();
    note.X=(float)o.X;note.Y=(float)o.Y;note.StartTime=o.Time;note.Samples=Samples(o,-1);note.Source=o.SourceIndex;input.HitObjects.Add(note);
   }
   MC.Converter engine=convert?new MC.Converter(input):null;
   map.ManiaColumns=convert?engine.Columns:Math.Max(1,Math.Min(18,(int)Math.Round(map.CS)));
   if(!convert){foreach(Circle o in original){o.Column=Math.Max(0,Math.Min(map.ManiaColumns-1,(int)Math.Floor((float)o.X/(512f/map.ManiaColumns))));if(o.Kind==ObjectKind.Slider){o.Kind=o.Duration>0?ObjectKind.Hold:ObjectKind.Circle;o.Events.Clear();}}
    // Older native mania spinners are random holds, rather than x-position notes.
    bool spinner=false;foreach(Circle o in original)if(o.Kind==ObjectKind.Spinner)spinner=true;
    if(!spinner)return;
   }
   if(engine==null)engine=new MC.Converter(input,map.ManiaColumns);
   map.Objects.Clear();
   foreach(MC.ManiaHitObject n in engine.Output){Circle source=byIndex[n.Source];var hold=n as MC.HoldNote;
    if(!convert && source.Kind!=ObjectKind.Spinner){map.Objects.Add(source);continue;}
    int edge=n.Samples.Count==0?-1:n.Samples[0].Edge;
    var note=new Circle {Kind=hold==null?ObjectKind.Circle:ObjectKind.Hold,Time=n.StartTime,Duration=hold==null?0:hold.Duration,Column=n.Column,X=(n.Column+.5)*512/map.ManiaColumns,Y=192,SourceIndex=source.SourceIndex,HitSound=n.Samples.Count==0?0:n.Samples[0].Flags,Sample=source.EdgeSamples!=null && edge>=0 && edge<source.EdgeSamples.Length?source.EdgeSamples[edge]:source.Sample};
    if(hold!=null && hold.NodeSamples!=null && hold.NodeSamples.Count>0){var tail=hold.NodeSamples[hold.NodeSamples.Count-1];if(tail.Count>0){int lastEdge=tail[0].Edge;HitSample sample=source.EdgeSamples!=null && lastEdge>=0 && lastEdge<source.EdgeSamples.Length?source.EdgeSamples[lastEdge]:source.Sample;note.ManiaTailSound=true;note.EdgeSounds=new int[]{note.HitSound,tail[0].Flags};note.EdgeSamples=new HitSample[]{note.Sample,sample};}}
    map.Objects.Add(note);
   }
   map.Objects.Sort(delegate(Circle a,Circle b){int c=a.Time.CompareTo(b.Time);if(c!=0)return c;c=a.Column.CompareTo(b.Column);return c!=0?c:a.SourceIndex.CompareTo(b.SourceIndex);});
   map.Mode=3;map.ManiaConverted=convert;
  }
 }
}
