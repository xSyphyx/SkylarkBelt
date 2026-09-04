# Skylark Belt

A client-side [Pulsar](https://github.com/SpaceGT/Pulsar) plugin for Space Engineers that
draws a **true wireframe torus** as a navigation marker around a ring-shaped region.

It was built for the Sigma Draconis Expanse "Belt" instance — an 8,500 km asteroid ring
around Sol with a 500 km tube — but the ring center, major radius, tube radius, and plane
are all configurable, so it works for any toroidal zone.

The stock [NavMarkers](https://steamcommunity.com/sharedfiles/filedetails/?id=3363175955)
mod can only draw spheres; a ring approximated by a string of spheres beads and scallops up
close. This plugin draws the actual torus with the same transparent-line primitive NavMarkers
uses, so it sits visually alongside the spherical zone markers.

- **Render-only and client-side.** Nothing is sent to the server; it draws in your client
  exactly like the NavMarkers zone spheres.
- **Distance-aware.** Line thickness scales with camera distance, and segments fade out as
  you fly inside the ring so the wireframe never fills the screen.
- **Multi-target.** Builds for both `net48` (Pulsar Legacy) and `net10.0` (Pulsar Interim).

## Install

**From the Pulsar plugin list** (once approved): open the Plugins menu on the Space Engineers
main menu, search for **Skylark Belt**, enable it, and restart when prompted.

**Directly from this repo:** in the Pulsar plugin list, add this repository by ID —
`xSyphyx/SkylarkBelt` — then enable it.

By default the torus only draws in worlds whose name is **Expanse**. Use `/belt here` in any
other world to add it to the draw list.

## Chat commands

All settings are persisted to the config file.

| Command | Effect |
|---|---|
| `/belt` | Toggle the torus on/off |
| `/belt on` / `/belt off` | Explicit set |
| `/belt info` | Ring/tube dimensions and your distance from the ring centerline |
| `/belt here` | Add/remove the current world from the draw list |
| `/belt reload` | Re-read the config file and rebuild geometry |
| `/belt color <#hex>` | Line color as `#RRGGBB` (e.g. `#5EF10D`), or `#RRGGBBAA` to set opacity too |
| `/belt scale <v>` | Line thickness per meter of distance (default `0.0015` ≈ 1 px at 1080p) |
| `/belt alpha <0-255>` | Line opacity (default `40`, matching NavMarkers' subtle look) |

## Configuration

`%AppData%\SpaceEngineers\Storage\SkylarkBelt.cfg` — created with defaults on first run. Edit
it (with the game running is fine), then `/belt reload` in-game.

- **Geometry:** `CenterX/Y/Z`, `MajorRadius`, `MinorRadius` (meters), `NormalX/Y/Z` (the torus
  plane normal; default `0,1,0` = the Y=0 ecliptic).
- **Density:** `ToroidalRings` (rings around the tube, default 6), `ToroidalSegments`,
  `PoloidalCircles` (cross-section circles, default 36), `PoloidalSegments`.
- **Look:** `ColorR/G/B/A`, `Material`, `Blend` (`Standard` / `AdditiveBottom` / `AdditiveTop`
  / `PostPP`), `ThicknessScale`, `Min`/`MaxThickness`.
- **Fade:** `FadeNearMeters` / `FadeFullMeters` fade out segments near the camera.
- **Worlds:** `Worlds` — session names the torus draws in. Empty = all worlds.

On worlds where the NavMarkers mod is loaded you can set `Material` to `NavMarkerLines` for an
exact visual match with its zone spheres. `Square` (the default) works everywhere.

## Building

Requires the .NET Framework 4.8 developer pack and the .NET 10 SDK. `Directory.Build.props`
auto-detects your Space Engineers install from Steam; each successful build deploys into
Pulsar's `Local` plugin folder automatically.

```
dotnet build ClientPlugin/ClientPlugin.csproj -c Release
```

## Bugs

Please report issues on the [GitHub issue tracker](https://github.com/xSyphyx/SkylarkBelt/issues).

## License

MIT — see [LICENSE](LICENSE).
