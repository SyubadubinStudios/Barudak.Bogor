using System.Numerics;
using BarudakBogor.Numerics;

namespace BarudakBogor.World;

public enum CorridorKind
{
    /// <summary>A river: the terrain is cut into a channel and water lies at the corridor's level.</summary>
    River,
    /// <summary>A railway: the terrain is graded flat under the track.</summary>
    Rail,
}

/// <summary>
/// A long, narrow feature that reshapes the ground along a line: the Ciliwung, the commuter
/// railway. <see cref="Levels"/> holds the water or rail height at each point of the line.
/// </summary>
public sealed class Corridor
{
    private const float Cell = 128f;
    private readonly float[] _cumulative;
    // Which segments come within a cell's width of each cell: a long river has thousands.
    private readonly Dictionary<long, List<int>> _grid = [];

    internal Corridor(CorridorKind kind, string nameKey, Vector2[] points, float[] levels, float halfWidth, float blend)
    {
        Kind = kind;
        NameKey = nameKey;
        Points = points;
        Levels = levels;
        HalfWidth = halfWidth;
        Blend = blend;
        _cumulative = new float[points.Length];
        for (int i = 1; i < points.Length; i++)
        {
            _cumulative[i] = _cumulative[i - 1] + Vector2.Distance(points[i - 1], points[i]);
            Vector2 min = Vector2.Min(points[i - 1], points[i]) - new Vector2(Cell);
            Vector2 max = Vector2.Max(points[i - 1], points[i]) + new Vector2(Cell);
            for (int z = (int)MathF.Floor(min.Y / Cell); z <= (int)MathF.Floor(max.Y / Cell); z++)
            {
                for (int x = (int)MathF.Floor(min.X / Cell); x <= (int)MathF.Floor(max.X / Cell); x++)
                {
                    long key = ((long)x << 32) ^ (uint)z;
                    if (!_grid.TryGetValue(key, out List<int>? bucket))
                    {
                        _grid[key] = bucket = [];
                    }

                    bucket.Add(i - 1);
                }
            }
        }
    }

    public CorridorKind Kind { get; }

    public string NameKey { get; }

    public Vector2[] Points { get; }

    /// <summary>Water surface (river) or top of the ballast (rail) at each point.</summary>
    public float[] Levels { get; }

    /// <summary>Half the width of the water or of the track bed.</summary>
    public float HalfWidth { get; }

    /// <summary>Distance from the centre line beyond which the ground is untouched.</summary>
    public float Blend { get; }

    public float Length => _cumulative[^1];

    public float DistanceAtVertex(int index) => _cumulative[index];

    /// <summary>Position, unit direction and level at <paramref name="distance"/> metres from the start.</summary>
    public Vector2 Sample(float distance, out Vector2 direction, out float level)
    {
        distance = Math.Clamp(distance, 0f, Length);
        int hi = Array.BinarySearch(_cumulative, distance);
        if (hi < 0)
        {
            hi = ~hi;
        }

        hi = Math.Clamp(hi, 1, Points.Length - 1);
        Vector2 a = Points[hi - 1];
        Vector2 b = Points[hi];
        float span = _cumulative[hi] - _cumulative[hi - 1];
        float t = span > 1e-5f ? (distance - _cumulative[hi - 1]) / span : 0f;
        direction = span > 1e-5f ? (b - a) / span : Vector2.UnitY;
        level = MathUtil.Lerp(Levels[hi - 1], Levels[hi], t);
        return Vector2.Lerp(a, b, t);
    }

    /// <summary>
    /// Nearest point of the line to <paramref name="p"/>, if within <paramref name="maxDistance"/>.
    /// <paramref name="lateral"/> is signed: positive to the left of the line's direction.
    /// </summary>
    public bool Closest(Vector2 p, float maxDistance, out float lateral, out float along, out float level)
    {
        float best = maxDistance * maxDistance;
        int found = -1;
        float foundT = 0f;
        Vector2 foundPoint = default;
        List<int>? near = null;
        if (maxDistance <= Cell
            && !_grid.TryGetValue(((long)(int)MathF.Floor(p.X / Cell) << 32) ^ (uint)(int)MathF.Floor(p.Y / Cell), out near))
        {
            lateral = along = level = 0f;
            return false;
        }

        int count = near?.Count ?? Points.Length - 1;
        for (int k = 0; k < count; k++)
        {
            int i = near is null ? k : near[k];
            float t = MathUtil.ClosestOnSegment(Points[i], Points[i + 1], p, out Vector2 closest);
            float d2 = Vector2.DistanceSquared(p, closest);
            if (d2 < best)
            {
                best = d2;
                found = i;
                foundT = t;
                foundPoint = closest;
            }
        }

        if (found < 0)
        {
            lateral = along = level = 0f;
            return false;
        }

        Vector2 direction = Vector2.Normalize(Points[found + 1] - Points[found]);
        float side = Vector2.Dot(p - foundPoint, MathUtil.Left(direction)) >= 0f ? 1f : -1f;
        lateral = side * MathF.Sqrt(best);
        along = MathUtil.Lerp(_cumulative[found], _cumulative[found + 1], foundT);
        level = MathUtil.Lerp(Levels[found], Levels[found + 1], foundT);
        return true;
    }
}

/// <summary>
/// A raised walking and driving surface over the ground: a bridge deck, a station platform.
/// Inside its footprint <see cref="Terrain.HeightAt(float, float)"/> answers with the deck, while the
/// terrain mesh keeps the ground underneath.
/// </summary>
public readonly record struct Deck(Vector2 A, Vector2 B, float HalfWidth, float Height);

/// <summary>Where a road is carried over a river: the centre line of the span, for drawing it.</summary>
public sealed record Bridge(Vector2[] Path, float HalfWidth, float Height, float WaterLevel);

/// <summary>Where a road crosses the railway on the level.</summary>
public readonly record struct LevelCrossing(Vector2 Point, float Along);

/// <summary>What a <see cref="Slab"/> is made of, for the view.</summary>
public enum SlabLook
{
    Concrete,
    Zinc,
    Dark,
    /// <summary>White plaster: columns, pediments, towers.</summary>
    White,
    /// <summary>Weathered copper: domes and spires.</summary>
    Copper,
}

/// <summary>A plain box of masonry or roofing: platform, canopy, post, buffer stop. Width runs along X at yaw 0.</summary>
/// <param name="Solid">True for walls and furniture: people and vehicles cannot pass through.</param>
public readonly record struct Slab(Vector2 Center, float HalfWidth, float HalfDepth, float Yaw, float Bottom, float Top, SlabLook Look, bool Solid = false);

/// <summary>What a piece of lettering is painted on.</summary>
public enum SignLook
{
    /// <summary>The green name board of a building.</summary>
    Board,

    /// <summary>A cloth banner at the roadside.</summary>
    Banner,

    /// <summary>The lit face of a shop's light box.</summary>
    LightBox,
}

/// <summary>A name board on a building: centre position, height of its centre, the way it faces, and its width.</summary>
public readonly record struct Sign(Vector2 Position, float Height, float Yaw, float Width, string NameKey, SignLook Look = SignLook.Board);

public enum SignalColour
{
    Red,
    Yellow,
    Green,
}

/// <summary>
/// A traffic light: it stands at the left kerb of <see cref="Edge"/> and speaks to the drivers
/// arriving at <see cref="Node"/> along it. The arms of a junction take turns, <see cref="Slot"/> of <see cref="Slots"/>.
/// </summary>
public readonly record struct TrafficSignal(Vector2 Position, float Yaw, int Node, int Edge, int Slot, int Slots)
{
    public const float GreenSeconds = 9f;
    public const float YellowSeconds = 2f;

    /// <summary>The colour shown at a time on the traffic clock (<c>Population.SignalTime</c>).</summary>
    public SignalColour ColourAt(double time)
    {
        const float turn = GreenSeconds + YellowSeconds;
        // Every junction starts its round at a moment of its own, so the town does not change in step.
        float since = (float)((time + (Node * 3.7)) % (Slots * turn)) - (Slot * turn);
        return since < 0f || since >= turn ? SignalColour.Red : since < GreenSeconds ? SignalColour.Green : SignalColour.Yellow;
    }
}
