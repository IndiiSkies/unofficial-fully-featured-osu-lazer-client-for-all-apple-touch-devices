# Mania (v0.91)

Source: supplied osu!lazer 2026.1005.0, MIT licence (osu-LICENCE).

`build-mania-converter.py` builds the original Pattern, PatternGenerator,
LegacyPatternGenerator, HitCirclePatternGenerator, SliderPatternGenerator,
SpinnerPatternGenerator, PatternType and LegacyRandom algorithms. Mechanical
compatibility changes replace unavailable modern BCL helpers and storage
types. `ManiaConversionCompat.cs` supplies the converter orchestration, data
adapters and minimal enumeration helpers against the existing Mono corlib.
It adds no System.Core or modern .NET runtime requirement. The reference
source files are not modified.

`ManiaConversionAdapter.cs` preserves native column positions (including
legacy native spinners), converts osu! patterns using the source's seeded RNG,
density, key-count selection, slider segmentation, chords and hold rules.
`check-mania-conversion.py` checks note count, times and columns against all
21 enabled upstream conversion fixtures, including native 1K–18K layouts.
It does not compare the fixture's intermediate RNG state snapshots.

`ManiaRuleset.cs` adapts ManiaHitWindows (six grades, floor + .5ms),
DrawableHoldNote (head/body/tail, 1.5 release lenience and broken-tail cap),
OrderedHitPolicy, ManiaScoreProcessor (305 accuracy for Perfect; logarithmic
combo portion and nonlinear accuracy portion), and ManiaHealthProcessor
(legacy HP gain calibration, zero passive drain). Pause has a two-second
resume countdown and allows preholding columns, with a 500ms reacquisition
grace for active holds. This grace and touchscreen column sliding are port
adaptations rather than claims of source equivalence.

GPU visuals use column primitives, coloured note bars, holds, key lighting,
judgement text and a bounded hit-error display. They approximate the modern
skin without its buffered shaders. Imported custom mania skins, replays,
dual-stage visual separation, audio-rate mods, and the complete mania mod
catalogue are not implemented. Supported mods are NF, EZ, HR, SD, PF, MR.
Native high-key-count maps are accepted, but the iPod's touchscreen limits
how many simultaneous contacts a player can physically hold.
