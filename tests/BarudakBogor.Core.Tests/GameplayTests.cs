using System.Numerics;
using BarudakBogor.Activities;
using BarudakBogor.Entities;
using BarudakBogor.Gameplay;
using BarudakBogor.Localization;
using BarudakBogor.Missions;
using BarudakBogor.Numerics;
using BarudakBogor.Sim;
using BarudakBogor.World;

namespace BarudakBogor.Tests;

[Collection("world")]
public sealed class GameplayTests(WorldFixture fixture)
{
    private const float Step = 1f / 30f;

    private readonly WorldMap _map = fixture.Map;

    private GameSession NewSession(CharacterId character = CharacterId.Ujang) => new(_map, character, seed: 7);

    private static void Run(GameSession session, float seconds, InputState input = default)
    {
        for (float t = 0f; t < seconds; t += Step)
        {
            session.Update(Step, input);
        }
    }

    /// <summary>Presses the action key until every queued line of dialogue has been read.</summary>
    private static void SkipDialog(GameSession session)
    {
        for (int i = 0; i < 40 && session.Dialog is not null; i++)
        {
            session.Update(Step, new InputState { Action = true });
        }
    }

    [Fact]
    public void ClockRollsOverMidnight()
    {
        GameClock clock = new();
        clock.Set(1, 23.9f);
        clock.Update(60f * 3f);
        Assert.Equal(2, clock.Day);
        Assert.True(clock.IsNight);
    }

    [Fact]
    public void RainWetsTheRoadAndStormsStrike()
    {
        WeatherSystem weather = new(3);
        GameClock clock = new();
        int strikes = 0;
        int thunder = 0;
        weather.LightningStruck += _ => strikes++;
        weather.Thunder += _ => thunder++;
        weather.Locked = true;
        weather.Set(WeatherKind.BadaiPetir);
        for (int i = 0; i < 30 * 90; i++)
        {
            weather.Update(Step, clock, Vector2.Zero);
        }

        Assert.True(weather.Rain > 0.9f);
        Assert.True(weather.Wetness > 0.9f);
        Assert.True(strikes >= 3, $"only {strikes} strikes in 90 s of storm");
        Assert.True(thunder >= 1);
    }

    [Fact]
    public void MistComesWithRainAndClearsAfterwards()
    {
        WeatherSystem weather = new(3) { Locked = true };
        GameClock clock = new();
        weather.Set(WeatherKind.HujanSedang);
        weather.SetMist(0.7f);
        for (int i = 0; i < 30 * 60; i++)
        {
            weather.Update(Step, clock, Vector2.Zero);
        }

        Assert.True(weather.Mist > 0.5f, $"mist {weather.Mist}");
        Assert.True(weather.FogAt(260f, 12f) > 0.5f);
        weather.Set(WeatherKind.Cerah);
        for (int i = 0; i < 30 * 120; i++)
        {
            weather.Update(Step, clock, Vector2.Zero);
        }

        Assert.True(weather.Mist < 0.05f, $"mist {weather.Mist}");
    }

    [Fact]
    public void DayRunsThroughItsFourPhases()
    {
        GameClock clock = new();
        List<DayPhase> seen = [];
        clock.Set(1, 4f);
        for (int i = 0; i < 24 * 60; i++)
        {
            clock.Update(1f);
            if (seen.Count == 0 || seen[^1] != clock.Phase)
            {
                seen.Add(clock.Phase);
            }
        }

        Assert.Equal([DayPhase.Malam, DayPhase.Pagi, DayPhase.Siang, DayPhase.Sore, DayPhase.Malam], seen);
    }

    [Fact]
    public void InterceptorGetsAheadOfASteadyDriver()
    {
        GameSession session = NewSession();
        session.Missions.Start(session.Missions.Find("prolog"));
        SkipDialog(session);
        Vehicle angkot = session.Vehicles.Where(v => v.Spec.Kind == VehicleKind.Angkot)
            .OrderBy(v => Vector2.Distance(v.Ground, session.Player.Ground)).First();
        // A long straight road, so the only question is who is in front.
        RoadEdge toll = _map.Roads.Edges.First(e => e.Kind == RoadKind.Toll);
        Vector2 start = toll.Sample(toll.Length * 0.3f, out Vector2 along);
        angkot.Place(_map, start, MathUtil.YawOf(along));
        session.EnterVehicle(angkot);
        session.Update(Step, default);
        SkipDialog(session);
        List<Vehicle> hunters = [.. session.Vehicles.Where(v => v.Spec.Kind == VehicleKind.MobilKeluarga && v.Occupied)];
        Assert.Equal(2, hunters.Count);

        // Cruise at a modest speed: brake whenever the angkot gets above about 40 km/h.
        bool ahead = false;
        for (int i = 0; i < 30 * 40 && !ahead && session.Missions.Active is not null; i++)
        {
            float throttle = angkot.Speed < 11f ? 0.8f : 0f;
            session.Update(Step, new InputState { Move = new Vector2(0f, throttle) });
            Vector2 forward = angkot.Forward;
            ahead = hunters.Any(h => Vector2.Dot(h.Ground - angkot.Ground, forward) > 3f && Vector2.Distance(h.Ground, angkot.Ground) < 30f);
        }

        Assert.True(ahead, "neither pursuer got in front of a player cruising at 40 km/h");
    }

    [Fact]
    public void ControllerMapsOntoTheSameInputAsTheKeyboard()
    {
        InputState input = default;
        PadState pad = new(PadButtons.A | PadButtons.X, PadButtons.None, new Vector2(1f, 0.5f), default, 0f, 0f);
        PadInput.Merge(ref input, pad, driving: false, choices: 0, lanes: 0);
        Assert.True(input.Move.X < -0.8f, "stick right must steer right, which is negative X");
        Assert.True(input.Move.Y > 0.3f);
        Assert.True(input.Jump && input.Action && input.ActionHeld && input.Interact);

        // Held buttons do not fire again.
        input = default;
        PadInput.Merge(ref input, pad with { Previous = pad.Buttons }, false, 0, 0);
        Assert.False(input.Jump || input.Interact);
        Assert.True(input.ActionHeld);

        // Driving: triggers are throttle and brake.
        input = default;
        PadInput.Merge(ref input, new PadState(PadButtons.RightBumper, default, default, default, 0.2f, 1f), driving: true, 0, 0);
        Assert.Equal(0.8f, input.Move.Y, 3);
        Assert.True(input.Handbrake);

        // Drift near the centre is ignored.
        Assert.Equal(Vector2.Zero, PadInput.ApplyDeadZone(new Vector2(0.1f, -0.1f)));

        // Dialogue: the d-pad picks; rhythm: B is a lane, not "leave".
        input = default;
        PadInput.Merge(ref input, new PadState(PadButtons.DPadRight, default, default, default, 0f, 0f), false, choices: 3, lanes: 0);
        Assert.Equal(0b10, input.Lanes);
        input = default;
        PadInput.Merge(ref input, new PadState(PadButtons.B, default, default, default, 0f, 0f), false, choices: 0, lanes: 4);
        Assert.Equal(0b1000, input.Lanes);
        Assert.False(input.Cancel);
    }

    [Fact]
    public void EveryVehicleKindHasASpec()
    {
        foreach (VehicleKind kind in Enum.GetValues<VehicleKind>())
        {
            Assert.Equal(kind, VehicleSpec.Of(kind).Kind);
        }
    }

    [Fact]
    public void PuncakIsFoggierThanTheCity()
    {
        WeatherSystem weather = new(3);
        Assert.True(weather.FogAt(1_200f, 6f) > weather.FogAt(260f, 6f) + 0.3f);
    }

    [Fact]
    public void VehicleAcceleratesAndBrakes()
    {
        Vehicle car = new(1, VehicleKind.MobilKeluarga);
        RoadEdge edge = _map.Roads.Edges.First(e => e.Kind == RoadKind.Toll);
        Vector2 start = edge.Sample(50f, out Vector2 direction);
        car.Place(_map, start, MathUtil.YawOf(direction));
        for (int i = 0; i < 30 * 6; i++)
        {
            car.Update(Step, new DriveInput(1f, 0f, 0f, false), _map, 0f);
        }

        Assert.True(car.Speed > 15f, $"speed after 6 s: {car.Speed}");
        Assert.True(Vector2.Distance(car.Ground, start) > 40f);
        for (int i = 0; i < 30 * 6; i++)
        {
            car.Update(Step, new DriveInput(0f, 1f, 0f, false), _map, 0f);
        }

        Assert.True(car.Speed < 1f, "the car should have stopped (or started reversing slowly)");
    }

    [Fact]
    public void WetRoadsMakeTheCarUndersteer()
    {
        // The same steering input turns the car less when the tyres have less grip.
        float Turned(float wetness)
        {
            Vehicle car = new(1, VehicleKind.Angkot);
            RoadEdge edge = _map.Roads.Edges.First(e => e.Kind == RoadKind.Toll);
            Vector2 start = edge.Sample(50f, out Vector2 direction);
            float yaw = MathUtil.YawOf(direction);
            car.Place(_map, start, yaw);
            car.Velocity = direction * 22f;
            for (int i = 0; i < 30; i++)
            {
                car.Update(Step, new DriveInput(0.5f, 0f, 1f, false), _map, wetness);
            }

            return MathF.Abs(MathUtil.WrapAngle(car.Yaw - yaw));
        }

        float dry = Turned(0f);
        float wet = Turned(1f);
        Assert.True(dry > wet * 1.3f, $"dry {dry} rad, wet {wet} rad");
    }

    [Fact]
    public void PlayerWalksAndCannotEnterBuildings()
    {
        GameSession session = NewSession();
        Building building = _map.Buildings[10];
        Vector2 outside = building.Center - (building.AxisDepth * (building.HalfDepth + 3f));
        session.Player.Teleport(_map, outside, 0f);
        InputState toward = new() { Move = new Vector2(0f, 1f), CameraYaw = MathUtil.YawOf(building.AxisDepth) };
        Run(session, 4f, toward);
        Assert.False(_map.IsInsideBuilding(session.Player.Ground));
        Assert.True(session.Player.DistanceWalked > 1f);
    }

    [Fact]
    public void PlayerCanEnterAndLeaveAVehicle()
    {
        GameSession session = NewSession();
        Vehicle angkot = session.Vehicles.First(v => v.Spec.Kind == VehicleKind.Angkot);
        session.Player.Teleport(_map, angkot.Ground + new Vector2(1.6f, 0f), 0f);
        session.Update(Step, default);
        Assert.NotNull(session.VehiclePrompt);
        session.Update(Step, new InputState { Interact = true });
        Assert.Same(angkot, session.Player.Vehicle);
        Run(session, 3f, new InputState { Move = new Vector2(0f, 1f) });
        Assert.True(angkot.Speed > 3f);
        Run(session, 5f, new InputState { Handbrake = true });
        session.Update(Step, new InputState { Interact = true });
        Assert.Null(session.Player.Vehicle);
    }

    [Fact]
    public void PopulationAppearsInTheCityAndStaysBounded()
    {
        GameSession session = NewSession();
        session.Clock.Set(1, 10f);
        session.Weather.Locked = true;
        session.Weather.Set(WeatherKind.Cerah);
        session.Player.Teleport(_map, _map[PoiId.PasarAnyar].RoadPoint, 0f);
        Run(session, 20f);
        Assert.True(session.Population.Pedestrians.Count >= 5, $"{session.Population.Pedestrians.Count} pedestrians");
        Assert.True(session.Population.Traffic.Count >= 3, $"{session.Population.Traffic.Count} cars");
        Assert.True(session.Population.Pedestrians.Count >= 20, $"{session.Population.Pedestrians.Count} pedestrians after 20 s in the market");
        Assert.True(session.Population.Pedestrians.Count <= 62);
        Assert.True(session.Population.Traffic.Count <= 20);
        Assert.Contains(session.Population.Animals, a => a.Kind is AnimalKind.Kucing or AnimalKind.Anjing or AnimalKind.Ayam);
    }

    /// <summary>
    /// Traffic queues, goes round or waits; it never drives through another vehicle. Checked on a
    /// crowded market street, at the big roundabout and on the toll road, where it is fastest.
    /// </summary>
    [Fact]
    public void TrafficNeverDrivesThroughItself()
    {
        Vector2 toll = _map.Roads.Edges.First(e => e.Kind == RoadKind.Toll && e.Length > 200f).Sample(100f, out Vector2 along);
        Vector2[] places = [_map[PoiId.PasarAnyar].RoadPoint, _map[PoiId.TuguKujang].RoadPoint, toll + (MathUtil.Left(along) * 14f)];
        int seen = 0;
        float moved = 0f;
        foreach (Vector2 place in places)
        {
            GameSession session = NewSession();
            session.Clock.Set(1, 10f);
            session.Player.Teleport(_map, place, 0f);
            for (float t = 0f; t < 90f; t += Step)
            {
                session.Update(Step, default);
                IReadOnlyList<Vehicle> cars = session.Population.Traffic;
                for (int a = 0; a < cars.Count; a++)
                {
                    moved += MathF.Abs(cars[a].Speed) * Step;
                    for (int b = a + 1; b < cars.Count; b++)
                    {
                        // Two boxes overlap only if neither sees a gap along its own length or width.
                        Assert.False(
                            Covers(cars[a], cars[b]) && Covers(cars[b], cars[a]),
                            $"{cars[a].Spec.Kind} is inside {cars[b].Spec.Kind} at {cars[a].Ground} after {t:0.0} s");
                    }
                }

                seen = Math.Max(seen, cars.Count);
            }
        }

        Assert.True(seen >= 5, $"only {seen} cars at once");
        Assert.True(moved > 3_000f, $"traffic only covered {moved:0} m: it is standing still");
    }

    [Fact]
    public void JunctionsHaveLightsThatTakeTurns()
    {
        int junctions = _map.Signals.Select(s => s.Node).Distinct().Count();
        Assert.True(junctions is >= 6 and <= 60, $"{junctions} junctions with traffic lights");
        foreach (IGrouping<int, TrafficSignal> junction in _map.Signals.GroupBy(s => s.Node))
        {
            HashSet<int> served = [];
            for (double time = 0; time < 120; time += 0.5)
            {
                TrafficSignal[] green = [.. junction.Where(s => s.ColourAt(time) == SignalColour.Green)];
                Assert.True(green.Length <= 1, $"{green.Length} arms of junction {junction.Key} are green at once");
                Assert.True(junction.Count(s => s.ColourAt(time) != SignalColour.Red) <= 1);
                served.UnionWith(green.Select(s => s.Edge));
            }

            Assert.Equal(junction.Count(), served.Count);
        }

        // The street furniture stands beside the road, never on it.
        foreach (string model in (ReadOnlySpan<string>)["lampu_merah", "cctv", "spanduk", "light_box", "videotron"])
        {
            Prop[] placed = [.. _map.Props.Where(p => p.Model == model)];
            Assert.True(placed.Length >= 3, $"only {placed.Length} of {model}");
            Assert.All(placed, p => Assert.False(_map.Roads.IsOnRoad(p.Position), $"{model} on the road at {p.Position}"));
        }

        Assert.Contains(_map.Signs, s => s.Look == SignLook.Banner);
        Assert.Contains(_map.Signs, s => s.Look == SignLook.LightBox);
    }

    [Fact]
    public void TrafficStopsAtARedLight()
    {
        Vector2 centre = _map[PoiId.TuguKujang].Position;
        TrafficSignal[] lights = [.. _map.Signals.OrderBy(s => Vector2.Distance(s.Position, centre)).Take(1)
            .SelectMany(nearest => _map.Signals.Where(s => s.Node == nearest.Node))];
        Vector2 junction = _map.Roads.Nodes[lights[0].Node].Position;
        GameSession session = NewSession();
        session.Clock.Set(1, 10f);
        // Stand well off the road: the player must not be what holds the cars up.
        session.Player.Teleport(_map, _map.ResolveCircle(junction + new Vector2(24f, 24f), 0.5f, out _), 0f);
        int waited = 0;
        int passed = 0;
        Dictionary<(int Edge, int Car), float> lastAlong = [];
        for (float t = 0f; t < 240f; t += Step)
        {
            session.Update(Step, default);
            foreach (TrafficSignal light in lights)
            {
                // The line is where the post stands; the post faces the drivers it speaks to.
                Vector2 travel = -MathUtil.Forward(light.Yaw);
                double now = session.Population.SignalTime;
                SignalColour colour = light.ColourAt(now);
                // Rolling over the line as it changes is how everyone drives; going through a light
                // that has been red for seconds is not.
                bool longRed = colour == SignalColour.Red && light.ColourAt(now - 1.5) == SignalColour.Red && light.ColourAt(now - 3) == SignalColour.Red;
                foreach (Vehicle car in session.Population.Traffic)
                {
                    Vector2 offset = car.Ground - light.Position;
                    float along = Vector2.Dot(offset, travel);
                    float across = -Vector2.Dot(offset, MathUtil.Left(travel));
                    if (Vector2.Dot(car.Forward, travel) < 0.8f || across < 0f || across > 6f)
                    {
                        continue;
                    }

                    if (colour == SignalColour.Red && along is > -12f and < 0f && MathF.Abs(car.Speed) < 0.2f)
                    {
                        waited++;
                    }

                    // Crossing the line, which is where the post stands. A car caught beyond it when
                    // the light changed (the junction was full) may clear off; nobody may drive over it.
                    bool before = lastAlong.TryGetValue((light.Edge, car.Id), out float last) && last < 0f;
                    lastAlong[(light.Edge, car.Id)] = along;
                    if (before && along >= 0f)
                    {
                        passed++;
                        Assert.False(longRed, $"a {car.Spec.Kind} ran the red light at {car.Ground} after {t:0.0} s (speed {car.Speed:0.0})");
                    }
                }
            }
        }

        Assert.True(waited > 30, $"nobody waited at the lights ({waited} frames)");
        Assert.True(passed >= 5, $"only {passed} cars got through the lights");
    }

    [Fact]
    public void PigeonsFlyOffAndComeDownAgain()
    {
        GameSession session = NewSession();
        session.Clock.Set(1, 10f);
        Vector2 square = _map[PoiId.TuguKujang].Position;
        session.Player.Teleport(_map, _map.ResolveCircle(square + new Vector2(70f, 0f), 0.5f, out _), 0f);
        Run(session, 2f);
        Animal[] flock = [.. session.Population.Animals.Where(a => a.Kind == AnimalKind.Burung)];
        Assert.True(flock.Length >= 5, $"only {flock.Length} pigeons");

        // Walk straight into one of them.
        Animal bird = flock.First(b => !b.Airborne);
        session.Player.Teleport(_map, bird.Position.Xz() + new Vector2(2f, 0f), 0f);
        float highest = 0f;
        bool landed = false;
        for (float t = 0f; t < 40f && !landed; t += Step)
        {
            session.Update(Step, default);
            float above = bird.Position.Y - _map.Terrain.HeightAt(bird.Position.Xz());
            highest = MathF.Max(highest, above);
            landed = highest > 2f && !bird.Airborne;
            Assert.True(above > -0.01f, "a pigeon went underground");
        }

        Assert.True(highest > 2f, $"the pigeon only got {highest:0.0} m up");
        Assert.True(landed, "the pigeon never came down");
        Assert.True(Vector2.Distance(bird.Position.Xz(), session.Player.Ground) > 6f);
    }

    [Fact]
    public void StreetLifeTurnsUp()
    {
        HashSet<NpcKind> seen = [];
        HashSet<AnimalKind> animals = [];
        foreach ((PoiId place, float hour) in (ReadOnlySpan<(PoiId, float)>)[(PoiId.PasarAnyar, 10f), (PoiId.TuguKujang, 16.5f),
            (PoiId.BalaiKota, 7.5f), (PoiId.SuryaKencana, 19f), (PoiId.GerbangKebunRaya, 11f)])
        {
            GameSession session = NewSession();
            session.Clock.Set(1, hour);
            session.Weather.Locked = true;
            session.Weather.Set(WeatherKind.Cerah);
            session.Player.Teleport(_map, _map[place].RoadPoint, 0f);
            Run(session, 25f);
            seen.UnionWith(session.Population.Pedestrians.Select(p => p.Kind));
            animals.UnionWith(session.Population.Animals.Select(a => a.Kind));
        }

        NpcKind[] street = [NpcKind.Pkl, NpcKind.Polisi, NpcKind.Pns, NpcKind.Badut, NpcKind.Pengamen, NpcKind.AnakPunk];
        Assert.True(street.All(seen.Contains), $"missing {string.Join(", ", street.Except(seen))}");
        AnimalKind[] wanted = [AnimalKind.Burung, AnimalKind.Monyet, AnimalKind.Ayam];
        Assert.True(wanted.All(animals.Contains), $"missing {string.Join(", ", wanted.Except(animals))}");
    }

    [Fact]
    public void WaterAnimalsLiveInTheWaterAndStayThere()
    {
        // The fishing pond, and the first stretch of the Ciliwung that has a crocodile.
        Corridor river = _map.Terrain.Corridors.First(c => c.Kind == CorridorKind.River);
        HashSet<AnimalKind> seen = [];
        foreach (Vector2 place in (ReadOnlySpan<Vector2>)[_map[PoiId.Empang].Position, river.Sample(150f, out _, out _) + new Vector2(30f, 0f)])
        {
            GameSession session = NewSession();
            session.Player.Teleport(_map, place, 0f);
            for (float t = 0f; t < 40f; t += Step)
            {
                session.Update(Step, default);
                foreach (Animal animal in session.Population.Animals.Where(a => Animal.IsAquatic(a.Kind)))
                {
                    seen.Add(animal.Kind);
                    Vector2 at = animal.Position.Xz();
                    Assert.True(_map.Terrain.WaterLevelAt(at) is { } level && animal.Position.Y <= level
                        && animal.Position.Y > _map.Terrain.GroundAt(at.X, at.Y) - 0.05f, $"{animal.Kind} is out of the water at {at}");
                }
            }
        }

        AnimalKind[] wanted = [AnimalKind.Ikan, AnimalKind.KuraKura, AnimalKind.Bebek, AnimalKind.Buaya];
        Assert.True(wanted.All(seen.Contains), $"missing {string.Join(", ", wanted.Except(seen))}");
    }

    [Fact]
    public void FishAndCrocodilesKeepAwayFromASwimmer()
    {
        GameSession session = NewSession();
        Pond pond = _map.Ponds[0];
        session.Player.Teleport(_map, _map[PoiId.Empang].Position, 0f);
        Run(session, 3f);
        Animal fish = session.Population.Animals.Where(a => a.Kind == AnimalKind.Ikan)
            .OrderBy(a => Vector2.Distance(a.Position.Xz(), pond.Center)).First();
        // Jump in right next to it.
        session.Player.Teleport(_map, fish.Position.Xz() + new Vector2(1.2f, 0f), 0f);
        Run(session, 4f);
        Assert.True(Vector2.Distance(fish.Position.Xz(), session.Player.Ground) > 4f, "the fish did not mind");
    }

    [Fact]
    public void PondsAndRiversHaveWaterPlants()
    {
        foreach (string model in (ReadOnlySpan<string>)["teratai", "eceng_gondok", "gelagah"])
        {
            Assert.True(_map.Props.Count(p => p.Model == model) > 20, $"hardly any {model}");
        }

        foreach (Pond pond in _map.Ponds)
        {
            Assert.Contains(_map.Props, p => p.Afloat && Vector2.Distance(p.Position, pond.Center) < pond.Radius);
        }

        // Whatever floats is over water, and none of it is in anybody's way.
        Assert.All(_map.Props.Where(p => p.Afloat), p => Assert.NotNull(_map.Terrain.WaterLevelAt(p.Position)));
        Assert.All(_map.Props.Where(p => p.Model is "teratai" or "eceng_gondok" or "gelagah"), p => Assert.Equal(0f, p.Radius));
    }

    /// <summary>Seen along and across <paramref name="a"/>, does the body of <paramref name="b"/> reach into it?</summary>
    private static bool Covers(Vehicle a, Vehicle b)
    {
        Vector2 forward = a.Forward;
        Vector2 offset = b.Ground - a.Ground;
        float cos = MathF.Abs(Vector2.Dot(forward, b.Forward));
        float sin = MathF.Sqrt(MathF.Max(0f, 1f - (cos * cos)));
        float halfLength = b.Spec.Length * 0.5f;
        float halfWidth = b.Spec.Radius * 0.8f;
        float reach = (a.Spec.Length * 0.5f) + (cos * halfLength) + (sin * halfWidth);
        float wide = (a.Spec.Radius * 0.8f) + (sin * halfLength) + (cos * halfWidth);
        return MathF.Abs(Vector2.Dot(offset, forward)) < reach * 0.9f && MathF.Abs(Vector2.Dot(offset, MathUtil.Left(forward))) < wide * 0.9f;
    }

    [Fact]
    public void DeerLiveOnThePalaceLawn()
    {
        GameSession session = NewSession();
        session.Player.Teleport(_map, _map[PoiId.IstanaRusa].Position + new Vector2(30f, 0f), 0f);
        Run(session, 2f);
        Assert.Contains(session.Population.Animals, a => a.Kind == AnimalKind.Rusa);
    }

    [Fact]
    public void FishingLandsAFishWithGoodTechnique()
    {
        GameSession session = NewSession(CharacterId.Cecep);
        FishingGame game = new(5, 0.35f, 0.5f);
        session.StartActivity(game);
        int before = session.Progress.FishCaught;
        for (int i = 0; i < 30 * 240 && game.Count == 0; i++)
        {
            InputState input = default;
            switch (game.State)
            {
                case FishingState.Ready or FishingState.Escaped or FishingState.Bite:
                    input.Action = true;
                    break;
                case FishingState.Reeling:
                    // Reel while there is headroom, ease off near the top of the safe band.
                    input.ActionHeld = game.Tension < 0.6f;
                    break;
            }

            session.Update(Step, input);
        }

        Assert.True(game.Count >= 1, "no fish landed in four minutes of sensible play");
        Assert.Equal(before + 1, session.Progress.FishCaught);
    }

    [Fact]
    public void RhythmGameScoresHitsAndCountsMisses()
    {
        GameSession session = NewSession();
        bool finished = false;
        RhythmGame game = RhythmGame.Karaoke(11, (_, _) => finished = true);
        List<(int Lane, float TimeToHit)> visible = [];
        // A perfect player for the first half, silence afterwards.
        for (int i = 0; i < 30 * 60 && !game.Finished; i++)
        {
            InputState input = default;
            game.VisibleBeats(visible);
            if (game.Time < 20f)
            {
                foreach ((int lane, float timeToHit) in visible)
                {
                    if (MathF.Abs(timeToHit) < Step * 0.6f)
                    {
                        input.Lanes |= 1 << lane;
                    }
                }
            }

            game.Update(session, Step, input);
        }

        Assert.True(finished);
        Assert.True(game.Hits > 20, $"{game.Hits} hits");
        Assert.True(game.Misses > 10, $"{game.Misses} misses");
        Assert.Equal(game.Total, game.Hits + game.Misses);
    }

    [Fact]
    public void PrologueCanBePlayedThrough()
    {
        GameSession session = NewSession();
        Mission prolog = session.Missions.Find("prolog");
        Assert.Equal(MissionState.Available, prolog.State);
        Assert.Equal(MissionState.Locked, session.Missions.Find("cilok").State);

        Assert.True(session.Missions.Start(prolog));
        SkipDialog(session);
        Assert.Equal(WeatherKind.HujanDeras, session.Weather.Kind);

        Vehicle angkot = session.Vehicles.Where(v => v.Spec.Kind == VehicleKind.Angkot)
            .OrderBy(v => Vector2.Distance(v.Ground, session.Player.Ground)).First();
        session.EnterVehicle(angkot);
        session.Update(Step, default);
        SkipDialog(session);
        Assert.Equal(2, session.Vehicles.Count(v => v.Spec.Kind == VehicleKind.MobilKeluarga && v.Occupied));

        // The pursuers must actually chase.
        Vehicle hunter = session.Vehicles.First(v => v.Spec.Kind == VehicleKind.MobilKeluarga && v.Occupied);
        Vector2 hunterStart = hunter.Ground;
        Run(session, 1f, new InputState { Move = new Vector2(0f, 1f) });
        Assert.True(Vector2.Distance(hunter.Ground, hunterStart) > 2f);

        long before = session.Progress.Money;
        angkot.Place(_map, _map[PoiId.RumahUjang].RoadPoint, 0f);
        session.Update(Step, default);
        SkipDialog(session);
        Run(session, 0.2f);

        Assert.Equal(MissionState.Completed, prolog.State);
        Assert.Null(session.Missions.Active);
        Assert.True(session.Progress.Money > before);
        Assert.Equal(MissionState.Available, session.Missions.Find("cilok").State);
        Assert.False(session.Weather.Locked);
    }

    [Fact]
    public void StandingStillInTheChaseFailsTheMission()
    {
        GameSession session = NewSession();
        session.Missions.Start(session.Missions.Find("prolog"));
        SkipDialog(session);
        session.EnterVehicle(session.Vehicles.First(v => v.Spec.Kind == VehicleKind.Angkot));
        session.Update(Step, default);
        SkipDialog(session);
        Run(session, 25f);
        Assert.Null(session.Missions.Active);
        Assert.Equal(MissionState.Available, session.Missions.Find("prolog").State);
        // Whatever the mission spawned is gone again.
        Assert.DoesNotContain(session.Vehicles, v => v.Spec.Kind == VehicleKind.MobilKeluarga && v.Occupied);
    }

    [Fact]
    public void EveryMissionStartsAndCanBeAbandonedCleanly()
    {
        GameSession session = NewSession();
        session.Progress.CompletedMissions.Add("prolog");
        session.Missions.Refresh();
        foreach (Mission mission in session.Missions.All.Where(m => m.Id != "prolog"))
        {
            // Later chapters need earlier ones; unlock whatever this one asks for.
            session.Progress.CompletedMissions.Remove(mission.Id);
            if (mission.Requires is { } needed)
            {
                session.Progress.CompletedMissions.Add(needed);
            }

            session.Missions.Refresh();
            int vehicles = session.Vehicles.Count;
            Assert.True(session.Missions.Start(mission), mission.Id);
            SkipDialog(session);
            Run(session, 1f);
            Assert.False(mission.Title.IsEmpty);
            Assert.False(mission.Summary.En.Length == 0);
            session.Missions.Abandon();
            Assert.Null(session.Missions.Active);
            Assert.Equal(MissionState.Available, mission.State);
            Assert.True(session.Vehicles.Count <= vehicles + 1, $"{mission.Id} leaked vehicles");
            Assert.False(session.Weather.Locked);
            Assert.Equal(1f, session.Clock.MinutesPerSecond);
        }
    }

    [Fact]
    public void BandrekCartSellsWhenParkedInTheMarket()
    {
        GameSession session = NewSession(CharacterId.Aceng);
        Vehicle cart = session.Vehicles.First(v => v.Spec.Kind == VehicleKind.GerobakBandrek);
        cart.Place(_map, _map[PoiId.PasarAnyar].RoadPoint, 0f);
        session.EnterVehicle(cart);
        long money = session.Progress.Money;
        int stock = session.Business.Stock;
        Run(session, 40f);
        Assert.True(session.Business.Stock < stock, "nothing sold in 40 s at the market");
        Assert.True(session.Progress.Money > money);
        Assert.True(session.Progress.Reputation(Career.Bandrek) > 0);
    }

    [Fact]
    public void ReputationUnlocksAnEnding()
    {
        GameSession session = NewSession();
        Career? reached = null;
        session.Progress.EndingReached += career => reached = career;
        session.Progress.AddReputation(Career.Mancing, 60);
        Assert.Null(reached);
        session.Progress.AddReputation(Career.Mancing, 45);
        Assert.Equal(Career.Mancing, reached);
        Assert.Contains(Career.Mancing, session.Progress.Endings);
    }

    [Fact]
    public void PhotoOfTheMonumentInRainScoresWell()
    {
        GameSession session = NewSession();
        session.Weather.Set(WeatherKind.HujanDeras);
        session.Weather.Settle();
        Poi kujang = _map[PoiId.TuguKujang];
        Vector3 camera = (kujang.Position + new Vector2(0f, -40f)).ToWorld(270f);
        PhotoResult? result = session.TakePhoto(camera, new Vector3(0f, 0f, 1f));
        Assert.NotNull(result);
        Assert.True(result.Score >= 70, $"score {result.Score}");
        Assert.Contains(result.Tags, t => t.Id == "Tugu Kujang");
        Assert.Null(session.TakePhoto(camera, Vector3.UnitZ));
    }

    [Fact]
    public void FastTravelCostsMoneyAndMovesThePlayer()
    {
        GameSession session = NewSession();
        session.Progress.Earn(1_000_000);
        long before = session.Progress.Money;
        Assert.True(session.FastTravel(PoiId.PuncakPass));
        Assert.True(Vector2.Distance(session.Player.Ground, _map[PoiId.PuncakPass].Position) < 60f);
        Assert.True(session.Progress.Money < before);
    }

    [Fact]
    public void SaveRoundTripRestoresProgress()
    {
        GameSession session = NewSession(CharacterId.Mumu);
        session.Progress.Earn(123_000);
        session.Progress.AddReputation(Career.Kuliner, 33);
        session.Progress.CompletedMissions.Add("prolog");
        session.Business.Tier = BusinessTier.Warung;
        session.Clock.Set(4, 9.25f);
        session.Player.Teleport(_map, _map[PoiId.KampusIpb].RoadPoint, 1f);

        string json = SaveSystem.ToJson(SaveSystem.Capture(session));
        SaveData data = SaveSystem.FromJson(json)!;
        GameSession restored = NewSession();
        SaveSystem.Apply(restored, data);

        Assert.Equal(CharacterId.Mumu, restored.Player.Profile.Id);
        Assert.Equal(session.Progress.Money, restored.Progress.Money);
        Assert.Equal(33, restored.Progress.Reputation(Career.Kuliner));
        Assert.Equal(MissionState.Completed, restored.Missions.Find("prolog").State);
        Assert.Equal(MissionState.Available, restored.Missions.Find("horor").State);
        Assert.Equal(BusinessTier.Warung, restored.Business.Tier);
        Assert.Equal(4, restored.Clock.Day);
        Assert.True(Vector2.Distance(restored.Player.Ground, session.Player.Ground) < 0.01f);
    }

    [Fact]
    public void TextExistsInBothLanguages()
    {
        Loc.Language = Language.English;
        Assert.Equal("Kujang Monument", Loc.Name("poi.tugukujang").Text);
        Loc.Language = Language.Indonesia;
        Assert.Equal("Tugu Kujang", Loc.Name("poi.tugukujang").Text);
        Assert.Equal("Rp 12.500", Loc.Rupiah(12_500));
        foreach (CharacterProfile profile in CharacterProfile.Roster)
        {
            Assert.False(string.IsNullOrWhiteSpace(profile.Bio.Id));
            Assert.False(string.IsNullOrWhiteSpace(profile.Bio.En));
        }

        foreach (PoiId id in Enum.GetValues<PoiId>())
        {
            Assert.DoesNotContain("poi.", Loc.Name(fixture.Map[id].NameKey).En);
        }
    }
}
