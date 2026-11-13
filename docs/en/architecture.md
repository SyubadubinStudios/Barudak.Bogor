# Architecture

Made by Ariana Mischa Fadhila of Syubadubin Studios. · [Bahasa Indonesia](../id/arsitektur.md)

## Stack and why

| Layer | Choice |
|---|---|
| Language and runtime | C# on .NET 10 |
| Rendering, audio | Three.Net 0.12.2: a Rust (wgpu) core behind a C ABI with a C# API. DirectX 12 on Windows |
| Window and interface | Avalonia UI 12; the 3D view is a `ThreeNetView` control with the HUD drawn over it |
| Models | Blender Python scripts (low-poly, no textures) and Rodin/Hyper3D (mission givers) |

The design document names Godot 4. Three.Net was chosen instead because the brief asks for .NET 10 with Rust underneath, it is available as a NuGet package with prebuilt natives, and it rendered correctly on the development machine. The game adds no Rust code of its own: the Rust part is the engine core.

## Two projects, one rule

```
src/BarudakBogor.Core   the whole game simulation; no rendering, no input devices, no UI
src/BarudakBogor.Game   the window: reads keys, steps the simulation, draws the result
tests/BarudakBogor.Core.Tests
```

**All game rules live in Core.** `GameSession.Update(dt, in InputState)` advances everything; the window only translates between the player, the session and the screen. That is what lets missions, physics and the economy be tested without a GPU.

```
keys/mouse ─► InputState ─► GameSession.Update ─► state ─► WorldRenderer / EntityRenderer / Atmosphere / HUD
                                   │
                                   └─► GameEvent queue ─► sound, toasts, banners, camera shake
```

## Core

| Folder | Contents |
|---|---|
| `World` | `WorldMap` (immutable after generation), `WorldGenerator`, `Terrain`, `RoadNetwork`, districts, places |
| `Sim` | `GameClock`, `WeatherSystem` |
| `Entities` | `Player`, `Vehicle`, `Population` (traffic, pedestrians, animals) |
| `Gameplay` | `GameSession`, `Progress`, events, markers, dialogue, `SaveSystem` |
| `Missions` | `Mission` base class with a step script, `MissionManager`, the ten story missions and three side missions |
| `Activities` | Fishing, the rhythm game behind badminton and karaoke, ojol, touring, photography, the bandrek business |
| `Localization` | `L(Id, En)`: every text carries both languages at the place it is written |

Things worth knowing before changing them:

- **Coordinates.** +X is east, +Z is south, +Y is up. Yaw 0 faces +Z and positive yaw turns toward +X, which is the left-hand side. `MathUtil.Forward`, `Left` and `YawOf` are the only places that encode this.
- **The world is a function of the seed.** Roads, buildings and places are generated once (about 1.5 s). Trees are derived per chunk from coordinate hashes, so any chunk can be built in any order with the same result.
- **Terrain height** is defined on an 8 m lattice; each cell is read as two triangles, exactly as the terrain mesh draws it, so gameplay height and drawn height agree.
- **A road is something to stand on.** A road is drawn flat from kerb to kerb at the height of its highest side, so across a slope it stands above the ground. `RoadNetwork.BuildSurfaces` works out the height profile of every stretch once when the world is made, and `Terrain.HeightAt` answers with the road surface in such places (`RoadNetwork.SurfaceAt`); `ChunkBuilder` draws the roads from the same `HeightAt`.
- **The real map.** Roads are loaded from `World/bogor_streets.txt`, made by `tools/osm/build_streets.py` from OpenStreetMap; the same script produces the points of the Cisadane and of the Sukabumi line.
- **Vehicles** use an arcade model: a bicycle steering model with separate forward and sideways grip. There is no rigid body solver. Traffic is moved along lanes kinematically and drives on the left. Each driver looks for whoever is in its lane (on the same stretch by distance along it, otherwise in a straight corridor ahead) and follows at stopping distance; a vehicle that stands or dawdles is passed by moving towards the middle of the road when nothing is coming; at a junction whoever would be hit in the side goes first. Above all that sits one hard rule: a step that would put a vehicle's box inside another one is taken back (`Population.UpdateTraffic`). Traffic lights (`TrafficSignal`, `WorldMap.Signals`) give each arm its turn on `Population.SignalTime`; the lit lamp is drawn by `EntityRenderer.DrawSignals`.
- **Missions** are linear scripts built from `Talk`, `Reach`, `Until`, `Ask`, `Do`. Anything a mission spawns or changes is registered with `OnEnd` so it is undone however the mission ends.
- **Events, not callbacks.** Gameplay code calls `session.Emit` or `session.Toast`; the window drains the queue once per frame.

## Game

| File | Role |
|---|---|
| `Rendering/WorldRenderer` | Streams 256 m chunks around the player; meshes are built on worker threads (`ChunkBuilder`) and uploaded a few per frame. Coarse 2 km tiles beyond give the mountains their outline |
| `Rendering/ChunkBuilder` | Terrain, a painted ground texture per chunk, road ribbons, buildings merged into one mesh per chunk, instanced trees and props |
| `Rendering/Textures` | Paints the building facade atlas and road textures at start-up; wetness and window glow are material parameters |
| `Rendering/EntityRenderer` | Pooled views for player, vehicles, people, animals, mission givers and beacons; walking and wheels are animated in code from named model parts; the player is a skinned mesh on a skeleton, moved by blended Blender clips (idle, walk, run, sit, and six acts: jump, swim, tread, fish, badminton, sing); water animals (`Animal.IsAquatic`) are floated at the surface by Core and only get a tail beat and a small bob here |
| `Rendering/Atmosphere` | Sun, sky, fog, rain streaks, lamp lights |
| `Rendering/CameraRig` | Third person camera |
| `Audio/GameAudio` | All sound is synthesised at start-up |
| `Views/MainWindow*.cs` | Frame loop, input, HUD, menus, credits, the `--capture` tour and `--bench` |
| `Views/MapControl`, `ActivityControl` | Custom-drawn map and mini game playfields |

## Assets

- `assets/blender/*.py` build `assets/models/*.glb`. Conventions the code relies on: metres, front faces glTF +Z, origin on the ground at the footprint centre, moving parts as named child nodes (`wheel_fl`, `leg_l`, `arm_r`, `head`, ...).
- `assets/models_hd` holds raw Rodin output; `optimize_rodin.py` decimates, rescales and shrinks textures into `assets/models`.
- `assets/art` holds the Rodin illustrations (key art, credits, portraits, icon, billboard).

## Performance notes

Measured with `--bench` at 1280 x 720 on Intel UHD 620, heavy rain, driving: Hemat 55-60 fps, Sedang 30 fps, Tinggi 30 fps (dynamic resolution on), with about 210 draw calls and 450 k triangles. Frames are pumped from the display refresh (`MainWindow.PumpFrame`) because the view's own `DispatcherTimer` rounds every frame up to the 15.6 ms Windows timer grid. The machine is pixel-bound: scene content barely moves the frame rate, resolution and post effects do.
