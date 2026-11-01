using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace OsuLegacy
{
    // Platform adapter for lazer's menu/navigation on the early UIKit host.
    // Coordinates are shared by drawing, hit testing and the host preview.
    public interface IUiSurface
    {
        void Rect(double x, double y, double width, double height, double r, double g, double b, double alpha, double slant);
        void Circle(double x, double y, double radius, double r, double g, double b, double alpha, bool fill);
        void Text(double x, double y, string text, double size, double r, double g, double b);
        void Image(string name, double x, double y, double width, double height, double alpha);
        bool Artwork(string path, double x, double y, double width, double height, double alpha);
        bool Background(string path, bool bundled, double dim);
        void Polyline(double[] coordinates, int count, double originX, double originY, double scale, double width, double r, double g, double b, double alpha);
        void Arc(double x, double y, double radius, double progress, double width, double r, double g, double b, double alpha);
    }

    public interface IMenuLogoSurface { void MenuLogo(double x,double y,double diameter,bool animate); }
    public interface ITextWidthSurface { double TextWidth(string text,double size); }
    public interface ILoaderTextSurface { void LoaderText(double x,double y,string text,double size,double r,double g,double b); }
    public interface ILoaderBackgroundSurface { void LoaderBackground(string path,bool bundled); }
    public interface INotificationLayerSurface {void NotificationRegion(double x,double y,double width,double height);}
    public interface IHudDrawingSurface {void PushHud(double x,double y,double scale,double angle,double opacity);void PopHud();}
    public interface ICatchSurface {void Push(double x,double y,double angle);void Pop();void Ellipse(double x,double y,double rx,double ry,double r,double g,double b,double alpha);}
    public interface ISliderBodySurface {void SliderBody(double[] points,int count,double x,double y,double scale,double outer,double inner,double r,double g,double b);}
    public interface IHitCircleSurface {void HitCircle(Circle obj,double x,double y,double radius,double r,double g,double b,bool reduced);}
    public interface IUiClipSurface {void BeginClip(double x,double y,double width,double height);void EndClip();}
    public interface ITopOverlaySurface {void BeginTopOverlay();}
    public interface IMenuOverlaySurface { void SettingsAboveLogo(bool active); }
    public interface ISongListSurface { void SongList(List<SongBrowserRow> rows,int selected);void HideSongList(); }
    public sealed class SongBrowserRow { public int Index;public bool Difficulty;public string Title,Subtitle,Artwork; }

    public enum UiScreen { Home, Play, Edit, Songs, Mods, Settings, Notice, Music, Results, SkinEditor, BeatmapEditor, BeatmapListing }

    public sealed class SongEntry
    {
        public string AudioPath,PlaybackPath,VideoPath;
        public double VideoOffset;
        public string SourceArchive,PendingArchive,ImportError;
        public int PreviewTime=-1;
        public string Path, Title, BackgroundPath, Artist = "Unknown artist", Difficulty = "Normal";
        public int Mode,SetId=-1;
        public string GroupKey { get {
            if(PendingArchive!=null)return "archive:"+PendingArchive;
            if(SetId>0)return "set:"+SetId;
            string directory=System.IO.Path.GetDirectoryName(Path??"");
            return !String.IsNullOrEmpty(directory)?"folder:"+directory:"song:"+Artist+"\n"+Title;
        }}
        public static SongEntry Read(string path)
        {
            SongEntry song = new SongEntry { Path = path, Title = System.IO.Path.GetFileNameWithoutExtension(path) };
            string section = "",audio=null; int backgroundPriority=0;
            using (StreamReader reader = new StreamReader(path))
            {
            string raw;
            while ((raw = reader.ReadLine()) != null)
            {
                string line = raw.Trim();
                if (line == "[HitObjects]") break;
                if (line.StartsWith("[")) { section = line; continue; }
                if (section == "[Events]") {
                    song.BackgroundPath=BeatmapArtwork.ReadEvent(path,line,ref backgroundPriority,song.BackgroundPath);
                    string video;double offset;if(song.VideoPath==null && BeatmapArtwork.ReadVideoEvent(path,line,out video,out offset)){song.VideoPath=video;song.VideoOffset=offset;}
                    continue;
                }
                int colon = line.IndexOf(':');
                if (colon < 0) continue;
                string key = line.Substring(0, colon).Trim(), value = line.Substring(colon + 1).Trim();
                if (section == "[Metadata]")
                {
                    if (key == "Title") song.Title = value;
                    if (key == "Artist") song.Artist = value;
                    if (key == "Version") song.Difficulty = value;
                    if(key=="BeatmapSetID"){int id;if(int.TryParse(value,out id))song.SetId=id;}
                }
                if(section=="[General]" && key=="AudioFilename")audio=value;
                if(section=="[General]" && key=="PreviewTime"){int preview;if(int.TryParse(value,out preview))song.PreviewTime=preview;}
                if (section == "[General]" && key == "Mode")
                { int mode; if (int.TryParse(value, out mode)) song.Mode = mode; }
            }
            }
            if(audio!=null)song.AudioPath=BeatmapArtwork.Resolve(path,audio);
            return song;
        }
    }

    public sealed partial class LazerUi
    {
        private sealed class Hit
        {
            public double X, Y, Width, Height, Slant,Radius;
            public Action Action;
            public bool Contains(double x, double y)
            {
                if(Radius>0){double dx=x-X-Width/2,dy=y-Y-Height/2;return dx*dx+dy*dy<=Radius*Radius;}
                if (y < Y || y >= Y + Height) return false;
                double shift = Slant * (1 - (y - Y) / Height);
                return x >= X + shift && x < X + Width - Slant + shift;
            }
        }
        public MenuPlaylist Music;
        public string ProfileIcon="avatar-guest.png";
        public readonly NotificationCenter Notifications=new NotificationCenter();
        public Action<NotificationKind> NotificationAlert;
        public bool NotificationsOpen {get;private set;}
        private readonly List<Hit> notificationHits=new List<Hit>();
        private LegacyNotification drawnToast;
        private LegacyNotification notificationDetail;
        private int notificationPaintRevision=-1,notificationScroll;
        private double notificationOpenedAt=-100,notificationDragY=-1;
        private bool notificationDragging;
        public bool NotificationsAnimating {get {return NotificationsOpen && now-notificationOpenedAt<.3;}}
        public void CloseNotifications(){NotificationsOpen=false;notificationDetail=null;notificationHits.Clear();}
        public bool PollNotifications(double time,bool suppressed){return notificationPaintRevision!=Notifications.Revision || drawnToast!=Notifications.Toast(time,suppressed);}
        public LegacyModState Mods;
        private UiScreen musicReturn;
        private bool musicCustom;
        public readonly List<SongEntry> Songs = new List<SongEntry>();
        public IDictionary<string,LocalScore> Scores = new Dictionary<string,LocalScore>();
        public UiScreen Screen = UiScreen.Home;
        public Action<string> StartSong;
        public Action ImportArchives;
        public Action RequestSongSearch;
        public Action<string> PrioritizeArchive;
        private string prioritizedArchive;
        public Action CancelSongLoad;
        public bool SongPreparing;
        public bool LoadingFrameDrawn;
        public string LoadingStatus="Preparing beatmap...";
        public string LoadingCreator="",LoadingSource="";
        private double loadingStarted=double.NaN,loadingExitAt=double.NaN;
        private bool loaderVisualExpanded=true,loaderAudioExpanded=true,loaderInputExpanded=true;
        private Action<double> loaderDrag;
        private double loaderHoldAt=-1;
        public bool LoadingReadyForGameplay(double time){if(double.IsNaN(loadingStarted) || time-loadingStarted<1.8)return false;if(double.IsNaN(loadingExitAt))loadingExitAt=time;return time-loadingExitAt>=.3;}
        public void BeginLoading(){LoadingFrameDrawn=false;loadingStarted=loadingExitAt=double.NaN;LoadingCreator=LoadingSource="";loaderHoldAt=-1;loaderDrag=null;}
        public bool ImportBusy;
        public string ImportStatus = "";
        public Action<double> VolumeChanged;
        public Action<double> MusicVolumeChanged,EffectsVolumeChanged;
        public Action<double> BrightnessChanged;
        public double Brightness=.5;
        private int edgeTouch=-1,edgeSide,volumeMeter=1;
        private double edgeY,edgeUntil=-10;
        public bool EdgeOverlayActive(double time) {return MainMenuClockVisible && edgeSide!=0;}
        public bool EdgePointer(int phase,int id,double x,double y,double time)
        {
            if(!MainMenuClockVisible){edgeTouch=-1;edgeSide=0;return false;}
            if(edgeTouch!=-1 && edgeTouch!=id)return true;
            if(phase==0) {
                bool shown=time<edgeUntil;
                if(y<35 || y>287)return false;
                if(shown && edgeSide==1 && x<112) {
                    volumeMeter=y<114?0:y<211?1:2;
                } else {
                    if(x>18 && x<462)return false;
                    edgeSide=x<240?1:2;volumeMeter=1;
                }
                edgeTouch=id;edgeY=y;edgeUntil=time+1;return true;
            }
            if(edgeTouch!=id)return false;
            double delta=(edgeY-y)/220;edgeY=y;
            if(phase==1) {
                if(edgeSide==2){Brightness=ClampEdge(Brightness+delta);if(BrightnessChanged!=null)BrightnessChanged(Brightness);}
                else if(volumeMeter==0){EffectsVolume=ClampEdge(EffectsVolume+delta);if(EffectsVolumeChanged!=null)EffectsVolumeChanged(EffectsVolume);}
                else if(volumeMeter==1){Volume=ClampEdge(Volume+delta);if(VolumeChanged!=null)VolumeChanged(Volume);}
                else {MusicVolume=ClampEdge(MusicVolume+delta);if(MusicVolumeChanged!=null)MusicVolumeChanged(MusicVolume);}
            }
            edgeUntil=time+1;
            if(phase==2 || phase==3){edgeTouch=-1;if(edgeSide==1)SaveSettings();}
            return true;
        }
        private static double ClampEdge(double value){return Math.Max(0,Math.Min(1,value));}
        private void DrawEdgeControls()
        {
            if(!EdgeOverlayActive(now))return;
            if(edgeTouch==-1 && now>=edgeUntil+.1){edgeSide=0;return;}
            double alpha=edgeTouch!=-1?1:Math.Min(1,Math.Max(0,(edgeUntil+.1-now)/.1));
            double x=edgeSide==1?0:360;
            for(int i=0;i<12;i++)Rect(edgeSide==1?i*10:470-i*10,34,10,254,0,0,0,alpha*.75*(1-i/12.0));
            if(edgeSide==1){DrawEdgeMeter(57,76,31,EffectsVolume,"EFFECTS",volumeMeter==0,false,alpha);DrawEdgeMeter(57,162,41,Volume,"MASTER",volumeMeter==1,true,alpha);DrawEdgeMeter(57,249,31,MusicVolume,"MUSIC",volumeMeter==2,false,alpha);}
            else DrawEdgeMeter(x+63,162,43,Brightness,"BRIGHTNESS",true,false,alpha);
        }
        private void DrawEdgeMeter(double x,double y,double radius,double value,string label,bool selected,bool master,double alpha)
        {
            surface.Circle(x,y,radius,.075,.065,.10,alpha,true);
            surface.Arc(x,y,radius-4,1,3,.20,.17,.23,alpha);
            surface.Arc(x,y,radius-4,value,selected?4:2,master?1:.25,master?.28:.65,master?.57:1,alpha);
            string percent=Math.Round(value*100)+"%";
            surface.Text(x-percent.Length*3.5,y-8,percent,14,1,1,1);
            surface.Text(x-label.Length*2.4,y+radius+3,label,8,selected?1:.65,selected?1:.65,selected?1:.65);
        }
        public double MusicVolume=1,EffectsVolume=1;
        public bool ReducedGameplayEffects;
        public bool GameplayCursor=true;
        public bool PerformanceOverlay;
        public Action<bool> PerformanceOverlayChanged;
        public bool VideoEnabled=true;
        public Action ButtonActivated;
        public Action<int> ArtworkQualityChanged;
        public int ArtworkQuality;
        public int ClockMode;
        public double RuntimeSeconds;
        private static readonly string[] artworkSizes = { "480x320", "240x160", "120x80" };
        public double Volume = 0.8, Dim = 0.35;
        public bool Animate = true;
        public string SettingsPath;
        public bool WallpapersEnabled=true;
        public readonly List<string> WallpaperPacks=new List<string>();
        private readonly Dictionary<string,List<string>> wallpapers=new Dictionary<string,List<string>>();
        private readonly List<string> disabledPacks=new List<string>();
        private string menuWallpaper;
        private UiScreen? wallpaperScreen;
        private int wallpaperPage;
        public bool UsingWallpaper {get {return WallpapersEnabled && menuWallpaper!=null;}}
        public void LoadWallpapers(string manifest)
        {
            if(!File.Exists(manifest))return;
            foreach(string line in File.ReadAllLines(manifest)) {
                string[] fields=line.Split('\t');if(fields.Length<2)continue;
                List<string> images;
                if(!wallpapers.TryGetValue(fields[0],out images)){images=new List<string>();wallpapers.Add(fields[0],images);WallpaperPacks.Add(fields[0]);}
                images.Add(fields[1]);
            }
            PickWallpaper();
        }
        private void PickWallpaper()
        {
            var choices=new List<string>();
            foreach(string pack in WallpaperPacks)if(!disabledPacks.Contains(pack))choices.AddRange(wallpapers[pack]);
            if(choices.Count==0){menuWallpaper=null;return;}
            if(choices.Count>1)choices.Remove(menuWallpaper);
            menuWallpaper=choices[random.Next(choices.Count)];
        }
        public void AdvanceMenuBackground()
        {
            var choices=new List<string>();
            foreach(string pack in WallpaperPacks)if(!disabledPacks.Contains(pack))choices.AddRange(wallpapers[pack]);
            if(choices.Count==0){menuWallpaper=null;return;}
            int current=choices.IndexOf(menuWallpaper);
            menuWallpaper=choices[(current+1)%choices.Count];
        }
        private readonly List<Hit> hits = new List<Hit>();
        private IUiSurface surface;
        private string noticeTitle = "", noticeBody = "";
        private UiScreen noticeReturn, settingsReturn;
        private int mode, page, selected = -1;
        private SongEntry rulesetSongAnchor;
        public bool DeferBrowserEntry;
        public bool BrowserPending {get;private set;}
        private bool browserShellDrawn;
        private List<int> cachedRowsFilter;private List<SongBrowserRow> cachedBrowserRows;
        private int cachedRowsSelected=-2;private string cachedRowsPriority;
        public int SelectedRuleset {get {return mode;}}
        public int ManiaScrollTime=1500;
        private bool hasSongSelection;
        private int sortMode;
        private int modCategory,modPage;
        private int settingsSection;
        private bool showScores;
        private readonly Random random=new Random();
        private bool expanded;
        private double now;
        private bool lastMenuOpen;
        private UiScreen lastButtonScreen;
        private double menuTransition=-100;
        private Hit pressedMenuHit;
        private double pointerX=-1,pointerY=-1;
        private double menuOpenAmount=1;
        private double drawnLogoX=240,drawnLogoDiameter=264;
        private double menuLastInteraction=Double.NaN,logoTransition=-100,logoFromAmount,logoAmount,menuFromAmount,menuOpenDelay;
        public const double MenuIdleSeconds=6;
        public double MenuLogoX {get{return drawnLogoX;}}public double MenuLogoDiameter{get{return drawnLogoDiameter;}}
        public void NoteMenuInteraction(double time){menuLastInteraction=Double.IsNaN(menuLastInteraction)?time:Math.Max(time,menuLastInteraction);}
        public bool PollMenuIdle(double time){if(Double.IsNaN(menuLastInteraction)){menuLastInteraction=time;return false;}if(!MainMenuClockVisible || (!expanded && Screen==UiScreen.Home) || pressedMenuHit!=null || edgeTouch>=0 || NotificationsOpen)return false;if(time-menuLastInteraction<=MenuIdleSeconds)return false;Screen=UiScreen.Home;expanded=false;pressedMenuHit=null;hits.Clear();return true;}
        public bool MainMenuExpanded {get {return expanded;}}
        private readonly List<Hit> menuButtonHits=new List<Hit>();
        public bool MenuAnimating {get {return SongPreparing || MusicListAnimating || (Animate && MainMenuClockVisible && (pressedMenuHit!=null || now-menuTransition<.65 || now-logoTransition<.95));}}
        public void MenuPointer(int phase,double x,double y)
        {
            NoteMenuInteraction(now);
            if(Screen==UiScreen.Music){MusicPointer(phase,x,y);return;}
            if(Screen==UiScreen.BeatmapListing){OnlinePointer(phase,x,y);return;}
            if(Screen==UiScreen.BeatmapEditor){BeatmapEditorPointer(phase,x,y);return;}
            if(Screen==UiScreen.SkinEditor){HudEditorPointer(phase,x,y);return;}
            if(SongPreparing){
                pointerX=x;pointerY=y;
                if(phase==0){if(x>=362 && y>=275){loaderHoldAt=now;return;}Tap(x,y);}
                else if(phase==1 && loaderDrag!=null)loaderDrag(x);
                if(phase==2 || phase==3){loaderDrag=null;loaderHoldAt=-1;SaveSettings();}
                return;
            }
            if(!MainMenuClockVisible){pressedMenuHit=null;if(phase==0)Tap(x,y);return;}
            if(phase==0) {
                double dx=x-drawnLogoX,dy=y-160;
                if(MainMenuClockVisible && dx*dx+dy*dy<=drawnLogoDiameter*drawnLogoDiameter*.445*.445){pressedMenuHit=null;if(ButtonActivated!=null)ButtonActivated();ActivateMenuLogo();hits.Clear();return;}
                foreach(Hit hit in menuButtonHits)if(hit.Contains(x,y)){pressedMenuHit=hit;pointerX=x;pointerY=y;return;}
                Tap(x,y);return;
            }
            if(pressedMenuHit==null)return;
            pointerX=x;pointerY=y;
            if(phase==2 || phase==3) {
                Hit hit=pressedMenuHit;pressedMenuHit=null;pointerX=pointerY=-1;
                if(phase==2 && hit.Contains(x,y)){if(ButtonActivated!=null)ButtonActivated();hit.Action();hits.Clear();}
            }
        }
        private static readonly string[] modes = { "osu!", "taiko", "catch", "mania" };
        private List<int> visibleSongs;
        public string SearchQuery { get; private set; }

        public void SetSearch(string query)
        {
            SearchQuery=(query??"").Trim();visibleSongs=null;page=0;selected=-1;rulesetSongAnchor=null;
        }

        public void LoadSettings()
        {
            if (String.IsNullOrEmpty(SettingsPath) || !File.Exists(SettingsPath)) return;
            foreach (string line in File.ReadAllLines(SettingsPath))
            {
                string[] item = line.Split('='); double value;
                if (item.Length != 2) continue;
                if (item[0] == "Animate") Animate = item[1] != "0";
                if(item[0]=="VideoEnabled")VideoEnabled=item[1]!="0";
                if(item[0]=="Wallpapers")WallpapersEnabled=item[1]!="0";
                if(item[0]=="DisabledWallpaperPacks")foreach(string pack in item[1].Split('|'))if(pack.Length>0)disabledPacks.Add(pack);
                if (!double.TryParse(item[1], NumberStyles.Float, CultureInfo.InvariantCulture, out value)) continue;
                if (item[0] == "Volume") Volume = Math.Max(0, Math.Min(1, value));
                if(item[0]=="MusicVolume")MusicVolume=Math.Max(0,Math.Min(1,value));
                if(item[0]=="EffectsVolume")EffectsVolume=Math.Max(0,Math.Min(1,value));
                if(item[0]=="ReducedGameplayEffects")ReducedGameplayEffects=item[1]=="1";
                if(item[0]=="GameplayCursor")GameplayCursor=item[1]!="0";
                if(item[0]=="PerformanceOverlay")PerformanceOverlay=item[1]=="1";
                if (item[0] == "Dim") Dim = Math.Max(0, Math.Min(0.8, value));
                if (item[0] == "ArtworkQuality" && value >= 0 && value <= 2) ArtworkQuality = (int)value;
                if (item[0] == "ClockMode" && value >= 0 && value <= 3) ClockMode = (int)value;
                if(item[0]=="ManiaScrollTime" && (value==1000 || value==1500 || value==2000))ManiaScrollTime=(int)value;
            }
        }
        private void SaveSettings()
        {
            if (!String.IsNullOrEmpty(SettingsPath)) File.WriteAllText(SettingsPath,
                "Volume=" + Volume.ToString(CultureInfo.InvariantCulture) + "\nDim=" + Dim.ToString(CultureInfo.InvariantCulture) + "\nAnimate=" + (Animate ? "1" : "0") + "\nArtworkQuality=" + ArtworkQuality + "\nClockMode=" + ClockMode+"\nManiaScrollTime="+ManiaScrollTime+"\nWallpapers="+(WallpapersEnabled?"1":"0")+"\nDisabledWallpaperPacks="+String.Join("|",new List<string>(disabledPacks).ToArray()));
            if (VolumeChanged != null) VolumeChanged(Volume);
            if(!String.IsNullOrEmpty(SettingsPath))File.AppendAllText(SettingsPath,"\nReducedGameplayEffects="+(ReducedGameplayEffects?"1":"0"));
            if(!String.IsNullOrEmpty(SettingsPath))File.AppendAllText(SettingsPath,"\nGameplayCursor="+(GameplayCursor?"1":"0"));
            if(!String.IsNullOrEmpty(SettingsPath))File.AppendAllText(SettingsPath,"\nPerformanceOverlay="+(PerformanceOverlay?"1":"0"));
            if(PerformanceOverlayChanged!=null)PerformanceOverlayChanged(PerformanceOverlay);
            if(!String.IsNullOrEmpty(SettingsPath))File.AppendAllText(SettingsPath,"\nVideoEnabled="+(VideoEnabled?"1":"0"));
            if(!String.IsNullOrEmpty(SettingsPath))File.AppendAllText(SettingsPath,"\nMusicVolume="+MusicVolume.ToString(CultureInfo.InvariantCulture)+"\nEffectsVolume="+EffectsVolume.ToString(CultureInfo.InvariantCulture));
            if(MusicVolumeChanged!=null)MusicVolumeChanged(MusicVolume);
            if(EffectsVolumeChanged!=null)EffectsVolumeChanged(EffectsVolume);
            if (ArtworkQualityChanged != null) ArtworkQualityChanged(ArtworkQuality);
        }
        private static string Clip(string text, int length)
        { return text.Length > length ? text.Substring(0, length - 3) + "..." : text; }
        private void Text(double x, double y, string text, double size)
        { surface.Text(x, y, text, size, 0.96, 0.95, 1); }
        private void Muted(double x, double y, string text, double size)
        { surface.Text(x, y, text, size, 0.71, 0.67, 0.77); }
        private void Rect(double x, double y, double w, double h, double r, double g, double b, double alpha)
        { surface.Rect(x, y, w, h, r, g, b, alpha, 0); }
        private void Button(double x, double y, double w, double h, string label, double r, double g, double b, Action action, double slant)
        {
            surface.Rect(x, y, w, h, r, g, b, 0.96, slant);
            Text(x + 11 + slant / 2, y + (h - 17) / 2, label, 13);
            hits.Add(new Hit { X = x, Y = y, Width = w, Height = h, Slant = slant, Action = action });
        }
        private void Link(double x, double y, double w, double h, string label, Action action)
        { Button(x, y, w, h, label, 0.18, 0.15, 0.23, action, 0); }
        public void Tap(double x, double y)
        {
            NoteMenuInteraction(now);
            for (int i = hits.Count - 1; i >= 0; i--)
                if (hits[i].Contains(x, y)) { if(ButtonActivated!=null)ButtonActivated();hits[i].Action(); hits.Clear(); return; }
        }
        public void OpenMusic() {if(Screen!=UiScreen.Music)musicReturn=Screen;Screen=UiScreen.Music;ResetMusicWindow();}
        private void OpenSettings(){if(Screen==UiScreen.Settings)return;settingsReturn=Screen;pressedMenuHit=null;Screen=UiScreen.Settings;}
        private void ToggleMusic() {if(Screen==UiScreen.Music)Screen=musicReturn;else OpenMusic();}
        public bool SongBrowserAudio {get {return Screen==UiScreen.Songs || Screen==UiScreen.Mods || (Screen==UiScreen.Notice && (noticeReturn==UiScreen.Songs||noticeReturn==UiScreen.Mods));}}
        public bool MainMenuClockVisible {get {return Screen==UiScreen.Home || Screen==UiScreen.Play || Screen==UiScreen.Edit;}}
        public bool MenuChromeVisible {get{return !MainMenuClockVisible || expanded || Screen!=UiScreen.Home || NotificationsOpen;}}
        public bool SharedToolbarVisible {get {return (MainMenuClockVisible && MenuChromeVisible) || Screen==UiScreen.Songs || Screen==UiScreen.Mods || Screen==UiScreen.BeatmapListing;}}
        public SongEntry PreviewSong {get {return selected>=0 && selected<Songs.Count?Songs[selected]:rulesetSongAnchor;}}
        public void SelectBrowserSong(int index) {if(index>=0 && index<Songs.Count && VisibleSongs().Contains(index)){selected=index;rulesetSongAnchor=Songs[index];prioritizedArchive=null;PrioritizeSelected();}}
        private void SwitchRuleset(int next){
            if(next==mode)return;
            if(CancelSongLoad!=null)CancelSongLoad();
            if(rulesetSongAnchor==null)rulesetSongAnchor=PreviewSong;
            mode=next;visibleSongs=null;page=0;ChooseRulesetSong();
        }
        private void ChooseRulesetSong(){
            selected=-1;List<int> filtered=VisibleSongs();SongEntry anchor=rulesetSongAnchor;
            if(anchor==null){EnsureSongSelection();return;}
            foreach(int index in filtered)if(Songs[index].Path==anchor.Path){selected=index;break;}
            if(selected<0){int first=-1,standard=-1,sameDifficulty=-1;
                foreach(int index in filtered){SongEntry song=Songs[index];if(song.GroupKey!=anchor.GroupKey)continue;
                    if(first<0)first=index;if(standard<0 && song.Mode==0)standard=index;
                    if(sameDifficulty<0 && song.Difficulty==anchor.Difficulty)sameDifficulty=index;
                }
                selected=sameDifficulty>=0?sameDifficulty:standard>=0?standard:first;
            }
            if(selected>=0){page=filtered.IndexOf(selected)/3;hasSongSelection=true;PrioritizeSelected();}
        }
        private void PrioritizeSelected() {
            SongEntry song=PreviewSong;
            if(song!=null && song.PendingArchive!=null && prioritizedArchive!=song.PendingArchive && PrioritizeArchive!=null){prioritizedArchive=song.PendingArchive;PrioritizeArchive(song.PendingArchive);}
        }
        public void MergeSongs(List<SongEntry> incoming) {
            string selectedPath=PreviewSong==null?null:PreviewSong.Path,selectedArchive=PreviewSong==null?null:PreviewSong.PendingArchive;
            foreach(SongEntry song in incoming) {
                if(song.PendingArchive!=null && Songs.Exists(delegate(SongEntry existing){return existing.PendingArchive==null && existing.SourceArchive==song.PendingArchive;}))continue;
                if(song.PendingArchive==null && song.SourceArchive!=null) {
                    if(selectedArchive==song.SourceArchive){selectedPath=song.Path;selectedArchive=null;}
                    int pending=Songs.FindIndex(delegate(SongEntry existing){return existing.PendingArchive==song.SourceArchive;});
                    if(pending>=0 && !Songs.Exists(delegate(SongEntry existing){return existing.Path==song.Path && existing.PendingArchive==null;})) {
                        Songs[pending]=song;continue;
                    }
                    Songs.RemoveAll(delegate(SongEntry existing){return existing.PendingArchive==song.SourceArchive;});
                }
                int index=Songs.FindIndex(delegate(SongEntry existing){return existing.Path==song.Path;});
                if(index>=0)Songs[index]=song;else Songs.Add(song);
            }
            selected=selectedPath==null?-1:Songs.FindIndex(delegate(SongEntry song){return song.Path==selectedPath;});
            LibraryAppended();
            if(rulesetSongAnchor!=null){
                int anchorIndex=Songs.FindIndex(delegate(SongEntry song){return song.Path==rulesetSongAnchor.Path;});
                rulesetSongAnchor=anchorIndex>=0?Songs[anchorIndex]:selected>=0?Songs[selected]:null;
                if(rulesetSongAnchor!=null)ChooseRulesetSong();
            }
        }
        public List<SongBrowserRow> BrowserRows()
        {
            List<int> filtered=VisibleSongs();
            if(cachedBrowserRows!=null && cachedRowsFilter==filtered && cachedRowsSelected==selected && cachedRowsPriority==prioritizedArchive)return cachedBrowserRows;
            var groups=new Dictionary<string,List<int>>();var order=new List<string>();
            foreach(int index in filtered) {
                string key=Songs[index].GroupKey;List<int> group;
                if(!groups.TryGetValue(key,out group)){group=new List<int>();groups.Add(key,group);order.Add(key);}
                group.Add(index);
            }
            var rows=new List<SongBrowserRow>();
            foreach(string key in order) {
                List<int> group=groups[key];SongEntry song=Songs[group[0]];
                bool expandedGroup=selected>=0 && selected<Songs.Count && Songs[selected].GroupKey==key;
                rows.Add(new SongBrowserRow {Index=expandedGroup?selected:group[0],Title=song.Title,Subtitle=song.PendingArchive!=null?(song.ImportError==null?(prioritizedArchive==song.PendingArchive?"Requested — loading first":"Queued — tap to load first"):"Import failed — tap to retry"):song.Artist+" / "+group.Count+" difficulties",Artwork=song.BackgroundPath});
                if(expandedGroup && song.PendingArchive==null)foreach(int index in group)rows.Add(new SongBrowserRow {Index=index,Difficulty=true,Title=Songs[index].Difficulty,Subtitle="osu! "+modes[mode]});
            }
            cachedRowsFilter=filtered;cachedRowsSelected=selected;cachedRowsPriority=prioritizedArchive;cachedBrowserRows=rows;return rows;
        }
        public void OpenSongs() {
            EditorPicking=false;Screen = UiScreen.Songs; expanded = true;
            if(DeferBrowserEntry){BrowserPending=true;browserShellDrawn=false;return;}
            PrepareSongBrowser();
        }
        public bool CompletePendingBrowserEntry(){
            if(!BrowserPending || !browserShellDrawn)return false;
            BrowserPending=false;browserShellDrawn=false;if(Screen!=UiScreen.Songs)return false;
            PrepareSongBrowser();return true;
        }
        private void PrepareSongBrowser(){
            if(Music!=null && Music.Current!=null) {
                int index=Songs.FindIndex(delegate(SongEntry song){return song.Mode==mode && song.AudioPath==Music.Current.Path;});
                if(index>=0 && VisibleSongs().Contains(index))selected=index;
            }
            EnsureSongSelection();
        }
        private void EnsureSongSelection() {
            List<int> filtered=VisibleSongs();
            if(selected>=0 && filtered.Contains(selected)){hasSongSelection=true;PrioritizeSelected();return;}
            if(rulesetSongAnchor!=null){ChooseRulesetSong();return;}
            if(filtered.Count==0){selected=-1;return;}
            selected=filtered[hasSongSelection?0:random.Next(filtered.Count)];hasSongSelection=true;
            PrioritizeSelected();
        }
        public void LibraryChanged() { selected = -1; page = 0; visibleSongs=null;rulesetSongAnchor=null; }
        public void LibraryAppended() { visibleSongs=null; }
        public void ShowNotice(string title, string body)
        { if (Screen != UiScreen.Notice) noticeReturn = Screen; noticeTitle = title; noticeBody = body; Screen = UiScreen.Notice; }
        public void Draw(IUiSurface target, double time)
        {
            surface = target; now = time; hits.Clear();menuButtonHits.Clear();
            ISongListSurface nativeSongs=surface as ISongListSurface;
            if(nativeSongs!=null)nativeSongs.HideSongList();
            if(Screen==UiScreen.BeatmapEditor){DrawBeatmapEditor();return;}
            if(Screen==UiScreen.SkinEditor){DrawHudEditor();return;}
            if(SongPreparing) {
                DrawPlayerLoader();LoadingFrameDrawn=true;return;
            }
            loadingStarted=loadingExitAt=double.NaN;
            if(Screen==UiScreen.Settings)
            {
                if(settingsReturn==UiScreen.Settings)settingsReturn=UiScreen.Home;
                // Settings is an overlay in lazer. Render the screen which
                // opened it first, then discard its hit map and draw the panel.
                Screen=settingsReturn;
                try { Draw(target,time); }
                finally { Screen=UiScreen.Settings;surface=target;now=time;hits.Clear();menuButtonHits.Clear(); }
                DrawSettings();
                if(nativeSongs!=null)nativeSongs.HideSongList();
                IMenuOverlaySurface overlay=surface as IMenuOverlaySurface;
                if(overlay!=null)overlay.SettingsAboveLogo(true);
                return;
            }
            IMenuOverlaySurface normalLayers=surface as IMenuOverlaySurface;
            if(normalLayers!=null)normalLayers.SettingsAboveLogo(false);
            // Match MainMenu.OnResuming: button submenus and overlays are
            // part of the same menu; only returning from a pushed screen
            // advances the seasonal rotation.
            if(Screen!=UiScreen.Notice && Screen!=UiScreen.Music) {
                bool main=Screen==UiScreen.Home || Screen==UiScreen.Play || Screen==UiScreen.Edit;
                if(wallpaperScreen.HasValue && main && (wallpaperScreen.Value==UiScreen.Songs || wallpaperScreen.Value==UiScreen.Mods))AdvanceMenuBackground();
                wallpaperScreen=Screen;
            }
            IMenuLogoSurface nativeLogo=surface as IMenuLogoSurface;
            if(nativeLogo!=null && Screen!=UiScreen.Home && Screen!=UiScreen.Play && Screen!=UiScreen.Edit)nativeLogo.MenuLogo(0,0,0,false);
            bool songBackground=false;
            if(Screen==UiScreen.Songs && !BrowserPending)
            {
                List<int> filtered=VisibleSongs();
                if(page*3>=filtered.Count)page=0;
                EnsureSongSelection();
                SongEntry preview=PreviewSong;if(preview!=null && preview.BackgroundPath!=null)songBackground=target.Background(preview.BackgroundPath,false,Dim);
            }
            if(!songBackground)target.Background(UsingWallpaper?menuWallpaper:"menu-background.jpg",true,Dim);
            if(MenuChromeVisible){
            Rect(0, 0, 480, 34, 0.08, 0.065, 0.12, 0.95);
            if(SharedToolbarVisible) {
                double clockWidth=ToolbarClockWidth(),clockX=448-clockWidth-2;
                double guestX=clockX-76,musicX=guestX-36;
                ToolbarIcon(0,38,0,OpenSettings);
                ToolbarIcon(39,46,1,delegate {Screen=UiScreen.Home;});
                if(Screen==UiScreen.Songs || Screen==UiScreen.Mods){
                    string[] icons={"osu","taiko","catch","mania"};
                    for(int i=0;i<4;i++){int ruleset=i;double bx=90+i*30;if(mode==i)Rect(bx,2,28,30,.4,.267,.8,.95);surface.Image("toolbar-ruleset-"+icons[i]+".png",bx+4,7,20,20,1);hits.Add(new Hit {X=bx,Y=0,Width=30,Height=34,Action=delegate {SwitchRuleset(ruleset);}});}
                }else {int trackChars=Math.Max(5,(int)((musicX-91)/5.5));Muted(91,12,Clip(Music==null||Music.Current==null?"No track":Music.Current.Title,trackChars),9);}
                ToolbarIcon(musicX,34,2,ToggleMusic);
                ToolbarGuest(guestX,74,delegate {ShowNotice("Your profile","Local play is available from Play > Solo.");});
            }
            else if(Screen==UiScreen.Music) {
                DrawMusicHeader();
            }
            else {Text(14, 8, "osu!lazer", 16);Muted(93, 12, "iPod touch", 10);}
            if(SharedToolbarVisible)DrawToolbarClock();
            Rect(0, 288, 480, 32, 0.08, 0.065, 0.12, 0.95);
            Muted(14, 298, "gh/IndiiSkies unofficial client", 9);
            if (ImportBusy) Muted(185, 298, Clip(ImportStatus, 58), 9);
            else Muted(325, 298, Songs.Count + " difficulties in library", 9);
            }
            if (Screen == UiScreen.Music) DrawMusic();
            else if (Screen == UiScreen.Songs) DrawSongs();
            else if (Screen == UiScreen.Mods) DrawMods();
            else if(Screen==UiScreen.BeatmapListing)DrawOnlineBrowser();
            else if (Screen == UiScreen.Notice) DrawNotice();
            else DrawMenu();
            if(EdgeOverlayActive(now)){ITopOverlaySurface top=surface as ITopOverlaySurface;if(top!=null)top.BeginTopOverlay();}
            DrawEdgeControls();
        }
        public bool NotificationPointer(int phase,double x,double y){
            if(NotificationsOpen){
                if(phase==0){notificationDragY=y;notificationDragging=false;}
                if(phase==1 && notificationDragY>=0 && Math.Abs(y-notificationDragY)>24){notificationScroll=Math.Max(0,notificationScroll+(y<notificationDragY?1:-1));notificationDragY=y;notificationDragging=true;return true;}
                if(phase==2 && !notificationDragging && notificationDragY>=0){foreach(Hit hit in notificationHits)if(hit.Contains(x,y)){hit.Action();notificationDragY=-1;return true;}if(x<292)CloseNotifications();}
                if(phase==2 || phase==3)notificationDragY=-1;
                return true;
            }
            if(phase==0)foreach(Hit hit in notificationHits)if(hit.Contains(x,y)){hit.Action();return true;}
            return false;
        }
        public void DrawNotificationOverlay(IUiSurface target,double time,bool gameplay){
            surface=target;now=time;notificationHits.Clear();
            INotificationLayerSurface layer=target as INotificationLayerSurface;if(layer!=null)layer.NotificationRegion(0,0,0,0);
            if(!gameplay && !SongPreparing && Screen!=UiScreen.SkinEditor && MenuChromeVisible){
                DrawNotificationBell(448);
            }
            if(NotificationsOpen && !gameplay && !SongPreparing){
                if(layer!=null)layer.NotificationRegion(0,0,480,320);
                if(Notifications.UnreadCount>0)Notifications.MarkRead();
                double ease=1-Math.Pow(1-Math.Min(1,Math.Max(0,(time-notificationOpenedAt)/.3)),5),x=480-188*ease;
                Rect(0,34,480,286,0,0,0,.3*ease);Rect(x,34,188,286,.11,.08,.15,.99);
                Text(x+10,44,"notifications",16);
                NotificationLink(x+133,40,48,25,"close",CloseNotifications);
                if(notificationDetail!=null){
                    NotificationLink(x+8,70,60,23,"< back",delegate {notificationDetail=null;notificationScroll=0;});
                    Text(x+10,102,Clip(notificationDetail.Title,26),12);
                    string details=(notificationDetail.Details??notificationDetail.Message??"").Replace('\r',' ').Replace('\n',' ');int chars=34,total=(details.Length+chars-1)/chars;
                    notificationScroll=Math.Min(notificationScroll,Math.Max(0,total-12));
                    for(int line=notificationScroll;line<total && line<notificationScroll+12;line++)Muted(x+10,126+(line-notificationScroll)*12,details.Substring(line*chars,Math.Min(chars,details.Length-line*chars)),9);
                    if(notificationDetail.Activated!=null)NotificationLink(x+117,286,64,25,"retry",delegate {Action action=notificationDetail.Activated;CloseNotifications();action();});
                    notificationPaintRevision=Notifications.Revision;drawnToast=null;return;
                }
                NotificationLink(x+118,70,63,23,"clear all",delegate {Notifications.ClearCompleted();notificationScroll=0;});
                var visible=new List<LegacyNotification>();foreach(var item in Notifications.Items)if(!item.Dismissed && item.Ongoing)visible.Add(item);foreach(var item in Notifications.Items)if(!item.Dismissed && !item.Ongoing)visible.Add(item);
                notificationScroll=Math.Min(notificationScroll,Math.Max(0,visible.Count-2));
                if(visible.Count==0){Muted(x+14,128,"You're all caught up!",12);Muted(x+14,150,"No notifications.",10);}
                double y=98;
                for(int i=notificationScroll;i<visible.Count && y+88<=314;i++){
                    LegacyNotification item=visible[i];Muted(x+10,y,item.Ongoing?"ONGOING TASKS":"NOTIFICATIONS",8);y+=14;
                    DrawNotificationCard(item,x+8,y,172,false);y+=82;
                }
                if(visible.Count>2)Muted(x+12,304,"swipe to scroll  "+(notificationScroll+1)+" / "+visible.Count,8);
                
                drawnToast=null;
            }else{
                LegacyNotification nextToast=Notifications.Toast(time,gameplay || SongPreparing);
                if(nextToast!=null && nextToast!=drawnToast && NotificationAlert!=null)NotificationAlert(nextToast.Kind);
                drawnToast=nextToast;
                if(drawnToast!=null){if(layer!=null)layer.NotificationRegion(284,42,188,73);DrawNotificationCard(drawnToast,284,42,188,true);}
            }
            notificationPaintRevision=Notifications.Revision;
        }
        private void DrawNotificationBell(double x){
            double cx=x+16;Rect(x,0,32,34,.08,.065,.12,.95);
            surface.Image("toolbar-notification.png",cx-11,6,22,22,1);
            int count=Notifications.UnreadCount;if(count>0){surface.Circle(x+25,8,7,.95,.18,.28,1,true);surface.Text(x+21,3,count>99?"99+":count.ToString(),8,1,1,1);}
            notificationHits.Add(new Hit {X=x,Y=0,Width=32,Height=34,Action=delegate {NotificationsOpen=!NotificationsOpen;notificationOpenedAt=now;notificationScroll=0;if(NotificationsOpen)Notifications.MarkRead();}});
        }
        private void NotificationLink(double x,double y,double w,double h,string label,Action action){Muted(x+3,y+4,label,9);notificationHits.Add(new Hit {X=x,Y=y,Width=w,Height=h,Action=action});}
        private void DrawNotificationCard(LegacyNotification item,double x,double y,double width,bool toast){
            double r=item.Kind==NotificationKind.Error?1:item.Kind==NotificationKind.Warning?1:.55,g=item.Kind==NotificationKind.Error?.3:item.Kind==NotificationKind.Warning?.75:.8,b=item.Kind==NotificationKind.Error?.4:item.Kind==NotificationKind.Warning?.25:1;
            Rect(x,y,width,73,.17,.13,.22,.98);Rect(x,y,3,73,r,g,b,1);
            surface.Circle(x+14,y+17,6,r,g,b,1,true);surface.Text(x+12,y+11,item.Kind==NotificationKind.Error || item.Kind==NotificationKind.Warning?"!":"i",10,.1,.08,.14);
            Text(x+26,y+8,Clip(item.Title,Math.Max(8,(int)((width-48)/5.5))),10);
            string message=(item.Message??"").Replace('\r',' ').Replace('\n',' ');int chars=Math.Max(12,(int)((width-20)/4.5));
            for(int line=0;line<2 && line*chars<message.Length;line++){int start=line*chars;Muted(x+10,y+29+line*12,Clip(message.Substring(start),chars),9);}
            if(item.Ongoing){Rect(x+10,y+65,width-20,2,.3,.25,.36,1);double progress=item.Progress<0?.35:Math.Max(0,Math.Min(1,item.Progress));Rect(x+10,y+65,(width-20)*progress,2,r,g,b,1);}
            else NotificationLink(x+width-22,y,22,24,"x",delegate {Notifications.Dismiss(item);});
            if(item.Activated!=null)NotificationLink(x+width-45,y+53,44,20,"retry",delegate {item.Read=true;Action action=item.Activated;CloseNotifications();action();});
            notificationHits.Add(new Hit {X=x,Y=y+24,Width=width,Height=49,Action=delegate {
                item.Read=true;notificationDetail=item;notificationScroll=0;NotificationsOpen=true;notificationOpenedAt=now;Notifications.MarkRead();
            }});
        }
        private void ToolbarIcon(double x,double width,int icon,Action action)
        {
            Rect(x,0,width,34,.08,.065,.12,.95);double cx=x+width/2,cy=17;
            if(icon==0) {
                // osu! settings cog.
                for(int i=0;i<8;i++){double a=i*Math.PI/4;surface.Polyline(new double[]{Math.Cos(a)*7,Math.Sin(a)*7,Math.Cos(a)*10,Math.Sin(a)*10},2,cx,cy,1,2,.94,.94,.97,1);}
                surface.Circle(cx,cy,7,.94,.94,.97,1,false);surface.Circle(cx,cy,2.5,.94,.94,.97,1,false);
            } else if(icon==1)surface.Image("toolbar-home.png",cx-11,cy-11,22,22,1);
            else surface.Image("toolbar-music.png",cx-11,cy-11,22,22,1);
            hits.Add(new Hit {X=x,Y=0,Width=width,Height=34,Action=action});
        }
        private void ToolbarGuest(double x,double width,Action action)
        {
            Rect(x,0,width,34,.08,.065,.12,.95);Text(x+5,9,"Guest",10);
            Rect(x+width-31,3,28,28,.16,.16,.18,1);surface.Image(ProfileIcon,x+width-31,3,28,28,1);
            hits.Add(new Hit {X=x,Y=0,Width=width,Height=34,Action=action});
        }
        private double ToolbarClockWidth() {return ClockMode==0?34:ClockMode==1?112:ClockMode==2?86:78;}
        private void DrawToolbarClock()
        {
            DateTime clock=DateTime.Now;bool analog=ClockMode==0 || ClockMode==1;
            bool digital=ClockMode!=0;bool runtime=ClockMode==1 || ClockMode==2;
            double width=ToolbarClockWidth(),x=448-width-2;
            Rect(x,0,width+2,34,.08,.065,.12,.95);
            double textX=analog?x+30:x+7;
            if(analog) {
                double cx=x+17,cy=17,r=11;surface.Circle(cx,cy,r,.9,.9,.94,1,false);
                double seconds=clock.Second/60.0,minutes=(clock.Minute+seconds)/60.0,hours=(clock.Hour%12+minutes)/12.0;
                ClockHand(cx,cy,r*.50,hours,2.1,.94,.94,.97);ClockHand(cx,cy,r*.72,minutes,1.5,.94,.94,.97);ClockHand(cx,cy,r*.78,seconds,.8,.98,.35,.65);
                surface.Circle(cx,cy,1.5,.98,.35,.65,1,true);
            }
            if(digital) {
                Text(textX,runtime?4:9,clock.ToString("h:mm:ss tt",CultureInfo.InvariantCulture),runtime?8:10);
                if(runtime)Muted(textX,18,"running "+Runtime(RuntimeSeconds),7);
            }
            hits.Add(new Hit {X=x,Y=0,Width=width+2,Height=34,Action=delegate {
                // Match lazer's Analog -> Full -> DigitalWithRuntime -> Digital cycle.
                ClockMode=ClockMode==0?1:ClockMode==1?2:ClockMode==2?3:0;SaveSettings();
            }});
        }
        private void ClockHand(double x,double y,double length,double fraction,double width,double r,double g,double b)
        {
            double angle=fraction*Math.PI*2-Math.PI/2;
            surface.Polyline(new double[]{0,0,Math.Cos(angle)*length,Math.Sin(angle)*length},2,x,y,1,width,r,g,b,1);
        }
        private static string Runtime(double seconds)
        {
            int total=Math.Max(0,(int)seconds),hours=total/3600;
            return hours+":"+(total/60%60).ToString("D2")+":"+(total%60).ToString("D2");
        }
        private void ActivateMenuLogo(){
            if(Screen==UiScreen.Home){if(!expanded)expanded=true;else Screen=UiScreen.Play;}
            else if(Screen==UiScreen.Play)OpenSongs();
            else if(Screen==UiScreen.Edit)OpenEditorPicker();
        }
        private static string MenuButtonIcon(string label){switch(label){case "settings":return "settings";case "back":return "prev-circle";case "play":return "logo";case "edit":return "edit-circle";case "browse":case "beatmap editor":return "beatmap";case "solo":return "player";case "multi":return "online";case "playlists":return "tournament";case "skin editor":return "skin-b";default:return null;}}
        private void DrawMenu()
        {
            bool open = expanded || Screen != UiScreen.Home;
            if(Double.IsNaN(menuLastInteraction))menuLastInteraction=now;
            bool visibilityChanged=open!=lastMenuOpen;
            if(visibilityChanged){menuOpenDelay=open?.15:0;logoFromAmount=logoAmount;logoTransition=now;menuFromAmount=menuOpenAmount;menuTransition=now;lastMenuOpen=open;}
            if(Screen!=lastButtonScreen){if(!visibilityChanged)menuOpenDelay=0;menuTransition=now;lastButtonScreen=Screen;}
            double elapsed=Math.Max(0,now-menuTransition),logoElapsed=Math.Max(0,now-logoTransition-(open?0:.15));
            double lp=Math.Min(1,logoElapsed/(open?.2:.8));double le=open?lp*lp:lp>=1?1:1-Math.Pow(2,-10*lp);
            logoAmount=Animate?logoFromAmount+((open?1:0)-logoFromAmount)*le:open?1:0;
            double bp=Math.Min(1,Math.Max(0,elapsed-menuOpenDelay)/.5),be=bp>=1?1:1-Math.Pow(2,-10*bp);
            menuOpenAmount=Animate?(open?be:menuFromAmount*(1-be)):open?1:0;
            double x=240-90*logoAmount;drawnLogoX=x;
            IMenuLogoSurface nativeLogo=surface as IMenuLogoSurface;
            double pulse=nativeLogo==null && Animate?Math.Sin(now*Math.PI*3)*2:0;
            double diameter=(264+pulse)*(1-.25*logoAmount);drawnLogoDiameter=diameter;
            if (open || menuOpenAmount>.001)
            {
                bool submenu=Screen!=UiScreen.Home;
                AnimatedMenuButton(-14, 116, 164, 90, submenu?"back":"settings", submenu?.20:.33, submenu?.227:.33, submenu?.369:.33,
                    submenu?(Action)delegate {Screen=UiScreen.Home;}:OpenSettings, 14);
                if (Screen == UiScreen.Home)
                {
                    AnimatedMenuButton(210, 116, 104, 90, "play", 0.40, 0.267, 0.80, delegate { Screen = UiScreen.Play; }, 14);
                    AnimatedMenuButton(300, 116, 104, 90, "edit", 0.933, 0.667, 0, delegate { Screen = UiScreen.Edit; }, 14);
                    AnimatedMenuButton(390, 116, 104, 90, "browse", 0.647, 0.80, 0, OpenOnlineBrowser, 14);
                }
                else if (Screen == UiScreen.Play)
                {
                    AnimatedMenuButton(210, 116, 104, 90, "solo", 0.40, 0.267, 0.80, OpenSongs, 14);
                    AnimatedMenuButton(300, 116, 104, 90, "multi", 0.369, 0.247, 0.729, delegate { ShowNotice("Multiplayer", "Room lists and multiplayer need the network\nand gameplay backends to be ported."); }, 14);
                    AnimatedMenuButton(390, 116, 104, 90, "playlists", 0.369, 0.247, 0.729, OpenMusic, 14);
                }
                else
                {
                    AnimatedMenuButton(210, 116, 149, 90, "beatmap editor", 0.933, 0.667, 0, OpenEditorPicker, 14);
                    AnimatedMenuButton(345, 116, 149, 90, "skin editor", 0.86, 0.627, 0, delegate {OpenHudEditor(mode);}, 14);
                }
            }
            if(nativeLogo!=null)nativeLogo.MenuLogo(x,160,diameter,Animate);
            else {
                surface.Circle(x, 160, diameter / 2 - 12, 0.95, 0.24, 0.54, 0.20, false);
                surface.Circle(x, 160, diameter * 0.445, 0.94, 0.36, 0.62, 1, true);
                surface.Image("logo.png", x - diameter / 2, 160 - diameter / 2, diameter, diameter, 1);
            }
            // The sprite contains transparent padding; use the visible disc as its hit area.
            double logoRadius=diameter*.445;
            hits.Add(new Hit {X=drawnLogoX-logoRadius,Y=160-logoRadius,Width=logoRadius*2,Height=logoRadius*2,Radius=logoRadius,Action=ActivateMenuLogo});
            if (!open) Muted(178, 264, "tap the circle to begin", 12);
            else if(Screen==UiScreen.Home) Muted(127, 264, "welcome to osu!  /  choose your next beat", 11);
        }
        private void AnimatedMenuButton(double x,double y,double w,double h,string label,double r,double g,double b,Action action,double slant)
        {
            bool hovered=pressedMenuHit!=null && pressedMenuHit.Contains(pointerX,pointerY) && pressedMenuHit.X==x;
            double scale=menuOpenAmount*(hovered?1.5:1),width=w*scale;
            double left=x+(w-width)/2,opacity=Math.Min(1,menuOpenAmount*6);
            if(width>0)for(int layer=4;layer>=1;layer--){double pad=layer*1.5;surface.Rect(left-pad,y-pad,width+pad*2,h+pad*2,0,0,0,.04*opacity,Math.Min(slant,width*.4));}
            surface.Rect(left,y,width,h,r,g,b,Math.Min(1,menuOpenAmount*6),Math.Min(14,width*.4));
            if(hovered)surface.Rect(x+(w-width)/2,y,width,h,1,1,1,.10,slant);
            if(menuOpenAmount>.55){string icon=MenuButtonIcon(label);double iconSize=32,centre=x<0?40:x+w/2+(x==210?8:0);if(icon!=null)surface.Image("menu-icon-"+icon+".png",centre-iconSize/2,y+17,iconSize,iconSize,1);double size=13,available=w-13-slant/2;ITextWidthSurface measure=surface as ITextWidthSurface;double textWidth=measure==null?label.Length*size*.54:measure.TextWidth(label,size);if(textWidth>available){size*=available/textWidth;textWidth=available;}Text(centre-textWidth/2,y+h-22,label,size);}
            var hit=new Hit {X=x,Y=y,Width=w,Height=h,Slant=14,Action=action};if(menuOpenAmount>.2){hits.Add(hit);menuButtonHits.Add(hit);}
        }
        private static string Clock(double value) {int seconds=Math.Max(0,(int)value);return seconds/60+":"+(seconds%60).ToString("D2");}
        private List<int> VisibleSongs()
        {
            if(visibleSongs!=null)return visibleSongs;
            List<int> result = new List<int>();
            for (int i = 0; i < Songs.Count; i++) if ((Songs[i].Mode == mode || (mode!=0 && Songs[i].Mode==0)) && MatchesSearch(Songs[i])) result.Add(i);
            result.Sort(delegate(int a,int b) {
                if(sortMode==0)return a.CompareTo(b);
                string left=sortMode==2?Songs[a].Artist:sortMode==3?Songs[a].Difficulty:Songs[a].Title;
                string right=sortMode==2?Songs[b].Artist:sortMode==3?Songs[b].Difficulty:Songs[b].Title;
                return StringComparer.OrdinalIgnoreCase.Compare(left,right);
            });
            visibleSongs=result;return result;
        }
        private bool MatchesSearch(SongEntry song)
        {
            if(String.IsNullOrEmpty(SearchQuery))return true;
            return Contains(song.Title,SearchQuery) || Contains(song.Artist,SearchQuery) || Contains(song.Difficulty,SearchQuery);
        }
        private static bool Contains(string value,string query)
        { return value!=null && value.IndexOf(query,StringComparison.OrdinalIgnoreCase)>=0; }
        private void RandomSong()
        {
            List<int> songs=VisibleSongs();if(songs.Count==0)return;
            int next=songs[random.Next(songs.Count)];if(songs.Count>1 && next==selected)next=songs[(songs.IndexOf(next)+1)%songs.Count];
            selected=next;rulesetSongAnchor=Songs[next];page=Math.Max(0,songs.IndexOf(next)/3);
        }
        private void Cover(SongEntry song,double x,double y,double width,double height)
        {
            if(song.BackgroundPath!=null && surface.Artwork(song.BackgroundPath,x,y,width,height,1))return;
            surface.Image("menu-background.jpg",x,y,width,height,0.65);
        }
        // PlayerLoader / BeatmapMetadataDisplay: 1200x800 logical layout
        // mapped to 480x320, preserving the centre stack and 270px toolboxes.
        private void DrawPlayerLoader()
        {
            if(double.IsNaN(loadingStarted))loadingStarted=now;
            if(loaderHoldAt>=0 && now-loaderHoldAt>=1){CancelLoading();return;}
            IMenuLogoSurface nativeLogo=surface as IMenuLogoSurface;
            SongEntry song=PreviewSong;
            string background=song!=null && song.BackgroundPath!=null?song.BackgroundPath:"menu-background.jpg";bool bundled=song==null || song.BackgroundPath==null;
            ILoaderBackgroundSurface blur=surface as ILoaderBackgroundSurface;
            if(blur!=null)blur.LoaderBackground(background,bundled);else surface.Background(background,bundled,.2);
            double elapsed=now-loadingStarted;
            double ease=1-Math.Pow(1-Math.Min(1,elapsed/.65),5),scale=.7+.3*ease;
            double exit=double.IsNaN(loadingExitAt)?0:Math.Min(1,(now-loadingExitAt)/.3);
            scale*=1-.1*exit;
            double fade=Math.Min(1,Math.Max(0,(elapsed-.5)/.5))*(1-exit);
            // The logo is the shared centre anchor; metadata fades in after 500ms.
            double logo=76*scale;
            if(nativeLogo!=null){IMenuOverlaySurface order=surface as IMenuOverlaySurface;if(order!=null)order.SettingsAboveLogo(false);nativeLogo.MenuLogo(240,90,logo,Animate);}
            else {surface.Circle(240,90,logo*.445,.94,.36,.62,1-exit,true);surface.Image("logo.png",240-logo/2,90-logo/2,logo,logo,1-exit);}
            if(fade>0){
                LoaderCentred(song==null?"":song.Title,132,14.4*scale,218,fade);
                LoaderCentred(song==null?"":song.Artist,150,10.4*scale,218,fade);
                double thumbWidth=120*scale,thumbHeight=24*scale,thumbX=240-thumbWidth/2,thumbY=170;
                if(song!=null && song.BackgroundPath!=null)surface.Artwork(song.BackgroundPath,thumbX,thumbY,thumbWidth,thumbHeight,fade);
                Rect(thumbX,thumbY,thumbWidth,thumbHeight,0,0,0,.5*fade);
                ICatchSurface transform=surface as ICatchSurface;
                if(transform!=null){transform.Push(240,thumbY+thumbHeight/2,elapsed/3.15*Math.PI*2);surface.Arc(0,0,6*scale,.82,1.3,1,1,1,fade);transform.Pop();}
                else surface.Arc(240,thumbY+thumbHeight/2,6*scale,.82,1.3,1,1,1,fade);
                LoaderCentred(song==null?"":song.Difficulty,204,10.4*scale,205,fade);
                LoaderCentred("Source   "+(String.IsNullOrEmpty(LoadingSource)?"-":LoadingSource),237,8*scale,210,fade*.8);
                LoaderCentred("Mapper   "+(String.IsNullOrEmpty(LoadingCreator)?"-":LoadingCreator),249,8*scale,210,fade*.8);
                if(Mods!=null && Mods.Summary!="None")LoaderCentred(Mods.SummaryFor(mode),270,9*scale,210,fade);
            }
            double sideEase=1-Math.Pow(1-Math.Min(1,Math.Max(0,(elapsed-.75)/.5)),5);
            double right=362+(1-sideEase)*128;
            if(right<480){
                double y=10;
                LoaderGroup(right,y,"VISUAL SETTINGS",loaderVisualExpanded,delegate {loaderVisualExpanded=!loaderVisualExpanded;});y+=15;
                if(loaderVisualExpanded){
                    LoaderSlider(right,y,"Background dim",Dim,.8,delegate(double value){Dim=value;});y+=27;
                    LoaderDisabledSlider(right,y,"Background blur");y+=27;
                    LoaderCheck(right,y,"Storyboard / video",VideoEnabled,true,delegate {VideoEnabled=!VideoEnabled;SaveSettings();});y+=18;
                    LoaderCheck(right,y,"Beatmap skins",false,false,null);y+=18;
                    LoaderCheck(right,y,"Beatmap colours",true,false,null);y+=18;
                    LoaderDisabledSlider(right,y,"Combo colour normalisation");y+=27;
                }
                y+=8;LoaderGroup(right,y,"AUDIO SETTINGS",loaderAudioExpanded,delegate {loaderAudioExpanded=!loaderAudioExpanded;});y+=15;
                if(loaderAudioExpanded){LoaderCheck(right,y,"Beatmap hitsounds",true,false,null);y+=18;Muted(right+4,y,"Local offset     0 ms",7.5);y+=20;}
                y+=8;LoaderGroup(right,y,"INPUT SETTINGS",loaderInputExpanded,delegate {loaderInputExpanded=!loaderInputExpanded;});y+=15;
                if(loaderInputExpanded)LoaderCheck(right,y,"Disable taps during gameplay",false,false,null);
            }
            // Base-screen back action and the loader's separate hold-to-menu affordance.
            Link(0,280,72,40,"< back",CancelLoading);
            Rect(362,280,108,28,.12,.09,.18,.8);
            Muted(373,289,"hold for menu",9);
            if(loaderHoldAt>=0)Rect(362,307,108*Math.Min(1,now-loaderHoldAt),2,.95,.3,.6,1);
        }
        private void CancelLoading(){loaderDrag=null;loaderHoldAt=-1;if(CancelSongLoad!=null)CancelSongLoad();SongPreparing=false;Screen=UiScreen.Songs;}
        private void LoaderCentred(string text,double y,double size,double maxWidth,double fade){
            text=text??"";ITextWidthSurface measure=surface as ITextWidthSurface;
            double width=measure==null?text.Length*size*.5:measure.TextWidth(text,size);
            while(width>maxWidth && text.Length>4){text=text.Substring(0,text.Length-4)+"...";width=measure==null?text.Length*size*.5:measure.TextWidth(text,size);}
            ILoaderTextSurface italic=surface as ILoaderTextSurface;
            if(italic!=null && y<220)italic.LoaderText(240-width/2,y,text,size,.96*fade,.95*fade,fade);
            else surface.Text(240-width/2,y,text,size,.96*fade,.95*fade,fade);
        }
        private void LoaderGroup(double x,double y,string label,bool expandedGroup,Action toggle){
            Rect(x,y,108,13,.13,.10,.19,.8);Text(x+4,y+2,label,7);Text(x+98,y+2,expandedGroup?"-":"+",8);
            hits.Add(new Hit {X=x,Y=y,Width=108,Height=13,Action=toggle});
        }
        private void LoaderSlider(double x,double y,string label,double value,double maximum,Action<double> change){
            Muted(x+4,y,label,7.5);Muted(x+83,y,Math.Round(value*100)+"%",7);
            Rect(x+5,y+17,98,2,.16,.12,.2,1);Rect(x+5,y+17,98*value/maximum,2,1,.8,.2,1);surface.Circle(x+5+98*value/maximum,y+18,2.5,1,.85,.35,1,true);
            hits.Add(new Hit {X=x,Y=y+9,Width=108,Height=16,Action=delegate {loaderDrag=delegate(double pointer){change(Math.Max(0,Math.Min(maximum,(pointer-x-5)/98*maximum)));};loaderDrag(pointerX);}});
        }
        private void LoaderDisabledSlider(double x,double y,string label){surface.Text(x+4,y,label,7,.42,.39,.46);Rect(x+5,y+17,98,2,.2,.18,.23,1);surface.Circle(x+5,y+18,2,.4,.37,.43,1,true);}
        private void LoaderCheck(double x,double y,string label,bool value,bool enabled,Action toggle){
            Rect(x+4,y+2,6,6,enabled?.8:.35,enabled?.65:.31,enabled?.2:.39,value?.9:.35);
            surface.Text(x+14,y,label,7,enabled?.9:.45,enabled?.87:.42,enabled?.95:.49);
            if(enabled)hits.Add(new Hit {X=x,Y=y,Width=108,Height=16,Action=toggle});
        }
        public DifficultyCache DifficultyRatings;
        private string difficultySettingsKey;private ModSettings difficultySettings;
        private double LocalStars(SongEntry song){if(DifficultyRatings==null || song.PendingArchive!=null)return -1;string signature=mode+"|"+(Mods==null?"None":Mods.SummaryFor(mode)+"|"+Mods.SpeedOverride+"|"+Mods.DACs+"|"+Mods.DAAr+"|"+Mods.DAOd+"|"+Mods.DAHp+"|"+Mods.Seed);if(signature!=difficultySettingsKey){difficultySettingsKey=signature;difficultySettings=Mods==null||Mods.SummaryFor(mode)=="None"?null:new ModSettings(Mods,mode);}return DifficultyRatings.Request(song.Path,mode,difficultySettings);}
        private static readonly double[] starStops={.1,.1,1.25,2,2.5,3.3,4.2,4.9,5.8,6.7,7.7,9,10};
        private static readonly int[] starColours={0xaaaaaa,0x4290fb,0x4fc0ff,0x4fffd5,0x7cff4f,0xf6f05c,0xff8068,0xff4e6f,0xc645b8,0x6563de,0x18158e,0,0};
        private static double[] StarColour(double stars){stars=Math.Round(stars,2,MidpointRounding.AwayFromZero);int i=0;while(i<starStops.Length-1 && stars>=starStops[i+1])i++;int j=Math.Min(i+1,starStops.Length-1);double t=j==i?0:Math.Max(0,Math.Min(1,(stars-starStops[i])/(starStops[j]-starStops[i])));var rgb=new double[3];for(int k=0;k<3;k++){int shift=16-8*k;rgb[k]=(((starColours[i]>>shift)&255)*(1-t)+((starColours[j]>>shift)&255)*t)/255;}return rgb;}
        private void StarBadge(double cx,double cy,double low,double high){
            bool range=low>=0 && Math.Abs(high-low)>=.005;string label=low<0?"—":low.ToString("0.00",CultureInfo.InvariantCulture)+(range?"–"+high.ToString("0.00",CultureInfo.InvariantCulture):"");double width=range?101:62;double[] rgb=StarColour(high);double r=high<6.5?0:1,g=high<6.5?0:217/255.0,b=high<6.5?0:102/255.0;
            if(high>=9){double[] stops={9,9.9,10.6,11.5,12.4};int[] colours={0xf6f05c,0xff8068,0xff4e6f,0xc645b8,0x6563de};int n=0;while(n<4 && high>=stops[n+1])n++;int next=Math.Min(4,n+1);double t=n==next?0:(high-stops[n])/(stops[next]-stops[n]);r=(((colours[n]>>16)&255)*(1-t)+((colours[next]>>16)&255)*t)/255;g=(((colours[n]>>8)&255)*(1-t)+((colours[next]>>8)&255)*t)/255;b=((colours[n]&255)*(1-t)+(colours[next]&255)*t)/255;}
            Rect(cx-width/2+10,cy-10,width-20,20,rgb[0],rgb[1],rgb[2],1);surface.Circle(cx-width/2+10,cy,10,rgb[0],rgb[1],rgb[2],1,true);surface.Circle(cx+width/2-10,cy,10,rgb[0],rgb[1],rgb[2],1,true);
            double sx=cx-width/2+13;for(int i=0;i<5;i++){double a=-Math.PI/2+i*2*Math.PI/5,c=a+4*Math.PI/5;surface.Polyline(new double[]{Math.Cos(a)*4,Math.Sin(a)*4,Math.Cos(c)*4,Math.Sin(c)*4},2,sx,cy,1,1.7,r,g,b,1);}surface.Text(cx-width/2+23,cy-7,label,11,r,g,b);
        }

        private void DrawSongs()
        {
            if(BrowserPending){
                Text(20,104,"Preparing song list...",17);Muted(20,133,"Beatmaps will appear as they become available.",11);
                Link(12,246,77,31,"< back",delegate {BrowserPending=false;Screen=UiScreen.Play;});browserShellDrawn=true;return;
            }
            List<int> filtered = VisibleSongs();
            if (page * 3 >= filtered.Count) page = 0;
            if (selected < 0 && filtered.Count > 0 && rulesetSongAnchor==null) selected = filtered[page * 3];
            Rect(10, 51, 169, 175, 0.10, 0.075, 0.14, 0.94);
            if (PreviewSong!=null)
            {
                SongEntry song = PreviewSong;
                Cover(song,10,51,169,82);
                Text(22, 141, Clip(song.Title, 21), 14);
                LocalScore score;Scores.TryGetValue(song.Path+(mode==3?"|mania":mode==2?"|catch":mode==1 && song.Mode==0?"|taiko":""),out score);
                if(showScores) {
                    if(score==null){Muted(22,162,"No local scores yet",10);Muted(22,179,"Complete this beatmap",9);}
                    else {Text(22,159,score.Rank+"  "+score.Score.ToString("N0"),13);Muted(22,175,score.Accuracy.ToString("F2")+"%  "+score.MaxCombo+"x  plays "+score.Plays,9);Muted(22,188,mode==3?"MAX:"+score.Perfect+" 300:"+score.Great+" 200:"+score.Good+" miss:"+score.Miss:"300:"+score.Great+" 100:"+score.Ok+" 50:"+score.Meh+" miss:"+score.Miss,8);}
                } else { Muted(22, 162, Clip(song.Artist, 25), 10);Text(22, 178, Clip(song.Difficulty, 24), 10); }
                Link(112,134,63,20,showScores?"details":"scores",delegate {showScores=!showScores;});
                if(selected<0)Muted(12,236,"No compatible difficulty in "+modes[mode],9);
            }
            else { Text(22, 100, "No beatmaps", 16); Muted(22, 133, "Choose another ruleset", 10); }
            if(PreviewSong!=null){double stars=LocalStars(PreviewSong);if(stars<0 && DifficultyRatings==null && OnlineBrowser!=null && mode==PreviewSong.Mode)stars=OnlineBrowser.Rating(PreviewSong.SetId,mode,PreviewSong.Difficulty);StarBadge(94.5,226,stars,stars);}
            if(selected>=0 && PreviewSong!=null && (PreviewSong.AudioPath==null || !MenuPlaylist.Supported(PreviewSong.AudioPath)))Muted(12,236,"Audio preview unavailable on iOS 4",9);
            List<SongBrowserRow> rows=BrowserRows();ISongListSurface scrollSurface=surface as ISongListSurface;
            if(scrollSurface!=null)scrollSurface.SongList(rows,selected);
            else {
                double y=60;
                foreach(SongBrowserRow row in rows) {
                    double h=row.Difficulty?30:43;if(y+h>236)break;int index=row.Index;
                    Button(row.Difficulty?208:190,y,row.Difficulty?262:280,h,"",selected==index?.4:.20,.14,.28,delegate {SelectBrowserSong(index);},8);
                    if(!row.Difficulty){Cover(Songs[index],203,y+3,40,35);Text(251,y+3,Clip(row.Title,29),11);Muted(251,y+23,Clip(row.Subtitle,36),8);}
                    else Text(226,y+6,Clip(row.Title,35),11);
                    y+=h+2;
                }
            }
            Link(190,39,58,19,Mods==null?"mods":"mods "+Mods.Summary,delegate {if(Mods!=null){Screen=UiScreen.Mods;modPage=0;}});
            Link(251,39,68,19,"random",RandomSong);
            Link(322,39,70,19,"sort:"+(sortMode==0?"added":sortMode==1?"title":sortMode==2?"artist":"diff"),delegate {sortMode=(sortMode+1)%4;visibleSongs=null;page=0;});
            Link(395,39,75,19,String.IsNullOrEmpty(SearchQuery)?"search":"clear: "+Clip(SearchQuery,7),delegate {
                if(!String.IsNullOrEmpty(SearchQuery))SetSearch("");
                else if(RequestSongSearch!=null)RequestSongSearch();
            });
            Link(12, 246, 77, 31, "< back", delegate { if(CancelSongLoad!=null)CancelSongLoad();bool editing=EditorPicking;EditorPicking=false;Screen = editing?UiScreen.Edit:UiScreen.Play; });
            Link(94, 246, 83, 31, ImportBusy ? "working..." : "import .osz", delegate {
                if (ImportBusy) return;
                if (ImportArchives != null) ImportArchives();
                else ShowNotice("Import beatmaps", "Copy .osz files into Documents/Imports, then\ntap import .osz to unpack and rescan them.");
            });
            Link(190,246,100,31,VideoEnabled?"video: on":"video: off",delegate {VideoEnabled=!VideoEnabled;SaveSettings();});
            if(mode==3)Link(291,242,63,36,(ManiaScrollTime/1000.0).ToString("F1")+"s fall",delegate {ManiaScrollTime=ManiaScrollTime==2000?1500:ManiaScrollTime==1500?1000:2000;SaveSettings();});
            else if(PreviewSong!=null && PreviewSong.VideoPath==null)Muted(294,257,"image",8);
            Button(355, 240, 115, 38, SongPreparing ? "loading..." : selected<0?"Unavailable":PreviewSong!=null && PreviewSong.PendingArchive!=null?"load song":EditorPicking?"Edit":"Play", 0.40, 0.267, 0.80, delegate {
                if(PreviewSong!=null && PreviewSong.PendingArchive!=null){prioritizedArchive=null;PrioritizeSelected();return;}
                if(!SongPreparing && selected>=0){if(EditorPicking && EditBeatmapRequested!=null)EditBeatmapRequested(Songs[selected].Path);else if(StartSong!=null)StartSong(Songs[selected].Path);}
            }, 10);
        }
        private bool modConfiguration;private int modConfigField;
        private void DrawModConfiguration(){
            string[] names={"Speed","Accuracy target","Circle size","Approach rate","Overall difficulty","Health drain","Random seed"};double[] values={Mods.DisplayRate,Mods.MinimumAccuracy,Mods.DACs,Mods.DAAr,Mods.DAOd,Mods.DAHp,Mods.Seed};
            Text(14,76,"Mod settings",16);Muted(14,102,"Select the matching mod to use these values.",9);
            for(int i=0;i<7;i++){int field=i;double y=121+i*18;Link(14,y,205,18,(modConfigField==i?"> ":"  ")+names[i],delegate {modConfigField=field;});Text(250,y+2,values[i]<0?"map default":values[i]==0 && (i==0 || i==6)?"automatic":values[i].ToString("0.##"),10);}
            Action<double> adjust=delegate(double delta){switch(modConfigField){case 0:Mods.SpeedOverride=Math.Max(Mods.Enabled("DT") || Mods.Enabled("NC")?1.05:.5,Math.Min(Mods.Enabled("HT") || Mods.Enabled("DC")?.95:2,Mods.DisplayRate+delta*.05));break;case 1:Mods.MinimumAccuracy=Math.Max(60,Math.Min(99.9,Mods.MinimumAccuracy+delta));break;case 2:Mods.DACs=Math.Max(-1,Math.Min(11,Mods.DACs+delta));break;case 3:Mods.DAAr=Math.Max(-1,Math.Min(11,Mods.DAAr+delta));break;case 4:Mods.DAOd=Math.Max(-1,Math.Min(11,Mods.DAOd+delta));break;case 5:Mods.DAHp=Math.Max(-1,Math.Min(10,Mods.DAHp+delta));break;case 6:Mods.Seed=Math.Max(0,Mods.Seed+(int)delta);break;}Mods.Save();};
            Link(330,137,55,40,"-",delegate {adjust(-1);});Link(395,137,55,40,"+",delegate {adjust(1);});Link(330,196,120,30,"defaults",delegate {Mods.SpeedOverride=0;Mods.MinimumAccuracy=90;Mods.DACs=Mods.DAAr=Mods.DAOd=Mods.DAHp=-1;Mods.Seed=0;Mods.Save();});Link(400,250,68,30,"done",delegate {modConfiguration=false;});
        }
        private void DrawMods()
        {
            Rect(0,34,480,254,.08,.06,.12,.98);Text(14,40,"select mods",19);
            if(Mods==null){Muted(14,85,"Mod catalogue unavailable.",12);return;}
            Link(187,39,94,23,"configure",delegate {modConfiguration=!modConfiguration;});if(modConfiguration){DrawModConfiguration();return;}
            Muted(290,45,"score x"+Mods.MultiplierFor(mode).ToString("F2"),11);
            for(int i=0;i<LegacyModState.Categories.Length;i++) {
                int index=i;string category=LegacyModState.Categories[i];
                Button(8+i*93,66,90,27,category.Substring(0,Math.Min(10,category.Length)),i==modCategory?.38:.17,i==modCategory?.25:.13,i==modCategory?.68:.24,
                    delegate {modCategory=index;modPage=0;},0);
            }
            List<LegacyMod> list=Mods.InCategory(LegacyModState.Categories[modCategory]);int pages=Math.Max(1,(list.Count+3)/4);
            if(modPage>=pages)modPage=0;
            for(int row=0;row<4;row++) {
                int item=modPage*4+row;if(item>=list.Count)break;LegacyMod mod=list[item];double y=99+34*row;bool available=Mods.AvailableFor(mod,mode),on=available && Mods.Enabled(mod.Acronym);
                Button(12,y,456,31,"",!available?.10:on?.37:.18,!available?.10:on?.24:.14,!available?.12:on?.66:.25,
                    delegate {if(available)Mods.Toggle(mod);},7);
                string description=available?mod.Description:"Unavailable in this mode.";if(mode==2){if(!available)description="Unavailable in this mode.";else if(mod.Acronym=="HR")description="Smaller catcher and shifting fruit positions.";else if(mod.Acronym=="EZ")description="Larger catcher and two extra lives.";else if(mod.Acronym=="HD")description="Fruits fade before reaching the plate.";}
                if(mode==3){if(!available)description="Unavailable in this mode.";else if(mod.Acronym=="HR")description="Tighter timing windows.";else if(mod.Acronym=="EZ")description="Wider timing windows and two extra lives.";else if(mod.Acronym=="MR")description="Reverse the order of the columns.";else if(mod.Acronym=="PF")description="Only MAX judgements are allowed.";}
                Text(23,y+3,mod.Acronym,15);Text(66,y+2,mod.Name,12);Muted(66,y+16,Clip(description,55),8);
                double multiplier=mode==2 && mod.Acronym=="HR"?1.12:mod.Multiplier;
                Muted(420,y+8,available?(multiplier==1?"x1.00":"x"+multiplier.ToString("F2")):"N/A",8);
            }
            Link(12,244,68,34,"reset",Mods.Clear);Link(84,244,48,34,"<",delegate {modPage=(modPage+pages-1)%pages;});
            Text(139,252,(modPage+1)+"/"+pages,11);Link(170,244,48,34,">",delegate {modPage=(modPage+1)%pages;});
            Muted(230,251,"selected: "+Clip(Mods.SummaryFor(mode),25),9);Link(400,244,68,34,"done",delegate {Screen=UiScreen.Songs;});
        }
        private void DrawSettings()
        {
            // Full-screen dim and dismiss area, followed by lazer's left panel.
            Rect(0,0,480,320,0,0,0,.48);
            hits.Add(new Hit {X=370,Y=0,Width=110,Height=320,Action=delegate {Screen=settingsReturn;}});
            Rect(0,0,370,320,.075,.055,.105,1);
            Rect(0,0,56,320,.045,.038,.065,1);
            Text(72,18,"settings",27);Muted(72,49,"change the way osu! behaves",9);
            Link(328,8,32,28,"x",delegate {Screen=settingsReturn;});
            string[] sections={"GEN","AUD","UI","GFX","BG"};
            for(int i=0;i<sections.Length;i++) {
                int index=i;double y=70+i*38;
                Button(3,y,50,35,sections[i],settingsSection==i?.36:.10,settingsSection==i?.23:.085,settingsSection==i?.60:.15,delegate {settingsSection=index;},0);
            }
            Link(3,270,50,43,"<",delegate {Screen=settingsReturn;});
            Rect(70,70,278,1,.65,.60,.72,.35);
            if(settingsSection==0) {
                Text(72,82,"General",18);Muted(72,106,"Basic application behaviour",9);
                SettingToggle(72,135,"Menu animation",Animate?"Enabled":"Disabled",delegate {Animate=!Animate;SaveSettings();});
                Rect(72,183,276,1,.65,.60,.72,.25);Text(72,195,"Debug",16);
                SettingToggle(72,229,"Performance overlay",PerformanceOverlay?"Enabled":"Disabled",delegate {PerformanceOverlay=!PerformanceOverlay;SaveSettings();});
                Muted(72,264,"FPS / app CPU / device GPU",9);
                Muted(72,280,"GPU shows N/A if no counter is available.",8);
            } else if(settingsSection==1) {
                Text(72,82,"Audio",18);Muted(72,106,"Volume and playback",9);
                SettingStepper(72,135,"Master volume",Math.Round(Volume*100)+"%",delegate {Volume=Math.Max(0,Volume-.1);SaveSettings();},delegate {Volume=Math.Min(1,Volume+.1);SaveSettings();});
                SettingStepper(72,180,"Music volume",Math.Round(MusicVolume*100)+"%",delegate {MusicVolume=Math.Max(0,MusicVolume-.1);SaveSettings();},delegate {MusicVolume=Math.Min(1,MusicVolume+.1);SaveSettings();});
                SettingStepper(72,225,"Effects volume",Math.Round(EffectsVolume*100)+"%",delegate {EffectsVolume=Math.Max(0,EffectsVolume-.1);SaveSettings();},delegate {EffectsVolume=Math.Min(1,EffectsVolume+.1);SaveSettings();});
            } else if(settingsSection==2) {
                Text(72,82,"User interface",18);Muted(72,106,"Menu and overlay appearance",9);
                SettingStepper(72,135,"Background dim",Math.Round(Dim*100)+"%",delegate {Dim=Math.Max(0,Dim-.1);SaveSettings();},delegate {Dim=Math.Min(.8,Dim+.1);SaveSettings();});
                SettingToggle(72,185,"Toolbar clock",ClockMode==0?"Analog":ClockMode==1?"Full":ClockMode==2?"Digital + run":"Digital",delegate {ClockMode=(ClockMode+1)%4;SaveSettings();});
            } else if(settingsSection==3) {
                Text(72,82,"Graphics",18);Muted(72,106,"Performance for this iPod",9);
                SettingToggle(72,185,"Reduced effects",ReducedGameplayEffects?"Enabled":"Disabled",delegate {ReducedGameplayEffects=!ReducedGameplayEffects;SaveSettings();});
                SettingToggle(72,235,"Gameplay cursor",GameplayCursor?"Enabled":"Disabled",delegate {GameplayCursor=!GameplayCursor;SaveSettings();});
                Muted(72,278,"Cursor toggle works with either effects mode.",9);
                Muted(72,293,"The orange resume target remains visible.",9);
                SettingToggle(72,135,"Artwork size",artworkSizes[ArtworkQuality],delegate {ArtworkQuality=(ArtworkQuality+1)%3;SaveSettings();});
            } else {
                Text(72,82,"Menu backgrounds",18);
                SettingToggle(72,116,"Contest wallpapers",WallpapersEnabled?"Enabled":"Triangles",delegate {WallpapersEnabled=!WallpapersEnabled;PickWallpaper();SaveSettings();});
                for(int row=0;row<5;row++) {
                    int index=wallpaperPage*5+row;if(index>=WallpaperPacks.Count)break;string pack=WallpaperPacks[index];
                    Link(72,146+row*25,276,23,(disabledPacks.Contains(pack)?"[ ] ":"[x] ")+Clip(pack,38),delegate {
                        if(disabledPacks.Contains(pack))disabledPacks.Remove(pack);else disabledPacks.Add(pack);
                        PickWallpaper();SaveSettings();
                    });
                }
                Link(72,280,45,28,"<",delegate {if(wallpaperPage>0)wallpaperPage--;});
                Link(121,280,45,28,">",delegate {if((wallpaperPage+1)*5<WallpaperPacks.Count)wallpaperPage++;});
                Muted(180,288,"No packs selected: triangles",8);
            }
        }
        private void SettingToggle(double x,double y,string label,string value,Action action)
        {Text(x,y,label,12);Link(x+153,y-8,120,30,value,action);}
        private void SettingStepper(double x,double y,string label,string value,Action minus,Action plus)
        {Text(x,y,label,12);Link(x+153,y-8,34,30,"-",minus);Text(x+194,y,value,10);Link(x+239,y-8,34,30,"+",plus);}
        private void DrawNotice()
        {
            Rect(47, 54, 386, 222, 0.10, 0.075, 0.15, 0.99);
            Rect(47, 54, 4, 222, 0.93, 0.20, 0.52, 1);
            Text(66, 67, noticeTitle, 20);
            string[] lines = noticeBody.Split('\n');
            for (int i = 0; i < Math.Min(lines.Length, 7); i++) Muted(66, 104 + i * 17, lines[i], 12);
            Link(66, 239, 84, 29, "< back", delegate { Screen = noticeReturn; });
        }
    }
}
