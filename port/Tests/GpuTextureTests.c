#include "../Native/GpuTexture.h"
#include "../Native/GpuGlyphLayout.h"
#include <assert.h>
#include <stdio.h>
#include <math.h>
int main(void) {
    // Proportional atlases at native text sizes must retain their gutters,
    // stay within the MBX texture limit and never overlap another glyph.
    for(int size=6;size<=64;size++) {
        float advances[96];unsigned short xs[96],ys[96],widths[96];int rowHeight=size+8,atlasWidth=size<=16?256:512;
        for(int i=0;i<96;i++)advances[i]=size*(.2f+(i%9)*.1f);
        int height=legacy_gpu_glyph_layout(advances,rowHeight,atlasWidth,xs,ys,widths);assert(height>0 && height<=1024);
        for(int i=0;i<96;i++) {
            assert(widths[i]>=ceilf(advances[i])+4 && xs[i]+widths[i]<=atlasWidth && ys[i]+rowHeight<=height);
            for(int j=0;j<i;j++)assert(xs[i]>=xs[j]+widths[j] || xs[j]>=xs[i]+widths[i] || ys[i]>=ys[j]+rowHeight || ys[j]>=ys[i]+rowHeight);
        }
    }
    // Asymmetric atlas: distinct cells and top/bottom halves detect both a
    // wrong glyph row and upside-down individual glyphs after upload.
    unsigned char *source=calloc(512*256,4);
    for(int y=0;y<256;y++)for(int x=0;x<512;x++){int offset=(y*512+x)*4;source[offset]=1+(y/40)*16+x/32;source[offset+1]=y%40<20?220:40;source[offset+3]=255;}
    CGColorSpaceRef cs=CGColorSpaceCreateDeviceRGB();
    CGDataProviderRef provider=CGDataProviderCreateWithData(NULL,source,512*256*4,NULL);
    CGImageRef image=CGImageCreate(512,256,8,32,512*4,cs,kCGImageAlphaPremultipliedLast|kCGBitmapByteOrder32Big,provider,NULL,0,kCGRenderingIntentDefault);
    unsigned char *uploaded=legacy_gpu_texture_pixels(image,512,256);assert(uploaded);
    for(int glyph=0;glyph<96;glyph++){
        LegacyGpuUV uv=legacy_gpu_texture_region(glyph%16*32,glyph/16*40,32,40,512,256);
        for(int half=0;half<2;half++){int x=(int)((uv.left+uv.right)*.5f*512),y=(int)((uv.top+(uv.bottom-uv.top)*(half?.75f:.25f))*256);
            int offset=(y*512+x)*4;assert(uploaded[offset]==glyph+1);assert(uploaded[offset+1]==(half?40:220));}
    }
    free(uploaded);CGImageRelease(image);CGDataProviderRelease(provider);free(source);
    unsigned char paddedSource[]={255,0,0,255,0,0,255,255};
    provider=CGDataProviderCreateWithData(NULL,paddedSource,sizeof(paddedSource),NULL);
    image=CGImageCreate(1,2,8,32,4,cs,kCGImageAlphaPremultipliedLast|kCGBitmapByteOrder32Big,provider,NULL,0,kCGRenderingIntentDefault);
    uploaded=legacy_gpu_texture_pixels(image,2,4);assert(uploaded);
    assert(uploaded[0]==255 && uploaded[2]==0);assert(uploaded[8]==0 && uploaded[10]==255);
    assert(uploaded[3]==255 && uploaded[7]==0 && uploaded[19]==0);
    LegacyGpuUV uv=legacy_gpu_texture_region(0,0,1,2,2,4);assert(uv.top==0 && uv.bottom==.5f && uv.right==.5f);
    LegacyGpuUV framebuffer=legacy_gpu_framebuffer_region(480,320,512,512);
    assert(framebuffer.left==0 && framebuffer.right==.9375f && framebuffer.top==.625f && framebuffer.bottom==0);
    free(uploaded);CGImageRelease(image);CGDataProviderRelease(provider);CGColorSpaceRelease(cs);
    puts("PASS: native-size glyph packing/gutters at 59 sizes, 96 upright glyph cells, and padded image UVs");return 0;
}
