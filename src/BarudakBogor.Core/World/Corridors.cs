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
    private readonly float[] _cumulative;

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
        for (int i = 0; i < Points.Length - 1; i++)
        {
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

/// <summary>A name board on a building: centre position, height of its centre, the way it faces, and its width.</summary>
public readonly record struct Sign(Vector2 Position, float Height, float Yaw, float Width, string NameKey);
