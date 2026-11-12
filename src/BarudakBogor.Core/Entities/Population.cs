using System.Numerics;
using BarudakBogor.Numerics;
using BarudakBogor.Sim;
using BarudakBogor.World;

namespace BarudakBogor.Entities;

public enum NpcKind
{
    Sopir,
    Mahasiswa,
    Pedagang,
    AnakSekolah,
    Pegawai,
    Ojol,
    Bapak,
    Ibu,
    AnakSd,
}

public enum PedestrianState
{
    Walk,
    Idle,
    Dodge,
}

/// <summary>A walker on the kerb. Pedestrians follow road edges and jump clear of vehicles.</summary>
public sealed class Pedestrian
{
    internal RoadEdge Edge = null!;
    internal float Distance;
    internal int Direction = 1;
    internal float Side = 1f;
    internal float Timer;
    internal Vector2 DodgeVelocity;

    public int Id { get; init; }

    public NpcKind Kind { get; init; }

    /// <summary>Model drawn for this walker; several look-alikes share a kind.</summary>
    public string Model { get; init; } = "npc_bapak";

    public Vector3 Position { get; internal set; }

    public float Yaw { get; internal set; }

    public float Speed { get; internal set; }

    public PedestrianState State { get; internal set; }

    public static string ModelOf(NpcKind kind) => kind switch
    {
        NpcKind.Sopir => "npc_sopir",
        NpcKind.Mahasiswa => "npc_mahasiswa",
        NpcKind.Pedagang => "npc_pedagang",
        NpcKind.AnakSekolah => "npc_anak_sekolah",
        NpcKind.Pegawai => "npc_pegawai",
        NpcKind.Ojol => "npc_ojol",
        NpcKind.Bapak => "npc_bapak",
        NpcKind.Ibu => "npc_ibu",
        _ => "npc_anak_sd",
    };

    /// <summary>Picks a model for a kind; townsfolk have several faces so a crowd does not look cloned.</summary>
    public static string ModelOf(NpcKind kind, uint roll) => kind switch
    {
        NpcKind.Bapak => (roll % 4) switch { 0 => "npc_bapak", 1 => "npc_haji_dadang", 2 => "npc_guru_bp", _ => "npc_haji_dudung" },
        NpcKind.Ibu => roll % 3 == 0 ? "npc_bu_euis" : "npc_ibu",
        NpcKind.Pedagang => (roll % 3) switch { 0 => "npc_pedagang", 1 => "npc_mang_udin", _ => "npc_abah_aceng" },
        NpcKind.AnakSd => roll % 3 == 0 ? "npc_anak_sekolah" : "npc_anak_sd",
        _ => ModelOf(kind),
    };
}

public enum AnimalKind
{
    Rusa,
    Tupai,
    Monyet,
    Kucing,
    Kerbau,
    Sapi,
    Kambing,
    Anjing,
    Ayam,
}

/// <summary>An animal wandering around its home range.</summary>
public sealed class Animal
{
    internal Vector2 Target;
    internal float Timer;
    internal int Zone;

    public int Id { get; init; }

    public AnimalKind Kind { get; init; }

    public Vector3 Position { get; internal set; }

    public float Yaw { get; internal set; }

    public float Speed { get; internal set; }

    public bool Fleeing { get; internal set; }

    public static string ModelOf(AnimalKind kind) => kind.ToString().ToLowerInvariant();
}

/// <summary>
/// Ambient life around the player: traffic on the lanes (driving on the left, as in Indonesia),
/// pedestrians on the kerbs and animals in their ranges. Everything is spawned in a ring
/// around the player and removed when left behind, so cost is constant however big the map is.
/// </summary>
public sealed class Population
{
    private const float SpawnNear = 70f;
    private const float SpawnFar = 200f;
    private const float DespawnDistance = 260f;
    private const float StrayRange = 110f;

    // People are small: far away they are dots, so the crowd is kept within a block of the player.
    private const float PeopleNear = 32f;
    private const float PeopleFar = 72f;
    private const float PeopleDespawn = 100f;

    private sealed class TrafficAgent
    {
        public required Vehicle Vehicle;
        public required RoadEdge Edge;
        public float Distance;
        public int Direction = 1;
        public float Cruise;
        public float Speed;
        public float StopTimer;
        public float NextStop;
        public float Blocked;
    }

    private readonly record struct AnimalZone(AnimalKind Kind, Vector2 Center, float Radius, int Count);

    private readonly WorldMap _map;
    private readonly List<TrafficAgent> _agents = [];
    private readonly List<Vehicle> _traffic = [];
    private readonly List<Pedestrian> _pedestrians = [];
    private readonly List<Animal> _animals = [];
    private readonly AnimalZone[] _zones;
    private readonly bool[] _zoneActive;
    private Rng _rng;
    private float _spawnTimer;
    private int _strays;
    private int _nextId = 10_000;

    public Population(WorldMap map, uint seed)
    {
        _map = map;
        _rng = new Rng(seed);
        Vector2 park = map.Districts.First(d => d.Id == DistrictId.KebunRaya).Center;
        _zones =
        [
            new(AnimalKind.Rusa, map[PoiId.IstanaRusa].Position, 110f, 8),
            new(AnimalKind.Tupai, park + new Vector2(120f, 80f), 260f, 5),
            new(AnimalKind.Kucing, map[PoiId.GangSelot].Position, 170f, 4),
            new(AnimalKind.Kucing, map[PoiId.PasarAnyar].Position, 190f, 3),
            new(AnimalKind.Kucing, map[PoiId.RumahUjang].Position, 120f, 2),
            new(AnimalKind.Monyet, map[PoiId.PuncakPass].Position, 240f, 5),
            new(AnimalKind.Monyet, map[PoiId.KebunTeh].Position, 200f, 3),
            new(AnimalKind.Kerbau, map[PoiId.SituGede].Position + new Vector2(260f, 240f), 240f, 3),
            new(AnimalKind.Sapi, map[PoiId.Ciawi].Position + new Vector2(200f, -160f), 260f, 3),
            new(AnimalKind.Kambing, map[PoiId.Empang].Position + new Vector2(-90f, 120f), 200f, 2),
            new(AnimalKind.Kambing, map[PoiId.VillaMisterius].Position, 90f, 2),
        ];
        _zoneActive = new bool[_zones.Length];
    }

    public IReadOnlyList<Vehicle> Traffic => _traffic;

    public IReadOnlyList<Pedestrian> Pedestrians => _pedestrians;

    public IReadOnlyList<Animal> Animals => _animals;

    /// <summary>Scales how many people and cars are around (settings / low-end machines).</summary>
    public float Density { get; set; } = 1f;

    /// <summary>Raised when a pedestrian leaps out of the player's way.</summary>
    public event Action<Pedestrian>? PedestrianDodged;

    /// <summary>Raised when a traffic driver honks at the player.</summary>
    public event Action<Vehicle>? Honked;

    public void Clear()
    {
        _agents.Clear();
        _traffic.Clear();
        _pedestrians.Clear();
        _animals.Clear();
        _strays = 0;
        Array.Clear(_zoneActive);
    }

    public void Update(float dt, Player player, GameClock clock, WeatherSystem weather)
    {
        Vector2 focus = player.Ground;
        _spawnTimer -= dt;
        if (_spawnTimer <= 0f)
        {
            _spawnTimer = 0.2f;
            Spawn(focus, MathUtil.Forward(player.Vehicle?.Yaw ?? player.Yaw), clock, weather);
        }

        UpdateTraffic(dt, player, focus);
        UpdatePedestrians(dt, player, focus, weather);
        UpdateAnimals(dt, player, focus);
    }

    // ------------------------------------------------------------------ spawning

    private void Spawn(Vector2 focus, Vector2 facing, GameClock clock, WeatherSystem weather)
    {
        LandUse use = _map.LandUseAt(focus);
        float night = clock.IsNight ? 0.45f : 1f;
        int wantedPeople = (int)(use switch
        {
            LandUse.Pasar => 60,
            LandUse.Kota => 46,
            LandUse.Gang or LandUse.Kampus => 36,
            LandUse.Kampung => 34,
            LandUse.Modern or LandUse.Taman => 16,
            LandUse.Villa => 9,
            _ => 5,
        } * night * (1f - (0.45f * weather.Rain)) * Density);
        int wantedCars = (int)(use switch
        {
            LandUse.Kota or LandUse.Pasar => 18,
            LandUse.Kampus or LandUse.Modern or LandUse.Kampung => 13,
            LandUse.Gang => 6,
            _ => 9,
        } * (clock.IsNight ? 0.55f : 1f) * Density);
        int wantedStrays = (int)(use switch
        {
            LandUse.Kampung or LandUse.Gang => 9,
            LandUse.Pasar => 7,
            LandUse.Kota or LandUse.Villa => 5,
            LandUse.Sawah => 4,
            LandUse.Kampus or LandUse.Modern => 3,
            _ => 0,
        } * Density);

        // A few tries per tick so a busy street fills within seconds of arriving.
        for (int i = 0; i < 4 && _pedestrians.Count < wantedPeople; i++)
        {
            // An empty street (just arrived) fills from close by; after that people walk in from further off.
            // Later arrivals step out close by only behind the player's back, so nobody pops into view.
            bool filling = _pedestrians.Count < wantedPeople / 2;
            float nearest = filling ? 14f : i % 2 == 0 ? 22f : PeopleNear;
            if (RandomRoadPoint(focus, e => e.Kind != RoadKind.Toll, nearest, PeopleFar) is { } walk
                && (filling || nearest >= PeopleNear || Vector2.Dot(Vector2.Normalize(walk.Point - focus), facing) < -0.25f))
            {
                SpawnPedestrian(walk, clock);
            }
        }

        for (int i = 0; i < 2 && _agents.Count < wantedCars; i++)
        {
            if (RandomRoadPoint(focus, null, _agents.Count < wantedCars / 2 ? 30f : SpawnNear) is { } drive)
            {
                SpawnTraffic(drive);
            }
        }

        if (_strays < wantedStrays)
        {
            SpawnStray(focus, use);
        }

        for (int i = 0; i < _zones.Length; i++)
        {
            AnimalZone zone = _zones[i];
            bool near = Vector2.Distance(focus, zone.Center) < zone.Radius + 280f;
            if (near && !_zoneActive[i])
            {
                _zoneActive[i] = true;
                for (int k = 0; k < zone.Count; k++)
                {
                    SpawnAnimal(i, zone);
                }
            }
            else if (!near && _zoneActive[i] && Vector2.Distance(focus, zone.Center) > zone.Radius + 380f)
            {
                _zoneActive[i] = false;
                _animals.RemoveAll(a => a.Zone == i);
            }
        }
    }

    private RoadHit? RandomRoadPoint(Vector2 focus, Func<RoadEdge, bool>? filter, float nearest, float farthest = SpawnFar)
    {
        float angle = _rng.Range(0f, MathUtil.Tau);
        float distance = _rng.Range(nearest + 6f, nearest < SpawnNear ? MathF.Min(farthest, 120f) : farthest);
        Vector2 p = focus + (new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * distance);
        RoadHit? hit = _map.Roads.Nearest(p, 80f, filter);
        return hit is { } h && Vector2.Distance(h.Point, focus) > nearest ? hit : null;
    }

    private void SpawnPedestrian(RoadHit hit, GameClock clock)
    {
        LandUse use = _map.LandUseAt(hit.Point);
        float roll = _rng.NextFloat();
        bool schoolRun = clock.Hours is (>= 6f and < 7.5f) or (>= 12.5f and < 14f);
        NpcKind kind = use switch
        {
            LandUse.Kampus => roll < 0.5f ? NpcKind.Mahasiswa : roll < 0.62f ? NpcKind.Ojol : roll < 0.75f ? NpcKind.Pedagang : roll < 0.88f ? NpcKind.Bapak : NpcKind.Ibu,
            LandUse.Pasar => roll < 0.34f ? NpcKind.Pedagang : roll < 0.62f ? NpcKind.Ibu : roll < 0.8f ? NpcKind.Bapak : roll < 0.9f ? NpcKind.Sopir : NpcKind.AnakSd,
            LandUse.Kota or LandUse.Modern => roll < 0.2f ? NpcKind.Pegawai : roll < 0.38f ? NpcKind.Bapak : roll < 0.56f ? NpcKind.Ibu
                : roll < 0.68f ? NpcKind.Mahasiswa : roll < 0.8f ? NpcKind.Ojol : roll < 0.92f ? NpcKind.Pedagang : NpcKind.AnakSd,
            _ => roll < 0.26f ? NpcKind.Bapak : roll < 0.52f ? NpcKind.Ibu : roll < 0.7f ? NpcKind.Pedagang : roll < 0.82f ? NpcKind.AnakSd : roll < 0.92f ? NpcKind.Sopir : NpcKind.Ojol,
        };
        if (schoolRun && _rng.Chance(0.45f))
        {
            kind = _rng.Chance(0.6f) ? NpcKind.AnakSd : NpcKind.AnakSekolah;
        }

        Pedestrian pedestrian = new()
        {
            Id = _nextId++,
            Kind = kind,
            Model = Pedestrian.ModelOf(kind, _rng.NextUInt()),
            Edge = hit.Edge,
            Distance = hit.Distance,
            Direction = _rng.Chance(0.5f) ? 1 : -1,
            Side = _rng.Chance(0.5f) ? 1f : -1f,
            Speed = _rng.Range(1.1f, 1.6f),
        };
        PlaceOnKerb(pedestrian);
        _pedestrians.Add(pedestrian);
    }

    private void SpawnTraffic(RoadHit hit)
    {
        RoadEdge edge = hit.Edge;
        float roll = _rng.NextFloat();
        VehicleKind kind = edge.Kind switch
        {
            RoadKind.Gang => roll < 0.85f ? VehicleKind.Motor : VehicleKind.Becak,
            RoadKind.Toll => roll < 0.45f ? VehicleKind.MobilKeluarga : roll < 0.7f ? VehicleKind.Sedan : roll < 0.92f ? VehicleKind.Truk : VehicleKind.TahuBulat,
            _ => roll < 0.24f ? VehicleKind.Angkot
                : roll < 0.52f ? VehicleKind.Motor
                : roll < 0.64f ? VehicleKind.MobilKeluarga
                : roll < 0.73f ? VehicleKind.Sedan
                : roll < 0.8f ? VehicleKind.Truk
                : roll < 0.86f ? VehicleKind.Becak
                : roll < 0.91f ? VehicleKind.Bemo
                : roll < 0.94f ? VehicleKind.Bajaj
                : roll < 0.96f ? VehicleKind.TahuBulat
                : roll < 0.98f ? VehicleKind.Bakso
                : VehicleKind.Delman,
        };
        // Delmans, becaks and bemos belong to the old town; out on the open road they would hold everyone up.
        if (kind is VehicleKind.Delman or VehicleKind.Becak or VehicleKind.Bemo
            && _map.DistrictAt(hit.Point)?.Id is not (DistrictId.PusatKota or DistrictId.Empang or DistrictId.PasarAnyar or DistrictId.GangSelot))
        {
            kind = roll < 0.9f ? VehicleKind.Motor : VehicleKind.Truk;
        }

        Vehicle vehicle = new(_nextId++, kind) { IsTraffic = true, Occupied = true };
        TrafficAgent agent = new()
        {
            Vehicle = vehicle,
            Edge = edge,
            Distance = hit.Distance,
            Direction = _rng.Chance(0.5f) ? 1 : -1,
            NextStop = _rng.Range(15f, 60f),
        };
        agent.Cruise = MathF.Min(edge.SpeedLimit * _rng.Range(0.75f, 1f), vehicle.Spec.MaxSpeed * 0.9f);
        agent.Speed = agent.Cruise;
        (Vector2 position, float yaw) = LanePose(agent);
        // Do not drop a car on top of another one.
        foreach (TrafficAgent other in _agents)
        {
            if (Vector2.DistanceSquared(other.Vehicle.Ground, position) < 100f)
            {
                return;
            }
        }

        vehicle.MoveKinematic(_map, position, yaw, agent.Speed, 0f);
        _agents.Add(agent);
        _traffic.Add(vehicle);
    }

    private void SpawnAnimal(int zoneIndex, AnimalZone zone)
    {
        for (int attempt = 0; attempt < 8; attempt++)
        {
            Vector2 p = RandomInZone(zone);
            if (_map.IsInsideBuilding(p, 1f) || _map.Roads.IsOnRoad(p, 1f) || _map.Terrain.WaterLevelAt(p) is not null)
            {
                continue;
            }

            _animals.Add(new Animal
            {
                Id = _nextId++,
                Kind = zone.Kind,
                Zone = zoneIndex,
                Position = p.ToWorld(_map.Terrain.HeightAt(p)),
                Yaw = _rng.Range(0f, MathUtil.Tau),
                Target = p,
                Timer = _rng.Range(0f, 4f),
            });
            return;
        }
    }

    /// <summary>Cats, dogs and chickens that belong to no particular place: they turn up wherever people live.</summary>
    private void SpawnStray(Vector2 focus, LandUse use)
    {
        float angle = _rng.Range(0f, MathUtil.Tau);
        Vector2 p = focus + (new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * _rng.Range(25f, StrayRange - 15f));
        if (_map.IsInsideBuilding(p, 0.8f) || _map.Roads.IsOnRoad(p, 0.5f) || _map.Terrain.WaterLevelAt(p) is not null)
        {
            return;
        }

        float roll = _rng.NextFloat();
        AnimalKind kind = use switch
        {
            LandUse.Sawah => roll < 0.6f ? AnimalKind.Ayam : roll < 0.85f ? AnimalKind.Anjing : AnimalKind.Kambing,
            LandUse.Pasar or LandUse.Gang => roll < 0.5f ? AnimalKind.Kucing : roll < 0.75f ? AnimalKind.Ayam : AnimalKind.Anjing,
            LandUse.Kampung => roll < 0.4f ? AnimalKind.Ayam : roll < 0.7f ? AnimalKind.Kucing : AnimalKind.Anjing,
            _ => roll < 0.55f ? AnimalKind.Kucing : AnimalKind.Anjing,
        };
        _strays++;
        _animals.Add(new Animal
        {
            Id = _nextId++,
            Kind = kind,
            Zone = -1,
            Position = p.ToWorld(_map.Terrain.HeightAt(p)),
            Yaw = _rng.Range(0f, MathUtil.Tau),
            Target = p,
            Timer = _rng.Range(0f, 4f),
        });
    }

    private Vector2 RandomInZone(AnimalZone zone)
    {
        float angle = _rng.Range(0f, MathUtil.Tau);
        float distance = MathF.Sqrt(_rng.NextFloat()) * zone.Radius;
        return zone.Center + (new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * distance);
    }

    // ------------------------------------------------------------------ traffic

    private (Vector2 Position, float Yaw) LanePose(TrafficAgent agent)
    {
        Vector2 point = agent.Edge.Sample(agent.Distance, out Vector2 direction);
        Vector2 travel = direction * agent.Direction;
        // Keep left. Alleys have a single lane down the middle.
        float lane = agent.Edge.Kind == RoadKind.Gang ? 0f : agent.Edge.Width * 0.25f;
        return (point + (MathUtil.Left(travel) * lane), MathUtil.YawOf(travel));
    }

    private void UpdateTraffic(float dt, Player player, Vector2 focus)
    {
        Vehicle? playerVehicle = player.Vehicle;
        float playerRadius = playerVehicle?.Spec.Radius ?? Player.Radius;
        for (int i = _agents.Count - 1; i >= 0; i--)
        {
            TrafficAgent agent = _agents[i];
            Vehicle vehicle = agent.Vehicle;
            Vector2 position = vehicle.Ground;
            if (Vector2.DistanceSquared(position, focus) > DespawnDistance * DespawnDistance)
            {
                _agents.RemoveAt(i);
                _traffic.Remove(vehicle);
                continue;
            }

            Vector2 forward = vehicle.Forward;
            float lookAhead = 7f + (agent.Speed * 0.9f);
            bool blocked = IsAhead(position, forward, focus, lookAhead, 1.6f + playerRadius);
            bool blockedByPlayer = blocked;
            for (int k = 0; k < _agents.Count && !blocked; k++)
            {
                if (k != i)
                {
                    blocked = IsAhead(position, forward, _agents[k].Vehicle.Ground, lookAhead, 2f);
                }
            }

            // Angkots stop to wait for passengers ("ngetem") wherever they please.
            agent.NextStop -= dt;
            if (agent.NextStop <= 0f)
            {
                agent.NextStop = _rng.Range(35f, 90f);
                if (vehicle.Spec.Kind == VehicleKind.Angkot && agent.Edge.Kind != RoadKind.Toll)
                {
                    agent.StopTimer = _rng.Range(4f, 9f);
                }
            }

            agent.StopTimer = MathF.Max(0f, agent.StopTimer - dt);
            float target = blocked || agent.StopTimer > 0f ? 0f : agent.Cruise;
            float rate = target < agent.Speed ? vehicle.Spec.Braking : vehicle.Spec.Acceleration * 0.6f;
            agent.Speed += Math.Clamp(target - agent.Speed, -rate * dt, rate * dt);

            vehicle.Horn = false;
            if (blockedByPlayer && agent.Speed < 0.5f)
            {
                agent.Blocked += dt;
                if (agent.Blocked > 2.5f)
                {
                    agent.Blocked = -_rng.Range(2f, 5f);
                    vehicle.Horn = true;
                    Honked?.Invoke(vehicle);
                }
            }
            else if (agent.Blocked > 0f)
            {
                agent.Blocked = 0f;
            }

            agent.Distance += agent.Speed * agent.Direction * dt;
            if (agent.Distance > agent.Edge.Length || agent.Distance < 0f)
            {
                NextEdge(agent);
            }

            (Vector2 lanePosition, float laneYaw) = LanePose(agent);
            // Ease onto the new lane after a junction instead of snapping.
            Vector2 eased = Vector2.Lerp(position, lanePosition, MathUtil.Damp(6f, dt));
            float yaw = MathUtil.TurnToward(vehicle.Yaw, laneYaw, 2.6f * dt);
            vehicle.MoveKinematic(_map, eased, yaw, agent.Speed, dt);
        }
    }

    private static bool IsAhead(Vector2 position, Vector2 forward, Vector2 other, float range, float halfWidth)
    {
        Vector2 offset = other - position;
        float ahead = Vector2.Dot(offset, forward);
        return ahead > 0.5f && ahead < range && MathF.Abs(Vector2.Dot(offset, MathUtil.Left(forward))) < halfWidth;
    }

    private void NextEdge(TrafficAgent agent)
    {
        int nodeId = agent.Direction > 0 ? agent.Edge.To : agent.Edge.From;
        RoadNode node = _map.Roads.Nodes[nodeId];
        VehicleKind kind = agent.Vehicle.Spec.Kind;
        RoadEdge? choice = null;
        int seen = 0;
        foreach (int edgeId in node.Edges)
        {
            RoadEdge candidate = _map.Roads.Edges[edgeId];
            if (candidate == agent.Edge
                || (candidate.Kind == RoadKind.Gang && kind is not (VehicleKind.Motor or VehicleKind.Becak))
                || (candidate.Kind == RoadKind.Toll && kind is VehicleKind.Delman or VehicleKind.Bajaj or VehicleKind.Becak or VehicleKind.Bemo))
            {
                continue;
            }

            // Reservoir sampling: every valid exit is equally likely without allocating a list.
            if (_rng.Range(0, ++seen) == 0)
            {
                choice = candidate;
            }
        }

        if (choice is null)
        {
            // Dead end: turn around.
            agent.Direction = -agent.Direction;
            agent.Distance = Math.Clamp(agent.Distance, 0f, agent.Edge.Length);
            return;
        }

        agent.Edge = choice;
        agent.Direction = choice.From == nodeId ? 1 : -1;
        agent.Distance = agent.Direction > 0 ? 0f : choice.Length;
        agent.Cruise = MathF.Min(choice.SpeedLimit * _rng.Range(0.75f, 1f), agent.Vehicle.Spec.MaxSpeed * 0.9f);
    }

    // ------------------------------------------------------------------ pedestrians

    private void PlaceOnKerb(Pedestrian pedestrian)
    {
        Vector2 point = pedestrian.Edge.Sample(pedestrian.Distance, out Vector2 direction);
        float offset = (pedestrian.Edge.Width * 0.5f) + (pedestrian.Edge.Kind == RoadKind.Gang ? -0.6f : 1.2f);
        Vector2 position = point + (MathUtil.Left(direction) * pedestrian.Side * offset);
        pedestrian.Position = position.ToWorld(_map.Terrain.HeightAt(position));
        pedestrian.Yaw = MathUtil.YawOf(direction * pedestrian.Direction);
    }

    private void UpdatePedestrians(float dt, Player player, Vector2 focus, WeatherSystem weather)
    {
        Vehicle? car = player.Vehicle is { Spec.IsCart: false } v ? v : null;
        for (int i = _pedestrians.Count - 1; i >= 0; i--)
        {
            Pedestrian p = _pedestrians[i];
            Vector2 position = new(p.Position.X, p.Position.Z);
            if (Vector2.DistanceSquared(position, focus) > PeopleDespawn * PeopleDespawn)
            {
                _pedestrians.RemoveAt(i);
                continue;
            }

            // Leap clear of a vehicle bearing down on us. Nobody gets hurt in Bogor.
            if (car is not null && p.State != PedestrianState.Dodge && MathF.Abs(car.Speed) > 2.5f)
            {
                Vector2 offset = position - car.Ground;
                float distance = offset.Length();
                Vector2 heading = Vector2.Normalize(car.Velocity);
                if (distance < 3f + (MathF.Abs(car.Speed) * 0.45f) && Vector2.Dot(offset, heading) > 0f
                    && MathF.Abs(Vector2.Dot(offset, MathUtil.Left(heading))) < 2.2f)
                {
                    float side = Vector2.Dot(offset, MathUtil.Left(heading)) >= 0f ? 1f : -1f;
                    p.State = PedestrianState.Dodge;
                    p.Timer = 0.7f;
                    p.DodgeVelocity = MathUtil.Left(heading) * side * 6f;
                    PedestrianDodged?.Invoke(p);
                }
            }

            switch (p.State)
            {
                case PedestrianState.Dodge:
                    position = _map.ResolveCircle(position + (p.DodgeVelocity * dt), 0.3f, out _);
                    p.Position = position.ToWorld(_map.Terrain.HeightAt(position));
                    p.Yaw = MathUtil.YawOf(p.DodgeVelocity);
                    p.Speed = 6f;
                    p.Timer -= dt;
                    if (p.Timer <= 0f)
                    {
                        // Rejoin the nearest kerb.
                        if (_map.Roads.Nearest(position, 60f, e => e.Kind != RoadKind.Toll) is { } hit)
                        {
                            p.Edge = hit.Edge;
                            p.Distance = hit.Distance;
                            p.Side = hit.Lateral >= 0f ? 1f : -1f;
                        }

                        p.State = PedestrianState.Idle;
                        p.Timer = 1.5f;
                        p.Speed = 0f;
                    }

                    break;

                case PedestrianState.Idle:
                    p.Speed = 0f;
                    p.Timer -= dt;
                    if (p.Timer <= 0f)
                    {
                        p.State = PedestrianState.Walk;
                        p.Timer = _rng.Range(12f, 40f);
                    }

                    break;

                default:
                    // People hurry when it pours.
                    float pace = _rng.Range(1.15f, 1.5f) * (1f + (0.8f * weather.Rain));
                    p.Speed += (pace - p.Speed) * MathUtil.Damp(3f, dt);
                    p.Distance += p.Speed * p.Direction * dt;
                    if (p.Distance > p.Edge.Length || p.Distance < 0f)
                    {
                        NextEdge(p);
                    }

                    Vector2 before = position;
                    PlaceOnKerb(p);
                    // Smooth the corner when switching edges.
                    Vector2 wanted = new(p.Position.X, p.Position.Z);
                    Vector2 eased = Vector2.Distance(before, wanted) > 6f ? wanted : Vector2.Lerp(before, wanted, MathUtil.Damp(8f, dt));
                    p.Position = eased.ToWorld(_map.Terrain.HeightAt(eased));
                    p.Timer -= dt;
                    if (p.Timer <= 0f)
                    {
                        p.State = PedestrianState.Idle;
                        p.Timer = _rng.Range(2f, 7f);
                    }

                    break;
            }
        }
    }

    private void NextEdge(Pedestrian p)
    {
        int nodeId = p.Direction > 0 ? p.Edge.To : p.Edge.From;
        RoadNode node = _map.Roads.Nodes[nodeId];
        RoadEdge? choice = null;
        int seen = 0;
        foreach (int edgeId in node.Edges)
        {
            RoadEdge candidate = _map.Roads.Edges[edgeId];
            if (candidate != p.Edge && candidate.Kind != RoadKind.Toll && _rng.Range(0, ++seen) == 0)
            {
                choice = candidate;
            }
        }

        if (choice is null)
        {
            p.Direction = -p.Direction;
            p.Distance = Math.Clamp(p.Distance, 0f, p.Edge.Length);
            return;
        }

        p.Edge = choice;
        p.Direction = choice.From == nodeId ? 1 : -1;
        p.Distance = p.Direction > 0 ? 0f : choice.Length;
    }

    // ------------------------------------------------------------------ animals

    private void UpdateAnimals(float dt, Player player, Vector2 focus)
    {
        bool playerFast = player.Speed > 3f;
        for (int i = _animals.Count - 1; i >= 0; i--)
        {
            // Strays left behind are forgotten; the next street has its own.
            if (_animals[i].Zone < 0 && Vector2.DistanceSquared(_animals[i].Position.Xz(), focus) > (StrayRange + 30f) * (StrayRange + 30f))
            {
                _animals.RemoveAt(i);
                _strays--;
            }
        }

        foreach (Animal animal in _animals)
        {
            Vector2 position = new(animal.Position.X, animal.Position.Z);
            AnimalZone zone = animal.Zone >= 0 ? _zones[animal.Zone] : new AnimalZone(animal.Kind, position, 14f, 1);
            float toPlayer = Vector2.Distance(position, focus);
            (float walk, float run, float shy) = animal.Kind switch
            {
                AnimalKind.Rusa => (1.1f, 7f, 7f),
                AnimalKind.Tupai => (1.6f, 5f, 5f),
                AnimalKind.Kucing => (1f, 5f, 2.5f),
                AnimalKind.Monyet => (1.5f, 4.5f, 0f),
                AnimalKind.Kambing => (0.9f, 3.5f, 2f),
                AnimalKind.Anjing => (1.3f, 5.5f, 0f),
                AnimalKind.Ayam => (0.8f, 3.6f, 3f),
                _ => (0.6f, 1.2f, 0f),
            };

            animal.Fleeing = shy > 0f && toPlayer < shy * (playerFast ? 1.8f : 1f);
            if (animal.Fleeing)
            {
                Vector2 away = toPlayer > 0.01f ? (position - focus) / toPlayer : Vector2.UnitX;
                animal.Target = position + (away * 12f);
                animal.Timer = 1.5f;
            }
            else if (animal.Kind is AnimalKind.Monyet or AnimalKind.Anjing && player.Vehicle is null && toPlayer is < 16f and > 2.5f
                && (animal.Kind == AnimalKind.Monyet || (animal.Id & 1) == 0))
            {
                // Puncak macaques are after your snacks.
                animal.Target = focus;
                animal.Timer = 1f;
            }
            else
            {
                animal.Timer -= dt;
                if (animal.Timer <= 0f)
                {
                    animal.Timer = _rng.Range(3f, 10f);
                    // Half the time just stand and graze.
                    animal.Target = _rng.Chance(0.5f) ? position : RandomInZone(zone);
                }
            }

            Vector2 toTarget = animal.Target - position;
            float distance = toTarget.Length();
            float speed = distance < 0.4f ? 0f : animal.Fleeing ? run : walk;
            animal.Speed += (speed - animal.Speed) * MathUtil.Damp(5f, dt);
            if (distance > 0.4f)
            {
                animal.Yaw = MathUtil.TurnToward(animal.Yaw, MathUtil.YawOf(toTarget), 5f * dt);
                Vector2 next = position + (MathUtil.Forward(animal.Yaw) * animal.Speed * dt);
                next = _map.ResolveCircle(next, 0.3f, out _);
                if (_map.Terrain.WaterLevelAt(next) is null)
                {
                    animal.Position = next.ToWorld(_map.Terrain.HeightAt(next));
                }
                else
                {
                    animal.Target = position;
                }
            }
        }
    }
}
