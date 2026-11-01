using System;
using System.IO;
using System.Text;
using System.Collections.Generic;

namespace OsuLegacy
{
    public sealed class MenuTrack
    {
        public string Key,Path,Title,Artist,ArtworkPath,PlaybackPath;
    }
    public enum MusicRepeat { Off,All,One }
    public sealed class MenuPlaylist
    {
        public readonly List<MenuTrack> Library=new List<MenuTrack>(),Custom=new List<MenuTrack>();
        public MenuTrack Current;
        public bool Paused,Shuffle,UseCustom;
        public MusicRepeat Repeat=MusicRepeat.All;
        public string Error="",SettingsPath;
        public Func<MenuTrack,bool> Start;
        public Action<bool> SetPaused;
        public Action<double> Seek;
        public double Position,Duration;
        private readonly string documents;
        private readonly List<string> storedKeys=new List<string>();
        private readonly List<MenuTrack> history=new List<MenuTrack>();
        private readonly List<string> shuffleBag=new List<string>();
        private readonly Random random=new Random();
        private string preferred;
        private bool refreshed,shuffleCycleStarted;
        public MenuPlaylist(string root,string theme)
        {
            documents=System.IO.Path.GetFullPath(root).TrimEnd(System.IO.Path.DirectorySeparatorChar);
            Current=new MenuTrack {Key="@theme",Path=theme,Title="triangles",Artist="cYsmix"};Library.Add(Current);
        }
        public List<MenuTrack> Queue {get {return UseCustom?Custom:Library;}}
        public void Append(IEnumerable<SongEntry> songs)
        {
            foreach(SongEntry song in songs) {
                if(String.IsNullOrEmpty(song.AudioPath) || !Supported(song.AudioPath) || song.Artist=="Generated click track")continue;
                string path=System.IO.Path.GetFullPath(song.AudioPath);
                if(!path.StartsWith(documents+System.IO.Path.DirectorySeparatorChar,StringComparison.Ordinal))continue;
                if(Library.Exists(delegate(MenuTrack item){return String.Equals(item.Path,path,StringComparison.OrdinalIgnoreCase);}))continue;
                var track=new MenuTrack {Key=path.Substring(documents.Length+1),Path=path,Title=song.Title,Artist=song.Artist,ArtworkPath=song.BackgroundPath,PlaybackPath=song.PlaybackPath};
                Library.Add(track);
            }
            // Rebuild only the saved queue, preserving its original ordering
            // even when metadata arrives in a different scan order.
            Custom.Clear();
            foreach(string key in storedKeys) {
                MenuTrack track=Library.Find(delegate(MenuTrack item){return String.Equals(item.Key,key,StringComparison.OrdinalIgnoreCase);});
                if(track!=null && !Custom.Contains(track))Custom.Add(track);
            }
        }
        public void AdoptSelection(SongEntry song)
        {
            if(song==null || song.AudioPath==null || (Current!=null && Current.Path==song.AudioPath))return;
            Append(new SongEntry[]{song});
            MenuTrack track=Library.Find(delegate(MenuTrack item){return item.Path==song.AudioPath;});
            if(track==null)return;
            refreshed=true;
            Current=track;preferred=track.Key;Paused=false;Position=0;Error="";
            if(UseCustom && !Custom.Contains(track))UseCustom=false;
            Save();
        }
        public static bool Supported(string path)
        {
            string ext=System.IO.Path.GetExtension(path).ToLowerInvariant();
            return ext==".mp3" || ext==".wav" || ext==".m4a" || ext==".aac" || ext==".aif" || ext==".aiff" || ext==".caf" || ext==".ogg" || ext==".oga" || ext==".opus" || ext==".flac" || ext==".wma" || ext==".mp2" || ext==".ape" || ext==".wv" || ext==".mp4" || ext==".mka" || ext==".webm";
        }
        private static string Encode(string value){return Convert.ToBase64String(Encoding.UTF8.GetBytes(value));}
        private static string Decode(string value){return Encoding.UTF8.GetString(Convert.FromBase64String(value));}
        public void Load()
        {
            if(String.IsNullOrEmpty(SettingsPath) || !File.Exists(SettingsPath))return;
            foreach(string line in File.ReadAllLines(SettingsPath))try {
                if(line.StartsWith("track="))storedKeys.Add(Decode(line.Substring(6)));
                else if(line.StartsWith("current="))preferred=Decode(line.Substring(8));
                else if(line=="shuffle=1")Shuffle=true;
                else if(line=="custom=1")UseCustom=true;
                else if(line.StartsWith("repeat=")){int value;if(int.TryParse(line.Substring(7),out value) && value>=0 && value<=2)Repeat=(MusicRepeat)value;}
            }catch(FormatException){}
        }
        public void Save()
        {
            if(String.IsNullOrEmpty(SettingsPath))return;
            var text=new StringBuilder();text.Append("shuffle=").Append(Shuffle?1:0).Append("\ncustom=").Append(UseCustom?1:0).Append("\nrepeat=").Append((int)Repeat).Append('\n');
            if(Current!=null)text.Append("current=").Append(Encode(Current.Key)).Append('\n');
            foreach(string key in storedKeys)text.Append("track=").Append(Encode(key)).Append('\n');
            string stage=SettingsPath+".tmp";File.WriteAllText(stage,text.ToString());
            if(File.Exists(SettingsPath))File.Delete(SettingsPath);File.Move(stage,SettingsPath);
        }
        public void Refresh(IEnumerable<SongEntry> songs)
        {
            MenuTrack theme=Library[0];string currentKey=Current==null?null:Current.Key;
            var map=new Dictionary<string,MenuTrack>(StringComparer.OrdinalIgnoreCase);map[theme.Key]=theme;
            Library.Clear();Library.Add(theme);
            foreach(SongEntry song in songs)
            {
                if(String.IsNullOrEmpty(song.AudioPath) || !Supported(song.AudioPath) || song.Artist=="Generated click track")continue;
                string path=System.IO.Path.GetFullPath(song.AudioPath);
                if(!path.StartsWith(documents+System.IO.Path.DirectorySeparatorChar,StringComparison.Ordinal))continue;
                string key=path.Substring(documents.Length+1);
                if(map.ContainsKey(key))continue;
                var track=new MenuTrack {Key=key,Path=path,Title=song.Title,Artist=song.Artist,ArtworkPath=song.BackgroundPath,PlaybackPath=song.PlaybackPath};map[key]=track;Library.Add(track);
            }
            Custom.Clear();var seen=new Dictionary<string,bool>(StringComparer.OrdinalIgnoreCase);
            foreach(string key in storedKeys){MenuTrack track;if(map.TryGetValue(key,out track) && !seen.ContainsKey(key)){Custom.Add(track);seen[key]=true;}}
            shuffleBag.Clear();history.Clear();
            if(!refreshed) {
                refreshed=true;MenuTrack wanted=null;
                if(preferred!=null)map.TryGetValue(preferred,out wanted);
                if(UseCustom && Custom.Count==0)UseCustom=false;
                if(wanted==null)wanted=UseCustom?Custom[0]:Library[Library.Count>1?1:0];
                if(UseCustom && !Custom.Contains(wanted))wanted=Custom[0];
                if(Current!=null && Current.Key==wanted.Key){Current=wanted;Save();return;}
                if(!PlayTrack(wanted))Advance(false);return;
            }
            MenuTrack replacement;
            if(currentKey!=null && map.TryGetValue(currentKey,out replacement))Current=replacement;
            else if(Queue.Count>0)PlayTrack(Queue[0]);else Pause(true);
        }
        public bool PlayAt(int index,bool custom)
        {
            List<MenuTrack> queue=custom?Custom:Library;
            if(index<0 || index>=queue.Count)return false;
            UseCustom=custom;shuffleBag.Clear();shuffleCycleStarted=false;return PlayTrack(queue[index]);
        }
        private bool PlayTrack(MenuTrack track)
        {
            if(Start!=null && !Start(track)){Error="Cannot play this file on iOS 4: "+track.Title;return false;}
            if(Current!=null && Current.Key!=track.Key && Queue.Exists(delegate(MenuTrack item){return item.Key==Current.Key;})){history.Add(Current);if(history.Count>64)history.RemoveAt(0);}
            Current=track;preferred=track.Key;Paused=false;Position=0;Error="";Save();return true;
        }
        public void Pause(bool value){Paused=value;if(SetPaused!=null)SetPaused(value);Save();}
        public void Toggle(){if(Current==null && Queue.Count>0)PlayTrack(Queue[0]);else Pause(!Paused);}
        public void Advance(bool automatic)
        {
            List<MenuTrack> queue=Queue;if(queue.Count==0){Pause(true);return;}
            if(automatic && Repeat==MusicRepeat.One && Current!=null){PlayTrack(Current);return;}
            int current=Current==null?-1:queue.FindIndex(delegate(MenuTrack item){return item.Key==Current.Key;});
            if(Shuffle) {
                if(shuffleBag.Count==0){if(automatic && Repeat==MusicRepeat.Off && shuffleCycleStarted){Pause(true);return;}shuffleCycleStarted=true;foreach(MenuTrack item in queue)if(Current==null || item.Key!=Current.Key)shuffleBag.Add(item.Key);}
                if(shuffleBag.Count==0){if(automatic && Repeat==MusicRepeat.Off)Pause(true);else PlayTrack(queue[0]);return;}
                int chosen=random.Next(shuffleBag.Count);string key=shuffleBag[chosen];shuffleBag.RemoveAt(chosen);
                foreach(MenuTrack item in queue)if(item.Key==key){PlayTrack(item);return;}
            }
            if(automatic && current+1>=queue.Count && Repeat==MusicRepeat.Off){Pause(true);return;}
            int index=(current+1)%queue.Count;
            for(int attempts=0;attempts<Math.Min(queue.Count,8);attempts++,index=(index+1)%queue.Count)if(PlayTrack(queue[index]))return;
            Pause(true);
        }
        public void Previous()
        {
            if(Position>3){if(Seek!=null)Seek(0);Position=0;return;}
            while(history.Count>0){MenuTrack prior=history[history.Count-1];history.RemoveAt(history.Count-1);if(!queueContains(prior))continue;MenuTrack old=Current;Current=null;PlayTrack(prior);if(Current==null)Current=old;return;}
            List<MenuTrack> queue=Queue;if(queue.Count==0)return;
            int current=Current==null?0:queue.FindIndex(delegate(MenuTrack item){return item.Key==Current.Key;});PlayTrack(queue[(current-1+queue.Count)%queue.Count]);
        }
        private bool queueContains(MenuTrack track){return Queue.Exists(delegate(MenuTrack item){return item.Key==track.Key;});}
        public bool Contains(MenuTrack track){return Custom.Exists(delegate(MenuTrack item){return item.Key==track.Key;});}
        public void Add(MenuTrack track){if(Contains(track))return;Custom.Add(track);storedKeys.Add(track.Key);Save();}
        public void Remove(int index)
        {
            if(index<0 || index>=Custom.Count)return;MenuTrack track=Custom[index];Custom.RemoveAt(index);storedKeys.RemoveAll(delegate(string key){return key==track.Key;});shuffleBag.Clear();
            if(UseCustom && Current!=null && Current.Key==track.Key){if(Custom.Count>0)PlayTrack(Custom[Math.Min(index,Custom.Count-1)]);else{Current=null;Pause(true);}}
            Save();
        }
        public void Move(int index,int direction){int next=index+direction;if(index<0 || next<0 || index>=Custom.Count || next>=Custom.Count)return;MenuTrack item=Custom[index];Custom.RemoveAt(index);Custom.Insert(next,item);storedKeys.Clear();foreach(MenuTrack track in Custom)storedKeys.Add(track.Key);Save();}
        public void CycleRepeat(){Repeat=(MusicRepeat)(((int)Repeat+1)%3);Save();}
        public void ToggleShuffle(){Shuffle=!Shuffle;shuffleBag.Clear();shuffleCycleStarted=false;Save();}
        public void Skip(double seconds){double target=Math.Max(0,Math.Min(Math.Max(0,Duration-.05),Position+seconds));if(Seek!=null)Seek(target);Position=target;}
    }
}
