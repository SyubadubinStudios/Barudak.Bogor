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

    public Vector3 Origin => new(X * WorldMap.ChunkSize, 0f, Z * WorldMap.ChunkSize);

    public MeshData Terrain { get; } = new();

    public byte[] GroundPixels { get; } = new byte[GroundTextureSize * GroundTextureSize * 4];

    public MeshData RoadMain { get; } = new();

    public MeshData RoadToll { get; } = new();

    public MeshData RoadGang { get; } = new();

    public MeshData Asphalt { get; } = new();

    public MeshData Buildings { get; } = new();

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

    public static ChunkData Build(WorldMap map, int chunkX, int chunkZ, bool detail)
    {
        ChunkData data = new() { X = chunkX, Z = chunkZ };
        Vector2 min = new(chunkX * WorldMap.ChunkSize, chunkZ * WorldMap.ChunkSize);
        Vector2 max = min + new Vector2(WorldMap.ChunkSize);
        BuildTerrain(map, data, chunkX, chunkZ);
        PaintGround(map, data, min);
        BuildRoads(map, data, min, max);
        if (detail)
        {
            map.ForEachBuilding(min, max, building => AddBuilding(data.Buildings, building, min));
            AddInstances(map, data, chunkX, chunkZ, min, max);
        }

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
        for (int j = 0; j < Cells; j++)
        {
            for (int i = 0; i < Cells; i++)
            {
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

                if (map.Terrain.WaterLevelAt(p) is not null)
                {
                    color = new Vector3(0.2f, 0.22f, 0.15f);
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

    // ------------------------------------------------------------------ buildings

    private static void AddBuilding(MeshData mesh, in Building b, Vector2 origin)
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

        Wall(mesh, c1, c0, -ad, b.BaseY, floors, floorHeight, b.HalfWidth * 2f, tiles, b.Seed, windows: true, groundFloor: true);
        Wall(mesh, c3, c2, ad, b.BaseY, floors, floorHeight, b.HalfWidth * 2f, tiles, b.Seed + 1u, windows: true, groundFloor: false);
        Wall(mesh, c2, c1, aw, b.BaseY, floors, floorHeight, b.HalfDepth * 2f, tiles, b.Seed + 2u, windows: !terraced, groundFloor: false);
        Wall(mesh, c0, c3, -aw, b.BaseY, floors, floorHeight, b.HalfDepth * 2f, tiles, b.Seed + 3u, windows: !terraced, groundFloor: false);

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

    private static void AddInstances(WorldMap map, ChunkData data, int chunkX, int chunkZ, Vector2 min, Vector2 max)
    {
        Terrain terrain = map.Terrain;
        foreach (Tree tree in map.TreesInChunk(chunkX, chunkZ))
        {
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
            Add(data, prop.Model, prop.Position, terrain.HeightAt(prop.Position), prop.Yaw, prop.Scale, min));
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
