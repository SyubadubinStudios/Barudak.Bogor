using System.Numerics;
using BarudakBogor.Activities;
using BarudakBogor.Entities;
using BarudakBogor.Localization;
using BarudakBogor.Missions;
using BarudakBogor.Numerics;
using BarudakBogor.Sim;
using BarudakBogor.World;

namespace BarudakBogor.Gameplay;

/// <summary>
/// The whole game simulation, with no rendering and no input devices: feed it an
/// <see cref="InputState"/> per frame and read back the state. The window is only a view of
/// this class, which is what lets the game be tested headlessly.
/// </summary>
public sealed class GameSession
{
    private readonly List<GameEvent> _events = [];
    private readonly Queue<Dialog> _dialogs = new();
    private readonly List<Vehicle> _vehicles = [];
    private Rng _rng;
    private int _nextVehicleId = 1;
    private static readonly L[] PhaseLines =
    [
        new("Pagi di Bogor. Tukang bubur sudah keliling.", "Morning in Bogor. The porridge sellers are already out."),
        new("Siang. Panasnya sebentar, awannya sudah kumpul.", "Midday. The heat is brief; the clouds are already gathering."),
        new("Sore. Jam-jamnya hujan turun.", "Afternoon. The hour when the rain comes."),
        new("Malam. Lampu-lampu kota menyala.", "Night. The city lights come on."),
    ];

    private DayPhase _phase;
    private float _photoCooldown;
    private float _crashCooldown;
    private float _dodgeCooldown;

    public GameSession(WorldMap map, CharacterId character = CharacterId.Ujang, uint seed = 1)
    {
        Map = map;
        _rng = new Rng(seed * 2_654_435_761u);
        Weather = new WeatherSystem(seed + 11u);
        Population = new Population(map, seed + 23u);
        Player.Profile = CharacterProfile.Of(character);
        Missions = new MissionManager(this);

        Poi home = map[PoiId.TuguKujang];
        Player.Teleport(map, home.RoadPoint + ((home.Position - home.RoadPoint) * 0.45f), home.Yaw);

        Weather.LightningStruck += p => Emit(new GameEvent(GameEventKind.Lightning, L.Empty, p.ToWorld(map.Terrain.HeightAt(p))));
        Weather.Thunder += volume => Emit(new GameEvent(GameEventKind.Thunder, L.Empty, default, volume));
        Weather.BlackoutStarted += cut =>
        {
            Emit(new GameEvent(GameEventKind.Blackout, L.Empty, cut.Center.ToWorld(), cut.Radius));
            if (Vector2.Distance(cut.Center, Player.Ground) < cut.Radius)
            {
                Toast(new("Yah, mati lampu deui...", "Great, the power is out again..."));
            }
        };
        Population.PedestrianDodged += pedestrian =>
        {
            Emit(new GameEvent(GameEventKind.Dodge, L.Empty, pedestrian.Position));
            if (_dodgeCooldown <= 0f)
            {
                _dodgeCooldown = 6f;
                Toast(DodgeLines[_rng.Range(0, DodgeLines.Length)]);
                Progress.AddReputation(Career.Warga, -1);
            }
        };
        Population.Honked += vehicle => Emit(new GameEvent(GameEventKind.Horn, L.Empty, vehicle.Position));
        Progress.MoneyChanged += (_, delta) => Emit(new GameEvent(GameEventKind.Money, L.Empty, default, delta));
        Progress.EndingReached += career => Emit(new GameEvent(
            GameEventKind.Ending,
            new L("Gelar baru: {0}!", "New title: {0}!").With(Progress.CareerTitle(career).Text),
            default,
            (int)career));

        _phase = Clock.Phase;
        SpawnParkedVehicles();
        CreateWorldInteractables();
    }

    private static readonly L[] DodgeLines =
    [
        new("\"WOY! Mata dipake atuh!\"", "\"OI! Use your eyes!\""),
        new("\"Astagfirullah, nyetirna kumaha éta!\"", "\"Good grief, who taught you to drive!\""),
        new("\"Ieu jalan lain sirkuit Sentul!\"", "\"This isn't Sentul Circuit!\""),
        new("\"Sendal aing leupas!\"", "\"I lost my sandal!\""),
    ];

    public WorldMap Map { get; }

    public GameClock Clock { get; } = new();

    public WeatherSystem Weather { get; }

    public Player Player { get; } = new();

    public Population Population { get; }

    public Progress Progress { get; } = new();

    public MissionManager Missions { get; }

    public OjolService Ojol { get; } = new();

    public TouringRace Touring { get; } = new();

    public BandrekBusiness Business { get; } = new();

    /// <summary>Vehicles that exist for the player: parked ones, mission ones, the one being driven.</summary>
    public IReadOnlyList<Vehicle> Vehicles => _vehicles;

    /// <summary>Beacons for this frame; rebuilt on every update.</summary>
    public List<Marker> Markers { get; } = [];

    public List<Interactable> Interactables { get; } = [];

    /// <summary>The interaction the player would trigger now, with its prompt; null when none.</summary>
    public Interactable? Focus { get; private set; }

    /// <summary>Prompt for entering or leaving a vehicle, when that is what Interact would do.</summary>
    public L? VehiclePrompt { get; private set; }

    public Dialog? Dialog { get; private set; }

    public Activity? Activity { get; private set; }

    /// <summary>When true the world is frozen (pause menu, map screen).</summary>
    public bool Paused { get; set; }

    /// <summary>Total simulated seconds.</summary>
    public double Time { get; private set; }

    /// <summary>The district the player is in, for the HUD.</summary>
    public L LocationName
    {
        get
        {
            Vector2 p = Player.Ground;
            L district = Loc.Name(Map.DistrictAt(p)?.NameKey ?? "district.rural");
            return Map.Roads.Nearest(p, 30f) is { } road
                ? new L($"{Loc.Name(road.Edge.NameKey).Id}, {district.Id}", $"{Loc.Name(road.Edge.NameKey).En}, {district.En}")
                : district;
        }
    }

    // ------------------------------------------------------------------ services for gameplay code

    public void Emit(in GameEvent gameEvent) => _events.Add(gameEvent);

    public void Toast(L text) => _events.Add(new GameEvent(GameEventKind.Toast, text));

    /// <summary>Hands the frame's events to the caller and clears the queue.</summary>
    public void DrainEvents(List<GameEvent> into)
    {
        into.AddRange(_events);
        _events.Clear();
    }

    public void Say(L speaker, L text) => ShowDialog(new Dialog(speaker, text));

    public void ShowDialog(Dialog dialog)
    {
        if (Dialog is null)
        {
            Dialog = dialog;
        }
        else
        {
            _dialogs.Enqueue(dialog);
        }
    }

    public void StartActivity(Activity activity) => Activity = activity;

    /// <summary>Pays the player (with the character's study bonus on mission rewards) and adds reputation.</summary>
    public void Reward(long amount, Career career, int reputation)
    {
        Progress.Earn(amount);
        if (reputation != 0)
        {
            Progress.AddReputation(career, reputation);
        }
    }

    public float Range(float min, float max) => _rng.Range(min, max);

    public int Random(int min, int maxExclusive) => _rng.Range(min, maxExclusive);

    public bool Chance(float probability) => _rng.Chance(probability);

    /// <summary>A point on a road between <paramref name="min"/> and <paramref name="max"/> metres from a centre.</summary>
    public Vector2? RandomRoadPoint(Vector2 center, float min, float max)
    {
        for (int attempt = 0; attempt < 12; attempt++)
        {
            float angle = _rng.Range(0f, MathUtil.Tau);
            Vector2 p = center + (new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * _rng.Range(min, max));
            if (Map.Roads.Nearest(p, 150f, e => e.Kind != RoadKind.Toll) is { } hit && Vector2.Distance(hit.Point, center) >= min * 0.8f)
            {
                return hit.Point;
            }
        }

        return null;
    }

    public Vehicle SpawnVehicle(VehicleKind kind, Vector2 position, float yaw)
    {
        Vehicle vehicle = new(_nextVehicleId++, kind);
        vehicle.Place(Map, position, yaw);
        _vehicles.Add(vehicle);
        return vehicle;
    }

    /// <summary>Parks a vehicle at the kerb next to a place, facing along the road.</summary>
    public Vehicle SpawnVehicleAt(VehicleKind kind, PoiId place, float along = 0f)
    {
        Poi poi = Map[place];
        RoadHit? hit = Map.Roads.Nearest(poi.RoadPoint, 20f);
        Vector2 direction = hit?.Direction ?? Vector2.UnitY;
        float halfWidth = (hit?.Edge.Width ?? 7f) * 0.5f;
        Vector2 toPlace = poi.Position - poi.RoadPoint;
        Vector2 side = toPlace.LengthSquared() > 0.01f ? Vector2.Normalize(toPlace) : MathUtil.Left(direction);
        Vector2 position = poi.RoadPoint + (side * MathF.Max(0.6f, halfWidth - 1.2f)) + (direction * along);
        return SpawnVehicle(kind, position, MathUtil.YawOf(direction));
    }

    public void RemoveVehicle(Vehicle vehicle)
    {
        if (Player.Vehicle == vehicle)
        {
            ExitVehicle(force: true);
        }

        _vehicles.Remove(vehicle);
    }

    public void EnterVehicle(Vehicle vehicle)
    {
        Player.Vehicle = vehicle;
        vehicle.Occupied = true;
        vehicle.LightsOn = Clock.IsNight || Weather.Rain > 0.5f;
        Emit(new GameEvent(GameEventKind.EnterVehicle, vehicle.Spec.Name, vehicle.Position));
    }

    public void ExitVehicle(bool force = false)
    {
        if (Player.Vehicle is not { } vehicle || (!force && MathF.Abs(vehicle.Speed) > 2.5f))
        {
            return;
        }

        vehicle.Occupied = false;
        vehicle.Horn = false;
        Player.Vehicle = null;
        // Step out on the left (kerb side), or stay put behind a cart.
        Vector2 exit = vehicle.Spec.IsCart
            ? vehicle.Ground - (vehicle.Forward * 1.3f)
            : vehicle.Ground + (MathUtil.Left(vehicle.Forward) * (vehicle.Spec.Radius + 0.9f));
        exit = Map.ResolveCircle(exit, Player.Radius, out _);
        Player.Teleport(Map, exit, vehicle.Yaw);
        Emit(new GameEvent(GameEventKind.ExitVehicle, L.Empty, vehicle.Position));
    }

    /// <summary>Pays an ojek to take the player (and nothing else) to a place.</summary>
    public bool FastTravel(PoiId place)
    {
        Poi poi = Map[place];
        long fare = FastTravelFare(place);
        if (Missions.Active is not null)
        {
            Toast(new("Selesaikan dulu misinya, baru naik ojek.", "Finish the mission first, then take the ojek."));
            return false;
        }

        if (!Progress.TrySpend(fare))
        {
            Toast(new("Ongkos ojekna kurang.", "Not enough for the ojek fare."));
            return false;
        }

        // The ride takes time: roughly 40 km/h.
        float hours = Vector2.Distance(Player.Ground, poi.Position) / 11f / 3_600f;
        ExitVehicle(force: true);
        Player.Teleport(Map, poi.RoadPoint + ((poi.Position - poi.RoadPoint) * 0.5f), poi.Yaw);
        Population.Clear();
        Clock.Set(Clock.Day, Clock.Hours + MathF.Max(0.1f, hours));
        Toast(new L("Naik ojek ke {0} ({1}).", "Took an ojek to {0} ({1}).").With(Loc.Name(poi.NameKey).Text, Loc.Rupiah(fare)));
        return true;
    }

    public long FastTravelFare(PoiId place) =>
        (long)(5_000 + (Vector2.Distance(Player.Ground, Map[place].Position) * 2.5f)) / 500 * 500;

    /// <summary>Takes a photo from the camera; returns null while the shutter is cooling down.</summary>
    public PhotoResult? TakePhoto(Vector3 cameraPosition, Vector3 cameraForward)
    {
        if (_photoCooldown > 0f)
        {
            return null;
        }

        _photoCooldown = 3f;
        return Photography.Shoot(this, cameraPosition, cameraForward);
    }

    internal void OnFishCaught(FishSpecies fish, float weightKg)
    {
        Progress.FishCaught++;
        Progress.BiggestFishKg = MathF.Max(Progress.BiggestFishKg, weightKg);
        long value = (long)(fish.PricePerKg * weightKg) / 500 * 500;
        Reward(value, Career.Mancing, fish.PricePerKg == 0 ? 0 : 2 + (int)weightKg);
        Emit(new GameEvent(GameEventKind.FishCaught, fish.Name, Player.Position, weightKg));
        Toast(fish.PricePerKg == 0
            ? new L("Dapet... {0}. Lumayan buat ke warung.", "You caught... {0}. Well, it's something.").With(fish.Name.Text)
            : new L("{0} {1:0.0} kg! Laku {2}", "{0} {1:0.0} kg! Sold for {2}").With(fish.Name.Text, weightKg, Loc.Rupiah(value)));
    }

    /// <summary>Call after setting the clock by hand so the change is not announced as a new part of the day.</summary>
    public void SyncPhase() => _phase = Clock.Phase;

    // ------------------------------------------------------------------ update

    public void Update(float dt, in InputState input)
    {
        if (Paused || dt <= 0f)
        {
            return;
        }

        dt = MathF.Min(dt, 0.05f);
        Time += dt;
        _photoCooldown = MathF.Max(0f, _photoCooldown - dt);
        _crashCooldown = MathF.Max(0f, _crashCooldown - dt);
        _dodgeCooldown = MathF.Max(0f, _dodgeCooldown - dt);
        Markers.Clear();

        Clock.Update(dt);
        Weather.Update(dt, Clock, Player.Ground);
        if (Clock.Phase != _phase)
        {
            _phase = Clock.Phase;
            Toast(PhaseLines[(int)_phase]);
        }


        bool modal = Dialog is not null || Activity is not null;
        if (Dialog is { } dialog)
        {
            UpdateDialog(dialog, input);
        }
        else if (Activity is { } activity)
        {
            activity.Update(this, dt, input);
            if (activity.Finished)
            {
                Activity = null;
            }
        }

        UpdatePlayer(dt, modal ? default : input);
        UpdateIdleVehicles(dt);
        Population.Update(dt, Player, Clock, Weather);
        ResolveTrafficContacts();

        Missions.Update(dt);
        Ojol.Update(this, dt);
        Touring.Update(this, dt);
        Business.Update(this, dt);

        UpdateFocus();
        if (!modal && input.Interact)
        {
            Interact();
        }
    }

    private void UpdateDialog(Dialog dialog, in InputState input)
    {
        int choice = -1;
        if (dialog.Choices.Count > 0)
        {
            for (int i = 0; i < dialog.Choices.Count; i++)
            {
                if ((input.Lanes & (1 << i)) != 0)
                {
                    choice = i;
                }
            }

            if (choice < 0)
            {
                return;
            }
        }
        else if (!input.Action && !input.Interact)
        {
            return;
        }

        Dialog = _dialogs.Count > 0 ? _dialogs.Dequeue() : null;
        dialog.OnChoice?.Invoke(choice);
    }

    private void UpdatePlayer(float dt, in InputState input)
    {
        if (Player.Vehicle is { } vehicle)
        {
            DriveInput drive = new(MathF.Max(0f, input.Move.Y), MathF.Max(0f, -input.Move.Y), input.Move.X, input.Handbrake);
            float bonus = 1f + (Player.Profile.DrivingBonus * 0.4f);
            Vector2 before = vehicle.Ground;
            vehicle.Update(dt, drive, Map, Weather.Wetness / bonus);
            vehicle.Horn = input.Horn;
            vehicle.LightsOn = Clock.IsNight || Weather.Rain > 0.5f;
            Progress.DistanceDriven += Vector2.Distance(before, vehicle.Ground);
            if (vehicle.Impact > 5f && _crashCooldown <= 0f)
            {
                _crashCooldown = 0.6f;
                Emit(new GameEvent(GameEventKind.Crash, L.Empty, vehicle.Position, vehicle.Impact));
            }

            Player.Update(dt, default, Map);
            return;
        }

        // On foot "forward" means away from the camera.
        Vector2 forward = MathUtil.Forward(input.CameraYaw);
        Vector2 direction = (forward * input.Move.Y) + (MathUtil.Left(forward) * input.Move.X);
        Player.Update(dt, new WalkInput(direction, input.Run, input.Jump), Map);
    }

    private void UpdateIdleVehicles(float dt)
    {
        foreach (Vehicle vehicle in _vehicles)
        {
            // Let an abandoned vehicle roll to a stop; fully parked ones cost nothing.
            if (vehicle != Player.Vehicle && !vehicle.Occupied && vehicle.Velocity.LengthSquared() > 0.01f)
            {
                vehicle.Update(dt, new DriveInput(0f, 0f, 0f, true), Map, Weather.Wetness);
            }
        }
    }

    private void ResolveTrafficContacts()
    {
        Vector2 position = Player.Ground;
        float radius = Player.Vehicle?.Spec.Radius ?? Player.Radius;
        foreach (Vehicle other in Population.Traffic)
        {
            PushApart(other, ref position, radius);
        }

        foreach (Vehicle other in _vehicles)
        {
            if (other != Player.Vehicle)
            {
                PushApart(other, ref position, radius);
            }
        }

        if (position != Player.Ground)
        {
            if (Player.Vehicle is { } vehicle)
            {
                vehicle.Position = position.ToWorld(Map.Terrain.HeightAt(position));
            }
            else
            {
                Player.Position = position.ToWorld(Player.Position.Y);
            }
        }
    }

    private void PushApart(Vehicle other, ref Vector2 position, float radius)
    {
        Vector2 offset = position - other.Ground;
        float reach = radius + other.Spec.Radius;
        float d2 = offset.LengthSquared();
        if (d2 >= reach * reach || d2 < 1e-6f)
        {
            return;
        }

        float distance = MathF.Sqrt(d2);
        Vector2 normal = offset / distance;
        position += normal * (reach - distance);
        if (Player.Vehicle is { } vehicle)
        {
            float into = Vector2.Dot(vehicle.Velocity, normal);
            if (into < 0f)
            {
                if (-into > 5f && _crashCooldown <= 0f)
                {
                    _crashCooldown = 0.6f;
                    Emit(new GameEvent(GameEventKind.Crash, L.Empty, vehicle.Position, -into));
                }

                vehicle.Velocity -= normal * into * 1.2f;
            }
        }
    }

    private void UpdateFocus()
    {
        Focus = null;
        VehiclePrompt = null;
        Vector2 position = Player.Ground;
        float best = float.MaxValue;
        foreach (Interactable interactable in Interactables)
        {
            if (interactable.Marker is { } marker && (interactable.Available?.Invoke() ?? true))
            {
                Markers.Add(marker with { Position = interactable.Position });
            }

            float distance = Vector2.Distance(position, interactable.Position);
            if (distance < interactable.Radius && distance < best && (interactable.Available?.Invoke() ?? true))
            {
                best = distance;
                Focus = interactable;
            }
        }

        if (Player.Vehicle is { } vehicle)
        {
            if (Focus is null && MathF.Abs(vehicle.Speed) < 2.5f)
            {
                VehiclePrompt = vehicle.Spec.IsCart ? new L("Lepas gerobak", "Let go of the cart") : new L("Turun", "Get out");
            }
        }
        else if (Focus is null && NearestFreeVehicle(position) is { } free)
        {
            VehiclePrompt = (free.Spec.IsCart ? new L("Dorong {0}", "Push the {0}") : new L("Naik {0}", "Get in the {0}")).With(free.Spec.Name.Text);
        }
    }

    private Vehicle? NearestFreeVehicle(Vector2 position)
    {
        Vehicle? nearest = null;
        float best = float.MaxValue;
        foreach (Vehicle vehicle in _vehicles)
        {
            float reach = vehicle.Spec.Radius + 1.6f;
            float d2 = Vector2.DistanceSquared(position, vehicle.Ground);
            if (!vehicle.Occupied && d2 < reach * reach && d2 < best)
            {
                best = d2;
                nearest = vehicle;
            }
        }

        return nearest;
    }

    private void Interact()
    {
        if (Focus is { } focus)
        {
            focus.Action();
            return;
        }

        if (Player.Vehicle is not null)
        {
            ExitVehicle();
        }
        else if (NearestFreeVehicle(Player.Ground) is { } vehicle)
        {
            EnterVehicle(vehicle);
        }
    }

    // ------------------------------------------------------------------ world set-up

    private void SpawnParkedVehicles()
    {
        SpawnVehicleAt(VehicleKind.Angkot, PoiId.TuguKujang, 6f);
        SpawnVehicleAt(VehicleKind.Motor, PoiId.TuguKujang, -6f);
        SpawnVehicleAt(VehicleKind.Motor, PoiId.RumahUjang);
        SpawnVehicleAt(VehicleKind.GerobakBandrek, PoiId.WarungBandrek, 5f);
        SpawnVehicleAt(VehicleKind.Delman, PoiId.GerbangKebunRaya, 12f);
        SpawnVehicleAt(VehicleKind.MobilKeluarga, PoiId.Karaoke, 6f);
        SpawnVehicleAt(VehicleKind.Bajaj, PoiId.PasarAnyar, 8f);
        SpawnVehicleAt(VehicleKind.TahuBulat, PoiId.LapanganStm, 8f);
        SpawnVehicleAt(VehicleKind.Bakso, PoiId.SekolahBinaInsani, 9f);
        SpawnVehicleAt(VehicleKind.Motor, PoiId.Empang, 5f);
        SpawnVehicleAt(VehicleKind.Angkot, PoiId.TerminalDramaga, 8f);
        SpawnVehicleAt(VehicleKind.Motor, PoiId.KampusIpb, 6f);
        SpawnVehicleAt(VehicleKind.Motor, PoiId.SituGede, 5f);
        SpawnVehicleAt(VehicleKind.Angkot, PoiId.JalanBaru, 8f);
        SpawnVehicleAt(VehicleKind.MobilKeluarga, PoiId.SirkuitSentul, 8f);
        SpawnVehicleAt(VehicleKind.Motor, PoiId.Ciawi, 6f);
        SpawnVehicleAt(VehicleKind.Motor, PoiId.PuncakPass, 6f);
        SpawnVehicleAt(VehicleKind.MobilKeluarga, PoiId.VillaMisterius, 10f);
        SpawnVehicleAt(VehicleKind.Becak, PoiId.PasarAnyar, -8f);
        SpawnVehicleAt(VehicleKind.Becak, PoiId.GangSelot, 6f);
        SpawnVehicleAt(VehicleKind.Bemo, PoiId.Empang, -9f);
        SpawnVehicleAt(VehicleKind.Sedan, PoiId.TuguKujang, -16f);
        SpawnVehicleAt(VehicleKind.Truk, PoiId.Ciawi, -10f);
    }

    private void CreateWorldInteractables()
    {
        bool OnFoot() => Player.Vehicle is null;
        bool Free() => Missions.Active is null;

        void FishingSpot(PoiId place)
        {
            Poi poi = Map[place];
            Interactables.Add(new Interactable(poi.Position, 5f, new("Mancing", "Go fishing"), () =>
                StartActivity(new FishingGame(_rng.NextUInt(), Player.Profile.FishingBonus, Weather.Rain)))
            {
                Available = () => OnFoot() && Free(),
                Marker = new Marker(MarkerKind.Activity, poi.Position, new("Mancing", "Fishing")),
            });
        }

        FishingSpot(PoiId.Empang);
        FishingSpot(PoiId.SituGede);

        Poi court = Map[PoiId.LapanganBadminton];
        Interactables.Add(new Interactable(court.Position, 6f, new("Main badminton (taruhan Rp 10.000)", "Play badminton (Rp 10,000 stake)"), () =>
        {
            if (!Progress.TrySpend(10_000))
            {
                Toast(new("Teu boga duit keur taruhan.", "No money for the stake."));
                return;
            }

            StartActivity(RhythmGame.Badminton(_rng.NextUInt(), (session, game) =>
            {
                if (game.Won)
                {
                    session.Reward(30_000, Career.Warga, 4);
                    session.Toast(new("Menang! Juara RT 05. +Rp 30.000", "You win! Champion of the block. +Rp 30,000"));
                }
                else
                {
                    session.Toast(new("Kalah ku bapak-bapak sarungan. Latihan deui!", "Beaten by a dad in a sarong. Keep practising!"));
                }
            }));
        })
        {
            Available = () => OnFoot() && Free(),
            Marker = new Marker(MarkerKind.Activity, court.Position, new("Badminton Kampung", "Kampung Badminton")),
        });

        Poi karaoke = Map[PoiId.Karaoke];
        Interactables.Add(new Interactable(karaoke.Position, 6f, new("Karaoke (Rp 15.000)", "Sing karaoke (Rp 15,000)"), () =>
        {
            if (!Progress.TrySpend(15_000))
            {
                Toast(new("Duitna kurang keur nyanyi.", "Not enough money to sing."));
                return;
            }

            StartActivity(RhythmGame.Karaoke(_rng.NextUInt(), (session, game) =>
            {
                int percent = (int)(game.Accuracy * 100f);
                long tips = percent >= 60 ? percent * 600L : 0L;
                session.Reward(tips, Career.Kuliner, percent / 20);
                session.Toast(percent >= 85
                    ? new L("Skor {0}! Suara emas, sawer {1}!", "Score {0}! Golden voice, tips {1}!").With(percent, Loc.Rupiah(tips))
                    : percent >= 60
                        ? new L("Skor {0}. Lumayan, disawer {1}.", "Score {0}. Not bad, tipped {1}.").With(percent, Loc.Rupiah(tips))
                        : new L("Skor {0}. Tetangga minta volume dikecilin.", "Score {0}. The neighbours asked you to turn it down.").With(percent));
            }));
        })
        {
            Available = () => OnFoot() && Free(),
            Marker = new Marker(MarkerKind.Activity, karaoke.Position, new("Karaoke", "Karaoke")),
        });

        Poi stall = Map[PoiId.WarungBandrek];
        Interactables.Add(new Interactable(stall.Position, 5f, new("Urus usaha bandrek", "Manage the bandrek business"), OpenBusinessMenu)
        {
            Available = OnFoot,
            Marker = new Marker(MarkerKind.Shop, stall.Position, new("Usaha Bandrek", "Bandrek Business")),
        });

        Poi ciawi = Map[PoiId.Ciawi];
        Interactables.Add(new Interactable(ciawi.RoadPoint, 9f, new("Mulai Touring Puncak", "Start the Puncak touring run"), () => Touring.Start(this))
        {
            Available = () => !Touring.Active && Free() && Player.Vehicle is not null,
            Marker = new Marker(MarkerKind.Activity, ciawi.RoadPoint, new("Touring Puncak", "Puncak Touring")),
        });

        Poi home = Map[PoiId.RumahUjang];
        Interactables.Add(new Interactable(home.Position, 4f, new("Istirahat sampai pagi", "Rest until morning"), () =>
        {
            Clock.Set(Clock.Day + (Clock.Hours > 6f ? 1 : 0), 6f);
            Weather.Set(WeatherKind.Berawan, 120f);
            Toast(new("Wilujeng enjing! Bogor nunggu.", "Good morning! Bogor is waiting."));
        })
        {
            Available = () => OnFoot() && Free(),
            Marker = new Marker(MarkerKind.Home, home.Position, new("Kontrakan", "Home")),
        });
    }

    private void OpenBusinessMenu()
    {
        BandrekBusiness business = Business;
        List<L> choices =
        [
            new L("Belanja stok {0} gelas ({1})", "Buy {0} cups of stock ({1})").With(BandrekBusiness.RestockCups, Loc.Rupiah(BandrekBusiness.CupCost * BandrekBusiness.RestockCups)),
            business.UpgradeCost is { } cost
                ? new L("Naik kelas jadi {0} ({1})", "Upgrade to {0} ({1})").With(BandrekBusiness.TierName(business.Tier + 1).Text, Loc.Rupiah(cost))
                : new L("Usaha sudah di puncak kejayaan", "The business is at its peak"),
            new L("Ambil setoran ({0})", "Collect takings ({0})").With(Loc.Rupiah(business.Pending)),
            new("Udahan", "Leave"),
        ];
        L status = new L(
            "{0} — stok {1} gelas, terjual {2}. Dorong gerobak ke tempat ramai, lalu berhenti buat jualan.",
            "{0} — {1} cups in stock, {2} sold. Push the cart somewhere busy and stop to sell.")
            .With(BandrekBusiness.TierName(business.Tier).Text, business.Stock, business.CupsSold);
        ShowDialog(new Dialog(new("Buku Kas Bandrek", "Bandrek Ledger"), status, choices, choice =>
        {
            switch (choice)
            {
                case 0:
                    business.Restock(this);
                    OpenBusinessMenu();
                    break;
                case 1:
                    business.Upgrade(this);
                    OpenBusinessMenu();
                    break;
                case 2:
                    business.Collect(this);
                    break;
            }
        }));
    }
}
