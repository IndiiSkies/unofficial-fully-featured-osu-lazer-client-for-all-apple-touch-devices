# Performance work: versions 0.93–0.99

Target: 60 presentations/second for animated scenes (16.67ms/frame), without
disabling effects, lowering image quality, dropping notes or changing scoring.
This is a target, not a verified guarantee across scenes or beatmaps.

Device: iPod2,1 / iOS 4.2.1; driver reports PowerVR MBXLite with VGPLite,
OpenGL ES-CM 1.1 (76). Current observations are captured in
`logs/device-performance-093.log` (includes multiple successive builds).

## Measured before/after

- User baseline: about 21.9fps on main menu, about 15fps in gameplay.
- v0.93 steady menu: approximately 22–23fps; scene construction about 6–7ms,
  presentation call about 30–34ms. Gameplay samples showed construction about
  34–37ms with presentation about 2ms. These are different scenes, not a single
  universally GPU-bound or CPU-bound workload.
- v0.94 steady menu: approximately 44–48fps in repeated 5-second samples;
  construction about 5–8ms, presentation about 11–13ms. Menus during interactions
  still dipped. No claim of 60fps or broad gameplay improvement is supported
  by those measurements.
- v0.95 adds direct GPU rendering and one frame owner, plus HUD/audio work
  reductions. Osu! gameplay reached about 50–55fps in lighter sections, with
  denser sections around 34–40fps. This still does not meet the target.
- v0.96 additionally caches fixed hit-circle geometry, speeds ASCII glyph
  submission and avoids repeated font-key creation. It protects glyph atlases
  from image churn within the same texture budget. Further device verification
  is required; it is not a 60fps guarantee.
- v0.97 addresses video-map slowdown: render frames publish the song clock
  instead of querying AVPlayer/AVAsset on every frame. The existing 100ms
  controller timer keeps playback and drift correction running; the initial
  clock, backward/large forward jumps, and video start/end boundaries still
  request immediate control updates. Video duration is cached from its track.
  Resolution, decoder and video frame rate are unchanged.
- v0.98 fixes the v0.97 native crash at `cachedTexture` line 110: glyph-aware
  eviction selected a background key but removed index zero from the order
  list, leaving the two containers inconsistent. Both now remove the same
  retained key, covered by a mixed glyph/background regression test. Intro
  presentation and reveal gates now run on the main frame scheduler instead
  of separate display links, which never advanced in the affected launches.
  Intro playback and video-map performance still require device retesting.
- v0.98 device PID 1307: intro presentation, playback and natural completion
  are logged successfully; no cache crash in that captured session. Browser
  entry has a roughly 12-second gap, then 2.2fps / 344.79ms average build;
  existing aggregate counters cannot identify that gap's exact operation.
  Song preparation reports 8.0s first load and 5.7s second load, including
  repeated rejected custom WAV effects. Video gameplay reaches 27.4fps in a
  light section, drops to 11.4fps, then runs around 20–23fps. Video presentations
  commonly cost 16–29ms, so moving controller work alone has not solved it.
- v0.99 moves ExtAudioFile I/O/conversion outside the playback lock; only
  buffer publication remains locked. A generation check rejects a load if
  reset occurred during decoding. A production-code concurrency regression
  blocks sample reading and verifies volume/reset remain responsive and the
  stale sample is discarded. Threshold timing logs now distinguish background
  polling, artwork preparation, music preparation, video setup, and GPU
  build/presentation stalls. This removes a proven locking hazard, but the
  exact cause of the captured browser gap still needs a new timed session.

`OSU frame` logs count actual screen presentations. `build` includes managed
scene construction and OpenGL submission; `present` includes driver, compositor
and synchronization waits. Neither is a direct GPU utilisation percentage.
Idle screens render on changes; their low presentation count is not an
animation performance measurement.

## Changes

- Replaced the main 30Hz NSTimer with a 60Hz CADisplayLink. Main scene,
  visualizer and scrolling now share that link and submit a single screen
  presentation. Drawing directly into GLES avoids UIKit bitmap redraw for
  normal frames; Core Graphics remains a fallback.
- Compose normal backgrounds and dimming inside an opaque GLES surface.
  Hide the redundant native image/dim layers. Beatmap video retains its
  system decoder layer and uses a transparent foreground; visibility changes
  request presentation so the first decoded frame is revealed promptly.
- Bake model transforms into submitted vertices, avoiding a GL batch flush
  for each fruit push/pop. Same tessellation, colours, AA fringes and order.
- Batch ordered managed shape commands in a reused 64KiB float buffer. Text,
  images and paths flush the stream at their original ordering boundaries.
  ES vertex attributes were already floats; gameplay timing/math remain doubles.
- Reuse constant visualizer directions, decay calculations, catch random
  appearance values and fruit layouts. Use bounded subpixel arc recurrence;
  retain the original number of segments. Reuse spinner/arrow scratch buffers.
- Cache both unchanged slider body layers and fixed hit-circle layers in vertex buffers, with a bounded
  2MiB LRU cache and exact original tessellation. Very large paths or allocation
  failures use the original direct path. The common geometry cache stays at
  2MiB. Moving balls, approach circles, numbers, ticks and input remain live.
- Show the song browser shell before filtering/row preparation, retain unchanged
  grouped rows and serialized native rows, and drain imports in smaller batches.
  Library polling is outside gameplay drawing; gameplay still pauses importing.
- Cache HUD/leaderboard formatting until its values change. Avoid reactivating an
  already-current OpenAL context and querying/stopping already-empty loop slots.
- Correct moving-logo hit priority and request redraw on input release as well
  as press. Regression covers the logo overlapping a menu button.
- Reproduced a v0.94 settings input sequence that makes the settings overlay its
  own background and exhausts the stack. Unified settings entry and guarded the
  backdrop; regression covers overlapping press/release events. Device reports
  at 23:01 and 23:05 show stack guard faults; old-build host repro also exits -11.
- A later crash has a managed trace in MonoIO.FindNext while scanning beatmaps.
  Fixed Mono 3.0.12's `error = ERROR_SUCCESS` typo to `*error = ERROR_SUCCESS`;
  the former makes a native enumeration error write through a null pointer.
  Rebuilt the ARMv6 runtime library. Test exercises the actual source function's
  success, end-of-directory and native-error paths with UBSan. ASan could not
  complete in this host sandbox, so that checker uses the working UBSan path.

## Validation

Build includes 10,000 transform/nested-transform comparisons and arc subpixel
error checks, command bounds validation, mesh budget bounds, existing AA/path/
glyph tests, 70 UI checks, and gameplay, import-gate, audio, playlist and scoring
regressions. Catch's 27,350 fixture objects, taiko's 65 objects and mania's 2,671
objects retain their upstream conversion checks.
Hit-circle cache preparation is compared vertex-for-vertex (including colour
and alpha) against the previous layered primitives at multiple radii in both
effects modes. Video clock tests exercise continuous publication and immediate
offset, seek, restart and end transitions.

The opaque single-surface path follows Apple's compositing recommendations:
https://developer.apple.com/library/archive/documentation/3DDrawing/Conceptual/OpenGLES_ProgrammingGuide/Performance/Performance.html
