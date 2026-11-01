#ifndef OSU_AUDIO_DECODE_H
#define OSU_AUDIO_DECODE_H
int legacy_decode_audio_wav(const char *source,const char *destination,char *error,int capacity);
int legacy_audio_prepare(const char *source,char *output,int capacity);
int legacy_apple_audio_supported(const char *source);
#endif
