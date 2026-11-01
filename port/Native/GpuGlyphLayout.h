#ifndef LEGACY_GPU_GLYPH_LAYOUT_H
#define LEGACY_GPU_GLYPH_LAYOUT_H
#include <math.h>
// Keep transparent gutters around every glyph so linear filtering cannot
// sample a neighbouring character. Pack proportional glyphs, not fixed cells.
static int legacy_gpu_glyph_layout(const float *advances,int height,int atlasWidth,
                                  unsigned short *xs,unsigned short *ys,unsigned short *widths) {
    int x=0,y=0;
    for(int i=0;i<96;i++){
        int width=(int)ceilf(advances[i])+4;
        if(width>atlasWidth)return 0;
        if(x+width>atlasWidth){x=0;y+=height;}
        xs[i]=x;ys[i]=y;widths[i]=width;x+=width;
    }
    return y+height;
}
#endif
