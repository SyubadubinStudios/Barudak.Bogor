# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

**Barudak Bogor**: an open-world, GTA-style game set in Bogor, Indonesia. .NET 10, the Three.Net engine (Rust/wgpu core, consumed as NuGet packages `ThreeNet`, `ThreeNet.Avalonia`, `ThreeNet.Native` 0.12.1) and Avalonia 12 for the window and HUD. `Barudak.Bogor.txt` is the brief and `solution-design.md` the game design (both Indonesian). `PLAN.md` is the roadmap and `Progress.md` the dated log: update both when meaningful work lands.

## Commands

```bash
dotnet build BarudakBogor.slnx -c Release
dotnet run --project src/BarudakBogor.Game -c Release
dotnet test tests/BarudakBogor.Core.Tests
dotnet test tests/BarudakBogor.Core.Tests --filter "FullyQualifiedName~PrologueCanBePlayedThrough"   # single test

# scripted screenshot tour through the real game loop; the docs' pictures come from this
src/BarudakBogor.Game/bin/Release/net10.0/BarudakBogor.exe --capture docs/images
# frame rate at each quality level, written to a text file
src/BarudakBogor.Game/bin/Release/net10.0/BarudakBogor.exe --bench bench.txt

blender --background --python assets/blender/build_all.py        # rebuild the low-poly GLBs
blender --background --python assets/blender/build_all.py -- becak ayam   # only the named models
blender --background --python assets/blender/bake_palette.py     # after build_all: one palette texture per model, one draw call per part
blender --background --python assets/blender/rig_rodin.py        # raw Rodin characters -> animated parts in assets/models
blender --background --python assets/blender/optimize_rodin.py   # raw Rodin props -> assets/models
```

## Release

- `.github/workflows/ci.yml` tests Core and builds the game on every push and pull request.
- `.github/workflows/release.yml` builds self-contained packages for win-x64 (Inno Setup installer + zip), linux-x64 (.deb + .tar.gz), osx-arm64 and osx-x64 (.dmg). Pushing a tag `vX.Y.Z` publishes them as a GitHub release; "Run workflow" only uploads them as artifacts.
- Packaging scripts live in `packaging/` and take the publish folder as an argument, so they can be run by hand.
- Only Windows has been run on real hardware. Android, iOS and consoles would need their own app heads and are not built.

To verify a visual change, run `--capture` into a scratch folder and look at the PNGs. Add a stop to the tour in `Views/MainWindow.Capture.cs` when a new screen needs covering.

## Architecture

- **`src/BarudakBogor.Core` holds every game rule and has no rendering, input or UI.** `GameSession.Update(dt, in InputState)` steps the whole simulation. `src/BarudakBogor.Game` is only a view: it reads keys into an `InputState`, steps the session, and draws. Keep it that way; it is why the tests can play missions headlessly.
- Core to view communication is one-way: the view reads session state each frame and drains `GameSession.DrainEvents` (sound, toasts, banners). Gameplay code never calls into the view; it uses `session.Emit` / `session.Toast`.
- `WorldMap` is generated once from a seed (`WorldGenerator`) and is immutable afterwards. Per-chunk detail (trees) is derived from coordinate hashes, never from a shared `Random`, so chunks can be built in any order on worker threads.
- `Terrain.HeightAt` interpolates an 8 m lattice; `ChunkBuilder` builds the terrain mesh on the same lattice. Change both together or the ground and gameplay disagree.
- `WorldRenderer` streams 256 m chunks: `ChunkBuilder.Build` runs on worker threads and must not touch the engine; uploads happen on the render thread.
- Missions (`Missions/StoryMissions.cs`) are linear scripts of `Talk` / `Reach` / `Until` / `Ask` / `Do` steps. Whatever a mission spawns or changes (vehicles, locked weather, clock speed) must be registered with `OnEnd`; `EveryMissionStartsAndCanBeAbandonedCleanly` checks for leaks.
- Text is `L(Id, En)` (`Localization/Loc.cs`): both languages are written side by side at the point of use. Interface strings are in `Views/Ui.cs`.

## Conventions the code depends on

- World axes: +X east, +Z south, +Y up. Yaw 0 faces +Z; positive yaw turns toward +X, which is **left**. Steering input positive = left. Only `MathUtil.Forward`, `Left`, `YawOf` encode this.
- Traffic drives on the left.
- Models: metres, front faces glTF +Z, origin on the ground at the footprint centre. Moving parts are named child nodes found by `ModelLibrary.FindPart`: `wheel_fl/fr/rl/rr`, `wheel_f/r`, `wheel_l/r`, `leg_l/r`, `arm_l/r`, `head`, `leg_fl/fr/rl/rr`, `tail`. Characters and animals are animated in code from these parts; there is no skeletal animation.
- Rodin characters come as one fused mesh. `rig_rodin.py` slices them into `torso`, `head`, `arm_l/r`, `leg_l/r` (T-pose sources get arms; A-pose sources only legs) so the same code animation works. Do not run a full `build_all.py` without checking it will not overwrite those files.
- Open vehicles (motorbike, delman, becak) get a rider model from `EntityRenderer.RiderOf` whenever they are occupied by someone other than the player.
- A GLB loads as one node per material primitive, so the low-poly models are baked by `bake_palette.py` to a single palette-textured material per part (a character is 6 draw calls). `papan_reklame` is left unbaked because the game swaps a poster onto its blank face. Static scenery goes through `ModelLibrary.Primitives` and `Node.SetInstances`.
- Frames are pumped by `MainWindow.PumpFrame` from `RequestAnimationFrame`; `Viewport.IsRendering` stays false. The view's own `DispatcherTimer` snaps to the 15.6 ms Windows timer grid and costs up to a third of the frame rate.
- Nobody gets hurt: pedestrians dodge vehicles. Keep the tone comedic and non-violent.
- Credit line in docs and in the app: "Dibuat oleh Ariana Mischa Fadhila dari Syubadubin Studios" (`Loc.Credit`).
- Documentation is bilingual: `README.md` holds both languages, `docs/en` and `docs/id` mirror each other, and screenshots live in `docs/images`.

## Gotchas

- Target hardware includes integrated graphics (about 30 fps on Intel UHD 620 at Hemat/Sedang, 18 at Tinggi). That machine is pixel-bound: resolution and post effects matter, scene content barely does. `--bench` reports real frame time and per-frame work; `F3` shows draw calls in game.
- The Bash tool here mangles heredocs containing apostrophes; write files with the editor tools.
- The user allows fixes to Three.Net itself in `C:\experiment\VibeCoding\Vibe_Graphics\ThreeNet` when the engine is the problem (the game currently uses the published NuGet packages, not that checkout).
- Asset tools: Blender MCP for modelling (the live Blender crashed once when a script overrode the view layer of a new scene; prefer `blender --background` for batch work) and Rodin MCP for generated 3D and 2D art (each generation costs the user credits). Raw Rodin GLBs are far too heavy to ship; always run them through `optimize_rodin.py`.
