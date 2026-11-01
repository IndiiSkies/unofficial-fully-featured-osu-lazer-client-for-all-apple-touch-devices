using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;

namespace OsuLegacy
{
    public static class Program
    {
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void Frame(int width, int height);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void Touch(int phase, int id, double x, double y);
        private static readonly Frame frame = Draw;
        private static readonly Touch touch = Tap;
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int Poll();
        private static readonly Poll backgroundPoll=PollBackground;
        [DllImport("__Internal")] private static extern void legacy_poll_jobs(Poll callback,int active);
        [DllImport("__Internal")] private static extern void legacy_import_thread_priority();
        [DllImport("__Internal")] private static extern void legacy_import_checkpoint();
        [DllImport("__Internal")] private static extern void legacy_import_pause(int paused);
        [DllImport("__Internal")] private static extern void legacy_import_begin_work();
        [DllImport("__Internal")] private static extern void legacy_import_interrupt();
        [DllImport("__Internal")] private static extern int legacy_import_interrupted();
        [DllImport("__Internal")] private static extern int legacy_menu_music_play(string path);
        [DllImport("__Internal")] private static extern void legacy_menu_music_pause(int value);
        [DllImport("__Internal")] private static extern void legacy_menu_music_seek(double time);
        [DllImport("__Internal")] private static extern double legacy_menu_music_position();
        [DllImport("__Internal")] private static extern double legacy_menu_music_duration();
        [DllImport("__Internal")] private static extern int legacy_menu_music_ended();
        private static MenuPlaylist menuMusic;
        private static LocalScoreStore scoreStore;
        [DllImport("__Internal")] private static extern int legacy_song_preview(string path,double milliseconds);
        [DllImport("__Internal")] private static extern void legacy_song_preview_end();
        private static double lastMenuPaint;
        private static string paintedImportStatus="";
        private static int libraryRevision, paintedLibraryRevision;
        private static bool menuPrimedBehindIntro;
        private static double appStarted,lastClockSecond=-1;
        [DllImport("__Internal")] private static extern int legacy_start(Frame frameCallback, Touch touchCallback);
        [DllImport("__Internal")] private static extern IntPtr legacy_bundle_path();
        [DllImport("__Internal")] private static extern IntPtr legacy_documents_path();
        [DllImport("__Internal")] private static extern void legacy_circle(double x, double y, double radius, double red, double green, double blue, double alpha, int fill);
        [DllImport("__Internal")] private static extern void legacy_text(double x, double y, string text, double size, double red, double green, double blue);
        [DllImport("__Internal")] private static extern double legacy_text_width(string text,double size);
        [DllImport("__Internal")] private static extern int legacy_audio_start(string path);
        [DllImport("__Internal")] private static extern int legacy_audio_start_delayed(string path,double leadin);
        [DllImport("__Internal")] private static extern int legacy_audio_prepare_gameplay(string path,double leadin);
        [DllImport("__Internal")] private static extern int legacy_audio_commit_gameplay();
        [DllImport("__Internal")] private static extern void legacy_audio_prime_retry(double leadin);
        [DllImport("__Internal")] private static extern uint legacy_presented_serial();
        private static readonly GameplayStartGate gameplayStart=new GameplayStartGate();
        [DllImport("__Internal")] private static extern void legacy_audio_stop();
        [DllImport("__Internal")] private static extern double legacy_audio_time();
        [DllImport("__Internal")] private static extern void legacy_audio_pause();
        [DllImport("__Internal")] private static extern void legacy_audio_resume();
        [DllImport("__Internal")] private static extern void legacy_audio_restart();
        [DllImport("__Internal")] private static extern void legacy_gameplay_mod_audio(double rate,int pitch,double gain);
        [DllImport("__Internal")] private static extern int legacy_audio_rate_prepare(string source,double rate,int pitch,[Out] byte[] output,int capacity);
        [DllImport("__Internal")] private static extern int legacy_audio_prepare(string path,[Out] byte[] output,int capacity);
        private static string PrepareAudio(string path)
        {
            byte[] output=new byte[4096];
            if(legacy_audio_prepare(path,output,output.Length)==0)return path;
            int end=Array.IndexOf(output,(byte)0);return System.Text.Encoding.UTF8.GetString(output,0,end<0?output.Length:end);
        }
        [DllImport("__Internal")] private static extern int legacy_video_start(string path,double offset);
        [DllImport("__Internal")] private static extern int legacy_video_ready();
        [DllImport("__Internal")] private static extern void legacy_video_preview(string path,double offset);
        [DllImport("__Internal")] private static extern void legacy_debug_set_enabled(int enabled);
        [DllImport("__Internal")] private static extern void legacy_debug_results(int active);
        [DllImport("__Internal")] private static extern void legacy_video_sync(double audioTime);
        [DllImport("__Internal")] private static extern void legacy_video_pause();
        [DllImport("__Internal")] private static extern void legacy_video_resume();
        [DllImport("__Internal")] private static extern void legacy_video_restart();
        [DllImport("__Internal")] private static extern void legacy_video_stop();
        [DllImport("__Internal")] private static extern void legacy_search_open(string current);
        [DllImport("__Internal")] private static extern void legacy_text_open(string title,string current);
        [DllImport("__Internal")] private static extern IntPtr legacy_search_take();
        [DllImport("__Internal")] private static extern void legacy_rect(double x, double y, double width, double height, double r, double g, double b, double alpha, double slant);
        [DllImport("__Internal")] private static extern void legacy_image(string name, double x, double y, double width, double height, double alpha);
        [DllImport("__Internal")] private static extern int legacy_artwork(string path, double x, double y, double width, double height, double alpha);
        [DllImport("__Internal")] private static extern void legacy_clear_artwork_cache();
        [DllImport("__Internal")] private static extern int legacy_background(string path, int bundled, double dim);
        [DllImport("__Internal")] private static extern void legacy_artwork_quality(int quality);
        [DllImport("__Internal")] private static extern void legacy_set_animation(int active);
        [DllImport("__Internal")] private static extern void legacy_set_idle_redraw(int active);
        [DllImport("__Internal")] private static extern void legacy_menu_triangles(int visible,int moving);
        [DllImport("__Internal")] private static extern double legacy_ui_time();
        [DllImport("__Internal")] private static extern void legacy_volume(double volume);
        [DllImport("__Internal")] private static extern void legacy_music_volume(double volume);
        [DllImport("__Internal")] private static extern void legacy_effects_volume(double volume);
        [DllImport("__Internal")] private static extern void legacy_brightness(double value);
        [DllImport("__Internal")] private static extern double legacy_brightness_get();
        [DllImport("__Internal")] private static extern void legacy_path([In] double[] points, int count, double originX, double originY, double scale, double width, double r, double g, double b, double alpha);
        [DllImport("__Internal")] private static extern void legacy_arc(double x, double y, double radius, double progress, double width, double r, double g, double b, double alpha);
        private static readonly GameplayView gameplayView = new GameplayView();
        private static readonly LazerUi ui = new LazerUi();
        private static readonly NativeUiSurface uiSurface = new NativeUiSurface();
        [DllImport("__Internal")] private static extern void legacy_menu_logo(double x,double y,double diameter,int animate);
        [DllImport("__Internal")] private static extern void legacy_settings_above_logo(int active);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void SongSelection(int index);
        private static readonly SongSelection songSelection=delegate(int index){ui.SelectBrowserSong(index);};
        [DllImport("__Internal")] private static extern void legacy_song_list_set(string rows,int selected,SongSelection callback);
        [DllImport("__Internal")] private static extern void legacy_song_list_visible(int visible);
        [DllImport("__Internal")] private static extern void legacy_gpu_push_transform(double x,double y,double scale,double angle);
        [DllImport("__Internal")] private static extern void legacy_gpu_pop_transform();
        [DllImport("__Internal")] private static extern void legacy_gpu_ellipse(double x,double y,double rx,double ry,double r,double g,double b,double alpha);
        [DllImport("__Internal")] private static extern int legacy_gpu_active();
        [DllImport("__Internal")] private static extern void legacy_ui_animation(int active);
        [DllImport("__Internal")] private static extern void legacy_profile_scene(int scene);
        [DllImport("__Internal")] private static extern void legacy_gpu_commands([In] float[] data,int count);
        [DllImport("__Internal")] private static extern void legacy_gpu_clear_paths();
        [DllImport("__Internal")] private static extern void legacy_gpu_slider(int identity,[In] double[] points,int count,double x,double y,double scale,double outer,double inner,double r,double g,double b);
        [DllImport("__Internal")] private static extern void legacy_gpu_hitcircle(int identity,double x,double y,double radius,double r,double g,double b,int reduced);
        private sealed class NativeUiSurface : IUiSurface, IMenuLogoSurface, IMenuOverlaySurface, ISongListSurface, ITextWidthSurface,ILoaderTextSurface,ILoaderBackgroundSurface,INotificationLayerSurface,ITopOverlaySurface,IUiClipSurface,IHudDrawingSurface,ICatchSurface,ISliderBodySurface,IHitCircleSurface
        {
            private readonly float[] commands=new float[16384];private int used;private bool gpu;
            private List<SongBrowserRow> lastRows;private int lastSelected=-2;
            private Beatmap pathScene;private readonly Dictionary<double[],int> pathIds=new Dictionary<double[],int>();
            private readonly Dictionary<Circle,int> circleIds=new Dictionary<Circle,int>();
            public void Scene(Beatmap map){if(pathScene==map)return;Flush();legacy_gpu_clear_paths();pathIds.Clear();circleIds.Clear();pathScene=map;}
            public void HitCircle(Circle obj,double x,double y,double radius,double r,double g,double b,bool reduced){Flush();if(!gpu){if(reduced){legacy_circle(x,y,radius,r*.4,g*.4,b*.4,1,1);legacy_circle(x,y,radius,1,1,1,1,0);return;}legacy_circle(x,y,radius,r*.2,g*.2,b*.2,1,1);legacy_circle(x,y,radius*.86,r,g,b,1,1);legacy_circle(x,y,radius*.69,r*.65,g*.65,b*.65,1,1);legacy_circle(x,y,radius*.48,r*.2,g*.2,b*.2,1,1);legacy_arc(x,y,radius,1,radius*.069,1,1,1,1);return;}int id;if(!circleIds.TryGetValue(obj,out id)){id=circleIds.Count+1;circleIds[obj]=id;}legacy_gpu_hitcircle(id,x,y,radius,r,g,b,reduced?1:0);}
            public void SliderBody(double[] points,int count,double x,double y,double scale,double outer,double inner,double r,double g,double b){Flush();if(!gpu){legacy_path(points,count,x,y,scale,outer,r,g,b,.85);legacy_path(points,count,x,y,scale,inner,r*.18,g*.18,b*.18,.96);return;}int id;if(!pathIds.TryGetValue(points,out id)){id=pathIds.Count+1;pathIds[points]=id;}legacy_gpu_slider(id,points,count,x,y,scale,outer,inner,r,g,b);}
            public void Begin(){used=0;gpu=legacy_gpu_active()!=0;}
            public void Flush(){if(used==0)return;legacy_gpu_commands(commands,used);used=0;}
            private void Reserve(int count){if(used+count>commands.Length)Flush();}
            private void Put(double value){commands[used++]=(float)value;}
            public void Push(double x,double y,double angle){if(!gpu){legacy_gpu_push_transform(x,y,1,angle);return;}Reserve(4);Put(5);Put(x);Put(y);Put(angle);}
            public void Pop(){if(!gpu){legacy_gpu_pop_transform();return;}Reserve(1);Put(6);}
            public void Ellipse(double x,double y,double rx,double ry,double r,double g,double b,double alpha){if(!gpu){legacy_gpu_ellipse(x,y,rx,ry,r,g,b,alpha);return;}Reserve(9);Put(4);Put(x);Put(y);Put(rx);Put(ry);Put(r);Put(g);Put(b);Put(alpha);}
            public void HideSongList(){legacy_song_list_visible(0);}
            public void SongList(List<SongBrowserRow> rows,int selected) {
                if(rows==lastRows && selected==lastSelected){legacy_song_list_visible(1);return;}
                var data=new System.Text.StringBuilder();
                foreach(SongBrowserRow row in rows)data.Append(row.Index).Append('\t').Append(row.Difficulty?1:0).Append('\t').Append(Clean(row.Title)).Append('\t').Append(Clean(row.Subtitle)).Append('\t').Append(Clean(row.Artwork)).Append('\n');
                legacy_song_list_set(data.ToString(),selected,songSelection);
                lastRows=rows;lastSelected=selected;
            }
            private static string Clean(string value){return (value??"").Replace('\t',' ').Replace('\r',' ').Replace('\n',' ');}
            public void SettingsAboveLogo(bool active) { legacy_settings_above_logo(active?1:0); }
            public void MenuLogo(double x,double y,double diameter,bool animate) { legacy_menu_logo(x,y,diameter,animate?1:0); }
            public double TextWidth(string text,double size){return legacy_text_width(text,size);}
            [DllImport("__Internal")] private static extern void legacy_loader_text(double x,double y,string text,double size,double r,double g,double b);
            public void LoaderText(double x,double y,string text,double size,double r,double g,double b){Flush();legacy_loader_text(x,y,text,size,r,g,b);}
            [DllImport("__Internal")] private static extern void legacy_loader_background(string path,int bundled);
            public void LoaderBackground(string path,bool bundled){Flush();legacy_loader_background(path,bundled?1:0);}
            [DllImport("__Internal")] private static extern void legacy_song_list_notification_region(double x,double y,double width,double height);
            [DllImport("__Internal")] private static extern void legacy_gpu_top_overlay();
            [DllImport("__Internal")] private static extern void legacy_ui_clip_begin(double x,double y,double w,double h);
            [DllImport("__Internal")] private static extern void legacy_ui_clip_end();
            public void BeginClip(double x,double y,double w,double h){Flush();legacy_ui_clip_begin(x,y,w,h);}
            public void EndClip(){Flush();legacy_ui_clip_end();}
            public void BeginTopOverlay(){Flush();legacy_gpu_top_overlay();}
            public void NotificationRegion(double x,double y,double width,double height){BeginTopOverlay();legacy_song_list_notification_region(x,y,width,height);}
            [DllImport("__Internal")] private static extern void legacy_hud_push(double x,double y,double scale,double angle,double opacity);
            [DllImport("__Internal")] private static extern void legacy_hud_pop();
            public void PushHud(double x,double y,double scale,double angle,double opacity){Flush();legacy_hud_push(x,y,scale,angle,opacity);}
            public void PopHud(){Flush();legacy_hud_pop();}
            public void Rect(double x, double y, double w, double h, double r, double g, double b, double a, double slant) {if(!gpu){legacy_rect(x,y,w,h,r,g,b,a,slant);return;}Reserve(10);Put(1);Put(x);Put(y);Put(w);Put(h);Put(r);Put(g);Put(b);Put(a);Put(slant);}
            public void Circle(double x, double y, double radius, double r, double g, double b, double a, bool fill) {if(!gpu){legacy_circle(x,y,radius,r,g,b,a,fill?1:0);return;}Reserve(9);Put(2);Put(x);Put(y);Put(radius);Put(r);Put(g);Put(b);Put(a);Put(fill?1:0);}
            public void Text(double x, double y, string value, double size, double r, double g, double b) {Flush();legacy_text(x,y,value,size,r,g,b);}
            public void Polyline(double[] points, int count, double x, double y, double scale, double width, double r, double g, double b, double a) {Flush();legacy_path(points,count,x,y,scale,width,r,g,b,a);}
            public void Arc(double x, double y, double radius, double progress, double width, double r, double g, double b, double a) {if(!gpu){legacy_arc(x,y,radius,progress,width,r,g,b,a);return;}Reserve(10);Put(3);Put(x);Put(y);Put(radius);Put(progress);Put(width);Put(r);Put(g);Put(b);Put(a);}
            public void Image(string name, double x, double y, double w, double h, double a) {Flush();legacy_image(name,x,y,w,h,a);}
            public bool Artwork(string path, double x, double y, double w, double h, double a) {Flush();return legacy_artwork(path,x,y,w,h,a)!=0;}
            public bool Background(string path, bool bundled, double dim) {Flush();return legacy_background(path,bundled?1:0,dim)!=0;}
        }
        [DllImport("__Internal")] private static extern int legacy_sound_load(string path);
        [DllImport("__Internal")] private static extern void legacy_sound_play(int sample,double volume);
        [DllImport("__Internal")] private static extern void legacy_sound_play_pitched(int sample,double volume,double pitch);
        [DllImport("__Internal")] private static extern void legacy_sound_loop(int slot,int sample,double volume);
        [DllImport("__Internal")] private static extern void legacy_sound_reset();
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int MirrorCancel();
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void MirrorProgress(uint bytes,uint total);
        [DllImport("__Internal")] private static extern int legacy_mirror_fetch(string path,string target,uint maximum,MirrorCancel cancelled,MirrorProgress progress,[Out] byte[] error,int capacity);
        private sealed class NativeMirrorTransport:IMirrorTransport {
            public void Fetch(string path,string target,int maximum,Func<bool> cancelled,Action<int,int> progress){byte[] error=new byte[512];MirrorCancel cancel=delegate {return cancelled!=null && cancelled()?1:0;};MirrorProgress report=delegate(uint bytes,uint total){if(progress!=null)progress((int)bytes,(int)total);};if(legacy_mirror_fetch(path,target,(uint)maximum,cancel,report,error,error.Length)==0){int end=Array.IndexOf(error,(byte)0);throw new IOException(System.Text.Encoding.UTF8.GetString(error,0,end<0?error.Length:end));}GC.KeepAlive(cancel);GC.KeepAlive(report);}
        }
        private sealed class NativeSoundOutput : ISoundOutput,IPitchedSoundOutput,IMetronomeSoundOutput
        {
            public void PlayPitched(int sample,double volume,double pitch){legacy_sound_play_pitched(sample,volume,pitch);}
            public int Load(string path) { return legacy_sound_load(path); }
            [DllImport("__Internal")] private static extern void legacy_sound_metronome(int sample,double gain,double pitch);
            public void Metronome(int sample,double gain,double pitch){legacy_sound_metronome(sample,gain,pitch);}
            public void Play(int sample,double volume) { legacy_sound_play(sample,volume); }
            public void Loop(int slot,int sample,double volume) { legacy_sound_loop(slot,sample,volume); }
        }
        private static HitSounds sounds;
        private static string soundBundle;
        private static int menuSound;
        private static int notificationSound,notificationErrorSound;
        private static void LoadUiSounds(){
            menuSound=legacy_sound_load(Path.Combine(soundBundle,"button-default-select.wav"));
            notificationSound=legacy_sound_load(Path.Combine(soundBundle,"notification-default.wav"));
            notificationErrorSound=legacy_sound_load(Path.Combine(soundBundle,"notification-error.wav"));
        }
        [DllImport("__Internal")] private static extern void legacy_intro_start();
        [DllImport("__Internal")] private static extern int legacy_intro_active();
        [DllImport("__Internal")] private static extern void legacy_intro_skip();
        private static Session session;
        private static bool scoreSaved;
        private static GameplayCompletion completion;
        private static ResultsScreen results;
        private static string playedMods="None";
        private static Dictionary<string,int> resultsSamples;
        private static bool gameplayPaused;
        private static bool gameplayFailed;
        private static bool resumePending;
        private static double catchResumeAt;
        private static int catchResumeCount=-1;
        private static double pausedTime;
        private sealed class PreparedSong { public volatile bool Finished,Cancelled; public bool VideoPrepared;public double VideoWaitAt; public Session Value; public HitSounds Sounds; public Exception Error; public string PlaybackPath,Mods;public Dictionary<string,int> ResultsSamples; }
        private static PreparedSong preparingSong, soundPreparation;
        private static string queuedSong;
        private static string pendingSong;
        private static void CancelSongLoad() {
            editorTestReturn=null;
            if(soundPreparation!=null)soundPreparation.Cancelled=true;
            queuedSong=null;pendingSong=null;preparingSong=null;ui.SongPreparing=false;legacy_video_stop();legacy_import_pause(0);
        }
        [DllImport("__Internal")] private static extern int legacy_take_import_request();
        [DllImport("__Internal")] private static extern IntPtr legacy_take_notification();
        private static string library;
        private static string errorLogPath;
        private static OszImportJob importJob;
        private static bool manualImport, importAgain, startupImportPending;

        public static int Main()
        {
            ui.DeferBrowserEntry=true;
            string bundle = Marshal.PtrToStringAnsi(legacy_bundle_path());
            soundBundle=Path.Combine(bundle,"Sounds");
            library = Path.Combine(Marshal.PtrToStringAnsi(legacy_documents_path()), "Beatmaps");
            Directory.CreateDirectory(library);
            string documents = Path.GetDirectoryName(library);
            Directory.CreateDirectory(Path.Combine(documents, "Imports"));
            importJob = new OszImportJob(documents, library, Path.Combine(bundle, "Demo.osz"));
            importJob.WorkerStarted=legacy_import_thread_priority;
            importJob.WorkerCheckpoint=legacy_import_checkpoint;
            importJob.WorkerBegin=legacy_import_begin_work;importJob.WorkerInterrupt=legacy_import_interrupt;
            importJob.WorkerInterrupted=delegate {return legacy_import_interrupted()!=0;};
            importJob.PrepareAudio=PrepareAudio;
            errorLogPath=Path.Combine(documents,"legacy-errors.log");
            ui.HudSkinPath=Path.Combine(documents,"hud-layouts-v1.tsv");
            try{ui.SkinLayout=HudSkin.Load(ui.HudSkinPath);}catch(Exception error){Console.WriteLine(error);ui.Notifications.Post("hud-load","HUD layout unavailable","The saved layout could not be read. Default HUD positions are being used.",NotificationKind.Warning,false,null);}
            ui.EditBeatmapRequested=BeginEditorLoad;ui.TestEditorBeatmap=delegate(string path){editorTestReturn=path;editorTestMode=SongEntry.Read(path).Mode;StartSong(path);};ui.EditorRequestText=legacy_text_open;
            ui.EditorSeekAudio=delegate(double time){legacy_menu_music_seek(time/1000);};ui.EditorPlaybackChanged=delegate(bool play){legacy_menu_music_pause(play?0:1);};ui.EditorAudioPosition=delegate{return legacy_menu_music_position()*1000;};
            ui.EditorSaved=delegate(string path){if(ui.DifficultyRatings!=null)ui.DifficultyRatings.Invalidate(path);var song=SongEntry.Read(path);int index=ui.Songs.FindIndex(delegate(SongEntry s){return s.Path==path;});if(index<0)ui.Songs.Add(song);else ui.Songs[index]=song;ui.LibraryAppended();libraryRevision++;};
            ui.BeatmapEditorActiveChanged=delegate(bool active){legacy_import_pause(active?1:0);if(!active){editorLoad=null;legacy_menu_music_pause(1);legacy_song_preview_end();if(menuMusic.Current!=null){legacy_menu_music_play(menuMusic.Current.PlaybackPath??menuMusic.Current.Path);legacy_menu_music_seek(editorReturnPosition);legacy_menu_music_pause(editorReturnPaused?1:0);}}};
            ui.HudEditorActiveChanged=delegate(bool active){legacy_import_pause(active || session!=null?1:0);legacy_poll_jobs(backgroundPoll,1);};
            ui.SettingsPath = Path.Combine(Path.GetDirectoryName(library), "legacy-ui.cfg");
            ui.LoadSettings();
            ui.PerformanceOverlayChanged=delegate(bool enabled){legacy_debug_set_enabled(enabled?1:0);};
            legacy_debug_set_enabled(ui.PerformanceOverlay?1:0);
            ui.LoadWallpapers(Path.Combine(bundle,"Assets/wallpapers.tsv"));
            appStarted=legacy_ui_time();
            ui.Mods=new LegacyModState {SettingsPath=Path.Combine(documents,"legacy-mods.cfg")};ui.Mods.Load();
            menuMusic=new MenuPlaylist(documents,Path.Combine(bundle,"Startup/menu.m4a"));
            scoreStore=new LocalScoreStore(Path.Combine(documents,"local-scores-v1.tsv"));ui.Scores=scoreStore.Scores;
            menuMusic.SettingsPath=Path.Combine(documents,"menu-playlist.cfg");
            menuMusic.Start=delegate(MenuTrack track){
                bool playing=legacy_menu_music_play(track.PlaybackPath??track.Path)!=0;
                if(playing && (menuMusic.Current==null || menuMusic.Current.Key!=track.Key))ui.AdvanceMenuBackground();
                return playing;
            };
            menuMusic.SetPaused=delegate(bool paused){legacy_menu_music_pause(paused?1:0);};
            menuMusic.Seek=legacy_menu_music_seek;menuMusic.Load();ui.Music=menuMusic;
            LoadUiSounds();
            ui.ButtonActivated=delegate { legacy_sound_play(menuSound,.6); };
            ui.NotificationAlert=delegate(NotificationKind kind){legacy_sound_play(kind==NotificationKind.Error?notificationErrorSound:notificationSound,.7);};
            ui.VolumeChanged = legacy_volume;
            ui.MusicVolumeChanged=legacy_music_volume;ui.EffectsVolumeChanged=legacy_effects_volume;
            ui.Brightness=legacy_brightness_get();ui.BrightnessChanged=legacy_brightness;
            ui.ArtworkQualityChanged = legacy_artwork_quality;
            legacy_artwork_quality(ui.ArtworkQuality);
            legacy_volume(ui.Volume);
            legacy_music_volume(ui.MusicVolume);legacy_effects_volume(ui.EffectsVolume);
            ui.StartSong = StartSong;
            ui.CancelSongLoad = CancelSongLoad;
            ui.ImportArchives = delegate { BeginImport(true); };
            ui.DifficultyRatings=new DifficultyCache(documents);ui.DifficultyRatings.WorkerStarted=legacy_import_thread_priority;ui.DifficultyRatings.WorkerCheckpoint=legacy_import_checkpoint;
            ui.OnlineBrowser=new OnlineBeatmaps(documents,new NativeMirrorTransport());ui.OnlineBrowser.WorkerStarted=legacy_import_thread_priority;ui.OnlineTextRequested=legacy_text_open;ui.MusicTextRequested=legacy_text_open;
            ui.OnlineBrowser.Downloaded=delegate(MirrorSet set,string path){ui.Notifications.Post("browser-download","Download complete",set.Artist+" - "+set.Title+" / queued for import",NotificationKind.Success,false,null);importJob.Prioritize(path);BeginImport(false);};
            ui.OnlineBrowser.DownloadFailed=delegate(string error){ui.Notifications.Post("browser-download","Download failed",error,NotificationKind.Error,false,null);};
            ui.RequestSongSearch = delegate { legacy_search_open(ui.SearchQuery); };
            ui.PrioritizeArchive=delegate(string key){importJob.Prioritize(key);if(!importJob.Busy)BeginImport(false);};
            // Runtime/framework setup is complete here. Beatmap disk work is
            // deliberately held until UIKit has committed the first menu frame;
            // the ARMv6 device cannot render that transition while parsing maps.
            startupImportPending=true;
            int result=legacy_start(frame, touch);
            legacy_set_idle_redraw(0);
            legacy_poll_jobs(backgroundPoll,0);
            legacy_intro_start();
            return result;
        }

        private static bool DrainLibrary()
        {
            List<SongEntry> ready=importJob.TakeReady(8);
            if(ready.Count==0)return false;
            ui.MergeSongs(ready);libraryRevision++;
            menuMusic.Append(ready);
            return true;
        }

        private static void BeginImport(bool manual)
        {
            if (importJob.Busy || importJob.Finished || importJob.PendingCount>0) { importAgain = true; manualImport |= manual; return; }
            manualImport = manual;
            importJob.Start();
            ui.ImportBusy = true;
            ui.Notifications.Progress("imports","Importing beatmaps",importJob.Status,-1);
            legacy_poll_jobs(backgroundPoll,1);
        }

        private static void PollImports()
        {
            IntPtr search=legacy_search_take();
            if(search!=IntPtr.Zero){string value=Marshal.PtrToStringAnsi(search);if(!ui.AcceptMusicText(value) && !ui.AcceptOnlineText(value) && !ui.AcceptEditorText(value))ui.SetSearch(value);}
            int incoming = legacy_take_import_request();
            if (incoming > 0) BeginImport(true);
            else if (incoming < 0) ui.Notifications.Post("import-copy","Import failed","Could not copy the archive into Documents/Imports.",NotificationKind.Error,false,null);
            DrainLibrary();
            ui.ImportBusy = importJob.Busy || importJob.PendingCount>0;
            ui.ImportStatus = importJob.Status;
            if(ui.ImportBusy)ui.Notifications.Progress("imports","Importing beatmaps",ui.ImportStatus,-1);
            if (!importJob.Finished || importJob.Busy || importJob.PendingCount>0) return;
            importJob.Finished = false;
            if(importJob.Imported>0 || importJob.Rebuilt>0)legacy_clear_artwork_cache();menuMusic.Refresh(ui.Songs);
            if (manualImport || importJob.Imported > 0 || importJob.Failed > 0)
            {
                string summary = importJob.Imported + " set(s) imported; " + importJob.Skipped + " already imported.";
                if (importJob.Failed > 0)
                {
                    string error = importJob.FirstError;
                    if (error.Length > 120) error = error.Substring(0, 117) + "...";
                    summary += "\n" + importJob.Failed + " failed: " + error;
                }
                else if (manualImport && importJob.Imported == 0) summary += "\nCopy .osz files into Documents/Imports first.";
                ui.Notifications.Post("imports","Beatmap import",summary,importJob.Failed>0?NotificationKind.Error:NotificationKind.Success,false,null);
            }
            else ui.Notifications.RemoveTask("imports");
            if (importAgain) { importAgain = false; BeginImport(true); }
        }

        // Poll inexpensive job state without repainting the menu. Progress
        // text is coalesced to at most one repaint a second; completion and
        // song/screen transitions repaint immediately.
        private static int paintedDifficultyRevision;
        private static int PollBackground()
        {
            try {
                for(int eventIndex=0;eventIndex<4;eventIndex++){
                    IntPtr native=legacy_take_notification();if(native==IntPtr.Zero)break;
                    string[] fields=Marshal.PtrToStringAnsi(native).Split(new char[]{'\t'},2);
                    if(fields.Length==2)ui.Notifications.Post("native:"+fields[0]+":"+fields[1],fields[0],fields[1],NotificationKind.Warning,false,null);
                }
                if(startupImportPending) { startupImportPending=false;BeginImport(false); }
                int difficultyRevision=ui.DifficultyRatings==null?0:ui.DifficultyRatings.Revision;bool difficultyChanged=difficultyRevision!=paintedDifficultyRevision;paintedDifficultyRevision=difficultyRevision;
                bool onlineChanged=ui.OnlineBrowser!=null && ui.OnlineBrowser.Poll();
                if(onlineChanged && ui.OnlineBrowser.Downloading)ui.Notifications.Progress("browser-download","Downloading beatmap", "#"+ui.OnlineBrowser.DownloadId+" / "+(ui.OnlineBrowser.DownloadBytes/1048576.0).ToString("0.0")+" MB",ui.OnlineBrowser.DownloadProgress);
                bool completed=importJob.Finished && !importJob.Busy && importJob.PendingCount==0;
                bool menuIdle=session==null && results==null && legacy_intro_active()==0 && ui.PollMenuIdle(legacy_ui_time());
                bool browserReady=ui.CompletePendingBrowserEntry();
                Session previous=session;UiScreen screen=ui.Screen;bool preparing=ui.SongPreparing;
                MenuTrack musicBefore=menuMusic.Current;bool pausedBefore=menuMusic.Paused;
                if(!ui.BrowserPending && !ui.SongPreparing && ui.Screen!=UiScreen.SkinEditor && ui.Screen!=UiScreen.BeatmapEditor && (session==null || results!=null))PollImports();PollSongLoad();bool editorReady=PollEditorLoad();PollMenuMusic();
                PollResultsSounds(legacy_ui_time());
                ui.RuntimeSeconds=legacy_ui_time()-appStarted;
                legacy_poll_jobs(backgroundPoll,importJob.Busy || importJob.Finished || importJob.PendingCount>0 || ui.SongPreparing || session==null || results!=null?1:0);
                bool urgent=difficultyChanged || onlineChanged || menuIdle || editorReady || browserReady || completed || previous!=session || screen!=ui.Screen || preparing!=ui.SongPreparing || musicBefore!=menuMusic.Current || pausedBefore!=menuMusic.Paused;
                bool progress=ui.Screen==UiScreen.BeatmapEditor && (ui.EditorPlaying || ui.EditorLoading) || results==null && (ui.ImportStatus!=paintedImportStatus || libraryRevision!=paintedLibraryRevision || ui.Screen==UiScreen.Music) && legacy_ui_time()-lastMenuPaint>=.25;
                double clockSecond=Math.Floor(legacy_ui_time());bool clockTick=ui.SharedToolbarVisible && clockSecond!=lastClockSecond;
                if(clockTick)lastClockSecond=clockSecond;
                return urgent || progress || clockTick || ui.MenuAnimating || ui.NotificationsAnimating || ui.PollNotifications(legacy_ui_time(),session!=null || legacy_intro_active()!=0) || ui.EdgeOverlayActive(legacy_ui_time())?1:0;
            } catch(Exception error) {legacy_poll_jobs(backgroundPoll,0);Fail(error,"Background task failed",false);return 1;}
        }

        private static void PollMenuMusic()
        {
            if(ui.Screen==UiScreen.BeatmapEditor || session!=null || ui.SongBrowserAudio || legacy_intro_active()!=0)return;
            menuMusic.Position=legacy_menu_music_position();menuMusic.Duration=legacy_menu_music_duration();
            if(legacy_menu_music_ended()!=0)menuMusic.Advance(true);
        }

        private static void Text(double x, double y, string value, double size)
        { legacy_text(x, y, value, size, 0.92, 0.94, 1); }

        private static void Draw(int width, int height)
        {
            uiSurface.Begin();
            try
            {
                ui.RuntimeSeconds=legacy_ui_time()-appStarted;
                if(legacy_intro_active()!=0) {
                    legacy_profile_scene(-1);
                    // The native intro is an opaque overlay. Render the actual
                    // home screen underneath it now, so the final flash reveals
                    // a completed menu instead of triggering its first render.
                    if(!menuPrimedBehindIntro) {
                        legacy_menu_triangles(ui.UsingWallpaper?0:1,ui.Animate?1:0);
                        legacy_set_animation(0);
                        ui.Draw(uiSurface,legacy_ui_time());
                        lastMenuPaint=legacy_ui_time();paintedImportStatus=ui.ImportStatus;
                        paintedLibraryRevision=libraryRevision;menuPrimedBehindIntro=true;
                    }
                    return;
                }
                if(ui.Screen==UiScreen.BeatmapEditor){legacy_menu_logo(0,0,0,0);legacy_song_list_visible(0);legacy_menu_triangles(0,0);legacy_set_animation(ui.EditorPlaying?1:0);legacy_profile_scene(10);PollEditorLoad();IntPtr input=legacy_search_take();if(input!=IntPtr.Zero)ui.AcceptEditorText(Marshal.PtrToStringAnsi(input));ui.Draw(uiSurface,legacy_ui_time());return;}
                if(ui.Screen==UiScreen.SkinEditor){legacy_menu_logo(0,0,0,0);legacy_song_list_visible(0);legacy_menu_triangles(0,0);legacy_set_animation(0);legacy_profile_scene(9);ui.Draw(uiSurface,legacy_ui_time());return;}
                PollSongLoad();PollEditorLoad();PollMenuMusic();
                legacy_profile_scene(results!=null?8:session==null?(int)ui.Screen:100+session.Map.Mode);
                if(results!=null){
                    legacy_menu_logo(0,0,0,0);legacy_song_list_visible(0);legacy_menu_triangles(0,0);
                    double now=legacy_ui_time();legacy_set_animation(results.IsAnimating(now)?1:0);legacy_poll_jobs(backgroundPoll,1);
                    results.Draw(uiSurface,now);PollResultsSounds(now);return;
                }
                if (session == null)
                {
                    // Native layers now animate the logo/ring independently.
                    // Background polling is separate from expensive menu rasterisation.
                    legacy_menu_triangles(!ui.UsingWallpaper && (ui.Screen==UiScreen.Home || ui.Screen==UiScreen.Play || ui.Screen==UiScreen.Edit) ? 1:0,ui.Animate?1:0);
                    legacy_set_animation(0);legacy_poll_jobs(backgroundPoll,1);
                    ui.Draw(uiSurface, legacy_ui_time());
                    if(ui.SongPreparing)return;
                    // Managed menu rasterisation is expensive on ARMv6.
                    // Animate via the paced job poll, leaving UIKit time to
                    // commit the intro's display-link reveal and other layers.
                    legacy_set_animation(0);
                    if(ui.SongBrowserAudio) {
                        SongEntry preview=ui.PreviewSong;
                        if(legacy_song_preview(preview==null?null:(preview.PlaybackPath??preview.AudioPath),preview==null?-1:preview.PreviewTime)!=0)menuMusic.AdoptSelection(preview);
                        legacy_video_preview(ui.VideoEnabled && preview!=null?preview.VideoPath:null,preview==null?0:preview.VideoOffset);
                    } else {legacy_song_preview_end();legacy_video_stop();}
                    lastMenuPaint=legacy_ui_time();paintedImportStatus=ui.ImportStatus;paintedLibraryRevision=libraryRevision;
                    return;
                }
                legacy_menu_logo(0,0,0,0);
                legacy_song_list_visible(0);
                legacy_menu_triangles(0,0);
                legacy_set_animation(1);
                if(gameplayStart.TryCommit(legacy_presented_serial())){
                    if(legacy_audio_commit_gameplay()==0)throw new Exception("Audio could not start after gameplay presentation");
                    legacy_video_resume();Console.WriteLine("OSU song load: two gameplay frames presented; clock released");
                }
                gameplayView.BackgroundDim=ui.Dim;
                gameplayView.ReducedEffects=ui.ReducedGameplayEffects;
                gameplayView.ShowCursor=ui.GameplayCursor;
                gameplayView.ProfileIcon=ui.ProfileIcon;
                gameplayView.Layout=ui.SkinLayout.Layouts[session.Map.Mode];
                if(session.Mania!=null)session.Mania.ScrollTime=ui.ManiaScrollTime;
                double time=gameplayPaused?pausedTime:legacy_audio_time();
                uiSurface.Scene(session.Map);
                legacy_video_sync(time);
                gameplayView.Draw(uiSurface,session,time,width,height);
                gameplayView.DrawBreak(session,time,width,height);
                legacy_gameplay_mod_audio(session.Mods.Settings==null?1:session.Mods.Settings.Rate,session.Mods.Settings!=null && session.Mods.Settings.PitchAudio?1:0,session.Mods.MusicGain);
                if(!gameplayPaused && !gameplayFailed && session.Failed && !session.Mods.Has("NF")){gameplayFailed=true;pausedTime=time;session.ReleaseAll(time);legacy_audio_pause();legacy_video_pause();}
                if(resumePending && (session.Catch!=null || session.Mania!=null)){double elapsed=legacy_ui_time()-catchResumeAt;int count=Math.Max(0,3-(int)(elapsed/2*3));if(count!=catchResumeCount){catchResumeCount=count;sounds.Play(session.Map.Objects[0],"catch-resume-countdown",count);}if(elapsed>=2){resumePending=false;gameplayPaused=false;legacy_audio_resume();legacy_video_resume();}else gameplayView.DrawCatchResume(uiSurface,elapsed);}
                else if(resumePending)gameplayView.DrawResumePrompt(uiSurface);
                else if(gameplayFailed)gameplayView.DrawFailedMenu(uiSurface,width,height);
                else if(gameplayPaused)gameplayView.DrawPauseMenu(uiSurface,width,height);else if(!gameplayStart.Armed)sounds.Update(session,time);
                if(completion!=null && completion.Ready(time,gameplayPaused,gameplayFailed))ShowResults();
            }
            catch (Exception error) { Fail(error,session==null?"Interface error":"Gameplay error",session!=null); }
            finally {
                ui.DrawNotificationOverlay(uiSurface,legacy_ui_time(),(session!=null && results==null) || legacy_intro_active()!=0);
                uiSurface.Flush();legacy_ui_animation(legacy_intro_active()==0 && session==null && results==null && (ui.MenuAnimating || ui.NotificationsAnimating || ui.EdgeOverlayActive(legacy_ui_time()))?1:0);
            }
        }

        private static void Tap(int phase, int id, double x, double y)
        {
            try
            {
                if(legacy_intro_active()!=0) return;
                ui.NoteMenuInteraction(legacy_ui_time());
                if(ui.NotificationPointer(phase,x,y))return;
                if(ui.Screen==UiScreen.SkinEditor || ui.Screen==UiScreen.BeatmapEditor){ui.MenuPointer(phase,x,y);return;}
                if(results!=null){
                    if(phase!=0)return;
                    ResultsAction action=results.Tap(x,y);
                    if(action!=ResultsAction.None && menuSound!=0)legacy_sound_play(menuSound,.6);
                    if(action==ResultsAction.Retry)RetrySession();
                    else if(action==ResultsAction.Back)ReturnToBrowser();
                    return;
                }
                if (session != null)
                {
                    if(gameplayStart.Armed)return;
                    double time = gameplayPaused?pausedTime:legacy_audio_time();
                    if (phase == 3) { session.ReleaseAll(time); return; }
                    if(resumePending) {
                        if(session.Mania!=null){session.Mania.Touch(phase,id,x,y,time,true);return;}
                        if(session.Catch!=null)return;
                        if(phase==0 && ui.SkinLayout.Layouts[session.Map.Mode].Hit("pause",x,y,ui.ReducedGameplayEffects,10)){resumePending=false;return;}
                        double dx=x-gameplayView.CursorX,dy=y-gameplayView.CursorY;
                        if(phase==0 && dx*dx+dy*dy<=24*24){resumePending=false;gameplayPaused=false;legacy_audio_resume();legacy_video_resume();}
                        return;
                    }
                    if(gameplayFailed) {
                        if(phase!=0)return;
                        if(x>=145 && x<335 && y>=138&&y<192){RetrySession();return;}
                        if(x>=145 && x<335 && y>=192&&y<250){ReturnToBrowser();return;}
                        return;
                    }
                    if(gameplayPaused) {
                        if(phase!=0)return;
                        if(x>=350 && y>=275){ui.OpenHudEditor(session.Map.Mode);return;}
                        if(x>=145 && x<335 && y>=105&&y<155){resumePending=true;catchResumeAt=legacy_ui_time();catchResumeCount=-1;return;}
                        if(x>=145 && x<335 && y>=155&&y<205){RetrySession();return;}
                        if(x>=145 && x<335 && y>=205&&y<255){ReturnToBrowser();return;}
                        return;
                    }
                    if(session.Finished)return;
                    if (phase == 0 && ui.SkinLayout.Layouts[session.Map.Mode].Hit("pause",x,y,ui.ReducedGameplayEffects,10)) {session.ReleaseAll(time);pausedTime=time;gameplayPaused=true;legacy_audio_pause();legacy_video_pause();return;}
                    if(session.Map.Mode==1){if(phase==0 && y>=238){int pad=Math.Max(0,Math.Min(3,(int)(x/120)));session.TaikoTap(pad==0 || pad==3,pad>=2,time);}return;}
                    if(session.Catch!=null){session.Catch.Touch(phase,id,x,y,time);return;}
                    if(session.Mania!=null){session.Mania.Touch(phase,id,x,y,time,false);return;}
                    if(session.Mods.Has("AL") || session.Mods.Has("SG")){if(session.IsModKey(id) || (phase==0 && y>=279)){session.ModKey(phase,id,x<240?0:1,time);return;}PathPoint aim=gameplayView.ToPlayfield(x,y);session.Mods.AimX=aim.X;session.Mods.AimY=aim.Y;gameplayView.CursorX=x;gameplayView.CursorY=y;gameplayView.CursorVisible=true;return;}
                    gameplayView.CursorX=x;gameplayView.CursorY=y;gameplayView.CursorVisible=true;
                    PathPoint point=gameplayView.ToPlayfield(x,y);
                    if (phase == 0) session.TouchDown(id,point.X,point.Y,time);
                    else if (phase == 1) session.TouchMove(id,point.X,point.Y,time);
                    else session.TouchUp(id,point.X,point.Y,time);
                    return;
                }
                if(!ui.EdgePointer(phase,id,x,y,legacy_ui_time()))ui.MenuPointer(phase,x,y);
                legacy_set_animation(0);
            }
            catch (Exception error) { Fail(error,"Input error",false); }
        }

        private sealed class EditorLoadJob {public volatile bool Finished;public BeatmapEditorDocument Document;public string Audio;public Exception Error;}
        private static string editorTestReturn;private static int editorTestMode;private static EditorLoadJob editorLoad;private static double editorReturnPosition;private static bool editorReturnPaused;
        private static void BeginEditorLoad(string path){
            editorReturnPosition=legacy_menu_music_position();editorReturnPaused=menuMusic.Paused;legacy_menu_music_pause(1);legacy_video_stop();ui.BeginBeatmapEditor();var job=new EditorLoadJob();editorLoad=job;
            new Thread(delegate(){try{job.Document=BeatmapEditorDocument.Load(path);job.Audio=PrepareAudio(job.Document.AudioPath);}catch(Exception e){job.Error=e;}finally{job.Finished=true;}}){IsBackground=true}.Start();legacy_poll_jobs(backgroundPoll,1);
        }
        private static bool PollEditorLoad(){if(editorLoad==null || !editorLoad.Finished || ui.Screen!=UiScreen.BeatmapEditor)return false;var job=editorLoad;editorLoad=null;if(job.Error!=null){ui.EditorLoadFailed(job.Error.Message);return true;}legacy_song_preview_end();if(legacy_menu_music_play(job.Audio)==0){ui.Notifications.Post("editor-audio","Editor audio unavailable","You can edit and save, but audio playback could not start.",NotificationKind.Warning,false,null);}legacy_menu_music_pause(1);legacy_menu_music_seek(0);ui.SetBeatmapEditor(job.Document,legacy_menu_music_duration()*1000);return true;}

        private static void StartSong(string path)
        {
            if (ui.SongPreparing) return;
            ui.CloseNotifications();
            pendingSong=path;ui.BeginLoading();ui.LoadingStatus="Preparing beatmap, audio and hitsounds...";ui.SongPreparing=true;
            legacy_video_stop();
            legacy_import_pause(1);legacy_poll_jobs(backgroundPoll,1);
        }
        private static void BeginSongPreparation(string path)
        {
            Console.WriteLine("OSU song load: loading screen presented; preparing {0}",Path.GetFileName(path));
            // Native sample buffers are shared. Wait for the cancelled worker
            // to exit before resetting them, but retain the user's new request.
            if(soundPreparation!=null && !soundPreparation.Finished){queuedSong=path;ui.SongPreparing=true;legacy_import_pause(1);return;}
            queuedSong=null;
            legacy_import_pause(1);
            menuSound=0;legacy_sound_reset();
            LoadUiSounds();
            var job = new PreparedSong(); preparingSong = soundPreparation = job; ui.SongPreparing = true;
            int requestedRuleset=editorTestReturn!=null?editorTestMode:ui.SelectedRuleset;
            ModSettings requestedSettings=ui.Mods.Capture(requestedRuleset);job.Mods=requestedSettings.Summary;
            legacy_poll_jobs(backgroundPoll,1);
            new Thread(delegate() {
                try {
                    DateTime started=DateTime.UtcNow;
                    Beatmap map=Beatmap.Load(path);if(requestedRuleset==2 && map.Mode==0)map.Mode=2;
                    if(requestedRuleset==1 && map.Mode==0)map.ConvertToTaiko();
                    if(requestedRuleset==3 && map.Mode==0)ManiaConversionAdapter.Prepare(map,true);
                    requestedSettings.Apply(map);
                    job.Value = new Session(map);
                    if(job.Cancelled)return;
                    Console.WriteLine("OSU song load: parsed {0} objects at {1:F0} ms",job.Value.Map.Objects.Count,(DateTime.UtcNow-started).TotalMilliseconds);
                    job.PlaybackPath=PrepareAudio(job.Value.Map.AudioPath);
                    if(requestedSettings.Rate!=1){byte[] output=new byte[4096];if(legacy_audio_rate_prepare(job.PlaybackPath,requestedSettings.Rate,requestedSettings.PitchAudio?1:0,output,output.Length)==0)throw new Exception("Unable to prepare speed-mod audio");int end=Array.IndexOf(output,(byte)0);job.PlaybackPath=System.Text.Encoding.UTF8.GetString(output,0,end<0?output.Length:end);}
                    if(job.Cancelled)return;
                    Console.WriteLine("OSU song load: audio ready at {0:F0} ms",(DateTime.UtcNow-started).TotalMilliseconds);
                    job.Sounds=new HitSounds(job.Value.Map,new NativeSoundOutput(),soundBundle);if(!job.Sounds.Prepare(delegate {return job.Cancelled;}))return;
                    job.ResultsSamples=new Dictionary<string,int>();foreach(string file in Directory.GetFiles(soundBundle,"results-*.wav")){if(job.Cancelled)return;job.ResultsSamples[Path.GetFileName(file)]=legacy_sound_load(file);}
                    Console.WriteLine("OSU song load: hitsounds ready at {0:F0} ms",(DateTime.UtcNow-started).TotalMilliseconds);
                }
                catch (Exception error) { job.Error = error; }
                finally { job.Finished = true; }
            }) { IsBackground = true }.Start();
        }

        private static void PollSongLoad()
        {
            if(pendingSong!=null && ui.LoadingFrameDrawn){string path=pendingSong;pendingSong=null;BeginSongPreparation(path);return;}
            if(soundPreparation!=null && soundPreparation.Finished && soundPreparation.Cancelled){
                soundPreparation=null;preparingSong=null;
                string next=queuedSong;queuedSong=null;ui.SongPreparing=false;
                if(next!=null)StartSong(next);
                return;
            }
            PreparedSong job = preparingSong;
            if (job == null || !job.Finished) return;
            if (job.Error != null) { Fail(job.Error); return; }
            ui.LoadingCreator=job.Value.Map.Creator;ui.LoadingSource=job.Value.Map.Source;
            if(!job.VideoPrepared){
                job.VideoPrepared=true;job.VideoWaitAt=legacy_ui_time();
                if(ui.VideoEnabled && job.Value.Map.VideoPath!=null){legacy_video_start(job.Value.Map.VideoPath,job.Value.Map.VideoOffset);legacy_video_pause();legacy_video_sync(0);}
                return;
            }
            if(legacy_video_ready()==0){if(legacy_ui_time()-job.VideoWaitAt<3)return;legacy_video_stop();Console.WriteLine("OSU song load: video readiness timed out; using background image");ui.Notifications.Post("video-timeout","Video unavailable","The video decoder did not become ready. The beatmap background will be used.",NotificationKind.Warning,false,null);}
            if(!ui.LoadingReadyForGameplay(legacy_ui_time()))return;
            double leadin=StartLeadIn(job.Value);
            legacy_gameplay_mod_audio(job.Value.Mods.Settings.Rate,job.Value.Mods.Settings.PitchAudio?1:0,1);
            if(legacy_audio_prepare_gameplay(job.PlaybackPath,leadin)==0){Fail(new Exception("Audio format could not be prepared"));return;}
            gameplayStart.Arm(legacy_presented_serial());
            Console.WriteLine("OSU song load: resources ready; warming gameplay at {0:F0} ms",-leadin);
            preparingSong = null; ui.SongPreparing = false;soundPreparation=null;
            session = job.Value; sounds=job.Sounds;session.Sound=sounds.Play;resultsSamples=job.ResultsSamples;
            ResetCompetition();
            results=null;legacy_debug_results(0);completion=new GameplayCompletion(session);playedMods=job.Mods;
            scoreSaved=false;gameplayPaused=false;gameplayFailed=false;resumePending=false;gameplayView.CursorX=240;gameplayView.CursorY=160;gameplayView.CursorVisible=true;
        }

        private static void ShowResults(){
            if(results!=null || gameplayFailed || !session.Finished)return;
            LocalScore previous;scoreStore.Scores.TryGetValue(session.ScoreKey,out previous);
            var data=new ScoreResult(session,playedMods,previous);
            if(!scoreSaved){if(editorTestReturn==null && (session.Mods.Settings==null || session.Mods.Settings.RecordScore))scoreStore.Record(session);scoreSaved=true;}
            session.ReleaseAll(legacy_audio_time());legacy_audio_pause();legacy_video_stop();legacy_import_pause(0);
            gameplayPaused=gameplayFailed=resumePending=false;ui.Screen=UiScreen.Results;
            results=new ResultsScreen(data,legacy_ui_time(),ui.Animate);legacy_poll_jobs(backgroundPoll,1);
            legacy_debug_results(1);
            PlayResultsSample("results-score-panel-top-appear.wav",.8);
            Console.WriteLine("OSU results: {0}, score {1}, accuracy {2:F2}%, combo {3}/{4}",data.Rank,data.Score,data.Accuracy,data.MaxCombo,data.PossibleCombo);
        }
        private static void PlayResultsSample(string name,double volume){int sample;if(resultsSamples!=null && resultsSamples.TryGetValue(name,out sample) && sample!=0)legacy_sound_play(sample,volume);}
        private static void PollResultsSounds(double now){if(results==null)return;int cues=results.TakeSoundCues(now);if((cues&1)!=0)PlayResultsSample(results.RankImpactSample,.8);if((cues&2)!=0)PlayResultsSample(results.ApplauseSample,.8);}
        private static double StartLeadIn(Session value){
            double preempt=value.Mania!=null?ui.ManiaScrollTime:value.Catch!=null?value.Catch.Preempt:value.Preempt;
            return GameplayStartGate.LeadIn(value.Map.Objects[0].Time,value.Map.AudioLeadIn,preempt);
        }
        private static void RetrySession(){
            if(session==null)return;legacy_import_pause(1);legacy_audio_pause();legacy_video_stop();
            session=new Session(session.Map);session.Sound=sounds.Play;completion=new GameplayCompletion(session);results=null;legacy_debug_results(0);
            ResetCompetition();
            scoreSaved=gameplayPaused=gameplayFailed=resumePending=false;
            gameplayView.CursorX=240;gameplayView.CursorY=160;gameplayView.CursorVisible=true;
            legacy_audio_prime_retry(StartLeadIn(session));gameplayStart.Arm(legacy_presented_serial());
            if(ui.VideoEnabled && session.Map.VideoPath!=null){legacy_video_start(session.Map.VideoPath,session.Map.VideoOffset);legacy_video_pause();}
            legacy_set_animation(1);
        }
        private static void ReturnToBrowser(){
            gameplayStart.Cancel();
            string playedPath=session==null?null:session.Map.SourcePath;
            results=null;legacy_debug_results(0);completion=null;session=null;scoreSaved=gameplayPaused=gameplayFailed=resumePending=false;
            legacy_video_stop();legacy_audio_stop();if(editorTestReturn!=null){string back=editorTestReturn;editorTestReturn=null;BeginEditorLoad(back);legacy_set_animation(0);return;}ui.OpenSongs();
            legacy_import_pause(0);
            if(playedPath!=null){int index=ui.Songs.FindIndex(delegate(SongEntry song){return song.Path==playedPath;});if(index>=0)ui.SelectBrowserSong(index);}
            legacy_set_animation(0);legacy_poll_jobs(backgroundPoll,1);
        }
        private static void ResetCompetition(){
            LocalScore best;scoreStore.Scores.TryGetValue(session.ScoreKey,out best);
            gameplayView.Competition=new SelfCompetition(best);
        }

        private static string WrapMessage(string message)
        {
            const int width = 43;
            List<string> lines = new List<string>();
            foreach (string original in message.Split('\n'))
            {
                string line = original;
                while (line.Length > width)
                {
                    int split = line.LastIndexOf(' ', width);
                    if (split < 1) split = width;
                    lines.Add(line.Substring(0, split));
                    line = line.Substring(split).TrimStart();
                }
                lines.Add(line);
            }
            return String.Join("\n", lines.ToArray());
        }

        private static void Fail(Exception error,string title="Playback unavailable",bool retryPlayback=true)
        {
            gameplayStart.Cancel();
            Console.WriteLine(error);
            try{if(errorLogPath!=null){if(File.Exists(errorLogPath) && new FileInfo(errorLogPath).Length>262144)File.WriteAllText(errorLogPath,"");File.AppendAllText(errorLogPath,DateTime.UtcNow.ToString("o")+" "+title+"\n"+error+"\n\n");}}catch(IOException){}catch(UnauthorizedAccessException){}
            CancelSongLoad();
            legacy_video_stop();legacy_audio_stop();session=null;results=null;legacy_debug_results(0);completion=null;ui.Screen=UiScreen.Songs;
            string retry=!retryPlayback || ui.PreviewSong==null?null:ui.PreviewSong.Path;
            ui.Notifications.Post("error:"+title,title,error.Message,NotificationKind.Error,false,retry==null?null:(Action)delegate {StartSong(retry);}).Details=error.ToString();
        }
    }
}
