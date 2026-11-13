using System.Numerics;
using BarudakBogor.Localization;
using BarudakBogor.World;

namespace BarudakBogor.Tests;

/// <summary>The city follows the real one: places sit where they are in Bogor, measured from Tugu Kujang.</summary>
[Collection("world")]
public sealed class RealBogorTests(WorldFixture fixture)
{
    private readonly WorldMap _map = fixture.Map;

    /// <summary>Metres east and south of Tugu Kujang, from OpenStreetMap.</summary>
    public static TheoryData<PoiId, float, float> Places => new()
    {
        { PoiId.IstanaBogor, -847f, -378f },
        { PoiId.MuseumZoologi, -900f, 238f },
        { PoiId.MuseumTanah, -786f, 265f },
        { PoiId.MuseumPeta, -963f, -1_795f },
        { PoiId.BotaniSquare, 254f, -11f },
        { PoiId.BogorTradeMall, -1_040f, 383f },
        { PoiId.YogyaJunction, -964f, -1_382f },
        { PoiId.PlazaJambuDua, 464f, -3_608f },
        { PoiId.Ramayana, 250f, -3_420f },
        { PoiId.Ekalokasari, 1_351f, 2_246f },
        { PoiId.LotteMart, -2_811f, -4_853f },
        { PoiId.BogorGreatMall, -3_254f, -5_028f },
        { PoiId.SuryaKencana, -630f, 235f },
        { PoiId.AirMancur, -906f, -2_260f },
        { PoiId.LapanganSempur, -473f, -1_079f },
        { PoiId.GorPajajaran, -808f, -2_683f },
        { PoiId.StasiunBogor, -1_579f, -774f },
        { PoiId.PasarAnyar, -1_429f, -1_083f },
        { PoiId.BalaiKota, -1_257f, -701f },
        { PoiId.TerminalBaranangsiang, 104f, 293f },
    };

    [Theory]
    [MemberData(nameof(Places))]
    public void PlaceIsWhereItIsInBogor(PoiId id, float east, float south)
    {
        Poi poi = _map[id];
        float off = Vector2.Distance(poi.Position, new Vector2(east, south));
        // Out of town the generated roads are sparser, so a place may sit further from its true spot.
        float allowed = MathF.Abs(south) > 4_000f ? 900f : 220f;
        Assert.True(off < allowed, $"{id} is {off:0} m from its real position");
        Assert.False(Loc.Name(poi.NameKey).Id.StartsWith("poi.", StringComparison.Ordinal), $"{id} has no name");
    }

    [Fact]
    public void EveryPlaceCanBeDrivenToFromTuguKujang()
    {
        RoadNode from = _map.Roads.NearestNode(_map[PoiId.TuguKujang].RoadPoint);
        foreach (Poi poi in _map.Pois)
        {
            RoadNode to = _map.Roads.NearestNode(poi.RoadPoint);
            Assert.True(to.Id == from.Id || _map.Roads.FindRoute(from.Id, to.Id).Count > 0, $"no road to {poi.Id}");
            Assert.False(_map.IsInsideBuilding(poi.RoadPoint), $"{poi.Id} is reached through a wall");
        }
    }

    [Fact]
    public void NoMainRoadJustStops()
    {
        // A main road may end at a place (a terminal, a mountain village), never in the middle of nowhere.
        List<string> loose = [];
        foreach (RoadNode node in _map.Roads.Nodes)
        {
            if (node.Edges.Count == 1 && _map.Roads.Edges[node.Edges[0]].Kind is RoadKind.Arterial or RoadKind.Toll
                && !_map.Pois.Any(p => Vector2.Distance(p.RoadPoint, node.Position) < 400f)
                && _map.Districts.All(d => d.Id == DistrictId.PusatKota || Vector2.Distance(d.Center, node.Position) > d.Radius)
                // The edge of the map is the one place a road may run out.
                && MathF.Max(MathF.Abs(node.Position.X), MathF.Abs(node.Position.Y)) < 19_000f)
            {
                loose.Add($"{node.Position.X:0},{node.Position.Y:0}");
            }
        }

        Assert.True(loose.Count == 0, "main roads that just stop: " + string.Join("  ", loose));
    }

    [Fact]
    public void TheCountryRoundTheCityIsRealToo()
    {
        // The toll roads: Jagorawi down from Jakarta, the outer ring, and Bocimi on to Sukabumi.
        float toll = _map.Roads.Edges.Where(e => e.Kind == RoadKind.Toll).Sum(e => e.Length);
        Assert.True(toll > 60_000f, $"{toll / 1000f:0} km of toll road");
        foreach (string name in (string[])["road.jagorawi", "road.borr", "road.bocimi", "road.puncak", "road.dramaga", "road.jalanbaru", "road.sirkuit"])
        {
            Assert.Contains(_map.Roads.Edges, e => e.NameKey == name);
        }

        Assert.Contains(_map.Roads.Edges, e => e.NameKey == "road.jagorawi" && e.Kind == RoadKind.Toll && e.Points[0].Y < -15_000f);
        Assert.Contains(_map.Roads.Edges, e => e.NameKey == "road.puncak" && e.Kind == RoadKind.Mountain);

        // Dramaga, Sentul and Cisarua have their own streets, and those are not a lattice.
        foreach (DistrictId id in (DistrictId[])[DistrictId.Dramaga, DistrictId.Sentul, DistrictId.Puncak])
        {
            District district = _map.Districts.First(d => d.Id == id);
            List<RoadEdge> inside = [.. _map.Roads.Edges.Where(e => Vector2.Distance(e.Points[e.Points.Length / 2], district.Center) < district.Radius)];
            Assert.True(inside.Count > 150, $"{id}: {inside.Count} streets");
            Assert.True(inside.Count(e => e.Points.Length > 2) > 60, $"{id}: the streets are all straight");
        }

        // The Cisadane and the line to Sukabumi.
        Corridor cisadane = _map.Corridors.Single(c => c.NameKey == "river.cisadane");
        Assert.Equal(CorridorKind.River, cisadane.Kind);
        Assert.True(cisadane.Length > 40_000f);
        Corridor south = _map.Corridors.Single(c => c.NameKey == "rail.sukabumi");
        Assert.True(Vector2.Distance(south.Points[0], new Vector2(-1_591f, -395f)) < 200f, "the line does not start at Paledang");
        Assert.True(south.Points[^1].Y > 19_000f, "the line does not reach the southern edge");
        // The commuter line is still the one with the train on it.
        Assert.Equal("rail.krl", _map.Railway!.NameKey);
        // The Sukabumi line gets over the Cisadane on a bridge of its own.
        Assert.Contains(_map.Bridges, b => !_map.Roads.IsOnRoad(b.Path[b.Path.Length / 2]) && south.Closest(b.Path[b.Path.Length / 2], 3f, out _, out _, out _));
        // Roads cross the Cisadane on bridges as well.
        Assert.True(_map.Bridges.Count(b => cisadane.Closest(b.Path[b.Path.Length / 2], 30f, out _, out _, out _)) >= 8);

        // Nobody is sent to a place on a toll road, and no road runs through a lake.
        foreach (Poi poi in _map.Pois)
        {
            Assert.True(_map.Roads.Nearest(poi.RoadPoint, 3f, e => e.Kind != RoadKind.Toll) is not null, $"{poi.Id} is not by an ordinary road");
        }

        foreach (Pond pond in _map.Ponds)
        {
            Assert.True(_map.Roads.Nearest(pond.Center, pond.Radius) is null, $"a road runs through {pond.NameKey}");
        }

        // Situ Gede is where it really is: north-east of the campus.
        Assert.True(Vector2.Distance(_map[PoiId.SituGede].Position, new Vector2(-6_290f, -5_400f)) < 200f);
    }

    [Fact]
    public void NobodySinksIntoARoad()
    {
        // A road is drawn flat from kerb to kerb at the height of its highest side, so across a
        // slope it stands above the ground. Whoever is on it must stand on the road, not in it.
        int sections = 0;
        int builtUp = 0;
        foreach (RoadEdge edge in _map.Roads.Edges)
        {
            float half = edge.Width * 0.5f;
            for (int i = 0; i < edge.Points.Length; i += 2)
            {
                Vector2 c = edge.Points[i];
                Vector2 side = BarudakBogor.Numerics.MathUtil.Left(edge.Tangent(i)) * (half - 0.3f);
                float surface = edge.SurfaceAt(edge.DistanceAtVertex(i));
                foreach (Vector2 p in (Vector2[])[c, c + side, c - side])
                {
                    float feet = _map.Terrain.HeightAt(p);
                    Assert.True(feet > surface - 0.08f, $"at {p.X:0},{p.Y:0} one stands {surface - feet:0.00} m deep in the {edge.Kind}");
                    if (_map.Terrain.GroundAt(p.X, p.Y) < surface - 0.3f)
                    {
                        builtUp++;
                    }
                }

                sections++;
            }
        }

        Assert.True(sections > 30_000);
        // The hill roads really are built up in places: the check above was not an empty one.
        Assert.True(builtUp > 500, $"{builtUp} built-up spots");
    }

    [Fact]
    public void TheMainStreetsAreThereByName()
    {
        string[] names = ["road.pajajaran", "road.juanda", "road.otista", "road.jalakharupat", "road.sudirman", "road.suryakencana", "road.muslihat", "road.pahlawan"];
        foreach (string name in names)
        {
            Assert.Contains(_map.Roads.Edges, e => e.NameKey == name);
        }

        // The loop round the Gardens is about four kilometres, as the real one is (parts of it are dual carriageway).
        float loop = _map.Roads.Edges.Where(e => e.NameKey is "road.juanda" or "road.otista" or "road.jalakharupat").Sum(e => e.Length);
        Assert.InRange(loop, 2_300f, 4_500f);

        // The river runs through the Gardens and the railway ends at the station.
        Corridor river = _map.Corridors.Single(c => c.NameKey == "river.ciliwung");
        Assert.True(river.Closest(new Vector2(-500f, -400f), 150f, out _, out _, out _));
        Assert.True(Vector2.Distance(_map.Railway!.Points[0], _map[PoiId.StasiunBogor].Position) < 120f);
        Assert.True(_map.Bridges.Count >= 5 && _map.Crossings.Count >= 2, $"{_map.Bridges.Count} bridges, {_map.Crossings.Count} level crossings");
        Assert.True(_map.Buildings.Count > 20_000, $"{_map.Buildings.Count} buildings");
    }
}
