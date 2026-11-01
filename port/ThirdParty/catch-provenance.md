# Catch backport, source and verification

Reference: supplied osu!lazer 2026.1005.0 tree, `osu.Game.Rulesets.Catch`.
The C# compatibility adapter is original port code derived from these MIT
licensed source implementations (the repository licence is already bundled):

- `Beatmaps/CatchBeatmapConverter.cs`, `Objects/JuiceStream.cs`,
  `Objects/BananaShower.cs`, `Beatmaps/CatchBeatmapProcessor.cs`:
  fruit/stream/shower conversion, tiny-droplet interpolation including the
  legacy last tick, seed 1337 RNG consumption, HR position offsets and
  hyperdash detection.
- `osu.Game/Utils/LegacyRandom.cs`, `StatelessRNG.cs`: legacy geometry RNG
  and stateless visual randomness, adapted to the legacy compiler.
- `UI/Catcher.cs`, `CatcherArea.cs`, `CatchTouchInputMapper.cs`: CS size,
  inclusive catch bounds, walk/dash speeds, hyperdash target velocity and
  overshoot, frame ordering and touch edge mapping.
- `Scoring/CatchScoreProcessor.cs`, `CatchHealthProcessor.cs`, shared
  `LegacyDrainingHealthProcessor.cs`: normalized score/combo formula,
  tiny-droplet accuracy and score contribution, banana bonuses, rank
  thresholds, health calibration and miss/gain categories.
- `UI/Catcher.cs`: plate stacking lenience, combo explosion/drop timings.
- `Skinning/Default`: catcher, fruit pulp formations and border sizes,
  droplets/bananas; `CatchPlayfieldAdjustmentContainer` supplies the
  screen geometry and -100..340 legacy fall interval.
- `Screens/Play/DelayedResumeOverlay`: two-second, three-count resume.

The original catcher idle/fail/kiai PNGs and catch-banana / resume-countdown
sounds are extracted from the user's supplied `osu.Game.Resources.dll` by
`port/extract-catch-assets.cs`. Resources retain the existing osu!resources
CC BY-NC licence and attribution bundled with the app. Catcher PNGs are
reduced to 128 pixels and mirrored for the two directions. Audio is mono
22.05 kHz PCM, with pitch changes submitted through OpenAL.

`Tests/check-catch-conversion.py` compares against all 37 committed upstream
conversion fixtures, including HR/EZ cases. It checks nested object counts,
times, positions and hyperdash flags using upstream's 3-unit tolerance.
Clock-rate mods do not change conversion geometry, so their presence in
fixture names does not test or imply playback-rate support.

`CatchTests.cs` separately checks movement, bounds, scoring, tiny misses,
bananas, hyperdash, health/failure, retry, result statistics and score keys.
`SoundTests.cs` exercises catch stream samples and pitched countdown cues.

## Remaining parity limits

This is not a verified pixel-for-pixel or complete ruleset-mod port.
OpenGL ES 1 renders simplified glow passes rather than lazer's buffered
shaders. Replay import/export, custom catch skins, the full catch mod
catalogue and playback-rate mods are not implemented by this client.
The port currently applies NF/EZ/HR/HD/SD/PF catch behaviour; other inherited
mod catalogue entries must not be interpreted as catch parity. Native skin
comparisons and touch feel still require testing on the physical iPod.

These limits are recorded explicitly so conversion test success is not
misrepresented as full 1:1 gameplay/UI/mod/replay equivalence.
