#import <Foundation/Foundation.h>
#include "MenuAudioWindow.h"
#include <CommonCrypto/CommonDigest.h>
#include <sys/stat.h>
#include <math.h>
#include <string.h>
#include <stdio.h>

// Reuse an existing cache, but never wait for creation of a whole-song cache.
NSString *legacy_cached_analysis_pcm(NSString *source) {
    NSString *theme=[[[NSBundle mainBundle] bundlePath] stringByAppendingPathComponent:@"Startup/menu.m4a"];
    if([source isEqualToString:theme])return [[[NSBundle mainBundle] bundlePath] stringByAppendingPathComponent:@"Startup/menu-analysis.pcm"];
    struct stat info;if(stat([source fileSystemRepresentation],&info))return nil;
    NSString *directory=[[NSSearchPathForDirectoriesInDomains(NSCachesDirectory,NSUserDomainMask,YES) objectAtIndex:0] stringByAppendingPathComponent:@"MenuPCM"];
    NSString *key=[NSString stringWithFormat:@"%@:%lld:%lld:11025-v1",source,(long long)info.st_size,(long long)info.st_mtime];
    unsigned char hash[CC_SHA256_DIGEST_LENGTH];CC_SHA256([key UTF8String],(CC_LONG)strlen([key UTF8String]),hash);
    char hex[65];for(int i=0;i<32;i++)sprintf(hex+i*2,"%02x",hash[i]);hex[64]=0;
    NSString *path=[directory stringByAppendingPathComponent:[[NSString stringWithUTF8String:hex] stringByAppendingString:@".pcm"]];
    return !stat([path fileSystemRepresentation],&info) && info.st_size>=1024?path:nil;
}
void menu_audio_window_close(MenuAudioWindow *w) {if(w->file)ExtAudioFileDispose(w->file);memset(w,0,sizeof(*w));}
int menu_audio_window_open(MenuAudioWindow *w,const char *path) {
    memset(w,0,sizeof(*w));
    NSURL *url=[NSURL fileURLWithPath:[NSString stringWithUTF8String:path]];
    OSStatus error=ExtAudioFileOpenURL((CFURLRef)url,&w->file);if(error){NSLog(@"OSU FFT decoder open failed %ld: %s",(long)error,path);return 0;}
    UInt32 manufacturer=0;
#if TARGET_OS_IPHONE
    manufacturer='appl'; // Prefer the software decoder on iOS 4.
    ExtAudioFileSetProperty(w->file,kExtAudioFileProperty_CodecManufacturer,sizeof(manufacturer),&manufacturer);
#endif
    AudioStreamBasicDescription source;UInt32 bytes=sizeof(source);
    error=ExtAudioFileGetProperty(w->file,kExtAudioFileProperty_FileDataFormat,&bytes,&source);
    SInt64 length=0;bytes=sizeof(length);
    if(error || source.mSampleRate<=0 || ExtAudioFileGetProperty(w->file,kExtAudioFileProperty_FileLengthFrames,&bytes,&length)){NSLog(@"OSU FFT decoder metadata failed %ld: %s",(long)error,path);menu_audio_window_close(w);return 0;}
    w->sourceRate=source.mSampleRate;w->duration=length/w->sourceRate;
    AudioStreamBasicDescription format;memset(&format,0,sizeof(format));format.mSampleRate=11025;format.mFormatID=kAudioFormatLinearPCM;
    format.mFormatFlags=kLinearPCMFormatFlagIsSignedInteger|kLinearPCMFormatFlagIsPacked;format.mBytesPerPacket=format.mBytesPerFrame=2;
    format.mFramesPerPacket=format.mChannelsPerFrame=1;format.mBitsPerChannel=16;
    error=ExtAudioFileSetProperty(w->file,kExtAudioFileProperty_ClientDataFormat,sizeof(format),&format);
    if(error) {
        // Some system MP3 decoders do not advertise the software manufacturer.
        // Let the platform select an available decoder when that hint fails.
        manufacturer=0;ExtAudioFileSetProperty(w->file,kExtAudioFileProperty_CodecManufacturer,sizeof(manufacturer),&manufacturer);
        error=ExtAudioFileSetProperty(w->file,kExtAudioFileProperty_ClientDataFormat,sizeof(format),&format);
    }
    if(error){NSLog(@"OSU FFT decoder PCM format failed %ld: %s",(long)error,path);menu_audio_window_close(w);return 0;}return 1;
}
static int read_frames(MenuAudioWindow *w,int16_t *pcm,UInt32 *count) {
    AudioBufferList list;list.mNumberBuffers=1;list.mBuffers[0].mNumberChannels=1;
    list.mBuffers[0].mData=pcm;list.mBuffers[0].mDataByteSize=*count*2;
    OSStatus error=ExtAudioFileRead(w->file,count,&list);w->decodedFrames+=*count;
    return !error;
}
int menu_audio_window_read(MenuAudioWindow *w,double centre,int16_t *pcm,unsigned count) {
    if(!w->file || !isfinite(centre) || count==0)return 0;
    memset(pcm,0,count*2);
    double start=fmax(0,centre-count/(2.0*11025));
    if(start>=w->duration)return 1;
    double gap=start-w->nextTime;
    if(!w->positioned || gap<-.0001 || gap>.2) {
        if(ExtAudioFileSeek(w->file,(SInt64)llround(start*w->sourceRate)))return 0;
        w->positioned=1;w->nextTime=start;w->seeks++;
    } else {
        // At 20 Hz the normal gap is only ~39 samples. Preserve the converter
        // and discard that tiny gap instead of re-seeking compressed audio.
        unsigned skip=(unsigned)fmax(0,llround(gap*11025));int16_t discard[1024];
        while(skip){UInt32 frames=skip>1024?1024:skip;if(!read_frames(w,discard,&frames))return 0;if(!frames)break;skip-=frames;w->nextTime+=frames/11025.0;}
    }
    UInt32 frames=count;if(!read_frames(w,pcm,&frames))return 0;
    w->nextTime+=frames/11025.0;return 1;
}
