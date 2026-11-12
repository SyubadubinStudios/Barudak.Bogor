using System.Numerics;
using System.Runtime.CompilerServices;

namespace BarudakBogor.Numerics;

/// <summary>Small allocation-free math helpers shared by the simulation.</summary>
public static class MathUtil
{
    public const float Tau = MathF.PI * 2f;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Saturate(float value) => Math.Clamp(value, 0f, 1f);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Lerp(float a, float b, float t) => a + ((b - a) * t);

    /// <summary>Hermite ramp from 0 at <paramref name="edge0"/> to 1 at <paramref name="edge1"/>.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float SmoothStep(float edge0, float edge1, float x)
    {
        float t = Saturate((x - edge0) / (edge1 - edge0));
        return t * t * (3f - (2f * t));
    }

    /// <summary>Frame-rate independent exponential approach factor.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Damp(float rate, float dt) => 1f - MathF.Exp(-rate * dt);

    /// <summary>Wraps an angle to (-pi, pi].</summary>
    public static float WrapAngle(float angle)
    {
        angle %= Tau;
        if (angle > MathF.PI)
        {
            angle -= Tau;
        }
        else if (angle <= -MathF.PI)
        {
            angle += Tau;
        }

        return angle;
    }

    /// <summary>Moves <paramref name="current"/> toward <paramref name="target"/> along the short arc.</summary>
    public static float TurnToward(float current, float target, float maxStep)
    {
        float delta = WrapAngle(target - current);
        return WrapAngle(current + Math.Clamp(delta, -maxStep, maxStep));
    }

    /// <summary>Heading convention of the game: yaw 0 looks toward +Z, positive yaw turns toward +X.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2 Forward(float yaw) => new(MathF.Sin(yaw), MathF.Cos(yaw));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float YawOf(Vector2 direction) => MathF.Atan2(direction.X, direction.Y);

    /// <summary>The direction to the left of a heading on the ground plane (for yaw 0, facing +Z, this is +X).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2 Left(Vector2 direction) => new(direction.Y, -direction.X);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2 Xz(this Vector3 v) => new(v.X, v.Z);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3 ToWorld(this Vector2 v, float y = 0f) => new(v.X, y, v.Y);

    /// <summary>Closest point on segment ab to p; returns the parameter in [0, 1].</summary>
    public static float ClosestOnSegment(Vector2 a, Vector2 b, Vector2 p, out Vector2 closest)
    {
        Vector2 ab = b - a;
        float lengthSquared = ab.LengthSquared();
        float t = lengthSquared < 1e-8f ? 0f : Saturate(Vector2.Dot(p - a, ab) / lengthSquared);
        closest = a + (ab * t);
        return t;
    }
}

/// <summary>
/// Deterministic integer hashing. World generation uses hashes of coordinates rather than a
/// shared <see cref="Random"/> so any chunk can be generated in any order with the same result.
/// </summary>
public static class Hash
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint Mix(uint x)
    {
        x ^= x >> 16;
        x *= 0x7FEB352Du;
        x ^= x >> 15;
        x *= 0x846CA68Bu;
        x ^= x >> 16;
        return x;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint Of(int x, int y, uint seed = 0) =>
        Mix((uint)x * 0x9E3779B1u ^ Mix((uint)y * 0x85EBCA77u ^ seed));

    /// <summary>Uniform float in [0, 1).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Unit(uint hash) => (hash >> 8) * (1f / 16777216f);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Unit(int x, int y, uint seed = 0) => Unit(Of(x, y, seed));
}

/// <summary>Tiny xorshift generator: a struct, so simulations can own one without allocating.</summary>
public struct Rng(uint seed)
{
    private uint _state = seed == 0 ? 0x9E3779B9u : seed;

    public uint NextUInt()
    {
        uint x = _state;
        x ^= x << 13;
        x ^= x >> 17;
        x ^= x << 5;
        _state = x;
        return x;
    }

    /// <summary>Uniform float in [0, 1).</summary>
    public float NextFloat() => (NextUInt() >> 8) * (1f / 16777216f);

    public float Range(float min, float max) => min + ((max - min) * NextFloat());

    /// <summary>Uniform integer in [min, maxExclusive).</summary>
    public int Range(int min, int maxExclusive) =>
        maxExclusive <= min ? min : min + (int)(NextUInt() % (uint)(maxExclusive - min));

    public bool Chance(float probability) => NextFloat() < probability;

    public T Pick<T>(ReadOnlySpan<T> items) => items[Range(0, items.Length)];
}
