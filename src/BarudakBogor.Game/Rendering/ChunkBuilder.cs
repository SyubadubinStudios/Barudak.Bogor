using System.Numerics;
using BarudakBogor.Numerics;
using BarudakBogor.World;

namespace BarudakBogor.Game.Rendering;

/// <summary>CPU side of one streamed chunk, ready to be uploaded.</summary>
public sealed class ChunkData
{
    public const int GroundTextureSize = 64;

    public required int X { get; init; }

    public required int Z { get; init; }

    /// <summary>False for a distant chunk: plain building shells, trees and landmarks only.</summary>
    public required bool Detailed { get; init; }

    public Vector3 Origin => new(X * WorldMap.ChunkSize, 0f, Z * WorldMap.ChunkSize);

    public MeshData Terrain { get; } = new();

    public byte[] GroundPixels { get; } = new byte[GroundTextureSize * GroundTextureSize * 4];

    public MeshData RoadMain { get; } = new();

    public MeshData RoadToll { get; } = new();

    public MeshData RoadGang { get; } = new();

    public MeshData Asphalt { get; } = new();

    public MeshData Buildings { get; } = new();

    /// <summary>River surface.</summary>
    public MeshData Water { get; } = new();

    /// <summary>Railway track bed.</summary>
    public MeshData Rail { get; } = new();

    /// <summary>Model name to instance transforms, relative to <see cref="Origin"/>.</summary>
    public Dictionary<string, List<Matrix4x4>> Instances { get; } = [];
}

/// <summary>
/// Turns the abstract <see cref="WorldMap"/> into meshes, one 256 m chunk at a time. Pure CPU
/// work with no engine calls, so it runs on worker threads while the game keeps rendering.
/// </summary>
public static class ChunkBuilder
{
    private const int Cells = (int)(WorldMap.ChunkSize / Terrain.Cell);
    private const float RoadLift = 0.12f;

    /// <param name="detail">
    /// True for chunks near the player. A distant chunk gets the same ground and roads, but its
    /// buildings are plain shells (one quad per wall instead of one per window) and it leaves out
    /// everything small: street furniture, stalls, bushes.
    /// </param>
    public static ChunkData Build(WorldMap map, int chunkX, int chunkZ, bool detail)
    {
        ChunkData data = new() { X = chunkX, Z = chunkZ, Detailed = detail };
        Vector2 min = new(chunkX * WorldMap.ChunkSize, chunkZ * WorldMap.ChunkSize);
        Vector2 max = min + new Vector2(WorldMap.ChunkSize);
        BuildTerrain(map, data, chunkX, chunkZ);
        PaintGround(map, data, min);
        BuildRoads(map, data, min, max);
        BuildCorridors(map, data, min, max);
        BuildBoulders(map, data.Buildings, min, max);
        BuildStructures(map, data.Buildings, min, max);
        map.ForEachBuilding(min, max, building => AddBuilding(data.Buildings, building, min, detail));
        AddInstances(map, data, chunkX, chunkZ, min, max, detail);

        return data;
    }

    // ------------------------------------------------------------------ terrain

    private static void BuildTerrain(WorldMap map, ChunkData data, int chunkX, int chunkZ)
    {
        Terrain terrain = map.Terrain;
        int baseX = chunkX * Cells;
        int baseZ = chunkZ * Cells;
        int side = Cells + 3;
        float[] heights = new float[side * side];
        for (int j = 0; j < side; j++)
        {
            for (int i = 0; i < side; i++)
            {
                heights[(j * side) + i] = terrain.LatticeHeight(baseX + i - 1, baseZ + j - 1);
            }
        }

        MeshData mesh = data.Terrain;
        for (int j = 0; j <= Cells; j++)
        {
            for (int i = 0; i <= Cells; i++)
            {
                int k = ((j + 1) * side) + i + 1;
                float dx = heights[k + 1] - heights[k - 1];
                float dz = heights[k + side] - heights[k - side];
                Vector3 normal = Vector3.Normalize(new Vector3(-dx, 2f * Terrain.Cell, -dz));
                mesh.Add(new Vector3(i * Terrain.Cell, heights[k], j * Terrain.Cell), normal, new Vector2(i / (float)Cells, j / (float)Cells));
            }
        }

        int row = Cells + 1;
        const int fine = (int)(Terrain.Cell / Terrain.FineCell);
        const int fineSide = fine + 3;
        float[] patch = new float[fineSide * fineSide];
        for (int j = 0; j < Cells; j++)
        {
            for (int i = 0; i < Cells; i++)
            {
                Vector2 centre = new((baseX + i + 0.5f) * Terrain.Cell, (baseZ + j + 0.5f) * Terrain.Cell);
                if (terrain.WaterDetail(centre, Terrain.Cell * 0.75f) > 0f)
                {
                    // A shoreline needs more than an 8 m lattice: this cell is drawn at 2 m, from
                    // the same heights gameplay reads. Away from water the detail fades to nothing,
                    // so the edge of a fine cell lies exactly on its coarse neighbour.
                    int fx = (baseX + i) * fine;
                    int fz = (baseZ + j) * fine;
                    for (int m = 0; m < fineSide; m++)
                    {
                        for (int k = 0; k < fineSide; k++)
                        {
                            patch[(m * fineSide) + k] = terrain.GroundAt((fx + k - 1) * Terrain.FineCell, (fz + m - 1) * Terrain.FineCell);
                        }
                    }

                    uint first = 0;
                    for (int m = 0; m <= fine; m++)
                    {
                        for (int k = 0; k <= fine; k++)
                        {
                            int q = ((m + 1) * fineSide) + k + 1;
                            Vector3 normal = Vector3.Normalize(new Vector3(patch[q - 1] - patch[q + 1], 2f * Terrain.FineCell, patch[q - fineSide] - patch[q + fineSide]));
                            float lx = (i * Terrain.Cell) + (k * Terrain.FineCell);
                            float lz = (j * Terrain.Cell) + (m * Terrain.FineCell);
                            uint index = mesh.Add(new Vector3(lx, patch[q], lz), normal, new Vector2(lx / WorldMap.ChunkSize, lz / WorldMap.ChunkSize));
                            if (m == 0 && k == 0)
                            {
                                first = index;
                            }
                        }
                    }

                    for (int m = 0; m < fine; m++)
                    {
                        for (int k = 0; k < fine; k++)
                        {
                            uint fa = first + (uint)((m * (fine + 1)) + k);
                            uint fb = fa + 1;
                            uint fd = fa + (uint)(fine + 1);
                            uint fc = fd + 1;
                            mesh.Triangle(fa, fd, fb);
                            mesh.Triangle(fb, fd, fc);
                        }
                    }

                    continue;
                }

                uint a = (uint)((j * row) + i);
                uint b = a + 1;
                uint d = a + (uint)row;
                uint c = d + 1;
                mesh.Triangle(a, d, b);
                mesh.Triangle(b, d, c);
            }
        }
    }

    private static void PaintGround(WorldMap map, ChunkData data, Vector2 min)
    {
        const int size = ChunkData.GroundTextureSize;
        float texel = WorldMap.ChunkSize / size;
        byte[] pixels = data.GroundPixels;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                Vector2 p = min + new Vector2((x + 0.5f) * texel, (y + 0.5f) * texel);
                LandUse use = map.LandUseAt(p);
                float n = Noise.Value(p.X / 34f, p.Y / 34f, 3u);
                float fine = Hash.Unit((int)p.X, (int)p.Y, 8u);
                Vector3 color = use switch
                {
                    // Paddies: a patchwork of flooded, green and ripening fields.
                    LandUse.Sawah => Paddy(p),
                    LandUse.Hutan => new Vector3(0.13f, 0.27f, 0.14f),
                    LandUse.KebunTeh => new Vector3(0.2f, 0.4f, 0.17f),
                    LandUse.Taman => new Vector3(0.24f, 0.45f, 0.19f),
                    LandUse.Kota => new Vector3(0.4f, 0.4f, 0.38f),
                    LandUse.Pasar => new Vector3(0.42f, 0.38f, 0.33f),
                    LandUse.Kampung => new Vector3(0.4f, 0.36f, 0.29f),
                    LandUse.Gang => new Vector3(0.38f, 0.36f, 0.33f),
                    LandUse.Kampus => new Vector3(0.28f, 0.44f, 0.2f),
                    LandUse.Modern => new Vector3(0.3f, 0.46f, 0.24f),
                    _ => new Vector3(0.22f, 0.4f, 0.19f),
                };
                if (use is LandUse.Kota or LandUse.Pasar or LandUse.Kampung or LandUse.Gang)
                {
                    // Yards and vacant lots break up the paving: patches of grass and bare earth.
                    float yard = Noise.Value(p.X / 19f, p.Y / 19f, 12u);
                    if (yard > 0.62f)
                    {
                        color = Vector3.Lerp(color, new Vector3(0.27f, 0.42f, 0.2f), MathUtil.SmoothStep(0.62f, 0.72f, yard));
                    }
                    else if (yard < 0.3f)
                    {
                        color = Vector3.Lerp(color, new Vector3(0.44f, 0.36f, 0.27f), MathUtil.SmoothStep(0.3f, 0.2f, yard) * 0.8f);
                    }
                }

                color *= 0.86f + (n * 0.2f) + (fine * 0.06f);

                float clearance = map.Terrain.CorridorClearance(p, out CorridorKind corridor);
                if (map.Terrain.WaterLevelAt(p) is not null)
                {
                    // The bed: silt in mid-stream, paler gravel toward the banks.
                    color = Vector3.Lerp(new Vector3(0.2f, 0.19f, 0.13f), new Vector3(0.42f, 0.38f, 0.28f), MathUtil.Saturate(1f + (clearance / 9f))) * (0.85f + (fine * 0.3f));
                }
                else if (clearance < 9f && corridor == CorridorKind.River)
                {
                    // Banks: wet mud at the water, shingle and scrub above it.
                    Vector3 bank = Vector3.Lerp(new Vector3(0.3f, 0.25f, 0.17f), new Vector3(0.46f, 0.42f, 0.33f), MathUtil.Saturate(clearance / 5f)) * (0.85f + (fine * 0.3f));
                    color = Vector3.Lerp(bank, color, MathUtil.SmoothStep(4f, 9f, clearance));
                }
                else if (clearance < 5f && corridor == CorridorKind.Rail)
                {
                    // Ballast spills a little way either side of the sleepers.
                    color = Vector3.Lerp(new Vector3(0.38f, 0.36f, 0.33f) * (0.8f + (fine * 0.4f)), color, MathUtil.SmoothStep(1.5f, 5f, clearance));
                }
                else if (map.Roads.Nearest(p, 22f) is { } road)
                {
                    // Verges: pavement in town, trodden earth in the country.
                    float edge = MathF.Abs(road.Lateral) - (road.Edge.Width * 0.5f);
                    float blend = 1f - MathUtil.SmoothStep(1.5f, 6f, edge);
                    bool urban = use is LandUse.Kota or LandUse.Pasar or LandUse.Modern or LandUse.Kampus or LandUse.Gang;
                    Vector3 verge = urban ? new Vector3(0.5f, 0.49f, 0.46f) : new Vector3(0.42f, 0.34f, 0.24f);
                    color = Vector3.Lerp(color, verge * (0.9f + (fine * 0.12f)), blend);
                }

                int k = ((y * size) + x) * 4;
                pixels[k] = (byte)(Math.Clamp(color.X, 0f, 1f) * 255f);
                pixels[k + 1] = (byte)(Math.Clamp(color.Y, 0f, 1f) * 255f);
                pixels[k + 2] = (byte)(Math.Clamp(color.Z, 0f, 1f) * 255f);
                pixels[k + 3] = 255;
            }
        }
    }

    private static Vector3 Paddy(Vector2 p)
    {
        int fieldX = (int)MathF.Floor(p.X / 46f);
        int fieldZ = (int)MathF.Floor(p.Y / 34f);
        float kind = Hash.Unit(fieldX, fieldZ, 21u);
        Vector3 color = kind < 0.25f ? new Vector3(0.3f, 0.36f, 0.3f)
            : kind < 0.75f ? new Vector3(0.3f, 0.52f, 0.2f)
            : new Vector3(0.58f, 0.55f, 0.24f);
        // Bunds between the fields.
        float bx = MathF.Abs((p.X / 46f) - fieldX - 0.5f);
        float bz = MathF.Abs((p.Y / 34f) - fieldZ - 0.5f);
        return bx > 0.45f || bz > 0.44f ? new Vector3(0.36f, 0.3f, 0.2f) : color;
    }

    // ------------------------------------------------------------------ roads

    private static void BuildRoads(WorldMap map, ChunkData data, Vector2 min, Vector2 max)
    {
        Terrain terrain = map.Terrain;
        HashSet<int> junctions = [];
        map.Roads.ForEachSegment(min, max, (edge, segment) =>
        {
            MeshData mesh = edge.Kind switch
            {
                RoadKind.Gang => data.RoadGang,
                RoadKind.Toll => data.RoadToll,
                _ => data.RoadMain,
            };
            Vector2[] points = edge.Points;
            Vector2 a = points[segment];
            Vector2 b = points[segment + 1];
            Vector2 ta = Tangent(points, segment);
            Vector2 tb = Tangent(points, segment + 1);
            float va = edge.DistanceAtVertex(segment) / 12f;
            float vb = edge.DistanceAtVertex(segment + 1) / 12f;
            float half = edge.Width * 0.5f;

            // Two pieces per segment so the ribbon hugs the terrain lattice.
            Vector2 mid = (a + b) * 0.5f;
            Vector2 tm = Vector2.Normalize(ta + tb);
            Ribbon(mesh, terrain, min, a, ta, va, mid, tm, (va + vb) * 0.5f, half);
            Ribbon(mesh, terrain, min, mid, tm, (va + vb) * 0.5f, b, tb, vb, half);

            if (segment == 0)
            {
                junctions.Add(edge.From);
            }

            if (segment == points.Length - 2)
            {
                junctions.Add(edge.To);
            }
        });

        foreach (int id in junctions)
        {
            RoadNode node = map.Roads.Nodes[id];
            if (node.Edges.Count < 2 || node.Position.X < min.X || node.Position.X >= max.X || node.Position.Y < min.Y || node.Position.Y >= max.Y)
            {
                continue;
            }

            // A paved disc hides the seams where ribbons of different roads meet.
            float radius = node.MaxWidth * 0.62f;
            MeshData mesh = data.Asphalt;
            Vector2 c = node.Position;
            uint centre = mesh.Add(Local(c, terrain.HeightAt(c) + RoadLift + 0.03f, min), Vector3.UnitY, new Vector2(0.5f, 0.5f));
            const int sides = 12;
            uint first = 0;
            for (int i = 0; i < sides; i++)
            {
                float angle = i * MathUtil.Tau / sides;
                Vector2 p = c + (new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius);
                uint index = mesh.Add(Local(p, terrain.HeightAt(p) + RoadLift + 0.03f, min), Vector3.UnitY, new Vector2(0.5f + (MathF.Cos(angle) * 0.4f), p.Y / 12f));
                if (i == 0)
                {
                    first = index;
                }
                else
                {
                    // Angles run from +X toward +Z, which is clockwise seen from above.
                    mesh.Triangle(centre, index, index - 1);
                }
            }

            mesh.Triangle(centre, first, first + sides - 1);
        }
    }

    private static Vector2 Tangent(Vector2[] points, int index)
    {
        Vector2 before = points[Math.Max(index - 1, 0)];
        Vector2 after = points[Math.Min(index + 1, points.Length - 1)];
        Vector2 d = after - before;
        return d.LengthSquared() > 1e-6f ? Vector2.Normalize(d) : Vector2.UnitY;
    }

    private static void Ribbon(MeshData mesh, Terrain terrain, Vector2 origin, Vector2 a, Vector2 ta, float va, Vector2 b, Vector2 tb, float vb, float half)
    {
        Vector2 la = a + (MathUtil.Left(ta) * half);
        Vector2 ra = a - (MathUtil.Left(ta) * half);
        Vector2 lb = b + (MathUtil.Left(tb) * half);
        Vector2 rb = b - (MathUtil.Left(tb) * half);
        // The whole cross-section rides on the highest of its three samples so the verge never pokes through.
        float ha = MathF.Max(terrain.HeightAt(a), MathF.Max(terrain.HeightAt(la), terrain.HeightAt(ra))) + RoadLift;
        float hb = MathF.Max(terrain.HeightAt(b), MathF.Max(terrain.HeightAt(lb), terrain.HeightAt(rb))) + RoadLift;
        mesh.Quad(
            Local(ra, ha, origin), Local(rb, hb, origin), Local(lb, hb, origin), Local(la, ha, origin),
            Vector3.UnitY,
            new Vector2(1f, va), new Vector2(1f, vb), new Vector2(0f, vb), new Vector2(0f, va));
    }

    private static Vector3 Local(Vector2 p, float y, Vector2 origin) => new(p.X - origin.X, y, p.Y - origin.Y);

    // ------------------------------------------------------------------ river, railway, bridges

    private static bool Inside(Vector2 p, Vector2 min, Vector2 max) => p.X >= min.X && p.X < max.X && p.Y >= min.Y && p.Y < max.Y;

    /// <summary>The water of a river and the bed of a railway, as ribbons along their lines.</summary>
    private static void BuildCorridors(WorldMap map, ChunkData data, Vector2 min, Vector2 max)
    {
        Terrain terrain = map.Terrain;
        foreach (Corridor corridor in map.Corridors)
        {
            bool river = corridor.Kind == CorridorKind.River;
            MeshData mesh = river ? data.Water : data.Rail;
            // The water reaches a little under the banks so no gap shows at the edge.
            float half = river ? corridor.HalfWidth + 3f : corridor.HalfWidth;
            float repeat = river ? 24f : 4f;
            Vector2[] points = corridor.Points;
            for (int i = 0; i < points.Length - 1; i++)
            {
                if (!Inside((points[i] + points[i + 1]) * 0.5f, min, max))
                {
                    continue;
                }

                Vector2 a = points[i];
                Vector2 b = points[i + 1];
                Vector2 sideA = MathUtil.Left(Tangent(points, i)) * half;
                Vector2 sideB = MathUtil.Left(Tangent(points, i + 1)) * half;
                float ya = corridor.Levels[i];
                float yb = corridor.Levels[i + 1];
                if (!river)
                {
                    // Like a road, the track rides on the highest of its three samples.
                    ya = MathF.Max(ya, MathF.Max(terrain.HeightAt(a), MathF.Max(terrain.HeightAt(a + sideA), terrain.HeightAt(a - sideA)))) + 0.2f;
                    yb = MathF.Max(yb, MathF.Max(terrain.HeightAt(b), MathF.Max(terrain.HeightAt(b + sideB), terrain.HeightAt(b - sideB)))) + 0.2f;
                }

                float va = corridor.DistanceAtVertex(i) / repeat;
                float vb = corridor.DistanceAtVertex(i + 1) / repeat;
                mesh.Quad(
                    Local(a - sideA, ya, min), Local(b - sideB, yb, min), Local(b + sideB, yb, min), Local(a + sideA, ya, min),
                    Vector3.UnitY,
                    new Vector2(1f, va), new Vector2(1f, vb), new Vector2(0f, vb), new Vector2(0f, va));
            }
        }
    }

    /// <summary>Boulders in the river and on its banks, as the Ciliwung has all the way down from Puncak.</summary>
    private static void BuildBoulders(WorldMap map, MeshData mesh, Vector2 min, Vector2 max)
    {
        foreach (Corridor corridor in map.Corridors)
        {
            if (corridor.Kind != CorridorKind.River)
            {
                continue;
            }

            for (float d = 0f; d < corridor.Length; d += 7f)
            {
                Vector2 at = corridor.Sample(d, out Vector2 direction, out float level);
                uint h = Hash.Of((int)d, 17, map.Seed);
                if (!Inside(at, min, max) || Hash.Unit(h) > 0.55f)
                {
                    continue;
                }

                // Mostly near the banks, now and then one in mid-stream.
                float side = (Hash.Unit(Hash.Mix(h)) * 2f) - 1f;
                float reach = MathF.Sign(side) * MathF.Pow(MathF.Abs(side), 0.4f) * (corridor.HalfWidth + 1.5f);
                Vector2 p = at + (MathUtil.Left(direction) * reach);
                if (map.Bridges.Any(b => Vector2.DistanceSquared(b.Path[b.Path.Length / 2], p) < 30f * 30f))
                {
                    continue;
                }

                // Low and broad, dark with wet: three overlapping slabs, each turned a little.
                float size = 0.35f + (Hash.Unit(Hash.Mix(h + 1u)) * 0.75f);
                float turn = Hash.Unit(Hash.Mix(h + 2u)) * MathUtil.Tau;
                float ground = map.Terrain.LatticeHeight((int)MathF.Round(p.X / Terrain.Cell), (int)MathF.Round(p.Y / Terrain.Cell));
                float foot = MathF.Min(ground, level) - 0.6f;
                float crown = MathF.Max(ground, level);
                for (int k = 0; k < 3; k++)
                {
                    float angle = turn + (k * 1.05f);
                    Vector2 axis = new(MathF.Cos(angle), MathF.Sin(angle));
                    float shrink = 1f - (k * 0.24f);
                    Box(mesh, p + (axis * size * 0.12f * k), axis, size * shrink, size * 0.7f * shrink, foot, crown + (size * (0.07f + (k * 0.07f))), WorldTextures.RoofDark, min);
                }
            }
        }
    }

    /// <summary>Bridges (slab, parapets, piers) and the station's platform, canopy and posts.</summary>
    private static void BuildStructures(WorldMap map, MeshData mesh, Vector2 min, Vector2 max)
    {
        foreach (Bridge bridge in map.Bridges)
        {
            Vector2[] path = bridge.Path;
            if (!Inside(path[path.Length / 2], min, max))
            {
                continue;
            }

            for (int i = 0; i < path.Length - 1; i++)
            {
                Vector2 along = path[i + 1] - path[i];
                float length = along.Length();
                if (length < 0.01f)
                {
                    continue;
                }

                along /= length;
                Vector2 across = MathUtil.Left(along);
                Vector2 centre = (path[i] + path[i + 1]) * 0.5f;
                float halfLength = (length * 0.5f) + 0.05f;
                Box(mesh, centre, along, halfLength, bridge.HalfWidth, bridge.Height - 0.6f, bridge.Height - 0.03f, WorldTextures.RoofConcrete, min);
                foreach (float side in (ReadOnlySpan<float>)[1f, -1f])
                {
                    Box(mesh, centre + (across * side * (bridge.HalfWidth - 0.2f)), along, halfLength, 0.18f, bridge.Height - 0.03f, bridge.Height + 0.95f, WorldTextures.RoofConcrete, min);
                }
            }

            // Two piers standing in the water.
            foreach (int at in (ReadOnlySpan<int>)[path.Length / 3, path.Length * 2 / 3])
            {
                int k = Math.Clamp(at, 0, path.Length - 2);
                Vector2 along = Vector2.Normalize(path[k + 1] - path[k]);
                Box(mesh, path[k], along, 0.7f, bridge.HalfWidth - 0.6f, bridge.WaterLevel - 1.8f, bridge.Height - 0.6f, WorldTextures.RoofConcrete, min);
            }
        }

        foreach (Slab slab in map.Slabs)
        {
            if (Inside(slab.Center, min, max))
            {
                int tile = slab.Look switch
                {
                    SlabLook.Zinc => WorldTextures.RoofZinc,
                    SlabLook.Dark => WorldTextures.RoofDark,
                    SlabLook.White => WorldTextures.PlasterWhite,
                    SlabLook.Copper => WorldTextures.Copper,
                    _ => WorldTextures.RoofConcrete,
                };
                Box(mesh, slab.Center, new Vector2(MathF.Cos(slab.Yaw), -MathF.Sin(slab.Yaw)), slab.HalfWidth, slab.HalfDepth, slab.Bottom, slab.Top, tile, min);
            }
        }
    }

    /// <summary>A closed box: <paramref name="halfU"/> along <paramref name="axisU"/>, <paramref name="halfV"/> across it.</summary>
    private static void Box(MeshData mesh, Vector2 centre, Vector2 axisU, float halfU, float halfV, float bottom, float top, int tile, Vector2 origin)
    {
        (Vector2 uv0, Vector2 uv1) = WorldTextures.TileUv(tile);
        Vector3 u = new Vector3(axisU.X, 0f, axisU.Y) * halfU;
        Vector2 left = MathUtil.Left(axisU);
        Vector3 v = new Vector3(left.X, 0f, left.Y) * halfV;
        Vector3 c = new(centre.X - origin.X, 0f, centre.Y - origin.Y);
        Vector3 lo = Vector3.UnitY * bottom;
        Vector3 hi = Vector3.UnitY * top;

        void Face(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, Vector3 normal)
        {
            // Wind each face so it is seen from the side its normal points to.
            if (Vector3.Dot(Vector3.Cross(p1 - p0, p2 - p0), normal) < 0f)
            {
                (p1, p3) = (p3, p1);
            }

            mesh.Quad(p0, p1, p2, p3, normal, new Vector2(uv0.X, uv1.Y), uv1, new Vector2(uv1.X, uv0.Y), uv0);
        }

        Face(c - u - v + hi, c + u - v + hi, c + u + v + hi, c - u + v + hi, Vector3.UnitY);
        Face(c - u - v + lo, c + u - v + lo, c + u + v + lo, c - u + v + lo, -Vector3.UnitY);
        Face(c + u - v + lo, c + u + v + lo, c + u + v + hi, c + u - v + hi, Vector3.Normalize(u));
        Face(c - u - v + lo, c - u + v + lo, c - u + v + hi, c - u - v + hi, -Vector3.Normalize(u));
        Face(c - u + v + lo, c + u + v + lo, c + u + v + hi, c - u + v + hi, Vector3.Normalize(v));
        Face(c - u - v + lo, c + u - v + lo, c + u - v + hi, c - u - v + hi, -Vector3.Normalize(v));
    }

    // ------------------------------------------------------------------ buildings

    private static void AddBuilding(MeshData mesh, in Building b, Vector2 origin, bool detail)
    {
        (int first, int count) = WorldTextures.VariantsOf(b.Style);
        int variant = first + (int)(b.Seed % (uint)count);
        int tiles = variant * WorldTextures.TilesPerVariant;
        float floorHeight = b.Style switch
        {
            BuildingStyle.Kampung => 3.2f,
            BuildingStyle.Kios => 3f,
            BuildingStyle.Kampus => 3.8f,
            BuildingStyle.Villa => 3.3f,
            BuildingStyle.Mall => 6f,
            _ => 3.6f,
        };
        int floors = Math.Max(1, (int)MathF.Round(b.Height / floorHeight));
        float top = b.BaseY + (floors * floorHeight);
        bool terraced = b.Style is BuildingStyle.Ruko or BuildingStyle.Kios or BuildingStyle.Kampung;
        bool gable = b.Style is BuildingStyle.Rumah or BuildingStyle.Kampung or BuildingStyle.Villa;

        Vector3 aw = new(b.AxisWidth.X, 0f, b.AxisWidth.Y);
        Vector3 ad = new(b.AxisDepth.X, 0f, b.AxisDepth.Y);
        Vector3 centre = new(b.Center.X - origin.X, 0f, b.Center.Y - origin.Y);
        Vector3 c0 = centre - (aw * b.HalfWidth) - (ad * b.HalfDepth);
        Vector3 c1 = centre + (aw * b.HalfWidth) - (ad * b.HalfDepth);
        Vector3 c2 = centre + (aw * b.HalfWidth) + (ad * b.HalfDepth);
        Vector3 c3 = centre - (aw * b.HalfWidth) + (ad * b.HalfDepth);

        Wall(mesh, c1, c0, -ad, b.BaseY, floors, floorHeight, b.HalfWidth * 2f, tiles, b.Seed, windows: detail, groundFloor: true);
        Wall(mesh, c3, c2, ad, b.BaseY, floors, floorHeight, b.HalfWidth * 2f, tiles, b.Seed + 1u, windows: detail, groundFloor: false);
        Wall(mesh, c2, c1, aw, b.BaseY, floors, floorHeight, b.HalfDepth * 2f, tiles, b.Seed + 2u, windows: detail && !terraced, groundFloor: false);
        Wall(mesh, c0, c3, -aw, b.BaseY, floors, floorHeight, b.HalfDepth * 2f, tiles, b.Seed + 3u, windows: detail && !terraced, groundFloor: false);

        Vector3 up = Vector3.UnitY * top;
        if (!gable)
        {
            int roof = b.Style == BuildingStyle.Kios ? WorldTextures.RoofZinc : WorldTextures.RoofConcrete;
            (Vector2 uv0, Vector2 uv1) = WorldTextures.TileUv(roof);
            mesh.Quad(c0 + up, c3 + up, c2 + up, c1 + up, Vector3.UnitY, uv0, new Vector2(uv0.X, uv1.Y), uv1, new Vector2(uv1.X, uv0.Y));
            return;
        }

        // Pitched roof with eaves; the ridge runs along the facade.
        const float eave = 0.55f;
        float rise = MathF.Min(b.HalfDepth * 0.62f, 2.6f);
        int tile = b.Style == BuildingStyle.Villa ? WorldTextures.RoofDark
            : b.Style == BuildingStyle.Kampung && b.Seed % 3 == 0 ? WorldTextures.RoofZinc : WorldTextures.RoofTerracotta;
        (Vector2 r0, Vector2 r1) = WorldTextures.TileUv(tile);
        Vector3 wide = aw * (b.HalfWidth + eave);
        Vector3 deep = ad * (b.HalfDepth + eave);
        Vector3 drop = Vector3.UnitY * (top - (eave * rise / b.HalfDepth));
        Vector3 ridge = Vector3.UnitY * (top + rise);
        Vector3 e0 = centre - wide - deep + drop;
        Vector3 e1 = centre + wide - deep + drop;
        Vector3 e2 = centre + wide + deep + drop;
        Vector3 e3 = centre - wide + deep + drop;
        Vector3 ridgeA = centre - wide + ridge;
        Vector3 ridgeB = centre + wide + ridge;
        Vector3 frontNormal = Vector3.Normalize(Vector3.Cross(e0 - e1, ridgeA - e1));
        Vector3 backNormal = Vector3.Normalize(Vector3.Cross(e2 - e3, ridgeB - e3));
        mesh.Quad(e1, e0, ridgeA, ridgeB, frontNormal, new Vector2(r0.X, r1.Y), r1, new Vector2(r1.X, r0.Y), r0);
        mesh.Quad(e3, e2, ridgeB, ridgeA, backNormal, new Vector2(r0.X, r1.Y), r1, new Vector2(r1.X, r0.Y), r0);

        // Gable ends, in the plain wall colour.
        (Vector2 p0, Vector2 p1) = WorldTextures.TileUv(tiles + 2);
        Vector2 apexUv = new((p0.X + p1.X) * 0.5f, p0.Y);
        Vector3 apexRight = centre + (aw * b.HalfWidth) + ridge;
        Vector3 apexLeft = centre - (aw * b.HalfWidth) + ridge;
        uint i0 = mesh.Add(c2 + up, aw, new Vector2(p0.X, p1.Y));
        uint i1 = mesh.Add(c1 + up, aw, p1);
        uint i2 = mesh.Add(apexRight, aw, apexUv);
        mesh.Triangle(i0, i1, i2);
        i0 = mesh.Add(c0 + up, -aw, new Vector2(p0.X, p1.Y));
        i1 = mesh.Add(c3 + up, -aw, p1);
        i2 = mesh.Add(apexLeft, -aw, apexUv);
        mesh.Triangle(i0, i1, i2);
    }

    /// <summary>
    /// One wall from <paramref name="left"/> to <paramref name="right"/> as seen from outside,
    /// cut into bays and floors so every window is its own atlas tile.
    /// </summary>
    private static void Wall(
        MeshData mesh, Vector3 left, Vector3 right, Vector3 normal, float baseY, int floors, float floorHeight, float length,
        int tiles, uint seed, bool windows, bool groundFloor)
    {
        (Vector2 plain0, Vector2 plain1) = WorldTextures.TileUv(tiles + 2);

        // Foundation skirt: keeps the wall closed where the ground falls away.
        const float skirt = 3.5f;
        Vector3 down = Vector3.UnitY * (baseY - skirt);
        Vector3 ground = Vector3.UnitY * baseY;
        Vector2 foot = new(plain0.X, plain1.Y);
        mesh.Quad(left + down, right + down, right + ground, left + ground, normal, foot, plain1, plain1, foot);

        if (!windows)
        {
            Vector3 topY = Vector3.UnitY * (baseY + (floors * floorHeight));
            mesh.Quad(left + ground, right + ground, right + topY, left + topY, normal,
                new Vector2(plain0.X, plain1.Y), plain1, new Vector2(plain1.X, plain0.Y), plain0);
            return;
        }

        int bays = Math.Max(1, (int)MathF.Round(length / 3.2f));
        Vector3 step = (right - left) / bays;
        for (int floor = 0; floor < floors; floor++)
        {
            Vector3 y0 = Vector3.UnitY * (baseY + (floor * floorHeight));
            Vector3 y1 = Vector3.UnitY * (baseY + ((floor + 1) * floorHeight));
            for (int bay = 0; bay < bays; bay++)
            {
                int part = floor == 0 && groundFloor ? 3
                    : floor == 0 && !groundFloor && bay % 2 == 1 ? 2
                    : Hash.Unit(bay, floor, seed) < 0.4f ? 0 : 1;
                (Vector2 uv0, Vector2 uv1) = WorldTextures.TileUv(tiles + part);
                Vector3 a = left + (step * bay);
                Vector3 b = a + step;
                mesh.Quad(a + y0, b + y0, b + y1, a + y1, normal,
                    new Vector2(uv0.X, uv1.Y), uv1, new Vector2(uv1.X, uv0.Y), uv0);
            }
        }
    }

    // ------------------------------------------------------------------ instances

    private static void AddInstances(WorldMap map, ChunkData data, int chunkX, int chunkZ, Vector2 min, Vector2 max, bool detail)
    {
        Terrain terrain = map.Terrain;
        foreach (Tree tree in map.TreesInChunk(chunkX, chunkZ))
        {
            if (!detail && tree.Kind is TreeKind.Teh or TreeKind.Bambu)
            {
                continue;
            }

            string model = tree.Kind switch
            {
                TreeKind.Beringin => "pohon_beringin",
                TreeKind.Kelapa => "pohon_kelapa",
                TreeKind.Pinus => "pohon_pinus",
                TreeKind.Bambu => "bambu",
                TreeKind.Teh => "semak_teh",
                _ => "pohon_kecil",
            };
            Add(data, model, tree.Position, terrain.HeightAt(tree.Position) - 0.05f, tree.Yaw, tree.Scale, min);
        }

        map.ForEachProp(min, max, prop =>
        {
            // From a distance only what has some bulk reads at all: landmarks, not lamp posts.
            if (detail || prop.Radius >= 2f)
            {
                Add(data, prop.Model, prop.Position, terrain.HeightAt(prop.Position), prop.Yaw, prop.Scale, min);
            }
        });
    }

    private static void Add(ChunkData data, string model, Vector2 position, float height, float yaw, float scale, Vector2 origin)
    {
        if (!data.Instances.TryGetValue(model, out List<Matrix4x4>? list))
        {
            data.Instances[model] = list = [];
        }

        list.Add(Matrix4x4.CreateScale(scale) * Matrix4x4.CreateRotationY(yaw) * Matrix4x4.CreateTranslation(position.X - origin.X, height, position.Y - origin.Y));
    }
}
