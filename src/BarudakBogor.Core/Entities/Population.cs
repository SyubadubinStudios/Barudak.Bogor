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

    /// <summary>Pedagang kaki lima: a hawker with a carrying pole.</summary>
    Pkl,
    Polisi,

    /// <summary>A civil servant in khaki.</summary>
    Pns,
    Badut,
    Pengamen,
    AnakPunk,
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
    internal bool CarriesUmbrella;

    public int Id { get; init; }

    public NpcKind Kind { get; init; }

    /// <summary>True while this walker has an umbrella up (they carry one and it is raining).</summary>
    public bool Umbrella { get; internal set; }

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
        NpcKind.Pkl => "npc_pkl",
        NpcKind.Polisi => "npc_polisi",
        NpcKind.Pns => "npc_pns",
        NpcKind.Badut => "npc_badut",
        NpcKind.Pengamen => "npc_pengamen",
        NpcKind.AnakPunk => "npc_anak_punk",
        _ => "npc_anak_sd",
    };

    /// <summary>People who are on the street to be on the street: they stand about more than they walk.</summary>
    public static bool Lingers(NpcKind kind) => kind is NpcKind.Polisi or NpcKind.Badut or NpcKind.Pengamen or NpcKind.AnakPunk;

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

    /// <summary>Town pigeons: they peck about in a flock and fly off when someone comes close.</summary>
    Burung,

    /// <summary>Carp just under the surface of ponds and rivers; they dart off when someone wades in.</summary>
    Ikan,

    /// <summary>Pond turtles paddling about with the shell out of the water.</summary>
    KuraKura,

    /// <summary>A lazy crocodile adrift in the river. It wants nothing to do with anybody and swims off.</summary>
    Buaya,

    /// <summary>Ducks on the water.</summary>
    Bebek,
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

    /// <summary>True while a bird is on the wing; <see cref="Position"/> is then above the ground.</summary>
    public bool Airborne { get; internal set; }

    internal float Altitude;

    public static string ModelOf(AnimalKind kind) => kind.ToString().ToLowerInvariant();

    /// <summary>True for the animals that live in the water and never leave it.</summary>
    public static bool IsAquatic(AnimalKind kind) => kind is AnimalKind.Ikan or AnimalKind.KuraKura or AnimalKind.Buaya or AnimalKind.Bebek;
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

        /// <summary>Metres moved over towards the middle of the road, to go round <see cref="Passing"/>.</summary>
        public float Pass;
        public Vehicle? Passing;
        public float PassTime;

        /// <summary>Seconds spent standing still behind other traffic.</summary>
        public float Waited;

        /// <summary>Standing in line for a good reason (a red light, a train), or behind someone who is.</summary>
        public bool Queued;
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
            new(AnimalKind.Monyet, Vector2.Lerp(map[PoiId.GerbangKebunRaya].Position, park, 0.35f), 150f, 5),
            new(AnimalKind.Ayam, map[PoiId.RumahUjang].Position, 70f, 4),
            new(AnimalKind.Ayam, map[PoiId.Empang].Position, 90f, 4),
            new(AnimalKind.Ayam, map[PoiId.GangSelot].Position, 100f, 3),
            new(AnimalKind.Burung, map[PoiId.TuguKujang].Position, 55f, 8),
            new(AnimalKind.Burung, map[PoiId.PasarAnyar].Position, 80f, 8),
            new(AnimalKind.Burung, map[PoiId.MasjidRaya].Position, 55f, 6),
            new(AnimalKind.Burung, map[PoiId.StasiunBogor].Position, 60f, 6),
            new(AnimalKind.Burung, map[PoiId.GerbangKebunRaya].Position, 70f, 6),
            new(AnimalKind.Burung, map[PoiId.BalaiKota].Position, 50f, 5),
            new(AnimalKind.Burung, map[PoiId.AlunAlun].Position, 60f, 7),
            new(AnimalKind.Burung, map[PoiId.LapanganSempur].Position, 70f, 6),
            new(AnimalKind.Kerbau, map[PoiId.SituGede].Position + new Vector2(260f, 240f), 240f, 3),
            new(AnimalKind.Sapi, map[PoiId.Ciawi].Position + new Vector2(200f, -160f), 260f, 3),
            new(AnimalKind.Kambing, map[PoiId.Empang].Position + new Vector2(-90f, 120f), 200f, 2),
            new(AnimalKind.Kambing, map[PoiId.VillaMisterius].Position, 90f, 2),
            .. WaterZones(map),
        ];
        _zoneActive = new bool[_zones.Length];
    }

    public IReadOnlyList<Vehicle> Traffic => _traffic;

    public IReadOnlyList<Pedestrian> Pedestrians => _pedestrians;

    public IReadOnlyList<Animal> Animals => _animals;

    /// <summary>Scales how many people and cars are around (settings / low-end machines).</summary>
    public float Density { get; set; } = 1f;

    /// <summary>
    /// How full the streets are at an hour of the day (1 = the busiest): nearly empty before
    /// dawn, peaks at the morning market, lunch and going-home time, thinning out after dark.
    /// </summary>
    public static float CrowdFactor(float hours) => hours switch
    {
        < 4f => 0.15f,
        < 6f => 0.15f + ((hours - 4f) * 0.35f),
        < 8f => 1f,
        < 11f => 0.85f,
        < 13f => 1f,
        < 16f => 0.8f,
        < 19f => 1f,
        < 22f => 1f - ((hours - 19f) * 0.2f),
        _ => 0.25f,
    };

    /// <summary>
    /// How likely a kind of person is to be out at an hour (0 = never, 1 = as usual, above 1 =
    /// their rush hour). Children are in class through the morning, office workers appear at
    /// the commute and at lunch, and only ojol riders, traders and the night watch keep late hours.
    /// </summary>
    public static float ScheduleFactor(NpcKind kind, float hours) => kind switch
    {
        NpcKind.AnakSd or NpcKind.AnakSekolah => hours switch
        {
            >= 6f and < 7.5f => 1.6f,
            >= 7.5f and < 12.5f => 0.04f,
            >= 12.5f and < 14.5f => 1.6f,
            >= 14.5f and < 18f => 0.5f,
            _ => 0f,
        },
        NpcKind.Pegawai => hours switch
        {
            >= 6.5f and < 9f => 1.5f,
            >= 9f and < 11.5f => 0.4f,
            >= 11.5f and < 13.5f => 1.2f,
            >= 13.5f and < 16f => 0.4f,
            >= 16f and < 19f => 1.5f,
            >= 19f and < 22f => 0.3f,
            _ => 0.03f,
        },
        NpcKind.Mahasiswa => hours switch { >= 7f and < 22f => 1f, >= 22f => 0.4f, _ => 0.05f },
        NpcKind.Pedagang => hours is >= 4.5f and < 21f ? 1f : 0.35f,
        NpcKind.Ibu => hours switch { >= 5f and < 11f => 1.4f, >= 11f and < 18f => 0.9f, >= 18f and < 21f => 0.5f, _ => 0.04f },
        NpcKind.Bapak => hours is >= 5f and < 22f ? 1f : 0.5f,
        NpcKind.Ojol => hours is >= 19f or < 5f ? 1.3f : 1f,
        NpcKind.Pns => hours switch { >= 6.5f and < 8.5f => 1.5f, >= 8.5f and < 15.5f => 0.6f, >= 15.5f and < 17.5f => 1.5f, _ => 0f },
        NpcKind.Polisi => hours is >= 6f and < 20f ? 1f : 0.25f,
        NpcKind.Pkl => hours is >= 5f and < 20f ? 1f : 0.15f,
        NpcKind.Badut => hours is >= 9f and < 18f ? 1f : 0f,
        NpcKind.Pengamen => hours is >= 8f and < 23f ? 1f : 0.2f,
        NpcKind.AnakPunk => hours switch { >= 15f or < 2f => 1.3f, >= 10f and < 15f => 0.5f, _ => 0.1f },
        _ => hours is >= 5f and < 22f ? 1f : 0.5f,
    };

    /// <summary>The commuter train: traffic waits at a level crossing while it passes.</summary>
    public Train? Train { get; set; }

    /// <summary>The vehicles that are not traffic (parked, mission, the player's): traffic keeps clear of them too.</summary>
    public IReadOnlyList<Vehicle>? Parked { get; set; }

    /// <summary>The clock the traffic lights run on (see <see cref="TrafficSignal.ColourAt"/>).</summary>
    public double SignalTime { get; private set; }

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
        SignalTime += dt;
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
        float night = CrowdFactor(clock.Hours);
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
        bool schoolRun = clock.Hours is (>= 6f and < 7.5f) or (>= 12.5f and < 14f);
        // Draw from who lives around here, then keep the draw only if that kind of person is
        // out at this hour; whoever is left after a few tries is the night's usual company.
        NpcKind kind = clock.Hours is >= 5f and < 22f ? NpcKind.Bapak : NpcKind.Ojol;
        for (int attempt = 0; attempt < 5; attempt++)
        {
            float roll = _rng.NextFloat();
            NpcKind drawn = use switch
            {
                LandUse.Kampus => roll < 0.5f ? NpcKind.Mahasiswa : roll < 0.62f ? NpcKind.Ojol : roll < 0.75f ? NpcKind.Pedagang : roll < 0.88f ? NpcKind.Bapak : NpcKind.Ibu,
                LandUse.Pasar => roll < 0.34f ? NpcKind.Pedagang : roll < 0.62f ? NpcKind.Ibu : roll < 0.8f ? NpcKind.Bapak : roll < 0.9f ? NpcKind.Sopir : NpcKind.AnakSd,
                LandUse.Kota or LandUse.Modern => roll < 0.2f ? NpcKind.Pegawai : roll < 0.38f ? NpcKind.Bapak : roll < 0.56f ? NpcKind.Ibu
                    : roll < 0.68f ? NpcKind.Mahasiswa : roll < 0.8f ? NpcKind.Ojol : roll < 0.92f ? NpcKind.Pedagang : NpcKind.AnakSd,
                _ => roll < 0.26f ? NpcKind.Bapak : roll < 0.52f ? NpcKind.Ibu : roll < 0.7f ? NpcKind.Pedagang : roll < 0.82f ? NpcKind.AnakSd : roll < 0.92f ? NpcKind.Sopir : NpcKind.Ojol,
            };
            // Street life on top of the residents: hawkers, buskers, the odd clown, the law.
            float extra = _rng.NextFloat();
            drawn = use switch
            {
                LandUse.Pasar => extra < 0.12f ? NpcKind.Pkl : extra < 0.16f ? NpcKind.Pengamen : extra < 0.19f ? NpcKind.Badut
                    : extra < 0.21f ? NpcKind.Polisi : drawn,
                LandUse.Kota or LandUse.Modern => extra < 0.06f ? NpcKind.Pns : extra < 0.1f ? NpcKind.Polisi : extra < 0.14f ? NpcKind.Pengamen
                    : extra < 0.18f ? NpcKind.AnakPunk : extra < 0.21f ? NpcKind.Badut : extra < 0.26f ? NpcKind.Pkl : drawn,
                LandUse.Kampus => extra < 0.05f ? NpcKind.Pengamen : extra < 0.1f ? NpcKind.Pkl : extra < 0.13f ? NpcKind.AnakPunk : drawn,
                LandUse.Kampung or LandUse.Gang => extra < 0.07f ? NpcKind.Pkl : extra < 0.09f ? NpcKind.Pengamen : extra < 0.12f ? NpcKind.AnakPunk : drawn,
                _ => drawn,
            };
            if (schoolRun && _rng.Chance(0.45f))
            {
                drawn = _rng.Chance(0.6f) ? NpcKind.AnakSd : NpcKind.AnakSekolah;
            }

            if (_rng.Chance(ScheduleFactor(drawn, clock.Hours)))
            {
                kind = drawn;
                break;
            }
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
            // Ojol riders wear a raincoat instead; children mostly forget theirs.
            CarriesUmbrella = _rng.Chance(kind switch
            {
                NpcKind.Ojol => 0.08f,
                NpcKind.AnakSd or NpcKind.AnakSekolah => 0.35f,
                NpcKind.Ibu or NpcKind.Pegawai => 0.8f,
                _ => 0.55f,
            }),
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

        if (Parked is { } parked)
        {
            foreach (Vehicle idle in parked)
            {
                if (Vector2.DistanceSquared(idle.Ground, position) < 36f)
                {
                    return;
                }
            }
        }

        vehicle.MoveKinematic(_map, position, yaw, agent.Speed, 0f);
        _agents.Add(agent);
        _traffic.Add(vehicle);
    }

    /// <summary>
    /// Where the water animals live: every pond has fish, turtles and ducks, and the two rivers
    /// have fish, a few turtles and the odd crocodile at the stretches people actually visit.
    /// </summary>
    private static IEnumerable<AnimalZone> WaterZones(WorldMap map)
    {
        foreach (Pond pond in map.Ponds)
        {
            float reach = pond.Radius * 0.9f;
            int size = pond.Radius > 100f ? 2 : 1;
            yield return new AnimalZone(AnimalKind.Ikan, pond.Center, reach, 9 * size);
            yield return new AnimalZone(AnimalKind.KuraKura, pond.Center, reach, 3 * size);
            yield return new AnimalZone(AnimalKind.Bebek, pond.Center, reach, 4 * size);
        }

        int river = 0;
        foreach (Corridor corridor in map.Terrain.Corridors)
        {
            if (corridor.Kind != CorridorKind.River)
            {
                continue;
            }

            // A home range every 400 m of river; which animal lives there goes round in turn.
            int range = 0;
            for (float along = 150f; along < corridor.Length - 150f; along += 400f, range++)
            {
                Vector2 centre = corridor.Sample(along, out _, out _);
                yield return new AnimalZone(AnimalKind.Ikan, centre, 55f, 6);
                AnimalKind other = ((range + river) % 4) switch
                {
                    0 => AnimalKind.Buaya,
                    1 => AnimalKind.KuraKura,
                    2 => AnimalKind.Bebek,
                    _ => AnimalKind.Ikan,
                };
                yield return new AnimalZone(other, centre, 55f, other == AnimalKind.Buaya ? 1 : 3);
            }

            river++;
        }
    }

    /// <summary>Water surface at <paramref name="p"/> when it is deep enough there for this animal, otherwise null.</summary>
    private float? Swimmable(Vector2 p, AnimalKind kind)
    {
        if (_map.Terrain.WaterLevelAt(p) is not { } level)
        {
            return null;
        }

        float needed = kind switch
        {
            AnimalKind.Buaya => 0.6f,
            AnimalKind.Ikan => 0.5f,
            _ => 0.3f,
        };
        return level - _map.Terrain.GroundAt(p.X, p.Y) >= needed ? level : null;
    }

    /// <summary>How far below the surface the model's origin rides: fish under it, the others with their back out.</summary>
    private static float Draught(AnimalKind kind) => kind switch
    {
        AnimalKind.Ikan => 0.17f,
        AnimalKind.Buaya => 0.2f,
        AnimalKind.KuraKura => 0.11f,
        _ => 0.03f,
    };

    private void SpawnAnimal(int zoneIndex, AnimalZone zone)
    {
        if (Animal.IsAquatic(zone.Kind))
        {
            for (int attempt = 0; attempt < 40; attempt++)
            {
                Vector2 p = RandomInZone(zone);
                if (Swimmable(p, zone.Kind) is { } level)
                {
                    _animals.Add(new Animal
                    {
                        Id = _nextId++,
                        Kind = zone.Kind,
                        Zone = zoneIndex,
                        Position = p.ToWorld(level - Draught(zone.Kind)),
                        Yaw = _rng.Range(0f, MathUtil.Tau),
                        Target = p,
                        Timer = _rng.Range(0f, 4f),
                    });
                    return;
                }
            }

            return;
        }

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
            LandUse.Pasar or LandUse.Gang => roll < 0.35f ? AnimalKind.Kucing : roll < 0.6f ? AnimalKind.Ayam : roll < 0.8f ? AnimalKind.Burung : AnimalKind.Anjing,
            LandUse.Kampung => roll < 0.4f ? AnimalKind.Ayam : roll < 0.65f ? AnimalKind.Kucing : roll < 0.8f ? AnimalKind.Burung : AnimalKind.Anjing,
            _ => roll < 0.35f ? AnimalKind.Kucing : roll < 0.75f ? AnimalKind.Burung : AnimalKind.Anjing,
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

    /// <summary>How far left of the centre line the agent drives. Keep left; an alley is one narrow lane each way.</summary>
    private static float LaneOffset(TrafficAgent agent) =>
        (agent.Edge.Kind == RoadKind.Gang ? 0.8f : agent.Edge.Width * 0.25f) - agent.Pass;

    private (Vector2 Position, float Yaw) LanePose(TrafficAgent agent)
    {
        Vector2 point = agent.Edge.Sample(agent.Distance, out Vector2 direction);
        // Look a little ahead and behind for the heading, so the car turns through a bend
        // instead of jerking at every point of the line.
        Vector2 ahead = agent.Edge.Sample(agent.Distance + 5f, out _) - agent.Edge.Sample(agent.Distance - 5f, out _);
        if (ahead.LengthSquared() > 1f)
        {
            direction = Vector2.Normalize(ahead);
        }

        Vector2 travel = direction * agent.Direction;
        return (point + (MathUtil.Left(travel) * LaneOffset(agent)), MathUtil.YawOf(travel));
    }

    /// <summary>Something a driver has to keep clear of: another car, a parked one, the player.</summary>
    private struct Obstacle
    {
        public Vector2 Position;
        public Vector2 Forward;
        public float Speed;
        public float HalfLength;
        public float HalfWidth;
        public int Id;
        public TrafficAgent? Agent;
        public Vehicle? Vehicle;
    }

    private readonly List<Obstacle> _obstacles = [];

    private static Obstacle ObstacleOf(Vehicle vehicle, TrafficAgent? agent) => new()
    {
        Position = vehicle.Ground,
        Forward = vehicle.Forward,
        Speed = agent?.Speed ?? MathF.Abs(vehicle.Speed),
        HalfLength = vehicle.Spec.Length * 0.5f,
        HalfWidth = vehicle.Spec.Radius * 0.8f,
        Id = vehicle.Id,
        Agent = agent,
        Vehicle = vehicle,
    };

    /// <summary>Half the room <paramref name="other"/> takes along and across a driver heading <paramref name="forward"/>.</summary>
    private static (float Along, float Across) Extent(in Obstacle other, Vector2 forward)
    {
        float cos = MathF.Abs(Vector2.Dot(other.Forward, forward));
        float sin = MathF.Sqrt(MathF.Max(0f, 1f - (cos * cos)));
        return ((cos * other.HalfLength) + (sin * other.HalfWidth), (sin * other.HalfLength) + (cos * other.HalfWidth));
    }

    /// <summary>Would a body of the given size at <paramref name="position"/> be inside <paramref name="other"/>?</summary>
    private static bool Touches(Vector2 position, Vector2 forward, float halfLength, float halfWidth, in Obstacle other)
    {
        // Two boxes overlap only if neither can see a gap along its own length or width.
        Vector2 offset = other.Position - position;
        (float along, float across) = Extent(other, forward);
        if (MathF.Abs(Vector2.Dot(offset, forward)) >= halfLength + along + 0.1f
            || MathF.Abs(Vector2.Dot(offset, MathUtil.Left(forward))) >= halfWidth + across)
        {
            return false;
        }

        float cos = MathF.Abs(Vector2.Dot(other.Forward, forward));
        float sin = MathF.Sqrt(MathF.Max(0f, 1f - (cos * cos)));
        return MathF.Abs(Vector2.Dot(offset, other.Forward)) < other.HalfLength + (cos * halfLength) + (sin * halfWidth) + 0.1f
            && MathF.Abs(Vector2.Dot(offset, MathUtil.Left(other.Forward))) < other.HalfWidth + (sin * halfLength) + (cos * halfWidth);
    }

    private void UpdateTraffic(float dt, Player player, Vector2 focus)
    {
        Vehicle? playerVehicle = player.Vehicle;
        float playerRadius = playerVehicle?.Spec.Radius ?? Player.Radius;
        for (int i = _agents.Count - 1; i >= 0; i--)
        {
            if (Vector2.DistanceSquared(_agents[i].Vehicle.Ground, focus) > DespawnDistance * DespawnDistance)
            {
                _traffic.Remove(_agents[i].Vehicle);
                _agents.RemoveAt(i);
            }
        }

        // Everything a driver must not drive into: the other traffic first (same order as the agents),
        // then whatever stands about, then the player.
        _obstacles.Clear();
        foreach (TrafficAgent agent in _agents)
        {
            _obstacles.Add(ObstacleOf(agent.Vehicle, agent));
        }

        if (Parked is { } parked)
        {
            foreach (Vehicle idle in parked)
            {
                if (idle != playerVehicle && Vector2.DistanceSquared(idle.Ground, focus) < DespawnDistance * DespawnDistance)
                {
                    _obstacles.Add(ObstacleOf(idle, null));
                }
            }
        }

        _obstacles.Add(playerVehicle is not null
            ? ObstacleOf(playerVehicle, null)
            : new Obstacle { Position = focus, Forward = MathUtil.Forward(player.Yaw), HalfLength = Player.Radius, HalfWidth = Player.Radius, Id = -1 });

        for (int i = _agents.Count - 1; i >= 0; i--)
        {
            TrafficAgent agent = _agents[i];
            Vehicle vehicle = agent.Vehicle;
            Vector2 position = vehicle.Ground;
            Vector2 forward = vehicle.Forward;
            Vector2 left = MathUtil.Left(forward);
            float halfLength = vehicle.Spec.Length * 0.5f;
            float halfWidth = vehicle.Spec.Radius * 0.8f;
            float braking = vehicle.Spec.Braking;
            float lookAhead = 7f + (agent.Speed * 0.9f);
            bool blockedByPlayer = IsAhead(position, forward, focus, lookAhead, 1.6f + playerRadius);

            // What the driver wants (target) and what the car in front allows right now (cap).
            float target = blockedByPlayer ? 0f : agent.Cruise;
            float cap = float.MaxValue;
            bool held = false;
            int lowestBlocker = int.MaxValue;

            // A red or yellow light: stop at the line, unless it is too late to do so decently.
            bool queued = false;
            int nodeAhead = agent.Direction > 0 ? agent.Edge.To : agent.Edge.From;
            float remaining = agent.Direction > 0 ? agent.Edge.Length - agent.Distance : agent.Distance;
            if (remaining > 11f && _map.SignalFor(nodeAhead, agent.Edge.Id) is { } signal && signal.ColourAt(SignalTime) != SignalColour.Green)
            {
                float room = remaining - 13f;
                if (agent.Speed * agent.Speed <= (2f * braking * MathF.Max(room, 0f)) + 1f)
                {
                    cap = MathF.Sqrt(braking * MathF.Max(0f, room));
                    queued = room < 25f;
                }
            }

            int slow = -1;
            float slowGap = float.MaxValue;
            float slowSide = 0f;
            for (int k = 0; k < _obstacles.Count - 1; k++)
            {
                if (k == i)
                {
                    continue;
                }

                Obstacle other = _obstacles[k];
                Vector2 offset = other.Position - position;
                if (offset.LengthSquared() > 80f * 80f)
                {
                    continue;
                }

                float along = Vector2.Dot(offset, forward);
                float side = Vector2.Dot(offset, left);
                float facing = Vector2.Dot(forward, other.Forward);
                (float otherAlong, float otherAcross) = Extent(other, forward);
                float reach = halfLength + otherAlong;
                float wide = halfWidth + otherAcross;
                bool inPath;
                float gap;
                bool sameLane = false;
                if (other.Agent is { } peer && peer.Edge == agent.Edge)
                {
                    // On the same stretch the road itself says who is in front, bends and all.
                    float ahead = (peer.Distance - agent.Distance) * agent.Direction;
                    float theirs = peer.Direction == agent.Direction ? LaneOffset(peer) : -LaneOffset(peer);
                    inPath = ahead > 0f && MathF.Abs(LaneOffset(agent) - theirs) < wide + 0.3f;
                    gap = ahead - reach;
                    sameLane = peer.Direction == agent.Direction;
                }
                else
                {
                    inPath = along > 0.5f && along < lookAhead + reach && MathF.Abs(side) < wide + (facing < -0.3f ? 0f : 0.35f);
                    gap = along - reach;
                }

                if (inPath)
                {
                    // Follow at the leader's pace with room to stop: v^2 = 2 a d, on half the brakes.
                    float lead = gap > 0.5f ? MathF.Max(0f, other.Speed * facing) : 0f;
                    float allowed = lead + MathF.Sqrt(braking * MathF.Max(0f, gap - 1.5f));
                    cap = MathF.Min(cap, allowed);
                    if (gap < 3f && allowed < 1f)
                    {
                        held = true;
                        lowestBlocker = Math.Min(lowestBlocker, other.Id);
                    }

                    queued |= gap < 8f && other.Agent is { Queued: true };

                    // Someone dawdling or standing in the lane: worth going round. Not a queue at the lights.
                    bool dawdling = other.Agent is { } leader
                        ? sameLane && leader.Pass == 0f && !leader.Queued && (leader.StopTimer > 0f || leader.Speed < agent.Cruise * 0.55f)
                        : other.Speed < 0.5f;
                    if (dawdling && gap < 14f && gap < slowGap && facing > 0.5f)
                    {
                        slow = k;
                        slowGap = gap;
                        slowSide = side;
                    }
                }
                else if (along > 0f && other.Agent is not null && MathF.Abs(facing) < 0.8f)
                {
                    // Crossing paths at a junction: whoever would be hit in the side goes first,
                    // and when both would, the older car does.
                    Vector2 closing = (other.Forward * other.Speed) - (forward * agent.Speed);
                    float closingSquared = closing.LengthSquared();
                    if (closingSquared > 0.25f)
                    {
                        float time = Math.Clamp(-Vector2.Dot(offset, closing) / closingSquared, 0f, 2.5f);
                        float miss = (offset + (closing * time)).Length();
                        bool theySeeMe = Vector2.Dot(-offset, other.Forward) > 0f;
                        if (time > 0f && miss < ((vehicle.Spec.Radius + other.Vehicle!.Spec.Radius) * 0.8f) + 0.3f
                            && (!theySeeMe || vehicle.Id > other.Id))
                        {
                            target = 0f;
                            held = true;
                            lowestBlocker = Math.Min(lowestBlocker, other.Id);
                        }
                    }
                }
            }

            bool atCrossing = false;
            if (Train is { } train)
            {
                foreach (LevelCrossing crossing in _map.Crossings)
                {
                    if (train.Closes(crossing) && IsAhead(position, forward, crossing.Point, MathF.Max(lookAhead, 16f) + 6f, 5f)
                        && Vector2.Distance(position, crossing.Point) > 7f)
                    {
                        target = 0f;
                        atCrossing = true;
                        break;
                    }
                }
            }

            // Angkots stop to wait for passengers ("ngetem") wherever they please.
            agent.NextStop -= dt;
            if (agent.NextStop <= 0f)
            {
                agent.NextStop = _rng.Range(35f, 90f);
                if (vehicle.Spec.Kind == VehicleKind.Angkot && agent.Edge.Kind != RoadKind.Toll && agent.Pass == 0f)
                {
                    agent.StopTimer = _rng.Range(4f, 9f);
                }
            }

            agent.StopTimer = MathF.Max(0f, agent.StopTimer - dt);
            if (agent.StopTimer > 0f)
            {
                target = 0f;
            }

            agent.Queued = queued || atCrossing;

            // Two drivers each waiting for the other: after a moment the older car edges forward.
            // It still cannot touch anyone (see below), it only stops being polite.
            if (held && agent.Waited > 3f && vehicle.Id < lowestBlocker && !blockedByPlayer && !agent.Queued && agent.StopTimer <= 0f)
            {
                target = 1.5f;
                cap = MathF.Max(cap, 1.5f);
            }

            float rate = target < agent.Speed ? braking : vehicle.Spec.Acceleration * 0.6f;
            agent.Speed += Math.Clamp(target - agent.Speed, -rate * dt, rate * dt);
            agent.Speed = MathF.Min(agent.Speed, cap);

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

            // Remember where the car was, so the step can be taken back if it would touch someone.
            (RoadEdge edge, float distance, int direction, float cruise, float pass, Vehicle? passing) =
                (agent.Edge, agent.Distance, agent.Direction, agent.Cruise, agent.Pass, agent.Passing);

            UpdatePassing(agent, i, agent.Queued ? -1 : slow, slowSide, halfWidth, position, forward, halfLength, dt);

            agent.Distance += agent.Speed * agent.Direction * dt;
            if (agent.Distance > agent.Edge.Length || agent.Distance < 0f)
            {
                NextEdge(agent);
                agent.Pass = 0f;
                agent.Passing = null;
            }

            (Vector2 lanePosition, float laneYaw) = LanePose(agent);
            // Ease onto the new lane after a junction instead of snapping, but only sideways:
            // easing the whole position leaves a fast car metres behind its place on the lane, and
            // on a bend "behind" is off the road.
            Vector2 heading = MathUtil.Forward(laneYaw);
            Vector2 error = position - lanePosition;
            error -= heading * Vector2.Dot(error, heading);
            Vector2 eased = lanePosition + (error * (1f - MathUtil.Damp(4f, dt)));
            float yaw = MathUtil.TurnToward(vehicle.Yaw, laneYaw, 2.6f * dt);

            // The one rule that is never bent: no step that puts this car inside another body,
            // unless it is already inside and the step takes it out.
            bool stopped = false;
            Vector2 yawForward = MathUtil.Forward(yaw);
            for (int k = 0; k < _obstacles.Count && !stopped; k++)
            {
                if (k == i)
                {
                    continue;
                }

                Obstacle other = _obstacles[k];
                stopped = Touches(eased, yawForward, halfLength, halfWidth, other)
                    && Vector2.DistanceSquared(eased, other.Position) < Vector2.DistanceSquared(position, other.Position) - 1e-5f;
            }

            if (stopped)
            {
                (agent.Edge, agent.Distance, agent.Direction, agent.Cruise, agent.Pass, agent.Passing) = (edge, distance, direction, cruise, pass, passing);
                agent.Speed = 0f;
                held = true;
                vehicle.MoveKinematic(_map, position, vehicle.Yaw, 0f, dt);
            }
            else
            {
                vehicle.MoveKinematic(_map, eased, yaw, agent.Speed, dt);
            }

            agent.Waited = held && agent.Speed < 0.3f && !blockedByPlayer && !agent.Queued && agent.StopTimer <= 0f ? agent.Waited + dt : 0f;
            _obstacles[i] = ObstacleOf(vehicle, agent);

            // A jam nobody can get out of (two becaks nose to nose in an alley) is cleared away
            // where the player is not looking, and at last even where they are.
            if (agent.Waited > 10f)
            {
                float away = Vector2.Distance(vehicle.Ground, focus);
                if (away > 45f || agent.Waited > 30f)
                {
                    _traffic.Remove(vehicle);
                    _agents.RemoveAt(i);
                    _obstacles.RemoveAt(i);
                }
            }
        }
    }

    /// <summary>Starts, keeps up and ends going round a vehicle that stands or dawdles in the lane.</summary>
    private void UpdatePassing(
        TrafficAgent agent, int index, int slow, float slowSide, float halfWidth, Vector2 position, Vector2 forward, float halfLength, float dt)
    {
        if (agent.Pass > 0f)
        {
            agent.PassTime += dt;
            bool done = agent.Passing is null;
            if (agent.Passing is { } passed)
            {
                Vector2 offset = passed.Ground - position;
                float behind = -Vector2.Dot(offset, forward);
                done = behind > halfLength + (passed.Spec.Length * 0.5f) + 1.5f || offset.LengthSquared() > 40f * 40f
                    || (!_traffic.Contains(passed) && passed.IsTraffic);
            }

            if (done)
            {
                agent.Pass = 0f;
                agent.Passing = null;
            }

            return;
        }

        if (slow < 0 || agent.Edge.Kind == RoadKind.Gang || agent.StopTimer > 0f)
        {
            return;
        }

        Obstacle blocker = _obstacles[slow];
        bool standing = blocker.Speed < 0.5f;
        float remaining = agent.Direction > 0 ? agent.Edge.Length - agent.Distance : agent.Distance;
        float needed = standing ? 30f : 70f;
        if (remaining < needed)
        {
            return;
        }

        // Far enough over to clear it, and still on the road.
        float amount = MathF.Max(0.5f, halfWidth + blocker.HalfWidth + 0.9f - slowSide);
        float room = (agent.Edge.Width * 0.25f) + (agent.Edge.Width * 0.5f) - halfWidth - 0.2f;
        if (amount > room)
        {
            return;
        }

        // Nobody coming the other way, nobody else already out there.
        for (int k = 0; k < _agents.Count; k++)
        {
            TrafficAgent other = _agents[k];
            if (k == index || other.Edge != agent.Edge)
            {
                continue;
            }

            float ahead = (other.Distance - agent.Distance) * agent.Direction;
            if ((other.Direction != agent.Direction && ahead > -6f && ahead < needed + 20f)
                || (other.Pass > 0f && MathF.Abs(ahead) < 30f))
            {
                return;
            }
        }

        agent.Pass = amount;
        agent.Passing = blocker.Vehicle;
        agent.PassTime = 0f;
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

            // Umbrellas go up one by one as the rain sets in, not all on the same frame.
            p.Umbrella = p.CarriesUmbrella && weather.Rain > 0.12f + ((p.Id % 7) * 0.03f);

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
                        p.Timer = Pedestrian.Lingers(p.Kind) ? _rng.Range(5f, 12f) : _rng.Range(12f, 40f);
                    }

                    break;

                default:
                    // People hurry when it pours, unless they are dry under an umbrella.
                    float pace = _rng.Range(1.15f, 1.5f) * (1f + ((p.Umbrella ? 0.15f : 1f) * weather.Rain));
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
                        p.Timer = Pedestrian.Lingers(p.Kind) ? _rng.Range(12f, 30f) : _rng.Range(2f, 7f);
                    }

                    break;
            }
        }
    }

    /// <summary>
    /// A pigeon: pecks about on the ground in little hops, and takes to the air when someone
    /// comes close, a car passes, or just because. It comes down somewhere else on its patch.
    /// </summary>
    private void UpdateBird(Animal bird, AnimalZone zone, Vector2 position, float toPlayer, Vector2 focus, bool playerFast, float dt)
    {
        if (bird.Airborne)
        {
            Vector2 toGoal = bird.Target - position;
            float left = toGoal.Length();
            bird.Speed += (8.5f - bird.Speed) * MathUtil.Damp(3f, dt);
            Vector2 next = position;
            if (left > 0.01f)
            {
                bird.Yaw = MathUtil.TurnToward(bird.Yaw, MathUtil.YawOf(toGoal), 6f * dt);
                next = position + (toGoal / left * MathF.Min(left, bird.Speed * dt));
            }

            // Climb out, cruise above the roofs of the kampung, glide down over the last stretch.
            float wanted = MathF.Min(7f + (bird.Id % 5), left * 0.9f);
            bird.Altitude += Math.Clamp(wanted - bird.Altitude, -5f * dt, 4f * dt);
            if (left < 0.3f && bird.Altitude < 0.3f)
            {
                bird.Airborne = false;
                bird.Fleeing = false;
                bird.Altitude = 0f;
                bird.Speed = 0f;
                bird.Timer = _rng.Range(2f, 6f);
            }

            bird.Position = next.ToWorld(_map.Terrain.HeightAt(next) + bird.Altitude);
            return;
        }

        bool startled = toPlayer < (playerFast ? 11f : 5.5f);
        for (int i = 0; i < _traffic.Count && !startled; i++)
        {
            startled = MathF.Abs(_traffic[i].Speed) > 1f && Vector2.DistanceSquared(_traffic[i].Ground, position) < 20f;
        }

        bird.Timer -= dt;
        if (startled || (bird.Timer <= 0f && _rng.Chance(0.12f)))
        {
            Vector2 away = toPlayer > 0.01f ? (position - focus) / toPlayer : Vector2.UnitX;
            for (int attempt = 0; attempt < 8; attempt++)
            {
                Vector2 goal = bird.Zone >= 0
                    ? RandomInZone(zone)
                    : position + (MathUtil.Forward(MathUtil.YawOf(away) + _rng.Range(-0.9f, 0.9f)) * _rng.Range(14f, 26f));
                if (Vector2.Distance(goal, position) > 8f && (!startled || Vector2.Distance(goal, focus) > 12f)
                    && !_map.IsInsideBuilding(goal, 0.6f) && !_map.Roads.IsOnRoad(goal, 0.5f) && _map.Terrain.WaterLevelAt(goal) is null)
                {
                    bird.Target = goal;
                    bird.Airborne = true;
                    bird.Fleeing = startled;
                    return;
                }
            }

            bird.Timer = 1f;
        }
        else if (bird.Timer <= 0f)
        {
            bird.Timer = _rng.Range(1.5f, 5f);
            bird.Target = position + new Vector2(_rng.Range(-1.5f, 1.5f), _rng.Range(-1.5f, 1.5f));
        }

        Vector2 hop = bird.Target - position;
        float reach = hop.Length();
        bird.Speed += ((reach > 0.25f ? 0.7f : 0f) - bird.Speed) * MathUtil.Damp(6f, dt);
        if (reach > 0.25f)
        {
            bird.Yaw = MathUtil.TurnToward(bird.Yaw, MathUtil.YawOf(hop), 6f * dt);
            Vector2 next = position + (MathUtil.Forward(bird.Yaw) * bird.Speed * dt);
            if (!_map.IsInsideBuilding(next, 0.2f) && _map.Terrain.WaterLevelAt(next) is null)
            {
                bird.Position = next.ToWorld(_map.Terrain.HeightAt(next));
            }
            else
            {
                bird.Target = position;
            }
        }
    }

    /// <summary>
    /// The nearest walker within <paramref name="radius"/> who is free to chat. They stop and
    /// turn to face <paramref name="toward"/> for a few seconds.
    /// </summary>
    public Pedestrian? Greet(Vector2 toward, float radius)
    {
        Pedestrian? nearest = null;
        float best = radius * radius;
        foreach (Pedestrian p in _pedestrians)
        {
            float d2 = Vector2.DistanceSquared(p.Position.Xz(), toward);
            if (d2 < best && p.State != PedestrianState.Dodge)
            {
                best = d2;
                nearest = p;
            }
        }

        if (nearest is not null)
        {
            nearest.State = PedestrianState.Idle;
            nearest.Timer = 3.5f;
            nearest.Speed = 0f;
            Vector2 facing = toward - nearest.Position.Xz();
            if (facing.LengthSquared() > 0.01f)
            {
                nearest.Yaw = MathUtil.YawOf(facing);
            }
        }

        return nearest;
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

    /// <summary>
    /// A water animal: cruises from one spot of its water to another, rests in between, and
    /// swims away from anyone who comes close. It never leaves water deep enough for it.
    /// </summary>
    private void UpdateSwimmer(Animal animal, AnimalZone zone, Vector2 position, float toPlayer, Vector2 focus, bool playerFast, float dt)
    {
        (float cruise, float dash, float shy, float rest) = animal.Kind switch
        {
            AnimalKind.Ikan => (0.55f, 2.6f, 4f, 0.15f),
            AnimalKind.KuraKura => (0.22f, 0.7f, 3f, 0.5f),
            AnimalKind.Buaya => (0.25f, 1.3f, 8f, 0.75f),
            _ => (0.5f, 1.9f, 5f, 0.35f),
        };

        animal.Fleeing = toPlayer < shy * (playerFast ? 1.6f : 1f);
        animal.Timer -= dt;
        if (animal.Fleeing)
        {
            Vector2 away = toPlayer > 0.01f ? (position - focus) / toPlayer : Vector2.UnitX;
            // Straight away if there is water that way, otherwise along the bank.
            foreach (float turn in (ReadOnlySpan<float>)[0f, 0.7f, -0.7f, 1.4f, -1.4f])
            {
                Vector2 goal = position + (MathUtil.Forward(MathUtil.YawOf(away) + turn) * 6f);
                if (Swimmable(goal, animal.Kind) is not null)
                {
                    animal.Target = goal;
                    break;
                }
            }

            animal.Timer = 1.5f;
        }
        else if (animal.Timer <= 0f)
        {
            animal.Timer = _rng.Range(3f, 9f);
            animal.Target = position;
            if (!_rng.Chance(rest))
            {
                for (int attempt = 0; attempt < 8; attempt++)
                {
                    Vector2 goal = Vector2.Distance(position, zone.Center) > zone.Radius
                        ? zone.Center + new Vector2(_rng.Range(-6f, 6f), _rng.Range(-6f, 6f))
                        : position + new Vector2(_rng.Range(-14f, 14f), _rng.Range(-14f, 14f));
                    if (Swimmable(goal, animal.Kind) is not null)
                    {
                        animal.Target = goal;
                        break;
                    }
                }
            }
        }

        Vector2 toTarget = animal.Target - position;
        float distance = toTarget.Length();
        float speed = distance < 0.4f ? 0f : animal.Fleeing ? dash : cruise;
        animal.Speed += (speed - animal.Speed) * MathUtil.Damp(4f, dt);
        if (distance < 0.4f)
        {
            return;
        }

        animal.Yaw = MathUtil.TurnToward(animal.Yaw, MathUtil.YawOf(toTarget), (animal.Kind == AnimalKind.Ikan ? 5f : 2.2f) * dt);
        Vector2 next = position + (MathUtil.Forward(animal.Yaw) * animal.Speed * dt);
        if (Swimmable(next, animal.Kind) is { } level)
        {
            animal.Position = next.ToWorld(level - Draught(animal.Kind));
        }
        else
        {
            // The bank: stop here and think of somewhere else.
            animal.Target = position;
            animal.Timer = MathF.Min(animal.Timer, 0.6f);
        }
    }

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
            if (animal.Kind == AnimalKind.Burung)
            {
                UpdateBird(animal, zone, position, toPlayer, focus, playerFast, dt);
                continue;
            }

            if (Animal.IsAquatic(animal.Kind))
            {
                UpdateSwimmer(animal, zone, position, toPlayer, focus, playerFast, dt);
                continue;
            }

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
