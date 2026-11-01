# UI asset attribution

Original osu! logo and menu background: ppy / osu!resources contributors.
Source: https://github.com/ppy/osu-resources
Commit: d481bc98f2603348ee81c15174cd72604286310d
Package: ppy.osu.Game.Resources 2026.918.0, required by the attached lazer source.

- `Assets/logo.png`: `osu.Game.Resources.Textures.Menu.logo.png`
- `Assets/menu-background.jpg`: `osu.Game.Resources.Textures.Menu.menu-background-1.jpg`
- `Assets/demo-background.jpg` (also copied to the demo archive as `background.jpg`): `osu.Game.Resources.Textures.Menu.menu-background-2.jpg`

The original asset bytes are unchanged. Images are scaled at draw time to fit
the legacy device. Resource licence: CC BY-NC 4.0; full text is bundled as
`osu-resources-LICENCE.md`. This experimental build is for noncommercial use.

The navigation, button colours and parallelogram layout are adapted from
`osu.Game/Screens/Menu/ButtonSystem.cs`, `MainMenuButton.cs`, and `OsuLogo.cs`
in the user's 2026.1005.0-lazer source, licensed under MIT. The UIKit adapter
is implemented in `Managed/LazerUi.cs` and `Native/App.m`; the original
osu.Framework drawable classes are not yet running on the device.

Menu destinations for unported editor/network services explicitly report
their current status. Their UI entry points do not implement those services.

Version 0.23 adds an ARMv6 adaptation of upward-moving equilateral triangle particles from `osu.Game/Graphics/Backgrounds/TrianglesV2.cs` (MIT). `Native/MenuTriangles.m` uses 24 persistent Core Animation layers sharing an 80×70 outline bitmap, with independent speeds and initial phases. They appear over the cached menu background below its dim layer. Positions move without per-frame managed rendering or image decoding. Animate freezes/resumes their clock; leaving Home/Play/Edit hides and pauses them, as does loss of application focus. The particle count, size and opacity are adapted for the 480×320 device; this is not the modern triangle-border shader.
