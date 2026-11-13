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
                && _map.Districts.All(d => d.Id == DistrictId.PusatKota || Vector2.Distance(d.Center, node.Position) > d.Radius))
            {
                loose.Add($"{node.Position.X:0},{node.Position.Y:0}");
            }
        }

        Assert.True(loose.Count == 0, "main roads that just stop: " + string.Join("  ", loose));
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
        Corridor river = _map.Corridors.Single(c => c.Kind == CorridorKind.River);
        Assert.True(river.Closest(new Vector2(-500f, -400f), 150f, out _, out _, out _));
        Assert.True(Vector2.Distance(_map.Railway!.Points[0], _map[PoiId.StasiunBogor].Position) < 120f);
        Assert.True(_map.Bridges.Count >= 5 && _map.Crossings.Count >= 2, $"{_map.Bridges.Count} bridges, {_map.Crossings.Count} level crossings");
        Assert.True(_map.Buildings.Count > 20_000, $"{_map.Buildings.Count} buildings");
    }
}
