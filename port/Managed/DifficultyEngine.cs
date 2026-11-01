using System;using System.Collections.Generic;
namespace OsuLegacy.Difficulty {
 // Source versions: osu!, catch and taiko 20260706; mania 20241007.
 public static class Engine {
  [ThreadStatic] public static Action Checkpoint;
  public static void Check(){if(Checkpoint!=null)Checkpoint();}
  private static void Process(List<DifficultyHitObject> objects,params Skill[] skills){for(int i=0;i<objects.Count;i++){if((i&31)==0)Check();foreach(var skill in skills)skill.Process(objects[i]);}Check();}
  public static double Standard(List<Osu.OsuHitObject> hits,double rate,Mod[] mods,int version,float leniency){if(hits.Count<2)return 0;Osu.Stacking.ApplyStacking(new Osu.IBeatmap{HitObjects=hits,BeatmapVersion=version,StackLeniency=leniency});
   foreach(var hit in hits){var slider=hit as Osu.Slider;if(slider!=null)foreach(Osu.OsuHitObject nested in slider.NestedHitObjects)nested.StackHeight=slider.StackHeight;}
   var objects=new List<DifficultyHitObject>(hits.Count-1);for(int i=1;i<hits.Count;i++){if((i&31)==0)Check();objects.Add(new Osu.OsuDifficultyHitObject(hits[i],hits[i-1],rate,objects,objects.Count));}
   var aim=new Osu.Aim(mods,true);var speed=new Osu.Speed(mods);var reading=new Osu.Reading(mods);Osu.Flashlight flashlight=null;foreach(var mod in mods)if(mod is OsuModFlashlight)flashlight=new Osu.Flashlight(mods,hits.Count);
   if(flashlight==null)Process(objects,aim,speed,reading);else Process(objects,aim,speed,reading,flashlight);
   double a=Math.Pow(aim.DifficultyValue(),.63)*.02275,s=Math.Sqrt(speed.DifficultyValue())*.0675,r=Math.Sqrt(reading.DifficultyValue())*.0675,f=flashlight==null?0:Math.Sqrt(flashlight.DifficultyValue())*.0675;
   double readingPerf=HarmonicSkill.DifficultyToPerformance(r),flashPerf=Osu.Flashlight.DifficultyToPerformance(f);double cognition=readingPerf<=0?flashPerf:flashPerf<=0?readingPerf:DiffUtils.Norm(1.1,readingPerf,flashPerf*Compat.Clamp(flashPerf/readingPerf,.25,1));
   return Compat.Cbrt(DiffUtils.Norm(1.1,4*Math.Pow(a,3),HarmonicSkill.DifficultyToPerformance(s),cognition)*1.12);
  }
  public static double CatchRating(List<Catch.PalpableCatchHitObject> hits,double rate,float halfWidth){var objects=new List<DifficultyHitObject>(hits.Count);for(int i=1;i<hits.Count;i++){if((i&31)==0)Check();objects.Add(new Catch.CatchDifficultyHitObject(hits[i],hits[i-1],rate,halfWidth,objects,objects.Count));}var movement=new Catch.Movement(new Mod[0]);Process(objects,movement);return Math.Sqrt(movement.DifficultyValue())*4.59;}
  private sealed class ManiaComparer:IComparer<Mania.ManiaHitObject>{public int Compare(Mania.ManiaHitObject a,Mania.ManiaHitObject b){return (int)Math.Round(a.StartTime)-(int)Math.Round(b.StartTime);}}
  public static double ManiaRating(List<Mania.ManiaHitObject> hits,int columns,double rate){var sorted=hits.ToArray();LegacySortHelper<Mania.ManiaHitObject>.Sort(sorted,new ManiaComparer());hits=new List<Mania.ManiaHitObject>(sorted);var objects=new List<DifficultyHitObject>(hits.Count);var perColumn=new List<DifficultyHitObject>[columns];for(int c=0;c<columns;c++)perColumn[c]=new List<DifficultyHitObject>();for(int i=1;i<hits.Count;i++){if((i&31)==0)Check();var o=new Mania.ManiaDifficultyHitObject(hits[i],hits[i-1],rate,objects,perColumn,objects.Count);objects.Add(o);perColumn[o.Column].Add(o);}var strain=new Mania.Strain(new Mod[0],columns);Process(objects,strain);return strain.DifficultyValue()*.018;}
  public static double TaikoRating(List<Taiko.TaikoHitObject> hits,double rate,Taiko.ControlPointInfo timing,double velocity,bool convert,bool relax){var objects=new List<DifficultyHitObject>(hits.Count);var centre=new List<Taiko.TaikoDifficultyHitObject>();var rim=new List<Taiko.TaikoDifficultyHitObject>();var notes=new List<Taiko.TaikoDifficultyHitObject>();for(int i=2;i<hits.Count;i++){if((i&31)==0)Check();objects.Add(new Taiko.TaikoDifficultyHitObject(hits[i],hits[i-1],rate,objects,centre,rim,notes,objects.Count,timing,velocity));}Taiko.TaikoColourDifficultyPreprocessor.ProcessAndAssign(objects);Taiko.TaikoRhythmDifficultyPreprocessor.ProcessAndAssign(notes);var mods=new Mod[0];var rhythm=new Taiko.Rhythm(mods);var reading=new Taiko.Reading(mods);var colour=new Taiko.Colour(mods);var stamina=new Taiko.Stamina(mods,false,convert);Process(objects,rhythm,reading,colour,stamina);return new Taiko.Aggregation().Calculate(rhythm,reading,colour,stamina,convert,relax);}
 }
}
