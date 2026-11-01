#import <UIKit/UIKit.h>
#import <AVFoundation/AVFoundation.h>
#import <MediaPlayer/MediaPlayer.h>
#import <QuartzCore/QuartzCore.h>
#import <ImageIO/ImageIO.h>
#include <stdlib.h>
#include <math.h>
#include <stdint.h>
#include <string.h>
#include <dlfcn.h>
#include "ImportGate.h"
#include "AudioLeadIn.h"
#import "GpuRenderer.h"

extern void mono_set_dirs(const char *, const char *);
extern void mono_config_parse(const char *);
extern void mono_jit_parse_options(int, char **);
extern void *mono_jit_init_version(const char *, const char *);
extern void *mono_domain_assembly_open(void *, const char *);
extern void *mono_assembly_get_image(void *);
extern void *mono_class_from_name(void *, const char *, const char *);
extern void *mono_class_get_method_from_name(void *, const char *, int);
extern int mono_runtime_run_main(void *, int, char **, void **);
extern void mono_print_unhandled_exception(void *);
static void start_managed_runtime(void);
extern void legacy_gpu_push_hud(double,double,double,double,double);
extern void legacy_gpu_pop_hud(void);
extern void legacy_sound_volume(double);
extern void legacy_sound_stop(void);
extern void legacy_sound_suspend(void);
extern void legacy_sound_resume(void);
extern void legacy_startup_volume(double);
extern void legacy_menu_suspend(void);
extern void legacy_menu_resume(void);
extern void legacy_startup_pause(void);
extern void legacy_startup_resume(void);
extern void legacy_startup_tick(void);
extern void legacy_software_frame_completed(void);
extern void legacy_triangles_attach(UIView *,UIView *);
extern void legacy_menu_triangles_suspend(int);
extern void legacy_menu_effects_attach(UIView *,UIView *);
extern void legacy_menu_effects_suspend(int);
extern void legacy_menu_effects_invalidate(void);
extern CGImageRef legacy_decode_image(const char *, int);
extern CGImageRef legacy_raster_artwork(const char *, int, int);
typedef void (*FrameCallback)(int, int);
typedef int (*PollCallback)(void);
typedef void (*TouchCallback)(int, int, double, double);
static FrameCallback frame_callback;
static TouchCallback touch_callback;
static PollCallback poll_callback;
static int poll_jobs;
static CFTimeInterval next_job_poll;
static CGContextRef drawing_context;
static AVAudioPlayer *player;
static BOOL gameplay_audio_paused;
static BOOL gameplay_audio_pending;
static NSMutableArray *native_notifications;
static NSString *taken_notification;
void legacy_notify_warning(const char *title,const char *message){
    @synchronized([NSMutableArray class]){
        if(!native_notifications)native_notifications=[[NSMutableArray alloc] init];
        NSString *label=title?[NSString stringWithUTF8String:title]:@"Playback warning",*body=message?[NSString stringWithUTF8String:message]:@"";
        if([native_notifications count]>=32)[native_notifications removeObjectAtIndex:0];
        [native_notifications addObject:[NSString stringWithFormat:@"%@\t%@",[label stringByReplacingOccurrencesOfString:@"\t" withString:@" "],body]];
    }
}
const char *legacy_take_notification(void){
    @synchronized([NSMutableArray class]){
        [taken_notification release];taken_notification=nil;
        if(![native_notifications count])return NULL;
        taken_notification=[[native_notifications objectAtIndex:0] retain];[native_notifications removeObjectAtIndex:0];return [taken_notification UTF8String];
    }
}
static LegacyAudioLeadIn gameplay_leadin;
static double audio_ended_at = -1;
static NSString *boot_message = @"Starting osu!";
static NSMutableDictionary *ui_images;
static NSMutableDictionary *artwork_images;
static NSMutableArray *artwork_order;
static UIImageView *background_view;
static UIView *dim_view;
static UIView *canvas_view;
static NSString *background_key;
static NSString *gpu_background_key;
UIView *legacy_canvas_view(void){return canvas_view;}
double legacy_background_dim(void){return dim_view.alpha;}
static int artwork_quality;
static int animate_frames = 1;
static int idle_redraws = 1;
static int canvas_dirty=1,cached_dirty,animate_ui;
void legacy_request_frame(void){canvas_dirty=1;}
void legacy_request_cached_frame(void){cached_dirty=1;}
void legacy_ui_animation(int active){animate_ui=active!=0;}
extern int legacy_menu_effects_tick(int present);
extern int legacy_song_list_tick(void);
static float master_volume = 0.8f;
static float music_volume=1,effects_volume=1;static double gameplay_rate=1,gameplay_gain=1;
extern void legacy_sound_mod_pitch(double);extern void legacy_sound_mod_gain(double);
double legacy_gameplay_rate(void){return gameplay_rate;}
static int pending_import;
static NSString *pending_search;
static UIAlertView *search_alert;
static UITextField *search_field;

void legacy_clear_artwork_cache(void) {
    legacy_gpu_clear_cache();
    [artwork_images removeAllObjects]; [artwork_order removeAllObjects];
    [background_key release]; background_key = nil;
    [gpu_background_key release];gpu_background_key=nil;
}

static BOOL queue_archive(NSURL *url) {
    if (![url isFileURL] || ![[[url pathExtension] lowercaseString] isEqualToString:@"osz"]) return NO;
    NSString *documents = [NSSearchPathForDirectoriesInDomains(NSDocumentDirectory, NSUserDomainMask, YES) objectAtIndex:0];
    NSString *sourcePath = [[url path] stringByStandardizingPath];
    if ([sourcePath hasPrefix:[documents stringByAppendingString:@"/"]]) {
        // Open In normally supplies a file already copied into Documents/Inbox.
        pending_import = 1; legacy_request_frame();return YES;
    }
    NSString *imports = [documents stringByAppendingPathComponent:@"Imports"];
    NSError *error = nil;
    NSFileManager *files = [NSFileManager defaultManager];
    [files createDirectoryAtPath:imports withIntermediateDirectories:YES attributes:nil error:&error];
    NSString *name = [NSString stringWithFormat:@"%@-%@", [[NSProcessInfo processInfo] globallyUniqueString], [[url path] lastPathComponent]];
    if (![files copyItemAtPath:[url path] toPath:[imports stringByAppendingPathComponent:name] error:&error]) {
        NSLog(@"OSU import copy failed: %@", error); pending_import = -1; legacy_request_frame();return NO;
    }
    pending_import = 1;legacy_request_frame();
    return YES;
}

int legacy_take_import_request(void) {
    int request = pending_import; pending_import = 0; return request;
}

@interface LegacyView : UIView
- (BOOL)renderGpuFrame;
@end

@interface LegacyAppDelegate : NSObject <UIApplicationDelegate, AVAudioPlayerDelegate, AVAudioSessionDelegate, UIAlertViewDelegate> {
    UIWindow *window;
    LegacyView *game_view;
    CADisplayLink *timer;
    unsigned idle_ticks;
}
@end

@implementation LegacyView
- (BOOL)renderGpuFrame {
    CFTimeInterval started=CACurrentMediaTime();if(!legacy_gpu_begin())return NO;
    canvas_dirty=0;
    if(frame_callback)frame_callback(480,320);
    else {legacy_gpu_text(34,128,[boot_message UTF8String],24,.95,.95,1);legacy_gpu_text(34,172,"Preparing the game. Please wait...",13,.95,.95,1);}
    legacy_gpu_end();
    static CFTimeInterval sample_start,draw_time;static unsigned sample_frames;
    CFTimeInterval ended=CACurrentMediaTime();if(!sample_start)sample_start=started;draw_time+=ended-started;sample_frames++;
    if(ended-sample_start>=5 && sample_frames>=10){NSLog(@"OSU drawing: %@ %.1f calls/s, %.1f ms/call",animate_frames?@"active":@"idle",sample_frames/(ended-sample_start),1000*draw_time/sample_frames);sample_start=ended;draw_time=0;sample_frames=0;}
    return YES;
}
- (void)drawRect:(CGRect)rect {
    if([self renderGpuFrame])return;
    CFTimeInterval started = CACurrentMediaTime();
    if(legacy_gpu_begin()) {
        if(frame_callback)frame_callback(480,320);
        else {legacy_gpu_text(34,128,[boot_message UTF8String],24,.95,.95,1);legacy_gpu_text(34,172,"Preparing the game. Please wait...",13,.95,.95,1);}
        legacy_gpu_end();
    } else {
    drawing_context = UIGraphicsGetCurrentContext();
    CGContextSaveGState(drawing_context);
    // A fixed landscape canvas keeps input and drawing in the same space on
    // iOS 4, including devices whose SpringBoard launches apps in portrait.
    CGFloat width = self.bounds.size.width, height = self.bounds.size.height;
    static BOOL logged_canvas;
    if (!logged_canvas) { NSLog(@"OSU UI: landscape canvas 480x320, view %gx%g", (double)width, (double)height); logged_canvas = YES; }
    if (height > width) {
        CGContextTranslateCTM(drawing_context, width, 0);
        CGContextRotateCTM(drawing_context, M_PI_2);
        CGContextScaleCTM(drawing_context, height / 480.0f, width / 320.0f);
    } else {
        CGContextScaleCTM(drawing_context, width / 480.0f, height / 320.0f);
    }
    if (frame_callback) frame_callback(480, 320);
    else {
        CGContextSetRGBFillColor(drawing_context, 0.95, 0.95, 1, 1);
        [boot_message drawAtPoint:CGPointMake(34, 128) withFont:[UIFont systemFontOfSize:24]];
        [@"Preparing the game. Please wait..." drawAtPoint:CGPointMake(34, 172) withFont:[UIFont systemFontOfSize:13]];
    }
    CGContextRestoreGState(drawing_context);
    drawing_context = NULL;
    legacy_software_frame_completed();
    }
    static CFTimeInterval sample_start, draw_time;
    static unsigned sample_frames;
    CFTimeInterval ended = CACurrentMediaTime();
    if (!sample_start) sample_start = started;
    draw_time += ended-started; sample_frames++;
    if (ended-sample_start >= 5 && sample_frames >= 10) {
        NSLog(@"OSU drawing: %@ %.1f calls/s, %.1f ms/call", animate_frames ? @"active" : @"idle", sample_frames/(ended-sample_start), 1000*draw_time/sample_frames);
        sample_start=ended; draw_time=0; sample_frames=0;
    }
}
- (void)sendTouches:(NSSet *)touches phase:(int)phase {
    for (UITouch *touch in touches) {
        CGPoint point = [touch locationInView:self];
        CGFloat width = self.bounds.size.width, height = self.bounds.size.height;
        double x, y;
        if (height > width) { x = point.y * 480.0 / height; y = (width - point.x) * 320.0 / width; }
        else { x = point.x * 480.0 / width; y = point.y * 320.0 / height; }
        if (touch_callback) touch_callback(phase, (int)(uintptr_t)touch, x, y);
        if(!animate_frames)legacy_request_frame();
    }
}
- (void)touchesBegan:(NSSet *)touches withEvent:(UIEvent *)event { [self sendTouches:touches phase:0]; }
- (void)touchesMoved:(NSSet *)touches withEvent:(UIEvent *)event { [self sendTouches:touches phase:1]; }
- (void)touchesEnded:(NSSet *)touches withEvent:(UIEvent *)event { [self sendTouches:touches phase:2]; }
- (void)touchesCancelled:(NSSet *)touches withEvent:(UIEvent *)event { [self sendTouches:touches phase:2]; }
@end

@implementation LegacyAppDelegate
- (void)alertView:(UIAlertView *)alert clickedButtonAtIndex:(NSInteger)button {
    if(alert!=search_alert)return;
    if(button==1){[pending_search release];pending_search=[[search_field text] copy];legacy_request_frame();}
    [search_field release];search_field=nil;[search_alert release];search_alert=nil;
}
- (BOOL)application:(UIApplication *)application handleOpenURL:(NSURL *)url { return queue_archive(url); }
- (BOOL)application:(UIApplication *)application openURL:(NSURL *)url sourceApplication:(NSString *)source annotation:(id)annotation { return queue_archive(url); }
- (void)applicationDidFinishLaunching:(UIApplication *)application {
    AVAudioSession *audioSession=[AVAudioSession sharedInstance];audioSession.delegate=self;NSError *audioError=nil;
    if(![audioSession setCategory:AVAudioSessionCategoryPlayback error:&audioError] || ![audioSession setActive:YES error:&audioError])NSLog(@"OSU sounds: audio session activation failed %@",audioError);
    window = [[UIWindow alloc] initWithFrame:[[UIScreen mainScreen] bounds]];
    background_view = [[UIImageView alloc] initWithFrame:CGRectMake(0,0,480,320)];
    background_view.center = CGPointMake(window.bounds.size.width/2,window.bounds.size.height/2);
    if (window.bounds.size.height > window.bounds.size.width) background_view.transform = CGAffineTransformMakeRotation(M_PI_2);
    background_view.contentMode = UIViewContentModeScaleToFill;
    background_view.backgroundColor = [UIColor colorWithRed:0.035 green:0.045 blue:0.075 alpha:1];
    background_view.opaque = YES;
    [window addSubview:background_view];
    legacy_triangles_attach(window,background_view);
    dim_view = [[UIView alloc] initWithFrame:window.bounds];
    dim_view.backgroundColor = [UIColor blackColor]; dim_view.alpha = 0; dim_view.userInteractionEnabled = NO;
    [window addSubview:dim_view];
    game_view = [[LegacyView alloc] initWithFrame:window.bounds];
    canvas_view = game_view;
    legacy_gpu_attach(game_view);
    game_view.opaque = NO; game_view.backgroundColor = [UIColor clearColor];
    game_view.clearsContextBeforeDrawing = YES;
    game_view.multipleTouchEnabled = YES;
    [window addSubview:game_view];
    legacy_menu_effects_attach(window,game_view);
    [window makeKeyAndVisible];
    timer=[[CADisplayLink displayLinkWithTarget:self selector:@selector(tick:)] retain];
    timer.frameInterval=1;[timer addToRunLoop:[NSRunLoop mainRunLoop] forMode:NSRunLoopCommonModes];
    // Return launch control to UIKit and present a native frame before loading
    // Mono or compiling managed UI code. iOS 4 has a strict launch watchdog.
    [NSTimer scheduledTimerWithTimeInterval:0.5 target:self selector:@selector(startManaged:)
        userInfo:nil repeats:NO];
}
- (void)tick:(CADisplayLink *)unused {
    legacy_startup_tick();
    CFTimeInterval now=CACurrentMediaTime();
    if(poll_jobs && poll_callback && now>=next_job_poll) {
        next_job_poll=now+.2;
        CFTimeInterval pollStart=CACurrentMediaTime();if(poll_callback())canvas_dirty=1;
        double pollMs=(CACurrentMediaTime()-pollStart)*1000;if(pollMs>100)NSLog(@"OSU stall: background poll %.1f ms",pollMs);
    }
    int redraw=canvas_dirty || !frame_callback || animate_frames || animate_ui || (idle_redraws && ++idle_ticks>=30);
    int presented=legacy_menu_effects_tick(!redraw);
    if(redraw){idle_ticks=0;if(![game_view renderGpuFrame])[game_view setNeedsDisplay];}
    else if(!presented){presented=legacy_song_list_tick();if(!presented && cached_dirty)legacy_gpu_present_cached();}
    cached_dirty=0;
    double tickMs=(CACurrentMediaTime()-now)*1000;if(tickMs>150)NSLog(@"OSU stall: main frame %.1f ms",tickMs);
}
- (void)startManaged:(NSTimer *)unused { start_managed_runtime(); }
- (void)applicationWillResignActive:(UIApplication *)application { timer.paused=YES;legacy_gpu_suspend(1);if(touch_callback)touch_callback(3,0,0,0);legacy_leadin_pause(&gameplay_leadin,CACurrentMediaTime()*1000);[player pause];legacy_sound_suspend();legacy_startup_pause();legacy_menu_triangles_suspend(1);legacy_menu_effects_suspend(1); }
- (void)applicationDidReceiveMemoryWarning:(UIApplication *)application { legacy_clear_artwork_cache(); }
- (void)applicationDidBecomeActive:(UIApplication *)application {
    NSError *audioError=nil;if(![[AVAudioSession sharedInstance] setActive:YES error:&audioError])NSLog(@"OSU sounds: foreground activation failed %@",audioError);legacy_sound_resume();
    timer.paused=NO;
    legacy_gpu_suspend(0);legacy_request_frame();
    legacy_startup_resume();
    legacy_menu_triangles_suspend(0);
    legacy_menu_effects_suspend(0);
    if (player && audio_ended_at < 0 && !gameplay_audio_paused){if(gameplay_leadin.waiting)legacy_leadin_resume(&gameplay_leadin,CACurrentMediaTime()*1000);else [player play];}
}
- (void)beginInterruption {legacy_sound_suspend();}
- (void)endInterruptionWithFlags:(NSUInteger)flags {if(flags&AVAudioSessionInterruptionFlags_ShouldResume){NSError *error=nil;if([[AVAudioSession sharedInstance] setActive:YES error:&error])legacy_sound_resume();else NSLog(@"OSU sounds: interruption activation failed %@",error);}}
- (void)audioPlayerDidFinishPlaying:(AVAudioPlayer *)audio successfully:(BOOL)flag {
    audio_ended_at = CFAbsoluteTimeGetCurrent();
}
- (void)dealloc {
    [timer invalidate]; [timer release]; [game_view release]; [background_view release]; [dim_view release]; [window release];
    [super dealloc];
}
@end

void legacy_text_open(const char *title,const char *current) {
    if(search_alert)return;
    search_alert=[[UIAlertView alloc] initWithTitle:title?[NSString stringWithUTF8String:title]:@"Search beatmaps" message:@"\n\n" delegate:[UIApplication sharedApplication].delegate cancelButtonTitle:@"Cancel" otherButtonTitles:title?@"Apply":@"Search",nil];
    search_field=[[UITextField alloc] initWithFrame:CGRectMake(12,45,260,30)];
    search_field.backgroundColor=[UIColor whiteColor];search_field.textColor=[UIColor blackColor];
    search_field.borderStyle=UITextBorderStyleRoundedRect;search_field.clearButtonMode=UITextFieldViewModeWhileEditing;
    search_field.autocorrectionType=UITextAutocorrectionTypeNo;search_field.autocapitalizationType=UITextAutocapitalizationTypeNone;
    search_field.placeholder=title?@"Enter value":@"title, artist, or difficulty";
    if(current)search_field.text=[NSString stringWithUTF8String:current];
    [search_alert addSubview:search_field];[search_alert show];[search_field becomeFirstResponder];
}
void legacy_search_open(const char *current){legacy_text_open(NULL,current);}
const char *legacy_search_take(void) {
    if(!pending_search)return NULL;
    static char value[512];const char *source=[pending_search UTF8String];
    strncpy(value,source?source:"",sizeof(value)-1);value[sizeof(value)-1]=0;
    [pending_search release];pending_search=nil;return value;
}

const char *legacy_bundle_path(void) {
    NSLog(@"OSU checkpoint: managed entry reached native bundle binding");
    return [[[NSBundle mainBundle] bundlePath] UTF8String];
}
const char *legacy_documents_path(void) {
    return [[NSSearchPathForDirectoriesInDomains(NSDocumentDirectory, NSUserDomainMask, YES) objectAtIndex:0] UTF8String];
}
int legacy_start(FrameCallback frame, TouchCallback touch) {
    frame_callback = frame; touch_callback = touch;
    legacy_request_frame();
    NSLog(@"OSU boot: managed callbacks registered");
    return 0;
}
void legacy_rect(double x, double y, double width, double height, double red, double green, double blue, double alpha, double slant) {
    if (legacy_gpu_active()) { legacy_gpu_rect(x,y,width,height,red,green,blue,alpha,slant); return; }
    if (!drawing_context) return;
    CGContextSetRGBFillColor(drawing_context, red, green, blue, alpha);
    CGContextBeginPath(drawing_context);
    CGContextMoveToPoint(drawing_context, x + slant, y);
    CGContextAddLineToPoint(drawing_context, x + width, y);
    CGContextAddLineToPoint(drawing_context, x + width - slant, y + height);
    CGContextAddLineToPoint(drawing_context, x, y + height);
    CGContextClosePath(drawing_context);
    CGContextFillPath(drawing_context);
}
void legacy_image(const char *name, double x, double y, double width, double height, double alpha) {
    if ((!drawing_context && !legacy_gpu_active()) || !name) return;
    if (!ui_images) ui_images = [[NSMutableDictionary alloc] init];
    NSString *assetName=[NSString stringWithUTF8String:name];
    int decodeSize=16;while(decodeSize<fmax(width,height) && decodeSize<512)decodeSize*=2;
    NSString *key=[NSString stringWithFormat:@"%@:%d",assetName,decodeSize];
    UIImage *image = [ui_images objectForKey:key];
    if (!image) {
        NSString *path = [[[NSBundle mainBundle] bundlePath] stringByAppendingPathComponent:[@"Assets" stringByAppendingPathComponent:assetName]];
        // Decode directly to a thumbnail; decoding the full multi-megapixel
        // resource and resizing it afterward delayed the first frame on ARMv6.
        CGImageRef thumbnail = legacy_decode_image([path fileSystemRepresentation], decodeSize);
        if (thumbnail) { image = [UIImage imageWithCGImage:thumbnail]; CGImageRelease(thumbnail); }
        if (image) [ui_images setObject:image forKey:key];
    }
    if (legacy_gpu_active()) { legacy_gpu_image(image,[@"asset:" stringByAppendingString:key],x,y,width,height,alpha); return; }
    [image drawInRect:CGRectMake(x, y, width, height) blendMode:kCGBlendModeNormal alpha:alpha];
}
void legacy_set_animation(int active) { animate_frames = active != 0;legacy_gpu_cache_ui(!active); }
void legacy_set_idle_redraw(int active) { idle_redraws = active != 0; }
void legacy_poll_jobs(PollCallback callback,int active) {
    if(active && (!poll_jobs || callback!=poll_callback))next_job_poll=0;
    poll_callback=callback;poll_jobs=active!=0;
}
void legacy_import_thread_priority(void) {
    legacy_import_worker_register();
    NSAutoreleasePool *pool=[[NSAutoreleasePool alloc] init];[NSThread setThreadPriority:.15];[pool drain];
}
void legacy_artwork_quality(int quality) {
    if (quality < 0 || quality > 2) quality = 0;
    if (quality == artwork_quality) return;
    artwork_quality = quality; legacy_clear_artwork_cache();
    background_view.layer.magnificationFilter = quality ? kCAFilterNearest : kCAFilterLinear;
    legacy_request_frame();
}
static UIImage *prepared_artwork(NSString *filename, BOOL bundled, int width, int height) {
    if (!filename || width < 1 || height < 1) return nil;
    if (!artwork_images) { artwork_images = [[NSMutableDictionary alloc] init]; artwork_order = [[NSMutableArray alloc] init]; }
    int divisor = 1 << artwork_quality;
    width = (width+divisor-1)/divisor; height = (height+divisor-1)/divisor;
    NSString *key = [NSString stringWithFormat:@"%d:%dx%d:%@", bundled, width, height, filename];
    id cached = [artwork_images objectForKey:key];
    if (!cached) {
        CFTimeInterval artworkStart=CACurrentMediaTime();
        NSString *path;
        BOOL allowed = bundled;
        if (bundled) path = [[[NSBundle mainBundle] bundlePath] stringByAppendingPathComponent:[@"Assets" stringByAppendingPathComponent:filename]];
        else {
            path = [[filename stringByStandardizingPath] stringByResolvingSymlinksInPath];
            NSString *documents = [[NSSearchPathForDirectoriesInDomains(NSDocumentDirectory, NSUserDomainMask, YES) objectAtIndex:0] stringByResolvingSymlinksInPath];
            allowed = [path hasPrefix:[documents stringByAppendingString:@"/"]];
        }
        UIImage *image = nil;
        if (allowed) {
            CGImageRef raster = legacy_raster_artwork([path fileSystemRepresentation], width, height);
            if (raster) { image = [UIImage imageWithCGImage:raster]; CGImageRelease(raster); }
        }
        cached = image ? (id)image : (id)[NSNull null];
        [artwork_images setObject:cached forKey:key];
        if (!image) NSLog(@"OSU artwork: unavailable %@", [filename lastPathComponent]);
        double artworkMs=(CACurrentMediaTime()-artworkStart)*1000;if(artworkMs>100)NSLog(@"OSU stall: artwork %@ %dx%d %.1f ms",[filename lastPathComponent],width,height,artworkMs);
    }
    [artwork_order removeObject:key]; [artwork_order addObject:key];
    while ([artwork_order count] > 12) {
        NSString *oldest = [artwork_order objectAtIndex:0];
        [artwork_images removeObjectForKey:oldest]; [artwork_order removeObjectAtIndex:0];
    }
    return cached == [NSNull null] ? nil : cached;
}
static BOOL loader_background_mode;
static UIImage *loader_blur(UIImage *image){
    if(!image)return nil;
    // Fixed loader blur is cached with the background, never filtered per frame.
    const int w=160,h=107,n=w*h*4;
    unsigned char *pixels=malloc(n),*scratch=malloc(n);if(!pixels || !scratch){free(pixels);free(scratch);return image;}
    CGColorSpaceRef colour=CGColorSpaceCreateDeviceRGB();
    CGContextRef bitmap=CGBitmapContextCreate(pixels,w,h,8,w*4,colour,kCGImageAlphaPremultipliedLast|kCGBitmapByteOrder32Big);CGColorSpaceRelease(colour);
    if(!bitmap){free(pixels);free(scratch);return image;}
    CGContextDrawImage(bitmap,CGRectMake(0,0,w,h),image.CGImage);
    // Three separable box passes approximate lazer's fixed Gaussian backdrop.
    for(int pass=0;pass<3;pass++){
        for(int y=0;y<h;y++)for(int x=0;x<w;x++)for(int c=0;c<4;c++){int sum=0;for(int k=-2;k<=2;k++){int xx=x+k;if(xx<0)xx=0;if(xx>=w)xx=w-1;sum+=pixels[(y*w+xx)*4+c];}scratch[(y*w+x)*4+c]=(sum+2)/5;}
        for(int y=0;y<h;y++)for(int x=0;x<w;x++)for(int c=0;c<4;c++){int sum=0;for(int k=-2;k<=2;k++){int yy=y+k;if(yy<0)yy=0;if(yy>=h)yy=h-1;sum+=scratch[(yy*w+x)*4+c];}pixels[(y*w+x)*4+c]=(sum+2)/5;}
    }
    CGImageRef raster=CGBitmapContextCreateImage(bitmap);UIImage *result=raster?[UIImage imageWithCGImage:raster]:image;
    if(raster)CGImageRelease(raster);CGContextRelease(bitmap);free(pixels);free(scratch);return result;
}
int legacy_background(const char *filename, int bundled, double dim) {
    NSString *path = filename ? [NSString stringWithUTF8String:filename] : nil;
    NSString *key = [NSString stringWithFormat:@"%d:%d:%d:%@", artwork_quality, bundled,loader_background_mode, path ? path : @""];
    if (![key isEqualToString:background_key]) {
        background_view.image = path ? prepared_artwork(path,bundled != 0,480,320) : nil;
        if(loader_background_mode)background_view.image=loader_blur(background_view.image);
        [background_key release]; background_key = [key copy];
        [gpu_background_key release];gpu_background_key=[[@"background:" stringByAppendingString:key] copy];
    }
    double nextDim=fmax(0,fmin(0.8,dim));if(dim_view.alpha!=nextDim){dim_view.alpha=nextDim;legacy_menu_effects_invalidate();}
    legacy_gpu_background(background_view.image,gpu_background_key,nextDim,artwork_quality!=0);
    if(!legacy_gpu_active()){background_view.hidden=NO;dim_view.hidden=NO;}
    return background_view.image != nil;
}
void legacy_loader_background(const char *path,int bundled){loader_background_mode=YES;legacy_background(path,bundled,.2);loader_background_mode=NO;}
void legacy_draw_gpu_background(void){legacy_gpu_background(background_view.image,gpu_background_key,dim_view.alpha,artwork_quality!=0);}
void legacy_gpu_background_layers(int opaque){BOOL hidden=opaque!=0;if(background_view.hidden!=hidden)background_view.hidden=hidden;if(!dim_view.hidden)dim_view.hidden=YES;}
UIImage *legacy_song_thumbnail(NSString *path){return prepared_artwork(path,NO,44,36);}
int legacy_artwork(const char *filename, double x, double y, double width, double height, double alpha) {
    if ((!drawing_context && !legacy_gpu_active()) || !filename || width <= 0 || height <= 0 || width > 480 || height > 320) return 0;
    NSString *path = [NSString stringWithUTF8String:filename];
    UIImage *image = prepared_artwork(path,NO,(int)ceil(width),(int)ceil(height));
    if (!image) return 0;
    if (legacy_gpu_active()) {
        NSString *key=[NSString stringWithFormat:@"art:%d:%dx%d:%@",artwork_quality,(int)ceil(width),(int)ceil(height),path];
        legacy_gpu_image(image,key,x,y,width,height,alpha); return 1;
    }
    CGContextSaveGState(drawing_context);
    CGContextSetInterpolationQuality(drawing_context,kCGInterpolationNone);
    [image drawInRect:CGRectMake(x,y,width,height) blendMode:alpha >= 1 ? kCGBlendModeCopy : kCGBlendModeNormal alpha:alpha];
    CGContextRestoreGState(drawing_context);
    return 1;
}
double legacy_ui_time(void) { return CFAbsoluteTimeGetCurrent(); }
static void update_volumes(void) {player.volume=master_volume*music_volume*gameplay_gain;legacy_sound_volume(master_volume*effects_volume);legacy_startup_volume(master_volume*music_volume);}
void legacy_gameplay_mod_audio(double rate,int pitch,double gain){static int last_pitch=-1;if(gameplay_rate==rate && gameplay_gain==gain && last_pitch==pitch)return;last_pitch=pitch;gameplay_rate=rate>=.5 && rate<=2?rate:1;gameplay_gain=fmax(0,fmin(1,gain));legacy_sound_mod_pitch(pitch?gameplay_rate:1);legacy_sound_mod_gain(gameplay_gain);update_volumes();}
void legacy_volume(double volume) {master_volume=fmax(0,fmin(1,volume));update_volumes();}
void legacy_music_volume(double volume) {music_volume=fmax(0,fmin(1,volume));update_volumes();}
void legacy_effects_volume(double volume) {effects_volume=fmax(0,fmin(1,volume));update_volumes();}
double legacy_brightness_get(void) {
    CFPropertyListRef value=CFPreferencesCopyAppValue(CFSTR("SBBacklightLevel"),CFSTR("com.apple.springboard"));
    double level=.5;if(value && CFGetTypeID(value)==CFNumberGetTypeID())CFNumberGetValue((CFNumberRef)value,kCFNumberDoubleType,&level);
    if(value)CFRelease(value);return fmax(0,fmin(1,level));
}
void legacy_brightness(double value) {
    static void (*setLevel)(float);static int resolved;
    if(!resolved){resolved=1;void *library=dlopen("/System/Library/PrivateFrameworks/GraphicsServices.framework/GraphicsServices",RTLD_LAZY);if(library)setLevel=dlsym(library,"GSEventSetBacklightLevel");if(!setLevel)NSLog(@"OSU backlight API unavailable");}
    if(setLevel)setLevel((float)fmax(.05,fmin(1,value)));
}
void legacy_path(const double *points, int count, double originX, double originY, double scale, double width, double red, double green, double blue, double alpha) {
    if (legacy_gpu_active()) { legacy_gpu_path(points,count,originX,originY,scale,width,red,green,blue,alpha); return; }
    if (!drawing_context || !points || count < 1) return;
    CGContextSetRGBStrokeColor(drawing_context, red, green, blue, alpha);
    CGContextSetLineWidth(drawing_context, width * scale);
    CGContextSetLineCap(drawing_context, kCGLineCapRound);
    CGContextSetLineJoin(drawing_context, kCGLineJoinRound);
    CGContextBeginPath(drawing_context);
    CGContextMoveToPoint(drawing_context, originX + points[0] * scale, originY + points[1] * scale);
    for (int i = 1; i < count; i++) CGContextAddLineToPoint(drawing_context, originX + points[i*2] * scale, originY + points[i*2+1] * scale);
    CGContextStrokePath(drawing_context);
}
void legacy_arc(double x, double y, double radius, double progress, double width, double red, double green, double blue, double alpha) {
    if (legacy_gpu_active()) { legacy_gpu_arc(x,y,radius,progress,width,red,green,blue,alpha); return; }
    if (!drawing_context || progress <= 0) return;
    CGContextSetRGBStrokeColor(drawing_context, red, green, blue, alpha);
    CGContextSetLineWidth(drawing_context, width);
    CGContextBeginPath(drawing_context);
    CGContextAddArc(drawing_context, x, y, radius, -M_PI_2, -M_PI_2 + fmin(1,progress) * 2 * M_PI, 0);
    CGContextStrokePath(drawing_context);
}
void legacy_circle(double x, double y, double radius, double red, double green, double blue, double alpha, int fill) {
    if (legacy_gpu_active()) { legacy_gpu_circle(x,y,radius,red,green,blue,alpha,fill); return; }
    if (!drawing_context) return;
    CGRect rect = CGRectMake(x-radius, y-radius, radius*2, radius*2);
    if (fill) {
        CGContextSetRGBFillColor(drawing_context, red, green, blue, alpha);
        CGContextFillEllipseInRect(drawing_context, rect);
    } else {
        CGContextSetRGBStrokeColor(drawing_context, red, green, blue, alpha);
        CGContextSetLineWidth(drawing_context, 2);
        CGContextStrokeEllipseInRect(drawing_context, rect);
    }
}
void legacy_text(double x, double y, const char *text, double size, double red, double green, double blue) {
    if (legacy_gpu_active()) { legacy_gpu_text(x,y,text,size,red,green,blue); return; }
    if (!drawing_context || !text) return;
    static int logged_text_calls;
    if (logged_text_calls++ < 3)
        NSLog(@"OSU text x=%g y=%g size=%g rgb=%g,%g,%g", x, y, size, red, green, blue);
    NSString *value = [NSString stringWithUTF8String:text];
    CGContextSetRGBFillColor(drawing_context, red, green, blue, 1);
    [value drawAtPoint:CGPointMake(x,y) withFont:[UIFont systemFontOfSize:size]];
}
void legacy_loader_text(double x,double y,const char *text,double size,double r,double g,double b){
    if(legacy_gpu_active()){legacy_gpu_loader_text(x,y,text,size,r,g,b);return;}
    if(!drawing_context || !text)return;CGContextSetRGBFillColor(drawing_context,r,g,b,1);
    [[NSString stringWithUTF8String:text] drawAtPoint:CGPointMake(x,y) withFont:[UIFont italicSystemFontOfSize:size]];
}
void legacy_ui_clip_begin(double x,double y,double w,double h){if(legacy_gpu_active()){legacy_gpu_clip(x,y,w,h);return;}if(drawing_context){CGContextSaveGState(drawing_context);CGContextClipToRect(drawing_context,CGRectMake(x,y,w,h));}}
void legacy_ui_clip_end(void){if(legacy_gpu_active()){legacy_gpu_clip(0,0,0,0);return;}if(drawing_context)CGContextRestoreGState(drawing_context);}
void legacy_hud_push(double x,double y,double scale,double angle,double opacity){if(legacy_gpu_active()){legacy_gpu_push_hud(x,y,scale,angle,opacity);return;}if(drawing_context){CGContextSaveGState(drawing_context);CGContextTranslateCTM(drawing_context,x,y);CGContextRotateCTM(drawing_context,angle);CGContextScaleCTM(drawing_context,scale,scale);CGContextSetAlpha(drawing_context,opacity);}}
void legacy_hud_pop(void){if(legacy_gpu_active())legacy_gpu_pop_hud();else if(drawing_context)CGContextRestoreGState(drawing_context);}
double legacy_text_width(const char *text,double size){NSString *value=text?[NSString stringWithUTF8String:text]:nil;return value && size>0?[value sizeWithFont:[UIFont systemFontOfSize:size]].width:0;}
static int start_gameplay_audio(const char *path,double leadin,int deferred) {
    gameplay_audio_paused=gameplay_audio_pending=deferred!=0;
    legacy_menu_suspend();
    [player stop]; [player release]; player = nil; audio_ended_at = -1;
    NSError *error = nil;
    NSURL *url = [NSURL fileURLWithPath:[NSString stringWithUTF8String:path]];
    player = [[AVAudioPlayer alloc] initWithContentsOfURL:url error:&error];
    if (!player) { NSLog(@"Audio open failed: %@", error); return 0; }
    player.delegate = (id<AVAudioPlayerDelegate>)[UIApplication sharedApplication].delegate;
    player.volume = master_volume*music_volume;
    [player prepareToPlay];
    legacy_leadin_start(&gameplay_leadin,leadin/gameplay_rate,CACurrentMediaTime()*1000);
    if(deferred)legacy_leadin_pause(&gameplay_leadin,CACurrentMediaTime()*1000);
    if(!deferred && !gameplay_leadin.waiting && ![player play])return 0;
    legacy_import_pause(1);return 1;
}
int legacy_audio_start(const char *path){return start_gameplay_audio(path,0,0);}
int legacy_audio_start_delayed(const char *path,double leadin){return start_gameplay_audio(path,leadin,0);}
int legacy_audio_prepare_gameplay(const char *path,double leadin){return start_gameplay_audio(path,leadin,1);}
int legacy_audio_commit_gameplay(void){
    if(!gameplay_audio_pending || !player)return 0;
    gameplay_audio_pending=gameplay_audio_paused=NO;
    legacy_leadin_start(&gameplay_leadin,gameplay_leadin.duration,CACurrentMediaTime()*1000);
    return gameplay_leadin.waiting || [player play];
}
void legacy_audio_prime_retry(double leadin){
    gameplay_audio_pending=gameplay_audio_paused=YES;audio_ended_at=-1;[player stop];player.currentTime=0;[player prepareToPlay];
    legacy_leadin_start(&gameplay_leadin,leadin/gameplay_rate,CACurrentMediaTime()*1000);legacy_leadin_pause(&gameplay_leadin,CACurrentMediaTime()*1000);
}
void legacy_audio_stop(void) {legacy_gameplay_mod_audio(1,0,1); gameplay_audio_pending=gameplay_audio_paused=NO;legacy_leadin_start(&gameplay_leadin,0,CACurrentMediaTime()*1000);legacy_sound_stop(); [player stop]; [player release]; player = nil; audio_ended_at = -1;legacy_import_pause(0);legacy_menu_resume(); }
void legacy_audio_pause(void) { gameplay_audio_paused=YES;legacy_leadin_pause(&gameplay_leadin,CACurrentMediaTime()*1000);legacy_sound_stop(); [player pause]; }
void legacy_audio_resume(void) { gameplay_audio_paused=NO;if(gameplay_leadin.waiting)legacy_leadin_resume(&gameplay_leadin,CACurrentMediaTime()*1000);else if(player && audio_ended_at<0)[player play]; }
void legacy_audio_restart(void) { gameplay_audio_paused=NO;if(!player)return;audio_ended_at=-1;[player stop];player.currentTime=0;[player prepareToPlay];legacy_leadin_start(&gameplay_leadin,gameplay_leadin.duration,CACurrentMediaTime()*1000);if(!gameplay_leadin.waiting)[player play]; }
double legacy_audio_time(void) {
    if (!player) return 0;
    if(gameplay_audio_pending)return -gameplay_leadin.duration*gameplay_rate;
    if(gameplay_leadin.waiting){double now=CACurrentMediaTime()*1000;if(legacy_leadin_due(&gameplay_leadin,now)){if(![player play])NSLog(@"OSU: gameplay audio failed after lead-in");}else return legacy_leadin_time(&gameplay_leadin,now)*gameplay_rate;}
    if (audio_ended_at >= 0) return (player.duration + CFAbsoluteTimeGetCurrent() - audio_ended_at) * 1000*gameplay_rate;
    return player.currentTime * 1000*gameplay_rate;
}

UIView *legacy_background_view(void){return background_view;}

static void start_managed_runtime(void) {
    NSString *bundle = [[NSBundle mainBundle] bundlePath];
    NSString *lib = [bundle stringByAppendingPathComponent:@"lib"];
    NSString *etc = [bundle stringByAppendingPathComponent:@"etc"];
    NSString *managed = [bundle stringByAppendingPathComponent:@"managed/OsuLegacy.exe"];
    mono_set_dirs([lib UTF8String], [etc UTF8String]);
    // In Mono 3.0.12's supported_runtimes table, abbreviated v4.0 selects
    // v4.0.30319 (profile 4.5); this exact ID selects the bundled profile 4.0.
    NSLog(@"OSU boot: initializing embedded Mono after native launch");
    char *options[] = { "--optimize=-all" };
    mono_jit_parse_options(1, options);
    mono_config_parse(NULL);
    void *domain = mono_jit_init_version("osu legacy", "v4.0.30128");
    void *assembly = domain ? mono_domain_assembly_open(domain, [managed UTF8String]) : NULL;
    void *klass = assembly ? mono_class_from_name(mono_assembly_get_image(assembly), "OsuLegacy", "Program") : NULL;
    void *entry = klass ? mono_class_get_method_from_name(klass, "Main", 0) : NULL;
    if (!entry) { boot_message = @"Could not load the managed UI"; NSLog(@"OSU boot: assembly/entry load failed"); return; }
    char *arguments[] = { (char *)[managed UTF8String], NULL };
    void *exception = NULL;
    int result = mono_runtime_run_main(entry, 1, arguments, &exception);
    // The runtime remains alive while UIKit invokes the registered delegates.
    // mono_main() would shut it down when the managed entry point returned.
    if (exception || result != 0) {
        frame_callback = NULL; touch_callback = NULL;
        boot_message = @"The managed UI could not start";
        NSLog(@"OSU boot: managed startup failed (code %d, exception %p)", result, exception);
        if (exception) mono_print_unhandled_exception(exception);
    } else NSLog(@"OSU boot: managed startup returned to UIKit");
}

int main(int argc, char **argv) {
    unsetenv("OSU_ARM_DIAGNOSTICS");
    NSAutoreleasePool *pool = [[NSAutoreleasePool alloc] init];
    int result = UIApplicationMain(argc, argv, nil, @"LegacyAppDelegate");
    [pool drain];
    return result;
}
