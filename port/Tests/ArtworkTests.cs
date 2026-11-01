using System;
using System.IO;
using OsuLegacy;

public static class ArtworkTests
{
    private static int count;
    private static void Check(bool result,string message) { count++;if(!result)throw new Exception(message); }
    private static bool SamePath(string a,string b) { return String.Equals(a,b,StringComparison.OrdinalIgnoreCase); }
    private static string WriteMap(string root,string name,string events)
    {
        string path=Path.Combine(root,name);
        File.WriteAllText(path,"osu file format v14\n[General]\nAudioFilename: audio.wav\nMode: 0\n[Metadata]\nTitle: "+name+"\nBeatmapSetID: "+(name.GetHashCode()&0x7fffffff)+"\nArtist: osu! community\nVersion: Normal\n[Events]\n"+events+"\n[HitObjects]\n256,192,1000,1,0\n");
        return path;
    }
    public static void Run(string assets,string output)
    {
        string root=Path.GetFullPath(Path.Combine(output,"artwork-fixture"));
        string images=Path.Combine(root,"Images");Directory.CreateDirectory(images);
        string first=Path.Combine(images,"Cover, one.JPG"),second=Path.Combine(images,"背景 二.png");
        File.Copy(Path.Combine(assets,"demo-background.jpg"),first,true);
        File.Copy(Path.Combine(assets,"logo.png"),second,true);
        File.WriteAllText(Path.Combine(root,"audio.wav"),"fixture");
        string map=WriteMap(root,"Cover art.osu","0,0,\"images\\cover, one.jpg\",0,0");
        SongEntry song=SongEntry.Read(map);
        Check(SamePath(song.BackgroundPath,first),"Quoted comma, Windows slashes and filename case resolve to the image");
        Check(SamePath(Beatmap.Load(map).BackgroundPath,first),"Gameplay and browsing use the same background metadata");
        string unicodeMap=WriteMap(root,"A different cover.osu","Background,0,\"Images/背景 二.png\",0,0");
        Check(SongEntry.Read(unicodeMap).BackgroundPath==second,"Named Background event and Unicode filename");
        string quotes=Path.Combine(images,"a\"quote.jpg");File.Copy(first,quotes,true);
        Check(SongEntry.Read(WriteMap(root,"quote.osu","0,0,\"Images/a\"\"quote.jpg\",0,0")).BackgroundPath==quotes,"CSV doubled quotes are decoded");
        Check(SongEntry.Read(WriteMap(root,"missing.osu","0,0,\"missing.jpg\",0,0")).BackgroundPath==null,"Missing artwork falls back without dropping the song");
        Check(SongEntry.Read(WriteMap(root,"bad-csv.osu","0,0,\"unfinished.jpg,0,0")).BackgroundPath==null,"Malformed event does not prevent metadata loading");
        string outside=Path.GetFullPath(Path.Combine(root,"..","outside-art.jpg"));File.Copy(first,outside,true);
        Check(BeatmapArtwork.Resolve(map,"../outside-art.jpg")==null,"Artwork cannot escape the song directory");
        Check(BeatmapArtwork.Resolve(map,first)==null && BeatmapArtwork.Resolve(map,"C:\\cover.jpg")==null,"Absolute artwork paths are rejected");
        string sprite="Sprite,Background,Centre,\"Images/Cover, one.JPG\",320,240";
        string sprite2="Sprite,Background,Centre,\"Images/背景 二.png\",320,240";
        Check(SongEntry.Read(WriteMap(root,"sprite.osu",sprite+"\n"+sprite2)).BackgroundPath==first,"First storyboard sprite is the fallback");
        Check(SongEntry.Read(WriteMap(root,"priority.osu",sprite+"\n0,0,\"Images/背景 二.png\",0,0")).BackgroundPath==second,"Explicit background overrides storyboard fallback");
        Check(SongEntry.Read(WriteMap(root,"old-video.osu","1,0,\"Images/Cover, one.JPG\",0,0")).BackgroundPath==first,"Legacy Video event pointing to an image is supported");
        File.WriteAllText(Path.Combine(root,"video.mp4"),"fixture");
        string videoMap=WriteMap(root,"video.osu","Video,375,\"video.mp4\",0,0");
        Check(SongEntry.Read(videoMap).BackgroundPath==null,"Actual videos are not treated as still images");
        Beatmap videoBeatmap=Beatmap.Load(videoMap);
        Check(SamePath(videoBeatmap.VideoPath,Path.Combine(root,"video.mp4")) && videoBeatmap.VideoOffset==375,"Video filename and start offset are parsed");
        SongEntry videoSong=SongEntry.Read(videoMap);
        Check(SamePath(videoSong.VideoPath,videoBeatmap.VideoPath) && videoSong.VideoOffset==375,"Browser metadata includes the video and its offset");
        Check(SongEntry.Read(WriteMap(root,"video-is-image.osu","Video,0,\"Images/Cover, one.JPG\",0,0")).VideoPath==null,"Image-as-video events remain images rather than opening a video decoder");
        Beatmap escapedVideo=Beatmap.Load(WriteMap(root,"escaped-video.osu","Video,0,\"../outside.mp4\",0,0"));
        Check(escapedVideo.VideoPath==null,"Video paths cannot escape the beatmap directory");
        var ui=new LazerUi { Screen=UiScreen.Songs };
        ui.Songs.Add(song);ui.Songs.Add(SongEntry.Read(unicodeMap));
        ui.SelectBrowserSong(0);
        var preview=new SvgUiSurface(assets);ui.Draw(preview,0);
        Check(SamePath(preview.BackgroundRequest,first),"Selected song supplies browsing background");
        Check(preview.ArtworkRequests.Contains(song.BackgroundPath) && preview.ArtworkRequests.Contains(second),"Visible song rows request their own cover images");
        preview.Save(Path.Combine(output,"song-artwork.svg"));
        ui.Tap(310,160);var next=new SvgUiSurface(assets);ui.Draw(next,0);
        Check(next.BackgroundRequest==second,"Selecting a different row changes the browsing background");
        var failed=new SvgUiSurface(assets) { RejectArtwork=true };ui.Draw(failed,0);
        Check(failed.ImageRequests.Count>=4,"An unreadable image falls back to placeholders");
        var gameplay=new SvgUiSurface(assets);new GameplayView().Draw(gameplay,new Session(Beatmap.Load(map)),500,480,320);
        Check(SamePath(gameplay.BackgroundRequest,first),"Song background is drawn during gameplay");
        gameplay.Save(Path.Combine(output,"gameplay-artwork.svg"));
        ui.SettingsPath=Path.Combine(output,"video-settings-test.cfg");ui.Screen=UiScreen.Songs;
        ui.Draw(new SvgUiSurface(assets),0);ui.Tap(230,260);
        Check(!ui.VideoEnabled,"Browser video toggle disables backgrounds without filtering songs");
        var persisted=new LazerUi {SettingsPath=ui.SettingsPath};persisted.LoadSettings();
        Check(!persisted.VideoEnabled,"Video background setting persists across launches");
        ui.Draw(new SvgUiSurface(assets),0);ui.Tap(230,260);Check(ui.VideoEnabled,"Video toggle can be re-enabled");
        Console.WriteLine("PASS: "+count+" artwork metadata, selection and fallback checks");
    }
}
