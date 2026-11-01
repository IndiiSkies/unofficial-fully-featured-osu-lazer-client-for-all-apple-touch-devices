#include <stdio.h>
int legacy_rate_audio_wav(const char *,const char *,double,int,char *,int);
int main(int argc,char **argv){char error[256];if(argc!=5)return 2;double rate;int pitch;if(sscanf(argv[3],"%lf",&rate)!=1 || sscanf(argv[4],"%d",&pitch)!=1)return 2;if(!legacy_rate_audio_wav(argv[1],argv[2],rate,pitch,error,sizeof(error))){fprintf(stderr,"%s\n",error);return 1;}return 0;}
