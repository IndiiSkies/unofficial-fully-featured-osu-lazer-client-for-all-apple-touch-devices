#include <assert.h>
#include <math.h>
#include <stdio.h>
#include <string.h>
#include <time.h>
#include "../Native/MenuSpectrum.h"
static void tone(int16_t *pcm,int bin,double amplitude) {for(int i=0;i<MENU_FFT_SIZE;i++)pcm[i]=(int16_t)(32767*amplitude*sin(2*M_PI*bin*i/MENU_FFT_SIZE));}
int main(void) {
    MenuSpectrum state;assert(menu_spectrum_init(&state));MenuSpectrumFrame frame;int16_t pcm[MENU_FFT_SIZE]={0};int checks=0;
    menu_spectrum_analyse(&state,pcm,&frame);assert(frame.rms==0 && frame.bass==0);checks++;
    for(int i=0;i<MENU_BANDS;i++){assert(frame.bands[i]==0);checks++;}
    for(int bin=2;bin<240;bin+=7) {
        tone(pcm,bin,.5);menu_spectrum_analyse(&state,pcm,&frame);
        assert(fabs(frame.rms-.35355)<.002);checks++;
        int peak=0;for(int i=1;i<MENU_BANDS;i++)if(frame.bands[i]>frame.bands[peak])peak=i;
        // The peak band must contain the input bin or its Hann-window neighbour.
        assert(state.edges[peak]<=bin+1 && state.edges[peak+1]>bin-1);checks++;
        for(int i=0;i<MENU_BANDS;i++){assert(isfinite(frame.bands[i]) && frame.bands[i]>=0 && frame.bands[i]<=1);checks++;}
    }
    tone(pcm,4,.5);menu_spectrum_analyse(&state,pcm,&frame);float bass=frame.bass;assert(bass>.1);checks++;
    tone(pcm,140,.5);menu_spectrum_analyse(&state,pcm,&frame);assert(frame.bass<bass*.01);checks++;
    for(int i=0;i<MENU_FFT_SIZE;i++)pcm[i]=10000;menu_spectrum_analyse(&state,pcm,&frame);assert(frame.rms<1e-6 && frame.bass<1e-6);checks++;
    tone(pcm,4,.05);menu_spectrum_analyse(&state,pcm,&frame);assert(frame.bass<bass*.11 && frame.bass>bass*.09);checks++;
    clock_t begin=clock();for(int i=0;i<20000;i++)menu_spectrum_analyse(&state,pcm,&frame);
    printf("PASS: %d FFT silence, tone-bin, RMS, bass, DC and range checks; host %.1f us/FFT (not an iPod measurement)\n",checks,1e6*(clock()-begin)/CLOCKS_PER_SEC/20000.0);
    menu_spectrum_destroy(&state);
}
