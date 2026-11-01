# Taiko conversion

The v0.90 osu! → taiko adapter in Managed/Beatmap.cs follows the attached
osu!lazer TaikoBeatmapConverter.cs (MIT licence): hidden 1.4f velocity factor,
integer drum-roll duration, short-slider split threshold, version-dependent
tick spacing, cyclic edge samples, and slider-velocity scroll changes.
Circles retain whistle/clap rim and finish strong-hit samples; spinners use
the existing source-derived swell required-hit formula in Session.cs.

Tests/check-taiko-conversion.py checks all seven committed upstream taiko
conversion fixtures for count, timing (upstream 2ms tolerance), type and strength.
This validates conversion; it does not claim full taiko gameplay parity.
Native taiko remains supported. Other source rulesets and mania conversion are
outside this change. Converted scores use a separate |taiko key.
