# Experimental ARMv6 osu! port

Version 0.89 adds native catch maps and osu!-to-catch conversion. The catch
tab shows both. It includes fruit, droplets/tiny droplets, banana showers,
walk/dash/hyperdash, the original catcher sprites, plate stacking/clears,
normalized catch scoring, health, separate local scores, catch statistics
and the source's two-second resume countdown. Touch the outer 15% edges:
upper 55% dashes, lower 45% walks. `Catch first steps` is bundled.
See `ThirdParty/catch-provenance.md` for exact source references, upstream
conversion comparisons and remaining full-parity limits.

Version 0.88 persists metadata and prepared audio paths in
`Documents/library-index-v1.tsv`. Startup publishes the saved entries first,
then checks for new/changed archives. Unchanged files reuse metadata and
prepared audio without reparsing, decoding or archive hashing/extraction.
The first launch builds this index from the existing unpacked library.
File size/mtime stamps detect edited maps/audio/archives; directory stamps
detect added artwork/video companions. Deleted decoded cache files are
prepared again when encountered. Loose `.osu` files remain discoverable.
Index writes use same-directory atomic replacement, and a missing/corrupt
index is rebuilt without removing imported songs or original archives.

Version 0.87 adds the original Pippidon mascot frames, beat/kiai animation,
miss and 50-combo/swell-clear reactions, split centre/rim input drum
flashes, and bounded hit bursts/flying notes. Reduced effects uses a static
mascot and suppresses bursts. Frames were extracted from the supplied
`osu.Game.Resources.dll` (`Skins.Legacy.pippidon*.png`) using
`extract-taiko-assets.cs` and reduced to at most 72 pixels before bundling.
These assets retain osu!resources' existing CC BY-NC attribution/licence.
Behaviour references: `DrawableTaikoMascot`, `TaikoMascotAnimation`
(100 ms clear sequence), `ArgonInputDrum` (40/750 ms input flash) and
`ArgonHitExplosion` (Great/Good feedback) in the supplied taiko source.

Version 0.86 adds basic native taiko (`Mode: 1`) compatibility. Select the
taiko tab in Solo; osu! remains the default. Four touch pads are arranged
rim, centre, centre, rim. Notes scroll towards the left target; large notes
accept the opposite hand within 30 ms for a bonus. Rolls accept repeated
taps; swells require alternating centre and rim. Pause, fail, retry, local
scores and results reuse the existing flow. A bundled Taiko first steps
demo covers these objects.

References in the supplied lazer source: `osu.Game.Rulesets.Taiko/Scoring/TaikoHitWindows.cs`
(50/35/20 and 120/80/50 timing ranges, floored minus 0.5 ms),
`Objects/Hit.cs` (whistle/clap rim notes), `Beatmaps/TaikoBeatmapConverter.cs`
(native rolls and swell hit-rate multiplier 1.65), and
`Objects/Drawables/DrawableSwell.cs` (alternating centre/rim).
This compatibility implementation uses a fixed scroll speed, a 50 ms
roll-tap debounce, and the port's aggregate scoring with bonus points;
it does not implement standard-to-taiko conversion, full lazer score
normalisation, taiko skins, or taiko-specific mods. Rolls and swells do
not add primary accuracy judgements or combo.

This is an experimental osu!standard app for the jailbroken iPod touch 2nd
generation on iOS 4.2.1. The UI was verified on device in version 0.12. Version
0.15 adds playable sliders and spinners and includes the launch-watchdog fix;
that combined build still requires physical-device verification.

Version 0.2 fixes the first observed startup failure: Mono tried to load a
4.5 core library, while the app bundles 4.0. The launcher now passes
`--runtime=v4.0.30128`, which maps to profile 4.0 in Mono 3.0.12's runtime
table. The abbreviated `v4.0` instead maps to profile 4.5 in that version.
The rebuilt app has been installed on the iPod; launch verification is pending.

Version 0.3 addresses the next device crash, SIGTRAP during Mono exception
trampoline initialization. Symbolication located it at `mono_arm_emit_load_imm`:
its rotate helper shifted a 32-bit value by 32 in the zero-rotation case.
Modern Clang optimized callers to an unconditional trap. The compatibility
patch handles zero rotation explicitly. The actual source helper now passes
all 4096 ARM immediate encodings under an undefined-behavior sanitizer, and
disassembly confirms the emitter no longer begins with a trap. Version 0.3 is
installed; the next device launch is being checked.

Subsequent device debugging found two additional ARM backend issues:

- The literal patcher accepted `LDR IP,[PC,#0]` but rejected the same valid
  instruction targeting R0. It now patches the literal independently of Rd;
  a host test checks all 16 register encodings and rejects other offsets.
- The iOS managed-to-native wrapper fast path assumes AOT/GOT-relative TLS
  patching. In JIT mode, it interpreted the pthread key itself as a relative
  address, read code bytes as a key, and faulted inside a native call. JIT
  wrappers now use the existing `mono_get_lmf_addr` helper; the fast path is
  restricted to AOT. This fix is installed in version 0.7 for device testing.

Version 0.8 still aborts before UIKit startup. The device reaches the native
bundle-path binding from managed Main, then fails in `mono_handle_global_vregs`
(`method-to-ir.c:12314`) while compiling a string constructor for `System.IO.Path`.
Disabling JIT optimizations did not resolve the invalid IR instruction. Version
0.9 adds IR checks immediately after translation and long decomposition, plus
verbose tracing for `CreateString`, to locate its origin. It is a diagnostic
build, not a claimed fix. Host checks and package-signature verification pass;
the menu and gameplay remain unverified on the device.

Version 0.9 traced the invalid IR to `conv.u` in `String.CreateString(char,int)`.
The host C# compiler emits this conversion while pinning a string; Mono 3.0.12's
translator handles managed pointers but omitted object references in that case.
Version 0.10 treats `STACK_OBJ` like the existing pointer cases (a register move
on ARMv6). A source-extracted regression test covers all six supported input
types at both pointer widths. Device launch verification is still required.

Version 0.10 now stays open and reaches the managed Draw callback, but the user
reported a black screen. CoreGraphics repeatedly logged singular transforms.
Clang's ARMv6 target reports `__alignof__(long long) == 8` but uses 4-byte
field alignment. Mono used the former for native argument layout, inserting
padding after the text pointer before subsequent double arguments. Version
0.11 explicitly uses Darwin's 4-byte argument alignment under TARGET_IOS.
A source-extracted initialization test verifies the rule; the first three text
calls also log their numeric arguments for device verification.

Debug builds currently enable `OSU_ARM_DIAGNOSTICS` to log JIT code ranges and
original fault registers. Versioned executables, IPAs and dSYMs are retained in
`build/port/version-*` so device crash addresses can be matched to the exact
binary. Diagnostics can be disabled after startup and gameplay are validated.

## Song artwork (version 0.17)

Solo song rows now display cropped cover thumbnails derived from each beatmap's
background image. The selected song also supplies the large preview card and
full browsing background, and its background carries into gameplay. The existing
background-dim setting applies to both scenes.

The shared metadata reader resolves Background events, legacy Video-as-image
entries, and the first storyboard Sprite fallback. It handles quoted commas,
CSV double quotes, Windows separators, filename case and Unicode. Artwork paths
must stay inside the song directory and cannot traverse symlinks. Missing or
unreadable images use a placeholder without removing the song from the library.

Native ImageIO decoding caps images at 512 pixels on the longest side, crops
with aspect-fill, and shares an eight-image LRU cache between covers/backgrounds.
Failures are cached too; rescanning the library clears the cache so replaced
or newly supplied images can load. iOS memory warnings also clear this cache.
The bundled demo now declares a background image for device verification.

Seventeen metadata/render-selection/fallback checks and fifteen native image
checks pass on the Mac. JPEG/PNG decoding, size caps, aspect ratios and corrupt
or missing image behavior are exercised through the production decode helper.
Device rendering of this version is pending.

## Slider precision correction (version 0.16)

The device log located the post-0.15 playback error in SliderPath.Position while
building repeat arrows. A length extension smaller than the 1e-6 vertex filter
was discarded by Add(), but a cumulative-distance entry was appended anyway.
This left a distance index with no matching path vertex. A 100-unit line with
expected length 100.0000001 reproduces the same stack on host Mono.

Sub-tolerance corrections now update the existing endpoint's cumulative
distance; larger extensions append both a vertex and its matching distance.
The reproducer now passes. Eight new checks cover endpoint lookup and repeat
arrows at corrections from 1e-12 to 1e-5; all 50 gameplay checks pass. Version
0.16 is packaged for device verification. The 0.15 device logs also confirm
native-first startup reached the menu and .osz extraction completed.

## Sliders and spinners (version 0.15)

The standard beatmap loader accepts circles, sliders and spinners, including
new-combo flags. Sliders support linear, Bezier/multi-Bezier, perfect-circle,
Catmull, explicit-degree B-spline and mixed curve segments. Paths are trimmed
or extended to the encoded distance. Red/inherited timing points, slider
multiplier, repeats, tick rate, legacy pre-v8 ticks and disabled-tick timing
points control event generation.

Gameplay tracks touch down/move/up/cancel with independent contact IDs.
Slider heads, ticks, reversals and tails respond to held following, including
recovery after a break, contact handoff, simultaneous circle taps, and tail
lenience. Spinner input measures held angular movement around the centre,
handles either direction and angle wrap, and ignores reposition/centre jitter.
Difficulty controls required turns; completed extra turns award capped bonus
score. Native strokes render slider tracks, repeat arrows and follow circles;
spinner rendering includes a shrinking ring, turn counter and clear progress.

The bundled `Sliders and spinners` difficulty exercises linear repeats,
Bezier/perfect-arc sliders and a four-second spinner. Geometry/model loading
runs on a worker so larger maps do not block input while loading. The Solo
Back button cancels applying a pending loaded map.

This remains the legacy gameplay backend. Slider judgements use classic
aggregation and the existing per-object accuracy model. Full lazer nested
accuracy, stacking/notelock, hit-sample playback, spinner history/smoothing,
mods and replays are not yet equivalent. See `ThirdParty/gameplay-provenance.md`
for exact upstream references and adaptation details.

## Launch watchdog correction (version 0.14)

Device reports for 0.13 identify `0x8badf00d` / "failed to launch in time", not
an archive-decoder exception. The demo ZIP extracted successfully on device,
but cold JIT work, per-method diagnostic logging and full-size artwork decode
kept the first UI transaction inside iOS's approximately 20-second deadline.

The app now enters UIApplicationMain immediately and presents a native loading
screen. A later timer initializes Mono through its embedding APIs and invokes
managed Main without shutting down the runtime when Main returns. The managed
legacy_start binding only registers callbacks; it no longer re-enters UIKit.
Automatic import starts after drawing the initial managed menu. Archive scans
and metadata parsing both run on the worker; the UI consumes completed metadata
without disk reads. ImageIO decodes device-sized thumbnails directly. Verbose
ARM/JIT tracing is disabled for normal startup. Fatal managed startup exceptions
are logged while the native loading/error screen remains available.

The device build and host checks pass; version 0.14 needs physical launch
verification. Archive regression coverage is now 20 native + 15 managed checks.

## Beatmap archives (version 0.13)

The Import button in Solo now processes `.osz` archives in the background.
Copy archives into the app's Documents folder, Documents/Imports, or
Documents/Beatmaps, then tap `import .osz`. Startup also scans automatically.
The app declares the `.osz` document type for iOS Open In and enables USB
file sharing; delivered Inbox files are queued automatically.

The importer streams stored/deflated ZIP entries through MiniZip (from the
pinned Mono source) and the system zlib. It preserves nested paths, audio,
images, video and other assets, verifies entry CRCs, and only publishes a set
by renaming a completed staging folder. The source `.osz` is retained.
SHA-256 content receipts skip repeat imports even when an archive is renamed.
Interrupted staging folders belonging to this importer are cleaned on retry.
Unicode names and both slash conventions are supported. ZIP64, encryption,
special files/links, and traversal paths are rejected. Import limits are
10,000 entries, 1 GB expanded in total, and 512 MB per file.

`python3 port/Tests/test-osz-import.py` builds and exercises the same native
extractor on the Mac, plus the actual managed publication and batch worker:
20 native checks and 14 managed checks pass. UI checks also cover invoking the
import worker, its busy state, and replacing an existing notice. The bundled
demo is now imported from a reproducible `.osz` on device startup, rather than
being added directly as a loose `.osu` path. Device validation is pending.
Gameplay support depends on the active ruleset engine; archive extraction retains
all assets even when another ruleset engine is not implemented.

## UI stage (version 0.12)

The native app now renders a landscape 480x320 UI adapted from the attached
lazer menu source: original logo/background artwork, expandable logo menu,
Play/Solo and Edit navigation, a song library with four ruleset tabs and
metadata, and persistent volume/dim/animation controls. Drawing and touch
input use the same portrait-to-landscape coordinate transform on iOS 4.
The original large images are cached at device size by the native adapter.

This is an early UIKit UI adapter, not the original framework drawable tree.
The multiplayer, browser, playlist, daily challenge and editor entry points
show explicit backend status. Only the existing circle-only gameplay backend
is currently connected to the Solo Play button. Ruleset tabs filter imported
metadata; they do not claim that the other ruleset engines have been ported.

Thirteen host checks cover menu navigation, song selection/paging, ruleset
filtering, status panels, and settings persistence/audio notification. SVG
previews generated by the same managed UI layout are in `build/ui-preview`.
The user verified version 0.12 display and input on the physical iPod.
Assets and menu-source provenance are in `ThirdParty/ui-provenance.md` and the
resource licence is bundled inside the app. Version 0.12 is built and packaged
for ARMv6; installation status is recorded in the task conversation.

## What is implemented

- Embedded ARMv6 Mono 3.0.12 with its matching, locally built .NET 4 core library.
- A native UIKit/CoreGraphics renderer, multitouch taps and AVAudioPlayer clock.
- A managed standard beatmap reader, circle/slider/spinner gameplay, song menu,
  approach circles, timing judgements, combo/accuracy and a completion indicator.
- A generated 20-second audio/demo map with no external media dependencies.
- Additional `.osu` maps and complete `.osz` archives, detected at launch or
  through the Solo import button.

Circles, sliders and spinners are playable in the legacy standard backend.
The other ruleset engines are not implemented.
There is no full lazer scoring/notelock parity, HP/failure system, mods, skins,
replays, online access, Realm library, or other rulesets yet. Audio calibration
and AudioLeadIn handling remain to be implemented. This is a port foundation,
not a completed port of the lazer client.

## Reused lazer source

`ThirdParty/OsuHitWindows.cs` is from the supplied `2026.1005.0-lazer` source.
Its hit-window calculations are retained. The only change to that file replaces
static interface helper calls with a classic-runtime helper. `Managed/Timing.cs`
adapts the difficulty-range formula, hit-result ordering and ResultFor search;
Session also uses lazer's AR/CS curves, including its radius allowance.
`ThirdParty/provenance.json` records the original file's hash and modifications.
The upstream MIT licence is retained.

GitHub's latest-release API was checked on October 28, 2026 and identified
`2026.1005.0-lazer` as the latest stable release. The compact response is saved
in `ThirdParty/upstream-release.json`.

The rest of the client has not been made source-compatible with classic Mono.
UIKit/CoreGraphics and AVAudioPlayer replace the framework services used by
this first gameplay slice; they do not implement the entire osu.Framework API.

## Build

From the workspace root, with the downloaded dependencies already present:

```sh
bash toolchain/build-mono.sh
bash port/build.sh
bash probes/package-runtime.sh
```

The SDK defaults to `toolchain/iPhoneOS4.2.sdk`; `IOS_SDK` can override it.
The scripts use the locally built ARMv6 linker at `build/toolchain/ld64`.
No global .NET/Xcode installation is changed. `toolchain/build-mono.sh` builds
SGen and skips the unused Boehm collector and profiler. The private pthread
header compatibility shim supplies only a constant; its optional function is
still resolved dynamically. The runtime uses pthread TLS and currently has
sigaltstack disabled pending device tests.

Outputs:

- `build/port/OsuLegacy.app`
- `build/port/OsuLegacy-armv6-ios421-experimental.ipa`
- `build/device-runtime-probes.tar.gz`

The IPA is an ad-hoc signed experimental jailbreak build. It has no App Store
provisioning. Signing uses an early-format CodeDirectory (0x20001), SHA-1 page
hashes, and a sealed resource envelope. macOS signature verification passes;
device installation and execution have been confirmed, but startup still fails.

The probe archive can be extracted to a temporary directory on the device.
Its `run.sh` checks native execution, generated ARM code permissions, the Mono
version and the managed runtime smoke test in sequence. Device access is still
pending; these steps have not been executed on the iPod.

## Validation and source material

The build runs host checks against the managed core: difficulty timing endpoints,
early/late boundaries, aim requirements, duplicate taps, late misses, combo and
accuracy, active-object culling, parser flags and AR fallback, unsupported
sliders, and audio-path containment. Host passes do not establish ARMv6 runtime
or UI compatibility. Native Mach-O metadata and signatures are inspected too.

Mono 3.0.12's runtime is LGPL; its licence and LGPL text are included in the app,
along with eglib and osu! licences. Its pinned source is in `mono-legacy/`, the
compatibility patch is in `toolchain/`, and `port/Native/App.m`, `build/port/App.o`,
the native static archives under `build/mono-sdk42/`, and `port/build.sh` provide
the local relinking material for runtime changes. The SDK is a build dependency
and its system libraries are not bundled in the app.

## Version 0.19 sound effects

Adds preloaded, overlapping hit sounds and sustained slider/spinner effects via OpenAL. The existing Volume setting controls music and effects. Supports beatmap sample banks, timing volume/index, custom hit filenames and slider edge samples, with bundled fallbacks. See `ThirdParty/sound-provenance.md` for formats and remaining limitations. Host sound checks are part of `build.sh`; audible playback requires checking on the iPod.

## Version 0.20 welcome intro

Plays the original welcome voice and piano over a Core Animation adaptation of lazer’s Welcome intro, then loops nekodex’s “welcome to osu!” theme in menus. Tap to skip the intro. Menu music pauses for gameplay and resumes on return; the existing Volume setting controls it. See `ThirdParty/startup-provenance.md` for attribution and adaptation details.

## Version 0.21 correct Triangles intro

Replaces the earlier Welcome variant with `IntroTriangles`: staged welcome text, triangle glitches, four ruleset icons, animated logo zoom and menu flash. Uses the original glyphs and logo progress-mask textures in a 480×320 Baseline H.264 clip. The matching cYsmix “triangles” track includes the welcome voice and continues into the menu. See `ThirdParty/startup-provenance.md` for source timings and rendering details.

## Version 0.22 startup ordering fix

Defers media setup until managed initialization returns to UIKit, then waits for the intro surface to have a presentation opportunity and for decoder readiness before starting playback. Prevents the welcome from starting behind the native loading screen.

## Version 0.23 animated menu triangles

Adds rising triangle particles behind the main title, Play and Edit menus using persistent Core Animation layers. The Animate setting freezes/resumes them. They pause off-screen and during focus loss, and do not run over song artwork or gameplay.

## Version 0.25 audio-reactive menu

Adds music-driven logo pulse, a 64-bar ring, side flashes and bounded star fountains. A 512-point Accelerate/vDSP FFT analyses a small window of a PCM sidecar for the bundled theme on a low-priority worker at up to 20 Hz, independently of audio playback. Static menu text redraws on input/events, with polling retained for loading jobs. Animate disables updates; effects pause off-screen and on focus loss. Device timing is logged. See `ThirdParty/menu-effects-provenance.md` for limits and source attribution. The system fonts from the v0.23 baseline remain unchanged.

## Version 0.26 import animation scheduling

Separates background-job polling from menu painting. Busy imports no longer force continuous menu redraws; progress text is capped at one repaint per second, with immediate completion updates. Import work uses a lower native thread priority. Adds first-analysis/first-spectrum diagnostics for device checks.

## Version 0.27 local music playlists

Header controls provide previous, pause/resume, next and list. The music panel offers all imported supported songs, a saved editable My playlist, shuffle, repeat and +/-10s seek. Background music defaults to imported music after library load and advances automatically. The FFT follows selected tracks using a background-built PCM cache. MP3/WAV/M4A/AAC/AIFF/CAF are supported; Ogg is not included in this queue. See `ThirdParty/playlist-provenance.md`.

## Version 0.28 song-browser audio

Song selection previews the highlighted beatmap using its PreviewTime. The title playlist pauses while browsing and resumes at its previous position on Back. Preview audio stops before gameplay; import notices keep the browser context.

## Version 0.29 visualizer startup delay

Uncached songs now feed the FFT through small streamed decode windows instead of waiting for whole-track PCM conversion. The converter is reused for sequential updates; cached tracks retain the pread fast path. Tests: `python3 port/Tests/test-menu-audio-window.py` (macOS CoreAudio codec access required).
