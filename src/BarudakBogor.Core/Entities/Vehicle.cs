using System.Numerics;
using BarudakBogor.Localization;
using BarudakBogor.Numerics;
using BarudakBogor.World;

namespace BarudakBogor.Entities;

public enum VehicleKind
{
    Angkot,
    Motor,
    MobilKeluarga,
    Delman,
    Bajaj,
    TahuBulat,
    Bakso,
    GerobakCilok,
    GerobakBandrek,
    Becak,
    Bemo,
    Truk,
    Sedan,
}

/// <summary>Handling and presentation data of a vehicle type.</summary>
public sealed record VehicleSpec(
    VehicleKind Kind,
    string Model,
    L Name,
    float MaxSpeed,
    float Acceleration,
    float Braking,
    float MaxSteer,
    float Wheelbase,
    float Grip,
    float Radius,
    float Length,
    float WheelRadius,
    int Seats,
    bool Motorised,
    float OffRoad)
{
    private static readonly VehicleSpec[] All =
    [
        new(VehicleKind.Angkot, "angkot", new("Angkot", "Angkot Minibus"), 25f, 5.2f, 11f, 0.55f, 2.3f, 1f, 1.15f, 3.9f, 0.3f, 12, true, 0.55f),
        new(VehicleKind.Motor, "motor", new("Motor", "Motorbike"), 31f, 8.5f, 12f, 0.6f, 1.3f, 1.05f, 0.6f, 1.9f, 0.28f, 2, true, 0.75f),
        new(VehicleKind.MobilKeluarga, "mobil_keluarga", new("Mobil Keluarga", "Family Car"), 36f, 6.4f, 13f, 0.5f, 2.7f, 1.1f, 1.25f, 4.4f, 0.32f, 7, true, 0.5f),
        new(VehicleKind.Delman, "delman", new("Delman", "Horse Cart"), 7.5f, 2.2f, 5f, 0.5f, 2.6f, 1.3f, 1.2f, 4.2f, 0.55f, 4, false, 0.8f),
        new(VehicleKind.Bajaj, "bajaj", new("Bajaj", "Auto Rickshaw"), 16f, 4f, 8f, 0.7f, 1.9f, 0.85f, 0.9f, 2.6f, 0.22f, 3, true, 0.5f),
        new(VehicleKind.TahuBulat, "mobil_tahu_bulat", new("Mobil Tahu Bulat", "Tahu Bulat Truck"), 22f, 4.2f, 9f, 0.5f, 2.5f, 0.95f, 1.2f, 4f, 0.3f, 2, true, 0.5f),
        new(VehicleKind.Bakso, "mobil_bakso", new("Mobil Bakso", "Bakso Truck"), 21f, 4f, 9f, 0.5f, 2.5f, 0.95f, 1.2f, 4f, 0.3f, 2, true, 0.5f),
        new(VehicleKind.GerobakCilok, "gerobak_cilok", new("Gerobak Cilok", "Cilok Cart"), 3.2f, 2.4f, 6f, 0.8f, 1.1f, 1.6f, 0.75f, 1.6f, 0.25f, 1, false, 0.7f),
        new(VehicleKind.GerobakBandrek, "gerobak_bandrek", new("Gerobak Bandrek", "Bandrek Cart"), 3.2f, 2.4f, 6f, 0.8f, 1.1f, 1.6f, 0.75f, 1.6f, 0.25f, 1, false, 0.7f),
        new(VehicleKind.Becak, "becak", new("Becak", "Cycle Rickshaw"), 5.5f, 1.8f, 5f, 0.6f, 1.6f, 1.3f, 0.8f, 2.4f, 0.33f, 3, false, 0.6f),
        new(VehicleKind.Bemo, "bemo", new("Bemo", "Bemo Microbus"), 14f, 3.5f, 7f, 0.65f, 1.9f, 0.85f, 0.95f, 2.9f, 0.24f, 7, true, 0.5f),
        new(VehicleKind.Truk, "truk", new("Truk", "Truck"), 20f, 3f, 8f, 0.45f, 3.2f, 0.95f, 1.5f, 5.2f, 0.4f, 3, true, 0.55f),
        new(VehicleKind.Sedan, "sedan", new("Taksi", "Taxi"), 38f, 7f, 13f, 0.5f, 2.6f, 1.1f, 1.25f, 4.4f, 0.31f, 4, true, 0.45f),
    ];

    public static VehicleSpec Of(VehicleKind kind) => All[(int)kind];

    /// <summary>True for carts the player pushes on foot rather than sits in.</summary>
    public bool IsCart => Kind is VehicleKind.GerobakCilok or VehicleKind.GerobakBandrek;
}

/// <summary>Driver input for one step, already mapped from keys or AI.</summary>
public readonly record struct DriveInput(float Throttle, float Brake, float Steer, bool Handbrake);

/// <summary>
/// Arcade vehicle model: a bicycle steering model with separate forward and sideways grip, so
/// wet roads and the handbrake make the tail step out. The car follows the terrain height and
/// is pushed out of buildings; there is no rigid body solver, which keeps it cheap and stable.
/// </summary>
public sealed class Vehicle(int id, VehicleKind kind)
{
    public int Id { get; } = id;

    public VehicleSpec Spec { get; } = VehicleSpec.Of(kind);

    public Vector3 Position { get; set; }

    /// <summary>Heading in radians: 0 faces +Z, positive turns toward +X.</summary>
    public float Yaw { get; set; }

    public float Pitch { get; private set; }

    public float Roll { get; private set; }

    /// <summary>Ground plane velocity in m/s.</summary>
    public Vector2 Velocity { get; set; }

    /// <summary>Signed speed along the heading in m/s.</summary>
    public float Speed { get; private set; }

    /// <summary>Current front wheel angle in radians, for the view.</summary>
    public float SteerAngle { get; private set; }

    /// <summary>Accumulated wheel rotation in radians, for the view.</summary>
    public float WheelSpin { get; private set; }

    /// <summary>0..1 how much the tyres are sliding sideways.</summary>
    public float Skid { get; private set; }

    /// <summary>Engine load 0..1, for audio.</summary>
    public float Revs { get; private set; }

    /// <summary>Speed in m/s at the last obstacle hit this step, 0 when nothing was hit.</summary>
    public float Impact { get; private set; }

    public bool IsOnRoad { get; private set; } = true;

    /// <summary>True while a driver (player or AI) is inside.</summary>
    public bool Occupied { get; set; }

    /// <summary>AI traffic is moved along its lane by <see cref="Population"/> instead of physics.</summary>
    public bool IsTraffic { get; init; }

    public bool Horn { get; set; }

    public bool LightsOn { get; set; }

    /// <summary>Passengers on board (angkot missions).</summary>
    public int Passengers { get; set; }

    public Vector2 Forward => MathUtil.Forward(Yaw);

    public Vector2 Ground => new(Position.X, Position.Z);

    /// <summary>Places the vehicle on the ground at a point, at rest.</summary>
    public void Place(WorldMap map, Vector2 position, float yaw)
    {
        Position = position.ToWorld(map.Terrain.HeightAt(position));
        Yaw = yaw;
        Velocity = Vector2.Zero;
        Speed = 0f;
        Skid = 0f;
        AlignToGround(map, 1f);
    }

    /// <summary>Advances the physics by <paramref name="dt"/> seconds.</summary>
    public void Update(float dt, in DriveInput input, WorldMap map, float wetness)
    {
        VehicleSpec spec = Spec;
        Vector2 forward = Forward;
        Vector2 lateral = MathUtil.Left(forward);
        float vf = Vector2.Dot(Velocity, forward);
        float vl = Vector2.Dot(Velocity, lateral);

        Vector2 ground = Ground;
        IsOnRoad = map.Roads.IsOnRoad(ground, 1.5f);
        bool inWater = map.Terrain.WaterLevelAt(ground) is { } level && Position.Y < level - 0.25f;
        float surface = inWater ? 0.2f : IsOnRoad ? 1f : spec.OffRoad;
        float topSpeed = spec.MaxSpeed * surface;

        // Longitudinal: throttle fades out toward top speed, brake doubles as reverse at rest.
        float throttle = Math.Clamp(input.Throttle, 0f, 1f);
        float brake = Math.Clamp(input.Brake, 0f, 1f);
        if (throttle > 0f && vf > -0.5f)
        {
            vf += spec.Acceleration * throttle * MathF.Max(0f, 1f - (vf / topSpeed)) * dt;
        }
        else if (throttle > 0f)
        {
            vf += spec.Braking * throttle * dt;
        }

        if (brake > 0f)
        {
            if (vf > 0.4f)
            {
                vf = MathF.Max(0f, vf - (spec.Braking * brake * dt));
            }
            else
            {
                float reverseTop = topSpeed * 0.3f;
                vf -= spec.Acceleration * 0.7f * brake * MathF.Max(0f, 1f + (vf / reverseTop)) * dt;
            }
        }

        // Rolling resistance, air drag and the slope of the hill.
        float drag = 0.45f + (0.012f * MathF.Abs(vf)) + (vf > topSpeed ? 2.5f : 0f);
        vf -= MathF.Sign(vf) * MathF.Min(MathF.Abs(vf), drag * dt);
        vf -= 9.81f * MathF.Sin(Pitch) * 0.55f * dt;
        if (input.Handbrake)
        {
            vf -= MathF.Sign(vf) * MathF.Min(MathF.Abs(vf), spec.Braking * 0.8f * dt);
        }

        // Sideways grip: this is what rain takes away.
        float grip = spec.Grip * (1f - (0.5f * wetness)) * (input.Handbrake ? 0.3f : 1f) * (IsOnRoad ? 1f : 0.8f);
        vl *= MathF.Exp(-grip * 7.5f * dt);

        // Steering: less lock at speed, and never more yaw than the tyres can hold.
        float speedFactor = 1f / (1f + (MathF.Abs(vf) / 16f));
        float targetSteer = Math.Clamp(input.Steer, -1f, 1f) * spec.MaxSteer * speedFactor;
        SteerAngle += (targetSteer - SteerAngle) * MathUtil.Damp(9f, dt);
        float yawRate = vf / spec.Wheelbase * MathF.Tan(SteerAngle);
        float maxLateral = grip * 9.5f;
        float demand = MathF.Abs(vf * yawRate);
        if (demand > maxLateral)
        {
            yawRate *= maxLateral / demand;
        }

        Yaw = MathUtil.WrapAngle(Yaw + (yawRate * dt));
        Velocity = (forward * vf) + (lateral * vl);
        Skid = MathUtil.Saturate((MathF.Abs(vl) - 1.2f) / 5f);

        // Integrate, then get pushed out of anything solid.
        Vector2 next = ground + (Velocity * dt);
        next = Vector2.Clamp(next, new Vector2(-Terrain.HalfSize + 20f), new Vector2(Terrain.HalfSize - 20f));
        Vector2 resolved = map.ResolveCircle(next, spec.Radius, out Vector2 push);
        Impact = 0f;
        if (push != Vector2.Zero)
        {
            Vector2 normal = Vector2.Normalize(push);
            float into = Vector2.Dot(Velocity, normal);
            if (into < 0f)
            {
                Impact = -into;
                // Lose the speed into the wall, keep a little slide along it.
                Velocity -= normal * into * 1.15f;
                Velocity *= 0.86f;
            }
        }

        Speed = Vector2.Dot(Velocity, MathUtil.Forward(Yaw));
        Position = resolved.ToWorld(map.Terrain.HeightAt(resolved));
        WheelSpin += Speed / spec.WheelRadius * dt;
        Revs = MathUtil.Saturate((MathF.Abs(Speed) / spec.MaxSpeed * 0.75f) + (throttle * 0.25f));
        AlignToGround(map, MathUtil.Damp(10f, dt));
    }

    /// <summary>Kinematic move used by traffic: follows a lane, no slip.</summary>
    public void MoveKinematic(WorldMap map, Vector2 position, float yaw, float speed, float dt)
    {
        Position = position.ToWorld(map.Terrain.HeightAt(position));
        Yaw = yaw;
        Speed = speed;
        Velocity = MathUtil.Forward(yaw) * speed;
        WheelSpin += speed / Spec.WheelRadius * dt;
        Revs = MathUtil.Saturate(MathF.Abs(speed) / Spec.MaxSpeed);
        AlignToGround(map, MathUtil.Damp(8f, dt));
    }

    private void AlignToGround(WorldMap map, float blend)
    {
        Vector3 normal = map.Terrain.NormalAt(Position.X, Position.Z);
        Vector2 forward = Forward;
        Vector2 lateral = MathUtil.Left(forward);
        // Slope along the heading tilts the nose, slope across it rolls the body.
        float pitch = -MathF.Atan2((normal.X * forward.X) + (normal.Z * forward.Y), normal.Y);
        float roll = MathF.Atan2((normal.X * lateral.X) + (normal.Z * lateral.Y), normal.Y);
        Pitch += (pitch - Pitch) * blend;
        Roll += (roll - Roll) * blend;
    }
}
