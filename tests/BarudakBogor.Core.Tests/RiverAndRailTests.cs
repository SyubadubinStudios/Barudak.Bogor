using System.Numerics;
using BarudakBogor.Entities;
using BarudakBogor.Gameplay;
using BarudakBogor.World;

namespace BarudakBogor.Tests;

/// <summary>The Ciliwung, its bridges, the railway, Stasiun Bogor and the commuter train.</summary>
[Collection("world")]
public sealed class RiverAndRailTests(WorldFixture fixture)
{
    private const float Step = 1f / 30f;

    private readonly WorldMap _map = fixture.Map;

    [Fact]
    public void TheRiverHoldsWaterAllTheWayDown()
    {
        Corridor river = _map.Corridors.Single(c => c.Kind == CorridorKind.River);
        Assert.True(river.Length > 12_000f);
        for (float d = 50f; d < river.Length - 50f; d += 150f)
        {
            Vector2 p = river.Sample(d, out Vector2 direction, out float level);
            Assert.Equal(level, _map.Terrain.WaterLevelAt(p)!.Value, 1);
            // Unless a bridge is overhead, the bed is under water here and the bank is dry.
            if (_map.Bridges.All(b => b.Path.All(q => Vector2.Distance(q, p) > 40f)))
            {
                Assert.True(_map.Terrain.HeightAt(p) < level - 1f, $"no water at {d} m");
                Vector2 bank = p + (new Vector2(direction.Y, -direction.X) * (river.HalfWidth + 8f));
                Assert.Null(_map.Terrain.WaterLevelAt(bank));
                Assert.True(_map.Terrain.HeightAt(bank) > level + 1f, $"flooded bank at {d} m");
            }
        }
    }

    [Fact]
    public void RoadsCrossTheRiverOnBridges()
    {
        Assert.True(_map.Bridges.Count >= 3, $"only {_map.Bridges.Count} bridges");
        foreach (Bridge bridge in _map.Bridges)
        {
            foreach (Vector2 p in bridge.Path)
            {
                Assert.True(_map.Roads.IsOnRoad(p));
                // Driving height is the deck, well clear of the water; the earth below is the river.
                // (Where two bridges meet, the higher deck wins by a few centimetres.)
                Assert.InRange(_map.Terrain.HeightAt(p) - bridge.Height, -0.01f, 0.8f);
                Assert.True(bridge.Height > bridge.WaterLevel + 2f);
            }

            // Somewhere under the span the earth drops away to the river bed.
            float bed = bridge.Path.Min(q => _map.Terrain.LatticeHeight((int)MathF.Round(q.X / Terrain.Cell), (int)MathF.Round(q.Y / Terrain.Cell)));
            Assert.True(bed < bridge.Height - 1f, "the ground under the bridge was filled in");
        }

        // A car can be driven across without dropping into the river.
        Bridge first = _map.Bridges[0];
        Vehicle car = new(1, VehicleKind.Angkot);
        Vector2 start = first.Path[0];
        Vector2 end = first.Path[^1];
        car.Place(_map, start, BarudakBogor.Numerics.MathUtil.YawOf(Vector2.Normalize(end - start)));
        float lowest = float.MaxValue;
        for (int i = 0; i < 30 * 8; i++)
        {
            car.Update(Step, new DriveInput(0.6f, 0f, 0f, false), _map, 0f);
            if (Vector2.Distance(car.Ground, start) < Vector2.Distance(start, end))
            {
                lowest = MathF.Min(lowest, car.Position.Y);
            }
        }

        Assert.True(lowest > first.WaterLevel + 1.5f, $"the car sank to {lowest - first.WaterLevel:0.0} m above the water");
    }

    [Fact]
    public void NothingIsBuiltInTheRiverOrOnTheTrack()
    {
        foreach (Building building in _map.Buildings)
        {
            Assert.True(_map.Terrain.CorridorClearance(building.Center) > 4f, $"building at {building.Center}");
        }
    }

    [Fact]
    public void TheRailwayIsGradedAndReachesTheStation()
    {
        Corridor rail = _map.Railway!;
        float previous = rail.Levels[0];
        for (float d = 0f; d < rail.Length; d += 20f)
        {
            Vector2 p = rail.Sample(d, out _, out float level);
            Assert.True(MathF.Abs(level - previous) < 0.8f, $"a step of {level - previous:0.00} m in the rails at {d} m");
            Assert.InRange(_map.Terrain.HeightAt(p) - level, -0.3f, 0.3f);
            previous = level;
        }

        Assert.NotEmpty(_map.Crossings);
        Poi station = _map[PoiId.StasiunBogor];
        RoadNode from = _map.Roads.NearestNode(_map[PoiId.TuguKujang].RoadPoint);
        RoadNode to = _map.Roads.NearestNode(station.RoadPoint);
        Assert.NotEmpty(_map.Roads.FindRoute(from.Id, to.Id));
        Assert.True(rail.Closest(station.Position, 60f, out _, out _, out _), "the station is not by the track");

        // The platform is a step up from the rails.
        Vector2 platform = _map.Slabs.First(slab => slab.Look == SlabLook.Concrete && slab.HalfDepth > 60f).Center;
        Assert.InRange(_map.Terrain.HeightAt(platform) - rail.Levels[3], 0.8f, 1.2f);
    }

    [Fact]
    public void TheTrainShuttlesAndComesBack()
    {
        GameSession session = new(_map, CharacterId.Ujang, seed: 3);
        Train train = session.Train!;
        float start = train.Tail;
        float farthest = start;
        bool left = false;
        bool returned = false;
        // The player stays in town; twelve minutes is more than a round trip.
        for (int i = 0; i < 30 * 60 * 12 && !returned; i++)
        {
            train.Update(Step, session.Player.Ground, 0.4f);
            farthest = MathF.Max(farthest, train.Tail);
            left |= train.State == TrainState.Away;
            returned = left && train.State == TrainState.AtStation;
        }

        Assert.True(farthest > start + 2_000f, $"the train only went {farthest - start:0} m");
        Assert.True(returned, "the train never came back");
        Assert.Equal(start, train.Tail, 0);
    }

    [Fact]
    public void TheTrainStopsForSomeoneOnTheTrack()
    {
        GameSession session = new(_map, CharacterId.Ujang, seed: 3);
        Train train = session.Train!;
        // Stand on the rails 600 m up the line, well beyond where the train reaches full speed.
        Vector2 person = train.Track.Sample(700f, out _, out _);
        int honks = 0;
        train.Honked += _ => honks++;
        for (int i = 0; i < 30 * 60 * 4; i++)
        {
            train.Update(Step, person, 0.4f);
        }

        Assert.True(train.Blocked);
        Assert.Equal(0f, train.Speed);
        float nose = train.Tail + train.Length;
        Assert.InRange(700f - nose, 3f, 40f);
        Assert.True(honks >= 3, $"only {honks} blasts of the horn");

        // Step aside and it carries on.
        Vector2 aside = person + new Vector2(30f, 0f);
        for (int i = 0; i < 30 * 30; i++)
        {
            train.Update(Step, aside, 0.4f);
        }

        Assert.True(train.Tail + train.Length > 800f);
    }
}
