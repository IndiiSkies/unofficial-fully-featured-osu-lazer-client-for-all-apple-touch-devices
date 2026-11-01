# Slider and spinner implementation provenance

The ARMv6-compatible implementation in `Managed/Beatmap.cs`, `SliderPath.cs`,
`Session.cs` and `GameplayView.cs` adapts algorithms and behavior from the
attached osu! 2026.1005.0-lazer source and its framework 2026.921.1.
Original work: ppy Pty Ltd and osu! contributors, MIT. See `osu-LICENCE`.

Reference sources:

- `osu.Framework/Utils/PathApproximator.cs`: adaptive Bezier subdivision with
  0.25-pixel tolerance, flat-curve approximation, Catmull interpolation at 50
  samples per segment, and circular-arc approximation at 0.1-pixel tolerance.
- `osu.Game/Rulesets/Objects/SliderPath.cs`: distance-based traversal, trimming
  and extension to expected length, with repeated-endpoint extension behavior.
- `osu.Game/Rulesets/Objects/Legacy/LegacyRulesetExtensions.cs`: inherited SV
  clamping and precision-adjusted beat length.
- `osu.Game.Rulesets.Osu/Objects/Slider.cs` and
  `osu.Game/Rulesets/Objects/SliderEventGenerator.cs`: span timing, tick spacing,
  repeat placement, 10 ms tick exclusion near an endpoint, and tail lenience.
- `osu.Game.Rulesets.Osu/Objects/Drawables/DrawableSlider.cs`: the classic
  aggregated result based on hit nested parts (all / at least half / some / none).
- `osu.Game.Rulesets.Osu/Objects/Spinner.cs`: difficulty-based clear and maximum
  RPM ranges, spin count, bonus gap and cap.
- `osu.Game.Rulesets.Osu/Skinning/Default/SpinnerRotationTracker.cs`: angle
  deltas normalized to [-pi,pi] while held during the spinner interval.
- `osu.Game.Rulesets.Osu/Objects/Drawables/DrawableSpinner.cs`: completion
  thresholds of 1, .9, and .75 and bonus-spin accounting.

Adaptation details: the legacy implementation uses a small double-coordinate
type and arrays/lists rather than osuTK, spans, bindables and drawables. It
adds a clamped-uniform de Boor evaluator for explicit B-spline degrees and a
UIKit multitouch adapter. Curves are computed off the UI thread. Native
CoreGraphics strokes, circles and arcs provide the visual representation.

This implements playable standard sliders/spinners in the legacy backend;
it does not claim complete lazer ruleset parity. Slider accuracy currently
uses classic aggregation within the existing per-object scoring model.
Full lazer nested accuracy, stacking/notelock parity, hit-sample playback,
spinner history/smoothing, replay integration and mod interactions still
require work. Direct touch rotation has a small centre dead zone to avoid
counting coordinate jitter; releasing/repositioning resets the angle baseline.
