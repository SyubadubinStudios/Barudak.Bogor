using System.Numerics;
using BarudakBogor.Numerics;
using BarudakBogor.World;

namespace BarudakBogor.Tests;

/// <summary>One generated map shared by every test class: generation is deterministic and read-only.</summary>
public sealed class WorldFixture
{
    public WorldMap Map { get; } = WorldMap.Generate();
}

[CollectionDefinition("world")]
public sealed class WorldCollection : ICollectionFixture<WorldFixture>;

[Collection("world")]
public sealed class WorldTests(WorldFixture fixture)
{
    private readonly WorldMap _map = fixture.Map;

    [Fact]
    public void GenerationIsDeterministic()
    {
        WorldMap other = WorldMap.Generate();
        Assert.Equal(_map.Buildings.Count, other.Buildings.Count);
        Assert.Equal(_map.Roads.Edges.Count, other.Roads.Edges.Count);
        Assert.Equal(_map.Buildings[100], other.Buildings[100]);
        Assert.Equal(_map.Terrain.HeightAt(1234f, -987f), other.Terrain.HeightAt(1234f, -987f));
    }

    [Fact]
    public void MapHasAllEightAreasAndEveryPlace()
    {
        foreach (DistrictId id in Enum.GetValues<DistrictId>().Where(d => d != DistrictId.Rural))
        {
            Assert.Contains(_map.Districts, d => d.Id == id);
        }

        foreach (PoiId id in Enum.GetValues<PoiId>())
        {
            Poi poi = _map[id];
            Assert.InRange(MathF.Abs(poi.Position.X), 0f, Terrain.HalfSize);
            Assert.InRange(MathF.Abs(poi.Position.Y), 0f, Terrain.HalfSize);
        }
    }

    [Fact]
    public void WorldIsSubstantial()
    {
        Assert.True(_map.Buildings.Count > 5_000, $"only {_map.Buildings.Count} buildings");
        Assert.True(_map.Roads.Edges.Sum(e => e.Length) > 150_000f, "less than 150 km of road");
    }

    [Theory]
    [InlineData(PoiId.TuguKujang, PoiId.KampusIpb)]
    [InlineData(PoiId.TuguKujang, PoiId.SirkuitSentul)]
    [InlineData(PoiId.TuguKujang, PoiId.PuncakPass)]
    [InlineData(PoiId.TerminalDramaga, PoiId.JalanBaru)]
    [InlineData(PoiId.GangSelot, PoiId.VillaMisterius)]
    public void PlacesAreConnectedByRoad(PoiId from, PoiId to)
    {
        RoadNode a = _map.Roads.NearestNode(_map[from].RoadPoint);
        RoadNode b = _map.Roads.NearestNode(_map[to].RoadPoint);
        Assert.NotEmpty(_map.Roads.FindRoute(a.Id, b.Id));
    }

    [Fact]
    public void BuildingsDoNotStandOnRoads()
    {
        int checkedCount = 0;
        for (int i = 0; i < _map.Buildings.Count; i += 7)
        {
            Assert.False(_map.Roads.IsOnRoad(_map.Buildings[i].Center), $"building {i} is on a road");
            checkedCount++;
        }

        Assert.True(checkedCount > 500);
    }

    [Fact]
    public void PuncakIsHighAndTheCityIsLow()
    {
        float city = _map.Terrain.HeightAt(_map[PoiId.TuguKujang].Position);
        float puncak = _map.Terrain.HeightAt(_map[PoiId.PuncakPass].Position);
        Assert.InRange(city, 200f, 330f);
        Assert.True(puncak > 900f, $"Puncak Pass is only at {puncak} m");
    }

    [Fact]
    public void TerrainIsContinuousAcrossLatticeCells()
    {
        // Sampling just either side of a lattice line must agree: no cracks between chunks.
        float a = _map.Terrain.HeightAt(Terrain.Cell * 10f - 0.001f, 33f);
        float b = _map.Terrain.HeightAt(Terrain.Cell * 10f + 0.001f, 33f);
        Assert.InRange(MathF.Abs(a - b), 0f, 0.01f);
    }

    [Fact]
    public void CollisionPushesOutOfBuildings()
    {
        Building building = _map.Buildings[42];
        Vector2 resolved = _map.ResolveCircle(building.Center, 0.4f, out Vector2 push);
        Assert.NotEqual(Vector2.Zero, push);
        Assert.False(_map.IsInsideBuilding(resolved));
    }

    [Fact]
    public void TreesAreDeterministicAndOffTheRoad()
    {
        (int x, int z) = WorldMap.ChunkOf(_map.Districts.First(d => d.Id == DistrictId.KebunRaya).Center);
        Tree[] first = _map.TreesInChunk(x, z);
        Tree[] again = WorldGenerator.ScatterTrees(_map, x, z);
        Assert.NotEmpty(first);
        Assert.Equal(first.Length, again.Length);
        Assert.All(first, tree => Assert.False(_map.Roads.IsOnRoad(tree.Position)));
    }

    [Fact]
    public void HashAndRngAreStable()
    {
        Assert.Equal(Hash.Of(3, 4, 5), Hash.Of(3, 4, 5));
        Assert.NotEqual(Hash.Of(3, 4, 5), Hash.Of(4, 3, 5));
        Rng a = new(99);
        Rng b = new(99);
        for (int i = 0; i < 20; i++)
        {
            float value = a.NextFloat();
            Assert.Equal(value, b.NextFloat());
            Assert.InRange(value, 0f, 1f);
        }
    }
}
