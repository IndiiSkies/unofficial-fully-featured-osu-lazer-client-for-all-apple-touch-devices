using System;
using System.IO;
using System.Threading;
using System.Runtime.InteropServices;
using OsuLegacy;

public static class ImportTests
{
    private static int checks;
    [DllImport("__Internal")] private static extern void legacy_import_worker_register();
    [DllImport("__Internal")] private static extern void legacy_import_begin_work();
    [DllImport("__Internal")] private static extern void legacy_import_interrupt();
    [DllImport("__Internal")] private static extern int legacy_import_interrupted();
    [DllImport("__Internal")] private static extern void legacy_import_checkpoint();
    private static void Check(bool ok, string message) { checks++; if (!ok) throw new Exception(message); }
    public static void Main(string[] args)
    {
        string root = args[0], library = Path.Combine(root, "library");
        string original = Path.Combine(root, "valid.osz");
        long originalSize = new FileInfo(original).Length;
        Check(OszImporter.Import(original, library), "First import publishes a set");
        Check(OszImporter.Beatmaps(library).Count == 1, "Imported nested difficulty is discoverable");
        Check(!OszImporter.Import(original, library), "Repeat import is skipped");
        string copy = Path.Combine(root, "renamed.osz"); File.Copy(original, copy);
        Check(!OszImporter.Import(copy, library), "Renamed identical archive is skipped");
        Check(File.Exists(original) && new FileInfo(original).Length == originalSize, "Original archive retained");
        bool rejected = false;
        try { OszImporter.Import(Path.Combine(root,"no-maps.osz"),library); } catch (IOException) { rejected = true; }
        Check(rejected && Directory.GetDirectories(library, ".importing-*").Length == 0, "No-map archive rolls back");
        rejected = false;
        try { OszImporter.Import(Path.Combine(root,"crc.osz"),library); } catch (IOException) { rejected = true; }
        Check(rejected && Directory.GetDirectories(library, ".importing-*").Length == 0, "Corrupt archive rolls back");
        Check(OszImporter.Beatmaps(library).Count == 1, "Failed imports do not publish partial sets");
        Check(OszImporter.Import(Path.Combine(root,"stored.osz"),library), "Stored archive imports");
        Check(OszImporter.Beatmaps(library).Count == 2, "Uppercase .OSU is discovered");
        string docs = Path.Combine(root,"documents"); string inbox = Path.Combine(docs,"Imports"); Directory.CreateDirectory(inbox);
        File.Copy(original,Path.Combine(inbox,"VALID.OSZ"));
        File.Copy(Path.Combine(root,"crc.osz"),Path.Combine(inbox,"bad.osz"));
        string nested = Path.Combine(docs,"Downloads"); Directory.CreateDirectory(nested);
        File.Copy(original,Path.Combine(nested,"same-content.osz"));
        string stale = Path.Combine(docs,"Beatmaps/.importing-interrupted"); Directory.CreateDirectory(stale);
        File.WriteAllText(Path.Combine(stale,".legacy-import"),"osu legacy");
        File.WriteAllText(Path.Combine(stale,"partial.osu"),"partial");
        var job = new OszImportJob(docs,Path.Combine(docs,"Beatmaps"),Path.Combine(root,"absent.osz"));
        job.Start();
        for(int i=0; i<1000 && job.Busy; i++) Thread.Sleep(10);
        var updates=job.TakeReady(100);var browser=new LazerUi();browser.MergeSongs(updates);
        Check(browser.Songs.Exists(delegate(SongEntry song){return song.PendingArchive==null && song.Title=="Archive test";}),"Parsed metadata replaces queued archive names incrementally");
        Check(browser.Songs.FindAll(delegate(SongEntry song){return song.PendingArchive==null;}).Count==1,"Identical archives do not leave duplicate playable song entries");
        Check(job.Finished && !job.Busy && job.Imported==1 && job.Failed==1,"Background batch continues after a failed archive");
        Check(OszImporter.Beatmaps(Path.Combine(docs,"Beatmaps")).Count==1,"Batch publishes valid archive");
        Check(!Directory.Exists(stale),"Interrupted staging folders are cleaned up on retry");
        Check(job.Skipped==1,"Archives delivered to nested Documents folders are discovered and deduplicated");
        Check(job.Library.Count==1 && job.Library[0].Title=="Archive test", "Worker publishes parsed library metadata without UI-thread file reads");
        string queueDocs=Path.Combine(root,"queue-documents"),queueInbox=Path.Combine(queueDocs,"Imports");Directory.CreateDirectory(queueInbox);
        foreach(string title in new string[]{"Queue A","Queue B","Queue C"})File.Copy(Path.Combine(root,title+".osz"),Path.Combine(queueInbox,title+".osz"));
        var queueJob=new OszImportJob(queueDocs,Path.Combine(queueDocs,"Beatmaps"),Path.Combine(root,"absent.osz"));
        queueJob.WorkerStarted=legacy_import_worker_register;queueJob.WorkerBegin=legacy_import_begin_work;queueJob.WorkerInterrupt=legacy_import_interrupt;
        queueJob.WorkerInterrupted=delegate {return legacy_import_interrupted()!=0;};queueJob.WorkerCheckpoint=legacy_import_checkpoint;
        var entered=new ManualResetEvent(false);var release=new ManualResetEvent(false);bool first=true;
        queueJob.PrepareAudio=delegate(string path){if(first){first=false;entered.Set();release.WaitOne();}return path;};
        queueJob.Start();Check(entered.WaitOne(5000),"Worker begins preparing first shuffled song");
        var names=queueJob.TakeReady(100);
        Check(names.Count==3 && names.TrueForAll(delegate(SongEntry song){return song.PendingArchive!=null;}),"Every archive name is available before first audio preparation completes");
        string preparing=queueJob.Status.Substring("Preparing audio: ".Length);
        SongEntry prioritySong=names.Find(delegate(SongEntry song){return song.Title!=preparing;});
        queueJob.Prioritize(prioritySong.PendingArchive);release.Set();
        for(int i=0;i<1000 && queueJob.Busy;i++)Thread.Sleep(10);
        var loaded=queueJob.TakeReady(100);
        Check(loaded.Count==3 && loaded[0].SourceArchive==prioritySong.PendingArchive,"Priority request interrupts current preparation and publishes selected archive first");
        Check(queueJob.Failed==0 && queueJob.Library.Count==3,"Interrupted preparation is requeued without failures or lost beatmaps");
        var queuedUi=new LazerUi();queuedUi.MergeSongs(names);queuedUi.OpenSongs();
        queuedUi.SelectBrowserSong(queuedUi.Songs.FindIndex(delegate(SongEntry song){return song.Path==prioritySong.Path;}));
        queuedUi.MergeSongs(loaded);
        Check(queuedUi.PreviewSong!=null && queuedUi.PreviewSong.Title==prioritySong.Title && queuedUi.PreviewSong.PendingArchive==null,"Selected queued song remains selected when unpacked metadata replaces its name");
        int preparedAgain=0;
        var restart=new OszImportJob(queueDocs,Path.Combine(queueDocs,"Beatmaps"),Path.Combine(root,"absent.osz"));
        restart.PrepareAudio=delegate(string path){preparedAgain++;return path;};restart.Start();
        for(int i=0;i<1000 && restart.Busy;i++)Thread.Sleep(10);
        var restored=restart.TakeReady(100);
        Check(restart.Failed==0 && restart.Restored==3 && restart.Rebuilt==0 && preparedAgain==0,"Restart restores persisted metadata without audio preparation");
        Check(restart.Imported==0 && restart.Skipped==3 && restored.Count==3 && restored.TrueForAll(delegate(SongEntry s){return s.PendingArchive==null;}),"Unchanged archives do not hash/extract or publish queued placeholders");
        File.Copy(Path.Combine(root,"stored.osz"),Path.Combine(queueInbox,"new.OSZ"));
        restart.Start();for(int i=0;i<1000 && restart.Busy;i++)Thread.Sleep(10);
        Check(restart.Imported==1 && restart.Restored==3 && restart.Rebuilt==1 && preparedAgain==1,"Only a newly added archive is imported and prepared");
        SongEntry edited=restart.Library.Find(delegate(SongEntry s){return s.Title=="Queue A";});
        File.AppendAllText(edited.Path,"\n// changed after indexing\n");preparedAgain=0;
        restart.Start();for(int i=0;i<1000 && restart.Busy;i++)Thread.Sleep(10);
        Check(restart.Failed==0 && restart.Restored==3 && restart.Rebuilt==1 && preparedAgain==1,"Changed loose metadata invalidates only its own cached entry");
        string archivePath=Path.Combine(queueInbox,"Queue A.osz");File.Delete(archivePath);File.Copy(Path.Combine(root,"valid.osz"),archivePath);
        preparedAgain=0;restart.Start();for(int i=0;i<1000 && restart.Busy;i++)Thread.Sleep(10);
        Check(restart.Imported==1 && restart.Rebuilt==1 && preparedAgain==1,"Replacing an archive at the same path is detected instead of trusting its filename");
        string indexFile=Path.Combine(queueDocs,"library-index-v1.tsv");File.WriteAllText(indexFile,"corrupt index");
        restart.Start();for(int i=0;i<1000 && restart.Busy;i++)Thread.Sleep(10);
        Check(restart.Failed==0 && restart.Restored==0 && restart.Library.Count==5,"A corrupt index is rebuilt from existing unpacked files without losing songs");
        var durable=new LibraryIndex(indexFile);durable.Load();SongEntry restoredSong;
        Check(durable.TrySong(restart.Library[0].Path,out restoredSong),"Rebuilt index is durably readable after repeated atomic replacement");
        string decoded=Path.Combine(queueDocs,"prepared.wav");File.WriteAllText(decoded,"pcm");restoredSong.PlaybackPath=decoded;restoredSong.Title="Unicode 音楽\tline\nbreak";durable.Remember(restoredSong);durable.Save();
        var reread=new LibraryIndex(indexFile);reread.Load();SongEntry encoded;
        Check(reread.TrySong(restoredSong.Path,out encoded) && encoded.Title==restoredSong.Title && encoded.PlaybackPath==decoded,"Index preserves Unicode metadata, delimiters and prepared audio paths");
        File.Delete(decoded);Check(!reread.TrySong(restoredSong.Path,out encoded),"Evicted decoded audio is detected and prepared again when needed");
        Console.WriteLine("PASS: " + checks + " managed import, rollback, deduplication, restart cache and worker checks");
    }
}
