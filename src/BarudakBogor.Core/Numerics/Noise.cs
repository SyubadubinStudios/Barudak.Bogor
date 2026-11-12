using System.Runtime.CompilerServices;

namespace BarudakBogor.Numerics;

/// <summary>Value noise on a hashed lattice: cheap, deterministic and good enough for landscape.</summary>
public static class Noise
{
    /// <summary>Smooth value noise in [0, 1].</summary>
    public static float Value(float x, float y, uint seed = 0)
    {
        int xi = (int)MathF.Floor(x);
        int yi = (int)MathF.Floor(y);
        float tx = Fade(x - xi);
        float ty = Fade(y - yi);
        float a = Hash.Unit(xi, yi, seed);
        float b = Hash.Unit(xi + 1, yi, seed);
        float c = Hash.Unit(xi, yi + 1, seed);
        float d = Hash.Unit(xi + 1, yi + 1, seed);
        return MathUtil.Lerp(MathUtil.Lerp(a, b, tx), MathUtil.Lerp(c, d, tx), ty);
    }

    /// <summary>Fractal sum of <see cref="Value"/> octaves, normalised to roughly [-1, 1].</summary>
    public static float Fbm(float x, float y, int octaves, uint seed = 0)
    {
        float sum = 0f;
        float amplitude = 1f;
        float total = 0f;
        for (int i = 0; i < octaves; i++)
        {
            sum += ((Value(x, y, seed + (uint)i) * 2f) - 1f) * amplitude;
            total += amplitude;
            amplitude *= 0.5f;
            x *= 2.03f;
            y *= 2.03f;
        }

        return sum / total;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float Fade(float t) => t * t * (3f - (2f * t));
}
