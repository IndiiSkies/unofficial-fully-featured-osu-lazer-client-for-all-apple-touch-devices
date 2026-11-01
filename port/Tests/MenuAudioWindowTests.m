#import <Foundation/Foundation.h>
#include <assert.h>
#include <math.h>
#include <stdio.h>
#include "../Native/MenuAudioWindow.h"
#include "../Native/MenuSpectrum.h"
int main(int argc,char **argv) {
    NSAutoreleasePool *pool=[[NSAutoreleasePool alloc] init];assert(argc>1);MenuSpectrum fft;assert(menu_spectrum_init(&fft));int checks=0;
    for(int file=1;file<argc;file++) {
        fprintf(stderr,"Testing %s\n",argv[file]);
        MenuAudioWindow stream;assert(menu_audio_window_open(&stream,argv[file]));checks++;
        int16_t pcm[MENU_FFT_SIZE];MenuSpectrumFrame result;
        for(int i=0;i<20;i++) {
            assert(menu_audio_window_read(&stream,.025+i*.05,pcm,MENU_FFT_SIZE));checks++;
            menu_spectrum_analyse(&fft,pcm,&result);
            if(i<3)fprintf(stderr,"window %d RMS %.4f\n",i,result.rms);
            assert(isfinite(result.rms) && result.rms>=0 && result.rms<.45);
            if(i>=3)assert(result.rms>.25);checks++;
        }
        assert(stream.decodedFrames<12000 && stream.seeks<=2);checks++;
        assert(menu_audio_window_read(&stream,6,pcm,MENU_FFT_SIZE));menu_spectrum_analyse(&fft,pcm,&result);checks++;
        int peak=0;for(int i=1;i<MENU_BANDS;i++)if(result.bands[i]>result.bands[peak])peak=i;
        assert(fft.edges[peak]<=49 && fft.edges[peak+1]>47);checks++;
        assert(menu_audio_window_read(&stream,1,pcm,MENU_FFT_SIZE));menu_spectrum_analyse(&fft,pcm,&result);checks++;
        peak=0;for(int i=1;i<MENU_BANDS;i++)if(result.bands[i]>result.bands[peak])peak=i;
        assert(fft.edges[peak]<=9 && fft.edges[peak+1]>7);checks++;
        assert(menu_audio_window_read(&stream,stream.duration+1,pcm,MENU_FFT_SIZE));menu_spectrum_analyse(&fft,pcm,&result);assert(result.rms==0);checks++;
        menu_audio_window_close(&stream);assert(!stream.file);checks++;
    }
    menu_spectrum_destroy(&fft);printf("PASS: %d streamed-window decode, bounded work, sequential reuse, seek and EOF checks\n",checks);[pool drain];return 0;
}
