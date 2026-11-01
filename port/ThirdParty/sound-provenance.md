# Sound effects

Bundled effects come from ppy.osu.Game.Resources 2026.918.0,
commit d481bc98f2603348ee81c15174cd72604286310d of
https://github.com/ppy/osu-resources .

Sources: `osu.Game.Resources.Samples.Gameplay.{normal,soft,drum}-*.wav`,
`Gameplay.spinnerbonus.wav`, `Gameplay.spinnerspin.wav`,
`Gameplay.combobreak.mp3`, and `Menu.button-default-select.wav`.
The copies in `port/Sounds` are converted to 22050 Hz mono signed 16-bit
PCM WAV using ffmpeg, with no intentional duration change. These are
CC BY-NC 4.0 assets; see the bundled osu-resources-LICENCE.md.

The legacy playback adapter uses OpenAL with 16 overlapping one-shot voices
and four sustained-effect voices, separate from the AVAudioPlayer song track.
Samples decode during song preparation, with an 8 MiB PCM budget, at most 128
buffers, and at most eight seconds per sample. Oversized, missing, unsupported,
or corrupt custom samples fall back to bundled sounds. iOS system decoding
supports WAV and other platform formats; Ogg custom samples currently fall
back. Custom sounds use the beatmap directory only, with symlink and traversal
checks. The master Volume setting controls both music and effects.

Implemented: successful circle/slider head hits, slider ticks/repeat/tail hits,
slider slide/whistle while tracking, spinner spin/bonus/end, combo breaks at
20 or higher, and menu button selection. Beatmap hit masks, normal/addition
banks, per-edge slider samples, timing sample index/volume and explicit custom
hit filenames are read. Effects stop on focus loss and leaving gameplay.
Spinner pitch does not yet track rotation speed. This adapter does not yet
implement storyboard sample events, custom skins, positional audio, or all
lazer sample-control-point edge cases.
