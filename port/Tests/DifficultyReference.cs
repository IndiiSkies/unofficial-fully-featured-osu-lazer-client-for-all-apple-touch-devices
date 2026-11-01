using System;using System.Collections.Generic;using OsuLegacy.Difficulty;
class DifficultyReference {
 static void Main(){for(int variant=0;variant<5;variant++){
 var standard=new List<OsuLegacy.Difficulty.Osu.OsuHitObject>();var catchHits=new List<OsuLegacy.Difficulty.Catch.PalpableCatchHitObject>();var mania=new List<OsuLegacy.Difficulty.Mania.ManiaHitObject>();var taiko=new List<OsuLegacy.Difficulty.Taiko.TaikoHitObject>();double rate=variant==1?1.5:variant==2?.75:1;
 for(int i=0;i<257;i++){double t=1000+i*(variant==4?62.5:150)+(i%5)*7;float x=64+(i%4)*128,y=64+(i%3)*128;standard.Add(new OsuLegacy.Difficulty.Osu.HitCircle{StartTime=t,EndTime=t,Position=new Vector2(x,y),EndPosition=new Vector2(x,y),Radius=36.48,TimePreempt=750});catchHits.Add(new OsuLegacy.Difficulty.Catch.PalpableCatchHitObject{StartTime=t,EndTime=t,EffectiveX=x,HyperDash=i%9==0,DistanceToHyperDash=i%17});mania.Add(new OsuLegacy.Difficulty.Mania.ManiaHitObject{StartTime=t,EndTime=t+(i%3==0?200:0),Column=i%4});taiko.Add(new OsuLegacy.Difficulty.Taiko.Hit{StartTime=t,EndTime=t,IsRim=i%3==0});}
 Mod[] mods=variant==3?new Mod[]{new OsuModHidden(),new OsuModFlashlight()}:new Mod[0];double a=Engine.Standard(standard,rate,mods,14,.7f),b=Engine.TaikoRating(taiko,rate,new OsuLegacy.Difficulty.Taiko.ControlPointInfo{BeatLengthAt=t=>500,ScrollSpeedAt=t=>1},1.4,false,false),c=Engine.CatchRating(catchHits,rate,42.7f),d=Engine.ManiaRating(mania,4,rate);Console.WriteLine("{0:R}\t{1:R}\t{2:R}\t{3:R}",a,b,c,d);
 }}
}
