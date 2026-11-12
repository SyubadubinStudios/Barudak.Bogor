using System.Numerics;
using BarudakBogor.Numerics;

namespace BarudakBogor.World;

public enum RoadKind
{
    /// <summary>Kampung alley: walkers, motorbikes and brave angkots.</summary>
    Gang,
    Street,
    Arterial,
    /// <summary>Winding two-lane mountain road.</summary>
    Mountain,
    Toll,
}

public sealed class RoadNode(int id, Vector2 position)
{
    public int Id { get; } = id;

    public Vector2 Position { get; } = position;

    /// <summary>Ids of the edges that meet here.</summary>
    public List<int> Edges { get; } = [];

    /// <summary>Widest road meeting here, used to pave the junction.</summary>
    public float MaxWidth { get; internal set; }
}

/// <summary>A stretch of road between two junctions, stored as a polyline on the ground plane.</summary>
public sealed class RoadEdge
{
    private readonly float[] _cumulative;

    internal RoadEdge(int id, int from, int to, Vector2[] points, RoadKind kind, string nameKey)
    {
        Id = id;
        From = from;
        To = to;
        Points = points;
        Kind = kind;
        NameKey = nameKey;
        Width = WidthOf(kind);
        _cumulative = new float[points.Length];
        for (int i = 1; i < points.Length; i++)
        {
            _cumulative[i] = _cumulative[i - 1] + Vector2.Distance(points[i - 1], points[i]);
        }
    }

    public int Id { get; }

    public int From { get; }

    public int To { get; }

    public Vector2[] Points { get; }

    public RoadKind Kind { get; }

    public string NameKey { get; }

    /// <summary>Full carriageway width in metres.</summary>
    public float Width { get; }

    public float Length => _cumulative[^1];

    /// <summary>Cruising speed of traffic in m/s.</summary>
    public float SpeedLimit => Kind switch
    {
        RoadKind.Gang => 4f,
        RoadKind.Street => 9f,
        RoadKind.Arterial => 14f,
        RoadKind.Mountain => 11f,
        _ => 24f,
    };

    public static float WidthOf(RoadKind kind) => kind switch
    {
        RoadKind.Gang => 3.2f,
        RoadKind.Street => 7f,
        RoadKind.Arterial => 10f,
        RoadKind.Mountain => 8f,
        _ => 14f,
    };

    /// <summary>Distance along the edge at polyline vertex <paramref name="index"/>.</summary>
    public float DistanceAtVertex(int index) => _cumulative[index];

    /// <summary>Position and unit direction at <paramref name="distance"/> metres from the start.</summary>
    public Vector2 Sample(float distance, out Vector2 direction)
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
        direction = span > 1e-5f ? (b - a) / span : Vector2.UnitY;
        return a + (direction * (distance - _cumulative[hi - 1]));
    }
}

/// <summary>Result of a nearest-road query. <see cref="Lateral"/> is positive to the left of the edge direction.</summary>
public readonly record struct RoadHit(RoadEdge Edge, float Distance, Vector2 Point, Vector2 Direction, float Lateral);

/// <summary>
/// The road graph of the whole map plus a uniform grid over its segments for fast
/// "what road am I near" queries (used by building placement, traffic, the HUD and missions).
/// </summary>
public sealed class RoadNetwork
{
    private const float GridCell = 96f;

    private readonly List<RoadNode> _nodes = [];
    private readonly List<RoadEdge> _edges = [];
    private readonly Dictionary<long, List<(int Edge, int Segment)>> _grid = [];

    public IReadOnlyList<RoadNode> Nodes => _nodes;

    public IReadOnlyList<RoadEdge> Edges => _edges;

    public RoadNode AddNode(Vector2 position)
    {
        RoadNode node = new(_nodes.Count, position);
        _nodes.Add(node);
        return node;
    }

    /// <summary>Adds a straight edge, subdivided so it can follow the terrain.</summary>
    public RoadEdge AddEdge(RoadNode from, RoadNode to, RoadKind kind, string nameKey) =>
        AddEdge(from, to, [from.Position, to.Position], kind, nameKey);

    /// <summary>Adds an edge along <paramref name="path"/>; the first and last points snap to the nodes.</summary>
    public RoadEdge AddEdge(RoadNode from, RoadNode to, ReadOnlySpan<Vector2> path, RoadKind kind, string nameKey)
    {
        List<Vector2> points = [from.Position];
        const float step = 14f;
        Vector2 previous = from.Position;
        for (int i = 1; i < path.Length; i++)
        {
            Vector2 target = i == path.Length - 1 ? to.Position : path[i];
            float length = Vector2.Distance(previous, target);
            int pieces = Math.Max(1, (int)MathF.Ceiling(length / step));
            for (int k = 1; k <= pieces; k++)
            {
                points.Add(Vector2.Lerp(previous, target, k / (float)pieces));
            }

            previous = target;
        }

        RoadEdge edge = new(_edges.Count, from.Id, to.Id, [.. points], kind, nameKey);
        _edges.Add(edge);
        from.Edges.Add(edge.Id);
        to.Edges.Add(edge.Id);
        from.MaxWidth = MathF.Max(from.MaxWidth, edge.Width);
        to.MaxWidth = MathF.Max(to.MaxWidth, edge.Width);
        for (int i = 0; i < edge.Points.Length - 1; i++)
        {
            Vector2 mid = (edge.Points[i] + edge.Points[i + 1]) * 0.5f;
            long key = Key(mid);
            if (!_grid.TryGetValue(key, out List<(int, int)>? bucket))
            {
                _grid[key] = bucket = [];
            }

            bucket.Add((edge.Id, i));
        }

        return edge;
    }

    /// <summary>Smooth path through control points (Catmull-Rom), sampled every ~20 m.</summary>
    public static Vector2[] Spline(ReadOnlySpan<Vector2> control)
    {
        if (control.Length < 3)
        {
            return control.ToArray();
        }

        List<Vector2> result = [control[0]];
        for (int i = 0; i < control.Length - 1; i++)
        {
            Vector2 p0 = control[Math.Max(i - 1, 0)];
            Vector2 p1 = control[i];
            Vector2 p2 = control[i + 1];
            Vector2 p3 = control[Math.Min(i + 2, control.Length - 1)];
            int pieces = Math.Max(2, (int)(Vector2.Distance(p1, p2) / 20f));
            for (int k = 1; k <= pieces; k++)
            {
                float t = k / (float)pieces;
                float t2 = t * t;
                float t3 = t2 * t;
                result.Add(0.5f * ((2f * p1) + ((p2 - p0) * t) + (((2f * p0) - (5f * p1) + (4f * p2) - p3) * t2) + ((-p0 + (3f * p1) - (3f * p2) + p3) * t3)));
            }
        }

        return [.. result];
    }

    /// <summary>The other end of <paramref name="edge"/> seen from <paramref name="nodeId"/>.</summary>
    public static int OtherEnd(RoadEdge edge, int nodeId) => edge.From == nodeId ? edge.To : edge.From;

    /// <summary>Nearest road within <paramref name="maxDistance"/> metres, or null.</summary>
    public RoadHit? Nearest(Vector2 p, float maxDistance = 96f, Func<RoadEdge, bool>? filter = null)
    {
        int reach = (int)MathF.Ceiling(maxDistance / GridCell);
        int cx = (int)MathF.Floor(p.X / GridCell);
        int cz = (int)MathF.Floor(p.Y / GridCell);
        float best = maxDistance * maxDistance;
        RoadHit? hit = null;
        for (int z = cz - reach; z <= cz + reach; z++)
        {
            for (int x = cx - reach; x <= cx + reach; x++)
            {
                if (!_grid.TryGetValue(Key(x, z), out List<(int Edge, int Segment)>? bucket))
                {
                    continue;
                }

                foreach ((int edgeId, int segment) in bucket)
                {
                    RoadEdge edge = _edges[edgeId];
                    if (filter is not null && !filter(edge))
                    {
                        continue;
                    }

                    Vector2 a = edge.Points[segment];
                    Vector2 b = edge.Points[segment + 1];
                    float t = MathUtil.ClosestOnSegment(a, b, p, out Vector2 closest);
                    float d2 = Vector2.DistanceSquared(p, closest);
                    if (d2 < best)
                    {
                        best = d2;
                        Vector2 direction = Vector2.Normalize(b - a);
                        float along = edge.DistanceAtVertex(segment) + (t * Vector2.Distance(a, b));
                        float lateral = Vector2.Dot(p - closest, MathUtil.Left(direction));
                        hit = new RoadHit(edge, along, closest, direction, lateral);
                    }
                }
            }
        }

        return hit;
    }

    /// <summary>True when the point lies on a carriageway (plus <paramref name="margin"/>).</summary>
    public bool IsOnRoad(Vector2 p, float margin = 0f)
    {
        RoadHit? hit = Nearest(p, 24f + margin);
        return hit is { } h && Vector2.Distance(p, h.Point) <= (h.Edge.Width * 0.5f) + margin;
    }

    /// <summary>Visits every polyline segment whose midpoint lies inside the axis-aligned box.</summary>
    public void ForEachSegment(Vector2 min, Vector2 max, Action<RoadEdge, int> visit)
    {
        int x0 = (int)MathF.Floor(min.X / GridCell);
        int x1 = (int)MathF.Floor(max.X / GridCell);
        int z0 = (int)MathF.Floor(min.Y / GridCell);
        int z1 = (int)MathF.Floor(max.Y / GridCell);
        for (int z = z0; z <= z1; z++)
        {
            for (int x = x0; x <= x1; x++)
            {
                if (!_grid.TryGetValue(Key(x, z), out List<(int Edge, int Segment)>? bucket))
                {
                    continue;
                }

                foreach ((int edgeId, int segment) in bucket)
                {
                    RoadEdge edge = _edges[edgeId];
                    Vector2 mid = (edge.Points[segment] + edge.Points[segment + 1]) * 0.5f;
                    if (mid.X >= min.X && mid.X < max.X && mid.Y >= min.Y && mid.Y < max.Y)
                    {
                        visit(edge, segment);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Fastest route between two junctions (A* on travel time). Returns the edge ids in order,
    /// or an empty list when the nodes are not connected.
    /// </summary>
    public List<int> FindRoute(int fromNode, int toNode)
    {
        List<int> route = [];
        if (fromNode == toNode)
        {
            return route;
        }

        Dictionary<int, (int Edge, float Cost)> visited = new() { [fromNode] = (-1, 0f) };
        PriorityQueue<int, float> open = new();
        open.Enqueue(fromNode, 0f);
        Vector2 goal = _nodes[toNode].Position;
        while (open.TryDequeue(out int current, out _))
        {
            if (current == toNode)
            {
                break;
            }

            float cost = visited[current].Cost;
            foreach (int edgeId in _nodes[current].Edges)
            {
                RoadEdge edge = _edges[edgeId];
                int next = OtherEnd(edge, current);
                float nextCost = cost + (edge.Length / edge.SpeedLimit);
                if (!visited.TryGetValue(next, out (int Edge, float Cost) seen) || nextCost < seen.Cost)
                {
                    visited[next] = (edgeId, nextCost);
                    open.Enqueue(next, nextCost + (Vector2.Distance(_nodes[next].Position, goal) / 24f));
                }
            }
        }

        if (!visited.ContainsKey(toNode))
        {
            return route;
        }

        for (int node = toNode; node != fromNode;)
        {
            int edgeId = visited[node].Edge;
            route.Add(edgeId);
            node = OtherEnd(_edges[edgeId], node);
        }

        route.Reverse();
        return route;
    }

    /// <summary>Junction closest to a point (linear scan; used at generation and mission setup only).</summary>
    public RoadNode NearestNode(Vector2 p)
    {
        RoadNode best = _nodes[0];
        float bestDistance = float.MaxValue;
        foreach (RoadNode node in _nodes)
        {
            float d = Vector2.DistanceSquared(node.Position, p);
            if (d < bestDistance)
            {
                bestDistance = d;
                best = node;
            }
        }

        return best;
    }

    private static long Key(Vector2 p) => Key((int)MathF.Floor(p.X / GridCell), (int)MathF.Floor(p.Y / GridCell));

    private static long Key(int x, int z) => ((long)x << 32) ^ (uint)z;
}
