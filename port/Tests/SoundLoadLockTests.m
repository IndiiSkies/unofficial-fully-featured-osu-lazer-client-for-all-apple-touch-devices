#import <Foundation/Foundation.h>
#import <AudioToolbox/AudioToolbox.h>
#import <OpenAL/al.h>
#import <OpenAL/alc.h>
#include <pthread.h>
#include <assert.h>
#include <stdio.h>
static pthread_mutex_t state=PTHREAD_MUTEX_INITIALIZER;
static pthread_cond_t changed=PTHREAD_COND_INITIALIZER;
static int reading,releaseRead,reads,loaded;
static int processCalls,suspendCalls,stopCalls;static float restoredGain;
static ALCboolean testCurrent(ALCcontext *value){(void)value;return ALC_TRUE;}
static void testSuspend(ALCcontext *value){(void)value;suspendCalls++;}
static void testProcess(ALCcontext *value){(void)value;processCalls++;}
static void testStop(ALsizei count,const ALuint *values){(void)count;(void)values;stopCalls++;}
static void testListener(ALenum parameter,ALfloat gain){(void)parameter;restoredGain=gain;}
static ALenum testError(void){return AL_NO_ERROR;}
static ALCenum testContextError(ALCdevice *value){(void)value;return ALC_NO_ERROR;}
int legacy_audio_prepare(const char *source,char *output,int capacity){(void)source;(void)output;(void)capacity;return 0;}
static OSStatus testOpen(CFURLRef url,ExtAudioFileRef *file){(void)url;*file=(ExtAudioFileRef)1;return 0;}
static OSStatus testProperty(ExtAudioFileRef file,ExtAudioFilePropertyID key,UInt32 size,const void *value){(void)file;(void)key;(void)size;(void)value;return 0;}
static OSStatus testRead(ExtAudioFileRef file,UInt32 *frames,AudioBufferList *list){
    (void)file;(void)list;
    pthread_mutex_lock(&state);reading=1;pthread_cond_signal(&changed);
    while(!releaseRead)pthread_cond_wait(&changed,&state);
    *frames=reads++?0:1;pthread_mutex_unlock(&state);return 0;
}
static OSStatus testDispose(ExtAudioFileRef file){(void)file;return 0;}
#define ExtAudioFileOpenURL testOpen
#define ExtAudioFileSetProperty testProperty
#define ExtAudioFileRead testRead
#define ExtAudioFileDispose testDispose
#define alcMakeContextCurrent testCurrent
#define alcSuspendContext testSuspend
#define alcProcessContext testProcess
#define alSourceStopv testStop
#define alListenerf testListener
#define alGetError testError
#define alcGetError testContextError
#include "../Native/Sound.m"
static void *load(void *unused){(void)unused;loaded=legacy_sound_load("/fixture.wav");return NULL;}
int main(void){
    NSAutoreleasePool *pool=[[NSAutoreleasePool alloc] init];pthread_t worker;
    assert(!pthread_create(&worker,NULL,load,NULL));
    pthread_mutex_lock(&state);while(!reading)pthread_cond_wait(&changed,&state);pthread_mutex_unlock(&state);
    // Both require the playback lock. They must complete while decoding is
    // blocked, and reset must invalidate that in-flight sample publication.
    legacy_sound_volume(.5);legacy_sound_reset();
    pthread_mutex_lock(&state);releaseRead=1;pthread_cond_signal(&changed);pthread_mutex_unlock(&state);
    pthread_join(worker,NULL);assert(!loaded);assert(!buffer_count);assert(!pcm_bytes);assert(!context);
    context=(ALCcontext *)1;device=(ALCdevice *)1;volume=.37;buffer_count=3;
    legacy_sound_suspend();assert(suspended && suspendCalls==1 && stopCalls==1);
    legacy_sound_play_pitched(1,1,1);assert(!processCalls);
    legacy_sound_resume();assert(!suspended && processCalls==1 && buffer_count==3 && restoredGain>.369 && restoredGain<.371);
    context=NULL;device=NULL;buffer_count=0;
    puts("PASS: blocked sample decoding permits playback-state access; reset rejects stale publication");
    puts("PASS: OpenAL interruption resume processes context, restores effects gain and preserves loaded samples");
    [pool drain];return 0;
}
