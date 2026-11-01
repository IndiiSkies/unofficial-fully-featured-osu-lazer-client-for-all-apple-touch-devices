// Compatibility types for MIT-licensed lazer difficulty algorithms. No UI/framework runtime required.
using System;using System.Collections.Generic;using System.Linq;
namespace System.Runtime.CompilerServices {[AttributeUsage(AttributeTargets.Method|AttributeTargets.Class|AttributeTargets.Assembly)]public sealed class ExtensionAttribute:Attribute{}}
namespace System.Linq {public static class Enumerable {
 public static T ElementAtOrDefault<T>(this IList<T> source,int i){return i>=0&&i<source.Count?source[i]:default(T);}
 public static T LastOrDefault<T>(this IEnumerable<T> source){T last=default(T);foreach(var v in source)last=v;return last;}
 public static IEnumerable<T> Distinct<T>(this IEnumerable<T> source){var seen=new Dictionary<T,bool>();foreach(var v in source)if(!seen.ContainsKey(v)){seen[v]=true;yield return v;}}
 public static Dictionary<K,V> ToDictionary<T,K,V>(this IEnumerable<T> source,Func<T,K> key,Func<T,V> value){var d=new Dictionary<K,V>();foreach(var v in source)d.Add(key(v),value(v));return d;}
 public static T MinBy<T>(this IEnumerable<T> source,Func<T,double> key){T best=default(T);double min=double.PositiveInfinity;foreach(var v in source){double n=key(v);if(n<min){best=v;min=n;}}return best;}
 public static IEnumerable<R> SelectMany<T,R>(this IEnumerable<T> source,Func<T,IEnumerable<R>> selector){foreach(var v in source)foreach(var w in selector(v))yield return w;}
 public static double Max<T>(this IEnumerable<T> source,Func<T,double> key){double max=double.NegativeInfinity;foreach(var v in source)max=Math.Max(max,key(v));return max;}
 public static double Average(this IEnumerable<double> source){double sum=0;int n=0;foreach(var v in source){sum+=v;n++;}return sum/n;}
 public static void AddInPlace<T>(this List<T> list,T value) where T:IComparable<T>{int i=list.BinarySearch(value);list.Insert(i<0?~i:i,value);}

 public static IEnumerable<T> Where<T>(this IEnumerable<T> source,Func<T,bool> predicate){foreach(var v in source)if(predicate(v))yield return v;}
 public static IEnumerable<R> Select<T,R>(this IEnumerable<T> source,Func<T,R> selector){foreach(var v in source)yield return selector(v);}
 public static IEnumerable<T> Append<T>(this IEnumerable<T> source,T value){foreach(var v in source)yield return v;yield return value;}
 public static List<T> ToList<T>(this IEnumerable<T> source){return new List<T>(source);}public static T[] ToArray<T>(this IEnumerable<T> source){return new List<T>(source).ToArray();}
 public static IEnumerable<T> OrderByDescending<T>(this IEnumerable<T> source,Func<T,double> key){var list=new List<T>(source);list.Sort((a,b)=>key(b).CompareTo(key(a)));return list;}
 public static IEnumerable<T> OrderBy<T>(this IEnumerable<T> source,Func<T,double> key){var list=new List<T>(source);list.Sort((a,b)=>key(a).CompareTo(key(b)));return list;}
 public static bool Any<T>(this IEnumerable<T> source,Func<T,bool> predicate){foreach(var v in source)if(predicate(v))return true;return false;}
 public static bool Any<T>(this IEnumerable<T> source){foreach(var v in source)return true;return false;}
 public static double Sum<T>(this IEnumerable<T> source,Func<T,double> selector){double n=0;foreach(var v in source)n+=selector(v);return n;}
 public static double Sum(this IEnumerable<double> source){double n=0;foreach(var v in source)n+=v;return n;}
 public static int Count<T>(this IEnumerable<T> source,Func<T,bool> predicate){int n=0;foreach(var v in source)if(predicate(v))n++;return n;}
 public static int Count<T>(this IEnumerable<T> source){int n=0;foreach(var v in source)n++;return n;}
 public static T FirstOrDefault<T>(this IEnumerable<T> source){foreach(var v in source)return v;return default(T);}
 public static T Last<T>(this IEnumerable<T> source){T last=default(T);bool any=false;foreach(var v in source){last=v;any=true;}if(!any)throw new InvalidOperationException();return last;}
 public static T First<T>(this IEnumerable<T> source){foreach(var v in source)return v;throw new InvalidOperationException();}
 public static double Max(this IEnumerable<double> source){double n=double.NegativeInfinity;foreach(var v in source)n=Math.Max(n,v);return n;}
 public static IEnumerable<T> Take<T>(this IEnumerable<T> source,int count){foreach(var v in source){if(count--<=0)yield break;yield return v;}}
 public static IEnumerable<T> Skip<T>(this IEnumerable<T> source,int count){foreach(var v in source){if(count-->0)continue;yield return v;}}
 public static IEnumerable<T> OfType<T>(this System.Collections.IEnumerable source){foreach(var v in source)if(v is T)yield return (T)v;}
 public static IEnumerable<T> Concat<T>(this IEnumerable<T> a,IEnumerable<T> b){foreach(var v in a)yield return v;foreach(var v in b)yield return v;}
}}
namespace OsuLegacy.Difficulty {
 public static class Compat {public static double DegreesToRadians(double d){return d*(Math.PI/180);}public static bool IsNormal(double v){return !double.IsNaN(v)&&!double.IsInfinity(v)&&Math.Abs(v)>=2.2250738585072014e-308;}public static double Clamp(double v,double a,double b){return Math.Max(a,Math.Min(b,v));}public static float Clamp(float v,float a,float b){return Math.Max(a,Math.Min(b,v));}public static int Clamp(int v,int a,int b){return Math.Max(a,Math.Min(b,v));}public static double Cbrt(double v){return Math.Sign(v)*Math.Pow(Math.Abs(v),1.0/3);}}
 public static class CompatF {public static float Pow(float a,float b){return (float)Math.Pow(a,b);}public static float Abs(float a){return Math.Abs(a);}public static float Max(float a,float b){return Math.Max(a,b);}public static float Min(float a,float b){return Math.Min(a,b);}}
 public class BoundBool{public bool Value;}public class BoundFloat{public float Value;}public class OsuModMagnetised:Mod{public BoundFloat AttractionStrength=new BoundFloat();}public class OsuModDeflate:Mod{public BoundFloat StartScale=new BoundFloat();}
 public class Mod{} public class OsuModHidden:Mod{public const double FADE_OUT_DURATION_MULTIPLIER=.3;public BoundBool OnlyFadeApproachCircles=new BoundBool();}public class OsuModFlashlight:Mod{}public class OsuModTouchDevice:Mod{}public class OsuModAutopilot:Mod{}public class OsuModRelax:Mod{}
 public enum HitResult {Great,Ok,Meh}
 public class HitWindows {public static readonly HitWindows Empty=new HitWindows();public double Great=50;public double WindowFor(HitResult hit){return Great;}}
 public class HitObject {public double StartTime,EndTime;public HitWindows HitWindows=new HitWindows();public List<HitObject> NestedHitObjects=new List<HitObject>();public double GetEndTime(){return EndTime;} }
 public struct Vector2 {public float X,Y;public Vector2(float x,float y){X=x;Y=y;}public static Vector2 Add(Vector2 a,Vector2 b){return a+b;}public static Vector2 Subtract(Vector2 a,Vector2 b){return a-b;}public static Vector2 Multiply(Vector2 a,float b){return a*b;}public static float Distance(Vector2 a,Vector2 b){return (a-b).Length;}public float Length{get{return (float)Math.Sqrt(X*X+Y*Y);}}public float LengthSquared{get{return X*X+Y*Y;}}public static Vector2 operator+(Vector2 a,Vector2 b){return new Vector2(a.X+b.X,a.Y+b.Y);}public static Vector2 operator-(Vector2 a,Vector2 b){return new Vector2(a.X-b.X,a.Y-b.Y);}public static Vector2 operator*(Vector2 a,float b){return new Vector2(a.X*b,a.Y*b);}public static Vector2 operator/(Vector2 a,float b){return new Vector2(a.X/b,a.Y/b);}public static float Dot(Vector2 a,Vector2 b){return a.X*b.X+a.Y*b.Y;}}
 public static class Precision {public static bool DefinitelyBigger(double a,double b,double epsilon=1e-3){return a-b>epsilon;}public static bool AlmostEquals(double a,double b,double epsilon=1e-3){return Math.Abs(a-b)<=epsilon;}}
 public static class Interpolation {public static double Lerp(double a,double b,double t){return a+(b-a)*t;}}
 public static class Contract {public static void Assert(bool v,string message){if(!v)throw new ArgumentException(message);}public static void Requires(bool v){if(!v)throw new ArgumentException();}public static void Assert(bool v){if(!v)throw new ArgumentException();}}
 public static class Debug {public static void Assert(bool v){if(!v)throw new InvalidOperationException("Difficulty assertion");}}
}
namespace OsuLegacy.Difficulty.Osu {
 public class OsuHitObject:HitObject {public Vector2 Position,EndPosition;public int StackHeight;public Vector2 StackedPosition{get{return Position+new Vector2(StackHeight*(float)(Radius/64)*-6.4f,StackHeight*(float)(Radius/64)*-6.4f);}}public Vector2 StackedEndPosition{get{return EndPosition+new Vector2(StackHeight*(float)(Radius/64)*-6.4f,StackHeight*(float)(Radius/64)*-6.4f);}}public double Radius=32,TimePreempt=1200,TimeFadeIn=400;public const double PREEMPT_MIN=450;}
 public class IBeatmap{public List<OsuHitObject> HitObjects=new List<OsuHitObject>();public int BeatmapVersion;public float StackLeniency;}public static class Vector2Extensions{public static float Distance(Vector2 a,Vector2 b){return (a-b).Length;}}
 public class HitCircle:OsuHitObject{}public class Spinner:OsuHitObject{}
 public class Slider:OsuHitObject {public int RepeatCount;public double Duration;public SliderHeadCircle HeadCircle;public SliderTailCircle TailCircle;public PathData Path=new PathData();public List<OsuHitObject> NestedSliderObjects=new List<OsuHitObject>();public double SpanDuration{get{return Duration/(RepeatCount+1);}}}
 public static class SliderEventGenerator{public const double TAIL_LENIENCY=-36;}
 public class SliderHeadCircle:HitCircle{} public class SliderTailCircle:HitCircle{}public class SliderRepeat:OsuHitObject{}public class SliderTick:OsuHitObject{}
 public class PathData {public double Distance;public Func<double,Vector2> PositionAt;}
}
namespace OsuLegacy.Difficulty.Catch {
 public class PalpableCatchHitObject:HitObject {public float EffectiveX;public bool HyperDash;public float DistanceToHyperDash;}
}
namespace OsuLegacy.Difficulty.Mania {
 public class ManiaHitObject:HitObject {public int Column;}public class HoldNote:ManiaHitObject{}
}
namespace OsuLegacy.Difficulty.Taiko {
 public class TaikoHitObject:HitObject{}public class Hit:TaikoHitObject{public bool IsRim;public bool IsStrong;public HitType Type{get{return IsRim?HitType.Rim:HitType.Centre;}}}public class DrumRoll:TaikoHitObject{}public class Swell:TaikoHitObject{}
}
namespace System {public struct ValueTuple<T1,T2>{public T1 Item1;public T2 Item2;public ValueTuple(T1 a,T2 b){Item1=a;Item2=b;}}}
namespace System.Runtime.CompilerServices {[AttributeUsage(AttributeTargets.All)]public sealed class TupleElementNamesAttribute:Attribute{public TupleElementNamesAttribute(string[] names){}}}
namespace OsuLegacy.Difficulty.Taiko {
 public enum HitType{Centre,Rim}
 public class TimingControlPoint {public double BPM;}
 public class EffectControlPoint {public double ScrollSpeed;}
 public class ControlPointInfo {public Func<double,double> BeatLengthAt,ScrollSpeedAt;public TimingControlPoint TimingPointAt(double t){return new TimingControlPoint{BPM=60000/BeatLengthAt(t)};}public EffectControlPoint EffectPointAt(double t){return new EffectControlPoint{ScrollSpeed=ScrollSpeedAt(t)};}}
}
