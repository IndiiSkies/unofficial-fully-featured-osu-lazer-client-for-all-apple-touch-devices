// Adapter for the MIT-licensed lazer mania pattern generators. Only corlib is required.
using System;using System.Collections.Generic;using System.Linq;
namespace System.Runtime.CompilerServices { [AttributeUsage(AttributeTargets.Method|AttributeTargets.Class|AttributeTargets.Assembly)] public sealed class ExtensionAttribute:Attribute {} }
namespace System.Linq {
 public static class Enumerable {
  public static IEnumerable<T> Empty<T>(){yield break;}
  public static IEnumerable<T> Yield<T>(this T value){yield return value;}
  public static bool Any<T>(this IEnumerable<T> items){foreach(T item in items)return true;return false;}
  public static bool Any<T>(this IEnumerable<T> items,Func<T,bool> predicate){foreach(T item in items)if(predicate(item))return true;return false;}
  public static IEnumerable<T> Where<T>(this IEnumerable<T> items,Func<T,bool> predicate){foreach(T item in items)if(predicate(item))yield return item;}
  public static List<T> ToList<T>(this IEnumerable<T> items){return new List<T>(items);}
  public static IEnumerable<T> Skip<T>(this IEnumerable<T> items,int count){foreach(T item in items){if(count>0){count--;continue;}yield return item;}}
  public static T First<T>(this IEnumerable<T> items){foreach(T item in items)return item;throw new InvalidOperationException();}
  public static T FirstOrDefault<T>(this IEnumerable<T> items){foreach(T item in items)return item;return default(T);}
  public static T LastOrDefault<T>(this IEnumerable<T> items){T last=default(T);foreach(T item in items)last=item;return last;}
  public static int Count<T>(this IEnumerable<T> items){int n=0;foreach(T item in items)n++;return n;}
  public static void ForEach<T>(this IEnumerable<T> items,Action<T> action){foreach(T item in items)action(item);}
 }
}
namespace OsuLegacy.ManiaConversion {
 public static class Debug {public static void Assert(bool condition){if(!condition)throw new InvalidOperationException("Invalid converter input");}}
 public static class LegacyMath {
  public static int Clamp(int x,int a,int b){return System.Math.Max(a,System.Math.Min(b,x));}
  public static double Clamp(double x,double a,double b){return System.Math.Max(a,System.Math.Min(b,x));}
  public static int Min(int a,int b){return System.Math.Min(a,b);}public static double Min(double a,double b){return System.Math.Min(a,b);}
  public static int Max(int a,int b){return System.Math.Max(a,b);}public static double Max(double a,double b){return System.Math.Max(a,b);}
  public static double Floor(double x){return System.Math.Floor(x);}public static double Round(double x){return System.Math.Round(x);}
 }
 public static class LegacyMathF {public static float Floor(float x){return (float)System.Math.Floor(x);}public static float Round(float x){return (float)System.Math.Round(x);}}
 public struct Vector2 {public float X,Y;public Vector2(float x,float y){X=x;Y=y;}public static Vector2 Zero{get{return new Vector2();}}public float Length{get{return (float)System.Math.Sqrt(X*X+Y*Y);}}public static Vector2 operator -(Vector2 a,Vector2 b){return new Vector2(a.X-b.X,a.Y-b.Y);}}
 public class HitSampleInfo {public const string HIT_NORMAL="hitnormal",HIT_FINISH="hitfinish",HIT_CLAP="hitclap",HIT_WHISTLE="hitwhistle";public string Name;public int Flags,Source,Edge;}
 public interface IHasXPosition {float X{get;}}
 public interface IHasPosition:IHasXPosition {Vector2 Position{get;}}
 public interface IHasDuration {double EndTime{get;}}
 public interface IHasSliderVelocity {double SliderVelocityMultiplier{get;}}
 public class ExpectedDistance {public double? Value;}
 public class PathData {public ExpectedDistance ExpectedDistance=new ExpectedDistance();}
 public interface IHasPath {PathData Path{get;}}
 public interface IHasRepeats {int Repeats{get;}IList<IList<HitSampleInfo>> NodeSamples{get;}}
 public interface IHasPathWithRepeats:IHasPath,IHasRepeats {}
 public class HitObject {public double StartTime;public IList<HitSampleInfo> Samples=new List<HitSampleInfo>();public int Source;}
 public class SourceNote:HitObject,IHasPosition {public float X{get;set;}public float Y;public Vector2 Position{get{return new Vector2(X,Y);}}}
 public class SourceSlider:SourceNote,IHasPathWithRepeats,IHasSliderVelocity {public PathData Path{get;set;}public double SliderVelocityMultiplier{get;set;}public int Repeats{get;set;}public IList<IList<HitSampleInfo>> NodeSamples{get;set;}}
 public class SourceSpinner:SourceNote,IHasDuration {public double EndTime{get;set;}}
 public class ManiaHitObject:HitObject {public int Column;}
 public class Note:ManiaHitObject {}
 public class HoldNote:ManiaHitObject,IHasDuration {public double Duration;public double EndTime{get{return StartTime+Duration;}}public bool PlaySlidingSamples;public IList<IList<HitSampleInfo>> NodeSamples;}
 public static class ObjectExtensions {public static int SpanCount(this IHasRepeats o){return o.Repeats+1;}public static double GetEndTime(this HitObject o){var duration=o as IHasDuration;return duration==null?o.StartTime:duration.EndTime;}}
 public class TimingControlPoint {public double BeatLength=500;}
 public class EffectControlPoint {public bool KiaiMode;}
 public class IBeatmapDifficultyInfo {public double DrainRate,CircleSize,OverallDifficulty,ApproachRate,SliderMultiplier=1.4;}
 public class ControlPoints {
  public readonly List<double[]> Points=new List<double[]>();
  public TimingControlPoint TimingPointAt(double time){double beat=500;foreach(var p in Points)if(p[2]!=0){beat=p[1];break;}foreach(var p in Points){if(p[0]>time)break;if(p[2]!=0)beat=p[1];}return new TimingControlPoint {BeatLength=beat};}
  public EffectControlPoint EffectPointAt(double time){bool kiai=false;foreach(var p in Points){if(p[0]>time)break;kiai=p[3]!=0;}return new EffectControlPoint {KiaiMode=kiai};}
  public double VelocityAt(double time){double sv=1;foreach(var p in Points){if(p[0]>time)break;sv=p[2]!=0?1:p[1]<0?LegacyMath.Clamp(-100/p[1],.1,10):1;}return sv;}
 }
 public class IBeatmap {public List<HitObject> HitObjects=new List<HitObject>();public IBeatmapDifficultyInfo Difficulty=new IBeatmapDifficultyInfo();public double TotalBreakTime;public ControlPoints ControlPointInfo=new ControlPoints();}
 public static class ManiaRuleset {public const string SHORT_NAME="mania";}
 public static class LegacyRulesetExtensions {public static double GetPrecisionAdjustedBeatLength(IHasSliderVelocity obj,TimingControlPoint timing,string name){double value=-100/obj.SliderVelocityMultiplier;return timing.BeatLength*(value<0?LegacyMath.Clamp((float)-value,10,10000)/100:1);}}
 public sealed class Converter {
  public int Columns;public readonly List<ManiaHitObject> Output=new List<ManiaHitObject>();
  private readonly List<double> times=new List<double>();private double density=int.MaxValue,lastTime;private Vector2 lastPosition;
  private void Density(double time){times.Add(time);if(times.Count>7)times.RemoveAt(0);if(times.Count>=2)density=(time-times[0])/times.Count;}
  private void Record(double time,Vector2 position){lastTime=time;lastPosition=position;}
  public Converter(IBeatmap map,int nativeColumns=0){
   var d=map.Difficulty;int special=0;foreach(var o in map.HitObjects)if(o is SourceSlider || o is SourceSpinner)special++;
   double cs=System.Math.Round(d.CircleSize),od=System.Math.Round(d.OverallDifficulty),ratio=map.HitObjects.Count==0?0:(double)special/map.HitObjects.Count;
   Columns=map.HitObjects.Count>0 && ratio<.2?7:map.HitObjects.Count>0 && (ratio<.3 || cs>=5)?od>5?7:6:map.HitObjects.Count>0 && ratio>.6?od>4?5:4:LegacyMath.Clamp((int)od+1,4,7);
   if(nativeColumns>0)Columns=nativeColumns;
   var random=new LegacyRandom((int)LegacyMathF.Round((float)d.DrainRate+(float)d.CircleSize)*20+(int)(d.OverallDifficulty*41.2)+(int)LegacyMathF.Round((float)d.ApproachRate));
   Pattern previous=new Pattern();PatternType stair=PatternType.Stair;
   foreach(HitObject source in map.HitObjects){PatternGenerator generator;var position=((SourceNote)source).Position;
    if(nativeColumns>0 && !(source is SourceSpinner)){Output.Add(new Note {StartTime=source.StartTime,Source=source.Source});Record(source.StartTime,position);continue;}
    if(source is SourceSlider){var slider=new SliderPatternGenerator(random,source,map,Columns,previous);generator=slider;for(int i=0;i<=slider.SpanCount;i++){double t=source.StartTime+slider.SegmentDuration*i;Record(t,position);Density(t);}}
    else if(source is SourceSpinner){generator=new SpinnerPatternGenerator(random,source,map,Columns,previous);Record(((SourceSpinner)source).EndTime,new Vector2(256,192));Density(lastTime);}
    else {Density(source.StartTime);generator=new HitCirclePatternGenerator(random,source,map,Columns,previous,lastTime,lastPosition,density,stair);Record(source.StartTime,position);}
    foreach(Pattern pattern in generator.Generate()){var circle=generator as HitCirclePatternGenerator;if(circle!=null)stair=circle.StairType;if(circle!=null || generator is SliderPatternGenerator)previous=pattern;foreach(var o in pattern.HitObjects){o.Source=source.Source;Output.Add(o);}}
    if(Output.Count>200000)throw new FormatException("Too many converted mania notes");
   }
  }
 }
}
