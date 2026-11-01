FFmpeg audio support
===================

FFmpeg 4.4.6, Copyright the FFmpeg developers, LGPL 2.1 or later.
Official source: https://github.com/FFmpeg/FFmpeg/tree/n4.4.6
Source archive: https://ffmpeg.org/releases/ffmpeg-4.4.6.tar.xz

This audio-only static build enables no GPL or nonfree components, network
protocols, hardware decoders, video decoders, or external codec libraries.
Configuration and rebuild instructions are in toolchain/build-ffmpeg.sh.
The unmodified source lives in build/ffmpeg-source; generated static libraries
and object files are retained in build/ffmpeg-armv6. port/build.sh relinks the
application with these libraries, permitting replacement builds of FFmpeg.

Apple's decoder is tested first by reading a short PCM block. Supported files
play directly from the original; successful probes are memoized for unchanged
files. Only files that fail Apple's decoder use FFmpeg and the PCM cache.
Fallback song audio is converted on a worker into stereo signed 16-bit WAV,
at its original sample rate up to 44100 Hz. The source file remains unchanged.
Cached WAV files are keyed by source path, size, modification time and decoder
revision, and published only after successful decoding. Each decode is bounded
to 256 MiB of PCM output, with a fixed output buffer rather than a full-song
memory allocation. Cache files live in Library/Caches/DecodedAudio.
