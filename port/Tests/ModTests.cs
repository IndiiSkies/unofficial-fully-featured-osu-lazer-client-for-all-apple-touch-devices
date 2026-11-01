using System;using System.IO;using OsuLegacy;using osu.Game.Rulesets.Scoring;
public static class ModTests {
 static int checks;static void Check(bool b,string s){checks++;if(!b)throw new Exception(s);}
 static LegacyModState State(params string[] mods){var s=new LegacyModState();s.Seed=123;foreach(string a in mods){var m=s.All.Find(delegate(LegacyMod v){return v.Acronym==a;});s.Toggle(m);}return s;}
 static Beatmap Map(int mode){var m=new Beatmap {Mode=mode};for(int i=0;i<4;i++)m.Objects.Add(new Circle {X=80+i*80,Y=80+i*50,Time=1000+i*500,Column=i,ComboNumber=i+1});return m;}
 static Session Session(int mode,params string[] mods){var map=Map(mode);State(mods).Capture(mode).Apply(map);return new Session(map);}
 public static int Main(){
  foreach(var mod in new LegacyModState().All)for(int mode=0;mode<4;mode++){
   var state=State(mod.Acronym);if(!state.AvailableFor(mod,mode))continue;var map=Map(mode);var config=state.Capture(mode);config.Apply(map);var s=new Session(map);Check(s.Mods.Has(mod.Acronym),mod.Acronym+" snapshot mode "+mode);s.Advance(0);s.Advance(1000);s.Advance(1100);var surface=new SvgUiSurface("port/Assets");var view=new GameplayView{ReducedEffects=true,CursorVisible=true};view.Draw(surface,s,1150,480,320);string preview=Path.Combine(Path.GetTempPath(),"osu-mod-render.svg");surface.Save(preview);string svg=File.ReadAllText(preview);File.Delete(preview);Check(!svg.Contains("NaN") && !svg.Contains("Infinity") && svg.Split(new string[]{"<g "},StringSplitOptions.None).Length==svg.Split(new string[]{"</g>"},StringSplitOptions.None).Length,mod.Acronym+" balanced finite render mode "+mode);Check(!Double.IsNaN(s.TotalScore),mod.Acronym+" finite score");
  }
  var hr=Session(0,"HR");Check(hr.Map.OD==7 && Math.Abs(hr.Map.CS-6.5)<.001 && hr.Map.Objects[0].Y==304,"HR difficulty and vertical mirror");
  var ez=Session(0,"EZ");Check(ez.Map.CS==2.5 && ez.Map.OD==2.5 && ez.Radius>Session(0).Radius,"EZ difficulty");
  var nf=Session(0,"NF");nf.TouchDown(1,80,80,1000);Check(nf.TotalScore==150,"NF multiplier applied once");
  var sd=Session(0,"SD");sd.Advance(1200);Check(sd.Failed,"SD fails on miss");
  var pf=Session(0,"PF");pf.TouchDown(1,80,80,1060);Check(pf.Failed,"PF fails on non-perfect hit");
  var ac=Session(0,"AC");ac.TouchDown(1,80,80,1060);Check(ac.Failed,"AC target enforced");
  foreach(int mode in new int[]{0,1,2,3})foreach(string a in new string[]{"AT","CN"}){var s=Session(mode,a);for(double t=0;t<=3200;t+=10)s.Advance(t);Check(s.Finished && s.Accuracy==100,a+" perfect automation mode "+mode);Check(!s.Mods.Settings.RecordScore,"automated plays do not overwrite bests");}
  var relax=Session(0,"RX");relax.Mods.AimX=80;relax.Mods.AimY=80;relax.Advance(1000);Check(relax.Great==1,"Relax aims without tapping");
  var pilot=Session(0,"AP");pilot.TouchDown(1,0,0,1000);Check(pilot.Great==1,"Autopilot aims but requires tapping");
  var mirror=Session(0,"MR");Check(mirror.Map.Objects[0].X==432,"Mirror positions");
  var random=Session(0,"RD");var random2=Session(0,"RD");Check(random.Map.Objects[0].X!=80 && random.Map.Objects[0].X==random2.Map.Objects[0].X,"Random repeatable seed");
  foreach(string a in new string[]{"WG","TR","SI","GR","DF","DP"}){var s=Session(0,a);var v=s.Mods.Object(s.Map.Objects[0],500);Check(v.X!=80 || v.Y!=80 || v.Scale!=1 || v.Rotation!=0,a+" transforms objects");var pt=s.Mods.Position(s.Map.Objects[0],new PathPoint(80,80),500);Check(Math.Abs(pt.X-v.X)<.001 && Math.Abs(pt.Y-v.Y)<.001,a+" hit testing follows rendering");}
  foreach(string a in new string[]{"MG","RP"}){var s=Session(0,a);s.Mods.AimX=450;s.Mods.AimY=300;s.Mods.Object(s.Map.Objects[0],100);var v=s.Mods.Object(s.Map.Objects[0],600);Check(v.X!=80,a+" responds to cursor");}
  var hidden=Session(0,"HD");Check(hidden.Mods.Object(hidden.Map.Objects[0],1000).Alpha==0 && hidden.Mods.Object(hidden.Map.Objects[0],500).Approach==0,"Hidden fade and no approach");
  var grow=Session(0,"GR");Check(Math.Abs(grow.Mods.Object(grow.Map.Objects[0],1000).Scale-1)<.001,"Grow finishes at actual hit radius");
  var freeze=Session(0,"FR");Check(freeze.VisibleEnd(0)==4,"FreezeFrame shows entire combo");
  var bloom=Session(0,"BM");bloom.Combo=50;Check(bloom.Mods.CursorScale==10,"Bloom cursor scale");
  var scope=Session(0,"NS");scope.Combo=10;Check(scope.Mods.CursorAlpha(1000)==0,"NoScope cursor fades");
  var muted=Session(0,"MU");muted.Combo=100;Check(muted.Mods.MusicGain==0,"Muted gain");
  var al=Session(0,"AL");Check(al.Mods.AcceptAction(0,1000) && !al.Mods.AcceptAction(0,1200) && al.Mods.AcceptAction(1,1400),"Alternate action identity");
  var sg=Session(0,"SG");Check(sg.Mods.AcceptAction(0,1000) && sg.Mods.AcceptAction(0,1200) && !sg.Mods.AcceptAction(1,1400),"SingleTap action identity");
  var adjustable=State("DA");adjustable.DACs=8;adjustable.DAAr=9;var adjusted=Map(0);adjustable.Capture(0).Apply(adjusted);Check(adjusted.CS==8 && adjusted.AR==9,"DA settings applied");
  var classic=Session(3,"CL");Check(classic.Mania.Windows[(int)HitResult.Perfect]==16.5,"Classic mania hit windows from source");
  var dt=State("DT");var snapshot=dt.Capture(0);dt.Clear();Check(snapshot.Has("DT") && snapshot.Rate==1.5,"play configuration immutable");
  Check(State("NC").Capture(0).PitchAudio && !State("DT").Capture(0).PitchAudio,"speed and pitch distinguished");
  Check(Session(3,"DT").Mania.Windows[1]>Session(3).Mania.Windows[1],"mania compensates clock rate in hit windows");
  var conflict=State("HT","NC");Check(!conflict.Enabled("HT") && conflict.Enabled("NC"),"speed mutually exclusive");
  var sliderDirectory=Path.Combine(Path.GetTempPath(),"osu-mod-"+Guid.NewGuid());Directory.CreateDirectory(sliderDirectory);var sliderPath=Path.Combine(sliderDirectory,"slider.osu");File.WriteAllBytes(Path.Combine(sliderDirectory,"audio.wav"),new byte[0]);try{
   File.WriteAllText(sliderPath,"osu file format v14\n[General]\nAudioFilename: audio.wav\n[Difficulty]\nOverallDifficulty:5\nCircleSize:5\nSliderMultiplier:1.4\nSliderTickRate:1\n[TimingPoints]\n0,500,4,1,0,100,1,0\n[HitObjects]\n80,80,1000,2,0,L|220:80,1,140\n256,192,2000,8,0,4000\n");
   var sm=Beatmap.Load(sliderPath);State("AT").Capture(0).Apply(sm);var auto=new Session(sm);for(int t=0;t<=4100;t+=10)auto.Advance(t);Check(auto.Accuracy==100 && auto.Finished && auto.SliderTicksMissed==0,"Autoplay tracks sliders and completes spinners");
   var soMap=Beatmap.Load(sliderPath);State("SO").Capture(0).Apply(soMap);var so=new Session(soMap);so.Advance(3000);Check(so.Map.Objects[1].VisualRotation>0,"SpunOut visibly rotates before completion");so.Advance(4000);Check(so.Great==1,"SpunOut completes spinner");
   var mirrored=Beatmap.Load(sliderPath);State("MR").Capture(0).Apply(mirrored);Check(Math.Abs(mirrored.Objects[0].PositionAt(1000).X-432)<.001 && Math.Abs(mirrored.Objects[0].Path.Coordinates[0]-432)<.001,"Mirror transforms slider lookup and GPU vertices together");
  }finally{Directory.Delete(sliderDirectory,true);}
  var catchRelax=Session(2,"RX");catchRelax.Catch.Touch(0,1,240,200,500);Check(Math.Abs(catchRelax.Catch.X-256)<1,"Catch Relax follows touch position");catchRelax.Advance(1000);Check(catchRelax.Miss==1,"Catch Relax does not automatically catch missed fruit");
  var path=Path.GetTempFileName();try{var saved=State("DA","DT");saved.SettingsPath=path;saved.SpeedOverride=1.25;saved.DACs=8;saved.MinimumAccuracy=95;saved.Save();var loaded=new LegacyModState{SettingsPath=path};loaded.Load();Check(loaded.Enabled("DT") && loaded.DACs==8 && loaded.SpeedOverride==1.25 && loaded.MinimumAccuracy==95,"mod settings persist");}finally{File.Delete(path);}
  Console.WriteLine("PASS: "+checks+" mod snapshot, ruleset, automation, difficulty, input, transform, scoring, failure and persistence checks");return 0;
 }
}
