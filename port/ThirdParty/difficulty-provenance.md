# Local star difficulty (v0.121)

Uses the attached osu!lazer source, MIT licensed. build-difficulty.py compiles
its Skill/StrainSkill/StrainDecaySkill/VariableLengthStrainSkill/HarmonicSkill,
DiffUtils, all four modes' preprocessing, skills and evaluators, osu! stacking,
and taiko final aggregation into a corlib-only Difficulty.dll. Source calculator
versions are 20260706 for osu!, taiko and catch; mania is 20241007.

Compatibility replacements cover LINQ, list interfaces, vector arithmetic,
Math.Clamp/Cbrt, tuples, and missing runtime APIs. Original evaluator source is
unchanged. Framework beatmap objects are adapted from this port's parser,
slider paths/nested events, catch converter and mania legacy converter.
DifficultyEngine computes star output; unrelated performance-point, legacy
score simulation and ancillary difficulty attributes are omitted. The unused
osu! no-slider aim and taiko monochrome stamina passes are omitted because they
only populate ancillary attributes, not the final star rating.

One low-priority background worker processes the latest selected map; rapid
selection supersedes older work. No audio decoding, artwork loading, gameplay
health calibration or rendering happens during calculation. Parser, converter
and evaluation checkpoints honour the existing native gameplay import pause.
In-memory results avoid repeated work; per-map/mode/mod results persist in
Documents/DifficultyCache with calculator version, file size and modification
time. Edited maps are explicitly invalidated. Conversion and supported mod
geometry use the same implementation as gameplay. Native mania sorting uses
lazer's LegacySortHelper with rounded start times.

Tests compare 20 generated difficulty inputs across the four modes, clock
rates and HD/FL against a .NET 10 build using system LINQ/math; they also test
local loading, nested slider objects, conversions, mods, persistence,
invalidation, cancellation and cooperative pause. This verifies evaluator
compatibility, not full-client end-to-end parity on every beatmap: parser,
curve flattening and gameplay converter differences may still affect results.
Online cards continue to display mirror-provided ratings.
