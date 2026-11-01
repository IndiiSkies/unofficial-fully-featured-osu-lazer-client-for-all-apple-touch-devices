#import <Foundation/Foundation.h>
#include <CommonCrypto/CommonDigest.h>
#include <sys/stat.h>
#include <string.h>
#include <stdio.h>
#include <math.h>
#include "AudioDecode.h"
#include "ImportGate.h"
extern int legacy_rate_audio_wav(const char *,const char *,double,int,char *,int);
int legacy_audio_rate_prepare(const char *source,double rate,int pitch,char *output,int capacity){
 if(!source || !output || capacity<1)return 0;if(fabs(rate-1)<.000001){if(strlen(source)>=(unsigned)capacity)return 0;strcpy(output,source);return 1;}
 NSAutoreleasePool *pool=[[NSAutoreleasePool alloc] init];int success=0;struct stat info;if(stat(source,&info))goto done;
 NSString *key=[NSString stringWithFormat:@"%s:%lld:%lld:rate-v1:%.6f:%d",source,(long long)info.st_size,(long long)info.st_mtime,rate,pitch];unsigned char hash[32];CC_SHA256([key UTF8String],(CC_LONG)strlen([key UTF8String]),hash);char hex[65];for(int i=0;i<32;i++)sprintf(hex+i*2,"%02x",hash[i]);hex[64]=0;
 NSString *folder=[[NSSearchPathForDirectoriesInDomains(NSCachesDirectory,NSUserDomainMask,YES) objectAtIndex:0] stringByAppendingPathComponent:@"RateAudio"];[[NSFileManager defaultManager] createDirectoryAtPath:folder withIntermediateDirectories:YES attributes:nil error:NULL];NSString *path=[folder stringByAppendingPathComponent:[[NSString stringWithUTF8String:hex] stringByAppendingString:@".wav"]];
 if(stat([path fileSystemRepresentation],&info) || info.st_size<=44){NSString *stage=[path stringByAppendingString:@".partial"];char error[256];NSLog(@"OSU mods: preparing %.2fx %@ audio",rate,pitch?@"pitched":@"pitch-preserving");if(!legacy_rate_audio_wav(source,[stage fileSystemRepresentation],rate,pitch,error,sizeof(error))){NSLog(@"OSU mods: rate preparation failed: %s",error);goto done;}if(![[NSFileManager defaultManager] moveItemAtPath:stage toPath:path error:NULL])goto done;}
 if(strlen([path fileSystemRepresentation])<(unsigned)capacity){strcpy(output,[path fileSystemRepresentation]);success=1;}
done:[pool drain];return success;
}

// Called only from the song/import worker. A disk cache avoids decoding again
// and keeps all existing time, seek and FFT paths on standard PCM audio.
int legacy_audio_prepare(const char *source,char *output,int capacity) {
    if(!source || !output || capacity<1)return 0;
    legacy_import_checkpoint();
    NSAutoreleasePool *pool=[[NSAutoreleasePool alloc] init];int result=0;
    {
        NSString *path=[NSString stringWithUTF8String:source];
        NSString *chosen=path;
        struct stat info;
        if(stat(source,&info))goto finished;
        NSString *key=[NSString stringWithFormat:@"%@:%lld:%lld:ffmpeg-pcm-v1",path,(long long)info.st_size,(long long)info.st_mtime];
        static NSMutableDictionary *appleSupport;
        BOOL native=NO;
        @synchronized([NSFileManager class]) {
            if(!appleSupport)appleSupport=[[NSMutableDictionary alloc] init];
            native=[[appleSupport objectForKey:key] boolValue];
        }
        if(!native && legacy_apple_audio_supported(source)) {
            native=YES;
            @synchronized([NSFileManager class]) {
                if([appleSupport count]>=2048)[appleSupport removeAllObjects];
                [appleSupport setObject:[NSNumber numberWithBool:YES] forKey:key];
            }
        }
        if(!native) {
            unsigned char hash[CC_SHA256_DIGEST_LENGTH];CC_SHA256([key UTF8String],(CC_LONG)strlen([key UTF8String]),hash);
            char hex[65];for(int i=0;i<32;i++)sprintf(hex+i*2,"%02x",hash[i]);hex[64]=0;
            NSString *directory=[[NSSearchPathForDirectoriesInDomains(NSCachesDirectory,NSUserDomainMask,YES) objectAtIndex:0] stringByAppendingPathComponent:@"DecodedAudio"];
            [[NSFileManager defaultManager] createDirectoryAtPath:directory withIntermediateDirectories:YES attributes:nil error:NULL];
            chosen=[directory stringByAppendingPathComponent:[[NSString stringWithUTF8String:hex] stringByAppendingString:@".wav"]];
            if(stat([chosen fileSystemRepresentation],&info) || info.st_size<=44) {
                NSString *stage=[chosen stringByAppendingFormat:@".partial-%@",[[NSProcessInfo processInfo] globallyUniqueString]];char error[256]={0};
                NSLog(@"OSU FFmpeg: Apple decoder unavailable, decoding %@",[path lastPathComponent]);
                if(!legacy_decode_audio_wav(source,[stage fileSystemRepresentation],error,sizeof(error))){NSLog(@"OSU FFmpeg: failed %@: %s",[path lastPathComponent],error);goto finished;}
                if(![[NSFileManager defaultManager] moveItemAtPath:stage toPath:chosen error:NULL]) {
                    [[NSFileManager defaultManager] removeItemAtPath:stage error:NULL];
                    if(stat([chosen fileSystemRepresentation],&info) || info.st_size<=44)goto finished;
                }
                NSLog(@"OSU FFmpeg: cached %@",[path lastPathComponent]);
            }
        }
        if(strlen([chosen fileSystemRepresentation])<(unsigned)capacity){strcpy(output,[chosen fileSystemRepresentation]);result=1;}
finished:;
    }
    [pool drain];return result;
}
