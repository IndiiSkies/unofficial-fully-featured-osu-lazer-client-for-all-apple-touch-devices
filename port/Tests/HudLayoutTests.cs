using System;using System.IO;using OsuLegacy;
class HudLayoutTests {
 static int checks;static void Check(bool value,string message){checks++;if(!value)throw new Exception(message);}
 static void Main(){
  var skin=new HudSkin();Check(HudSkin.Parse(skin.Serialize()).Serialize()==skin.Serialize(),"Default layout round-trips exactly across all rulesets");
  var score=skin.Layouts[0].Items.Find(delegate(HudItem item){return item.Kind=="score";});score.X=-80;score.Y=60;score.Scale=1.5;score.Angle=15;score.Opacity=.6;
  var restored=skin.Copy();var restoredScore=restored.Layouts[0].Find(score.Id);Check(restoredScore.X==-80 && restoredScore.Y==60 && restoredScore.Scale==1.5 && restoredScore.Angle==15 && restoredScore.Opacity==.6,"Position, scale, rotation and opacity survive serialization");
  Check(restored.Layouts[1].Items.Find(delegate(HudItem item){return item.Kind=="score";}).X==0,"Ruleset layouts are isolated");
  var pause=skin.Layouts[0].Items.Find(delegate(HudItem item){return item.Kind=="pause";});pause.X=120;pause.Y=70;pause.Angle=90;
  Check(skin.Layouts[0].Hit("pause",110,80,false,0) && !skin.Layouts[0].Hit("pause",10,10,false,0),"Pause hit testing follows moved and rotated component");
  Check(!skin.Layouts[0].CanRemove(pause),"Last visible pause button is protected");var second=pause.Copy();second.Id=skin.NextId();skin.Layouts[0].Items.Add(second);Check(skin.Layouts[0].CanRemove(pause),"Redundant pause instance may be removed");
  pause.X=-1000;pause.Y=1000;pause.Opacity=0;skin.Layouts[0].KeepPauseReachable(pause,false);var bounds=skin.Layouts[0].DisplayBounds(pause,false);Check(bounds.X>=-.000001 && bounds.Y>=-.000001 && bounds.X+bounds.W<=480.000001 && bounds.Y+bounds.H<=320.000001 && pause.Opacity>=.1,"Pause stays on screen and visible after transforms");
  var history=new HudEditHistory();history.Before(skin);score.X=20;var undo=history.Undo(skin);Check(undo.Layouts[0].Find(score.Id).X==-80 && history.CanRedo,"Undo restores a complete layout snapshot");var redo=history.Redo(undo);Check(redo.Layouts[0].Find(score.Id).X==20,"Redo reapplies layout change");history.Before(redo);Check(!history.CanRedo,"Editing clears redo branch");
  bool rejected=false;try{HudSkin.Parse(skin.Serialize().Replace("\t1.5\t","\tNaN\t"));}catch(FormatException){rejected=true;}Check(rejected,"Non-finite transform values are rejected");
  rejected=false;try{HudSkin.Parse("wrong-header\n");}catch(FormatException){rejected=true;}Check(rejected,"Wrong layout version is rejected");
  var noPause=new HudSkin();noPause.Layouts[0].Items.RemoveAll(delegate(HudItem item){return item.Kind=="pause";});var safe=HudSkin.Parse(noPause.Serialize());Check(safe.Layouts[0].Hit("pause",10,10,false,0),"Missing pause button is restored when reading an external layout");
  string folder=Path.Combine(Path.GetTempPath(),"osu-hud-test-"+Guid.NewGuid());Directory.CreateDirectory(folder);string file=Path.Combine(folder,"hud.tsv");
  try{skin.Save(file);string original=File.ReadAllText(file);Check(HudSkin.Load(file).Serialize()==skin.Serialize(),"Saved layout loads after restart");Directory.CreateDirectory(file+".tmp");score.X=35;bool failed=false;try{skin.Save(file);}catch(IOException){failed=true;}catch(UnauthorizedAccessException){failed=true;}Check(failed && File.ReadAllText(file)==original,"Staging failure preserves previous saved layout");Directory.Delete(file+".tmp");skin.Save(file);Check(File.Exists(file+".bak") && File.ReadAllText(file+".bak")==original && HudSkin.Load(file).Layouts[0].Find(score.Id).X==35,"Successful replacement preserves backup and saves new values");}
  finally{Directory.Delete(folder,true);}
  Console.WriteLine("PASS: "+checks+" HUD layout persistence, transforms, rulesets, undo/redo, validation and failed-save recovery checks");
 }
}
