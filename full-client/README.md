# Full-client port workspace

The requested deliverable is the lazer client experience, not the circle-only
runtime prototype in `../port`. No full-client ARMv6 build exists yet.

Feature preservation takes priority over loading time and frame rate. The
compatibility build retains the original client projects and all four rulesets.
Features are not to be removed to obtain a successful build. Hardware-specific
implementations may be replaced with slower implementations of the same
functionality; unresolved dependencies must remain explicit blockers.

`osu/` is a working copy of the user's complete 2026.1005.0-lazer attachment.
`osu-framework/` is the matching framework tag 2026.921.1, commit
37c3e328c8cc7a018234ec78982ea12d3712096a. Each source tree retains its licence.
The original attachment is untouched. The only initial client build change is
an opt-in local framework project reference in `Directory.Build.targets`.

`bash build-baseline.sh` builds the original desktop client against that
framework source, including its original four rulesets. It requires a .NET 10
host SDK and package restore. This establishes a reference client; it does not
produce a package for iOS 4.2.1. Changing the target framework string alone will
not make it an ARMv6 port.

The local SDK is installed under `../toolchain/dotnet-10`; build scripts select
it without changing the system installation. SDK state and restored packages
are kept under `../build`. The complete baseline build now succeeds.

`bash build-compatibility.sh` runs the legacy scalar checks, then builds the
same complete host client with `LegacyScalarMath=true`. This replaces the
tensor operations in the actual framework's path-fitting implementation with
scalar loops and removes its direct System.Numerics.Tensors package reference.
The curve algorithms and public methods remain present. Ten checks cover
matrix arithmetic, required in-place updates, interpolation alias rejection,
and distance normalisation. The extracted scalar methods compile against the
bundled .NET 4 core library and execute on host Mono. This is not proof of
device execution of the full framework, which still uses other newer APIs.
Both complete host configurations build with zero errors; dependency warnings
are retained in `../logs/full-client-*-build.log` and the baseline log.

## Work required before an iPod package

1. Backport the framework and client language/runtime dependencies to the
   device runtime, or extend/replace that runtime. Both source projects target
   net10.0 and C# 14; Mono 3.0.12 and its .NET 4 library cannot load this graph.
2. Implement a legacy graphics backend beneath the existing framework scene
   graph. `GLRenderer` uses shader programs and uniform buffers. Preserve the
   original menu, song selection, HUD and ruleset drawables; unsupported shader
   effects need compatible rendering implementations and device validation.
3. Replace the platform host and audio backend while retaining the framework's
   clock, sample mixing, rate controls, input and threading contracts.
   AVAudioPlayer alone is not an equivalent replacement for these contracts.
4. Port storage/import, fonts and image decoding. The current graph includes
   Realm 20.1.0, SQLite, ImageSharp, BASS, SDL and other dependencies whose
   desktop/modern iOS binaries cannot simply be bundled on ARMv6.
5. Integrate original gameplay, including sliders, spinners, scoring, mods,
   replays, skins, settings, and all four rulesets. Validate against the source
   tests and physical-device runs rather than a separately simplified scorer.
6. Validate network/account services separately; do not represent offline
   gameplay as complete online feature parity.

Completion requires working navigation, import/library, complete gameplay,
results, settings and the agreed feature scope on the physical iPod, plus
measured loading time, frame timing and memory use. The existing prototype's
host tests establish none of these full-client requirements.
