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
# only some stops of the tour, for a quick look at one change (names are the Shot names in MainWindow.Capture.cs)
src/BarudakBogor.Game/bin/Release/net10.0/BarudakBogor.exe --capture scratch --only ciliwung,kolam
# frame rate at each quality level, written to a text file
src/BarudakBogor.Game/bin/Release/net10.0/BarudakBogor.exe --bench bench.txt

blender --background --python assets/blender/build_all.py        # rebuild the low-poly GLBs
blender --background --python assets/blender/build_all.py -- becak ayam   # only the named models
blender --background --python assets/blender/bake_palette.py     # after build_all: one palette texture per model, one draw call per part
blender --background --python assets/blender/rig_rodin.py        # raw Rodin characters -> animated parts in assets/models
blender --background --python assets/blender/rig_bones.py        # T-pose Rodin characters -> skinned <name>_rig.glb with idle/walk/run/sit clips
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
- The city follows the real Bogor. `WorldGenerator` holds real geometry in metres east/south of Tugu Kujang (from OpenStreetMap, ODbL; keep the credit): `RingPoints`, `RealRoads`, `RiverPoints`, the rail control points and `RealPlaces`. The streets of the centre are real too: `World/bogor_streets.txt` (embedded resource, one stretch per line, cut at junctions) is loaded by `LoadRealStreets`; `BuildRoads` then carries every main road that ends at the edge of that data into the hand-made country roads (`NoMainRoadJustStops` guards this). `Connect` links stranded pockets. Add a real place by adding a `RealPlace` row, a `PoiId`, a `Loc` name and a row in `RealBogorTests`.
- Rivers and railways are `Corridor`s added to `Terrain` before anything reads a height: they reshape the ground. `Terrain.LatticeHeight` is the earth (what the mesh draws); `Terrain.HeightAt` adds `Deck`s (bridges, the station platform), which is how roads cross the river. `BuildCrossings` creates bridges and level crossings for every road automatically.
- `Slab`s are plain boxes drawn into the chunk mesh (platform, columns, rooms); `Solid: true` gives them collision. `BuildRoom` makes a walk-in room; `Sign`s are name boards painted by `SignPainter`.
- Water uses `WaterShader.cs` (river ribbon uv.x bank to bank, uv.y downstream; ponds pass centre and radius in custom slots). Ground and roads use the WGSL surface shader in `GroundShader.cs` (layered detail, puddles; off at Hemat). The closed-vehicle first-person view swaps the vehicle model for the box-built cockpit in `EntityRenderer`.
- `WorldMap` is generated once from a seed (`WorldGenerator`) and is immutable afterwards. Per-chunk detail (trees) is derived from coordinate hashes, never from a shared `Random`, so chunks can be built in any order on worker threads.
- `Terrain.HeightAt` interpolates an 8 m lattice; `ChunkBuilder` builds the terrain mesh on the same lattice. Change both together or the ground and gameplay disagree. Near rivers and ponds both follow the generating function instead (`Terrain.WaterDetail`, weight 1 at the bank fading to 0), and the mesh draws those cells at 2 m (`Terrain.FineCell`), which is what gives the water a smooth shoreline.
- `WorldRenderer` streams 256 m chunks: `ChunkBuilder.Build` runs on worker threads and must not touch the engine; uploads happen on the render thread.
- Missions (`Missions/StoryMissions.cs`) are linear scripts of `Talk` / `Reach` / `Until` / `Ask` / `Do` steps. Whatever a mission spawns or changes (vehicles, locked weather, clock speed) must be registered with `OnEnd`; `EveryMissionStartsAndCanBeAbandonedCleanly` checks for leaks.
- Text is `L(Id, En)` (`Localization/Loc.cs`): both languages are written side by side at the point of use. Interface strings are in `Views/Ui.cs`.

## Conventions the code depends on

- World axes: +X east, +Z south, +Y up. Yaw 0 faces +Z; positive yaw turns toward +X, which is **left**. Steering input positive = left. Only `MathUtil.Forward`, `Left`, `YawOf` encode this.
- Traffic drives on the left.
- Models: metres, front faces glTF +Z, origin on the ground at the footprint centre. Moving parts are named child nodes found by `ModelLibrary.FindPart`: `wheel_fl/fr/rl/rr`, `wheel_f/r`, `wheel_l/r`, `leg_l/r`, `arm_l/r`, `head`, `leg_fl/fr/rl/rr`, `tail`. The crowd, riders and animals are animated in code from these parts. The player is skeletal: `rig_bones.py` writes `<name>_rig.glb` (one skinned mesh, 12 bones, clips `idle`/`walk`/`run`/`sit`), loaded by `ModelLibrary.SpawnRigged` and mixed by `EntityRenderer.AnimateRig`. A skinned model must be imported once per instance (`Node.Clone` shares the deforming mesh), which is why the crowd does not use it. Animation players are applied in the order they were started and are never stopped.
- Rodin characters come as one fused mesh. `rig_rodin.py` slices them into `torso`, `head`, `arm_l/r`, `leg_l/r` (T-pose sources get arms; A-pose sources only legs) so the same code animation works. Do not run a full `build_all.py` without checking it will not overwrite those files.
- Open vehicles (motorbike, delman, becak) get a rider model from `EntityRenderer.RiderOf` whenever they are occupied by someone other than the player.
- A GLB loads as one node per material primitive, so the low-poly models are baked by `bake_palette.py` to a single palette-textured material per part (a character is 6 draw calls). `papan_reklame` is left unbaked because the game swaps a poster onto its blank face. Static scenery goes through `ModelLibrary.Primitives` and `Node.SetInstances`.
- Frames are pumped by `MainWindow.PumpFrame` from `RequestAnimationFrame`; `Viewport.IsRendering` stays false. The view's own `DispatcherTimer` snaps to the 15.6 ms Windows timer grid and costs up to a third of the frame rate.
- Controllers: the button layout is `PadInput.Merge` in Core (unit tested); `MainWindow.Gamepad.cs` only polls the engine and handles window-level buttons. The `--capture` chase scene drives through a virtual pad. Menus are pad-navigable (`HandleGamepadMenus`); button edges are tracked by the window, not the engine.
- `CameraRig` has a chase view (pulls in for buildings, tree trunks and vehicles) and a first-person view (`V`). In closed vehicles it sits in the driver seat (`CameraRig.SeatOf`).
- Nobody gets hurt: pedestrians dodge vehicles. Keep the tone comedic and non-violent.
- Credit line in docs and in the app: "Dibuat oleh Ariana Mischa Fadhila dari Syubadubin Studios" (`Loc.Credit`).
- Documentation is bilingual: `README.md` holds both languages, `docs/en` and `docs/id` mirror each other, and screenshots live in `docs/images`.

## Gotchas

- `--capture` and `--bench` never write the player's save or settings (`MainWindow.Scripted`). Keep it that way.
- The game draws through its own `GameViewport` (not the engine's `ThreeNetView`) so dynamic resolution can resize the target without rebuilding the renderer. `Program.Main` raises the Windows timer resolution to 1 ms; without it Avalonia locks a 14 ms frame to 30 fps.
- Target hardware includes integrated graphics (Intel UHD 620: about 60 fps at Hemat, 30 at Sedang, 20 at Tinggi with dynamic resolution). That machine is pixel-bound: resolution and post effects matter, scene content barely does. `--bench` reports real frame time and per-frame work; `F3` shows draw calls in game.
- Ray marched clouds (`ThreeNet.Effects.CloudLayer`, Tinggi only, set up in `Atmosphere`) come from Three.Net 0.12.2, which is not published yet: the game restores `ThreeNet 0.12.2-clouds.1` from `packages-local/` (`nuget.config`) and keeps `ThreeNet.Native` 0.12.1. Rebuild that package from the Three.Net checkout with `dotnet pack src/ThreeNet -c Release -p:SkipRustBuild=true -p:VersionPrefix=0.12.1 -p:PackageVersion=0.12.2-clouds.1 -o <game>/packages-local` (the assembly version must stay 0.12.1 or the native assembly fails to load), and delete `~/.nuget/packages/threenet/0.12.2-clouds.1` so the new one is picked up.
- The Bash tool here mangles heredocs containing apostrophes; write files with the editor tools.
- The user allows fixes to Three.Net itself in `C:\experiment\VibeCoding\Vibe_Graphics\ThreeNet` when the engine is the problem (the game currently uses the published NuGet packages, not that checkout).
- Asset tools: Blender MCP for modelling (the live Blender crashed once when a script overrode the view layer of a new scene; prefer `blender --background` for batch work) and Rodin MCP for generated 3D and 2D art (each generation costs the user credits). Raw Rodin GLBs are far too heavy to ship; always run them through `optimize_rodin.py`.
