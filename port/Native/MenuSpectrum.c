#include "MenuSpectrum.h"
#include <math.h>
#include <string.h>
int menu_spectrum_init(MenuSpectrum *s) {
    memset(s,0,sizeof(*s));s->setup=vDSP_create_fftsetup(9,kFFTRadix2);if(!s->setup)return 0;
    vDSP_hann_window(s->window,MENU_FFT_SIZE,0);
    s->edges[0]=1;
    for(int i=1;i<=MENU_BANDS;i++) {
        unsigned edge=(unsigned)lround(pow(256.0,(double)i/MENU_BANDS));
        if(edge<=s->edges[i-1])edge=s->edges[i-1]+1;
        s->edges[i]=edge>256?256:edge;
    }
    return 1;
}
void menu_spectrum_destroy(MenuSpectrum *s) {if(s->setup)vDSP_destroy_fftsetup(s->setup);s->setup=0;}
void menu_spectrum_analyse(MenuSpectrum *s,const int16_t *pcm,MenuSpectrumFrame *out) {
    const float scale=1.0f/32768;float mean;
    // The iOS 4 header predates const-correct vDSP declarations.
    vDSP_vflt16((short *)pcm,1,s->input,1,MENU_FFT_SIZE);
    vDSP_vsmul(s->input,1,&scale,s->input,1,MENU_FFT_SIZE);
    vDSP_meanv(s->input,1,&mean,MENU_FFT_SIZE);mean=-mean;
    vDSP_vsadd(s->input,1,&mean,s->input,1,MENU_FFT_SIZE);
    vDSP_rmsqv(s->input,1,&out->rms,MENU_FFT_SIZE);
    vDSP_vmul(s->input,1,s->window,1,s->input,1,MENU_FFT_SIZE);
    DSPSplitComplex split={s->real,s->imag};
    vDSP_ctoz((const DSPComplex *)s->input,2,&split,1,MENU_FFT_SIZE/2);
    vDSP_fft_zrip(s->setup,&split,1,9,FFT_FORWARD);
    split.realp[0]=split.imagp[0]=0;
    vDSP_zvmags(&split,1,s->power,1,MENU_FFT_SIZE/2);
    float sum=0;vDSP_sve(s->power+1,1,&sum,12);
    out->bass=sqrtf(sum)/MENU_FFT_SIZE;
    for(int i=0;i<MENU_BANDS;i++) {
        sum=0;unsigned count=s->edges[i+1]-s->edges[i];
        if(count)vDSP_sve(s->power+s->edges[i],1,&sum,count);
        float energy=sqrtf(sum)/MENU_FFT_SIZE;
        out->bands[i]=fminf(1,log1pf(24*energy)/logf(25));
    }
}
