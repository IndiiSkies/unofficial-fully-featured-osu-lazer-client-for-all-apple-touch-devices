using System;
using System.Collections.Generic;
using System.IO;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace OsuLegacy
{
    // Metadata and prepared-audio paths survive process exit. File stamps are
    // cheap to validate; unchanged entries never reopen .osu/audio contents.
    public sealed class LibraryIndex
    {
        private sealed class Entry {public SongEntry Song;public string MapStamp,AudioStamp,PlaybackStamp,DirectoryStamp;}
        private sealed class Archive {public string Stamp,Directory;}
        private readonly Dictionary<string,Entry> songs=new Dictionary<string,Entry>(StringComparer.Ordinal);
        private readonly Dictionary<string,Archive> archives=new Dictionary<string,Archive>(StringComparer.Ordinal);
        private readonly string file;
        private bool dirty=true;
        private static string Encode(string value){return value==null?"-":Convert.ToBase64String(Encoding.UTF8.GetBytes(value));}
        private static string Decode(string value){return value=="-"?null:Encoding.UTF8.GetString(Convert.FromBase64String(value));}
        private static string Stamp(string path){if(String.IsNullOrEmpty(path))return "-";var info=new FileInfo(path);return info.Exists?info.Length+":"+info.LastWriteTimeUtc.Ticks:"-";}
        private static string DirectoryStamp(string path){return Directory.GetLastWriteTimeUtc(Path.GetDirectoryName(path)).Ticks.ToString(CultureInfo.InvariantCulture);}
        public LibraryIndex(string path){file=path;}
        public void Load(){
            songs.Clear();archives.Clear();if(!File.Exists(file))return;
            try{using(var reader=new StreamReader(file)){
                if(reader.ReadLine()!="osu-legacy-library-index-v1")return;
                dirty=false;
                string line;while((line=reader.ReadLine())!=null)try{
                    string[] f=line.Split('\t');
                    if(f.Length==4 && f[0]=="A")archives[Decode(f[1])]=new Archive {Stamp=f[2],Directory=Decode(f[3])};
                    if(f.Length!=18 || f[0]!="S")continue;
                    var song=new SongEntry {Path=Decode(f[1]),Title=Decode(f[2]),Artist=Decode(f[3]),Difficulty=Decode(f[4]),AudioPath=Decode(f[5]),PlaybackPath=Decode(f[6]),BackgroundPath=Decode(f[7]),VideoPath=Decode(f[8]),VideoOffset=double.Parse(f[9],CultureInfo.InvariantCulture),Mode=int.Parse(f[10]),SetId=int.Parse(f[11]),PreviewTime=int.Parse(f[12]),SourceArchive=Decode(f[13])};
                    if(String.IsNullOrEmpty(song.Path) || !Path.IsPathRooted(song.Path))continue;
                    songs[song.Path]=new Entry {Song=song,MapStamp=f[14],AudioStamp=f[15],PlaybackStamp=f[16],DirectoryStamp=f[17]};
                }catch(FormatException){}catch(ArgumentException){}catch(OverflowException){}
            }}catch(IOException){}catch(UnauthorizedAccessException){}
        }
        public bool TrySong(string path,out SongEntry song){
            song=null;Entry entry;if(!songs.TryGetValue(path,out entry))return false;
            try{if(entry.MapStamp=="-" || Stamp(path)!=entry.MapStamp || DirectoryStamp(path)!=entry.DirectoryStamp || Stamp(entry.Song.AudioPath)!=entry.AudioStamp || Stamp(entry.Song.PlaybackPath)!=entry.PlaybackStamp)return false;}
            catch(IOException){return false;}catch(UnauthorizedAccessException){return false;}
            song=entry.Song;return true;
        }
        public IEnumerable<SongEntry> Restore(){
            foreach(string path in new List<string>(songs.Keys)){
                SongEntry song;if(TrySong(path,out song))yield return song;
                else if(!File.Exists(path)){songs.Remove(path);dirty=true;}
            }
        }
        public void Remember(SongEntry song){songs[song.Path]=new Entry {Song=song,MapStamp=Stamp(song.Path),AudioStamp=Stamp(song.AudioPath),PlaybackStamp=Stamp(song.PlaybackPath),DirectoryStamp=DirectoryStamp(song.Path)};dirty=true;}
        public bool TryArchive(string archive,out string directory){
            directory=null;Archive entry;if(!archives.TryGetValue(archive,out entry))return false;
            if(entry.Stamp=="-" || entry.Stamp!=Stamp(archive) || !Directory.Exists(entry.Directory) || !File.Exists(Path.Combine(entry.Directory,".legacy-osz-sha256")))return false;
            directory=entry.Directory;return true;
        }
        public void RememberArchive(string archive,string directory){archives[archive]=new Archive {Stamp=Stamp(archive),Directory=directory};dirty=true;}
        public void Save(){
            if(!dirty && File.Exists(file))return;
            Directory.CreateDirectory(Path.GetDirectoryName(file));string stage=file+".tmp";
            using(var writer=new StreamWriter(stage,false,Encoding.UTF8)){
                writer.WriteLine("osu-legacy-library-index-v1");
                foreach(var pair in archives)writer.WriteLine("A\t"+Encode(pair.Key)+"\t"+pair.Value.Stamp+"\t"+Encode(pair.Value.Directory));
                foreach(Entry entry in songs.Values){SongEntry s=entry.Song;writer.WriteLine(String.Join("\t",new string[]{"S",Encode(s.Path),Encode(s.Title),Encode(s.Artist),Encode(s.Difficulty),Encode(s.AudioPath),Encode(s.PlaybackPath),Encode(s.BackgroundPath),Encode(s.VideoPath),s.VideoOffset.ToString("R",CultureInfo.InvariantCulture),s.Mode.ToString(),s.SetId.ToString(),s.PreviewTime.ToString(),Encode(s.SourceArchive),entry.MapStamp,entry.AudioStamp,entry.PlaybackStamp,entry.DirectoryStamp}));}
            }
            if(File.Exists(file))File.Replace(stage,file,null);else File.Move(stage,file);
            dirty=false;
        }
    }
    public static class OszImporter
    {
        [DllImport("__Internal")] private static extern int legacy_osz_extract(string archive, string destination, [Out] byte[] error, int capacity);
        [DllImport("__Internal")] private static extern int legacy_osz_hash(string archive, [Out] byte[] hash, int capacity);

        public static bool HasExtension(string path, string extension)
        { return String.Equals(Path.GetExtension(path), extension, StringComparison.OrdinalIgnoreCase); }

        public static List<string> Beatmaps(string directory)
        {
            List<string> result = new List<string>();
            foreach (string path in EnumerateBeatmaps(directory)) result.Add(path);
            result.Sort(StringComparer.OrdinalIgnoreCase);
            return result;
        }

        public static IEnumerable<string> EnumerateBeatmaps(string directory)
        {
            if (!Directory.Exists(directory)) yield break;
            foreach (string path in Directory.EnumerateFiles(directory))
                if (HasExtension(path, ".osu") && !Path.GetFileName(path).StartsWith("._")) yield return path;
            foreach (string child in Directory.EnumerateDirectories(directory))
            {
                string name = Path.GetFileName(child);
                if (name.StartsWith(".importing-", StringComparison.Ordinal) || name == "__MACOSX") continue;
                if ((File.GetAttributes(child) & FileAttributes.ReparsePoint) != 0) continue;
                foreach (string path in EnumerateBeatmaps(child)) yield return path;
            }
        }

        public static bool Import(string archive, string library)
        { string ignored; return Import(archive, library, out ignored); }

        public static bool Import(string archive, string library, out string destination)
        {
            byte[] hashBuffer = new byte[65];
            int hashResult=legacy_osz_hash(archive,hashBuffer,hashBuffer.Length);
            if(hashResult==-2)throw new OperationCanceledException();
            if(hashResult!=0)throw new IOException("Could not read archive");
            string hash = Encoding.ASCII.GetString(hashBuffer, 0, 64);
            destination = Path.Combine(library, "Imported-" + hash);
            string receipt = Path.Combine(destination, ".legacy-osz-sha256");
            if (Directory.Exists(destination))
            {
                if (File.Exists(receipt) && File.ReadAllText(receipt) == hash) return false;
                throw new IOException("An import folder already exists without a matching receipt");
            }
            Directory.CreateDirectory(library);
            string stage = Path.Combine(library, ".importing-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(stage);
            try
            {
                File.WriteAllText(Path.Combine(stage, ".legacy-import"), "osu legacy");
                byte[] error = new byte[256];
                int extracted=legacy_osz_extract(archive,stage,error,error.Length);
                if(extracted==-2)throw new OperationCanceledException();
                if (extracted != 0)
                {
                    int end = Array.IndexOf(error, (byte)0); if (end < 0) end = error.Length;
                    throw new IOException(Encoding.UTF8.GetString(error, 0, end));
                }
                if (Beatmaps(stage).Count == 0) throw new IOException("Archive contains no .osu beatmaps");
                File.WriteAllText(Path.Combine(stage, ".legacy-osz-sha256"), hash);
                File.WriteAllText(Path.Combine(stage, ".source-osz"), Path.GetFileName(archive));
                // Same-volume rename publishes a complete set in one step.
                Directory.Move(stage, destination);
                return true;
            }
            finally
            {
                if (Directory.Exists(stage)) Directory.Delete(stage, true);
            }
        }
    }

    public sealed class OszImportJob
    {
        public Action WorkerStarted;
        public Action WorkerCheckpoint;
        public Action WorkerBegin,WorkerInterrupt;
        public Func<bool> WorkerInterrupted;
        public Func<string,string> PrepareAudio;
        public volatile bool Busy;
        public volatile bool Finished;
        public volatile string Status = "";
        public int Imported, Skipped, Failed,Restored,Rebuilt;
        public string FirstError = "";
        public readonly List<SongEntry> Library = new List<SongEntry>();
        private readonly List<SongEntry> ready = new List<SongEntry>();
        private readonly object readyLock = new object();
        private readonly string documents, library, demo;
        private LibraryIndex index;
        private DateTime lastIndexSave;
        private void SaveIndex(bool force){if(index==null || (!force && (DateTime.UtcNow-lastIndexSave).TotalSeconds<2))return;index.Save();lastIndexSave=DateTime.UtcNow;}
        private sealed class Work {public string Key,Archive,Directory,Title;}
        private readonly List<Work> queue=new List<Work>();
        private readonly object queueLock=new object();
        private string priority,activeWork;
        public void Prioritize(string key){lock(queueLock){
            priority=key;
            if(activeWork!=null && activeWork!=key && queue.Exists(delegate(Work item){return item.Key==key;})) {
                Status="Prioritizing: "+Path.GetFileNameWithoutExtension(key);
                if(WorkerInterrupt!=null)WorkerInterrupt();
            }
        }}
        private bool YieldToPriority(string key){lock(queueLock)return priority!=null && priority!=key && queue.Exists(delegate(Work item){return item.Key==priority;});}
        private void Checkpoint(){if(WorkerCheckpoint!=null)WorkerCheckpoint();if(WorkerInterrupted!=null && WorkerInterrupted())throw new OperationCanceledException();}
        public OszImportJob(string documentsPath, string libraryPath, string demoArchive)
        { documents = documentsPath; library = libraryPath; demo = demoArchive; }

        public void Start()
        {
            if (Busy) return;
            Imported = Skipped = Failed = Restored = Rebuilt = 0; FirstError = ""; Finished = false;
            Library.Clear();
            lock (readyLock) ready.Clear();
            Status = "Looking for .osz files..."; Busy = true;
            new Thread(Run) { IsBackground = true }.Start();
        }
        private void Run()
        {
            try
            {
                if(WorkerStarted!=null)WorkerStarted();
                if(WorkerCheckpoint!=null)WorkerCheckpoint();
                var indexed = new Dictionary<string,bool>(StringComparer.Ordinal);
                var restoredSets=new Dictionary<string,bool>(StringComparer.Ordinal);
                index=new LibraryIndex(Path.Combine(documents,"library-index-v1.tsv"));index.Load();
                Status="Restoring saved library...";
                foreach(SongEntry song in index.Restore()){
                    if(WorkerCheckpoint!=null)WorkerCheckpoint();
                    if(!song.Path.StartsWith(library+Path.DirectorySeparatorChar,StringComparison.Ordinal))continue;
                    Library.Add(song);indexed[song.Path]=true;Restored++;
                    string relative=song.Path.Substring(library.Length+1);int slash=relative.IndexOf(Path.DirectorySeparatorChar);
                    if(slash>=0)restoredSets[Path.Combine(library,relative.Substring(0,slash))]=true;
                    lock(readyLock)ready.Add(song);
                }
                Status="Checking for new .osz files...";
                if (Directory.Exists(library))
                {
                    foreach (string stage in Directory.GetDirectories(library, ".importing-*"))
                    {
                        string marker = Path.Combine(stage, ".legacy-import");
                        if ((File.GetAttributes(stage) & FileAttributes.ReparsePoint) == 0 &&
                            File.Exists(marker) && File.ReadAllText(marker) == "osu legacy") Directory.Delete(stage, true);
                    }
                }
                List<string> archives = new List<string>();
                if (File.Exists(demo)) archives.Add(demo);
                Scan(documents, true, archives);
                Scan(library, true, archives);
                var tasks=new List<Work>();var assigned=new Dictionary<string,bool>();
                var existing=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
                if(Directory.Exists(library))foreach(string directory in Directory.GetDirectories(library)) {
                    Checkpoint();
                    string receipt=Path.Combine(directory,".source-osz");
                    if(File.Exists(receipt) && File.Exists(Path.Combine(directory,".legacy-osz-sha256")) && (File.GetAttributes(directory)&FileAttributes.ReparsePoint)==0)existing[File.ReadAllText(receipt)]=directory;
                }
                foreach(string archive in archives) {
                    string directory;
                    if(index.TryArchive(archive,out directory)){
                        assigned[directory]=true;Skipped++;
                        // Existing metadata is already visible; no queued title,
                        // hash, extraction or audio preparation is needed.
                        continue;
                    }
                    tasks.Add(new Work {Key=archive,Archive=archive,Title=Path.GetFileNameWithoutExtension(archive)});
                }
                foreach(var entry in existing)if(!assigned.ContainsKey(entry.Value) && !restoredSets.ContainsKey(entry.Value))tasks.Add(new Work {Key=entry.Value,Directory=entry.Value,Title=Path.GetFileNameWithoutExtension(entry.Key)});
                var random=new Random();for(int i=tasks.Count-1;i>0;i--){int j=random.Next(i+1);Work temp=tasks[i];tasks[i]=tasks[j];tasks[j]=temp;}
                // Publish every lightweight archive name before reading audio
                // or unpacking files, so the full queue is immediately visible.
                lock(readyLock)foreach(Work item in tasks)ready.Add(new SongEntry {Path=item.Key,PendingArchive=item.Key,SourceArchive=item.Key,Title=item.Title,Artist="",Difficulty="Queued"});
                lock(queueLock){queue.Clear();queue.AddRange(tasks);}
                while(true)
                {
                    if(WorkerCheckpoint!=null)WorkerCheckpoint();Work item;
                    lock(queueLock) {
                        if(queue.Count==0){activeWork=null;if(WorkerBegin!=null)WorkerBegin();break;}
                        int index=priority==null?-1:queue.FindIndex(delegate(Work candidate){return candidate.Key==priority;});
                        if(index<0)index=0;item=queue[index];queue.RemoveAt(index);
                        if(priority==item.Key)priority=null;
                        activeWork=item.Key;if(WorkerBegin!=null)WorkerBegin();
                    }
                    Status = "Loading: " + item.Title;
                    try {
                        if(item.Directory==null) {
                            string destination;
                            if(OszImporter.Import(item.Archive,library,out destination))Imported++;else Skipped++;
                            item.Directory=destination;
                            index.RememberArchive(item.Archive,destination);
                        }
                        if(!PublishDirectory(item.Directory,indexed,item.Key))lock(queueLock)queue.Add(item);
                        SaveIndex(false);
                    }
                    catch(OperationCanceledException){lock(queueLock)queue.Add(item);}
                    catch (Exception error)
                    {
                        Failed++;
                        if (FirstError.Length == 0) FirstError = item.Title + ": " + error.Message;
                        lock(readyLock)ready.Add(new SongEntry {Path=item.Key,PendingArchive=item.Key,SourceArchive=item.Key,Title=item.Title,Artist="",ImportError=error.Message});
                    }
                }
                PublishDirectory(library,indexed,null);
                SaveIndex(true);Status="Library ready; new archives checked.";
            }
            catch (Exception error) { Failed++; FirstError = error.Message; }
            finally {lock(queueLock){activeWork=null;queue.Clear();}Finished = true; Busy = false;}
        }
        private bool PublishDirectory(string directory, Dictionary<string,bool> indexed,string sourceArchive)
        {
            Status = "Reading beatmaps: " + Library.Count;
            foreach (string path in OszImporter.EnumerateBeatmaps(directory))
            {
                Checkpoint();if(sourceArchive!=null && YieldToPriority(sourceArchive))return false;
                if (indexed.ContainsKey(path)) {
                    SongEntry known=Library.Find(delegate(SongEntry song){return song.Path==path;});
                    if(known!=null && sourceArchive!=null)lock(readyLock)ready.Add(new SongEntry {Path=known.Path,AudioPath=known.AudioPath,PlaybackPath=known.PlaybackPath,VideoPath=known.VideoPath,VideoOffset=known.VideoOffset,Title=known.Title,Artist=known.Artist,Difficulty=known.Difficulty,BackgroundPath=known.BackgroundPath,Mode=known.Mode,SetId=known.SetId,PreviewTime=known.PreviewTime,SourceArchive=sourceArchive});
                    continue;
                }
                try {
                    SongEntry song;bool cached=index.TrySong(path,out song);
                    if(!cached){song=SongEntry.Read(path);Rebuilt++;}
                    song.SourceArchive=sourceArchive;
                    if(!cached && PrepareAudio!=null && song.AudioPath!=null) {
                        Status="Preparing audio: "+song.Title;
                        song.PlaybackPath=PrepareAudio(song.AudioPath);
                    }
                    Checkpoint();
                    index.Remember(song);
                    indexed[path]=true;
                    Library.Add(song);
                    lock (readyLock) ready.Add(song);
                    Status = "Reading beatmaps: " + Library.Count;
                } catch(OperationCanceledException){throw;}
                catch (Exception error) { Failed++; if (FirstError.Length == 0) FirstError = error.Message; }
            }
            return true;
        }
        public int PendingCount { get { lock (readyLock) return ready.Count; } }
        public List<SongEntry> TakeReady(int maximum)
        {
            List<SongEntry> result = new List<SongEntry>();
            lock (readyLock) while (result.Count < maximum && ready.Count > 0) { result.Add(ready[0]); ready.RemoveAt(0); }
            return result;
        }
        private static void Scan(string directory, bool recursive, List<string> paths)
        {
            if (!Directory.Exists(directory)) return;
            foreach (string path in Directory.GetFiles(directory))
                if (OszImporter.HasExtension(path, ".osz") && !paths.Contains(path)) paths.Add(path);
            if (!recursive) return;
            foreach (string child in Directory.GetDirectories(directory))
            {
                string name = Path.GetFileName(child);
                if (name.StartsWith(".importing-", StringComparison.Ordinal) || name.StartsWith("Imported-", StringComparison.Ordinal)) continue;
                if ((File.GetAttributes(child) & FileAttributes.ReparsePoint) != 0) continue;
                Scan(child, true, paths);
            }
        }
    }
}
