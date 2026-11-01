using System;using System.IO;using OsuLegacy;
class CatchTests {
 static int checks;
 static void Check(bool value,string message){checks++;if(!value)throw new Exception(message);}
 static Beatmap Map(){return new Beatmap {Mode=2,SourcePath="catch-test.osu",Title="Catch first steps",Artist="Generated click track"};}
 static Beatmap Fruits(int count){var map=Map();for(int i=0;i<count;i++)map.Objects.Add(new Circle {X=256,Y=192,Time=1000+i*200});return map;}
 static void Main(string[] args){
  var session=new Session(Fruits(100));Check(Math.Abs(session.Catch.CatchWidth-85.4)<1e-8,"Catcher uses source CS5 width and 0.8 allowed range");
  for(int i=0;i<100;i++)session.Advance(1000+i*200);
  Check(session.Finished && session.Great==100 && session.MaxCombo==100 && session.Accuracy==100,"Perfect catch resolves every fruit without tapping");
  Check(session.TotalScore==1000000,"Perfect no-banana run reaches lazer's normalized one-million score");
  var missed=new Session(Fruits(100));for(int i=0;i<100;i++){missed.Catch.X=i==24 || i==49?0:256;missed.Advance(1000+i*200);}
  Check(missed.Miss==2 && missed.MaxCombo==50 && missed.Accuracy==98 && LocalScoreStore.CatchRank(missed.Accuracy)=="S","Catch rank uses 98% S cutoff even with fruit misses");
  double part=0,maximum=0;int combo=0;for(int i=0;i<100;i++){maximum+=300*Math.Min(Math.Max(.5,Math.Log(i+1,4)),Math.Log(200,4));if(i==24 || i==49)combo=0;else {combo++;part+=300*Math.Min(Math.Max(.5,Math.Log(combo,4)),Math.Log(200,4));}}
  Check(missed.TotalScore==(int)Math.Round(1000000*part/maximum),"Missed-fruit score matches upstream CatchScoreProcessor's capped logarithmic combo formula");
  var roundedMap=Fruits(100);roundedMap.ApplyCatchMods("HR");var rounded=new Session(roundedMap);
  for(int i=0;i<100;i++){rounded.Catch.X=i==24 || i==49?0:rounded.Catch.Objects[i].X;rounded.Advance(1000+i*200);}
  Check(rounded.TotalScore==(int)Math.Round(Math.Round(1000000*part/maximum)*1.12),"Mod score follows source's separate base-score and multiplier rounding");
  var boundary=new Session(Fruits(1));boundary.Catch.X=256+boundary.Catch.CatchWidth/2;boundary.Advance(1000);Check(boundary.Great==1,"Catch width includes its exact boundary");
  boundary=new Session(Fruits(1));boundary.Catch.X=256+boundary.Catch.CatchWidth/2+.001;boundary.Advance(1000);Check(boundary.Miss==1,"Fruit just outside catch width misses");
  var control=new Session(Fruits(1));control.Catch.Touch(0,1,1,220,0);control.Advance(300);Check(control.Catch.X==106,"Touch lower-left walks at 0.5 units/ms");
  control.Catch.Touch(1,1,1,100,300);control.Advance(400);Check(control.Catch.X==6,"Sliding into upper-left dashes at 1 unit/ms");
  control.Catch.Touch(0,2,479,100,400);control.Advance(500);Check(control.Catch.X==6 && control.Catch.Direction==0,"Simultaneous left and right cancel movement");
  control.Catch.Touch(2,1,0,0,500);control.Advance(600);Check(control.Catch.X==106,"Releasing one touch retains the opposite direction");
  control.ReleaseAll(600);control.Advance(700);Check(control.Catch.X==106 && !control.Catch.Dashing,"Pause/release clears all held catch controls");
  var stream=Map();stream.Objects.Add(new Circle {Kind=ObjectKind.Slider,X=256,Y=192,Time=1000,Duration=1000,SpanDuration=1000,SpanCount=1,Velocity=.14,TickDistance=70,Path=SliderPath.Parse("L|396:192",256,192,140)});
  var tiny=new Session(stream);foreach(CatchObject obj in tiny.Catch.Objects){tiny.Catch.X=obj.Kind==CatchKind.Tiny?0:obj.X;tiny.Advance(obj.Time);}
  Check(tiny.Finished && tiny.Catch.TinyMiss>0 && tiny.Miss==0 && tiny.Combo==tiny.Catch.PossibleCombo && tiny.Accuracy<100,"Tiny misses lower accuracy without breaking combo or increasing primary misses");
  Check(tiny.TotalScore<1000000 && tiny.Great==2 && tiny.Ok==1,"Fruit and droplet combo portions are separate from tiny-droplet score");
  var shower=Map();shower.Objects.Add(new Circle {Kind=ObjectKind.Spinner,Time=1000,Duration=1000});var bananas=new Session(shower);
  Check(bananas.Catch.Objects.Count==17,"Banana shower halves spacing to <=100ms and includes endpoints");
  foreach(CatchObject obj in bananas.Catch.Objects){bananas.Catch.X=obj.X;bananas.Advance(obj.Time);}
  Check(bananas.Finished && bananas.Catch.BananaHit==17 && bananas.Accuracy==100 && bananas.Combo==0 && bananas.BonusScore==3400,"Bananas award 200 bonus each without changing accuracy or combo");
  var hyperMap=Map();hyperMap.Objects.Add(new Circle {X=0,Time=369});hyperMap.Objects.Add(new Circle {X=512,Time=450});var hyper=new Session(hyperMap);
  Check(hyper.Catch.Objects[0].HyperTarget==1,"Source hyperdash detection marks an impossible normal dash");
  hyper.Catch.X=0;hyper.Advance(369);Check(hyper.Catch.HyperMultiplier>1,"Catching a hyperfruit activates the source velocity boost");
  hyper.Catch.Touch(0,1,479,100,369);hyper.Advance(450);Check(hyper.Great==2 && hyper.Catch.X==512 && hyper.Catch.HyperMultiplier==1,"Hyperdash reaches target, clamps overshoot and resets after target catch");
  var retry=new Session(stream);Check(retry.Catch.Next==0 && retry.Catch.X==256 && retry.Catch.Plate.Count==0,"Retry creates a fresh catcher and nested judgements");
  var result=new ScoreResult(tiny,"None",null);Check(result.Catch && result.PossibleCombo==3 && result.TinyMiss==tiny.Catch.TinyMiss && result.FullCombo,"Catch results preserve tiny misses and combo maximum");
  var store=new LocalScoreStore(Path.Combine(args[1],"catch-scores.tsv"));store.Record(session);Check(store.Scores.ContainsKey(session.ScoreKey) && !store.Scores.ContainsKey(session.Map.SourcePath),"Catch personal bests are isolated from standard scores on the same map");
  var visual=new Session(stream);var canvas=new SvgUiSurface(args[0]);new GameplayView().Draw(canvas,visual,1100,480,320);canvas.Save(Path.Combine(args[1],"catch-gameplay.svg"));
  var healthFruit=new Session(Fruits(1));healthFruit.Health=.01;healthFruit.Catch.X=0;healthFruit.Advance(1000);Check(healthFruit.Catch.HasFailed,"Fruit miss at depleted HP fails as in source health tests");
  var healthTiny=new Session(stream);healthTiny.Advance(1000);healthTiny.Health=0;healthTiny.Catch.X=0;
  CatchObject firstTiny=healthTiny.Catch.Objects.Find(delegate(CatchObject obj){return obj.Kind==CatchKind.Tiny;});healthTiny.Advance(firstTiny.Time);
  Check(!healthTiny.Catch.HasFailed && healthTiny.Catch.TinyMiss==1,"A tiny-droplet miss cannot trigger failure at zero health");
  var hiddenMap=Fruits(1);hiddenMap.ApplyCatchMods("HD");var hidden=new Session(hiddenMap);hidden.Advance(1000);Check(hidden.TotalScore==1060000 && hidden.Catch.Plate.Count==0,"Hidden uses source score multiplier and disables fruit on plate");
  var easyMap=Fruits(4);easyMap.ApplyCatchMods("EZ");var easy=new Session(easyMap);easy.Catch.X=0;
  for(int i=0;i<3;i++){easy.Health=.001;easy.Advance(1000+i*200);}
  Check(easy.Catch.Lives==0 && easy.Catch.HasFailed,"Easy grants two extra lives then allows failure");
  var sdMap=Fruits(1);sdMap.ApplyCatchMods("SD");var sd=new Session(sdMap);sd.Catch.X=0;sd.Advance(1000);Check(sd.Catch.HasFailed,"Sudden Death fails after a fruit miss");
  var hrMap=Fruits(1);hrMap.ApplyCatchMods("HR");Check(hrMap.CatchHardRock && Math.Abs(hrMap.CS-6.5)<.001 && Math.Abs(hrMap.AR-7)<.001 && hrMap.CatchScoreMultiplier==1.12,"Catch Hard Rock uses custom CS 1.3 and score 1.12 ratios");
  var frameMap=Map();frameMap.Objects.Add(new Circle {Time=100,X=300});frameMap.Objects.Add(new Circle {Time=110,X=310});var frame=new Session(frameMap);frame.Catch.Touch(0,1,479,100,0);frame.Advance(110);
  Check(frame.Catch.X==366 && frame.Great==0,"Catcher moves once before the frame's due objects are judged, matching source ordering");
  var stats=new SvgUiSurface(args[0]);var resultScreen=new ResultsScreen(new ScoreResult(tiny,"None",null),0,false);resultScreen.Tap(200,295);resultScreen.Draw(stats,4);stats.Save(Path.Combine(args[1],"catch-results.svg"));
  Console.WriteLine("PASS: "+checks+" catch simulation, controls, scoring, conversion and results checks");
 }
}
