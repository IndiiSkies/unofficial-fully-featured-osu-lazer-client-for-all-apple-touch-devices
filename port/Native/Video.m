#import <UIKit/UIKit.h>
#import <AVFoundation/AVFoundation.h>
#import <QuartzCore/QuartzCore.h>
#include <math.h>
#include "VideoTimeline.h"
extern UIView *legacy_background_view(void);
extern int legacy_menu_audio_state(double *,double *);
extern double legacy_menu_music_position(void);
extern void legacy_request_cached_frame(void);
extern void legacy_notify_warning(const char *,const char *);

// AVPlayerLayer lets the system video decoder feed the GPU compositor directly.
// Keep its surface below the dim layer and GLES canvas; the song image remains
// visible until a decoded frame is ready, and if playback fails or finishes.
@interface LegacyBeatmapVideo : NSObject {
@public NSString *source;double offset;BOOL preview,paused;
    AVPlayer *player;AVPlayerLayer *layer;UIView *host;NSTimer *timer;
    LegacyVideoClock audioClock;double lastSeek,durationSeconds;BOOL started,failed,loggedFrame,background;
}
- (id)initWithPath:(NSString *)path offset:(double)value preview:(BOOL)isPreview;
- (void)tick:(NSTimer *)unused;
- (void)sync:(double)milliseconds;
- (void)restart;
- (void)setPaused:(BOOL)value;
- (void)stop;
- (void)setVisible:(BOOL)visible;
@end
@implementation LegacyBeatmapVideo
- (id)initWithPath:(NSString *)path offset:(double)value preview:(BOOL)isPreview {
    double setupStart=CACurrentMediaTime();
    if(!(self=[super init]))return nil;source=[path copy];offset=value;preview=isPreview;lastSeek=-100;
    NSString *prepared=[path stringByAppendingString:@".ipod.mp4"];
    if(![[NSFileManager defaultManager] fileExistsAtPath:prepared])prepared=path;
    AVURLAsset *asset=[AVURLAsset URLAssetWithURL:[NSURL fileURLWithPath:prepared] options:nil];
    NSArray *tracks=[asset tracksWithMediaType:AVMediaTypeVideo];
    if(![tracks count]){NSLog(@"OSU video: no readable video track in %@",[prepared lastPathComponent]);legacy_notify_warning("Video unavailable","No readable video track was found. Using the beatmap background.");failed=YES;return self;}
    // Strip the movie's audio, so the shared song player is always the only
    // music source. A video-only composition also works on the iOS 4 API.
    AVAssetTrack *track=[tracks objectAtIndex:0];AVMutableComposition *composition=[AVMutableComposition composition];
    durationSeconds=CMTimeGetSeconds(track.timeRange.duration);
    AVMutableCompositionTrack *video=[composition addMutableTrackWithMediaType:AVMediaTypeVideo preferredTrackID:kCMPersistentTrackID_Invalid];
    NSError *error=nil;
    if(![video insertTimeRange:track.timeRange ofTrack:track atTime:kCMTimeZero error:&error]){NSLog(@"OSU video: cannot prepare %@: %@",[prepared lastPathComponent],error);legacy_notify_warning("Video unavailable",[[error localizedDescription] UTF8String]);failed=YES;return self;}
    video.preferredTransform=track.preferredTransform;
    player=[[AVPlayer alloc] initWithPlayerItem:[AVPlayerItem playerItemWithAsset:composition]];
    player.actionAtItemEnd=AVPlayerActionAtItemEndPause;
    host=[[UIView alloc] initWithFrame:CGRectMake(0,0,480,320)];host.backgroundColor=[UIColor clearColor];host.opaque=NO;host.clipsToBounds=YES;host.userInteractionEnabled=NO;host.hidden=YES;
    UIView *image=legacy_background_view(),*parent=image.superview;
    host.center=CGPointMake(parent.bounds.size.width/2,parent.bounds.size.height/2);
    if(parent.bounds.size.height>parent.bounds.size.width)host.transform=CGAffineTransformMakeRotation(M_PI_2);
    layer=[[AVPlayerLayer playerLayerWithPlayer:player] retain];layer.frame=host.bounds;layer.videoGravity=AVLayerVideoGravityResizeAspectFill;[host.layer addSublayer:layer];
    [parent insertSubview:host aboveSubview:image];
    [[NSNotificationCenter defaultCenter] addObserver:self selector:@selector(finished:) name:AVPlayerItemDidPlayToEndTimeNotification object:player.currentItem];
    [[NSNotificationCenter defaultCenter] addObserver:self selector:@selector(resign:) name:UIApplicationWillResignActiveNotification object:nil];
    [[NSNotificationCenter defaultCenter] addObserver:self selector:@selector(activate:) name:UIApplicationDidBecomeActiveNotification object:nil];
    // The timer retains its target; stop explicitly invalidates it before the
    // controller is released. Default run-loop mode yields to song scrolling.
    timer=[NSTimer scheduledTimerWithTimeInterval:.1 target:self selector:@selector(tick:) userInfo:nil repeats:YES];
    NSLog(@"OSU video: preparing %@ (%@), native decoder + GPU layer, setup %.1f ms",[prepared lastPathComponent],preview?@"browser":@"gameplay",(CACurrentMediaTime()-setupStart)*1000);return self;
}
- (void)setVisible:(BOOL)visible {if(host.hidden==visible){host.hidden=!visible;legacy_request_cached_frame();}}
- (void)finished:(NSNotification *)notification {[player pause];[self setVisible:NO];started=NO;}
- (void)resign:(NSNotification *)notification {background=YES;[player pause];}
- (void)activate:(NSNotification *)notification {background=NO;[self tick:nil];}
- (void)setPaused:(BOOL)value {paused=value;if(value)[player pause];else [self tick:nil];}
- (void)sync:(double)milliseconds {if(legacy_video_clock_update(&audioClock,milliseconds,offset,durationSeconds))[self tick:nil];}
- (void)restart {started=NO;lastSeek=-100;audioClock.received=0;audioClock.milliseconds=0;[self setVisible:NO];[player pause];}
- (void)tick:(NSTimer *)unused {
    if(failed || background || !player)return;
    if(player.status==AVPlayerStatusFailed || player.currentItem.status==AVPlayerItemStatusFailed){failed=YES;[self setVisible:NO];[player pause];NSError *error=player.currentItem.error?player.currentItem.error:player.error;NSLog(@"OSU video: playback failed %@: %@",[source lastPathComponent],error);legacy_notify_warning("Video playback failed",error?[[error localizedDescription] UTF8String]:"Using the beatmap background.");return;}
    double time=audioClock.milliseconds/1000;BOOL running=!paused;
    if(preview){double gain;running=legacy_menu_audio_state(&time,&gain)!=0;if(!running)time=legacy_menu_music_position();}
    double expected;
    if(!legacy_video_target(time,offset,durationSeconds,&expected)){[self setVisible:NO];[player pause];return;}
    if(player.currentItem.status!=AVPlayerItemStatusReadyToPlay)return;
    double now=CACurrentMediaTime(),position=CMTimeGetSeconds(player.currentTime);
    if(legacy_video_seek_needed(started,now,lastSeek,position,expected)) {
        [player seekToTime:CMTimeMakeWithSeconds(expected,600) toleranceBefore:CMTimeMakeWithSeconds(.05,600) toleranceAfter:CMTimeMakeWithSeconds(.05,600)];lastSeek=now;started=YES;
    }
    if(running){extern double legacy_gameplay_rate(void);float rate=preview?1:legacy_gameplay_rate();if(player.rate!=rate)player.rate=rate;}else [player pause];
    [self setVisible:layer.readyForDisplay];
    if(!host.hidden && !loggedFrame){loggedFrame=YES;NSLog(@"OSU video: first decoded frame visible %@",[source lastPathComponent]);}
}
- (void)stop {[self setVisible:NO];[timer invalidate];timer=nil;[[NSNotificationCenter defaultCenter] removeObserver:self];[player pause];[host removeFromSuperview];layer.player=nil;}
- (void)dealloc {[source release];[layer release];[host release];[player release];[super dealloc];}
@end
static LegacyBeatmapVideo *video;
int legacy_video_visible(void){return video && video->host && !video->host.hidden;}
int legacy_video_ready(void){
    if(!video || video->failed || !video->player)return 1;
    if(video->player.status==AVPlayerStatusFailed || video->player.currentItem.status==AVPlayerItemStatusFailed)return 1;
    return video->player.currentItem.status==AVPlayerItemStatusReadyToPlay && (video->offset>0 || video->layer.readyForDisplay);
}
void legacy_video_stop(void){[video stop];[video release];video=nil;}
int legacy_video_start(const char *path,double offset){legacy_video_stop();if(!path)return 0;video=[[LegacyBeatmapVideo alloc] initWithPath:[NSString stringWithUTF8String:path] offset:offset preview:NO];return video!=nil;}
void legacy_video_preview(const char *path,double offset){if(!path){legacy_video_stop();return;}NSString *source=[NSString stringWithUTF8String:path];if(video && video->preview && [video->source isEqualToString:source] && video->offset==offset)return;legacy_video_stop();video=[[LegacyBeatmapVideo alloc] initWithPath:source offset:offset preview:YES];[video tick:nil];}
void legacy_video_sync(double time){[video sync:time];}
void legacy_video_pause(void){[video setPaused:YES];}
void legacy_video_resume(void){[video setPaused:NO];}
void legacy_video_restart(void){[video setPaused:NO];[video restart];}
