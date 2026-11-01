#include <bearssl.h>
#include <sys/socket.h>
#include <sys/select.h>
#include <sys/time.h>
#include <netdb.h>
#include <unistd.h>
#include <fcntl.h>
#include <errno.h>
#include <stdio.h>
#include <stdlib.h>
#include <stdint.h>
#include <string.h>
#include <strings.h>
#include <time.h>
#include "ImportGate.h"
#include "BrowserTrust.h"
#ifndef MIRROR_TRUST_COUNT
#define MIRROR_TRUST_COUNT TAs_NUM
#endif
#define MIRROR_HOST "mirror.hinamizawa.ai"
typedef int (*MirrorCancel)(void);
typedef void (*MirrorProgress)(unsigned,unsigned);
typedef struct {int fd;MirrorCancel cancel;time_t began;unsigned maximumSeconds;br_ssl_client_context ssl;br_x509_minimal_context x509;br_sslio_context io;unsigned char tls[BR_SSL_BUFSIZE_BIDI],buffer[4096];size_t pos,len;} MirrorConnection;
static int stopped(MirrorConnection *c){return (c->cancel && c->cancel()) || time(NULL)-c->began>c->maximumSeconds;}
static int net_read(void *context,unsigned char *data,size_t length){MirrorConnection *c=context;if(stopped(c))return -1;legacy_import_checkpoint();int n;do{n=(int)recv(c->fd,data,length,0);}while(n<0 && errno==EINTR && !stopped(c));return n<=0?-1:n;}
static int net_write(void *context,const unsigned char *data,size_t length){MirrorConnection *c=context;if(stopped(c))return -1;int n;do{n=(int)send(c->fd,data,length,0);}while(n<0 && errno==EINTR && !stopped(c));return n<=0?-1:n;}
static int connect_mirror(MirrorConnection *c){struct addrinfo hint,*list=NULL;memset(&hint,0,sizeof hint);hint.ai_family=AF_UNSPEC;hint.ai_socktype=SOCK_STREAM;if(getaddrinfo(MIRROR_HOST,"443",&hint,&list))return 0;
 for(struct addrinfo *p=list;p && !stopped(c);p=p->ai_next){int fd=socket(p->ai_family,p->ai_socktype,p->ai_protocol);if(fd<0)continue;if(fd>=FD_SETSIZE){close(fd);continue;}int one=1;setsockopt(fd,SOL_SOCKET,SO_NOSIGPIPE,&one,sizeof one);fcntl(fd,F_SETFL,O_NONBLOCK);int result=connect(fd,p->ai_addr,p->ai_addrlen);if(result<0 && errno==EINPROGRESS){for(int retry=0;retry<10 && !stopped(c);retry++){fd_set write;FD_ZERO(&write);FD_SET(fd,&write);struct timeval wait={1,0};result=select(fd+1,NULL,&write,NULL,&wait);if(result>0){int error=0;socklen_t size=sizeof error;getsockopt(fd,SOL_SOCKET,SO_ERROR,&error,&size);result=error?-1:0;break;}result=-1;}}
  if(result==0){fcntl(fd,F_SETFL,0);struct timeval wait={10,0};setsockopt(fd,SOL_SOCKET,SO_RCVTIMEO,&wait,sizeof wait);setsockopt(fd,SOL_SOCKET,SO_SNDTIMEO,&wait,sizeof wait);c->fd=fd;freeaddrinfo(list);return 1;}close(fd);
 }freeaddrinfo(list);return 0;
}
static int byte(MirrorConnection *c){if(c->pos==c->len){int n=br_sslio_read(&c->io,c->buffer,sizeof c->buffer);if(n<=0)return -1;c->pos=0;c->len=n;}return c->buffer[c->pos++];}
static int line(MirrorConnection *c,char *text,size_t capacity){size_t i=0;while(i+1<capacity){int b=byte(c);if(b<0)return -1;if(b=='\n'){if(i && text[i-1]=='\r')i--;text[i]=0;return (int)i;}text[i++]=(char)b;}return -1;}
static int write_body(MirrorConnection *c,FILE *file,uint64_t count,unsigned maximum,unsigned *written,MirrorProgress progress,unsigned total){unsigned char data[4096];while(count){if(stopped(c))return 0;size_t n=count<sizeof data?(size_t)count:sizeof data;for(size_t i=0;i<n;i++){int b=byte(c);if(b<0)return 0;data[i]=(unsigned char)b;}if(n>maximum-*written || fwrite(data,1,n,file)!=n)return 0;*written+=(unsigned)n;count-=n;if(progress)progress(*written,total);}return 1;}
int legacy_mirror_fetch(const char *path,const char *destination,unsigned maximum,MirrorCancel cancel,MirrorProgress progress,char *error,int errorSize){
 int success=0,status=0;FILE *file=NULL;unsigned written=0;MirrorConnection *c=NULL;const char *reason="Network request failed";
 if(!path || path[0]!='/' || strlen(path)>4096 || !destination || !maximum){reason="Invalid mirror request";goto finish;}for(const char *p=path;*p;p++)if((unsigned char)*p<33 || (unsigned char)*p>126){reason="Invalid mirror path";goto finish;}
 c=calloc(1,sizeof *c);if(!c){reason="Not enough memory for HTTPS";goto finish;}c->fd=-1;c->cancel=cancel;c->began=time(NULL);c->maximumSeconds=maximum>8*1024*1024?900:60;
 if(!connect_mirror(c)){reason="Could not connect to Hinamizawa. Check Wi-Fi and retry.";goto finish;}
 br_ssl_client_init_full(&c->ssl,&c->x509,TAs,MIRROR_TRUST_COUNT);br_ssl_engine_set_versions(&c->ssl.eng,BR_TLS12,BR_TLS12);br_ssl_engine_set_buffer(&c->ssl.eng,c->tls,sizeof c->tls,1);if(!br_ssl_client_reset(&c->ssl,MIRROR_HOST,0)){reason="TLS setup failed";goto finish;}br_sslio_init(&c->io,&c->ssl.eng,net_read,c,net_write,c);
 char request[4600];int length=snprintf(request,sizeof request,"GET %s HTTP/1.1\r\nHost: " MIRROR_HOST "\r\nUser-Agent: OsuLegacy/0.118 (ARMv6; iOS 4.2.1; Hinamizawa browser)\r\nAccept: */*\r\nAccept-Encoding: identity\r\nConnection: close\r\n\r\n",path);
 if(length<0 || length>=(int)sizeof request || br_sslio_write_all(&c->io,request,length)<0 || br_sslio_flush(&c->io)<0){reason="TLS connection failed; check the iPod date and Wi-Fi";goto finish;}
 char header[8192];int n=line(c,header,sizeof header);if(n<0 || sscanf(header,"HTTP/1.%*d %d",&status)!=1){reason="Invalid HTTP response";goto finish;}
 int chunked=0,compressed=0;long long contentLength=-1;unsigned headerBytes=n;char retryAfter[40]="";
 while((n=line(c,header,sizeof header))>0){headerBytes+=n;if(headerBytes>32768){reason="Response headers too large";goto finish;}char *colon=strchr(header,':');if(!colon)continue;*colon++=0;while(*colon==' ' || *colon=='\t')colon++;if(!strcasecmp(header,"Content-Length")){char *end;contentLength=strtoll(colon,&end,10);if(*end || contentLength<0){reason="Invalid response length";goto finish;}}if(!strcasecmp(header,"Transfer-Encoding"))chunked=!strcasecmp(colon,"chunked");if(!strcasecmp(header,"Content-Encoding") && strcasecmp(colon,"identity"))compressed=1;if(!strcasecmp(header,"Retry-After"))snprintf(retryAfter,sizeof retryAfter,"%.30s",colon);}
 if(n<0){reason="Incomplete response headers";goto finish;}if(status!=200){if(error && errorSize>0)snprintf(error,errorSize,"Hinamizawa HTTP %d%s%s",status,retryAfter[0]?"; retry after ":"",retryAfter);reason=NULL;goto finish;}if(compressed){reason="Unexpected compressed response";goto finish;}if(contentLength>(long long)maximum){reason="Response exceeds device download limit";goto finish;}
 file=fopen(destination,"wb");if(!file){reason="Could not create download; check available storage";goto finish;}
 if(chunked){while(1){n=line(c,header,sizeof header);if(n<1){reason="Invalid chunked response";goto finish;}char *end;errno=0;unsigned long long size=strtoull(header,&end,16);if(errno || (*end && *end!=';') || size>maximum-written){reason="Download chunk exceeds limit";goto finish;}if(!size){unsigned trailers=0;while((n=line(c,header,sizeof header))>0){trailers+=n;if(trailers>32768){reason="Response trailers too large";goto finish;}}if(n<0){reason="Incomplete response trailers";goto finish;}break;}if(!write_body(c,file,size,maximum,&written,progress,contentLength>0?(unsigned)contentLength:0) || byte(c)!='\r' || byte(c)!='\n'){reason="Download interrupted or storage full";goto finish;}}}
 else if(contentLength>=0){if(!write_body(c,file,contentLength,maximum,&written,progress,(unsigned)contentLength)){reason="Download interrupted or storage full";goto finish;}}
 else {unsigned char data[4096];while(1){size_t size=0;while(size<sizeof data){int b=byte(c);if(b<0)break;data[size++]=(unsigned char)b;}if(size){if(size>maximum-written || fwrite(data,1,size,file)!=size){reason="Download exceeds limit or storage full";goto finish;}written+=(unsigned)size;if(progress)progress(written,0);}if(size<sizeof data){if(br_ssl_engine_last_error(&c->ssl.eng)!=0){reason="Incomplete TLS response";goto finish;}break;}}}
 if(stopped(c)){reason="Request cancelled or timed out";goto finish;}if(!written){reason="Empty mirror response";goto finish;}success=1;
finish:
 if(file && fclose(file)){success=0;reason="Could not finish download; storage may be full";}if(c){if(!success && reason && error && errorSize>0){int tls=br_ssl_engine_last_error(&c->ssl.eng);snprintf(error,errorSize,"%s%s%d",reason,tls?" (TLS ":" (code ",tls);size_t size=strlen(error);if(size+2<(unsigned)errorSize){error[size]=')';error[size+1]=0;}}if(c->fd>=0)close(c->fd);free(c);}else if(reason && error && errorSize>0)snprintf(error,errorSize,"%s",reason);if(!success && destination)remove(destination);return success;
}
