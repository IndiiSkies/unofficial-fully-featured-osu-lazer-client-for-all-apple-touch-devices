#import <UIKit/UIKit.h>
#import <AVFoundation/AVFoundation.h>
#import <MediaPlayer/MediaPlayer.h>
#import <QuartzCore/QuartzCore.h>
#import "GpuRenderer.h"
#include "StartupGate.h"
extern void legacy_set_animation(int);
extern void legacy_menu_analysis_source(const char *);

// IntroTriangles is baked from its source timeline and progress-mask shader
// into a three-second Baseline H.264 clip for the ARMv6 hardware decoder.
// The full theme includes the voice and continues across the menu transition.
@interface LegacyStartup : NSObject <AVAudioPlayerDelegate> {
    UIView *overlay;
    UIView *bootCover;
    MPMoviePlayerController *movie;
    AVAudioPlayer *music;
    NSString *previewPath;
    BOOL previewMode;
    NSTimer *timer;
    BOOL revealPending;
    LegacyStartupGate gate;
    BOOL prepared;
    int revealFrames;
    BOOL userPaused;
    int ended;
    double deadline,remaining;
    float volume;
    BOOL started,active,background,wantsMusic,musicStarted,resumeMusicAfterPause;
}
- (void)start;
- (void)drawBootCover;
- (void)frameTick;
- (void)prepare;
- (void)tryPlayback;
- (void)loadMusic;
- (void)finish:(NSTimer *)unused;
- (void)skip;
- (void)removeOverlay;
- (void)revealTick:(CADisplayLink *)link;
- (void)setVolume:(double)value;
- (void)suspendMusic;
- (void)resumeMusic;
- (void)pause;
- (void)resume;
- (BOOL)active;
- (BOOL)audioTime:(double *)time gain:(double *)gain;
- (int)selectTrack:(NSString *)path;
- (void)userPause:(int)value;
- (void)seek:(double)time;
- (double)position;
- (double)duration;
- (int)takeEnded;
- (int)previewPath:(NSString *)path milliseconds:(double)milliseconds;
- (void)endPreview;
@end
static LegacyStartup *startup;
static LegacyStartup *get_startup(void) {
    if(!startup)startup=[[LegacyStartup alloc] init];return startup;
}
@implementation LegacyStartup
- (id)init { if((self=[super init])){volume=.8f;wantsMusic=YES;}return self; }
- (void)armTimer:(double)delay {
    deadline=CACurrentMediaTime()+delay;
    timer=[[NSTimer scheduledTimerWithTimeInterval:MAX(.001,delay) target:self selector:@selector(finish:) userInfo:nil repeats:NO] retain];
}
- (void)playbackChanged:(NSNotification *)notification {
    if(active && gate.requested && !background && movie.playbackState==MPMoviePlaybackStatePlaying && !musicStarted) {
        musicStarted=YES;
        // Video preparation can be asynchronous. Start the combined voice and
        // theme only when the decoder actually starts the silent movie.
        music.currentTime=MAX(0,movie.currentPlaybackTime);
        NSLog(@"OSU startup: triangles video playing; theme=%d",[music play]);
        // Keep the native preparation frame above both the pre-rendered menu
        // and the movie until playback has actually begun.
        [bootCover performSelector:@selector(removeFromSuperview) withObject:nil afterDelay:.07];
    }
}
- (void)movieFinished:(NSNotification *)notification {
    NSLog(@"OSU startup: video ended (%@)",[notification userInfo]);
    [self finish:nil];
}
- (void)start {
    if(started)return;started=active=YES;
    background=[UIApplication sharedApplication].applicationState!=UIApplicationStateActive;
    gate.foreground=!background;
    // Install a synchronous cover before managed callback registration can
    // draw the menu. This closes the one-run-loop gap before prepare creates
    // and presents the movie surface.
    UIWindow *window=[UIApplication sharedApplication].keyWindow;
    overlay=[[UIView alloc] initWithFrame:CGRectMake(0,0,480,320)];overlay.bounds=CGRectMake(0,0,480,320);
    overlay.center=CGPointMake(window.bounds.size.width/2,window.bounds.size.height/2);
    if(window.bounds.size.height>window.bounds.size.width)overlay.transform=CGAffineTransformMakeRotation(M_PI_2);
    overlay.backgroundColor=[UIColor blackColor];overlay.opaque=YES;overlay.userInteractionEnabled=NO;overlay.clipsToBounds=YES;
    bootCover=[legacy_gpu_create_surface(overlay) retain];bootCover.backgroundColor=[UIColor colorWithRed:.035 green:.045 blue:.075 alpha:1];[window addSubview:overlay];
    if(legacy_gpu_active())[self performSelector:@selector(drawBootCover) withObject:nil afterDelay:0];else [self drawBootCover];
    // Main() must return to the native host before UIKit can replace its boot
    // frame. No media setup or playback happens inside the managed call.
    [self performSelector:@selector(prepare) withObject:nil afterDelay:0];
}
- (void)drawBootCover {
    if(!bootCover || !legacy_gpu_begin_surface(bootCover))return;
    legacy_gpu_rect(0,0,480,320,.035,.045,.075,1,0);
    legacy_gpu_text(34,128,"Starting osu!",24,.95,.95,1);
    legacy_gpu_text(34,172,"Preparing the game. Please wait...",13,.95,.95,1);legacy_gpu_end();
}
- (void)loadMusic {
    if(music)return;
    NSString *bundle=[[NSBundle mainBundle] bundlePath];NSError *error=nil;
    music=[[AVAudioPlayer alloc] initWithContentsOfURL:[NSURL fileURLWithPath:[bundle stringByAppendingPathComponent:@"Startup/menu.m4a"]] error:&error];
    if(!music)NSLog(@"OSU startup: triangles theme failed: %@",error);
    music.volume=volume;music.numberOfLoops=0;music.delegate=self;[music prepareToPlay];
    legacy_menu_analysis_source([[[bundle stringByAppendingPathComponent:@"Startup/menu.m4a"] stringByStandardizingPath] fileSystemRepresentation]);
}
- (void)loadChanged:(NSNotification *)notification { [self tryPlayback]; }
- (void)tryPlayback {
    if(!active || !prepared)return;
    gate.playable=(movie.loadState & (MPMovieLoadStatePlayable|MPMovieLoadStatePlaythroughOK))!=0;
    if(!legacy_startup_gate_take(&gate))return;
    movie.currentPlaybackTime=0;music.currentTime=0;
    NSLog(@"OSU startup: visible surface and decoder ready; starting from zero");
    [movie play];
}
- (void)presentationTick:(CADisplayLink *)link {
    if(!active || background || !overlay.window || overlay.window.hidden)return;
    if(++gate.presented_frames>=2) {
        NSLog(@"OSU startup: intro surface presented");
        [self tryPlayback];
    }
}
- (void)frameTick {
    if(background)return;
    if(active && prepared && gate.presented_frames<2)[self presentationTick:nil];
    else if(revealPending)[self revealTick:nil];
}
- (void)prepare {
    if(!active || prepared || background)return;
    prepared=YES;
    [self loadMusic];
    NSString *bundle=[[NSBundle mainBundle] bundlePath];
    movie=[[MPMoviePlayerController alloc] initWithContentURL:[NSURL fileURLWithPath:[bundle stringByAppendingPathComponent:@"Startup/triangles-intro.mp4"]]];
    movie.controlStyle=MPMovieControlStyleNone;movie.scalingMode=MPMovieScalingModeAspectFit;
    movie.shouldAutoplay=NO;movie.useApplicationAudioSession=YES;
    movie.view.frame=overlay.bounds;movie.view.userInteractionEnabled=NO;[overlay insertSubview:movie.view belowSubview:bootCover];
    [[NSNotificationCenter defaultCenter] addObserver:self selector:@selector(playbackChanged:) name:MPMoviePlayerPlaybackStateDidChangeNotification object:movie];
    [[NSNotificationCenter defaultCenter] addObserver:self selector:@selector(movieFinished:) name:MPMoviePlayerPlaybackDidFinishNotification object:movie];
    [[NSNotificationCenter defaultCenter] addObserver:self selector:@selector(loadChanged:) name:MPMoviePlayerLoadStateDidChangeNotification object:movie];
    [movie prepareToPlay]; // Decoder may prepare, but playback remains gated.
    [self armTimer:8]; // Same escape hatch as lazer if a media decoder stalls.
    NSLog(@"OSU startup: IntroTriangles requested, 480x320 / 30 fps");
}
- (void)finish:(NSTimer *)unused {
    if(!active)return;active=NO;gate.cancelled=1;
    [self loadMusic];
    [timer invalidate];[timer release];timer=nil;
    [[NSNotificationCenter defaultCenter] removeObserver:self name:nil object:movie];
    [movie stop];[movie.view removeFromSuperview];
    // A skip or decoder failure advances past the spoken intro. Natural
    // completion preserves the uninterrupted theme's current position.
    if(music.currentTime<3)music.currentTime=3;
    musicStarted=YES;
    legacy_set_animation(1);[self resumeMusic];
    // Keep an opaque final flash above the window until the managed menu has
    // had two display passes. Fading immediately can expose the old black
    // boot frame on slower devices before Draw() reaches the screen.
    overlay.backgroundColor=[UIColor whiteColor];
    revealFrames=0;
    revealPending=YES;
    NSLog(@"OSU startup: waiting for menu frame");
    // A busy run loop can miss display-link callbacks on iOS 4. The menu
    // was already primed behind the intro; never retain its white cover
    // indefinitely if presentation callbacks stall.
    [self performSelector:@selector(revealFallback) withObject:nil afterDelay:.25];
}
- (void)revealFallback {
    if(!revealPending || active || !overlay)return;
    revealFrames=2;[self revealTick:nil];
}
- (void)revealTick:(CADisplayLink *)link {
    if(++revealFrames<2)return;
    revealPending=NO;
    // IntroTriangles ends with GameWideFlash (1000 ms, Easing.Out).
    [UIView beginAnimations:@"triangles-menu-flash" context:NULL];[UIView setAnimationDuration:1];[UIView setAnimationCurve:UIViewAnimationCurveEaseOut];overlay.alpha=0;[UIView commitAnimations];
    [self performSelector:@selector(removeOverlay) withObject:nil afterDelay:1.01];
    NSLog(@"OSU startup: triangles menu revealed");
}
- (void)removeOverlay { [overlay removeFromSuperview];[bootCover release];bootCover=nil;[movie release];movie=nil;[overlay release];overlay=nil; }
- (void)skip { [self finish:nil]; }
- (BOOL)active {return active;}
- (BOOL)audioTime:(double *)time gain:(double *)gain {
    if(!music || active || background || !wantsMusic || userPaused || !music.playing)return NO;
    *time=music.currentTime;*gain=music.volume;return YES;
}
- (int)selectTrack:(NSString *)path {
    double prepareStart=CACurrentMediaTime();
    NSError *error=nil;AVAudioPlayer *next=[[AVAudioPlayer alloc] initWithContentsOfURL:[NSURL fileURLWithPath:path] error:&error];
    if(!next){NSLog(@"OSU music: cannot open %@: %@",[path lastPathComponent],error);return 0;}
    next.volume=volume;next.numberOfLoops=0;next.delegate=self;[next prepareToPlay];
    double prepareMs=(CACurrentMediaTime()-prepareStart)*1000;if(prepareMs>100)NSLog(@"OSU stall: music prepare %@ %.1f ms",[path lastPathComponent],prepareMs);
    [music stop];[music release];music=next;userPaused=NO;ended=0;wantsMusic=YES;
    [previewPath release];previewPath=[path copy];music.numberOfLoops=previewMode?-1:0;
    legacy_menu_analysis_source([path fileSystemRepresentation]);
    if(!active && !background && ![music play])return 0;
    NSLog(@"OSU music: playing %@",[path lastPathComponent]);return 1;
}
- (void)audioPlayerDidFinishPlaying:(AVAudioPlayer *)audio successfully:(BOOL)success {if(audio==music)ended=1;}
- (int)previewPath:(NSString *)path milliseconds:(double)milliseconds {
    previewMode=YES;
    if(!path)return 0;
    if(![path isEqualToString:previewPath]) {
        if(![self selectTrack:path])return 0;
        music.currentTime=MAX(0,MIN(MAX(0,music.duration-.05),milliseconds>=0?milliseconds/1000:0));
        NSLog(@"OSU shared music: selected %@ at %.2f sec",[path lastPathComponent],music.currentTime);
    }
    music.numberOfLoops=-1;
    if(!background && wantsMusic && !userPaused && !music.playing)[music play];
    return music?1:0;
}
- (void)endPreview {
    if(!previewMode)return;previewMode=NO;
    music.numberOfLoops=0;[self resumeMusic];
}
- (void)userPause:(int)value {userPaused=value!=0;if(userPaused)[music pause];else if(!background && !active && !previewMode && wantsMusic){ended=0;[music play];}}
- (void)seek:(double)time {music.currentTime=MAX(0,MIN(MAX(0,music.duration-.05),time));}
- (double)position {return music.currentTime;}
- (double)duration {return music.duration;}
- (int)takeEnded {int value=ended;ended=0;return value;}
- (void)setVolume:(double)value {volume=(float)MAX(0,MIN(1,value));music.volume=volume;}
- (void)suspendMusic {wantsMusic=NO;[music pause];}
- (void)resumeMusic {
    wantsMusic=YES;
    if(started && !active && !background && !userPaused && !ended && !music.playing)NSLog(@"OSU startup: menu music play=%d",[music play]);
}
- (void)pause {
    if(background)return;background=YES;gate.foreground=0;
    resumeMusicAfterPause=music.playing;[music pause];
    if(active && prepared){remaining=MAX(0,deadline-CACurrentMediaTime());[timer invalidate];[timer release];timer=nil;[movie pause];}
}
- (void)resume {
    if(!background)return;background=NO;gate.foreground=1;
    if(active){
        if(!prepared){[self prepare];return;}
        if(gate.requested){if(resumeMusicAfterPause)[music play];[movie play];}
        else [self tryPlayback];
        [self armTimer:remaining];
    }
    else if(wantsMusic)[self resumeMusic];
}
@end
void legacy_intro_start(void) {[get_startup() start];}
void legacy_startup_tick(void) {[startup frameTick];}
int legacy_intro_active(void) {return startup && [startup active];}
void legacy_intro_skip(void) {[startup skip];}
void legacy_startup_volume(double volume) {[get_startup() setVolume:volume];}
void legacy_menu_suspend(void) {[startup suspendMusic];}
void legacy_menu_resume(void) {[startup resumeMusic];}
void legacy_startup_pause(void) {[startup pause];}
void legacy_startup_resume(void) {[startup resume];}

int legacy_menu_audio_state(double *time,double *gain) {return startup && [startup audioTime:time gain:gain];}

int legacy_menu_music_play(const char *path) {return [get_startup() selectTrack:[NSString stringWithUTF8String:path]];}
void legacy_menu_music_pause(int value) {[startup userPause:value];}
void legacy_menu_music_seek(double time) {[startup seek:time];}
double legacy_menu_music_position(void) {return [startup position];}
double legacy_menu_music_duration(void) {return [startup duration];}
int legacy_menu_music_ended(void) {return [startup takeEnded];}

int legacy_song_preview(const char *path,double milliseconds){return [get_startup() previewPath:path?[NSString stringWithUTF8String:path]:nil milliseconds:milliseconds];}
void legacy_song_preview_end(void){[startup endPreview];}
