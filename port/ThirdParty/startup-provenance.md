# Triangles intro and menu theme (version 0.21)

The requested sequence is `IntroTriangles`, not the distinct `IntroWelcome`
implemented in version 0.20. Source: attached osu!lazer
`osu.Game/Screens/Menu/IntroTriangles.cs` and
`osu.Game/Graphics/Sprites/LogoAnimation.cs` (MIT; bundled osu-LICENCE).

Resource source: ppy.osu.Game.Resources 2026.918.0, osu-resources commit
`d481bc98f2603348ee81c15174cd72604286310d`.
https://github.com/ppy/osu-resources

The offline renderer `port/render-triangles-intro.py` consumes these originals:

- `Fonts.Torus.Torus-Light.bin` and `Torus-Light_0.png`: original glyph metrics
  and atlas for the welcome text.
- `Textures.Icons.Ruleset{Osu,Taiko,Catch,Mania}.png`: four ruleset icons.
- `Textures.Intro.Triangles.logo-{highlight,background}.png`: original animated
  logo textures. `Shaders.sh_LogoAnimation.fs` stores appearance progress in
  red and alpha in green. The renderer applies that threshold per frame,
  rather than displaying the encoded texture as an ordinary image.
- `Tracks.triangles.osz` / `audio.mp3`: **cYsmix – triangles**, including the
  spoken welcome and the following theme. Converted to 44100 Hz, 128 kbps
  AAC-LC M4A; the complete 126-second track is retained.

Resource licence: CC BY-NC 4.0; see the accompanying
`osu-resources-LICENCE.md`. These assets are used for this noncommercial port.

Source timeline in milliseconds: 200 "wel", 400 "welcome", 700 "welcome to",
900 "welcome to osu!", 1060 glitch triangles, 1450/1650/1850 ruleset icon
stages, 2080 animated logo, 3000 menu reveal, then a 1000 ms white flash.
The logo uses the source's 920 ms progress ramp, quadratic parent shrink,
and late quintic zoom. Font sizing and positions are adapted to 480x320.
Triangle glitches use a fixed random seed rather than changing each launch.

`Startup/triangles-intro.mp4` is a silent 480x320, 30 fps, three-second H.264
Constrained Baseline level 3.0 rendering of those stages. MPMoviePlayerController
provides the iOS 4 video decoder, avoiding the modern graphics framework and
per-frame managed image drawing. Native UIKit supplies the concluding flash.
The combined voice/theme starts when movie playback starts and continues
through menu entry. These are two media players and are not sample-locked.
A tap skips to the menu/theme at three seconds. A decoder error or eight-second
timeout also reaches the menu. Menu music pauses for gameplay and resumes on
return. Focus loss pauses media and the timeout. Volume applies to the theme.
The earlier welcome WAVs and welcome-text image are retained as source assets
for the previous experiment but are not used by the current intro.

## Version 0.22 startup ordering

Managed callbacks register before the intro request. The request only marks the intro active and queues setup for the next UIKit run-loop turn, after managed initialization returns. An opaque intro surface must get two display-link callbacks and the movie decoder must report playable/play-through readiness before playback is requested. Playback and music start at zero. Focus loss blocks this readiness gate; skip/timeout cancel it. `StartupGateTests.c` exercises both event orders, focus changes and cancellation (58 checks). Physical screen presentation still requires a device launch check.
