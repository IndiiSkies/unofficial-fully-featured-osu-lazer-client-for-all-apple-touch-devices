# Audio-reactive menu (version 0.25)

Adapted from the MIT-licensed attached osu!lazer source:
`Screens/Menu/OsuLogo.cs`, `LogoVisualisation.cs`, `MenuSideFlashes.cs`, and
`StarFountain.cs`. The ring, amplitude pulse, alternate side flashes and
corner star fountains use a deliberately smaller rendering budget on ARMv6.
The original fountain star texture is from
`osu.Game.Resources.Textures.Menu.fountain-star.png`, resource package
2026.918.0 / commit d481bc98f2603348ee81c15174cd72604286310d.
It remains under the resource licence; see osu-resources-LICENCE.md.

Analysis uses the platform Accelerate/vDSP radix-2 real FFT, 512 samples at
11025 Hz, Hann window, DC removal, 32 logarithmic frequency bands, RMS and
bass energy. One FFT setup and fixed arrays are reused on a low-priority
worker. There is no allocation inside the FFT operation. Main-thread state
exchange uses try-locks and drops old requests instead of accumulating work.
The ring requests updates at 20 Hz, matching the source's 50 ms amplitude
refresh interval, using 64 bars rather than the source's 200 bars.

`Startup/menu-analysis.pcm` is signed little-endian mono int16 audio decoded
from the exact `menu.m4a` at 11025 Hz with ffmpeg. It is an analysis-only sidecar;
AVAudioPlayer still plays the original full-rate AAC independently. The worker
reads only a 512-sample window (1024 bytes) per request using pread; the full
sidecar is not loaded into RAM. Windows are centred near the current playback
clock with 25 ms look-ahead from the known file to offset the analysis handoff.
A 512-sample window spans 46.4 ms. It does not introduce an audio playback
buffer or wait on the FFT. These are design bounds, not measured end-to-end
latency on the iPod. Analysis timings including PCM reads are logged on device.

Bass onsets drive the beat pulse and side flashes, with a 220 ms refractory
interval. Star fountains use 24 reusable layers on every fourth onset, rather
than the original higher-rate kiai-triggered emitter. This is not a full port
of lazer's timing-control-point/kiai beat provider. The current analysed source
is the bundled menu track; custom menu music is not yet implemented.

The logo moves from managed drawRect into native layers. Static menu contents
redraw on input and archive-open events, with polling only while background
import or song preparation is active, instead of repainting unchanged text. Spectrum rendering uses small solid-color bars, and star particles share
one texture. Hide, Animate-off and focus loss pause the update display link;
the worker then waits on a condition variable. They do not run during gameplay.
The user's system-font rollback remains in place.

Host tests exercise silence, frequency-bin placement, RMS, bass discrimination,
DC removal and finite/ranged bands (1193 checks). Host microsecond timings are
not claims about ARMv6 performance. Physical timing/frame-rate verification is
still necessary on the connected iPod.

## Version 0.26 import scheduling correction

The first implementation still enabled full-frame drawing while imports were busy, so heavy menu rasterisation could prevent display-link updates. Import and song-load polling now uses a separate callback at five polls per second. A poll does not draw unless completion/state changes require it, or changed import-status text has waited one second since the last menu paint. Gameplay keeps its own frame cadence. The import thread requests native NSThread priority 0.15 (FFT priority 0.2), below the default UI thread. Native-layer particle animations do not depend on menu text redraws. First-window/first-spectrum logs distinguish analysis readiness from visible rendering. Main-thread finalisation and runtime GC can still cause brief stalls; continuous zero-stall rendering is not guaranteed.
