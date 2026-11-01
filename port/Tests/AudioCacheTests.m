#import <Foundation/Foundation.h>
#include <pthread.h>
#include <assert.h>
#include <stdio.h>
#include <string.h>
#include <stdlib.h>
#include "../Native/AudioDecode.h"
static NSString *testRoot;
static pthread_mutex_t state=PTHREAD_MUTEX_INITIALIZER;
static pthread_cond_t changed=PTHREAD_COND_INITIALIZER;
static int decoding,releaseDecode;
static char slowPath[4096];
static NSArray *testDirectories(NSSearchPathDirectory directory,NSSearchPathDomainMask domains,BOOL expand){
    (void)directory;(void)domains;(void)expand;return [NSArray arrayWithObject:testRoot];
}
int legacy_apple_audio_supported(const char *path){return strstr(path,".wav")!=NULL;}
int legacy_rate_audio_wav(const char *source,const char *destination,double rate,int pitch,char *error,int capacity){(void)source;(void)destination;(void)rate;(void)pitch;(void)error;(void)capacity;return 0;}
int legacy_decode_audio_wav(const char *source,const char *destination,char *error,int capacity){
    (void)source;(void)error;(void)capacity;
    pthread_mutex_lock(&state);decoding=1;pthread_cond_signal(&changed);while(!releaseDecode)pthread_cond_wait(&changed,&state);pthread_mutex_unlock(&state);
    FILE *file=fopen(destination,"wb");if(!file)return 0;char bytes[48]={0};fwrite(bytes,1,sizeof(bytes),file);fclose(file);return 1;
}
#define NSSearchPathForDirectoriesInDomains testDirectories
#include "../Native/AudioCache.m"
#undef NSSearchPathForDirectoriesInDomains
static void *prepareSlow(void *unused){(void)unused;char output[4096];assert(legacy_audio_prepare(slowPath,output,sizeof(output)));return NULL;}
int main(void){
    NSAutoreleasePool *pool=[[NSAutoreleasePool alloc] init];
    char folder[]="/private/tmp/osu-audio-cache-test-XXXXXX";assert(mkdtemp(folder));testRoot=[NSString stringWithUTF8String:folder];
    NSString *slow=[testRoot stringByAppendingPathComponent:@"song.ogg"],*fast=[testRoot stringByAppendingPathComponent:@"button.wav"];
    [@"fixture" writeToFile:slow atomically:YES encoding:NSUTF8StringEncoding error:NULL];[@"fixture" writeToFile:fast atomically:YES encoding:NSUTF8StringEncoding error:NULL];strcpy(slowPath,[slow fileSystemRepresentation]);
    pthread_t thread;assert(!pthread_create(&thread,NULL,prepareSlow,NULL));
    pthread_mutex_lock(&state);while(!decoding)pthread_cond_wait(&changed,&state);pthread_mutex_unlock(&state);
    char output[4096];assert(legacy_audio_prepare([fast fileSystemRepresentation],output,sizeof(output)));assert(!strcmp(output,[fast fileSystemRepresentation]));
    pthread_mutex_lock(&state);releaseDecode=1;pthread_cond_signal(&changed);pthread_mutex_unlock(&state);pthread_join(thread,NULL);
    [[NSFileManager defaultManager] removeItemAtPath:testRoot error:NULL];
    puts("PASS: unrelated native audio prepares while another song's FFmpeg conversion is blocked");[pool drain];return 0;
}
