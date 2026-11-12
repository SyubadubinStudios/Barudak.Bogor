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

    private readonly District[] _districts;
    private readonly Pond[] _ponds;
    private readonly uint _seed;

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

    /// <summary>Ground height in metres at a world position.</summary>
    public float HeightAt(float x, float z)
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
        return MathUtil.Lerp(MathUtil.Lerp(h00, h10, tx), MathUtil.Lerp(h01, h11, tx), tz);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float HeightAt(Vector2 p) => HeightAt(p.X, p.Y);

    /// <summary>Height at lattice vertex (ix, iz), i.e. at world (ix * Cell, iz * Cell).</summary>
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

    /// <summary>Water surface level when the point is inside a pond, otherwise null.</summary>
    public float? WaterLevelAt(Vector2 p)
    {
        foreach (Pond pond in _ponds)
        {
            if (Vector2.DistanceSquared(p, pond.Center) < pond.Radius * pond.Radius)
            {
                return pond.Level;
            }
        }

        return null;
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
        }

        return h;
    }
}
