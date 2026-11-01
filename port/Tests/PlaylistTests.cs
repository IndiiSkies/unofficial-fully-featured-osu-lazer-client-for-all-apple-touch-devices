using System;using System.IO;using System.Collections.Generic;using OsuLegacy;
class PlaylistTests
{
 static int checks;static void Check(bool valid,string message){checks++;if(!valid)throw new Exception(message);}
 static void Main(string[] args)
 {
  string root=Path.Combine(Path.GetTempPath(),"osu-playlist-"+Guid.NewGuid());Directory.CreateDirectory(root);
  try {
   string theme=Path.Combine(root,"theme.m4a");var songs=new List<SongEntry>();
   for(int i=0;i<3;i++)songs.Add(new SongEntry {AudioPath=Path.Combine(root,"track"+i+".mp3"),Title="Track "+i,Artist="Test"});
   songs.Add(new SongEntry {AudioPath=songs[0].AudioPath,Title="Duplicate difficulty"});
   songs.Add(new SongEntry {AudioPath=Path.Combine(root,"unsupported.xyz"),Title="Unsupported"});
   songs.Add(new SongEntry {AudioPath=Path.Combine(root,"demo.wav"),Artist="Generated click track"});
   var music=new MenuPlaylist(root,theme);music.SettingsPath=Path.Combine(root,"menu.cfg");string playing="";int starts=0;double seek=-1;bool paused=false;
   music.Start=delegate(MenuTrack track){playing=track.Path;starts++;return true;};music.SetPaused=delegate(bool value){paused=value;};music.Seek=delegate(double value){seek=value;};
   music.Refresh(songs);Check(music.Library.Count==4,"One track per audio file; skip unsupported and generated demo");
   Check(playing==songs[0].AudioPath,"Imported music becomes the default background track");
   music.Add(music.Library[1]);music.Add(music.Library[2]);music.Add(music.Library[1]);Check(music.Custom.Count==2,"Add is deduplicated");
   music.PlayAt(0,true);music.Advance(false);Check(music.Current==music.Custom[1],"Next follows custom queue");
   music.Position=9;music.Previous();Check(seek==0,"Previous restarts after three seconds");music.Position=0;music.Previous();Check(music.Current==music.Custom[0],"Previous follows playback history");
   music.Toggle();Check(music.Paused && paused,"Pause reaches backend");music.Toggle();Check(!music.Paused && !paused,"Resume reaches backend");
   music.Duration=100;music.Position=99;music.Skip(10);Check(seek<100 && seek>99,"Seek clamps before EOF");music.Position=2;music.Skip(-10);Check(seek==0,"Seek clamps at zero");
   music.Move(1,-1);Check(music.Custom[0].Path==songs[1].AudioPath,"Reorder updates saved playlist");
   music.Repeat=MusicRepeat.One;string before=music.Current.Key;music.Advance(true);Check(music.Current.Key==before,"Repeat one replays same track");
   music.Repeat=MusicRepeat.Off;music.PlayAt(1,true);music.Advance(true);Check(music.Paused,"Repeat off stops at queue end");
   music.Repeat=MusicRepeat.All;music.Advance(false);Check(!music.Paused && music.Current==music.Custom[0],"Manual next wraps and resumes");
   music.PlayAt(0,false);music.Shuffle=true;music.Repeat=MusicRepeat.Off;var visited=new Dictionary<string,bool>();visited[music.Current.Key]=true;
   for(int i=0;i<music.Library.Count-1;i++){music.Advance(true);Check(!visited.ContainsKey(music.Current.Key),"Shuffle does not repeat within cycle");visited[music.Current.Key]=true;}
   music.Advance(true);Check(music.Paused,"Shuffle with repeat off stops after cycle");
   music.Shuffle=false;music.PlayAt(0,true);music.Save();var restored=new MenuPlaylist(root,theme);restored.SettingsPath=music.SettingsPath;restored.Load();restored.Start=delegate(MenuTrack track){return true;};restored.Refresh(songs);
   Check(restored.Custom.Count==2 && restored.Custom[0].Key==music.Custom[0].Key,"Custom order survives restart");Check(restored.UseCustom && restored.Current.Key==music.Current.Key,"Queue mode/current choice survive restart");
   restored.Remove(0);Check(restored.Custom.Count==1 && restored.Current.Key==restored.Custom[0].Key,"Remove current chooses remaining track");restored.Remove(0);Check(restored.Current==null && restored.Paused,"Empty queue stops safely");
   var empty=new MenuPlaylist(root,theme);int themeStarts=0;empty.Start=delegate(MenuTrack track){themeStarts++;return true;};empty.Refresh(new SongEntry[0]);Check(themeStarts==0,"Library refresh does not replay welcome voice");
   var streaming=new MenuPlaylist(root,theme);int streamStarts=0;streaming.Start=delegate(MenuTrack track){streamStarts++;return true;};
   streaming.Append(new SongEntry[]{songs[0]});streaming.AdoptSelection(songs[0]);streaming.Append(new SongEntry[]{songs[1],songs[0]});
   Check(streaming.Current.Title==songs[0].Title && streaming.Library.Count==3 && streamStarts==0,"Streaming import exposes current song immediately without restarting or duplicating audio");
   streaming.Refresh(songs);Check(streaming.Current.Path==songs[0].AudioPath && streamStarts==0,"Final import refresh preserves a browser-selected song");
   var ui=new LazerUi();ui.Music=music;var surface=new SvgUiSurface(args[0]);ui.Draw(surface,0);ui.MenuPointer(0,240,160);ui.Draw(new SvgUiSurface(args[0]),0);ui.Tap(315,17);Check(ui.Screen==UiScreen.Music,"Toolbar music icon opens now-playing overlay beside notification bell");
   ui.Draw(new SvgUiSurface(args[0]),0);ui.Tap(410,17);Check(ui.Screen==UiScreen.Home,"Close button returns from now-playing overlay without overlapping bell");
   ui.Songs.Add(new SongEntry {Path="browse0",Title="Browse zero",AudioPath=songs[0].AudioPath});
   ui.Songs.Add(new SongEntry {Path="browse1",Title="Browse one",AudioPath=songs[1].AudioPath});
   music.PlayAt(2,false);
   ui.OpenSongs();ui.Draw(new SvgUiSurface(args[0]),0);Check(ui.SongBrowserAudio && ui.PreviewSong==ui.Songs[1],"Solo preserves the current valid working track");
   ui.SelectBrowserSong(0);ui.Draw(new SvgUiSurface(args[0]),0);
   ui.Tap(300,160);ui.Draw(new SvgUiSurface(args[0]),0);Check(ui.PreviewSong==ui.Songs[1],"Selecting a song changes preview source");
   int beforeAdoption=starts;music.AdoptSelection(ui.PreviewSong);
   Check(music.Current.Path==ui.PreviewSong.AudioPath && starts==beforeAdoption,"Browser updates shared playlist metadata without starting a second player");
   ui.ShowNotice("Import","Complete");Check(ui.SongBrowserAudio && ui.PreviewSong==ui.Songs[1],"Import notice retains browser audio context");
   ui.Screen=UiScreen.Songs;ui.Draw(new SvgUiSurface(args[0]),0);ui.Tap(30,260);Check(!ui.SongBrowserAudio && ui.Screen==UiScreen.Play,"Back restores title playlist context");
   File.WriteAllText(songs[0].AudioPath,"fixture");string metadata=Path.Combine(root,"preview.osu");
   File.WriteAllText(metadata,"[General]\nAudioFilename: track0.mp3\nPreviewTime: 12345\n[Metadata]\nTitle: Preview fixture\n");
   SongEntry entry=SongEntry.Read(metadata);Check(entry.PreviewTime==12345 && entry.AudioPath==songs[0].AudioPath,"Metadata preserves preview milliseconds and resolved audio path");
   Console.WriteLine("PASS: "+checks+" playlist dedupe, controls, shuffle/repeat, persistence, removal and UI checks");
  } finally {Directory.Delete(root,true);}
 }
}
