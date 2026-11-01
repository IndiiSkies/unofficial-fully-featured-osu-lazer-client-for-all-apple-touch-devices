#ifndef LEGACY_GPU_TEXTURE_H
#define LEGACY_GPU_TEXTURE_H
#include <CoreGraphics/CoreGraphics.h>
#include <stdlib.h>
typedef struct {float left,top,right,bottom;} LegacyGpuUV;
// CGContextDrawImage preserves CGImage row order in this bitmap buffer.
// GL's first uploaded row is v=0: atlas row zero must therefore use v=0.
static LegacyGpuUV legacy_gpu_texture_region(int x,int y,int w,int h,int tw,int th) {
    LegacyGpuUV uv={(float)x/tw,(float)y/th,(float)(x+w)/tw,(float)(y+h)/th};return uv;
}
// GL-rendered targets have their first logical row at the top of the viewport,
// unlike bitmap uploads. Flip V when reusing the cached UI framebuffer.
static LegacyGpuUV legacy_gpu_framebuffer_region(int w,int h,int tw,int th){LegacyGpuUV uv={0,(float)h/th,(float)w/tw,0};return uv;}
static void *legacy_gpu_texture_pixels(CGImageRef image,int tw,int th) {
    void *pixels=calloc((size_t)tw*th,4);if(!pixels)return NULL;
    CGColorSpaceRef colour=CGColorSpaceCreateDeviceRGB();
    CGContextRef bitmap=CGBitmapContextCreate(pixels,tw,th,8,tw*4,colour,kCGImageAlphaPremultipliedLast|kCGBitmapByteOrder32Big);CGColorSpaceRelease(colour);
    if(!bitmap){free(pixels);return NULL;}
    // Bitmap memory begins at its top row. Place the image at the top of a
    // padded power-of-two allocation, leaving padding after the image rows.
    CGContextDrawImage(bitmap,CGRectMake(0,th-CGImageGetHeight(image),CGImageGetWidth(image),CGImageGetHeight(image)),image);CGContextRelease(bitmap);return pixels;
}
#endif
