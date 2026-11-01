#include <CoreFoundation/CoreFoundation.h>
#include <CoreGraphics/CoreGraphics.h>
#include <ImageIO/ImageIO.h>
#include <string.h>
#include <stdbool.h>

/* Returns a retained, orientation-correct image capped at device resolution.
 * Keeping this independent of UIKit also permits native decode regression tests. */
CGImageRef legacy_decode_image(const char *path, int max_pixels) {
    CGImageRef image = NULL;
    if (!path || !*path || max_pixels < 1 || max_pixels > 2048) return NULL;
    CFURLRef url = CFURLCreateFromFileSystemRepresentation(NULL, (const UInt8 *)path, strlen(path), false);
    if (!url) return NULL;
    CGImageSourceRef source = CGImageSourceCreateWithURL(url, NULL);
    CFRelease(url);
    if (!source) return NULL;
    CFNumberRef size = CFNumberCreate(NULL, kCFNumberIntType, &max_pixels);
    const void *keys[] = { kCGImageSourceThumbnailMaxPixelSize, kCGImageSourceCreateThumbnailFromImageAlways,
        kCGImageSourceCreateThumbnailWithTransform, kCGImageSourceShouldCache };
    const void *values[] = { size, kCFBooleanTrue, kCFBooleanTrue, kCFBooleanFalse };
    CFDictionaryRef options = CFDictionaryCreate(NULL, keys, values, 4, &kCFTypeDictionaryKeyCallBacks, &kCFTypeDictionaryValueCallBacks);
    image = CGImageSourceCreateThumbnailAtIndex(source, 0, options);
    CFRelease(options); CFRelease(size); CFRelease(source);
    return image;
}

/* Bake cropping, resizing and alpha into an opaque, ready-to-draw bitmap once.
 * Per-frame drawing never has to decode or aspect-fill the original source. */
CGImageRef legacy_raster_artwork(const char *path, int width, int height) {
    if (width < 1 || height < 1 || width > 480 || height > 320) return NULL;
    CGImageRef source = legacy_decode_image(path, width > height ? width : height);
    if (!source) return NULL;
    CGColorSpaceRef space = CGColorSpaceCreateDeviceRGB();
    CGContextRef context = CGBitmapContextCreate(NULL, width, height, 8, width * 4, space,
        kCGImageAlphaNoneSkipLast | kCGBitmapByteOrder32Big);
    CGColorSpaceRelease(space);
    if (!context) { CGImageRelease(source); return NULL; }
    CGContextSetRGBFillColor(context, 0.035, 0.045, 0.075, 1);
    CGContextFillRect(context, CGRectMake(0,0,width,height));
    double sourceWidth = CGImageGetWidth(source), sourceHeight = CGImageGetHeight(source);
    double xScale = width / sourceWidth, yScale = height / sourceHeight;
    double scale = xScale > yScale ? xScale : yScale;
    CGContextSetInterpolationQuality(context, kCGInterpolationMedium);
    CGContextDrawImage(context, CGRectMake((width-sourceWidth*scale)/2, (height-sourceHeight*scale)/2,
        sourceWidth*scale, sourceHeight*scale), source);
    CGImageRef result = CGBitmapContextCreateImage(context);
    CGContextRelease(context); CGImageRelease(source);
    return result;
}
