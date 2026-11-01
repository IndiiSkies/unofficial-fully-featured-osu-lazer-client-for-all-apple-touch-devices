#ifndef LEGACY_MENU_AUDIO_WINDOW_H
#define LEGACY_MENU_AUDIO_WINDOW_H
#include <AudioToolbox/AudioToolbox.h>
#include <stdint.h>
typedef struct {
    ExtAudioFileRef file;
    double sourceRate,duration,nextTime;
    int positioned;
    unsigned long long decodedFrames;
    unsigned seeks;
} MenuAudioWindow;
int menu_audio_window_open(MenuAudioWindow *window,const char *path);
int menu_audio_window_read(MenuAudioWindow *window,double centre,int16_t *pcm,unsigned count);
void menu_audio_window_close(MenuAudioWindow *window);
#endif
