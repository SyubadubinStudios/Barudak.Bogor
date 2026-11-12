using System.Numerics;
using ThreeNet;
using ThreeNet.Interop;

namespace BarudakBogor.Game.Rendering;

/// <summary>
/// Vertex and index lists filled on a worker thread and turned into a GPU geometry on the
/// render thread, which keeps chunk generation off the frame.
/// </summary>
public sealed class MeshData
{
    public List<Vertex> Vertices { get; } = [];

    public List<uint> Indices { get; } = [];

    public bool IsEmpty => Indices.Count == 0;

    public uint Add(Vector3 position, Vector3 normal, Vector2 uv)
    {
        Vertices.Add(new Vertex(position, normal, uv));
        return (uint)(Vertices.Count - 1);
    }

    public void Triangle(uint a, uint b, uint c)
    {
        Indices.Add(a);
        Indices.Add(b);
        Indices.Add(c);
    }

    /// <summary>Quad from four corners in counter clockwise order seen from the front.</summary>
    public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 normal, Vector2 uvA, Vector2 uvB, Vector2 uvC, Vector2 uvD)
    {
        uint i0 = Add(a, normal, uvA);
        uint i1 = Add(b, normal, uvB);
        uint i2 = Add(c, normal, uvC);
        uint i3 = Add(d, normal, uvD);
        Triangle(i0, i1, i2);
        Triangle(i0, i2, i3);
    }

    public Geometry Build(Scene scene) =>
        scene.CreateGeometry(System.Runtime.InteropServices.CollectionsMarshal.AsSpan(Vertices), System.Runtime.InteropServices.CollectionsMarshal.AsSpan(Indices));
}
