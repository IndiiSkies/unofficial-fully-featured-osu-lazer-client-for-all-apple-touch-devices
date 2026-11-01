#ifndef LEGACY_MENU_SPECTRUM_H
#define LEGACY_MENU_SPECTRUM_H
#include <stdint.h>
#include <Accelerate/Accelerate.h>
#define MENU_FFT_SIZE 512
#define MENU_BANDS 32
#define MENU_PCM_RATE 11025
// One setup and fixed buffers for the lifetime of the analysis thread.
typedef struct {
    FFTSetup setup;
    float window[MENU_FFT_SIZE] __attribute__((aligned(16)));
    float input[MENU_FFT_SIZE];
    float real[MENU_FFT_SIZE/2],imag[MENU_FFT_SIZE/2],power[MENU_FFT_SIZE/2];
    unsigned edges[MENU_BANDS+1];
} MenuSpectrum;
typedef struct { float bands[MENU_BANDS],rms,bass; double time; } MenuSpectrumFrame;
int menu_spectrum_init(MenuSpectrum *state);
void menu_spectrum_destroy(MenuSpectrum *state);
void menu_spectrum_analyse(MenuSpectrum *state,const int16_t *pcm,MenuSpectrumFrame *result);
#endif
