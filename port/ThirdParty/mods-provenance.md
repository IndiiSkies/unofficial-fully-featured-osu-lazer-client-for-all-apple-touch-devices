# Mod behaviour references

The port's ModSettings/ModRuntime and integration were implemented against the
user-provided osu-2026.1005.0-lazer checkout under full-client/osu (ppy Pty Ltd,
MIT; original licence bundled with the port's other gameplay adaptations).

References include osu.Game/Rulesets/Mods/Mod{Muted,Classic,RateAdjust}.cs,
MetronomeBeat.cs; osu.Game.Rulesets.Osu/Mods/OsuMod*.cs;
osu.Game.Rulesets.Mania/Scoring/ManiaHitWindows.cs and the per-ruleset mod
catalogues. Mania Classic uses the original/native vs converted timing-window
formulas. Catch Relax directly positions the catcher. Blinds uses health and
the upstream .6*v*v+.4*v curve with the playfield's 10% leniency.

The existing port catalogue is the scope; new upstream mods are not added.
Default effects are implemented, but not every upstream mod-specific setting.
The shared Configure panel exposes rate, accuracy target, difficulty overrides
and random seed. Source incompatibilities and ruleset filtering are enforced.

Platform adaptations: Flashlight uses 8-pixel horizontal occlusion strips;
Depth uses projected 2D geometry; Blinds uses solid panels without upstream
panel textures/transition easing; Target Practice uses a simplified deterministic
beat generator. Object motion, visibility and cursor effects use the existing
ES1.1 transform stack. The implementations are not a claim of identical lazer
rendering/scoring, replay compatibility or multiplayer eligibility.

DT/HT use FFmpeg's atempo filter; NC/DC use asetrate/aresample, after Apple's
decoder (or the existing unsupported-codec fallback) prepares the input.
FFmpeg remains statically linked and its existing LGPL/source notices apply.
Rate processing is cached and completes on the loading worker before gameplay.
