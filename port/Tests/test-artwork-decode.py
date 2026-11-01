"""Decode real JPEG/PNG art through the native helper used on the iPod."""
from pathlib import Path
import ctypes
import subprocess

root=Path(__file__).resolve().parents[2]
out=root/'build/artwork-decode-tests'
out.mkdir(parents=True,exist_ok=True)
library=out/'libartwork.dylib'
subprocess.run(['clang','-dynamiclib','-O2',str(root/'port/Native/Artwork.c'),
                '-framework','CoreFoundation','-framework','CoreGraphics','-framework','ImageIO','-o',str(library)],check=True)
dll=ctypes.CDLL(str(library))
dll.legacy_decode_image.argtypes=[ctypes.c_char_p,ctypes.c_int]
dll.legacy_decode_image.restype=ctypes.c_void_p
dll.legacy_raster_artwork.argtypes=[ctypes.c_char_p,ctypes.c_int,ctypes.c_int]
dll.legacy_raster_artwork.restype=ctypes.c_void_p
cg=ctypes.CDLL('/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics')
for name in ['CGImageGetWidth','CGImageGetHeight']:
    getattr(cg,name).argtypes=[ctypes.c_void_p]
    getattr(cg,name).restype=ctypes.c_size_t
cg.CGImageRelease.argtypes=[ctypes.c_void_p]
cg.CGImageRelease.restype=None
checks=0
def check(ok,message):
    global checks
    checks+=1
    if not ok:raise AssertionError(message)
for name in ['demo-background.jpg','logo.png']:
    for maximum in [96,512]:
        image=dll.legacy_decode_image(bytes(root/'port/Assets'/name),maximum)
        check(bool(image),'Valid image decodes')
        try:
            width,height=cg.CGImageGetWidth(image),cg.CGImageGetHeight(image)
            check(max(width,height)<=maximum and min(width,height)>0,'Decoded image is capped at requested resolution')
            if name.endswith('.jpg'):check(abs(width/height-2732/1537)<0.02,'JPEG aspect ratio is preserved')
            else:check(width==height,'PNG aspect ratio is preserved')
        finally:cg.CGImageRelease(image)
corrupt=out/'corrupt.jpg';corrupt.write_bytes(b'not an image')
for path in [corrupt,out/'missing.jpg']:
    check(not dll.legacy_decode_image(bytes(path),512),'Corrupt/missing image returns failure')
check(not dll.legacy_decode_image(bytes(root/'port/Assets/logo.png'),0),'Invalid decode size returns failure')
for width,height in [(480,320),(240,160),(120,80),(43,39),(169,82)]:
    image=dll.legacy_raster_artwork(bytes(root/'port/Assets/demo-background.jpg'),width,height)
    check(bool(image),'Screen-sized raster is created')
    try:check(cg.CGImageGetWidth(image)==width and cg.CGImageGetHeight(image)==height,'Raster has exact final dimensions')
    finally:cg.CGImageRelease(image)
check(not dll.legacy_raster_artwork(bytes(corrupt),480,320),'Corrupt raster input returns failure')
print('PASS: '+str(checks)+' native artwork decoding and sizing checks')
