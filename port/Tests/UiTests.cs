using System;
using System.IO;
using System.Text;
using System.Globalization;
using System.Collections.Generic;
using OsuLegacy;

public sealed class SvgUiSurface : IUiSurface,ICatchSurface,ILoaderTextSurface,ILoaderBackgroundSurface,INotificationLayerSurface,IHudDrawingSurface,IMenuOverlaySurface,ITopOverlaySurface,IUiClipSurface
{
    private int clipId;public void BeginClip(double x,double y,double w,double h){int id=++clipId;svg.Append("<defs><clipPath id='clip"+id+"'><rect x='"+N(x)+"' y='"+N(y)+"' width='"+N(w)+"' height='"+N(h)+"'/></clipPath></defs><g clip-path='url(#clip"+id+")'>");}public void EndClip(){svg.Append("</g>");}
    public void PushHud(double x,double y,double scale,double angle,double opacity){svg.Append("<g transform='translate("+N(x)+" "+N(y)+") rotate("+N(angle*180/Math.PI)+") scale("+N(scale)+")' opacity='"+N(opacity)+"'>");}
    public void PopHud(){svg.Append("</g>");}
    public int TopOverlayStarts;public void BeginTopOverlay(){TopOverlayStarts++;}
    public bool LogoBehindUi;public void SettingsAboveLogo(bool active){LogoBehindUi=active;}
    public double NotificationX,NotificationY,NotificationWidth,NotificationHeight;
    public void NotificationRegion(double x,double y,double width,double height){NotificationX=x;NotificationY=y;NotificationWidth=width;NotificationHeight=height;}
    public void Push(double x,double y,double angle){svg.Append("<g transform='translate("+N(x)+" "+N(y)+") rotate("+N(angle*180/Math.PI)+")'>");}
    public void Pop(){svg.Append("</g>");}
    public void Ellipse(double x,double y,double rx,double ry,double r,double g,double b,double alpha){svg.Append("<ellipse cx='"+N(x)+"' cy='"+N(y)+"' rx='"+N(rx)+"' ry='"+N(ry)+"' fill='"+Colour(r,g,b)+"' opacity='"+N(alpha)+"'/>");}
    private readonly StringBuilder svg = new StringBuilder();
    private readonly string assets;
    public readonly System.Collections.Generic.List<string> ArtworkRequests = new System.Collections.Generic.List<string>();
    public readonly System.Collections.Generic.List<string> ImageRequests = new System.Collections.Generic.List<string>();
    public bool RejectArtwork;
    public string BackgroundRequest;
    private static string N(double value) { return value.ToString("0.###", CultureInfo.InvariantCulture); }
    private static string Escape(string value) { return value.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;"); }
    private static string Colour(double r, double g, double b) { return "rgb(" + N(r * 255) + "," + N(g * 255) + "," + N(b * 255) + ")"; }
    public SvgUiSurface(string assetDirectory)
    { assets = assetDirectory; svg.Append("<svg xmlns='http://www.w3.org/2000/svg' xmlns:xlink='http://www.w3.org/1999/xlink' width='960' height='640' viewBox='0 0 480 320'>"); }
    public void Rect(double x, double y, double w, double h, double r, double g, double b, double a, double slant)
    {
        svg.Append("<polygon points='" + N(x + slant) + "," + N(y) + " " + N(x+w) + "," + N(y) + " " + N(x+w-slant) + "," + N(y+h) + " " + N(x) + "," + N(y+h) + "' fill='" + Colour(r,g,b) + "' opacity='" + N(a) + "'/>");
    }
    public void Circle(double x, double y, double radius, double r, double g, double b, double a, bool fill)
    { svg.Append("<circle cx='" + N(x) + "' cy='" + N(y) + "' r='" + N(radius) + "' opacity='" + N(a) + "' fill='" + (fill ? Colour(r,g,b) : "none") + "' stroke='" + Colour(r,g,b) + "' stroke-width='2'/>"); }
    public void Text(double x, double y, string text, double size, double r, double g, double b)
    { svg.Append("<text x='" + N(x) + "' y='" + N(y + size) + "' font-family='Arial,Helvetica,sans-serif' font-size='" + N(size) + "' fill='" + Colour(r,g,b) + "'>" + Escape(text) + "</text>"); }
    public void LoaderText(double x,double y,string text,double size,double r,double g,double b){svg.Append("<g font-style='italic'>");Text(x,y,text,size,r,g,b);svg.Append("</g>");}
    public void LoaderBackground(string path,bool bundled){svg.Append("<defs><filter id='loader-blur'><feGaussianBlur stdDeviation='6'/></filter></defs><g filter='url(#loader-blur)'>");Background(path,bundled,.2);svg.Append("</g>");}
    public void Image(string name, double x, double y, double w, double h, double alpha)
    {
        ImageRequests.Add(name);
        string mime = name.EndsWith("png") ? "image/png" : "image/jpeg";
        svg.Append("<image x='" + N(x) + "' y='" + N(y) + "' width='" + N(w) + "' height='" + N(h) + "' preserveAspectRatio='none' opacity='" + N(alpha) + "' xlink:href='data:" + mime + ";base64," + Convert.ToBase64String(File.ReadAllBytes(Path.Combine(assets,name))) + "'/>");
    }
    public bool Artwork(string path,double x,double y,double w,double h,double alpha)
    {
        ArtworkRequests.Add(path);
        if(x==0 && y==0 && w==480 && h==320)BackgroundRequest=path;
        if(RejectArtwork || !File.Exists(path))return false;
        string extension=Path.GetExtension(path).ToLowerInvariant();
        string mime=extension==".png"?"image/png":extension==".gif"?"image/gif":"image/jpeg";
        svg.Append("<image x='"+N(x)+"' y='"+N(y)+"' width='"+N(w)+"' height='"+N(h)+"' preserveAspectRatio='xMidYMid slice' opacity='"+N(alpha)+"' xlink:href='data:"+mime+";base64,"+Convert.ToBase64String(File.ReadAllBytes(path))+"'/>");
        return true;
    }
    public bool Background(string path,bool bundled,double dim)
    {
        bool success;
        if(bundled){Image(path,0,0,480,320,1);success=true;}
        else success=path!=null && Artwork(path,0,0,480,320,1);
        Rect(0,0,480,320,0.035,0.025,0.06,dim,0);
        return success;
    }
    public void Polyline(double[] points,int count,double x,double y,double scale,double width,double r,double g,double b,double alpha)
    {
        svg.Append("<polyline points='");
        for(int i=0;i<count;i++)svg.Append(N(x+points[i*2]*scale)+","+N(y+points[i*2+1]*scale)+" ");
        svg.Append("' fill='none' stroke='"+Colour(r,g,b)+"' stroke-width='"+N(width*scale)+"' stroke-linecap='round' stroke-linejoin='round' opacity='"+N(alpha)+"'/>");
    }
    public void Arc(double x,double y,double radius,double progress,double width,double r,double g,double b,double alpha)
    {
        if(progress<=0)return;
        if(progress>=1){svg.Append("<circle cx='"+N(x)+"' cy='"+N(y)+"' r='"+N(radius)+"' fill='none' stroke='"+Colour(r,g,b)+"' stroke-width='"+N(width)+"'/>");return;}
        double angle=-Math.PI/2+progress*2*Math.PI;
        svg.Append("<path d='M "+N(x)+" "+N(y-radius)+" A "+N(radius)+" "+N(radius)+" 0 "+(progress>0.5?"1":"0")+" 1 "+N(x+Math.Cos(angle)*radius)+" "+N(y+Math.Sin(angle)*radius)+"' fill='none' stroke='"+Colour(r,g,b)+"' stroke-width='"+N(width)+"' opacity='"+N(alpha)+"'/>");
    }
    public void Save(string path) { File.WriteAllText(path,svg.ToString() + "</svg>"); }
}

public static class UiTests
{
    static int assertions;
    static void Check(bool ok, string name) { assertions++; if (!ok) throw new Exception(name); }
    static void Render(LazerUi ui, string assets, string output, string name)
    { var surface = new SvgUiSurface(assets); ui.Draw(surface, 0); if (name != null) surface.Save(Path.Combine(output,name + ".svg")); }
    public static void Main(string[] args)
    {
        string assets=args[0], output=args[1]; Directory.CreateDirectory(output);
        var ui = new LazerUi {Animate=false};
        for (int i=0;i<8;i++) ui.Songs.Add(new SongEntry { Path="map"+i, Title=i==0 ? "Welcome to osu!" : "Test song " + i, Artist="osu! community", Difficulty="Normal", Mode=i==7 ? 3 : 0 });
        Render(ui,assets,output,"welcome");
        ui.Tap(240,160); Render(ui,assets,output,"menu");
        ui.Tap(430,17);Check(ui.ClockMode==1,"Clock cycles from analog to full");
        Render(ui,assets,output,null);ui.Tap(430,17);Check(ui.ClockMode==2,"Clock cycles to digital with runtime");
        Render(ui,assets,output,null);ui.Tap(430,17);Check(ui.ClockMode==3,"Clock cycles to digital");
        Render(ui,assets,output,null);ui.Tap(430,17);Check(ui.ClockMode==0,"Clock cycles back to analog");
        Render(ui,assets,output,null);
        ui.Tap(220,155); Check(ui.Screen==UiScreen.Play,"Play navigation");
        Check(ui.MainMenuClockVisible,"Clock remains visible in Play submenu");
        Render(ui,assets,output,null);ui.Tap(40,160);Check(ui.Screen==UiScreen.Home,"Play submenu replaces settings with back");
        Render(ui,assets,output,null);ui.Tap(330,155);Check(ui.Screen==UiScreen.Edit,"Edit navigation");
        Render(ui,assets,output,"edit");ui.Tap(420,155);Check(ui.Screen==UiScreen.SkinEditor,"Skin editor is in the shared horizontal row");
        ui.Screen=UiScreen.Edit;Render(ui,assets,output,null);ui.Tap(40,160);Check(ui.Screen==UiScreen.Home,"Edit submenu replaces settings with back");
        Render(ui,assets,output,null);ui.Tap(220,155);Render(ui,assets,output,null);ui.Tap(40,260);Check(ui.Screen==UiScreen.Play,"Redundant lower main-menu back button is removed");

        Render(ui,assets,output,"play"); ui.Tap(220,155); Check(ui.Screen==UiScreen.Songs,"Solo navigation");
        Check(ui.SharedToolbarVisible && !ui.MainMenuClockVisible,"Song browser inherits shared toolbar without main-menu logo interaction");
        var toolbarPreview=new SvgUiSurface(assets);ui.Draw(toolbarPreview,0);foreach(string rulesetIcon in new string[]{"osu","taiko","catch","mania"})Check(toolbarPreview.ImageRequests.Contains("toolbar-ruleset-"+rulesetIcon+".png"),"Browser toolbar uses upstream "+rulesetIcon+" icon");
        var logoNav=new LazerUi {Animate=false};logoNav.Draw(new SvgUiSurface(assets),0);logoNav.MenuPointer(0,240,160);var topIcons=new SvgUiSurface(assets);logoNav.Draw(topIcons,1);foreach(string icon in new[]{"settings","logo","edit-circle","beatmap"})Check(topIcons.ImageRequests.Contains("menu-icon-"+icon+".png"),"Top-level menu displays upstream "+icon+" icon");logoNav.MenuPointer(0,174,160);Check(logoNav.Screen==UiScreen.Play,"Expanded logo advances to Play");var playIcons=new SvgUiSurface(assets);logoNav.Draw(playIcons,2);foreach(string icon in new[]{"prev-circle","player","online","tournament"})Check(playIcons.ImageRequests.Contains("menu-icon-"+icon+".png"),"Play menu displays upstream "+icon+" icon");Check(!playIcons.ImageRequests.Contains("menu-icon-settings.png"),"Back icon replaces settings in Play");logoNav.MenuPointer(0,174,160);Check(logoNav.Screen==UiScreen.Songs,"Play logo accepts Solo rather than returning home");logoNav.Screen=UiScreen.Edit;var editIcons=new SvgUiSurface(assets);logoNav.Draw(editIcons,3);Check(editIcons.ImageRequests.Contains("menu-icon-skin-b.png") && editIcons.ImageRequests.Contains("menu-icon-beatmap.png"),"Edit displays both upstream editor icons");logoNav.Tap(174,160);Check(logoNav.Screen==UiScreen.Songs && logoNav.EditorPicking,"Edit logo accepts beatmap editor via common click action");
        var idleMenu=new LazerUi{Animate=false};idleMenu.Draw(new SvgUiSurface(assets),0);Check(idleMenu.MenuLogoDiameter==264 && idleMenu.MenuLogoX==240,"Retracted logo is large and centred");idleMenu.MenuPointer(0,240,160);idleMenu.Draw(new SvgUiSurface(assets),1);Check(idleMenu.MenuLogoDiameter==198 && idleMenu.MenuLogoX==150,"Open logo protrudes above and below the row while remaining smaller than idle");Check(!idleMenu.PollMenuIdle(6),"Idle does not retract before upstream six-second threshold");Check(idleMenu.PollMenuIdle(6.01),"Idle retracts expanded menu at upstream threshold");var idleSurface=new SvgUiSurface(assets);idleMenu.Draw(idleSurface,7);Check(!idleMenu.MainMenuExpanded && idleMenu.Screen==UiScreen.Home && idleMenu.MenuLogoDiameter==264 && idleMenu.MenuLogoX==240,"Idle restores large centred logo");Check(!idleSurface.ImageRequests.Exists(delegate(string name){return name.StartsWith("menu-icon-");}),"Idle removes the whole button strip");idleMenu.Screen=UiScreen.Play;idleMenu.NoteMenuInteraction(8);idleMenu.Draw(new SvgUiSurface(assets),8);Check(idleMenu.PollMenuIdle(14.01) && idleMenu.Screen==UiScreen.Home,"Idle retracts Play submenu");idleMenu.Screen=UiScreen.Edit;idleMenu.NoteMenuInteraction(15);idleMenu.Draw(new SvgUiSurface(assets),15);Check(idleMenu.PollMenuIdle(21.01),"Idle retracts Edit submenu");idleMenu.Screen=UiScreen.Songs;idleMenu.NoteMenuInteraction(22);Check(!idleMenu.PollMenuIdle(40) && idleMenu.Screen==UiScreen.Songs,"Idle never leaves song browser");
        var timingMenu=new LazerUi();timingMenu.Draw(new SvgUiSurface(assets),0);timingMenu.MenuPointer(0,240,160);timingMenu.Draw(new SvgUiSurface(assets),0);timingMenu.Draw(new SvgUiSurface(assets),.2);Check(timingMenu.MenuLogoDiameter<200 && timingMenu.MenuLogoDiameter>196,"Logo opening completes in 200ms");timingMenu.Draw(new SvgUiSurface(assets),1);double menuSize=timingMenu.MenuLogoDiameter;timingMenu.Screen=UiScreen.Edit;timingMenu.Draw(new SvgUiSurface(assets),1.01);Check(Math.Abs(timingMenu.MenuLogoDiameter-menuSize)<2,"Switching submenus does not restart logo scaling");timingMenu.NoteMenuInteraction(2);timingMenu.PollMenuIdle(8.01);timingMenu.Draw(new SvgUiSurface(assets),8.01);timingMenu.Draw(new SvgUiSurface(assets),8.97);Check(timingMenu.MenuLogoDiameter>261 && Math.Abs(timingMenu.MenuLogoX-240)<.01,"Idle return completes after 150ms delay and 800ms restore");
        var tileMenu=new LazerUi{Animate=false};tileMenu.Draw(new SvgUiSurface(assets),0);tileMenu.MenuPointer(0,240,160);tileMenu.Draw(new SvgUiSurface(assets),1);tileMenu.MenuPointer(0,55,120);tileMenu.MenuPointer(2,55,120);Check(tileMenu.Screen==UiScreen.Settings,"Settings fills the same full-height row as other buttons");
        var chrome=new LazerUi{Animate=false};var closedChrome=new SvgUiSurface(assets);chrome.Draw(closedChrome,0);chrome.DrawNotificationOverlay(closedChrome,0,false);Check(!chrome.MenuChromeVisible && !chrome.SharedToolbarVisible && !closedChrome.ImageRequests.Contains("toolbar-home.png") && !closedChrome.ImageRequests.Contains("toolbar-notification.png"),"Idle hides header, clock and notification bell");string chromeFile=Path.Combine(output,"idle-chrome.svg");closedChrome.Save(chromeFile);Check(!File.ReadAllText(chromeFile).Contains("gh/IndiiSkies unofficial client"),"Idle hides footer text");chrome.MenuPointer(0,240,160);var openChrome=new SvgUiSurface(assets);chrome.Draw(openChrome,1);chrome.DrawNotificationOverlay(openChrome,1,false);Check(chrome.MenuChromeVisible && chrome.SharedToolbarVisible && openChrome.ImageRequests.Contains("toolbar-home.png") && openChrome.ImageRequests.Contains("toolbar-notification.png"),"Opening menu restores header and bell");chrome.NotificationPointer(0,464,17);var notificationLayers=new SvgUiSurface(assets);chrome.Draw(notificationLayers,2);chrome.DrawNotificationOverlay(notificationLayers,2,false);Check(chrome.NotificationsOpen && !notificationLayers.LogoBehindUi,"Notifications do not move navigation above the logo");chrome.CloseNotifications();chrome.Notifications.Post("layer-toast","Layer check","Toast remains a separate top layer",NotificationKind.Info,false,null);var toastLayers=new SvgUiSurface(assets);chrome.Draw(toastLayers,3);chrome.DrawNotificationOverlay(toastLayers,3,false);Check(!toastLayers.LogoBehindUi,"Toast does not move navigation above logo");chrome.Tap(20,17);var settingsLayers=new SvgUiSurface(assets);chrome.Draw(settingsLayers,4);chrome.DrawNotificationOverlay(settingsLayers,4,false);Check(settingsLayers.LogoBehindUi,"Settings keeps its explicit overlay priority");
        string launched=null; ui.StartSong=delegate(string path) { launched=path; };
        Render(ui,assets,output,"songs");ui.SelectBrowserSong(0);Render(ui,assets,output,null);ui.Tap(410,255);Check(launched=="map0","Selected song launches");
        ui.SetSearch("welcome");Render(ui,assets,output,null);ui.Tap(410,255);Check(launched=="map0","Search matches song title");
        ui.SetSearch("community");Render(ui,assets,output,null);ui.Tap(410,255);Check(launched=="map0","Search matches artist case-insensitively");
        ui.SetSearch("missing");Render(ui,assets,output,null);ui.Tap(410,255);Check(launched=="map0","Empty search result cannot launch a stale selection");
        ui.SetSearch("");
        Render(ui,assets,output,null);ui.SelectBrowserSong(3);Render(ui,assets,output,null);
        ui.Tap(410,255);Check(launched=="map3","Scroll-list selection launches the selected difficulty");
        Render(ui,assets,output,null); ui.Tap(195,17); Render(ui,assets,output,null); ui.Tap(410,255); Check(launched=="map3","Mania tab preserves the selected standard map for conversion");
        ui.SelectBrowserSong(7);Render(ui,assets,output,null);ui.Tap(410,255);Check(launched=="map7","Mania tab also launches native mania maps");
        Render(ui,assets,output,null); ui.Tap(130,255); Check(ui.Screen==UiScreen.Notice,"Import status opens");
        Render(ui,assets,output,null); ui.Tap(100,250); Check(ui.Screen==UiScreen.Songs,"Notice returns to songs");
        int importRequests=0; ui.ImportArchives=delegate { importRequests++; };
        Render(ui,assets,output,null); ui.Tap(130,255); Check(importRequests==1,"Import action invokes archive worker");
        ui.ImportBusy=true; Render(ui,assets,output,null); ui.Tap(130,255); Check(importRequests==1,"Busy import does not start another worker"); ui.ImportBusy=false;
        Render(ui,assets,output,null); ui.Tap(40,255); Check(ui.Screen==UiScreen.Play,"Song back navigation");
        Render(ui,assets,output,null); ui.Tap(20,17); Check(ui.Screen==UiScreen.Settings,"Toolbar settings remains available in submenus");
        ui.SettingsPath=Path.Combine(output,"settings-test.cfg");
        bool? overlayEnabled=null;ui.PerformanceOverlayChanged=delegate(bool enabled){overlayEnabled=enabled;};
        Render(ui,assets,output,null);ui.Tap(25,90);Render(ui,assets,output,"settings-general-debug");ui.Tap(290,229);
        Check(ui.PerformanceOverlay && overlayEnabled==true,"General Debug toggle enables the native performance overlay");
        var debugSettings=new LazerUi {SettingsPath=ui.SettingsPath};debugSettings.LoadSettings();
        Check(debugSettings.PerformanceOverlay,"Performance overlay setting persists");
        Render(ui,assets,output,null);ui.Tap(290,229);Check(!ui.PerformanceOverlay && overlayEnabled==false,"Debug overlay can be disabled");
        double volume=-1; ui.VolumeChanged=delegate(double value) { volume=value; };
        Render(ui,assets,output,"settings");ui.Tap(25,90);Render(ui,assets,output,null);ui.Tap(280,142);Render(ui,assets,output,null);ui.Tap(25,120);Render(ui,assets,output,null);ui.Tap(320,142);Check(volume > 0.8,"Volume control invokes audio binding");
        double musicGain=-1,effectsGain=-1;ui.MusicVolumeChanged=delegate(double value){musicGain=value;};ui.EffectsVolumeChanged=delegate(double value){effectsGain=value;};
        Render(ui,assets,output,null);ui.Tap(235,180);Render(ui,assets,output,null);ui.Tap(235,225);
        Check(Math.Abs(musicGain-.9)<.001 && Math.Abs(effectsGain-.9)<.001,"Music and effects controls invoke separate volume bindings");
        Render(ui,assets,output,null);ui.Tap(25,160);Render(ui,assets,output,null);ui.Tap(320,142);Render(ui,assets,output,null);ui.Tap(260,185);
        int quality=-1;ui.ArtworkQualityChanged=delegate(int value){quality=value;};
        Render(ui,assets,output,null);ui.Tap(25,196);Render(ui,assets,output,null);ui.Tap(280,142);Check(quality==1,"Half-resolution artwork option invokes renderer");
        Render(ui,assets,output,null);ui.Tap(280,142);Check(quality==2,"Quarter-resolution artwork option invokes renderer");
        var restored=new LazerUi { SettingsPath=ui.SettingsPath }; restored.LoadSettings();
        Check(Math.Abs(restored.Volume-ui.Volume)<0.0001 && Math.Abs(restored.Dim-ui.Dim)<0.0001 && restored.Animate==ui.Animate,"Settings persist");
        Check(restored.ArtworkQuality==2,"Artwork resolution persists");
        Check(Math.Abs(restored.MusicVolume-.9)<.001 && Math.Abs(restored.EffectsVolume-.9)<.001,"Separate audio volumes survive settings reload");ui.Animate=false;
        string wallpaperManifest=Path.Combine(output,"wallpaper-test.tsv");
        File.WriteAllText(wallpaperManifest,"Pack A\tmenu-background.jpg\tArtist A\nPack B\tdemo-background.jpg\tArtist B\n");
        var wallpapers=new LazerUi();wallpapers.LoadWallpapers(wallpaperManifest);
        Check(wallpapers.WallpaperPacks.Count==2 && wallpapers.UsingWallpaper,"Wallpaper manifest groups packs and selects startup background");
        var firstWallpaper=new SvgUiSurface(assets);wallpapers.Draw(firstWallpaper,0);string initialWallpaper=firstWallpaper.ImageRequests[0];
        wallpapers.Screen=UiScreen.Play;var submenuWallpaper=new SvgUiSurface(assets);wallpapers.Draw(submenuWallpaper,1);
        Check(submenuWallpaper.ImageRequests[0]==initialWallpaper,"Main-menu submenus preserve the current wallpaper");
        wallpapers.AdvanceMenuBackground();var nextWallpaper=new SvgUiSurface(assets);wallpapers.Draw(nextWallpaper,2);
        Check(nextWallpaper.ImageRequests[0]!=initialWallpaper,"Track-change event advances seasonal rotation");
        wallpapers.Screen=UiScreen.Songs;wallpapers.Draw(new SvgUiSurface(assets),3);wallpapers.Screen=UiScreen.Play;
        var resumedWallpaper=new SvgUiSurface(assets);wallpapers.Draw(resumedWallpaper,4);
        Check(resumedWallpaper.ImageRequests[0]==initialWallpaper,"Returning from song browser advances to the next wallpaper");
        wallpapers.WallpapersEnabled=false;Check(!wallpapers.UsingWallpaper,"Disabled wallpapers restore triangle background");
        Render(ui,assets,output,null); ui.Tap(28,290); Check(ui.Screen==UiScreen.Play,"Settings sidebar returns to previous menu");
        Render(ui,assets,output,null); ui.Tap(350,155); Check(ui.Screen==UiScreen.Notice,"Unported destination reports status");
        ui.ShowNotice("Beatmap import","Import finished"); Render(ui,assets,output,null); ui.Tap(100,250);
        Check(ui.Screen==UiScreen.Play,"Replacing a notice preserves its back destination");
        string game=Path.Combine(output,"gameplay-fixture");Directory.CreateDirectory(game);
        File.WriteAllText(Path.Combine(game,"audio.wav"),"fixture");
        string header="osu file format v14\n[General]\nAudioFilename: audio.wav\n[Difficulty]\nOverallDifficulty: 5\nSliderMultiplier: 1.4\n[TimingPoints]\n0,500,4,1,0,100,1,0\n[HitObjects]\n";
        string mapPath=Path.Combine(game,"map.osu");
        File.WriteAllText(mapPath,header+"100,192,1000,2,0,B|200:40|400:192,2,350\n256,192,5000,8,0,8000\n");
        var session=new Session(Beatmap.Load(mapPath));
        var gameView=new GameplayView();
        session.TouchDown(1,100,192,1000);
        PathPoint follow=session.Map.Objects[0].PositionAt(1400);session.TouchMove(1,follow.X,follow.Y,1400);
        var sliderPreview=new SvgUiSurface(assets);gameView.Draw(sliderPreview,session,1400,480,320);sliderPreview.Save(Path.Combine(output,"slider.svg"));
        session.TouchDown(2,356,192,5000);
        for(int i=1;i<=96;i++){double angle=i*2*Math.PI*3/96;session.TouchMove(2,256+100*Math.Cos(angle),192+100*Math.Sin(angle),5000+i*490.0/96);}
        var spinnerPreview=new SvgUiSurface(assets);gameView.Draw(spinnerPreview,session,5500,480,320);spinnerPreview.Save(Path.Combine(output,"spinner.svg"));
        ArtworkTests.Run(assets,output);
        var grouped=new LazerUi {Screen=UiScreen.Songs};
        grouped.Songs.Add(new SongEntry {Path="a/easy.osu",SetId=10,Title="Same song",Artist="Artist",Difficulty="Easy"});
        grouped.Songs.Add(new SongEntry {Path="a/hard.osu",SetId=10,Title="Same song",Artist="Artist",Difficulty="Hard"});
        grouped.Songs.Add(new SongEntry {Path="b/normal.osu",SetId=20,Title="Same song",Artist="Artist",Difficulty="Normal"});
        Render(grouped,assets,output,null);grouped.SelectBrowserSong(0);var groupedRows=grouped.BrowserRows();
        Check(groupedRows.Count==4 && !groupedRows[0].Difficulty && groupedRows[1].Difficulty && groupedRows[2].Difficulty,"One song section expands its related difficulties");
        grouped.SelectBrowserSong(2);Check(grouped.BrowserRows().Count==3,"Different beatmap sets remain separate and previous set collapses");
        grouped.SetSearch("Hard");Render(grouped,assets,output,null);
        Check(grouped.BrowserRows().Count==2 && grouped.PreviewSong.Difficulty=="Hard","Search filters grouped difficulties without stale selection");
        var initialSelection=new LazerUi();initialSelection.Songs.AddRange(grouped.Songs);
        initialSelection.OpenSongs();SongEntry initialSong=initialSelection.PreviewSong;
        Check(initialSong!=null && initialSelection.Songs.Contains(initialSong),"Initial Solo entry chooses a valid random beatmap when no working beatmap exists");
        initialSelection.Screen=UiScreen.Play;initialSelection.OpenSongs();
        Check(initialSelection.PreviewSong==initialSong,"Reopening Solo preserves its previous valid selection");
        var edge=new LazerUi {Volume=.5,MusicVolume=.5,EffectsVolume=.5};
        string edgeConfig=Path.Combine(output,"edge-controls.cfg");edge.SettingsPath=edgeConfig;
        double appliedMaster=-1,appliedMusic=-1,appliedBrightness=-1;
        edge.VolumeChanged=delegate(double v){appliedMaster=v;};edge.MusicVolumeChanged=delegate(double v){appliedMusic=v;};edge.BrightnessChanged=delegate(double v){appliedBrightness=v;};
        Check(!edge.EdgePointer(0,1,240,160,10),"Centre touches do not activate edge controls");
        Check(edge.EdgePointer(0,1,5,200,10),"Left edge captures volume gesture");
        edge.EdgePointer(1,2,5,0,10.1);Check(edge.Volume==.5,"Second finger cannot change captured control");
        edge.EdgePointer(1,1,5,90,10.2);edge.EdgePointer(2,1,5,90,10.3);
        Check(edge.Volume==1 && appliedMaster==1,"Upward slide adjusts and clamps master volume live");
        edge.EdgePointer(0,1,57,249,10.4);edge.EdgePointer(1,1,57,139,10.5);edge.EdgePointer(2,1,57,139,10.6);
        Check(edge.MusicVolume==1 && appliedMusic==1 && edge.EffectsVolume==.5,"Selecting music ring leaves effects unchanged");
        var savedEdge=new LazerUi {SettingsPath=edgeConfig};savedEdge.LoadSettings();Check(savedEdge.Volume==1 && savedEdge.MusicVolume==1,"Edge volume adjustments persist");
        edge.Draw(new SvgUiSurface(assets),12);Check(!edge.EdgeOverlayActive(12),"Auto-hide repaints and clears overlay");
        edge.EdgePointer(0,1,477,200,13);edge.EdgePointer(1,1,477,90,13.1);edge.EdgePointer(2,1,477,90,13.2);
        Check(appliedBrightness==1 && edge.Volume==1,"Right edge adjusts brightness independently");
        edge.Screen=UiScreen.Songs;Check(!edge.EdgePointer(0,1,5,160,14),"Edge controls are disabled outside main menu");
        var edgeLayers=new LazerUi{Animate=false,Screen=UiScreen.Play};double edgeAt=20;foreach(double y in new double[]{162,76,249}){edgeLayers.EdgePointer(0,1,5,y,edgeAt);var layerSurface=new SvgUiSurface(assets);edgeLayers.Draw(layerSurface,edgeAt);Check(layerSurface.TopOverlayStarts==1 && !layerSurface.LogoBehindUi,"Volume panel uses independent top layer for ring at "+y);edgeLayers.EdgePointer(2,1,5,y,edgeAt+.01);edgeAt+=.1;}edgeLayers.EdgePointer(0,1,477,162,edgeAt);var brightnessLayer=new SvgUiSurface(assets);edgeLayers.Draw(brightnessLayer,edgeAt);Check(brightnessLayer.TopOverlayStarts==1 && !brightnessLayer.LogoBehindUi,"Brightness panel leaves the logo above navigation");edgeLayers.EdgePointer(2,1,477,162,edgeAt+.01);var expiredLayer=new SvgUiSurface(assets);edgeLayers.Draw(expiredLayer,edgeAt+2);Check(!edgeLayers.EdgeOverlayActive(edgeAt+2) && !expiredLayer.LogoBehindUi,"Panel expiration retains normal logo ordering");
        var tabs=new LazerUi();tabs.Screen=UiScreen.Songs;
        var logoTap=new LazerUi();logoTap.Draw(new SvgUiSurface(assets),0);logoTap.MenuPointer(0,295,160);Check(logoTap.MainMenuExpanded,"Logo receives a tap on the right of its closed position");logoTap.Draw(new SvgUiSurface(assets),.01);logoTap.MenuPointer(0,295,160);logoTap.MenuPointer(2,295,160);Check(logoTap.MainMenuExpanded && logoTap.Screen==UiScreen.Play,"Visible moving logo accepts Play rather than collapsing or activating a button underneath");
        var settingsTap=new LazerUi();settingsTap.Animate=false;settingsTap.Draw(new SvgUiSurface(assets),0);settingsTap.MenuPointer(0,240,160);settingsTap.Draw(new SvgUiSurface(assets),1);settingsTap.MenuPointer(0,40,150);settingsTap.MenuPointer(0,20,17);settingsTap.MenuPointer(2,40,150);settingsTap.Draw(new SvgUiSurface(assets),2);Check(settingsTap.Screen==UiScreen.Settings,"Overlapping settings presses cannot make the overlay its own background");settingsTap.MenuPointer(0,342,20);settingsTap.Draw(new SvgUiSurface(assets),3);Check(settingsTap.Screen==UiScreen.Home,"Settings safely returns after overlapping press/release events");
        var deferred=new LazerUi();deferred.DeferBrowserEntry=true;deferred.Songs.Add(new SongEntry {Path="deferred",Title="Deferred song"});deferred.OpenSongs();Check(deferred.BrowserPending && !deferred.CompletePendingBrowserEntry(),"Browser preparation waits until its shell has drawn");
        Render(deferred,assets,output,null);Check(deferred.BrowserPending && deferred.CompletePendingBrowserEntry() && deferred.PreviewSong.Path=="deferred","Browser displays before preparing and selecting songs");
        tabs.Songs.Add(new SongEntry {Path="standard",Title="Shared song",SetId=42,Mode=0,Difficulty="Hard"});
        tabs.Songs.Add(new SongEntry {Path="native-mania",Title="Shared song",SetId=42,Mode=3,Difficulty="4K"});
        tabs.Songs.Add(new SongEntry {Path="native-taiko-only",Title="Taiko exclusive",SetId=43,Mode=1});
        Render(tabs,assets,output,null);tabs.SelectBrowserSong(0);
        Check(tabs.BrowserRows()==tabs.BrowserRows(),"Unchanged browser rows reuse their grouping and text");
        foreach(int x in new int[]{135,165,195,105}){Render(tabs,assets,output,null);tabs.Tap(x,17);Render(tabs,assets,output,null);Check(tabs.PreviewSong.Path=="standard","Standard song stays selected across ruleset tabs");}
        Render(tabs,assets,output,null);tabs.Tap(195,17);Render(tabs,assets,output,null);tabs.SelectBrowserSong(1);
        Render(tabs,assets,output,null);tabs.Tap(135,17);Render(tabs,assets,output,null);Check(tabs.PreviewSong.Path=="standard","Native song finds a compatible difficulty in the same set");
        Render(tabs,assets,output,null);tabs.Tap(195,17);Render(tabs,assets,output,null);Check(tabs.PreviewSong.Path=="native-mania","Returning to original mode restores the exact native difficulty");
        Render(tabs,assets,output,null);tabs.Tap(135,17);Render(tabs,assets,output,null);tabs.SelectBrowserSong(2);string tabLaunch=null;tabs.StartSong=delegate(string path){tabLaunch=path;};
        Render(tabs,assets,output,null);tabs.Tap(105,17);Render(tabs,assets,output,null);tabs.Tap(410,255);Check(tabs.PreviewSong.Path=="native-taiko-only" && tabLaunch==null,"Unsupported mode preserves song preview and cannot launch the wrong ruleset");
        tabs.MergeSongs(new List<SongEntry> {new SongEntry {Path="new-song",Title="New import",Mode=0,SetId=44}});Render(tabs,assets,output,null);tabs.Tap(410,255);Check(tabs.PreviewSong.Path=="native-taiko-only" && tabLaunch==null,"Incremental imports cannot replace an unavailable song selection");
        Render(tabs,assets,output,null);tabs.Tap(135,17);Render(tabs,assets,output,null);Check(tabs.PreviewSong.Path=="native-taiko-only","Switching back restores a mode-exclusive song");
        Render(tabs,assets,output,null);tabs.SelectBrowserSong(0);Render(tabs,assets,output,null);tabs.Tap(195,17);Render(tabs,assets,output,null);Check(tabs.PreviewSong.Path=="standard","Explicitly selecting another song resets the tab selection anchor");
        var loader=new LazerUi();loader.Songs.Add(new SongEntry {Path="loading",Title="Loading test",Artist="Artist",Difficulty="Hard",Mode=0});loader.OpenSongs();
        int loaderLaunches=0,loaderCancels=0;loader.StartSong=delegate {loaderLaunches++;};loader.CancelSongLoad=delegate {loaderCancels++;};
        loader.SongPreparing=true;Check(!loader.LoadingFrameDrawn,"Loader starts before its first presentation");
        Render(loader,assets,output,"player-loader");Check(loader.LoadingFrameDrawn,"Dedicated loader acknowledges its painted frame");
        loader.LoadingCreator="Test mapper";loader.LoadingSource="Test source";
        var loadingPreview=new SvgUiSurface(assets);loader.Draw(loadingPreview,1.5);loadingPreview.Save(Path.Combine(output,"player-loader.svg"));
        string loadingSvg=File.ReadAllText(Path.Combine(output,"player-loader.svg"));
        Check(loadingSvg.Contains("VISUAL SETTINGS") && loadingSvg.Contains("AUDIO SETTINGS") && loadingSvg.Contains("INPUT SETTINGS") && loadingSvg.Contains("Test mapper"),"Loader uses source metadata stack and three settings groups");
        Check(!loadingSvg.Contains("Preparing to play") && !loadingSvg.Contains("Preparing beatmap"),"Generic loading heading and progress bar are removed");
        Check(!loader.LoadingReadyForGameplay(1.7) && !loader.LoadingReadyForGameplay(1.8) && !loader.LoadingReadyForGameplay(2.0) && loader.LoadingReadyForGameplay(2.11),"Loader preserves upstream minimum entry and 300ms exit transitions");
        loader.MenuPointer(0,410,43);loader.MenuPointer(1,460,43);loader.MenuPointer(2,460,43);Check(loader.Dim>.7,"Loader background dim slider updates its actual gameplay setting");
        loader.Tap(410,255);loader.Tap(105,17);Check(loaderLaunches==0 && loader.SelectedRuleset==0,"Loader hides browser Play and mode-tab hit targets");
        Render(loader,assets,output,null);loader.Tap(50,285);Check(loaderCancels==1 && !loader.SongPreparing && loader.Screen==UiScreen.Songs,"Loading cancellation restores browser");
        var notices=new LazerUi {Screen=UiScreen.Play};int alertSounds=0;notices.NotificationAlert=delegate {alertSounds++;};
        notices.Notifications.Post("example","Beatmap import","One set imported successfully.",NotificationKind.Success,false,null);
        var noticeSurface=new SvgUiSurface(assets);notices.Draw(noticeSurface,0);notices.DrawNotificationOverlay(noticeSurface,0,false);
        Check(noticeSurface.NotificationX==284 && noticeSurface.NotificationY==42 && noticeSurface.NotificationWidth==188 && noticeSurface.NotificationHeight==73,"Toast excludes covered native song rows from touch dispatch");
        notices.DrawNotificationOverlay(noticeSurface,.1,false);Check(alertSounds==1,"Notification alert sounds only when a toast first appears");
        Check(noticeSurface.ImageRequests.Contains("toolbar-notification.png"),"Bell uses upstream OsuIcon.Notification resource");
        Check(!notices.NotificationPointer(0,430,17),"Clock region is separate from bell hit target");
        Check(notices.NotificationPointer(0,464,17) && notices.NotificationsOpen,"Bell opens notification overlay after the clock");
        notices.DrawNotificationOverlay(new SvgUiSurface(assets),.2,false);notices.NotificationPointer(2,464,17);
        Check(notices.NotificationsOpen && notices.Notifications.UnreadCount==0,"Bell release does not close drawer; opening marks alerts read");
        var panelPreview=new SvgUiSurface(assets);notices.Draw(panelPreview,1);notices.DrawNotificationOverlay(panelPreview,1,false);panelPreview.Save(Path.Combine(output,"notifications.svg"));
        Check(panelPreview.NotificationWidth==480 && panelPreview.NotificationHeight==320,"Drawer blocks native scroll input through its dimmed backdrop");
        notices.NotificationPointer(0,450,50);notices.NotificationPointer(2,450,50);Check(!notices.NotificationsOpen,"Notification close button dismisses only overlay");
        notices.Notifications.Post("critical-test","Critical","Test alert",NotificationKind.Error,true,null);notices.DrawNotificationOverlay(new SvgUiSurface(assets),2,true);Check(alertSounds==2,"Critical toast can render over gameplay");
        var breakMap=new Beatmap();breakMap.Circles.Add(new Circle {X=256,Y=192,Time=1000});breakMap.Circles.Add(new Circle {X=256,Y=192,Time=20000});breakMap.Breaks.Add(new double[]{3000,15000});var breakSession=new Session(breakMap);breakSession.Tap(256,192,1000);
        var breakSurface=new SvgUiSurface(assets);var breakView=new GameplayView();breakView.Draw(breakSurface,breakSession,5000,480,320);breakView.DrawBreak(breakSession,5000,480,320);breakSurface.Save(Path.Combine(output,"gameplay-break.svg"));Check(File.ReadAllText(Path.Combine(output,"gameplay-break.svg")).Contains("CURRENT PROGRESS"),"Gameplay break draws countdown and live progress panel");
        var hudMap=new Beatmap();hudMap.Circles.Add(new Circle {X=256,Y=192,Time=5000});var hudSession=new Session(hudMap);
        var fullHud=new GameplayView {CursorX=321,CursorY=151,ShowCursor=true,ReducedEffects=false,ProfileIcon=ui.ProfileIcon};var fullSurface=new SvgUiSurface(assets);fullHud.Draw(fullSurface,hudSession,0,480,320);fullSurface.Save(Path.Combine(output,"gameplay-argon-health.svg"));Check(fullSurface.ImageRequests.Contains(ui.ProfileIcon),"Full-effects HUD inherits toolbar profile icon");
        var lowHud=new GameplayView {CursorX=321,CursorY=151,CursorVisible=true,ShowCursor=true,ReducedEffects=true};var lowSurface=new SvgUiSurface(assets);lowHud.Draw(lowSurface,hudSession,0,480,320);lowSurface.Save(Path.Combine(output,"gameplay-reduced-cursor.svg"));Check(File.ReadAllText(Path.Combine(output,"gameplay-reduced-cursor.svg")).Contains("cx='321' cy='151'"),"Cursor can render with reduced effects enabled");Check(!lowSurface.ImageRequests.Contains("avatar-guest.png"),"Reduced HUD retains lightweight bar without avatar texture");
        lowHud.ShowCursor=false;var noCursor=new SvgUiSurface(assets);lowHud.Draw(noCursor,hudSession,0,480,320);noCursor.Save(Path.Combine(output,"gameplay-reduced-no-cursor.svg"));Check(!File.ReadAllText(Path.Combine(output,"gameplay-reduced-no-cursor.svg")).Contains("cx='321' cy='151'"),"Independent cursor setting disables it in reduced mode");
        var cursorSettings=new LazerUi {Screen=UiScreen.Play,SettingsPath=Path.Combine(output,"cursor-settings.cfg")};Render(cursorSettings,assets,output,null);cursorSettings.Tap(20,17);Render(cursorSettings,assets,output,null);cursorSettings.Tap(25,196);Render(cursorSettings,assets,output,null);cursorSettings.Tap(280,195);Render(cursorSettings,assets,output,null);cursorSettings.Tap(280,245);Render(cursorSettings,assets,output,null);cursorSettings.Tap(280,245);Check(cursorSettings.ReducedGameplayEffects && cursorSettings.GameplayCursor,"Settings allow cursor toggle while reduced effects remains enabled");
        var savedCursorSettings=new LazerUi {SettingsPath=cursorSettings.SettingsPath};savedCursorSettings.LoadSettings();Check(savedCursorSettings.ReducedGameplayEffects && savedCursorSettings.GameplayCursor,"Reduced effects and cursor preference persist independently");
        var editor=new LazerUi {Animate=false,HudSkinPath=Path.Combine(output,"saved-hud.tsv"),Screen=UiScreen.Edit};bool editorActive=false;editor.HudEditorActiveChanged=delegate(bool active){editorActive=active;};Render(editor,assets,output,null);editor.Tap(420,160);Check(editor.Screen==UiScreen.SkinEditor && editorActive,"Edit menu opens working HUD editor");
        Render(editor,assets,output,null);editor.MenuPointer(0,300,40);editor.MenuPointer(1,240,85);editor.MenuPointer(2,240,85);
        var movedScore=editor.HudDraft.Layouts[0].Items.Find(delegate(HudItem item){return item.Kind=="score";});Check(movedScore.X==-80 && movedScore.Y==60 && editor.HudEditorDirty,"Canvas drag moves actual HUD score with grid snap");
        Render(editor,assets,output,"skin-editor");Check(editor.SaveHudEditor(false),"HUD editor saves layout");Check(editor.SkinLayout.Layouts[0].Find(movedScore.Id).X==-80 && HudSkin.Load(editor.HudSkinPath).Layouts[0].Find(movedScore.Id).Y==60,"Saved draft applies to runtime and persists");
        Render(editor,assets,output,null);editor.MenuPointer(0,210,14);Check(editor.HudDraft.Layouts[0].Find(movedScore.Id).X==0,"Editor Undo restores prior layout");Render(editor,assets,output,null);editor.MenuPointer(0,250,14);Check(editor.HudDraft.Layouts[0].Find(movedScore.Id).X==-80,"Editor Redo reapplies layout");
        Render(editor,assets,output,null);editor.MenuPointer(0,440,14);Check(editor.Screen==UiScreen.Edit && !editorActive,"Closing saved editor returns to prior screen");editor.OpenHudEditor(1);int taikoItems=editor.HudDraft.Layouts[1].Items.Count;editor.AddHudComponent("score");Check(editor.HudDraft.Layouts[1].Items.Count==taikoItems+1 && editor.HudDraft.Layouts[0].Items.Count==11,"Component addition is scoped to selected ruleset");Render(editor,assets,output,null);editor.MenuPointer(0,440,14);Render(editor,assets,output,null);editor.MenuPointer(0,200,195);Check(editor.Screen==UiScreen.Edit && editor.SkinLayout.Layouts[1].Items.Count==taikoItems,"Discard leaves saved layout unchanged");
        var baseView=new GameplayView();var baseSvg=new SvgUiSurface(assets);baseView.Draw(baseSvg,hudSession,0,480,320);baseSvg.Save(Path.Combine(output,"hud-unmodified.svg"));var defaultView=new GameplayView {Layout=new HudLayout(0,true)};var defaultSvg=new SvgUiSurface(assets);defaultView.Draw(defaultSvg,hudSession,0,480,320);defaultSvg.Save(Path.Combine(output,"hud-default-layout.svg"));Check(File.ReadAllText(Path.Combine(output,"hud-unmodified.svg"))==File.ReadAllText(Path.Combine(output,"hud-default-layout.svg")),"Default layout leaves rendered gameplay HUD unchanged");
        var customView=new GameplayView {Layout=editor.SkinLayout.Layouts[0]};var customSvg=new SvgUiSurface(assets);customView.Draw(customSvg,hudSession,0,480,320);customSvg.Save(Path.Combine(output,"hud-edited-layout.svg"));Check(File.ReadAllText(Path.Combine(output,"hud-edited-layout.svg")).Contains("translate(284 65)"),"Saved position transforms live gameplay score, not only editor preview");
        editor.OpenHudEditor(0);Render(editor,assets,output,null);editor.MenuPointer(0,240,83);editor.MenuPointer(2,240,83);Render(editor,assets,output,null);editor.MenuPointer(0,455,121);Render(editor,assets,output,null);editor.MenuPointer(0,455,145);Render(editor,assets,output,null);editor.MenuPointer(0,430,170);
        var transformedScore=editor.HudDraft.Layouts[0].Find(movedScore.Id);Check(Math.Abs(transformedScore.Scale-1.1)<.001 && transformedScore.Angle==15 && Math.Abs(transformedScore.Opacity-.9)<.001,"Editor properties change component scale, rotation and opacity");
        Render(editor,assets,output,null);editor.MenuPointer(0,390,245);var duplicate=editor.HudDraft.Layouts[0].Items[editor.HudDraft.Layouts[0].Items.Count-1];Check(duplicate.Kind=="score" && duplicate.Id!=transformedScore.Id && duplicate.Angle==15,"Duplicate preserves component settings with unique identity");Render(editor,assets,output,null);editor.MenuPointer(0,400,195);Check(!duplicate.Visible,"Editor hide changes live component visibility");Render(editor,assets,output,null);editor.MenuPointer(0,445,245);Check(editor.HudDraft.Layouts[0].Find(duplicate.Id)==null,"Editor delete removes selected duplicate");
        Render(editor,assets,output,null);editor.MenuPointer(0,240,90);editor.MenuPointer(2,240,90);Render(editor,assets,output,null);var resizeBounds=editor.HudDraft.Layouts[0].DisplayBounds(transformedScore,false);double handleX=(resizeBounds.X+resizeBounds.W)*.75,handleY=28+(resizeBounds.Y+resizeBounds.H)*.75,previousScale=transformedScore.Scale;editor.MenuPointer(0,handleX,handleY);editor.MenuPointer(1,handleX+12,handleY+6);editor.MenuPointer(2,handleX+12,handleY+6);Check(transformedScore.Scale>previousScale && transformedScore.X==-80,"Corner handle resizes without translating selected component");
        Render(editor,assets,output,null);editor.MenuPointer(0,400,280);Render(editor,assets,output,"skin-editor-components");Check(editor.Screen==UiScreen.SkinEditor,"Component toolbox opens without leaving editor");
        Console.WriteLine("PASS: " + assertions + " UI navigation, selection and persistence checks");
    }
}
