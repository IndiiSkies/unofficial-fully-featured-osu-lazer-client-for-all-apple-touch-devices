#include "../Native/AudioDecode.h"
#include <stdio.h>
#include <string.h>
int main(int argc,char **argv) {
    char error[256]={0};if(argc!=3)return 2;
    if(!strcmp(argv[1],"--apple"))return legacy_apple_audio_supported(argv[2])?0:1;
    if(!legacy_decode_audio_wav(argv[1],argv[2],error,sizeof(error))){fprintf(stderr,"%s\n",error);return 1;}
    return 0;
}
