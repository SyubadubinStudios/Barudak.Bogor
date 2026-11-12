using System.Numerics;
using BarudakBogor.Numerics;

namespace BarudakBogor.World;

/// <summary>
/// Builds the map from a seed. The layout follows the real geography loosely: Tugu Kujang is
/// the origin, +X is east and +Z is south, so Dramaga lies to the west, Sentul to the
/// north-east and Puncak far to the south-east.
/// </summary>
internal static class WorldGenerator
{
    private static readonly Vector2 ParkCenter = new(-560f, -220f);
    private const float ParkRadius = 560f;
    private const float RingRadius = 600f;

    public static WorldMap Generate(uint seed)
    {
        District[] districts =
        [
            new(DistrictId.PusatKota, "district.pusatkota", new Vector2(-500f, -300f), 1_900f, LandUse.Kota),
            new(DistrictId.Dramaga, "district.dramaga", new Vector2(-9_000f, 300f), 900f, LandUse.Kampus),
            new(DistrictId.Sentul, "district.sentul", new Vector2(5_500f, -7_500f), 1_000f, LandUse.Modern),
            new(DistrictId.Puncak, "district.puncak", new Vector2(12_800f, 10_800f), 1_100f, LandUse.Villa),
            new(DistrictId.KebunRaya, "district.kebunraya", ParkCenter, ParkRadius, LandUse.Taman),
            new(DistrictId.Empang, "district.empang", new Vector2(-1_250f, 950f), 420f, LandUse.Kampung),
            new(DistrictId.PasarAnyar, "district.pasaranyar", new Vector2(-900f, -1_500f), 340f, LandUse.Pasar),
            new(DistrictId.GangSelot, "district.gangselot", new Vector2(-1_650f, -780f), 230f, LandUse.Gang),
        ];
        Pond[] ponds =
        [
            new("pond.empang", new Vector2(-1_330f, 1_030f), 62f, 2.2f),
            new("pond.situgede", new Vector2(-8_350f, -520f), 150f, 3f),
            new("pond.telagawarna", new Vector2(13_650f, 11_650f), 90f, 4f),
        ];

        WorldMap map = new(seed, districts, ponds);
        map.SetPuncakCenter(districts[3].Center);
        Rng rng = new(seed * 7919u);

        BuildRoads(map, ref rng, out Dictionary<DistrictId, List<RoadNode>> grids, out List<RoadNode> ring);
        PlacePois(map, ring);
        PlaceLandmarks(map, ring);
        PlaceBuildings(map, ref rng);
        PlaceInfill(map, ref rng);
        PlaceStreetFurniture(map, grids);
        return map;
    }

    // ------------------------------------------------------------------ roads

    private static void BuildRoads(WorldMap map, ref Rng rng, out Dictionary<DistrictId, List<RoadNode>> grids, out List<RoadNode> ring)
    {
        RoadNetwork roads = map.Roads;
        Dictionary<DistrictId, List<RoadNode>> gridMap = [];
        grids = gridMap;

        // The one-way loop around Kebun Raya, the best known road of the city.
        ring = [];
        const int ringCount = 20;
        for (int i = 0; i < ringCount; i++)
        {
            // Start so that one node lands near the origin (Tugu Kujang sits at the south-east corner).
            float angle = MathF.Atan2(-ParkCenter.Y, -ParkCenter.X) + (i * MathUtil.Tau / ringCount);
            ring.Add(roads.AddNode(ParkCenter + (new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * RingRadius)));
        }

        for (int i = 0; i < ringCount; i++)
        {
            roads.AddEdge(ring[i], ring[(i + 1) % ringCount], RoadKind.Arterial, "road.lingkarkebunraya");
        }

        District gang = map.Districts.First(d => d.Id == DistrictId.GangSelot);
        List<RoadNode> core = BuildGrid(map, ref rng, map.Districts[0], 150f, 18f, RoadKind.Street, 4, 0.08f,
            p => Vector2.Distance(p, ParkCenter) < RingRadius + 95f || Vector2.Distance(p, gang.Center) < gang.Radius + 30f);
        gridMap[DistrictId.PusatKota] = core;
        foreach (RoadNode node in ring)
        {
            RoadNode nearest = NearestOf(core, node.Position);
            if (Vector2.Distance(nearest.Position, node.Position) < 260f)
            {
                roads.AddEdge(node, nearest, RoadKind.Street, "road.kota");
            }
        }

        // Gang Selot: a tight alley maze hanging off the city grid.
        List<RoadNode> alleys = BuildGrid(map, ref rng, gang, 46f, 7f, RoadKind.Gang, 0, 0.12f, _ => false);
        gridMap[DistrictId.GangSelot] = alleys;
        int links = 0;
        foreach (RoadNode node in alleys)
        {
            if (Vector2.Distance(node.Position, gang.Center) > gang.Radius - 50f && rng.Chance(0.45f))
            {
                RoadNode nearest = NearestOf(core, node.Position);
                if (Vector2.Distance(nearest.Position, node.Position) < 220f)
                {
                    roads.AddEdge(node, nearest, RoadKind.Gang, "road.gangselot");
                    links++;
                }
            }
        }

        if (links == 0)
        {
            roads.AddEdge(alleys[0], NearestOf(core, alleys[0].Position), RoadKind.Gang, "road.gangselot");
        }

        gridMap[DistrictId.Dramaga] = BuildGrid(map, ref rng, map.Districts[1], 170f, 22f, RoadKind.Street, 3, 0.12f, _ => false);
        gridMap[DistrictId.Sentul] = BuildGrid(map, ref rng, map.Districts[2], 220f, 12f, RoadKind.Street, 2, 0.05f, _ => false);
        gridMap[DistrictId.Puncak] = BuildGrid(map, ref rng, map.Districts[3], 260f, 45f, RoadKind.Mountain, 0, 0.25f, _ => false);

        RoadNode jalanBaru = roads.AddNode(new Vector2(-2_300f, -4_600f));
        RoadNode ciawi = roads.AddNode(new Vector2(5_200f, 5_600f));
        RoadNode gadog = roads.AddNode(new Vector2(8_200f, 7_800f));

        RoadNode Gateway(DistrictId id, Vector2 toward)
        {
            District district = map.Districts.First(d => d.Id == id);
            Vector2 direction = Vector2.Normalize(toward - district.Center);
            return NearestOf(gridMap[id], district.Center + (direction * district.Radius * 0.93f));
        }

        void Arterial(RoadNode from, RoadNode to, RoadKind kind, string name, params Vector2[] via)
        {
            Vector2[] control = [from.Position, .. via, to.Position];
            roads.AddEdge(from, to, RoadNetwork.Spline(control), kind, name);
        }

        Arterial(Gateway(DistrictId.PusatKota, new Vector2(-9_000f, 0f)), Gateway(DistrictId.Dramaga, new Vector2(0f, 0f)),
            RoadKind.Arterial, "road.dramaga", new(-3_600f, -150f), new(-5_500f, 320f), new(-7_000f, 120f));
        Arterial(Gateway(DistrictId.PusatKota, jalanBaru.Position), jalanBaru,
            RoadKind.Arterial, "road.jalanbaru", new Vector2(-1_700f, -3_300f));
        Arterial(jalanBaru, Gateway(DistrictId.Dramaga, new Vector2(-8_000f, -4_000f)),
            RoadKind.Arterial, "road.jalanbaru", new(-4_500f, -4_250f), new(-6_800f, -2_700f));
        Arterial(jalanBaru, Gateway(DistrictId.Sentul, new Vector2(0f, -7_000f)),
            RoadKind.Arterial, "road.jalanbaru", new(0f, -5_300f), new(2_500f, -6_300f));
        Arterial(Gateway(DistrictId.PusatKota, new Vector2(4_000f, -1_500f)), Gateway(DistrictId.Sentul, new Vector2(5_000f, 0f)),
            RoadKind.Toll, "road.jagorawi", new(2_600f, -1_700f), new(3_900f, -4_000f), new(4_900f, -5_600f));
        Arterial(Gateway(DistrictId.PusatKota, ciawi.Position), ciawi,
            RoadKind.Arterial, "road.tajur", new(2_400f, 2_500f), new(3_900f, 4_200f));
        Arterial(ciawi, gadog, RoadKind.Arterial, "road.puncak", new Vector2(6_700f, 6_600f));

        // Jalan Raya Puncak: the climb is a string of hairpins.
        RoadNode puncakGate = Gateway(DistrictId.Puncak, gadog.Position);
        List<Vector2> hairpins = [];
        Vector2 along = puncakGate.Position - gadog.Position;
        Vector2 side = Vector2.Normalize(MathUtil.Left(along));
        const int bends = 9;
        for (int i = 1; i < bends; i++)
        {
            float t = i / (float)bends;
            hairpins.Add(gadog.Position + (along * t) + (side * ((i % 2 == 0 ? 1f : -1f) * 190f)));
        }

        Arterial(gadog, puncakGate, RoadKind.Mountain, "road.puncak", [.. hairpins]);
    }

    /// <summary>
    /// Jittered street lattice inside a district. Every <paramref name="arterialEvery"/>-th line is
    /// an arterial; a fraction of the lesser streets is dropped so blocks are not uniform.
    /// </summary>
    private static List<RoadNode> BuildGrid(
        WorldMap map, ref Rng rng, District district, float spacing, float jitter, RoadKind kind, int arterialEvery, float dropRate, Func<Vector2, bool> excluded)
    {
        RoadNetwork roads = map.Roads;
        int half = (int)(district.Radius / spacing);
        int size = (half * 2) + 1;
        RoadNode?[,] lattice = new RoadNode?[size, size];
        List<RoadNode> nodes = [];
        for (int j = 0; j < size; j++)
        {
            for (int i = 0; i < size; i++)
            {
                Vector2 p = district.Center + new Vector2((i - half) * spacing, (j - half) * spacing);
                p += new Vector2(rng.Range(-jitter, jitter), rng.Range(-jitter, jitter));
                if (Vector2.Distance(p, district.Center) > district.Radius || excluded(p) || NearPond(map, p, 40f))
                {
                    continue;
                }

                RoadNode node = roads.AddNode(p);
                lattice[i, j] = node;
                nodes.Add(node);
            }
        }

        string name = "road." + district.Id.ToString().ToLowerInvariant();
        for (int j = 0; j < size; j++)
        {
            for (int i = 0; i < size; i++)
            {
                if (lattice[i, j] is not { } node)
                {
                    continue;
                }

                // Lines through the district centre are always kept so the grid stays connected.
                bool mainRow = arterialEvery > 0 && (j - half) % arterialEvery == 0;
                bool mainColumn = arterialEvery > 0 && (i - half) % arterialEvery == 0;
                if (i + 1 < size && lattice[i + 1, j] is { } east && (mainRow || j == half || !rng.Chance(dropRate)))
                {
                    roads.AddEdge(node, east, mainRow ? RoadKind.Arterial : kind, name);
                }

                if (j + 1 < size && lattice[i, j + 1] is { } south && (mainColumn || i == half || !rng.Chance(dropRate)))
                {
                    roads.AddEdge(node, south, mainColumn ? RoadKind.Arterial : kind, name);
                }
            }
        }

        return nodes;
    }

    // ------------------------------------------------------------------ places

    private static void PlacePois(WorldMap map, List<RoadNode> ring)
    {
        // Tugu Kujang stands just outside the ring road at its south-east corner.
        RoadNode kujangNode = NearestOf(ring, Vector2.Zero);
        Vector2 outward = Vector2.Normalize(kujangNode.Position - ParkCenter);
        AddPoi(map, PoiId.TuguKujang, "poi.tugukujang", kujangNode.Position + (outward * 22f), kujangNode.Position, MathUtil.YawOf(-outward), 20f);

        RoadNode gateNode = NearestOf(ring, ParkCenter + new Vector2(120f, -RingRadius));
        Vector2 inward = Vector2.Normalize(ParkCenter - gateNode.Position);
        AddPoi(map, PoiId.GerbangKebunRaya, "poi.gerbangkebunraya", gateNode.Position + (inward * 16f), gateNode.Position, MathUtil.YawOf(-inward), 12f);
        AddPoi(map, PoiId.IstanaRusa, "poi.istanarusa", ParkCenter + new Vector2(-120f, -160f), gateNode.Position, 0f, 0f);

        Roadside(map, PoiId.PasarAnyar, "poi.pasaranyar", new Vector2(-900f, -1_500f), 14f);
        Roadside(map, PoiId.GangSelot, "poi.gangselot", new Vector2(-1_650f, -780f), 5f);
        Shore(map, PoiId.Empang, "poi.empang", map.Ponds[0]);
        Roadside(map, PoiId.RumahUjang, "poi.rumahujang", new Vector2(-1_120f, 760f), 10f);
        Roadside(map, PoiId.WarungBandrek, "poi.warungbandrek", new Vector2(-260f, 420f), 9f);
        Roadside(map, PoiId.LapanganBadminton, "poi.badminton", new Vector2(-1_480f, 1_180f), 14f);
        Roadside(map, PoiId.Karaoke, "poi.karaoke", new Vector2(420f, -820f), 10f);
        Roadside(map, PoiId.SekolahBinaInsani, "poi.binainsani", new Vector2(-1_250f, -2_050f), 16f);
        Roadside(map, PoiId.LapanganStm, "poi.lapanganstm", new Vector2(650f, 620f), 18f);
        Roadside(map, PoiId.TerminalDramaga, "poi.terminaldramaga", new Vector2(-8_300f, 320f), 14f);
        Roadside(map, PoiId.KampusIpb, "poi.kampusipb", new Vector2(-9_000f, 300f), 16f);
        Shore(map, PoiId.SituGede, "poi.situgede", map.Ponds[1]);
        Roadside(map, PoiId.JalanBaru, "poi.jalanbaru", new Vector2(-2_300f, -4_600f), 12f);
        Roadside(map, PoiId.SirkuitSentul, "poi.sirkuitsentul", new Vector2(5_500f, -7_500f), 16f);
        Roadside(map, PoiId.Ciawi, "poi.ciawi", new Vector2(5_200f, 5_600f), 12f);
        Roadside(map, PoiId.PuncakPass, "poi.puncakpass", new Vector2(12_800f, 10_800f), 14f);
        Roadside(map, PoiId.VillaMisterius, "poi.villamisterius", new Vector2(13_300f, 10_250f), 22f);
        Roadside(map, PoiId.KebunTeh, "poi.kebunteh", new Vector2(12_100f, 10_300f), 12f);
    }

    /// <summary>Snaps a wished-for location to the nearest road and puts the place on its left kerb.</summary>
    private static void Roadside(WorldMap map, PoiId id, string nameKey, Vector2 wish, float clearRadius)
    {
        RoadHit hit = map.Roads.Nearest(wish, 900f) ?? throw new InvalidOperationException($"No road near {id}.");
        Vector2 left = MathUtil.Left(hit.Direction);
        // Choose the side the wish was on, so a pond-side place stays by its pond.
        float sign = Vector2.Dot(wish - hit.Point, left) >= 0f ? 1f : -1f;
        Vector2 position = hit.Point + (left * sign * ((hit.Edge.Width * 0.5f) + 2.5f + clearRadius));
        AddPoi(map, id, nameKey, position, hit.Point, MathUtil.YawOf(-left * sign), clearRadius);
    }

    /// <summary>A place on the bank of a pond, on the side facing the nearest road.</summary>
    private static void Shore(WorldMap map, PoiId id, string nameKey, Pond pond)
    {
        RoadHit hit = map.Roads.Nearest(pond.Center, 900f) ?? throw new InvalidOperationException($"No road near {id}.");
        Vector2 toRoad = Vector2.Normalize(hit.Point - pond.Center);
        Vector2 position = pond.Center + (toRoad * (pond.Radius + 3.5f));
        // Facing the water, so the hut and the angler look out over the pond.
        AddPoi(map, id, nameKey, position, hit.Point, MathUtil.YawOf(-toRoad), 7f);
    }

    private static void AddPoi(WorldMap map, PoiId id, string nameKey, Vector2 position, Vector2 roadPoint, float yaw, float clearRadius)
    {
        map.AddPoi(new Poi(id, nameKey, position, roadPoint, yaw));
        if (clearRadius > 0f)
        {
            map.Reserved.Add((position, clearRadius));
        }
    }

    private static void PlaceLandmarks(WorldMap map, List<RoadNode> ring)
    {
        void At(PoiId id, string model, float radius, float scale = 1f, float yawOffset = 0f, Vector2 offset = default)
        {
            Poi poi = map[id];
            map.AddProp(new Prop(model, poi.Position + offset, poi.Yaw + yawOffset, scale, radius));
        }

        At(PoiId.TuguKujang, "tugu_kujang", 5f);
        At(PoiId.GerbangKebunRaya, "gerbang_kebun_raya", 0f);
        At(PoiId.VillaMisterius, "villa_puncak", 7f, 1.25f);
        At(PoiId.Empang, "saung_pancing", 1.6f, 1f, 0f, MathUtil.Left(MathUtil.Forward(map[PoiId.Empang].Yaw)) * 7f);
        At(PoiId.SituGede, "saung_pancing", 1.6f, 1f, 0f, MathUtil.Left(MathUtil.Forward(map[PoiId.SituGede].Yaw)) * 7f);
        At(PoiId.WarungBandrek, "warung", 1.8f);
        At(PoiId.WarungBandrek, "gerobak_bandrek", 0.8f, 1f, 0f, new Vector2(3.5f, 1f));
        At(PoiId.Karaoke, "warung", 1.8f, 1.4f);
        At(PoiId.PuncakPass, "warung", 1.8f, 1.2f);
        At(PoiId.KebunTeh, "pos_ronda", 1.2f);
        At(PoiId.LapanganBadminton, "pos_ronda", 1.2f, 1f, 0f, new Vector2(9f, 0f));
        At(PoiId.TerminalDramaga, "halte", 1.4f, 1.3f);
        At(PoiId.JalanBaru, "halte", 1.4f);
        At(PoiId.SekolahBinaInsani, "halte", 1.4f);
        At(PoiId.KampusIpb, "papan_reklame", 0.5f);
        At(PoiId.SirkuitSentul, "papan_reklame", 0.5f, 1.3f);
        At(PoiId.Ciawi, "papan_reklame", 0.5f);
        At(PoiId.GangSelot, "gapura_gang", 0f);
        At(PoiId.PasarAnyar, "tenda_pasar", 1.4f);

        // Market tents crowd the kerbs of Pasar Anyar.
        District pasar = map.Districts.First(d => d.Id == DistrictId.PasarAnyar);
        Rng rng = new(map.Seed + 4242u);
        foreach (RoadEdge edge in map.Roads.Edges)
        {
            Vector2 mid = edge.Sample(edge.Length * 0.5f, out _);
            if (Vector2.Distance(mid, pasar.Center) > pasar.Radius)
            {
                continue;
            }

            for (float t = 18f; t < edge.Length - 18f; t += 9f)
            {
                if (!rng.Chance(0.55f))
                {
                    continue;
                }

                Vector2 point = edge.Sample(t, out Vector2 direction);
                float sign = rng.Chance(0.5f) ? 1f : -1f;
                Vector2 left = MathUtil.Left(direction) * sign;
                Vector2 position = point + (left * ((edge.Width * 0.5f) + 1.6f));
                map.AddProp(new Prop("tenda_pasar", position, MathUtil.YawOf(-left), rng.Range(0.85f, 1.05f), 1.3f));
                map.Reserved.Add((position, 2.2f));
            }
        }

        // Haltes along the ring road.
        for (int i = 2; i < ring.Count; i += 5)
        {
            Vector2 outward = Vector2.Normalize(ring[i].Position - ParkCenter);
            Vector2 position = ring[i].Position + (outward * 8.5f) + (MathUtil.Left(outward) * 14f);
            map.AddProp(new Prop("halte", position, MathUtil.YawOf(-outward), 1f, 1.4f));
            map.Reserved.Add((position, 4f));
        }
    }

    // ------------------------------------------------------------------ buildings

    private static void PlaceBuildings(WorldMap map, ref Rng rng)
    {
        foreach (RoadEdge edge in map.Roads.Edges)
        {
            if (edge.Kind == RoadKind.Toll)
            {
                continue;
            }

            for (int sideIndex = 0; sideIndex < 2; sideIndex++)
            {
                float sign = sideIndex == 0 ? 1f : -1f;
                float margin = edge.Kind == RoadKind.Gang ? 4f : 11f;
                float t = margin;
                while (t < edge.Length - margin)
                {
                    Vector2 point = edge.Sample(t, out Vector2 direction);
                    LandUse use = map.LandUseAt(point);
                    (float density, float gap) = Density(use, edge.Kind);
                    BuildingStyle style = PickStyle(use, ref rng, point, map);
                    (float width, float depth, float height, float setback) = Dimensions(style, ref rng);
                    if (!rng.Chance(density))
                    {
                        t += width + gap;
                        continue;
                    }

                    Vector2 centreOnRoad = edge.Sample(t + (width * 0.5f), out direction);
                    Vector2 left = MathUtil.Left(direction) * sign;
                    float kerb = (edge.Width * 0.5f) + (edge.Kind == RoadKind.Gang ? 0.4f : 2.4f) + setback;
                    Vector2 centre = centreOnRoad + (left * (kerb + (depth * 0.5f)));
                    Building building = new(centre, width * 0.5f, depth * 0.5f, MathUtil.YawOf(left), height, style, rng.NextUInt(), 0f);
                    if (CanPlace(map, building, edge))
                    {
                        map.AddBuilding(building with { BaseY = FoundationHeight(map, building) });
                    }

                    t += width + gap;
                }
            }
        }
    }

    /// <summary>
    /// Fills the inside of city blocks with houses behind the street-front row, so town reads
    /// as dense kampung rather than as roads through empty lots.
    /// </summary>
    private static void PlaceInfill(WorldMap map, ref Rng rng)
    {
        const float spacing = 17f;
        foreach (District district in map.Districts)
        {
            float fill = district.Use switch
            {
                LandUse.Kota => 0.62f,
                LandUse.Pasar => 0.7f,
                LandUse.Kampung => 0.7f,
                LandUse.Gang => 0.8f,
                LandUse.Kampus => 0.22f,
                LandUse.Modern => 0.3f,
                _ => 0f,
            };
            if (fill <= 0f)
            {
                continue;
            }

            int half = (int)(district.Radius / spacing);
            for (int j = -half; j <= half; j++)
            {
                for (int i = -half; i <= half; i++)
                {
                    Vector2 p = district.Center + new Vector2(i * spacing, j * spacing) + new Vector2(rng.Range(-3f, 3f), rng.Range(-3f, 3f));
                    // Smaller districts lie inside the city; let each point be handled by its own district only.
                    if (!rng.Chance(fill) || map.DistrictAt(p) != district || map.Roads.Nearest(p, 120f) is not { } road)
                    {
                        continue;
                    }

                    float roll = rng.NextFloat();
                    BuildingStyle style = district.Use is LandUse.Modern or LandUse.Kampus
                        ? BuildingStyle.Rumah
                        : roll < 0.62f ? BuildingStyle.Kampung : roll < 0.84f ? BuildingStyle.Rumah : BuildingStyle.Ruko;
                    (float width, float depth, float height, _) = Dimensions(style, ref rng);
                    // Face the nearest street, like every house in a kampung does.
                    Vector2 toRoad = road.Point - p;
                    float yaw = toRoad.LengthSquared() > 1f ? MathUtil.YawOf(-Vector2.Normalize(toRoad)) : 0f;
                    Building building = new(p, width * 0.5f, depth * 0.5f, yaw, height, style, rng.NextUInt(), 0f);
                    if (CanPlace(map, building, null))
                    {
                        map.AddBuilding(building with { BaseY = FoundationHeight(map, building) });
                    }
                }
            }
        }
    }

    private static (float Density, float Gap) Density(LandUse use, RoadKind road) => use switch
    {
        LandUse.Kota => (0.92f, 0.3f),
        LandUse.Pasar => (0.95f, 0f),
        LandUse.Kampung => (0.9f, 1f),
        LandUse.Gang => (0.96f, 0.3f),
        LandUse.Kampus => (0.7f, 6f),
        LandUse.Modern => (0.75f, 5f),
        LandUse.Villa => (0.4f, 14f),
        LandUse.Taman => (0f, 20f),
        LandUse.KebunTeh => (0.05f, 30f),
        LandUse.Hutan => (0.06f, 30f),
        _ => (road == RoadKind.Arterial ? 0.24f : 0.12f, 16f),
    };

    private static BuildingStyle PickStyle(LandUse use, ref Rng rng, Vector2 p, WorldMap map)
    {
        float roll = rng.NextFloat();
        switch (use)
        {
            case LandUse.Kota:
                return roll < 0.52f ? BuildingStyle.Ruko : roll < 0.72f ? BuildingStyle.Kantor : roll < 0.96f ? BuildingStyle.Rumah : BuildingStyle.Mall;
            case LandUse.Pasar:
                return roll < 0.45f ? BuildingStyle.Kios : BuildingStyle.Ruko;
            case LandUse.Kampung:
                return roll < 0.7f ? BuildingStyle.Kampung : roll < 0.9f ? BuildingStyle.Rumah : BuildingStyle.Ruko;
            case LandUse.Gang:
                return BuildingStyle.Kampung;
            case LandUse.Kampus:
                District campus = map.Districts.First(d => d.Id == DistrictId.Dramaga);
                bool core = Vector2.Distance(p, campus.Center) < 380f;
                return core
                    ? (roll < 0.7f ? BuildingStyle.Kampus : BuildingStyle.Ruko)
                    : (roll < 0.45f ? BuildingStyle.Rumah : roll < 0.8f ? BuildingStyle.Kampung : BuildingStyle.Ruko);
            case LandUse.Modern:
                return roll < 0.5f ? BuildingStyle.Rumah : roll < 0.7f ? BuildingStyle.Ruko : roll < 0.92f ? BuildingStyle.Kantor : BuildingStyle.Mall;
            case LandUse.Villa:
                return roll < 0.6f ? BuildingStyle.Villa : roll < 0.8f ? BuildingStyle.Rumah : BuildingStyle.Kios;
            default:
                return roll < 0.45f ? BuildingStyle.Rumah : roll < 0.8f ? BuildingStyle.Kampung : BuildingStyle.Kios;
        }
    }

    private static (float Width, float Depth, float Height, float Setback) Dimensions(BuildingStyle style, ref Rng rng) => style switch
    {
        BuildingStyle.Ruko => (rng.Range(5f, 6.5f), 12f, 3.6f * rng.Range(2, 4), 0.4f),
        BuildingStyle.Rumah => (rng.Range(9f, 12f), rng.Range(9f, 11f), 3.6f, 3f),
        BuildingStyle.Kios => (rng.Range(3f, 4.2f), 4f, 3f, 0.2f),
        BuildingStyle.Kantor => (rng.Range(18f, 26f), rng.Range(15f, 20f), 3.6f * rng.Range(4, 10), 6f),
        BuildingStyle.Kampung => (rng.Range(6f, 8f), rng.Range(6f, 7.5f), 3.2f, 0.3f),
        BuildingStyle.Kampus => (rng.Range(30f, 44f), 18f, 3.8f * rng.Range(3, 5), 8f),
        BuildingStyle.Villa => (rng.Range(12f, 16f), 10f, 6.6f, 8f),
        _ => (rng.Range(50f, 64f), 42f, 18f, 10f),
    };

    private static bool CanPlace(WorldMap map, in Building b, RoadEdge? own)
    {
        if (Vector2.Distance(b.Center, ParkCenter) < RingRadius - 6f + b.HalfDepth || NearPond(map, b.Center, b.HalfWidth + b.HalfDepth + 6f))
        {
            return false;
        }

        float reach = MathF.Max(b.HalfWidth, b.HalfDepth) * 1.42f;
        foreach ((Vector2 centre, float radius) in map.Reserved)
        {
            if (Vector2.DistanceSquared(centre, b.Center) < (radius + reach) * (radius + reach))
            {
                return false;
            }
        }

        // Sample the footprint: no point may sit on a carriageway, and the lot must be near level.
        float low = float.MaxValue;
        float high = float.MinValue;
        for (int v = -1; v <= 1; v++)
        {
            for (int u = -1; u <= 1; u++)
            {
                Vector2 p = b.Center + (b.AxisWidth * (u * b.HalfWidth)) + (b.AxisDepth * (v * b.HalfDepth));
                if (map.Roads.Nearest(p, 40f) is { } hit && Vector2.Distance(p, hit.Point) < (hit.Edge.Width * 0.5f) + (hit.Edge == own ? 0.3f : 1.2f))
                {
                    return false;
                }

                float h = map.Terrain.HeightAt(p);
                low = MathF.Min(low, h);
                high = MathF.Max(high, h);
            }
        }

        return high - low < 3.5f && !map.OverlapsBuilding(b);
    }

    private static float FoundationHeight(WorldMap map, in Building b)
    {
        float high = float.MinValue;
        for (int v = -1; v <= 1; v += 2)
        {
            for (int u = -1; u <= 1; u += 2)
            {
                high = MathF.Max(high, map.Terrain.HeightAt(b.Center + (b.AxisWidth * (u * b.HalfWidth)) + (b.AxisDepth * (v * b.HalfDepth))));
            }
        }

        return high + 0.05f;
    }

    private static void PlaceStreetFurniture(WorldMap map, Dictionary<DistrictId, List<RoadNode>> grids)
    {
        foreach (RoadEdge edge in map.Roads.Edges)
        {
            if (edge.Kind != RoadKind.Arterial)
            {
                continue;
            }

            // Lamps on arterials only where people live; the open road stays dark and moody.
            float side = 1f;
            for (float t = 20f; t < edge.Length - 12f; t += 42f)
            {
                Vector2 point = edge.Sample(t, out Vector2 direction);
                if (map.DistrictAt(point) is null)
                {
                    continue;
                }

                Vector2 left = MathUtil.Left(direction) * side;
                Vector2 position = point + (left * ((edge.Width * 0.5f) + 0.9f));
                map.AddProp(new Prop("lampu_jalan", position, MathUtil.YawOf(-left), 1f, 0.2f));
                side = -side;
            }
        }
    }

    // ------------------------------------------------------------------ vegetation

    public static Tree[] ScatterTrees(WorldMap map, int chunkX, int chunkZ)
    {
        List<Tree> trees = [];
        Vector2 origin = new(chunkX * WorldMap.ChunkSize, chunkZ * WorldMap.ChunkSize);
        const float spacing = 16f;
        int cells = (int)(WorldMap.ChunkSize / spacing);
        uint seed = map.Seed + 555u;
        for (int j = 0; j < cells; j++)
        {
            for (int i = 0; i < cells; i++)
            {
                int gx = (chunkX * cells) + i;
                int gz = (chunkZ * cells) + j;
                uint h = Hash.Of(gx, gz, seed);
                Vector2 p = origin + new Vector2((i + Hash.Unit(h)) * spacing, (j + Hash.Unit(Hash.Mix(h))) * spacing);
                LandUse use = map.LandUseAt(p);
                float roll = Hash.Unit(Hash.Mix(h + 1u));
                float pick = Hash.Unit(Hash.Mix(h + 2u));
                TreeKind? kind = use switch
                {
                    LandUse.Taman => roll < 0.6f ? (pick < 0.3f ? TreeKind.Beringin : pick < 0.6f ? TreeKind.Kecil : pick < 0.85f ? TreeKind.Kelapa : TreeKind.Bambu) : null,
                    LandUse.Hutan => roll < 0.55f ? (pick < 0.8f ? TreeKind.Pinus : TreeKind.Kecil) : null,
                    LandUse.KebunTeh => roll < 0.06f ? TreeKind.Pinus : null,
                    LandUse.Villa => roll < 0.3f ? (pick < 0.75f ? TreeKind.Pinus : TreeKind.Kecil) : null,
                    LandUse.Sawah => roll < 0.07f ? (pick < 0.5f ? TreeKind.Kelapa : pick < 0.8f ? TreeKind.Kecil : TreeKind.Bambu) : null,
                    LandUse.Kampus => roll < 0.16f ? (pick < 0.6f ? TreeKind.Kecil : TreeKind.Beringin) : null,
                    LandUse.Gang or LandUse.Pasar => roll < 0.01f ? TreeKind.Kecil : null,
                    _ => roll < 0.07f ? (pick < 0.7f ? TreeKind.Kecil : TreeKind.Kelapa) : null,
                };
                if (kind is not { } treeKind)
                {
                    continue;
                }

                if (map.Roads.IsOnRoad(p, 2.2f) || map.IsInsideBuilding(p, 1.2f) || NearPond(map, p, 2f) || IsReserved(map, p))
                {
                    continue;
                }

                float scale = 0.8f + (0.5f * Hash.Unit(Hash.Mix(h + 3u)));
                trees.Add(new Tree(treeKind, p, Hash.Unit(Hash.Mix(h + 4u)) * MathUtil.Tau, scale));
            }
        }

        // Tea terraces: dense rows of bushes, far closer together than trees.
        const float teaSpacing = 8f;
        int teaCells = (int)(WorldMap.ChunkSize / teaSpacing);
        if (map.LandUseAt(origin + new Vector2(WorldMap.ChunkSize * 0.5f)) == LandUse.KebunTeh
            || map.LandUseAt(origin) == LandUse.KebunTeh
            || map.LandUseAt(origin + new Vector2(WorldMap.ChunkSize)) == LandUse.KebunTeh)
        {
            for (int j = 0; j < teaCells; j++)
            {
                for (int i = 0; i < teaCells; i++)
                {
                    Vector2 p = origin + new Vector2((i + 0.5f) * teaSpacing, (j + 0.5f) * teaSpacing);
                    if (map.LandUseAt(p) != LandUse.KebunTeh || map.Roads.IsOnRoad(p, 1.5f) || map.IsInsideBuilding(p, 1f))
                    {
                        continue;
                    }

                    uint h = Hash.Of((chunkX * teaCells) + i, (chunkZ * teaCells) + j, seed + 9u);
                    trees.Add(new Tree(TreeKind.Teh, p + new Vector2(Hash.Unit(h) - 0.5f, 0f), 0f, 1.2f + (0.4f * Hash.Unit(Hash.Mix(h)))));
                }
            }
        }

        return [.. trees];
    }

    // ------------------------------------------------------------------ helpers

    private static bool IsReserved(WorldMap map, Vector2 p)
    {
        foreach ((Vector2 centre, float radius) in map.Reserved)
        {
            if (Vector2.DistanceSquared(centre, p) < radius * radius)
            {
                return true;
            }
        }

        return false;
    }

    private static bool NearPond(WorldMap map, Vector2 p, float margin)
    {
        foreach (Pond pond in map.Ponds)
        {
            float reach = (pond.Radius * 1.1f) + margin;
            if (Vector2.DistanceSquared(p, pond.Center) < reach * reach)
            {
                return true;
            }
        }

        return false;
    }

    private static RoadNode NearestOf(List<RoadNode> nodes, Vector2 p)
    {
        RoadNode best = nodes[0];
        float bestDistance = float.MaxValue;
        foreach (RoadNode node in nodes)
        {
            float d = Vector2.DistanceSquared(node.Position, p);
            if (d < bestDistance)
            {
                bestDistance = d;
                best = node;
            }
        }

        return best;
    }
}
