#include <AudioToolbox/AudioToolbox.h>
#include <CoreFoundation/CoreFoundation.h>
#include <stdint.h>
#include <string.h>
#include "AudioDecode.h"

// Opening a file alone only checks its container. Decode a small PCM block
// to establish that Apple's codec can really handle the audio stream.
int legacy_apple_audio_supported(const char *path) {
    if(!path)return 0;
    CFURLRef url=CFURLCreateFromFileSystemRepresentation(NULL,(const UInt8 *)path,strlen(path),false);
    if(!url)return 0;
    int supported=0;
    for(int attempt=0;attempt<2 && !supported;attempt++) {
        ExtAudioFileRef file=NULL;
        OSStatus error=ExtAudioFileOpenURL(url,&file);
        if(error)break;
        if(!attempt){UInt32 manufacturer='appl';ExtAudioFileSetProperty(file,kExtAudioFileProperty_CodecManufacturer,sizeof(manufacturer),&manufacturer);}
        AudioStreamBasicDescription original;UInt32 size=sizeof(original);
        error=ExtAudioFileGetProperty(file,kExtAudioFileProperty_FileDataFormat,&size,&original);
        if(!error && original.mSampleRate>0) {
            AudioStreamBasicDescription pcm;memset(&pcm,0,sizeof(pcm));
            pcm.mSampleRate=original.mSampleRate;pcm.mFormatID=kAudioFormatLinearPCM;
            pcm.mFormatFlags=kLinearPCMFormatFlagIsSignedInteger|kLinearPCMFormatFlagIsPacked;
            pcm.mChannelsPerFrame=original.mChannelsPerFrame==1?1:2;
            pcm.mBitsPerChannel=16;pcm.mFramesPerPacket=1;
            pcm.mBytesPerFrame=pcm.mBytesPerPacket=pcm.mChannelsPerFrame*2;
            error=ExtAudioFileSetProperty(file,kExtAudioFileProperty_ClientDataFormat,sizeof(pcm),&pcm);
            if(!error) {
                int16_t samples[512];UInt32 frames=256;AudioBufferList buffers;
                buffers.mNumberBuffers=1;buffers.mBuffers[0].mNumberChannels=pcm.mChannelsPerFrame;
                buffers.mBuffers[0].mData=samples;buffers.mBuffers[0].mDataByteSize=sizeof(samples);
                error=ExtAudioFileRead(file,&frames,&buffers);
                supported=!error && frames>0;
            }
        }
        ExtAudioFileDispose(file);
    }
    CFRelease(url);return supported;
}
