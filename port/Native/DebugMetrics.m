#import <Foundation/Foundation.h>
#import <QuartzCore/QuartzCore.h>
#import "GpuRenderer.h"
#import "DebugMetrics.h"
#include "DebugMetricsMath.h"
#include <sys/resource.h>
#include <stdint.h>
#include <pthread.h>
#include <dlfcn.h>
extern void legacy_request_cached_frame(void);

// Load the registry reader dynamically: missing IOKit APIs or GPU counters
// must not prevent the app from running on this reconstructed iOS 4 SDK.
typedef uint32_t IOObject;
static void *ioKit;
static BOOL resultsPosition;
static CFMutableDictionaryRef (*serviceMatching)(const char *);
static int (*matchingServices)(uint32_t,CFDictionaryRef,IOObject *);
static IOObject (*iteratorNext)(IOObject);
static int (*objectRelease)(IOObject);
static CFTypeRef (*entryProperty)(IOObject,CFStringRef,CFAllocatorRef,uint32_t);
static int (*entryProperties)(IOObject,CFMutableDictionaryRef *,CFAllocatorRef,uint32_t);
static int (*objectClass)(IOObject,char *);
static int (*entryName)(IOObject,char *);
static int (*registryIterator)(uint32_t,const char *,uint32_t,IOObject *);
static double gpu_value(CFTypeRef property){
    if(!property || CFGetTypeID(property)!=CFDictionaryGetTypeID())return -1;
    const CFStringRef keys[]={CFSTR("Device Utilization %"),CFSTR("GPU Activity(%)"),CFSTR("GPU Utilization %")};
    for(unsigned i=0;i<sizeof(keys)/sizeof(keys[0]);i++){CFTypeRef value=CFDictionaryGetValue((CFDictionaryRef)property,keys[i]);double percent;
        if(value && CFGetTypeID(value)==CFNumberGetTypeID() && CFNumberGetValue((CFNumberRef)value,kCFNumberDoubleType,&percent) && legacy_gpu_percent(percent)>=0)return percent;}
    return -1;
}
static double process_cpu_seconds(void){struct rusage usage;if(getrusage(RUSAGE_SELF,&usage))return -1;return usage.ru_utime.tv_sec+usage.ru_utime.tv_usec/1e6+usage.ru_stime.tv_sec+usage.ru_stime.tv_usec/1e6;}
static void save_gpu_probe(NSDictionary *probe){NSString *documents=[NSSearchPathForDirectoriesInDomains(NSDocumentDirectory,NSUserDomainMask,YES) objectAtIndex:0];[probe writeToFile:[documents stringByAppendingPathComponent:@"debug-gpu-probe.plist"] atomically:YES];}
@interface LegacyDebugMetrics : NSObject {
@public BOOL enabled,suspended;
    NSTimer *timer;NSString *line;BOOL probeStarted;
    unsigned long long frames,previousFrames;double lastWall,lastCPU;
    IOObject gpuService;NSString *gpuProperty;pthread_mutex_t gpuLock;
}
- (void)setEnabled:(BOOL)value;
- (void)setSuspended:(BOOL)value;
- (void)sample:(NSTimer *)unused;
- (void)discoverGPU;
- (void)draw;
- (void)frame;
@end
static LegacyDebugMetrics *metrics;
@implementation LegacyDebugMetrics
- (id)init {if((self=[super init]))pthread_mutex_init(&gpuLock,NULL);return self;}
- (void)reset {frames=previousFrames=0;lastWall=CACurrentMediaTime();lastCPU=process_cpu_seconds();}
- (void)setEnabled:(BOOL)value {
    if(enabled==value)return;enabled=value;[self reset];
    [timer invalidate];[timer release];timer=nil;
    [line release];line=value?[@"FPS --   CPU --   GPU N/A" copy]:nil;
    if(value){
        timer=[[NSTimer scheduledTimerWithTimeInterval:1 target:self selector:@selector(sample:) userInfo:nil repeats:YES] retain];
        [[NSRunLoop mainRunLoop] addTimer:timer forMode:NSRunLoopCommonModes];
        if(!probeStarted){probeStarted=YES;[NSThread detachNewThreadSelector:@selector(discoverGPU) toTarget:self withObject:nil];}
    }
}
- (void)setSuspended:(BOOL)value {suspended=value;[self reset];}
- (void)frame {if(enabled && !suspended)frames++;}
- (void)sample:(NSTimer *)unused {
    if(!enabled || suspended)return;double now=CACurrentMediaTime(),cpu=process_cpu_seconds(),wall=now-lastWall;
    double fps=legacy_presented_fps(previousFrames,frames,wall),cpuPercent=lastCPU>=0 && cpu>=0?legacy_cpu_percent(lastCPU,cpu,wall):-1,gpuPercent=-1;
    pthread_mutex_lock(&gpuLock);IOObject service=gpuService;NSString *key=gpuProperty;pthread_mutex_unlock(&gpuLock);
    if(service && entryProperty){CFTypeRef property=NULL;
        if([key length])property=entryProperty(service,(CFStringRef)key,kCFAllocatorDefault,0);
        else if(entryProperties){CFMutableDictionaryRef all=NULL;if(entryProperties(service,&all,kCFAllocatorDefault,0)==0)property=all;}
        gpuPercent=gpu_value(property);if(property)CFRelease(property);}
    NSString *cpuLabel=cpuPercent>=0?[NSString stringWithFormat:@"%.0f%%",cpuPercent]:@"N/A";
    NSString *gpuLabel=gpuPercent>=0?[NSString stringWithFormat:@"%.0f%%",gpuPercent]:@"N/A";
    NSString *value=[NSString stringWithFormat:@"FPS %.1f   CPU %@   GPU %@",fmax(0,fps),cpuLabel,gpuLabel];
    [line release];line=[value copy];previousFrames=frames;lastCPU=cpu;lastWall=now;
    // Refresh an idle menu once a second; gameplay already draws each frame.
    // The debug overlay does not create another GL surface or a display link.
    legacy_request_cached_frame();
}
- (void)discoverGPU {
    NSAutoreleasePool *pool=[[NSAutoreleasePool alloc] init];[NSThread setThreadPriority:.1];
    NSMutableArray *records=[NSMutableArray array],*apiStatus=[NSMutableArray array];unsigned visited=0;
    const char *paths[]={"/System/Library/Frameworks/IOKit.framework/IOKit","/System/Library/Frameworks/IOKit.framework/Versions/A/IOKit","/System/Library/PrivateFrameworks/IOKit.framework/IOKit"};
    for(unsigned i=0;i<sizeof(paths)/sizeof(paths[0]);i++){ioKit=dlopen(paths[i],RTLD_LAZY|RTLD_LOCAL);if(ioKit)break;
        const char *error=dlerror();[apiStatus addObject:[NSString stringWithFormat:@"load %s: %s",paths[i],error?error:"unknown error"]];}
    if(!ioKit)goto unavailable;
    serviceMatching=dlsym(ioKit,"IOServiceMatching");matchingServices=dlsym(ioKit,"IOServiceGetMatchingServices");iteratorNext=dlsym(ioKit,"IOIteratorNext");objectRelease=dlsym(ioKit,"IOObjectRelease");entryProperty=dlsym(ioKit,"IORegistryEntryCreateCFProperty");registryIterator=dlsym(ioKit,"IORegistryCreateIterator");
    entryProperties=dlsym(ioKit,"IORegistryEntryCreateCFProperties");objectClass=dlsym(ioKit,"IOObjectGetClass");entryName=dlsym(ioKit,"IORegistryEntryGetName");
    if(!iteratorNext || !objectRelease || !entryProperty){[apiStatus addObject:@"required IOKit symbols missing"];goto unavailable;}
    for(int pass=0;pass<2;pass++){
        IOObject iterator=0;int status;
        if(pass==0){if(!serviceMatching || !matchingServices)continue;CFMutableDictionaryRef match=serviceMatching("IOAccelerator");if(!match)continue;status=matchingServices(0,match,&iterator);}
        else {if(!registryIterator)continue;status=registryIterator(0,"IOService",1,&iterator);}
        [apiStatus addObject:[NSString stringWithFormat:@"registry pass %d: 0x%x iterator %u",pass,status,iterator]];
        if(status || !iterator)continue;IOObject entry;
        while((entry=iteratorNext(iterator))){
            visited++;char className[128]={0},name[128]={0};if(objectClass)objectClass(entry,className);if(entryName)entryName(entry,name);
            NSString *identity=[NSString stringWithFormat:@"%s %s",className,name],*lower=[identity lowercaseString];
            BOOL graphics=pass==0 || [lower rangeOfString:@"mbx"].location!=NSNotFound || [lower rangeOfString:@"pvr"].location!=NSNotFound || [lower rangeOfString:@"sgx"].location!=NSNotFound || [lower rangeOfString:@"gpu"].location!=NSNotFound || [lower rangeOfString:@"accelerator"].location!=NSNotFound || [lower rangeOfString:@"graphics"].location!=NSNotFound;
            CFMutableDictionaryRef all=NULL;int propertiesStatus=-1;
            if(graphics && entryProperties)propertiesStatus=entryProperties(entry,&all,kCFAllocatorDefault,0);
            NSMutableDictionary *record=nil;
            if(graphics){record=[NSMutableDictionary dictionaryWithObjectsAndKeys:identity,@"driver",[NSNumber numberWithInt:propertiesStatus],@"propertiesStatus",nil];
                if(all){[record setObject:[(NSDictionary *)all allKeys] forKey:@"propertyKeys"];
                    NSMutableDictionary *counters=[NSMutableDictionary dictionary];
                    for(NSString *key in (NSDictionary *)all){NSString *k=[key lowercaseString];
                        if([k rangeOfString:@"stat"].location!=NSNotFound || [k rangeOfString:@"perf"].location!=NSNotFound || [k rangeOfString:@"util"].location!=NSNotFound || [k rangeOfString:@"busy"].location!=NSNotFound || [k rangeOfString:@"idle"].location!=NSNotFound || [k rangeOfString:@"counter"].location!=NSNotFound)[counters setObject:[(NSDictionary *)all objectForKey:key] forKey:key];}
                    [record setObject:counters forKey:@"counterProperties"];}
                [records addObject:record];}
            NSArray *keys=[NSArray arrayWithObjects:@"PerformanceStatistics",@"IOPerformanceStatistics",@"GPUStatistics",@"Statistics",@"GPU Performance Statistics",@"",nil];
            for(NSString *key in keys){
                CFTypeRef stats=NULL;
                if(![key length]){if(all)stats=CFRetain(all);}
                else if(all){CFTypeRef value=CFDictionaryGetValue(all,(CFStringRef)key);if(value)stats=CFRetain(value);}
                else if(graphics || [key isEqualToString:@"PerformanceStatistics"])stats=entryProperty(entry,(CFStringRef)key,kCFAllocatorDefault,0);
                double percent=gpu_value(stats);
                if(stats && record && CFGetTypeID(stats)==CFDictionaryGetTypeID())[record setObject:(id)stats forKey:[key length]?key:@"directProperties"];
                if(percent>=0){
                    if(stats)CFRelease(stats);if(all)CFRelease(all);objectRelease(iterator);
                    pthread_mutex_lock(&gpuLock);gpuService=entry;gpuProperty=[key copy];pthread_mutex_unlock(&gpuLock);
                    save_gpu_probe([NSDictionary dictionaryWithObjectsAndKeys:@"counter found",@"status",records,@"drivers",apiStatus,@"apiStatus",[NSNumber numberWithUnsignedInt:visited],@"visitedNodes",nil]);
                    NSLog(@"OSU debug: GPU counter found in %@ / %@",identity,[key length]?key:@"direct properties");[pool drain];return;
                }
                if(stats)CFRelease(stats);
            }
            if(all)CFRelease(all);objectRelease(entry);
        }
        objectRelease(iterator);
    }
unavailable:
    save_gpu_probe([NSDictionary dictionaryWithObjectsAndKeys:@"counter not identified",@"status",records,@"drivers",apiStatus,@"apiStatus",[NSNumber numberWithUnsignedInt:visited],@"visitedNodes",nil]);
    NSLog(@"OSU debug: GPU probe inspected %u nodes, %u graphics entries; saved debug-gpu-probe.plist",visited,(unsigned)[records count]);[pool drain];
}
- (void)draw {
    if(!enabled || suspended || !line)return;
    double x=resultsPosition?200:4,y=resultsPosition?65:300;
    legacy_gpu_rect(x,y,270,16,0,0,0,.82,0);
    legacy_gpu_text(x+5,y+3,[line UTF8String],9,.7,1,.75);
}
@end
void legacy_debug_set_enabled(int enabled){if(!metrics && enabled)metrics=[[LegacyDebugMetrics alloc] init];[metrics setEnabled:enabled!=0];}
void legacy_debug_suspend(int suspended){[metrics setSuspended:suspended!=0];}
void legacy_debug_draw(void){[metrics draw];}
void legacy_debug_frame_presented(void){[metrics frame];}
void legacy_debug_results(int active){resultsPosition=active!=0;}
