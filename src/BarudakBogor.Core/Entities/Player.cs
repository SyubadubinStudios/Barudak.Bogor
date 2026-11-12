using System.Numerics;
using BarudakBogor.Localization;
using BarudakBogor.Numerics;
using BarudakBogor.World;

namespace BarudakBogor.Entities;

public enum CharacterId
{
    Ujang,
    Cecep,
    Mumu,
    Deden,
    Aceng,
}

/// <summary>A playable character: who they are and the perk that makes them play differently.</summary>
public sealed record CharacterProfile(
    CharacterId Id,
    string Model,
    string Name,
    L Role,
    L Bio,
    L Perk,
    float DrivingBonus,
    float FishingBonus,
    float StudyBonus,
    float SprintBonus,
    float TradeBonus)
{
    private static readonly CharacterProfile[] All =
    [
        new(CharacterId.Ujang, "hero_ujang", "Ujang",
            new("Mantan sopir angkot", "Former angkot driver"),
            new("Hafal semua jalan tikus Bogor. Pensiun dini gara-gara setoran, balik lagi gara-gara tas misterius.",
                "Knows every back road in Bogor. Retired early because of the daily quota, dragged back by a mysterious bag."),
            new("Raja Jalanan: tarif angkot +25%, mobil lebih lincah.", "King of the Road: +25% angkot fares, nimbler cars."),
            0.25f, 0f, 0f, 0f, 0f),
        new(CharacterId.Cecep, "hero_cecep", "Cecep",
            new("Pemancing legendaris", "Legendary angler"),
            new("Katanya pernah narik lele segede bedug. Saksinya cuma dia sendiri.",
                "Says he once landed a catfish the size of a mosque drum. The only witness is himself."),
            new("Tangan Dingin: ikan lebih cepat nyamber dan tarikan lebih kuat.", "Cold Hands: fish bite sooner and reeling is stronger."),
            0f, 0.35f, 0f, 0f, 0f),
        new(CharacterId.Mumu, "hero_mumu", "Mumu",
            new("Mahasiswi cerdas", "Brilliant student"),
            new("Mahasiswi IPB yang bisa ngitung untung rugi warung sambil ngerjain skripsi.",
                "An IPB student who can balance a warung's books while writing her thesis."),
            new("Otak Encer: hadiah misi +15%, pilihan dialog cerdas terbuka.", "Sharp Mind: +15% mission rewards, smart dialogue options unlocked."),
            0f, 0f, 0.15f, 0f, 0f),
        new(CharacterId.Deden, "hero_deden", "Deden",
            new("Anak STM pemberani", "Fearless vocational-school kid"),
            new("Jago bongkar mesin, lebih jago lagi lari dari guru BP.",
                "Great at stripping engines, even better at outrunning the school counsellor."),
            new("Kaki Seribu: lari lebih kencang dan stamina lebih awet.", "Thousand Legs: runs faster with longer stamina."),
            0f, 0f, 0f, 0.3f, 0f),
        new(CharacterId.Aceng, "hero_aceng", "Aceng",
            new("Penjual bandrek", "Bandrek seller"),
            new("Racikan bandreknya bikin hujan Bogor terasa kayak pelukan.",
                "His bandrek recipe makes Bogor rain feel like a hug."),
            new("Tangan Dagang: untung jualan +30%.", "Merchant's Touch: +30% profit on sales."),
            0f, 0f, 0f, 0f, 0.3f),
    ];

    public static IReadOnlyList<CharacterProfile> Roster => All;

    public static CharacterProfile Of(CharacterId id) => All[(int)id];
}

/// <summary>Walking input for one step, in world space.</summary>
public readonly record struct WalkInput(Vector2 Direction, bool Run, bool Jump);

/// <summary>
/// The player on foot: a kinematic capsule that walks the terrain, slides along buildings,
/// jumps and wades. When <see cref="Vehicle"/> is set the player rides along with it.
/// </summary>
public sealed class Player
{
    public const float Radius = 0.35f;
    private const float WalkSpeed = 1.9f;
    private const float RunSpeed = 5.4f;
    private const float Gravity = 22f;

    private float _verticalVelocity;

    public CharacterProfile Profile { get; set; } = CharacterProfile.Of(CharacterId.Ujang);

    public Vector3 Position { get; set; }

    public float Yaw { get; set; }

    /// <summary>Ground speed in m/s (drives the walk animation).</summary>
    public float Speed { get; private set; }

    public bool Grounded { get; private set; } = true;

    /// <summary>True while wading in a pond deeper than the knees.</summary>
    public bool InWater { get; private set; }

    /// <summary>0..1; sprinting drains it, resting restores it.</summary>
    public float Stamina { get; private set; } = 1f;

    /// <summary>The vehicle being driven (or the cart being pushed), if any.</summary>
    public Vehicle? Vehicle { get; set; }

    /// <summary>Distance walked in metres since the session started (stats and achievements).</summary>
    public float DistanceWalked { get; private set; }

    public Vector2 Ground => Vehicle?.Ground ?? new Vector2(Position.X, Position.Z);

    public void Teleport(WorldMap map, Vector2 position, float yaw)
    {
        Position = position.ToWorld(map.Terrain.HeightAt(position));
        Yaw = yaw;
        Speed = 0f;
        _verticalVelocity = 0f;
        Grounded = true;
    }

    public void Update(float dt, in WalkInput input, WorldMap map)
    {
        if (Vehicle is { } vehicle)
        {
            // Sit in the vehicle; carts are pushed from behind.
            Vector2 seat = vehicle.Spec.IsCart ? vehicle.Ground - (vehicle.Forward * 1.2f) : vehicle.Ground;
            Position = vehicle.Spec.IsCart ? seat.ToWorld(map.Terrain.HeightAt(seat)) : vehicle.Position;
            Yaw = vehicle.Yaw;
            Speed = MathF.Abs(vehicle.Speed);
            Grounded = true;
            Stamina = MathF.Min(1f, Stamina + (dt * 0.2f));
            return;
        }

        Vector2 ground = new(Position.X, Position.Z);
        float groundHeight = map.Terrain.HeightAt(ground);
        float? water = map.Terrain.WaterLevelAt(ground);
        InWater = water is { } level && groundHeight < level - 0.45f;

        Vector2 direction = input.Direction;
        float length = direction.Length();
        if (length > 1f)
        {
            direction /= length;
            length = 1f;
        }

        bool running = input.Run && Stamina > 0.05f && length > 0.1f && !InWater;
        float top = running ? RunSpeed * (1f + Profile.SprintBonus) : WalkSpeed * 1.6f;
        if (InWater)
        {
            top = 1.4f;
        }

        float drain = 0.085f / (1f + (2f * Profile.SprintBonus));
        Stamina = MathUtil.Saturate(Stamina + ((running ? -drain : 0.14f) * dt));

        Vector2 velocity = direction * top;
        Speed += ((velocity.Length()) - Speed) * MathUtil.Damp(10f, dt);
        if (length > 0.05f)
        {
            Yaw = MathUtil.TurnToward(Yaw, MathUtil.YawOf(direction), 11f * dt);
        }

        Vector2 next = ground + (direction * Speed * dt);
        next = Vector2.Clamp(next, new Vector2(-Terrain.HalfSize + 20f), new Vector2(Terrain.HalfSize - 20f));
        next = map.ResolveCircle(next, Radius, out _);
        DistanceWalked += Vector2.Distance(ground, next);

        float floor = map.Terrain.HeightAt(next);
        if (InWater && water is { } surface)
        {
            // Float chest deep instead of walking the pond bed.
            floor = MathF.Max(floor, surface - 1.15f);
        }

        float y = Position.Y;
        if (Grounded && input.Jump && !InWater)
        {
            _verticalVelocity = 6.2f;
            Grounded = false;
        }

        if (!Grounded)
        {
            _verticalVelocity -= Gravity * dt;
            y += _verticalVelocity * dt;
            if (y <= floor)
            {
                y = floor;
                Grounded = true;
                _verticalVelocity = 0f;
            }
        }
        else
        {
            y = floor;
        }

        Position = new Vector3(next.X, y, next.Y);
    }
}
