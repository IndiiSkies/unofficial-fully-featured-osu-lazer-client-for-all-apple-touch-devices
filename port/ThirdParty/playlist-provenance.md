# Local music playlist (version 0.27)

Implemented locally in Managed/MenuPlaylist.cs, LazerUi.cs, Program.cs and the
native Startup.m audio adapter. This is a local background-music queue, not
osu!'s online multiplayer playlist service.

The header has previous, pause/resume, next and list controls. Play > playlists
opens the same panel. All songs deduplicates imported difficulties by their
resolved audio path. My playlist supports adding, removing and moving entries.
Shuffle uses a non-repeating bag; repeat supports off, all and one. Previous
restarts after three seconds, otherwise uses history. The panel offers +/-10s
seeking and a position bar. Custom order, selected track, repeat/shuffle and
queue mode persist in Documents/menu-playlist.cfg using relative document paths.

After library loading, background music selects a saved track or the first
imported supported song, preserving the startup intro. The generated click
track demo is omitted. End-of-track advances the queue. User pause persists
through gameplay suspend/resume. No iTunes media-library permission is required;
these are audio files already imported with beatmaps.

Supported by this iOS 4 adapter: MP3, WAV, M4A/AAC, AIFF and CAF, subject to the
system decoder. Ogg/Vorbis files are not included in this version's music queue.
Unavailable/corrupt files report an inline playback error; automatic sequential
advance skips up to eight candidates to bound main-thread work.

The visualizer now switches to the chosen file. Its low-priority worker creates
an 11025 Hz mono PCM cache using ExtAudioFile with the software codec selected,
leaving the playback hardware codec available to AVAudioPlayer. Playback starts
without waiting for this analysis cache; the ring can take time to activate on
first play. Cached tracks need no repeated decoding. Cache entries incorporate
path, file size and modification time, use atomic temporary-file rename, and
are bounded to 64 MiB. Decoding is cancelled after a track switch, hiding the
menu effects, disabling Animate, or backgrounding. New-generation results
replace old ones so one song does not display another song's spectrum.

Native controls, playback end events and position updates use the existing
cheap polling path, without restoring continuous menu text redraws. Header
state repaints on track changes; the playlist position display refreshes at
most once per second. The existing system fonts remain unchanged.

## Version 0.28 song-browser previews

Song browsing pauses the title playlist and plays the selected beatmap through a separate preview AVAudioPlayer. PreviewTime is interpreted in milliseconds (zero when missing), clamped to the file duration. Selecting another difficulty with the same audio path does not restart the preview. Empty/unsupported selections stay silent rather than leaking playlist audio into the browser. Import notices retain browser context. Back stops the preview and resumes the playlist at its preserved position, respecting user pause. Gameplay stops the preview before opening its own audio player, and returning to song selection restarts the selected preview. The browser header displays the selected track instead of title-playlist transport controls.

## Version 0.29 immediate spectrum windows

The earlier uncached-song path waited for full-song PCM conversion. This is replaced by a retained ExtAudioFile decoder that reads only a 512-sample, playback-aligned window. Existing PCM caches and the bundled theme sidecar still use pread. No new whole-song cache is required or written. The normal 20 Hz gap is discarded sequentially through the retained converter; source-frame seeks are reserved for jumps, backward movement or source changes. Source changes sample the newest requested playback time after decoder setup and discard prior-generation output. Analysis remains off the playback thread. Software decoder selection is preferred on iOS, with automatic selection as a fallback when unsupported. Host tests cover WAV, MP3 and AAC, bounded decoded-frame counts, converter reuse, forward/backward seeks and EOF (144 checks). CoreAudio codec helpers require an unsandboxed host test in Codex. Actual first-frame delay on ARMv6 is measured by device logs, not asserted from host results.
