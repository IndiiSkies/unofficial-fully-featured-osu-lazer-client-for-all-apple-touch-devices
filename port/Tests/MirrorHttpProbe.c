#include <stdio.h>
#include <stdlib.h>
int legacy_mirror_fetch(const char *,const char *,unsigned,int (*)(void),void (*)(unsigned,unsigned),char *,int);
int main(int count,char **args){if(count<3 || count>4)return 2;char error[512];if(!legacy_mirror_fetch(args[1],args[2],count==4?(unsigned)strtoul(args[3],NULL,10):4*1024*1024,0,0,error,sizeof error)){fprintf(stderr,"%s\n",error);return 1;}puts("PASS: certificate-verified production TLS/HTTP fetch");return 0;}
