# Results screen adaptation

`Managed/ResultsScreen.cs` adapts the supplied osu! 2026.1005.0-lazer results
presentation to the legacy 480x320 GPU surface. Source references in
`full-client/osu/osu.Game/`:

- `Screens/Play/Player.cs`: `RESULTS_DISPLAY_DELAY` of 1000 ms, completed
  judgement gating, and preventing a return to completed gameplay.
- `Screens/Ranking/ResultsScreen.cs` and `SoloResultsScreen.cs`: local score
  presentation, retry, rank-specific applause at volume 0.8.
- `Screens/Ranking/Expanded/ExpandedPanelMiddleContent.cs`: accuracy/rank
  circle, total score, maximum combo, and judgement statistics.
- `Screens/Ranking/Expanded/Accuracy/AccuracyCircle.cs`: 450 ms accuracy delay,
  3000 ms fill, OutPow10 easing, 1500 ms rank-reveal delay, 545 ms additional
  applause delay, rank-specific impact samples, and graded circle segments.
- `Rulesets/Scoring/ScoreProcessor.cs` and
  `../osu.Game.Rulesets.Osu/Scoring/OsuScoreProcessor.cs`: SS at 100%, S at 95%,
  A at 90%, B at 80%, C at 70%, and no S/SS when a miss is present.
- `Graphics/OsuColour.cs`: SS/S/A/B/C/D colours.
- `Rulesets/Scoring/HitEventExtensions.cs`: whole-play average offset and
  Welford population variance for unstable rate (`10 * standard deviation`).

The screen uses the port's existing `Session.TotalScore` and judgement model,
so it agrees with the live HUD and local scores. This does not substitute the
full lazer nested-judgement score processor or implement online rankings,
performance-point calculation, replay export, or storyboard outros. Graphs and
statistics describe the events the legacy engine actually records. No PP or
global leaderboard values are fabricated. Failed/abandoned plays do not enter
the successful results scene. No Fail plays may complete at zero health.

Code reference copyright: ppy Pty Ltd and osu! contributors, MIT; see
`osu-LICENCE`. Samples were extracted from the supplied official
`osu.Game.Resources.dll` and converted to mono 22050 Hz PCM for this device:
`Samples.Results.applause-{a,b,c,d,s}.wav`, `rank-impact-{fail-d,fail,pass,pass-ss}.wav`,
and `score-panel-top-appear.wav`. These resources are under the supplied
Attribution-NonCommercial 4.0 licence; see `osu-resources-LICENCE.md`. Their
content is unchanged apart from audio format conversion.
