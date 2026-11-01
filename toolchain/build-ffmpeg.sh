#!/bin/bash
set -euo pipefail
root="$(cd "$(dirname "$0")/.." && pwd)"
if [[ "$root" == *" "* ]]; then
    ln -sfn "$root" /private/tmp/osu-armv6-codec-root
    root=/private/tmp/osu-armv6-codec-root
fi
source="$root/build/ffmpeg-source"
out="$root/build/ffmpeg-armv6"
mkdir -p "$out"
cd "$out"
export IOS_SDK="$root/toolchain/iPhoneOS4.2.sdk" IOS_TARGET=armv6-apple-ios4.2.1 IOS_CRT=crt1.3.1.o
"$source/configure" --prefix="$out/install" --target-os=darwin --arch=arm --cpu=arm1176jzf-s \
 --enable-cross-compile --cc="$root/toolchain/armv6-clang" --ar=/usr/bin/ar --ranlib=/usr/bin/ranlib \
 --disable-everything --disable-programs --disable-doc --disable-network --disable-autodetect \
 --disable-asm --disable-inline-asm --disable-fast-unaligned --disable-debug --disable-shared --enable-static --enable-small \
 --disable-avdevice --enable-avfilter --disable-postproc --disable-swscale \
 --enable-filter=abuffer,abuffersink,atempo,aformat,aresample,asetrate \
 --disable-videotoolbox --disable-audiotoolbox --disable-securetransport --disable-iconv \
 --enable-avformat --enable-avcodec --enable-avutil --enable-swresample \
 --enable-protocol=file --enable-demuxer=ogg,mp3,flac,wav,mov,aac,aiff,asf,matroska,ape,wv \
 --enable-parser=vorbis,opus,mpegaudio,flac,aac,aac_latm \
 --enable-decoder=vorbis,opus,mp3,mp3float,mp2,flac,aac,aac_latm,alac,wmav1,wmav2,wmapro,wmavoice,ape,wavpack,pcm_s16le,pcm_s16be,pcm_s24le,pcm_s24be,pcm_s32le,pcm_s32be,pcm_f32le,pcm_f32be,pcm_u8,adpcm_ima_wav,adpcm_ms
make -j4
make install
