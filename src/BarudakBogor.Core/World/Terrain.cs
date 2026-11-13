using System.Numerics;
using System.Runtime.CompilerServices;
using BarudakBogor.Numerics;

namespace BarudakBogor.World;

/// <summary>
/// Height field of the whole 40 km x 40 km map. The landscape is a pure function of position:
/// Bogor sits on a plain around 260 m that climbs south-east to Puncak and south-west to
/// Gunung Salak. Heights are defined on an 8 m lattice and bilinearly interpolated, so the
/// terrain mesh (built on the same lattice) and gameplay queries always agree.
/// </summary>
public sealed class Terrain
{
    /// <summary>Half the side of the square map, in metres.</summary>
    public const float HalfSize = 20_000f;

    /// <summary>Lattice spacing shared by the mesh and <see cref="HeightAt(float, float)"/>.</summary>
    public const float Cell = 8f;

    private static readonly Vector2 PuncakPeak = new(15_500f, 13_500f);
    private static readonly Vector2 SalakPeak = new(-8_500f, 16_000f);

    private const float CorridorCell = 128f;

    private readonly District[] _districts;
    private readonly Pond[] _ponds;
    private readonly uint _seed;
    private readonly List<Corridor> _corridors = [];
    private readonly Dictionary<long, List<(int Corridor, int Segment)>> _corridorGrid = [];
    private readonly List<Deck> _decks = [];
    private Vector2 _deckMin = new(float.MaxValue);
    private Vector2 _deckMax = new(float.MinValue);

    public Terrain(IReadOnlyList<District> districts, IReadOnlyList<Pond> ponds, uint seed)
    {
        _districts = [.. districts];
        _ponds = [.. ponds];
        _seed = seed;
        foreach (Pond pond in _ponds)
        {
            pond.Level = Raw(pond.Center.X, pond.Center.Y, applyPonds: false) - 0.6f;
        }
    }

    /// <summary>Rivers and railways, in the order they were added.</summary>
    public IReadOnlyList<Corridor> Corridors => _corridors;

    /// <summary>Bridge decks and platforms.</summary>
    public IReadOnlyList<Deck> Decks => _decks;

    /// <summary>
    /// Lays a river or a railway along a smooth line through <paramref name="control"/>. Its level
    /// follows the lie of the land averaged over <paramref name="smooth"/> points either side, so
    /// the cut or the embankment is never more than a few metres. Must be called before anything
    /// reads heights.
    /// </summary>
    internal Corridor AddCorridor(CorridorKind kind, string nameKey, ReadOnlySpan<Vector2> control, float halfWidth, float blend, int smooth)
    {
        Vector2[] points = RoadNetwork.Spline(control);
        float[] raw = new float[points.Length];
        for (int i = 0; i < points.Length; i++)
        {
            raw[i] = Raw(points[i].X, points[i].Y, applyPonds: false);
        }

        float[] levels = new float[points.Length];
        for (int i = 0; i < points.Length; i++)
        {
            float sum = 0f;
            int count = 0;
            for (int k = Math.Max(0, i - smooth); k <= Math.Min(points.Length - 1, i + smooth); k++)
            {
                sum += raw[k];
                count++;
            }

            // A river lies a little below the country it runs through.
            levels[i] = (sum / count) - (kind == CorridorKind.River ? 2f : 0f);
        }

        Corridor corridor = new(kind, nameKey, points, levels, halfWidth, blend);
        int index = _corridors.Count;
        _corridors.Add(corridor);
        for (int i = 0; i < points.Length - 1; i++)
        {
            Vector2 min = Vector2.Min(points[i], points[i + 1]) - new Vector2(blend);
            Vector2 max = Vector2.Max(points[i], points[i + 1]) + new Vector2(blend);
            for (int z = (int)MathF.Floor(min.Y / CorridorCell); z <= (int)MathF.Floor(max.Y / CorridorCell); z++)
            {
                for (int x = (int)MathF.Floor(min.X / CorridorCell); x <= (int)MathF.Floor(max.X / CorridorCell); x++)
                {
                    long key = ((long)x << 32) ^ (uint)z;
                    if (!_corridorGrid.TryGetValue(key, out List<(int, int)>? bucket))
                    {
                        _corridorGrid[key] = bucket = [];
                    }

                    bucket.Add((index, i));
                }
            }
        }

        return corridor;
    }

    internal void AddDeck(Vector2 a, Vector2 b, float halfWidth, float height)
    {
        _decks.Add(new Deck(a, b, halfWidth, height));
        _deckMin = Vector2.Min(_deckMin, Vector2.Min(a, b) - new Vector2(halfWidth));
        _deckMax = Vector2.Max(_deckMax, Vector2.Max(a, b) + new Vector2(halfWidth));
    }

    /// <summary>The nearest corridor whose influence reaches <paramref name="p"/>, with the distance to its centre line.</summary>
    private bool NearestCorridor(Vector2 p, out Corridor corridor, out float distance, out float level)
    {
        corridor = null!;
        distance = level = 0f;
        if (_corridors.Count == 0
            || !_corridorGrid.TryGetValue(((long)(int)MathF.Floor(p.X / CorridorCell) << 32) ^ (uint)(int)MathF.Floor(p.Y / CorridorCell), out List<(int Corridor, int Segment)>? bucket))
        {
            return false;
        }

        float best = float.MaxValue;
        foreach ((int index, int segment) in bucket)
        {
            Corridor candidate = _corridors[index];
            float t = MathUtil.ClosestOnSegment(candidate.Points[segment], candidate.Points[segment + 1], p, out Vector2 closest);
            float d2 = Vector2.DistanceSquared(p, closest);
            if (d2 < best && d2 < candidate.Blend * candidate.Blend)
            {
                best = d2;
                corridor = candidate;
                level = MathUtil.Lerp(candidate.Levels[segment], candidate.Levels[segment + 1], t);
            }
        }

        distance = MathF.Sqrt(best);
        return best < float.MaxValue;
    }

    /// <summary>
    /// Metres from <paramref name="p"/> to the edge of the nearest river or track bed (negative
    /// inside it); very large when none is near.
    /// </summary>
    public float CorridorClearance(Vector2 p, out CorridorKind kind)
    {
        if (NearestCorridor(p, out Corridor corridor, out float distance, out _))
        {
            kind = corridor.Kind;
            return distance - corridor.HalfWidth;
        }

        kind = CorridorKind.River;
        return float.MaxValue;
    }

    /// <summary>
    /// Metres from <paramref name="p"/> to the edge of the nearest river or track bed (negative
    /// inside it); very large when none is near.
    /// </summary>
    public float CorridorClearance(Vector2 p) =>
        NearestCorridor(p, out Corridor corridor, out float distance, out _) ? distance - corridor.HalfWidth : float.MaxValue;

    /// <summary>Spacing of the terrain mesh where <see cref="WaterDetail"/> is not zero.</summary>
    public const float FineCell = 2f;

    /// <summary>Height of the earth itself at a world position: no bridge decks, no platforms.</summary>
    public float GroundAt(float x, float z)
    {
        float fx = x / Cell;
        float fz = z / Cell;
        int ix = (int)MathF.Floor(fx);
        int iz = (int)MathF.Floor(fz);
        float tx = fx - ix;
        float tz = fz - iz;
        float h00 = LatticeHeight(ix, iz);
        float h10 = LatticeHeight(ix + 1, iz);
        float h01 = LatticeHeight(ix, iz + 1);
        float h11 = LatticeHeight(ix + 1, iz + 1);
        float ground = MathUtil.Lerp(MathUtil.Lerp(h00, h10, tx), MathUtil.Lerp(h01, h11, tx), tz);
        float detail = WaterDetail(new Vector2(x, z), 0f);
        if (detail > 0f)
        {
            ground = MathUtil.Lerp(ground, Raw(x, z, applyPonds: true), detail);
        }

        return ground;
    }

    /// <summary>
    /// How far the ground at <paramref name="p"/> follows the generating function itself instead of
    /// the 8 m lattice: 1 on the banks of a river or a pond, 0 away from water. An 8 m lattice
    /// cannot draw a shoreline, so the mesh subdivides these cells (see <see cref="FineCell"/>).
    /// <paramref name="margin"/> widens the reach, for asking whether a whole cell is clear.
    /// </summary>
    public float WaterDetail(Vector2 p, float margin)
    {
        float detail = 0f;
        foreach (Pond pond in _ponds)
        {
            float d = Vector2.Distance(p, pond.Center) - margin;
            if (d < pond.Radius * 1.6f)
            {
                detail = MathF.Max(detail, 1f - MathUtil.SmoothStep(pond.Radius * 1.3f, pond.Radius * 1.6f, d));
            }
        }

        if (NearestCorridor(p, out Corridor corridor, out float distance, out _) && corridor.Kind == CorridorKind.River)
        {
            detail = MathF.Max(detail, 1f - MathUtil.SmoothStep(corridor.HalfWidth + 8f, corridor.HalfWidth + 16f, distance - margin));
        }

        return detail;
    }

    /// <summary>Ground height in metres at a world position: the earth, or a bridge deck or platform over it.</summary>
    public float HeightAt(float x, float z)
    {
        float ground = GroundAt(x, z);
        if (x >= _deckMin.X && x <= _deckMax.X && z >= _deckMin.Y && z <= _deckMax.Y)
        {
            Vector2 p = new(x, z);
            foreach (Deck deck in _decks)
            {
                if (ground < deck.Height)
                {
                    MathUtil.ClosestOnSegment(deck.A, deck.B, p, out Vector2 closest);
                    if (Vector2.DistanceSquared(p, closest) < deck.HalfWidth * deck.HalfWidth)
                    {
                        ground = deck.Height;
                    }
                }
            }
        }

        return ground;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float HeightAt(Vector2 p) => HeightAt(p.X, p.Y);

    /// <summary>
    /// Ground height at lattice vertex (ix, iz), i.e. at world (ix * Cell, iz * Cell). This is the
    /// earth itself: bridge decks and platforms are not part of it.
    /// </summary>
    public float LatticeHeight(int ix, int iz) => Raw(ix * Cell, iz * Cell, applyPonds: true);

    /// <summary>Height straight from the generating function, for coarse far-away meshes.</summary>
    public float CoarseHeightAt(float x, float z) => Raw(x, z, applyPonds: false);

    /// <summary>Unit surface normal from central differences.</summary>
    public Vector3 NormalAt(float x, float z)
    {
        const float d = Cell;
        float dx = HeightAt(x + d, z) - HeightAt(x - d, z);
        float dz = HeightAt(x, z + d) - HeightAt(x, z - d);
        return Vector3.Normalize(new Vector3(-dx, 2f * d, -dz));
    }

    /// <summary>Water surface level when the point is inside a pond or a river, otherwise null.</summary>
    public float? WaterLevelAt(Vector2 p)
    {
        foreach (Pond pond in _ponds)
        {
            if (Vector2.DistanceSquared(p, pond.Center) < pond.Radius * pond.Radius)
            {
                return pond.Level;
            }
        }

        return NearestCorridor(p, out Corridor corridor, out float distance, out float level)
            && corridor.Kind == CorridorKind.River && distance < corridor.HalfWidth
            ? level
            : null;
    }

    /// <summary>0 in open country, 1 well inside a built-up district: flattens small relief.</summary>
    public float Urbanity(float x, float z)
    {
        float urban = 0f;
        Vector2 p = new(x, z);
        foreach (District district in _districts)
        {
            if (district.Use is LandUse.Villa or LandUse.Taman)
            {
                continue;
            }

            float d = Vector2.Distance(p, district.Center);
            urban = MathF.Max(urban, 1f - MathUtil.SmoothStep(district.Radius * 0.9f, district.Radius * 1.6f, d));
        }

        return urban;
    }

    private float Raw(float x, float z, bool applyPonds)
    {
        Vector2 p = new(x, z);

        // Regional shape: a plain tilting up to the south, two volcano massifs.
        float h = 262f + (z * 0.0045f) + (x * 0.0012f);
        float puncak = MathUtil.Saturate(1f - (Vector2.Distance(p, PuncakPeak) / 15_000f));
        h += 1_250f * puncak * puncak * puncak * (3f - (2f * puncak));
        float salak = MathUtil.Saturate(1f - (Vector2.Distance(p, SalakPeak) / 9_500f));
        h += 1_500f * salak * salak * salak;

        // Rolling relief, damped in town so streets stay drivable.
        float mountain = MathUtil.Saturate((puncak * 1.6f) + (salak * 1.4f));
        float calm = 1f - (0.85f * Urbanity(x, z));
        h += Noise.Fbm(x / 2_600f, z / 2_600f, 3, _seed) * (24f + (190f * mountain)) * calm;
        h += Noise.Fbm(x / 420f, z / 420f, 2, _seed + 17u) * (5f + (16f * mountain)) * calm;

        if (applyPonds)
        {
            foreach (Pond pond in _ponds)
            {
                float d = Vector2.Distance(p, pond.Center);
                if (d < pond.Radius * 1.5f)
                {
                    // Level the shore to the water line, then dig the bowl.
                    float shore = 1f - MathUtil.SmoothStep(pond.Radius * 1.05f, pond.Radius * 1.5f, d);
                    h = MathUtil.Lerp(h, pond.Level + 0.5f, shore);
                    float bowl = 1f - MathUtil.SmoothStep(pond.Radius * 0.55f, pond.Radius * 1.02f, d);
                    h -= bowl * (pond.Depth + 0.5f);
                }
            }

            if (NearestCorridor(p, out Corridor corridor, out float distance, out float level))
            {
                if (corridor.Kind == CorridorKind.River)
                {
                    // A bed under the water, a bank just above it, then a slope back up to the country.
                    float bank = level + 2.2f;
                    float edge = corridor.HalfWidth + 5f;
                    h = distance < edge
                        ? MathUtil.Lerp(level - 1.4f, bank, MathUtil.SmoothStep(corridor.HalfWidth * 0.6f, edge, distance))
                        : MathUtil.Lerp(bank, h, MathUtil.SmoothStep(edge, corridor.Blend, distance));
                }
                else
                {
                    // A level bed for the track, eased into the ground either side.
                    h = MathUtil.Lerp(level, h, MathUtil.SmoothStep(corridor.HalfWidth + 4f, corridor.Blend, distance));
                }
            }
        }

        return h;
    }
}
