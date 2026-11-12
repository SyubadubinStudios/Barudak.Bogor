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
