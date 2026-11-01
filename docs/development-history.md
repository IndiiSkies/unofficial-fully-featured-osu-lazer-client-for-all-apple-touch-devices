# Historical development notes

Archived from the previous README on 2026-10-31. These entries describe earlier
builds and experiments; some statements are superseded. See the root README
for the current feature and installation guide. Paths in these historical
notes are relative to the repository root.

Version 0.118 adds Browse → an online beatmap listing backed by Hinamizawa.
It has search, mode/status/sort filters, star ranges, genre/language filters,
a scrolling cover grid, difficulty details and no-video/full .osz downloads.
Completed downloads go to Documents/Imports and are prioritized for import;
installed sets can be opened directly in Solo. Search, artwork and streaming
run on background workers. Error/retry/cancel states and cached results are
visible; duplicate taps cannot launch simultaneous archive downloads.

TLS 1.2 runs on the device through BearSSL with a Mozilla CA trust bundle and
normal certificate/name/time checks. No Mac relay or API login is required.
Use a correct iPod date and Wi-Fi connection. Online audio previews, account
features, comments and ratings are outside this first listing implementation.
See port/ThirdParty/browser-network.md for endpoints, limits and validation.

Version 0.117 puts brightness and master/music/effects rings in the existing
top overlay cache before drawing them. They no longer raise the whole menu
above the logo. Notifications are appended afterward and stay above these
panels. No additional render target is allocated beyond v0.116.

Version 0.116 hides header/footer chrome (including clock and bell) while the
main menu is retracted. Opening the menu restores them. Notifications and
toasts use a separate cached GPU overlay, composed after the logo; opening
them no longer raises the whole navigation strip above the logo. Explicit
settings-sidebar priority remains intact. Empty notification layers are not
composited. A host test checks the production compositor layer ordering.

Version 0.115 corrects the logo-to-menu proportions: the open logo spans
198px against the 90px navigation strip and protrudes above and below it.
The idle logo is larger at 264px and stays within the iPod viewport. Right-hand
tiles/content move outward to stay clear of the larger disc. Input follows
the visible circle throughout both transitions.

Version 0.114 follows the upstream six-second idle timeout: main-menu sections
retract to the centred logo. Opening scales the logo to half size over 200ms;
idle restores it over 800ms after a 150ms delay. Submenu switches preserve the
logo scale. The settings/back tile fills the row height, adjacent wedge tiles
meet without exposed gray gaps, and layered shadows separate them. Hit testing
tracks the animated logo radius. Song browsing/editors are excluded from idle
retraction; active buttons, edge gestures and notifications keep their controls.

Version 0.113 uses the original upstream menu icons above the labels and makes
logo clicks accept the first option: reveal → Play → Solo; in Edit, open the
beatmap editor. The separate Back button handles returning to the main menu.

Version 0.112 aligns Play and Edit submenu buttons in a horizontal row, uses
the left settings position for Back in those sections, and removes the lower
redundant Back link and Daily entry. The toolbar settings cog remains available.

Version 0.111 adds a touch beatmap editor at Edit → beatmap editor. Choose an
installed difficulty and press Edit. Compose supports selecting/dragging,
circles, straight sliders, spinners, and native mania holds; existing curved
sliders and sample fields survive editing. The summary/detail timelines seek
through audio with beat snapping, grid and zoom. Tools cycles compose, timing,
and setup; timing edits BPM, offset, inherited slider velocity and kiai;
setup edits title/artist/creator/difficulty and CS/OD/AR/HP. Undo/redo, duplicate,
delete and clearing a draft are supported. Metadata/numeric entry uses the
iOS keyboard. Preview audio shares the existing decoder fallback and player.

Save creates a new `(edited).osu` beside the original, retains the set ID,
clears the difficulty ID and adds an editor marker. Reopening that edited copy
updates it with a `.bak` backup. Writes are staged and validated; an externally
modified destination is not overwritten. Empty difficulties are supported for
composition. Test saves, plays the difficulty and returns to the editor when
leaving gameplay; test scores are not recorded as personal bests. Imported
songs pause while the editor is active. The original file is preserved.

This is the first functional editor port, not the complete lazer editor:
waveforms/spectrograms, storyboard authoring, path control-point editing,
multi-object selection/transforms, sample-file selection and submission/export
UI remain unported. Existing authored data is preserved. Host editor tests and
rendered previews do not establish physical iPod input/audio performance.

Version 0.110 connects the mod browser to captured per-play settings across
osu!, taiko, catch and mania. It adds difficulty/position changes, score
multipliers, failure challenges, automation and input constraints, visibility
and object/cursor effects, and cached rate-mod audio. Configure speed,
accuracy target, difficulty overrides and random seed from Mods → configure.
Alternate/Single Tap use two bottom touch keys and a separate aiming finger.
Catch Relax uses direct finger positioning. Automation/assisted and adapted
fun-mod plays do not replace local personal bests.

Speed audio is prepared before the presentation-gated gameplay start. Apple
still decodes supported originals; FFmpeg supplies tempo/pitch processing
and the existing fallback decoder. DT/HT preserve pitch; NC/DC change pitch.
First preparation needs additional loading time and disk space; repeat plays
reuse the rate cache. Production audio tests verify duration and tone pitch.

The mod implementation targets the defaults of the existing port catalogue,
not every upstream configuration option or exact visual parity: ES1.1 uses a
strip-based flashlight mask and a projected 2D Depth effect; Target Practice
uses a simplified beat target generator. Catch Classic has no object changes
in upstream either. No iPod performance or physical-input result is implied
by the host regression checks.

# osu! on iPod touch 2 / iOS 4.2.1

Version 0.109 adds a working HUD skin/layout editor, opened from Edit → skin
editor or the gameplay pause menu's edit HUD button. It edits health, avatar,
title, score, accuracy, combo, progress, pause, timing, self-competition and
break display components. Drag to move; the corner handle resizes. The panel
also provides scale, rotation, opacity, hide/show, duplicate/delete, alignment,
reset, grid snapping and undo/redo. Each ruleset has its own layout. Save writes
Documents/hud-layouts-v1.tsv through a staged replacement with a .bak copy;
saved layouts apply to real gameplay and survive restarts. Unsaved changes
offer save/discard/keep editing. The last visible pause button is protected,
and its hit area moves/rotates/scales with its rendering.

This is the editor for the HUD components implemented by this port, not full
lazer skin-editor parity: asset/audio editing, .osk import/export, unported
components, multi-selection and cross-component layer ordering remain absent.
Layouts use the legacy renderer's fixed 480x320 space and local file format.

Version 0.108 uses an Argon-style health HUD when reduced effects are off:
the source's stepped/rounded 300x50 path, white/cyan fill, 50ms health damping,
and a 500ms red damage hold followed by a 300ms return. The profile image comes
from the same UI property as the toolbar avatar. ES 1.1 polylines approximate
the source's path shaders and glow. Reduced effects retain the lightweight
rectangle. Gameplay cursor visibility now follows its own setting independently
of reduced effects; the orange resume target remains visible as required.

Version 0.107 implements the authored-break countdown from upstream
`BreakPeriod`, `BreakTracker`, `BreakOverlay` and `RemainingTimeCounter`:
650ms minimum effective break, 325ms fades, ceiling of remaining seconds,
shrinking bar, inward arrows and current accuracy/grade. It uses the song clock
in all modes, so pause and retry require no independent timer. Unmarked gaps
do not invent breaks. osu!/taiko health drain excludes active break time, even
when a frame spans the break. Catch/mania retain their existing ruleset drain.
The layout is adapted to the legacy renderer; upstream blur/parallax shaders
are approximated rather than running on ES 1.1.

Version 0.106 adds explicit iOS audio-session activation and OpenAL context
suspend/process handling for foreground changes and interruptions. Failed
OpenAL source initialization now tears down the incomplete context for retry.
Bounded diagnostics report the first sample loads/play requests, source state,
gain and AL/ALC errors. Saved effects volume was 100%, master 20% on the
reported device; preferences are preserved. Host lifecycle and locking tests
pass, but the reported silence requires an audible device retest.

Version 0.105 places native song rows below the cached scene/notification UI,
and routes touches away from the native scroll view wherever notifications
cover it. Logo effects and debug rendering respect the same overlay order.
Song select and mods inherit the shared settings/home/music/profile/clock/bell
toolbar; four original ruleset icon buttons replace the now-playing title.
The separate Solo/mode bar is removed and song-list height grows from 144 to
176px. Selection retention and cross-ruleset conversion behaviour are unchanged.

Version 0.104 uses the original `Textures/Icons/notification.png` resource
(`OsuIcon.Notification`) and the upstream toolbar order: Guest → clock → bell.
Clock hit bounds retain their dynamic width and end before the bell.

Version 0.103 adds the local notification system adapted from lazer's
`ToolbarNotificationButton`, `NotificationOverlay`, `Notification` and
`ProgressNotification`: bell/unread badge, sliding drawer, history, toasts,
ongoing imports, dismissal/clear, details and playback recovery actions.
Routine toasts wait during gameplay/loading; critical alerts can bypass this.
Original UI notification samples are extracted from osu.Game.Resources
2026.918.0 and converted to 22050Hz mono PCM for the existing effects mixer.
Imports, managed playback/UI/background failures and native video fallbacks
feed the centre. Managed error details are retained in memory and logged to
Documents/legacy-errors.log (bounded to roughly 256KiB between resets).
History is limited to 100 completed/local entries; active tasks are retained.
Online account/push notifications and recovery from native process crashes
are not implemented by this local UI/error layer.

Version 0.102 restores the loader's FFT-driven native logo/ring. Gameplay audio
is prepared without starting its clock; two successful GPU presentations warm
the first gameplay scene before playback is committed. A negative lead-in
preserves the full first object's approach time (including early osu! notes),
and respects explicit map lead-in. Retry uses the same presentation gate.
Host regressions cover early notes, arbitrarily repeated frozen warm-up frames,
single commit, cancellation and presentation-counter wrap; device retest is pending.

Version 0.101 rebuilds the loader around upstream `PlayerLoader` and
`BeatmapMetadataDisplay`: centred pink logo, italic title/artist/difficulty,
thumbnail spinner, source/mapper/mods, right-hand collapsible toolboxes, back
and hold-for-menu controls, 500ms metadata delay, 1.8s minimum entry and 300ms
exit. Background blur is computed once per image rather than per frame.
Background dim and video controls work; unported skin/colour/offset/input
settings are disabled. The previous system font is retained (obliqued for
metadata), and no fabricated star rating is shown. This is a source-derived
480x320 adaptation, not pixel-identical shader/font output from the full client.

Version 0.100 adds a dedicated player loading screen. It is presented before
song preparation starts; map conversion, audio and hitsounds finish before
gameplay begins. Video gets a readiness gate with image fallback on timeout.
The loading screen supports cancellation back to the browser.

Versions 0.93–0.98 optimize presentation, batching, transforms, static slider/hit-circle
geometry, UI/library scheduling and repeated HUD/audio work. Device menu samples
improved from about 22fps to 44–48fps in v0.94; 60fps across all scenes is not yet
verified. Version 0.95 also corrects moving-logo taps and a reproduced settings
stack recursion crash. Version 0.96 fixes a Mono directory-scanning error-pointer
bug caught during device testing and speeds text submission. Version 0.97 reduces
per-frame video controller work while preserving video playback and immediate
timeline transitions. Version 0.98 repairs mixed font/background texture eviction
and moves intro presentation gating onto the main frame scheduler; device
retesting is pending. See [performance notes](port/performance-notes.md).

Version 0.92 preserves the selected song across ruleset tabs. Convertible
maps keep their exact difficulty; native maps select a compatible difficulty
from the same set and restore the original when returning. If no difficulty
is compatible, artwork/music selection is retained with Play unavailable.
Incremental imports do not replace that selection.

Version 0.91 adds mania: native 1K–18K maps and automatic osu! conversion
using lazer's original seeded pattern generators. Touch any column to press;
hold long notes and release at their tail. Simultaneous fingers play chords.
Gameplay includes six timing grades, normalized scoring, hold breaks, health,
mode-specific results/bests, pause/resume countdown, and NF/EZ/HR/SD/PF/MR.
The mania browser's `1.5s fall` control cycles between 1.0/1.5/2.0 seconds of
visible scrolling and persists across launches. Try `Mania first steps` for
the bundled 4K demo. Custom mania skins/replays and full mod parity remain
outside this version; see port/ThirdParty/mania-provenance.md.

Version 0.90 adds automatic osu! → taiko conversion: choose taiko in Solo
to include standard beatmaps. Short sliders become sample-coloured drum hits,
long sliders become drum rolls, and spinners become swells. Conversion follows
lazer's legacy timing and slider-velocity scroll changes; converted scores are
stored separately from osu! scores. All seven upstream taiko conversion
fixtures pass (65 objects, upstream 2ms timing tolerance). Native taiko still
works; mania conversion remains for a later update.

The requested deliverable is the full lazer experience. The legacy app
under `port/` is a runtime/gameplay prototype and does not satisfy that scope.
Version 0.12 introduced the landscape menu and was verified working by the
user. Version 0.13 added streaming `.osz` import, but device logs showed a launch
watchdog timeout. Version 0.15 reached the menu and imported the demo on device. Version 0.16 corrected the slider endpoint-table error, and the user confirmed
playback works. Version 0.17 adds song cover thumbnails and selected-song
backgrounds in browsing and gameplay; device verification is pending. Full gameplay remains
unverified and most client features are not present in that prototype.

Full-client work now lives under [full-client/](full-client/README.md), with a
working copy of the complete attached source and its exact framework source.
The original client has not yet been backported to ARMv6. Its complete host baseline and scalar-compatibility builds now succeed using
a workspace-local .NET 10 SDK. Ten scalar-math checks also compile against
the device core library and pass on host Mono. These are host verification
builds, not a full-client ARMv6 package. Feature preservation takes priority
over frame rate and loading time.

See [port/README.md](port/README.md) for the prototype's build instructions,
runtime fixes, source provenance, licences and device validation.

## Built artifacts

- `build/port/OsuLegacy-armv6-ios421-experimental.ipa`
- `build/port/OsuLegacy.app`
- `build/device-runtime-probes.tar.gz`

The app embeds Mono 3.0.12 and uses a batched OpenGL ES 1.1 renderer for the
PowerVR MBX Lite: circles, slider strokes, HUD text and textured images. UIKit
handles input, scrolling and native overlays; Core Graphics remains a fallback
if GL initialization fails. Textures are cached within a 6 MiB budget, and an
ASCII glyph atlases at native font sizes avoid rescaling small HUD text or
rasterizing it every frame. Shape edges use alpha coverage fringes for smoothing
without full-screen supersampling. Audio and timing use
AVAudioPlayer. It includes a generated demo beatmap.
The menu logo, FFT ring, rising triangles, side flashes, fireworks and song-list
rows also use batched OpenGL. Menu geometry is cached in a 480x320 framebuffer
texture when UI state changes; effects and scrolling share one onscreen
presentation with that cached texture. Gameplay draws directly without the menu
cache pass. Native display links animate menus without rebuilding managed UI
each frame. UIKit supplies touch/scroll physics and system dialogs; background
images and video use the system GPU compositor. CPU work still includes FFT,
geometry, input/game logic, media loading and initial glyph/texture preparation.

Settings > General > Debug > Performance overlay enables FPS, app CPU %, and
device GPU % at the bottom of the screen. It is off by default and persists.
FPS counts successful main UI presentations, including the menu visualizer;
CPU is process user + system CPU time divided by elapsed time. GPU uses a
recognized hardware utilization counter from IOKit's PerformanceStatistics
when exposed by the driver, otherwise displays N/A. It never substitutes
render-thread time for GPU utilization. Sampling runs once per second, pauses
while inactive and stops when disabled. Registry discovery runs once on a
background thread. The overlay reuses the existing GL presentation pass.
Lazer's OsuHitWindows scoring source is reused with a compatibility helper.
The legacy engine now includes sliders and spinners; the other ruleset engines
and full lazer-client parity remain unported.

Completed plays now transition after a one-second outro into a GPU results
scene adapted from lazer's expanded score panel: animated rank/accuracy rings,
total score, max/possible combo, judgement counts, mods, local personal-best
comparison, retry/back, and a statistics page with full-play tap timing and
accuracy graphs. Grade thresholds, grade colours, rank impact and applause
samples follow the supplied source. Results save once on entry; retry starts
fresh and Back preserves the played difficulty. This uses the port's current
score model, with no fabricated PP or online leaderboard data. See
`port/ThirdParty/results-provenance.md` for adaptation details.

GitHub's latest stable release was verified as 2026.1005.0-lazer on October 28,
2026, matching the supplied source snapshot. The app is a new port foundation;
the original net10.0 lazer solution has not been retargeted wholesale.

## Rebuild

With the downloaded dependencies in this workspace:

```sh
bash toolchain/build-mono.sh
bash port/build.sh
bash probes/package-runtime.sh
```

The SDK is local at `toolchain/iPhoneOS4.2.sdk`; the replacement linker is at
`build/toolchain/ld64`. These do not change the installed Xcode or .NET SDKs.
`mono-legacy/` is pinned to mono-3.0.12,
commit d6c5db881b26fd0f8ef64a65eba00e72fda552a2.
The archived SDK came from the iPhoneOS4.2.sdk directory of
https://github.com/Sn0wCooder/theos-sdks at commit
3111c86f53b021dabee781aec3fb2d9fb8f3f862.
The linker came from https://github.com/tpoechtrager/cctools-port at commit
904de2a71d4da6a9b30d2efaf912a10ddc7d9ddb.

## Experiments and logs

The installed Apple linker rejected iOS 4.2.1; a locally built cctools linker
removed that blocker. A hand-declared, SDK-free native probe also links
(`probes/build-without-sdk.sh`). The open-source reconstructed SDK at
https://github.com/touchHLE/common-3.0-sdk builds a small native probe but lacks
some Mach and pthread headers required by Mono, so the runtime build uses the
archived 4.2 SDK instead. Those intermediate dependencies remain under
`toolchain/` for investigation.

Build and validation logs are in `logs/`, particularly:

- `mono-reproducible-build.log`: native runtime and managed core-library build.
- `port-build.log`: app build, managed host assertions, signing and packaging.
- `runtime-package.log`: standalone device-probe staging.

The probes cover native execution, generated-code permissions and managed
execution/GC/threads/reflection. They are staged, not executed on the iPod.
USB now identifies the iPod and the installer reports Install: Complete.
The installed bundle is org.osu.legacy.experimental, displayed as osu legacy.
The app still needs physical-device launch, JIT, drawing, input, audio and
performance tests. SSH is reachable through USB but requires authentication.
