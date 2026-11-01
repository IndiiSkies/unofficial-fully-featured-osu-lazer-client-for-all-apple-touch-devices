# Beatmap video on iPod touch 2G

The song browser's **video: on/off** control enables video backgrounds in the
browser and gameplay. It does not filter the song library. Its value persists
in `legacy-ui.cfg`. Missing, disabled, not-yet-ready, failed and finished videos
leave the ordinary background image visible.

`Native/Video.m` uses the iOS 4 AVPlayer/AVPlayerLayer API. The native decoder
feeds Core Animation's GPU compositor beneath the dim layer and OpenGL ES 1.1
canvas. No decoded-frame uploads or per-frame FFmpeg video decoding run in the
managed drawing callback. A video-only composition removes the movie's audio;
the shared music player supplies the clock, including preview seeks, pauses,
retries and negative/positive beatmap video offsets.

Original MP4 extension alone does not establish decoder compatibility. The
provided Enemy and NULCTRL videos use H.264 Main profile. Their prepared copies
use 480x320, 30 fps, YUV420p, Constrained Baseline level 3.0, capped at 1 Mbit/s,
with no audio track. The original videos and beatmap events stay untouched.
Playback prefers `<original filename>.ipod.mp4` when it exists.

For newly imported videos that the Apple decoder cannot handle, prepare and
transfer companions on the Mac with the iPod connected:

```sh
clang -O2 probes/device-documents.c $(pkg-config --cflags --libs libimobiledevice-1.0) -o build/device-documents
python3 port/prepare-device-videos.py --udid DEVICE_UDID
```

The helper scans videos directly inside imported beatmap folders. It reuses a
unique matching video in an `.osz` in Downloads, otherwise reads the original
from the device. Conversion is cached by source content, verified with ffprobe,
and uploaded to a temporary filename before publishing the final companion.
The app does not yet transcode unsupported video formats on the iPod itself.
`prepare-video-osz.py` can alternatively prepare an archive before import.

Logs distinguish preparation, the first decoded frame becoming visible, and
decoder errors. Cross-compilation and timeline tests do not prove successful
hardware playback; check the first-frame log and actual iPod display.
