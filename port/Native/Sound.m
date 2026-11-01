#import <Foundation/Foundation.h>
#import <AudioToolbox/AudioToolbox.h>
#import <OpenAL/al.h>
#import <OpenAL/alc.h>
#include <stdlib.h>
#include <string.h>
#include "AudioDecode.h"

// 16 overlapping one-shots and four independent sustained effects. All file
// decoding happens during song preparation; playback only submits AL commands.
#define VOICES 20
#define MAX_BUFFERS 128
#define PCM_LIMIT (8 * 1024 * 1024)
static ALCdevice *device;
static ALCcontext *context;
static ALuint sources[VOICES], buffers[MAX_BUFFERS];
static int buffer_count, next_voice;
static unsigned pcm_bytes;
static unsigned load_generation;
static float volume = .8f;static double mod_pitch=1,mod_gain=1;void legacy_sound_mod_gain(double gain){mod_gain=gain;}void legacy_sound_mod_pitch(double pitch){mod_pitch=pitch;}
static int loop_buffers[4];
static int suspended,play_reports;
static double last_play_error;
static int ready(void) {
    if (!context) {
        device = alcOpenDevice(NULL);
        if (!device) return 0;
        context = alcCreateContext(device, NULL);
        if (!context) { alcCloseDevice(device); device = NULL; return 0; }
        if(!alcMakeContextCurrent(context)){alcDestroyContext(context);alcCloseDevice(device);context=NULL;device=NULL;NSLog(@"OSU sounds: cannot bind OpenAL context");return 0;}
        for(int clear=0;clear<16 && alGetError()!=AL_NO_ERROR;clear++){}
        alGenSources(VOICES, sources);
        ALenum error=alGetError();
        if (error != AL_NO_ERROR) { NSLog(@"OSU sounds: source allocation failed 0x%x",error);alcMakeContextCurrent(NULL);alcDestroyContext(context);alcCloseDevice(device);context=NULL;device=NULL;memset(sources,0,sizeof(sources));return 0; }
        alListenerf(AL_GAIN, volume);
        NSLog(@"OSU sounds: OpenAL ready, %d voices, effects gain %.3f",VOICES,volume);
    }
    return alcGetCurrentContext()==context || alcMakeContextCurrent(context) == ALC_TRUE;
}
int legacy_sound_load(const char *path) {
    NSAutoreleasePool *pool = [[NSAutoreleasePool alloc] init];
    unsigned generation;
    @synchronized([NSProcessInfo class]) {generation=load_generation;}
    char preparedPath[4096];
    if(legacy_audio_prepare(path,preparedPath,sizeof(preparedPath)))path=preparedPath;
    int result = 0;
    // File I/O and sample-rate conversion must not hold the playback lock.
    // Only OpenAL context/buffer publication is serialized with UI sounds.
    {
            ExtAudioFileRef file = NULL;
            NSURL *url = [NSURL fileURLWithPath:[NSString stringWithUTF8String:path]];
            OSStatus error = ExtAudioFileOpenURL((CFURLRef)url, &file);
            if (!error) {
                AudioStreamBasicDescription format; memset(&format, 0, sizeof(format));
                format.mSampleRate = 22050; format.mFormatID = kAudioFormatLinearPCM;
                format.mFormatFlags = kLinearPCMFormatFlagIsSignedInteger | kLinearPCMFormatFlagIsPacked;
                format.mBytesPerPacket = format.mBytesPerFrame = 2;
                format.mFramesPerPacket = 1; format.mChannelsPerFrame = 1; format.mBitsPerChannel = 16;
                error = ExtAudioFileSetProperty(file, kExtAudioFileProperty_ClientDataFormat, sizeof(format), &format);
                // Eight seconds is ample for hit/loop samples. Oversized or
                // unsupported custom samples fall back to the bundled effect.
                unsigned capacity = 22050 * 8, total = 0;
                short *pcm = malloc((capacity + 1) * 2);
                if (pcm && !error) {
                    while (total <= capacity) {
                        UInt32 frames = capacity + 1 - total;
                        AudioBufferList list; list.mNumberBuffers = 1;
                        list.mBuffers[0].mNumberChannels = 1; list.mBuffers[0].mDataByteSize = frames * 2; list.mBuffers[0].mData = pcm + total;
                        error = ExtAudioFileRead(file, &frames, &list);
                        if (error || !frames) break;
                        total += frames;
                    }
                    if (!error && total && total <= capacity) {
                      @synchronized([NSProcessInfo class]) {
                       if(generation==load_generation && buffer_count<MAX_BUFFERS && pcm_bytes+total*2<=PCM_LIMIT && ready()) {
                        for(int clear=0;clear<16 && alGetError()!=AL_NO_ERROR;clear++){}
                        ALuint buffer; alGenBuffers(1, &buffer);
                        alBufferData(buffer, AL_FORMAT_MONO16, pcm, total * 2, 22050);
                        if (alGetError() == AL_NO_ERROR) {
                            buffers[buffer_count++] = buffer; pcm_bytes += total * 2; result = (int)buffer;
                            if(buffer_count<=3)NSLog(@"OSU sounds: loaded %@ buffer %u, %u PCM bytes",[url lastPathComponent],buffer,total*2);
                        } else {NSLog(@"OSU sounds: buffer upload failed %@",[url lastPathComponent]);alDeleteBuffers(1, &buffer);}
                       }
                      }
                    }
                }
                free(pcm); ExtAudioFileDispose(file);
            }
            if (!result) NSLog(@"OSU sounds: using fallback for %@ (decoder %ld)", [url lastPathComponent], (long)error);
    }
    [pool drain]; return result;
}
static void play_internal(int buffer,double gain,double pitch,int metronome) {
    if(!metronome)gain*=mod_gain;
    if (!buffer || suspended) return;
    @synchronized([NSProcessInfo class]) {
        if (!ready()) return;
        int slot = next_voice;
        for (int i = 0; i < 16; i++) { int candidate = (next_voice + i) % 16; ALint state; alGetSourcei(sources[candidate], AL_SOURCE_STATE, &state); if (state != AL_PLAYING) { slot = candidate; break; } }
        next_voice = (slot + 1) % 16;
        ALuint source = sources[slot]; alSourceStop(source);
        alSourcei(source, AL_BUFFER, buffer); alSourcei(source, AL_LOOPING, AL_FALSE);
        alSourcef(source, AL_GAIN, (ALfloat)gain);alSourcef(source,AL_PITCH,(ALfloat)(pitch*mod_pitch));alSourcePlay(source);
        ALenum error=alGetError();
        if(play_reports<8 || error!=AL_NO_ERROR){double now=[NSDate timeIntervalSinceReferenceDate];if(play_reports<8 || now-last_play_error>=2){ALint state=0;alGetSourcei(source,AL_SOURCE_STATE,&state);NSLog(@"OSU sounds: play buffer %d source %u state 0x%x gain %.3f x %.3f error 0x%x",buffer,source,state,gain,volume,error);play_reports++;if(error!=AL_NO_ERROR)last_play_error=now;}}
    }
}
void legacy_sound_play_pitched(int buffer,double gain,double pitch){play_internal(buffer,gain,pitch,0);}
void legacy_sound_metronome(int buffer,double gain,double pitch){play_internal(buffer,gain,pitch,1);}
void legacy_sound_play(int buffer,double gain){legacy_sound_play_pitched(buffer,gain,1);}
void legacy_sound_loop(int slot, int buffer, double gain) {
    gain*=mod_gain;
    if(suspended)return;
    if (slot < 0 || slot >= 4) return;
    if(!buffer && !loop_buffers[slot])return;
    @synchronized([NSProcessInfo class]) {
        if (!ready()) return;
        ALuint source = sources[16 + slot]; ALint previous, state;
        alGetSourcei(source, AL_BUFFER, &previous); alGetSourcei(source, AL_SOURCE_STATE, &state);
        if (!buffer) { if (state == AL_PLAYING) alSourceStop(source);loop_buffers[slot]=0;return; }
        if (previous != buffer || state != AL_PLAYING) {
            alSourceStop(source); alSourcei(source, AL_BUFFER, buffer); alSourcei(source, AL_LOOPING, AL_TRUE);
            alSourcef(source,AL_PITCH,(ALfloat)mod_pitch);alSourcef(source, AL_GAIN, (ALfloat)gain); alSourcePlay(source);
        } else alSourcef(source, AL_GAIN, (ALfloat)gain);
        loop_buffers[slot]=buffer;
    }
}
void legacy_sound_volume(double gain) {
    @synchronized([NSProcessInfo class]) { volume = (float)gain; if (context && ready()) alListenerf(AL_GAIN, volume); }
}
void legacy_sound_stop(void) {
    @synchronized([NSProcessInfo class]) { if (context && ready()) alSourceStopv(VOICES, sources);memset(loop_buffers,0,sizeof(loop_buffers)); }
}
void legacy_sound_suspend(void){
    @synchronized([NSProcessInfo class]){if(suspended)return;suspended=1;if(context){alcMakeContextCurrent(context);alSourceStopv(VOICES,sources);memset(loop_buffers,0,sizeof(loop_buffers));alcSuspendContext(context);}}
}
void legacy_sound_resume(void){
    @synchronized([NSProcessInfo class]){
        suspended=0;play_reports=0;if(!context)return;
        if(!alcMakeContextCurrent(context)){NSLog(@"OSU sounds: context resume bind failed");return;}
        alcProcessContext(context);alListenerf(AL_GAIN,volume);
        NSLog(@"OSU sounds: resumed context, gain %.3f, %d samples, AL error 0x%x / ALC error 0x%x",volume,buffer_count,alGetError(),alcGetError(device));
    }
}
void legacy_sound_reset(void) {
    @synchronized([NSProcessInfo class]) {
        load_generation++;
        if (context && ready()) { alSourceStopv(VOICES, sources); for (int i = 0; i < VOICES; i++) alSourcei(sources[i], AL_BUFFER, 0); alDeleteBuffers(buffer_count, buffers); }
        buffer_count = 0; pcm_bytes = 0;
        memset(loop_buffers,0,sizeof(loop_buffers));
    }
}
