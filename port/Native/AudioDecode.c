#include "AudioDecode.h"
#include "ImportGate.h"
#include <libavformat/avformat.h>
#include <libavcodec/avcodec.h>
#include <libswresample/swresample.h>
#include <libavutil/channel_layout.h>
#include <libavutil/error.h>
#include <stdint.h>
#include <stdio.h>
#include <string.h>

#define MAX_PCM_BYTES (256u*1024u*1024u)
static void le32(unsigned char *p,uint32_t n){p[0]=n;p[1]=n>>8;p[2]=n>>16;p[3]=n>>24;}
static int write_pcm(FILE *file,SwrContext *swr,AVFrame *frame,uint32_t *total) {
    uint8_t buffer[32768];uint8_t *out[]={buffer};
    const uint8_t **input=frame?(const uint8_t **)frame->extended_data:NULL;
    int count=swr_convert(swr,out,sizeof(buffer)/4,input,frame?frame->nb_samples:0);
    if(count<0)return count;
    do {
        uint32_t bytes=count*4;
        if(*total>MAX_PCM_BYTES-bytes)return AVERROR(EFBIG);
        if(bytes && fwrite(buffer,1,bytes,file)!=bytes)return AVERROR(EIO);
        *total+=bytes;
        if(frame && count<(int)(sizeof(buffer)/4))break;
        count=swr_convert(swr,out,sizeof(buffer)/4,NULL,0);
        if(count<0)return count;
    } while(count);
    return 0;
}
static int receive_pcm(AVCodecContext *codec,AVFrame *frame,SwrContext *swr,FILE *file,uint32_t *total) {
    int status;
    while((status=avcodec_receive_frame(codec,frame))>=0) {
        status=write_pcm(file,swr,frame,total);av_frame_unref(frame);
        if(status<0)return status;
    }
    return status==AVERROR(EAGAIN)||status==AVERROR_EOF?0:status;
}
int legacy_decode_audio_wav(const char *source,const char *destination,char *error,int capacity) {
    AVFormatContext *format=NULL;AVCodecContext *codec=NULL;SwrContext *swr=NULL;
    AVPacket *packet=NULL;AVFrame *frame=NULL;FILE *file=NULL;
    int status=AVERROR(EINVAL),stream=-1;uint32_t total=0;unsigned char header[44]={0};
    if(!source || !destination)goto done;
    legacy_import_checkpoint();
    status=avformat_open_input(&format,source,NULL,NULL);if(status<0)goto done;
    status=avformat_find_stream_info(format,NULL);if(status<0)goto done;
    stream=av_find_best_stream(format,AVMEDIA_TYPE_AUDIO,-1,-1,NULL,0);if(stream<0){status=stream;goto done;}
    const AVCodec *decoder=avcodec_find_decoder(format->streams[stream]->codecpar->codec_id);
    if(!decoder){status=AVERROR_DECODER_NOT_FOUND;goto done;}
    codec=avcodec_alloc_context3(decoder);if(!codec){status=AVERROR(ENOMEM);goto done;}
    status=avcodec_parameters_to_context(codec,format->streams[stream]->codecpar);if(status<0)goto done;
    codec->thread_count=1;
    status=avcodec_open2(codec,decoder,NULL);if(status<0)goto done;
    if(codec->sample_rate<=0 || codec->channels<1 || codec->channels>8){status=AVERROR_INVALIDDATA;goto done;}
    int rate=codec->sample_rate>44100?44100:codec->sample_rate;
    int64_t layout=codec->channel_layout?codec->channel_layout:av_get_default_channel_layout(codec->channels);
    swr=swr_alloc_set_opts(NULL,AV_CH_LAYOUT_STEREO,AV_SAMPLE_FMT_S16,rate,layout,codec->sample_fmt,codec->sample_rate,0,NULL);
    if(!swr){status=AVERROR(ENOMEM);goto done;}
    status=swr_init(swr);if(status<0)goto done;
    packet=av_packet_alloc();frame=av_frame_alloc();if(!packet || !frame){status=AVERROR(ENOMEM);goto done;}
    file=fopen(destination,"wb");if(!file){status=AVERROR(EIO);goto done;}
    if(fwrite(header,1,44,file)!=44){status=AVERROR(EIO);goto done;}
    while((legacy_import_checkpoint(),status=av_read_frame(format,packet))>=0) {
        if(legacy_import_interrupted()){status=AVERROR_EXIT;goto done;}
        if(packet->stream_index==stream) {
            status=avcodec_send_packet(codec,packet);
            if(status>=0)status=receive_pcm(codec,frame,swr,file,&total);
        }
        av_packet_unref(packet);if(status<0)goto done;
    }
    if(status!=AVERROR_EOF)goto done;
    status=avcodec_send_packet(codec,NULL);if(status<0 && status!=AVERROR_EOF)goto done;
    status=receive_pcm(codec,frame,swr,file,&total);if(status<0)goto done;
    status=write_pcm(file,swr,NULL,&total);if(status<0)goto done;
    if(!total){status=AVERROR_INVALIDDATA;goto done;}
    memcpy(header,"RIFF",4);le32(header+4,total+36);memcpy(header+8,"WAVEfmt ",8);le32(header+16,16);
    header[20]=1;header[22]=2;le32(header+24,rate);le32(header+28,rate*4);header[32]=4;header[34]=16;
    memcpy(header+36,"data",4);le32(header+40,total);
    if(fseek(file,0,SEEK_SET) || fwrite(header,1,44,file)!=44){status=AVERROR(EIO);goto done;}
    status=0;
done:
    if(file && fclose(file) && status==0)status=AVERROR(EIO);
    av_packet_free(&packet);av_frame_free(&frame);swr_free(&swr);avcodec_free_context(&codec);avformat_close_input(&format);
    if(status<0){if(destination)remove(destination);if(error && capacity>0)av_strerror(status,error,capacity);return 0;}
    return 1;
}
