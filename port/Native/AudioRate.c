#include <AudioToolbox/AudioToolbox.h>
#include <CoreFoundation/CoreFoundation.h>
#include <libavfilter/avfilter.h>
#include <libavfilter/buffersrc.h>
#include <libavfilter/buffersink.h>
#include <libavutil/opt.h>
#include <libavutil/error.h>
#include <libavutil/channel_layout.h>
#include <stdio.h>
#include <stdint.h>
#include <string.h>
#include <math.h>
#include "ImportGate.h"
static void put32(unsigned char *p,uint32_t n){p[0]=n;p[1]=n>>8;p[2]=n>>16;p[3]=n>>24;}
static int drain_rate(AVFilterContext *sink,AVFrame *out,FILE *file,uint32_t *total){
 int rc;while((rc=av_buffersink_get_frame(sink,out))>=0){unsigned bytes=out->nb_samples*4;if(*total>256u*1024u*1024u-bytes)return AVERROR(EFBIG);if(fwrite(out->data[0],1,bytes,file)!=bytes)return AVERROR(EIO);*total+=bytes;av_frame_unref(out);}return rc==AVERROR(EAGAIN)||rc==AVERROR_EOF?0:rc;
}
int legacy_rate_audio_wav(const char *source,const char *destination,double rate,int pitch,char *error,int capacity){
 ExtAudioFileRef audio=NULL;AVFilterGraph *graph=NULL;AVFilterContext *input=NULL,*sink=NULL;AVFilterInOut *inputs=NULL,*outputs=NULL;AVFrame *frame=NULL,*out=NULL;FILE *file=NULL;CFURLRef url=NULL;int rc=0;uint32_t total=0;int64_t position=0;unsigned char header[44]={0};
 if(!isfinite(rate)||rate<.5||rate>2){rc=AVERROR(EINVAL);goto done;}
 url=CFURLCreateFromFileSystemRepresentation(NULL,(const UInt8 *)source,strlen(source),false);if(!url || ExtAudioFileOpenURL(url,&audio)){rc=AVERROR_INVALIDDATA;goto done;}
 AudioStreamBasicDescription format={0};format.mSampleRate=44100;format.mFormatID=kAudioFormatLinearPCM;format.mFormatFlags=kLinearPCMFormatFlagIsSignedInteger|kLinearPCMFormatFlagIsPacked;format.mBytesPerPacket=format.mBytesPerFrame=4;format.mFramesPerPacket=1;format.mChannelsPerFrame=2;format.mBitsPerChannel=16;
 if(ExtAudioFileSetProperty(audio,kExtAudioFileProperty_ClientDataFormat,sizeof(format),&format)){rc=AVERROR_INVALIDDATA;goto done;}
 graph=avfilter_graph_alloc();if(!graph){rc=AVERROR(ENOMEM);goto done;}graph->nb_threads=1;
 rc=avfilter_graph_create_filter(&input,avfilter_get_by_name("abuffer"),"input","time_base=1/44100:sample_rate=44100:sample_fmt=s16:channel_layout=stereo",NULL,graph);if(rc<0)goto done;
 rc=avfilter_graph_create_filter(&sink,avfilter_get_by_name("abuffersink"),"output",NULL,NULL,graph);if(rc<0)goto done;
 inputs=avfilter_inout_alloc();outputs=avfilter_inout_alloc();if(!inputs||!outputs){rc=AVERROR(ENOMEM);goto done;}
 inputs->name=av_strdup("out");inputs->filter_ctx=sink;inputs->pad_idx=0;outputs->name=av_strdup("in");outputs->filter_ctx=input;outputs->pad_idx=0;
 char filters[160];if(pitch)snprintf(filters,sizeof(filters),"asetrate=%.0f,aresample=44100,aformat=sample_fmts=s16:channel_layouts=stereo",44100*rate);else snprintf(filters,sizeof(filters),"atempo=%.8f,aformat=sample_fmts=s16:channel_layouts=stereo",rate);
 rc=avfilter_graph_parse_ptr(graph,filters,&inputs,&outputs,NULL);if(rc<0)goto done;rc=avfilter_graph_config(graph,NULL);if(rc<0)goto done;
 frame=av_frame_alloc();out=av_frame_alloc();if(!frame||!out){rc=AVERROR(ENOMEM);goto done;}
 file=fopen(destination,"wb");if(!file || fwrite(header,1,44,file)!=44){rc=AVERROR(EIO);goto done;}
 while(1){legacy_import_checkpoint();if(legacy_import_interrupted()){rc=AVERROR_EXIT;goto done;}av_frame_unref(frame);frame->format=AV_SAMPLE_FMT_S16;frame->channel_layout=AV_CH_LAYOUT_STEREO;frame->channels=2;frame->sample_rate=44100;frame->nb_samples=4096;frame->pts=position;
  rc=av_frame_get_buffer(frame,0);if(rc<0)goto done;UInt32 count=4096;AudioBufferList data;data.mNumberBuffers=1;data.mBuffers[0].mNumberChannels=2;data.mBuffers[0].mDataByteSize=count*4;data.mBuffers[0].mData=frame->data[0];
  if(ExtAudioFileRead(audio,&count,&data)){rc=AVERROR_INVALIDDATA;goto done;}if(!count)break;frame->nb_samples=count;position+=count;rc=av_buffersrc_add_frame_flags(input,frame,AV_BUFFERSRC_FLAG_KEEP_REF);if(rc<0)goto done;rc=drain_rate(sink,out,file,&total);if(rc<0)goto done;
 }
 rc=av_buffersrc_add_frame_flags(input,NULL,0);if(rc<0)goto done;rc=drain_rate(sink,out,file,&total);if(rc<0)goto done;if(!total){rc=AVERROR_INVALIDDATA;goto done;}
 memcpy(header,"RIFF",4);put32(header+4,total+36);memcpy(header+8,"WAVEfmt ",8);put32(header+16,16);header[20]=1;header[22]=2;put32(header+24,44100);put32(header+28,176400);header[32]=4;header[34]=16;memcpy(header+36,"data",4);put32(header+40,total);
 if(fseek(file,0,SEEK_SET)||fwrite(header,1,44,file)!=44)rc=AVERROR(EIO);else rc=0;
done:
 if(file && fclose(file) && rc==0)rc=AVERROR(EIO);if(audio)ExtAudioFileDispose(audio);if(url)CFRelease(url);av_frame_free(&frame);av_frame_free(&out);avfilter_inout_free(&inputs);avfilter_inout_free(&outputs);avfilter_graph_free(&graph);
 if(rc<0){remove(destination);if(error && capacity>0)av_strerror(rc,error,capacity);return 0;}return 1;
}
