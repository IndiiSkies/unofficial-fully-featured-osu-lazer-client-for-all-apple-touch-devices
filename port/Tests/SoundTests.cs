using System;
using System.IO;
using System.Collections.Generic;
using OsuLegacy;
class SoundTests
{
    class Output : ISoundOutput,IPitchedSoundOutput
    {
        public readonly List<string> Paths=new List<string>();
        public readonly List<int> Played=new List<int>();
        public readonly List<double> Gains=new List<double>();
        public readonly int[] Loops=new int[4];
        public readonly List<double> Pitches=new List<double>();
        public void PlayPitched(int id,double volume,double pitch){Pitches.Add(pitch);Play(id,volume);}
        public int Load(string path){Paths.Add(path);return Paths.Count;}
        public void Play(int id,double volume){Played.Add(id);Gains.Add(volume);}
        public void Loop(int slot,int id,double volume){Loops[slot]=id;}
        public string Last { get {return Paths[Played[Played.Count-1]-1];} }
        public void Clear(){Played.Clear();Gains.Clear();}
    }
    static int checks;
    static void Check(bool valid,string name){checks++;if(!valid)throw new Exception(name);}
    static void Main()
    {
        string root=Path.Combine(Path.GetTempPath(),"osu-sounds-"+Guid.NewGuid());Directory.CreateDirectory(root);
        try
        {
            File.WriteAllText(Path.Combine(root,"audio.wav"),"audio");
            File.WriteAllText(Path.Combine(root,"soft-hitnormal2.wav"),"custom");
            File.WriteAllText(Path.Combine(root,"custom.wav"),"custom");
            string file=Path.Combine(root,"map.osu");
            File.WriteAllText(file,"osu file format v14\n[General]\nAudioFilename: audio.wav\nSampleSet: Normal\n[TimingPoints]\n0,500,4,1,0,80,1,0\n2000,-100,4,2,2,35,0,0\n[HitObjects]\n100,100,1000,1,14,1:3:0:60:\n100,100,2500,1,0,0:0:0:0:\n100,100,3000,1,0,0:0:0:0:custom.wav\n100,192,4000,2,2,L|380:192,2,280,0|8|4,1:0|2:3|3:1,0:0:0:0:\n256,192,7000,8,0,9000,0:0:0:0:\n");
            Beatmap map=Beatmap.Load(file);var output=new Output();var sound=new HitSounds(map,output,root);sound.Prepare();
            Check(map.Objects[0].HitSound==14 && map.Objects[0].Sample.AdditionSet==3,"Hit masks and object sample sets parsed");
            Check(map.Objects[3].EdgeSounds[1]==8 && map.Objects[3].EdgeSamples[1].Set==2,"Slider per-edge sample data parsed");
            Check(sound.PointAt(1999).Volume==80 && sound.PointAt(2000).Volume==35,"Timing sample volume changes at boundary");
            int loaded=output.Paths.Count;
            var session=new Session(map);session.Sound=sound.Play;
            session.TouchDown(1,100,100,1000);
            Check(output.Played.Count==4,"Normal plus three additions overlap");
            Check(Path.GetFileName(output.Paths[output.Played[0]-1])=="normal-hitnormal.wav" && Path.GetFileName(output.Last)=="drum-hitclap.wav","Normal and addition banks resolve independently");
            Check(output.Gains.TrueForAll(delegate(double value){return value==.6;}),"Per-object volume overrides control point");
            output.Clear();session.TouchDown(2,100,100,2500);
            Check(output.Last==Path.Combine(root,"soft-hitnormal2.wav") && output.Gains[0]==.35,"Timing sample index, bank and volume select custom file");
            output.Clear();session.TouchDown(3,100,100,3000);
            Check(output.Last==Path.Combine(root,"custom.wav"),"Explicit custom filename overrides normal sample");
            output.Clear();session.TouchDown(4,100,192,4000);
            Check(output.Played.Count==1 && Path.GetFileName(output.Last)=="normal-hitnormal.wav","Slider head uses edge mask and bank");
            sound.Update(session,4000);Check(output.Loops[0]!=0 && output.Loops[1]!=0,"Tracked slider has independent slide and whistle loops");
            output.Clear();session.TouchMove(4,240,192,4500);
            Check(output.Played.Count==1 && Path.GetFileName(output.Last)=="soft-slidertick.wav","Slider tick uses its timing bank");
            output.Clear();session.TouchMove(4,380,192,5000);
            Check(output.Played.Count==2 && Path.GetFileName(output.Last)=="drum-hitclap.wav","Repeat uses its own addition mask and bank");
            session.TouchUp(4,380,192,5010);sound.Update(session,5010);
            Check(output.Loops[0]==0 && output.Loops[1]==0,"Released slider stops sustained effects");
            output.Clear();session.TouchMove(4,100,192,6000);session.Advance(6200);
            Check(output.Played.Count==0,"Missed checkpoints do not play hit effects");
            Check(output.Paths.Count==loaded,"Gameplay performs no file loads");
            output.Clear();session.TouchDown(5,356,192,7000);
            for(int i=1;i<=320;i++){double a=i*Math.PI*2*10/320;session.TouchMove(5,256+100*Math.Cos(a),192+100*Math.Sin(a),7000+i*1990.0/320);}
            Check(output.Played.Count>0 && Path.GetFileName(output.Last)=="spinnerbonus.wav","Spinner bonus is emitted during rotation");
            sound.Update(session,8990);Check(output.Loops[2]!=0,"Moving spinner plays sustained effect");
            session.TouchUp(5,356,192,8991);sound.Update(session,8991);Check(output.Loops[2]==0,"Spinner lift stops effect");
            output.Clear();session.Advance(9000);Check(output.Played.Count==1,"Successful spinner end plays its hit sample once");session.Advance(10000);Check(output.Played.Count==1,"Resolved objects do not replay effects");
            var missing=new Circle {Time=11000,Sample=HitSample.Parse("1:0:9:0:../outside.wav")};
            map.Objects.Add(missing);sound.Prepare();output.Clear();sound.Play(missing,"hit",0);
            Check(Path.GetFileName(output.Last)=="normal-hitnormal.wav","Missing or escaping custom path falls back to bundled sample");
            var missMap=new Beatmap();missMap.Objects.Add(new Circle {Time=0,X=100,Y=100});var missSession=new Session(missMap);missSession.Combo=20;int breaks=0;missSession.Sound=delegate(Circle obj,string kind,int edge){if(kind=="combobreak")breaks++;};missSession.Advance(1000);
            Check(breaks==1,"Combo break effect fires once at miss");
            var cancelledOutput=new Output();var cancelledSound=new HitSounds(map,cancelledOutput,root);
            Check(!cancelledSound.Prepare(delegate {return true;}) && cancelledOutput.Paths.Count==0,"Cancelled preparation performs no sample loads");
            Check(!cancelledSound.Prepare(delegate {return cancelledOutput.Paths.Count>=3;}) && cancelledOutput.Paths.Count==3,"Cancellation stops preparation between sample loads");
            var nextOutput=new Output();var nextSound=new HitSounds(map,nextOutput,root);
            Check(nextSound.Prepare(delegate {return false;}) && nextOutput.Paths.Count>3,"A fresh song can prepare after cancellation");
            var taikoMap=new Beatmap {Mode=1};taikoMap.Objects.Add(new Circle {Kind=ObjectKind.Slider,Time=0,Duration=1000});
            var taikoOutput=new Output();var taikoSound=new HitSounds(taikoMap,taikoOutput,root);taikoSound.Prepare();
            taikoSound.Update(new Session(taikoMap),500);Check(taikoOutput.Loops[0]==0 && taikoOutput.Loops[2]==0,"Taiko rolls do not access standard slider meshes or sustained spinner loops");
            map.Mode=2;var catchOutput=new Output();var catchSounds=new HitSounds(map,catchOutput,root);catchSounds.Prepare();var catchSession=new Session(map);catchSession.Sound=catchSounds.Play;
            foreach(CatchObject obj in catchSession.Catch.Objects){catchSession.Catch.X=obj.X;catchSession.Advance(obj.Time);}
            Check(catchOutput.Played.Count>0 && catchOutput.Pitches.Count>0,"Catch fruit/droplet/banana samples play without standard slider event-index access");
            catchSounds.Play(map.Objects[0],"catch-resume-countdown",0);Check(catchOutput.Pitches[catchOutput.Pitches.Count-1]==.5,"Catch resume final sound uses source half-frequency cue");
            Console.WriteLine("PASS: "+checks+" hitsound parsing, selection, event, loop and preload checks");
        }
        finally {Directory.Delete(root,true);}
    }
}
