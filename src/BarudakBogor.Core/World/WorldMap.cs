using System.Numerics;
using BarudakBogor.Numerics;

namespace BarudakBogor.World;

public enum BuildingStyle
{
    Ruko,
    Rumah,
    Kios,
    Kantor,
    Kampung,
    Kampus,
    Villa,
    Mall,
    /// <summary>Only a footprint to bump into: the building itself is a model (see <see cref="Prop.Landmark"/>).</summary>
    Landmark,
}

/// <summary>A procedurally placed building: an oriented box footprint with a height and a look.</summary>
public readonly record struct Building(
    Vector2 Center, float HalfWidth, float HalfDepth, float Yaw, float Height, BuildingStyle Style, uint Seed, float BaseY)
{
    /// <summary>Unit vector along the facade (the side that faces the road is at -Depth).</summary>
    public Vector2 AxisWidth => new(MathF.Cos(Yaw), -MathF.Sin(Yaw));

    /// <summary>Unit vector pointing from the road into the lot.</summary>
    public Vector2 AxisDepth => new(MathF.Sin(Yaw), MathF.Cos(Yaw));
}

/// <summary>A hand placed model (landmark, stall, lamp). <see cref="Radius"/> 0 means no collision.</summary>
/// <param name="Landmark">A whole building: seen from far away, and bumped into through a <see cref="Building"/> footprint of its own.</param>
/// <param name="Afloat">Drawn on the water surface instead of on the ground under it (lily pads, water hyacinth).</param>
public readonly record struct Prop(string Model, Vector2 Position, float Yaw, float Scale, float Radius, bool Landmark = false, bool Afloat = false);

public enum TreeKind
{
    Beringin,
    Kelapa,
    Pinus,
    Kecil,
    Bambu,
    Teh,
}

/// <summary>A scattered plant, generated per chunk from hashes.</summary>
public readonly record struct Tree(TreeKind Kind, Vector2 Position, float Yaw, float Scale)
{
    /// <summary>Trunk radius for collision; tea bushes can be walked through.</summary>
    public float Radius => Kind switch
    {
        TreeKind.Beringin => 1.1f * Scale,
        TreeKind.Teh => 0f,
        TreeKind.Bambu => 0.8f * Scale,
        _ => 0.35f * Scale,
    };
}

/// <summary>
/// The static world: terrain, districts, roads, buildings, props and places. Everything is
/// generated once from a seed by <see cref="WorldGenerator"/>; chunk-level detail (trees) is
/// derived on demand. The class is immutable after generation and safe to read from any thread.
/// </summary>
public sealed class WorldMap
{
    /// <summary>Side of a streaming chunk in metres.</summary>
    public const float ChunkSize = 256f;

    private const float CollisionCell = 64f;

    private readonly Dictionary<long, List<int>> _buildingGrid = [];
    private readonly Dictionary<long, List<int>> _propGrid = [];
    private readonly Dictionary<long, Tree[]> _treeCache = [];
    private readonly Lock _treeLock = new();
    private readonly List<Building> _buildings = [];
    private readonly List<Prop> _props = [];
    private readonly Dictionary<PoiId, Poi> _pois = [];
    private readonly List<Bridge> _bridges = [];
    private readonly List<LevelCrossing> _crossings = [];
    private readonly List<Slab> _slabs = [];
    private readonly List<Sign> _signs = [];
    private readonly List<Building> _solids = [];

    internal WorldMap(uint seed, District[] districts, Pond[] ponds)
    {
        Seed = seed;
        Districts = districts;
        Ponds = ponds;
        Terrain = new Terrain(districts, ponds, seed);
        Roads = new RoadNetwork();
    }

    public uint Seed { get; }

    public IReadOnlyList<District> Districts { get; }

    public IReadOnlyList<Pond> Ponds { get; }

    public Terrain Terrain { get; }

    public RoadNetwork Roads { get; }

    public IReadOnlyList<Building> Buildings => _buildings;

    public IReadOnlyList<Prop> Props => _props;

    public IReadOnlyCollection<Poi> Pois => _pois.Values;

    /// <summary>Rivers and railways.</summary>
    public IReadOnlyList<Corridor> Corridors => Terrain.Corridors;

    /// <summary>The railway, when the map has one.</summary>
    public Corridor? Railway => Terrain.Corridors.FirstOrDefault(c => c.Kind == CorridorKind.Rail);

    public IReadOnlyList<Bridge> Bridges => _bridges;

    public IReadOnlyList<LevelCrossing> Crossings => _crossings;

    /// <summary>Name boards of the well-known places.</summary>
    public IReadOnlyList<Sign> Signs => _signs;

    /// <summary>Plain masonry boxes: station platform, canopy, posts.</summary>
    public IReadOnlyList<Slab> Slabs => _slabs;

    public Poi this[PoiId id] => _pois[id];

    /// <summary>Creates the default world.</summary>
    public static WorldMap Generate(uint seed = 2026) => WorldGenerator.Generate(seed);

    public static (int X, int Z) ChunkOf(Vector2 p) =>
        ((int)MathF.Floor(p.X / ChunkSize), (int)MathF.Floor(p.Y / ChunkSize));

    public District? DistrictAt(Vector2 p)
    {
        District? best = null;
        // Later, smaller districts (Gang Selot, Pasar Anyar...) win over the city they sit in.
        foreach (District district in Districts)
        {
            if (Vector2.DistanceSquared(p, district.Center) <= district.Radius * district.Radius
                && (best is null || district.Radius < best.Radius))
            {
                best = district;
            }
        }

        return best;
    }

    public LandUse LandUseAt(Vector2 p)
    {
        if (DistrictAt(p) is { } district)
        {
            return district.Use;
        }

        float height = Terrain.CoarseHeightAt(p.X, p.Y);
        if (height > 520f)
        {
            bool tea = Vector2.Distance(p, _puncakCenter) < 4_200f
                && Noise.Value(p.X / 380f, p.Y / 380f, Seed + 99u) > 0.42f;
            return tea ? LandUse.KebunTeh : LandUse.Hutan;
        }

        return LandUse.Sawah;
    }

    /// <summary>Visits buildings whose centre cell overlaps the box (may include near misses).</summary>
    public void ForEachBuilding(Vector2 min, Vector2 max, Action<Building> visit)
    {
        int x0 = (int)MathF.Floor(min.X / CollisionCell);
        int x1 = (int)MathF.Floor(max.X / CollisionCell);
        int z0 = (int)MathF.Floor(min.Y / CollisionCell);
        int z1 = (int)MathF.Floor(max.Y / CollisionCell);
        for (int z = z0; z <= z1; z++)
        {
            for (int x = x0; x <= x1; x++)
            {
                if (_buildingGrid.TryGetValue(Key(x, z), out List<int>? bucket))
                {
                    foreach (int index in bucket)
                    {
                        Building building = _buildings[index];
                        if (building.Center.X >= min.X && building.Center.X < max.X
                            && building.Center.Y >= min.Y && building.Center.Y < max.Y)
                        {
                            visit(building);
                        }
                    }
                }
            }
        }
    }

    /// <summary>Visits props whose position lies inside the box.</summary>
    public void ForEachProp(Vector2 min, Vector2 max, Action<Prop> visit)
    {
        int x0 = (int)MathF.Floor(min.X / CollisionCell);
        int x1 = (int)MathF.Floor(max.X / CollisionCell);
        int z0 = (int)MathF.Floor(min.Y / CollisionCell);
        int z1 = (int)MathF.Floor(max.Y / CollisionCell);
        for (int z = z0; z <= z1; z++)
        {
            for (int x = x0; x <= x1; x++)
            {
                if (_propGrid.TryGetValue(Key(x, z), out List<int>? bucket))
                {
                    foreach (int index in bucket)
                    {
                        Prop prop = _props[index];
                        if (prop.Position.X >= min.X && prop.Position.X < max.X
                            && prop.Position.Y >= min.Y && prop.Position.Y < max.Y)
                        {
                            visit(prop);
                        }
                    }
                }
            }
        }
    }

    /// <summary>
    /// Pushes a circle out of buildings, props and tree trunks. Returns the corrected position
    /// and reports the summed push direction so callers can kill velocity into the obstacle.
    /// </summary>
    public Vector2 ResolveCircle(Vector2 position, float radius, out Vector2 push)
    {
        push = Vector2.Zero;
        // Being pushed out of one wall can land inside the next (terraced rows), so go round again
        // until nothing pushes back.
        for (int pass = 0; pass < 4; pass++)
        {
            position = ResolveOnce(position, radius, out Vector2 step);
            push += step;
            if (step == Vector2.Zero)
            {
                return position;
            }
        }

        // Still wedged between two walls that push against each other: step out to the street.
        if (IsInsideBuilding(position) && Roads.Nearest(position, 150f) is { } street)
        {
            push += street.Point - position;
            position = street.Point;
        }

        return position;
    }

    private Vector2 ResolveOnce(Vector2 position, float radius, out Vector2 push)
    {
        push = Vector2.Zero;
        int cx = (int)MathF.Floor(position.X / CollisionCell);
        int cz = (int)MathF.Floor(position.Y / CollisionCell);
        for (int z = cz - 1; z <= cz + 1; z++)
        {
            for (int x = cx - 1; x <= cx + 1; x++)
            {
                long key = Key(x, z);
                if (_buildingGrid.TryGetValue(key, out List<int>? buildings))
                {
                    foreach (int index in buildings)
                    {
                        if (PushOutOfBox(_buildings[index], ref position, radius, out Vector2 delta))
                        {
                            push += delta;
                        }
                    }
                }

                if (_propGrid.TryGetValue(key, out List<int>? props))
                {
                    foreach (int index in props)
                    {
                        Prop prop = _props[index];
                        if (prop.Radius > 0f && PushOutOfCircle(prop.Position, prop.Radius, ref position, radius, out Vector2 delta))
                        {
                            push += delta;
                        }
                    }
                }
            }
        }

        // Walls and furniture of the walk-in rooms: few enough to test one by one.
        foreach (Building solid in _solids)
        {
            if (Vector2.DistanceSquared(solid.Center, position) < 400f && PushOutOfBox(solid, ref position, radius, out Vector2 delta))
            {
                push += delta;
            }
        }

        (int chunkX, int chunkZ) = ChunkOf(position);
        foreach (Tree tree in TreesInChunk(chunkX, chunkZ))
        {
            float treeRadius = tree.Radius;
            if (treeRadius > 0f && PushOutOfCircle(tree.Position, treeRadius, ref position, radius, out Vector2 delta))
            {
                push += delta;
            }
        }

        return position;
    }

    /// <summary>True when a point lies inside any building footprint grown by <paramref name="margin"/>.</summary>
    public bool IsInsideBuilding(Vector2 p, float margin = 0f)
    {
        int cx = (int)MathF.Floor(p.X / CollisionCell);
        int cz = (int)MathF.Floor(p.Y / CollisionCell);
        for (int z = cz - 1; z <= cz + 1; z++)
        {
            for (int x = cx - 1; x <= cx + 1; x++)
            {
                if (!_buildingGrid.TryGetValue(Key(x, z), out List<int>? bucket))
                {
                    continue;
                }

                foreach (int index in bucket)
                {
                    Building b = _buildings[index];
                    Vector2 local = p - b.Center;
                    if (MathF.Abs(Vector2.Dot(local, b.AxisWidth)) < b.HalfWidth + margin
                        && MathF.Abs(Vector2.Dot(local, b.AxisDepth)) < b.HalfDepth + margin)
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }

    /// <summary>Deterministic vegetation of one chunk (cached).</summary>
    public Tree[] TreesInChunk(int chunkX, int chunkZ)
    {
        long key = Key(chunkX, chunkZ);
        lock (_treeLock)
        {
            if (_treeCache.TryGetValue(key, out Tree[]? cached))
            {
                return cached;
            }
        }

        Tree[] trees = WorldGenerator.ScatterTrees(this, chunkX, chunkZ);
        lock (_treeLock)
        {
            if (_treeCache.Count > 600)
            {
                _treeCache.Clear();
            }

            _treeCache[key] = trees;
        }

        return trees;
    }

    private Vector2 _puncakCenter;

    internal List<(Vector2 Center, float Radius)> Reserved { get; } = [];

    internal void SetPuncakCenter(Vector2 center) => _puncakCenter = center;

    internal void AddPoi(Poi poi) => _pois[poi.Id] = poi;

    internal void AddBridge(Bridge bridge) => _bridges.Add(bridge);

    internal void AddCrossing(LevelCrossing crossing) => _crossings.Add(crossing);

    internal void AddSlab(Slab slab)
    {
        _slabs.Add(slab);
        if (slab.Solid)
        {
            _solids.Add(new Building(slab.Center, slab.HalfWidth, slab.HalfDepth, slab.Yaw, slab.Top - slab.Bottom, BuildingStyle.Kios, 0u, slab.Bottom));
        }
    }

    internal void AddSign(Sign sign) => _signs.Add(sign);

    private readonly List<TrafficSignal> _signals = [];
    private readonly Dictionary<(int Node, int Edge), TrafficSignal> _signalAt = [];

    /// <summary>Every traffic light of the town.</summary>
    public IReadOnlyList<TrafficSignal> Signals => _signals;

    /// <summary>The light a driver arriving at <paramref name="node"/> along <paramref name="edge"/> has to obey, if there is one.</summary>
    public TrafficSignal? SignalFor(int node, int edge) => _signalAt.TryGetValue((node, edge), out TrafficSignal signal) ? signal : null;

    internal void AddSignal(TrafficSignal signal)
    {
        _signals.Add(signal);
        _signalAt[(signal.Node, signal.Edge)] = signal;
    }

    internal void AddBuilding(Building building)
    {
        Bucket(_buildingGrid, building.Center).Add(_buildings.Count);
        _buildings.Add(building);
    }

    internal void AddProp(Prop prop)
    {
        Bucket(_propGrid, prop.Position).Add(_props.Count);
        _props.Add(prop);
    }

    /// <summary>Separating axis test between a candidate footprint and everything already placed nearby.</summary>
    internal bool OverlapsBuilding(in Building candidate)
    {
        int cx = (int)MathF.Floor(candidate.Center.X / CollisionCell);
        int cz = (int)MathF.Floor(candidate.Center.Y / CollisionCell);
        for (int z = cz - 1; z <= cz + 1; z++)
        {
            for (int x = cx - 1; x <= cx + 1; x++)
            {
                if (!_buildingGrid.TryGetValue(Key(x, z), out List<int>? bucket))
                {
                    continue;
                }

                foreach (int index in bucket)
                {
                    if (BoxesOverlap(candidate, _buildings[index]))
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }

    private static bool BoxesOverlap(in Building a, in Building b)
    {
        const float slack = 0.25f;
        Vector2 d = b.Center - a.Center;
        Span<Vector2> axes = [a.AxisWidth, a.AxisDepth, b.AxisWidth, b.AxisDepth];
        foreach (Vector2 axis in axes)
        {
            float ra = (a.HalfWidth * MathF.Abs(Vector2.Dot(a.AxisWidth, axis))) + (a.HalfDepth * MathF.Abs(Vector2.Dot(a.AxisDepth, axis)));
            float rb = (b.HalfWidth * MathF.Abs(Vector2.Dot(b.AxisWidth, axis))) + (b.HalfDepth * MathF.Abs(Vector2.Dot(b.AxisDepth, axis)));
            if (MathF.Abs(Vector2.Dot(d, axis)) > ra + rb - slack)
            {
                return false;
            }
        }

        return true;
    }

    private static bool PushOutOfBox(in Building b, ref Vector2 position, float radius, out Vector2 delta)
    {
        delta = Vector2.Zero;
        Vector2 local = position - b.Center;
        float u = Vector2.Dot(local, b.AxisWidth);
        float v = Vector2.Dot(local, b.AxisDepth);
        float cu = Math.Clamp(u, -b.HalfWidth, b.HalfWidth);
        float cv = Math.Clamp(v, -b.HalfDepth, b.HalfDepth);
        float du = u - cu;
        float dv = v - cv;
        float d2 = (du * du) + (dv * dv);
        if (d2 >= radius * radius)
        {
            return false;
        }

        if (d2 > 1e-6f)
        {
            float d = MathF.Sqrt(d2);
            delta = ((b.AxisWidth * du) + (b.AxisDepth * dv)) / d * (radius - d);
        }
        else
        {
            // Centre is inside the box: leave through the nearest face.
            float exitU = b.HalfWidth - MathF.Abs(u);
            float exitV = b.HalfDepth - MathF.Abs(v);
            delta = exitU < exitV
                ? b.AxisWidth * (MathF.Sign(u == 0f ? 1f : u) * (exitU + radius))
                : b.AxisDepth * (MathF.Sign(v == 0f ? 1f : v) * (exitV + radius));
        }

        position += delta;
        return true;
    }

    private static bool PushOutOfCircle(Vector2 centre, float obstacleRadius, ref Vector2 position, float radius, out Vector2 delta)
    {
        delta = Vector2.Zero;
        Vector2 offset = position - centre;
        float reach = obstacleRadius + radius;
        float d2 = offset.LengthSquared();
        if (d2 >= reach * reach)
        {
            return false;
        }

        float d = MathF.Sqrt(d2);
        delta = d > 1e-4f ? offset / d * (reach - d) : new Vector2(reach, 0f);
        position += delta;
        return true;
    }

    private static List<int> Bucket(Dictionary<long, List<int>> grid, Vector2 p)
    {
        long key = Key((int)MathF.Floor(p.X / CollisionCell), (int)MathF.Floor(p.Y / CollisionCell));
        if (!grid.TryGetValue(key, out List<int>? bucket))
        {
            grid[key] = bucket = [];
        }

        return bucket;
    }

    private static long Key(int x, int z) => ((long)x << 32) ^ (uint)z;
}
