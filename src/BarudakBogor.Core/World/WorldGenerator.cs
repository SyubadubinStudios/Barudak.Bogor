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
    // Positions are real: metres east (+X) and south (+Z) of Tugu Kujang, taken from
    // OpenStreetMap (© OpenStreetMap contributors, ODbL). The roads, the rivers and the
    // railways follow their true lines.
    private static readonly Vector2 ParkCenter = new(-600f, -380f);
    private const float ParkRadius = 520f;
    private const float RiverHalfWidth = 13f;

    /// <summary>The loop around Kebun Raya: Pajajaran, Otista, Juanda, Jalak Harupat.</summary>
    private static readonly Vector2[] RingPoints =
    [
        new(-26f, -255f), new(32f, -38f), new(-48f, 172f), new(-334f, 104f), new(-486f, 125f), new(-641f, 173f), new(-877f, 283f),
        new(-1038f, 138f), new(-1112f, -59f), new(-1168f, -244f), new(-1229f, -446f), new(-1194f, -663f), new(-1042f, -822f),
        new(-854f, -917f), new(-653f, -926f), new(-456f, -995f), new(-269f, -911f), new(-58f, -842f), new(-65f, -592f), new(-50f, -421f),
    ];

    private static string RingName(int index) => index switch
    {
        <= 1 or >= 17 => "road.pajajaran",
        <= 5 => "road.otista",
        <= 11 => "road.juanda",
        _ => "road.jalakharupat",
    };

    private sealed record RealRoad(string Name, Vector2[] Points);

    /// <summary>The main streets of the city, each along its real line.</summary>
    private static readonly RealRoad[] RealRoads =
    [
        new("road.sudirman", [new(-875f, -968f), new(-880f, -1118f), new(-886f, -1293f), new(-892f, -1477f), new(-897f, -1632f), new(-903f, -1815f), new(-909f, -2005f), new(-906f, -2200f)]),
        new("road.ahmadyani", [new(-906f, -2200f), new(-849f, -2348f), new(-772f, -2487f), new(-674f, -2628f), new(-569f, -2768f), new(-453f, -2923f), new(-302f, -3127f), new(-178f, -3292f), new(-92f, -3402f), new(47f, -3563f), new(207f, -3637f), new(368f, -3619f)]),
        new("road.pemuda", [new(-906f, -2200f), new(-921f, -2318f), new(-930f, -2530f), new(-939f, -2675f), new(-943f, -2840f), new(-949f, -3070f), new(-918f, -3156f), new(-806f, -3320f), new(-710f, -3453f)]),
        new("road.pajajaran", [new(436f, -3545f), new(385f, -3294f), new(345f, -3070f), new(285f, -2783f), new(227f, -2481f), new(182f, -2243f), new(120f, -1935f), new(65f, -1666f), new(-10f, -1380f), new(-36f, -1111f), new(-58f, -842f)]),
        new("road.pajajaran", [new(-48f, 172f), new(190f, 330f), new(481f, 652f), new(680f, 919f), new(844f, 1160f), new(972f, 1412f), new(1070f, 1673f), new(1151f, 1883f), new(1239f, 2110f)]),
        new("road.suryakencana", [new(-641f, 173f), new(-618f, 324f), new(-506f, 472f), new(-399f, 605f), new(-307f, 714f), new(-202f, 820f), new(-64f, 942f)]),
        new("road.siliwangi", [new(-64f, 942f), new(81f, 1071f), new(184f, 1171f), new(291f, 1299f), new(373f, 1416f), new(453f, 1532f), new(596f, 1663f), new(737f, 1791f), new(822f, 1870f), new(967f, 1992f), new(1092f, 2093f), new(1239f, 2110f)]),
        new("road.muslihat", [new(-1229f, -446f), new(-1280f, -524f), new(-1422f, -556f), new(-1601f, -593f), new(-1753f, -628f), new(-1967f, -647f), new(-2111f, -658f), new(-2299f, -692f), new(-2460f, -766f), new(-2637f, -615f), new(-2798f, -578f)]),
        new("road.merdeka", [new(-1753f, -628f), new(-1838f, -681f), new(-1884f, -827f), new(-1909f, -991f), new(-1911f, -1203f), new(-1911f, -1327f), new(-1918f, -1548f), new(-1922f, -1710f), new(-1917f, -1866f), new(-1913f, -2030f)]),
        new("road.empang", [new(-1038f, 138f), new(-1120f, 400f), new(-1076f, 709f)]),
        new("road.pahlawan", [new(-1076f, 709f), new(-946f, 757f), new(-822f, 859f), new(-678f, 990f), new(-555f, 1085f), new(-409f, 1163f), new(-280f, 1250f), new(-187f, 1399f), new(-101f, 1572f), new(-17f, 1695f), new(99f, 1828f), new(190f, 1912f)]),
    ];

    /// <summary>The Ciliwung from above Katulampa weir, through Kebun Raya, to the northern edge of the map.</summary>
    private static readonly Vector2[] RiverPoints =
    [
        new(5640f, 5055f), new(5573f, 4869f), new(5326f, 4709f), new(5288f, 4424f), new(4963f, 4370f), new(4928f, 4127f), new(4778f, 3962f), new(4586f, 3953f),
        new(4159f, 3692f), new(3572f, 3519f), new(3123f, 3539f), new(2979f, 3433f), new(2791f, 3411f), new(2535f, 3218f), new(2370f, 2992f), new(2305f, 2759f),
        new(1695f, 2354f), new(1422f, 1799f), new(588f, 1519f), new(574f, 1041f), new(110f, 655f), new(-447f, -128f), new(-549f, -417f), new(-453f, -607f),
        new(-534f, -977f), new(-823f, -1395f), new(-720f, -1761f), new(-814f, -1872f), new(-737f, -2098f), new(-787f, -2219f), new(-460f, -2455f), new(-299f, -2833f),
        new(-85f, -2927f), new(294f, -3415f), new(226f, -3544f), new(338f, -3750f), new(260f, -4204f), new(526f, -4568f), new(341f, -5002f), new(82f, -5163f),
        new(22f, -5512f), new(-351f, -5669f), new(-440f, -6059f), new(-295f, -6448f), new(-526f, -6681f), new(-249f, -6937f), new(-117f, -7210f), new(-23f, -7661f),
        new(-115f, -7956f), new(220f, -8113f), new(242f, -8348f), new(-125f, -8507f), new(-115f, -8988f), new(-281f, -9120f), new(-85f, -9402f),
    ];

    /// <summary>The Cisadane from the foot of Gunung Salak, past Empang and Dramaga, to the north-west corner of the map.</summary>
    private static readonly Vector2[] CisadanePoints =
    [
        new(1088f, 14262f), new(1132f, 14131f), new(1263f, 14087f), new(1255f, 13990f), new(1106f, 13989f), new(898f, 13561f), new(1055f, 13487f), new(1258f, 13507f),
        new(1322f, 13421f), new(1126f, 13123f), new(920f, 12976f), new(1109f, 12393f), new(1343f, 12345f), new(1571f, 11927f), new(1484f, 11747f), new(1517f, 11571f),
        new(1189f, 11150f), new(1369f, 11068f), new(1292f, 10883f), new(1564f, 10662f), new(1633f, 10374f), new(1518f, 10326f), new(1401f, 10049f), new(1506f, 9962f),
        new(1542f, 9471f), new(1740f, 9102f), new(1502f, 8839f), new(1532f, 8661f), new(1413f, 8494f), new(1602f, 8149f), new(1490f, 7177f), new(1265f, 7124f),
        new(1267f, 6847f), new(1163f, 6609f), new(939f, 6507f), new(862f, 6361f), new(397f, 6019f), new(360f, 5669f), new(600f, 5307f), new(598f, 4982f),
        new(749f, 4770f), new(527f, 4380f), new(604f, 4041f), new(442f, 3883f), new(806f, 3777f), new(737f, 3665f), new(490f, 3556f), new(542f, 3331f), new(468f, 3167f),
        new(488f, 2778f), new(168f, 2779f), new(215f, 2444f), new(-117f, 2371f), new(-205f, 2017f), new(-383f, 2028f), new(-509f, 1860f), new(-718f, 1939f),
        new(-702f, 1564f), new(-1130f, 1336f), new(-1153f, 1050f), new(-1395f, 649f), new(-1383f, 307f), new(-1589f, 332f), new(-1473f, 86f), new(-1860f, -60f),
        new(-1899f, -160f), new(-1815f, -235f), new(-1895f, -344f), new(-2120f, -358f), new(-2292f, -227f), new(-2476f, -301f), new(-2410f, -579f), new(-2464f, -697f),
        new(-2247f, -939f), new(-2246f, -1027f), new(-2388f, -1042f), new(-2624f, -938f), new(-2693f, -987f), new(-2793f, -1264f), new(-2655f, -1475f),
        new(-2838f, -1454f), new(-2910f, -1553f), new(-3265f, -1676f), new(-3214f, -2101f), new(-3297f, -2170f), new(-3749f, -2154f), new(-3873f, -2280f),
        new(-3950f, -2732f), new(-4196f, -2666f), new(-4492f, -2853f), new(-4406f, -3025f), new(-4453f, -3149f), new(-4840f, -3444f), new(-4857f, -3694f),
        new(-5190f, -4054f), new(-5029f, -4342f), new(-5331f, -4691f), new(-5253f, -4870f), new(-4980f, -5002f), new(-4934f, -5088f), new(-5201f, -5530f),
        new(-5438f, -5504f), new(-5551f, -5867f), new(-5624f, -5849f), new(-5762f, -5571f), new(-5940f, -5606f), new(-6128f, -6007f), new(-6205f, -5913f),
        new(-6308f, -5933f), new(-6265f, -6106f), new(-6417f, -6228f), new(-6439f, -6387f), new(-6745f, -6368f), new(-7129f, -6529f), new(-7023f, -6790f),
        new(-7150f, -6839f), new(-7365f, -6820f), new(-7464f, -6564f), new(-7593f, -6509f), new(-7751f, -6765f), new(-7915f, -6729f), new(-7965f, -6606f),
        new(-8160f, -6606f), new(-8197f, -6890f), new(-8389f, -6920f), new(-8693f, -6790f), new(-8960f, -6808f), new(-9081f, -6494f), new(-9323f, -6309f),
        new(-9470f, -6335f), new(-9831f, -6222f), new(-9938f, -6288f), new(-10267f, -6252f), new(-10250f, -6546f), new(-10583f, -6486f), new(-10699f, -6649f),
        new(-10932f, -6667f), new(-10943f, -6902f), new(-11662f, -6991f), new(-11735f, -7188f), new(-11981f, -7160f), new(-12062f, -7539f), new(-12253f, -7780f),
        new(-12422f, -7802f), new(-12333f, -7941f), new(-12419f, -8128f), new(-12312f, -8288f), new(-12551f, -8469f), new(-12573f, -9037f), new(-12922f, -9489f),
        new(-13372f, -9803f), new(-13834f, -9483f), new(-14014f, -9527f), new(-13874f, -10148f), new(-13751f, -10371f), new(-14544f, -10723f), new(-14763f, -11039f),
        new(-14745f, -11180f), new(-14490f, -11494f), new(-14348f, -11879f), new(-14005f, -12157f), new(-14074f, -12314f), new(-14537f, -12595f), new(-14577f, -12769f),
        new(-14511f, -13109f), new(-14595f, -13822f), new(-15179f, -14049f), new(-15138f, -14476f), new(-15238f, -14594f), new(-15833f, -14292f), new(-15763f, -14821f),
        new(-15894f, -14841f), new(-16276f, -14626f), new(-16456f, -14642f), new(-16699f, -15149f), new(-16976f, -15416f), new(-16691f, -15843f), new(-16379f, -16055f),
        new(-16448f, -16260f), new(-16321f, -16661f), new(-16384f, -16835f), new(-16334f, -17598f), new(-16465f, -17703f), new(-16854f, -17742f), new(-17085f, -17873f),
        new(-17023f, -18180f), new(-17171f, -18532f), new(-17095f, -18945f), new(-16886f, -19300f),
    ];

    /// <summary>The line to Sukabumi: from Bogor Paledang south through Batutulis, Maseng and Cigombong.</summary>
    private static readonly Vector2[] SouthRailPoints =
    [
        new(-1609f, -513f), new(-1592f, -409f), new(-1539f, -317f), new(-1351f, -177f), new(-1287f, -96f), new(-1115f, 421f), new(-932f, 574f), new(-890f, 637f),
        new(-870f, 714f), new(-862f, 1061f), new(-732f, 1268f), new(-662f, 1466f), new(-564f, 1640f), new(-466f, 1758f), new(-312f, 1885f), new(-97f, 2004f),
        new(-60f, 2060f), new(20f, 2288f), new(76f, 2340f), new(242f, 2410f), new(352f, 2619f), new(532f, 2742f), new(587f, 2824f), new(597f, 2914f), new(549f, 3132f),
        new(607f, 3383f), new(677f, 3524f), new(828f, 3679f), new(856f, 3779f), new(815f, 3899f), new(648f, 4100f), new(605f, 4248f), new(609f, 4354f), new(780f, 4668f),
        new(802f, 4777f), new(769f, 4869f), new(643f, 5045f), new(667f, 5301f), new(642f, 5400f), new(464f, 5584f), new(426f, 5647f), new(441f, 5916f), new(478f, 6005f),
        new(622f, 6118f), new(722f, 6248f), new(889f, 6356f), new(1002f, 6489f), new(1203f, 6607f), new(1325f, 6875f), new(1391f, 7130f), new(1438f, 7428f),
        new(1514f, 7631f), new(1499f, 7847f), new(1545f, 8141f), new(1521f, 8235f), new(1370f, 8442f), new(1357f, 8524f), new(1456f, 8840f), new(1608f, 9002f),
        new(1646f, 9126f), new(1621f, 9215f), new(1509f, 9383f), new(1423f, 9880f), new(1338f, 10066f), new(1354f, 10138f), new(1530f, 10418f), new(1540f, 10527f),
        new(1445f, 10719f), new(1204f, 10899f), new(975f, 11364f), new(990f, 11495f), new(1170f, 11747f), new(1184f, 11830f), new(1167f, 11899f), new(687f, 12469f),
        new(596f, 12705f), new(390f, 12891f), new(318f, 13319f), new(288f, 13391f), new(206f, 13459f), new(-26f, 13544f), new(-101f, 13655f), new(-86f, 14323f),
        new(-31f, 14420f), new(138f, 14519f), new(195f, 14592f), new(212f, 14662f), new(210f, 14910f), new(186f, 14974f), new(135f, 15032f), new(-257f, 15193f),
        new(-334f, 15295f), new(-352f, 15422f), new(-325f, 15502f), new(-141f, 15624f), new(-66f, 15748f), new(-77f, 15831f), new(-167f, 16020f), new(-121f, 16198f),
        new(-117f, 16290f), new(-147f, 16365f), new(-200f, 16420f), new(-420f, 16492f), new(-537f, 16594f), new(-579f, 16704f), new(-612f, 17102f), new(-602f, 17211f),
        new(-567f, 17323f), new(-258f, 17917f), new(-235f, 18035f), new(-250f, 18134f), new(-326f, 18254f), new(-444f, 18318f), new(-1269f, 18437f), new(-1365f, 18480f),
        new(-1415f, 18542f), new(-1437f, 18616f), new(-1462f, 19020f), new(-1632f, 19300f),
    ];

    private const float CisadaneHalfWidth = 15f;

    // The commuter line ends at Stasiun Bogor and runs north to Cilebut and on to Jakarta.
    private static readonly Vector2 Terminus = new(-1586f, -789f);
    private static readonly Vector2 TrackHeading = Vector2.Normalize(new Vector2(-1533f, -1135f) - Terminus);

    /// <summary>The side of the track the station is on: east, toward the alun-alun.</summary>
    private static Vector2 StationSide
    {
        get
        {
            Vector2 left = MathUtil.Left(TrackHeading);
            return left.X > 0f ? left : -left;
        }
    }

    private static bool InsideRing(Vector2 p)
    {
        bool inside = false;
        for (int i = 0, j = RingPoints.Length - 1; i < RingPoints.Length; j = i++)
        {
            Vector2 a = RingPoints[i];
            Vector2 b = RingPoints[j];
            if ((a.Y > p.Y) != (b.Y > p.Y) && p.X < ((b.X - a.X) * (p.Y - a.Y) / (b.Y - a.Y)) + a.X)
            {
                inside = !inside;
            }
        }

        return inside;
    }

    private static float DistanceTo(List<(Vector2 A, Vector2 B)> segments, Vector2 p)
    {
        float best = float.MaxValue;
        foreach ((Vector2 a, Vector2 b) in segments)
        {
            MathUtil.ClosestOnSegment(a, b, p, out Vector2 closest);
            best = MathF.Min(best, Vector2.DistanceSquared(p, closest));
        }

        return MathF.Sqrt(best);
    }

    /// <summary>True when segment ab properly crosses any of the segments (touching at an end does not count).</summary>
    private static bool Crosses(List<(Vector2 A, Vector2 B)> segments, Vector2 a, Vector2 b)
    {
        static float Side(Vector2 o, Vector2 u, Vector2 v) => ((u.X - o.X) * (v.Y - o.Y)) - ((u.Y - o.Y) * (v.X - o.X));
        foreach ((Vector2 c, Vector2 d) in segments)
        {
            float d1 = Side(c, d, a);
            float d2 = Side(c, d, b);
            float d3 = Side(a, b, c);
            float d4 = Side(a, b, d);
            if (d1 * d2 < -1e-3f && d3 * d4 < -1e-3f)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>What a straight street from a to b would run into: 0 nothing, 1 the railway, 2 the river.</summary>
    private static int CorridorOnTheWay(WorldMap map, Vector2 a, Vector2 b)
    {
        int found = 0;
        foreach (Corridor corridor in map.Corridors)
        {
            for (int i = 0; i <= 16; i++)
            {
                if (corridor.Closest(Vector2.Lerp(a, b, i / 16f), corridor.HalfWidth + 4f, out _, out _, out _))
                {
                    found = Math.Max(found, corridor.Kind == CorridorKind.River ? 2 : 1);
                    break;
                }
            }
        }

        return found;
    }

    public static WorldMap Generate(uint seed)
    {
        District[] districts =
        [
            new(DistrictId.PusatKota, "district.pusatkota", new Vector2(-600f, -500f), 2_300f, LandUse.Kota),
            // The campus town, the circuit and the hill resort (Cisarua) at their real places.
            new(DistrictId.Dramaga, "district.dramaga", new Vector2(-8_630f, -4_800f), 1_150f, LandUse.Kampus),
            new(DistrictId.Sentul, "district.sentul", new Vector2(5_740f, -7_290f), 1_150f, LandUse.Modern),
            new(DistrictId.Puncak, "district.puncak", new Vector2(16_000f, 8_900f), 1_300f, LandUse.Villa),
            new(DistrictId.KebunRaya, "district.kebunraya", ParkCenter, ParkRadius, LandUse.Taman),
            new(DistrictId.Empang, "district.empang", new Vector2(-1_250f, 900f), 400f, LandUse.Kampung),
            new(DistrictId.PasarAnyar, "district.pasaranyar", new Vector2(-1_380f, -1_090f), 300f, LandUse.Pasar),
            new(DistrictId.GangSelot, "district.gangselot", new Vector2(-2_330f, -1_400f), 230f, LandUse.Gang),
            new(DistrictId.SuryaKencana, "district.suryakencana", new Vector2(-400f, 560f), 270f, LandUse.Pasar),
        ];
        Pond[] ponds =
        [
            // In a gap between the real streets of Empang, so no road runs through the water.
            new("pond.empang", new Vector2(-1_480f, 1_200f), 62f, 2.2f),
            new("pond.situgede", new Vector2(-6_290f, -5_400f), 125f, 3f),
            // The real lake lies just past the eastern edge of the map; this one sits in a fold above Cisarua.
            new("pond.telagawarna", new Vector2(16_540f, 7_930f), 90f, 4f),
        ];

        WorldMap map = new(seed, districts, ponds);
        map.SetPuncakCenter(districts[3].Center);
        Rng rng = new(seed * 7919u);

        // The river and the railway reshape the ground, so they come before anything reads a height.
        map.Terrain.AddCorridor(CorridorKind.River, "river.ciliwung", RiverPoints, RiverHalfWidth, 60f, 10);
        // The first points are in line, so the track runs dead straight past the platform.
        map.Terrain.AddCorridor(CorridorKind.Rail, "rail.krl",
            [Terminus, Terminus + (TrackHeading * 70f), Terminus + (TrackHeading * 160f), Terminus + (TrackHeading * 340f),
                new(-1_289f, -2_002f), new(-1_354f, -2_644f), new(-1_295f, -2_930f), new(-1_099f, -3_222f), new(-607f, -3_555f), new(-471f, -3_694f),
                new(-249f, -4_225f), new(-251f, -4_422f), new(-547f, -5_757f), new(-613f, -6_254f), new(-418f, -8_578f), new(-437f, -8_779f)],
            2.2f, 30f, 15);
        map.Terrain.AddCorridor(CorridorKind.River, "river.cisadane", CisadanePoints, CisadaneHalfWidth, 50f, 10);
        map.Terrain.AddCorridor(CorridorKind.Rail, "rail.sukabumi", SouthRailPoints, 2.2f, 30f, 15);

        BuildRoads(map, ref rng, out Dictionary<DistrictId, List<RoadNode>> grids, out List<RoadNode> ring);
        BuildCrossings(map);
        // The ground and the bridges are final: from here on a road is something to stand on.
        map.Roads.BuildSurfaces(map.Terrain);
        map.Terrain.AttachRoads(map.Roads);
        PlacePois(map, ring);
        List<Building> landmarks = PlaceRealPlaces(map);
        BuildInteriors(map);
        if (BuildStation(map) is { } hall)
        {
            landmarks.Add(hall);
        }

        BuildPaledang(map);

        PlaceLandmarks(map, ring);
        PlaceStreetLife(map);
        PlaceWaterPlants(map);
        PlaceBuildings(map, ref rng);
        PlaceInfill(map, ref rng);
        // The named buildings go in last: their plots were reserved before the rest was built.
        foreach (Building landmark in landmarks)
        {
            map.AddBuilding(landmark with { BaseY = FoundationHeight(map, landmark) });
        }

        PlaceStreetFurniture(map, grids);
        return map;
    }

    // ------------------------------------------------------------------ roads

    private static void BuildRoads(WorldMap map, ref Rng rng, out Dictionary<DistrictId, List<RoadNode>> grids, out List<RoadNode> ring)
    {
        RoadNetwork roads = map.Roads;
        Dictionary<DistrictId, List<RoadNode>> gridMap = [];
        grids = gridMap;

        // The streets of the city proper are the real ones (see bogor_streets.txt).
        List<RoadNode> core = LoadRealStreets(map);
        gridMap[DistrictId.PusatKota] = core;
        ring = [];
        foreach (Vector2 point in RingPoints)
        {
            RoadNode node = NearestOf(core, point);
            if (!ring.Contains(node))
            {
                ring.Add(node);
            }
        }

        District gang = map.Districts.First(d => d.Id == DistrictId.GangSelot);

        // Gang Selot: a tight alley maze hanging off the city grid.
        List<RoadNode> alleys = BuildGrid(map, ref rng, gang, 46f, 7f, RoadKind.Gang, 0, 0.12f, p => map.Roads.Nearest(p, 14f) is not null, null);
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

        // Dramaga, Sentul and Cisarua, the toll roads and the country roads between them are in
        // the same data: nothing outside Gang Selot is generated any more.
        // Twin carriageways sometimes end a few metres apart: close what is left.
        foreach (RoadNode loose in core)
        {
            if (loose.Edges.Count != 1 || roads.Edges[loose.Edges[0]].Kind != RoadKind.Arterial)
            {
                continue;
            }

            RoadNode? mate = null;
            float best = 90f * 90f;
            foreach (RoadNode other in core)
            {
                float d2 = Vector2.DistanceSquared(other.Position, loose.Position);
                if (other != loose && d2 < best && RoadNetwork.OtherEnd(roads.Edges[loose.Edges[0]], loose.Id) != other.Id)
                {
                    best = d2;
                    mate = other;
                }
            }

            if (mate is not null)
            {
                roads.AddEdge(loose, mate, RoadKind.Arterial, roads.Edges[loose.Edges[0]].NameKey);
            }
        }

        // Station Road: from the forecourt to the nearest street.
        RoadNode forecourt = roads.AddNode(Terminus + (TrackHeading * 60f) + (StationSide * 58f));
        roads.AddEdge(NearestOf(core, forecourt.Position), forecourt, RoadKind.Street, "road.stasiun");

        Connect(map);
    }

    /// <summary>
    /// A room people can walk into: three walls, a front with a doorway, a roof, and whatever
    /// furniture the caller adds. <paramref name="door"/> is the middle of the doorway and
    /// <paramref name="back"/> points from it into the room.
    /// </summary>
    private static void BuildRoom(WorldMap map, Vector2 door, Vector2 back, float halfWidth, float depth, string nameKey, Action<Func<float, float, Vector2>, float, float> furnish)
    {
        Vector2 across = MathUtil.Left(back);
        float yaw = MathF.Atan2(-across.Y, across.X);
        Vector2 centre = door + (back * (depth * 0.5f));
        float floor = map.Terrain.HeightAt(centre) + 0.06f;
        float top = floor + 2.9f;
        const float wall = 0.12f;
        Vector2 At(float right, float inward) => door + (across * right) + (back * inward);

        map.AddSlab(new Slab(centre, halfWidth, depth * 0.5f, yaw, floor - 1.5f, floor, SlabLook.Concrete));
        map.AddSlab(new Slab(At(0f, depth), halfWidth, wall, yaw, floor - 1.5f, top, SlabLook.White, Solid: true));
        map.AddSlab(new Slab(At(halfWidth, depth * 0.5f), wall, depth * 0.5f, yaw, floor - 1.5f, top, SlabLook.White, Solid: true));
        map.AddSlab(new Slab(At(-halfWidth, depth * 0.5f), wall, depth * 0.5f, yaw, floor - 1.5f, top, SlabLook.White, Solid: true));
        // The front: a pier each side of a doorway 1.4 m wide, and a lintel over it.
        float pier = (halfWidth - 0.7f) * 0.5f;
        map.AddSlab(new Slab(At(0.7f + pier, 0f), pier, wall, yaw, floor - 1.5f, top, SlabLook.White, Solid: true));
        map.AddSlab(new Slab(At(-0.7f - pier, 0f), pier, wall, yaw, floor - 1.5f, top, SlabLook.White, Solid: true));
        map.AddSlab(new Slab(At(0f, 0f), 0.7f, wall, yaw, floor + 2.15f, top, SlabLook.White));
        map.AddSlab(new Slab(centre, halfWidth + 0.5f, (depth * 0.5f) + 0.5f, yaw, top, top + 0.18f, SlabLook.Zinc));
        map.AddSign(new Sign(At(0f, -0.16f), top - 0.25f, MathUtil.YawOf(-back), MathF.Min(halfWidth * 1.7f, 5f), nameKey));
        map.Reserved.Add((centre, MathF.Max(halfWidth, depth * 0.5f) + 3f));
        furnish((right, inward) => At(right, inward), floor, yaw);
    }

    /// <summary>The two interiors: the bandrek stall and Ujang's rented room.</summary>
    private static void BuildInteriors(WorldMap map)
    {
        Poi stall = map[PoiId.WarungBandrek];
        Vector2 inward = -MathUtil.Forward(stall.Yaw);
        BuildRoom(map, stall.Position + (MathUtil.Left(inward) * 9f) + (inward * 1.5f), inward, 3.2f, 5f, "poi.warungbandrek", (at, floor, yaw) =>
        {
            // A counter along the back with the pots on it, and two benches at a long table.
            map.AddSlab(new Slab(at(0f, 4.2f), 2.6f, 0.35f, yaw, floor, floor + 0.95f, SlabLook.Dark, Solid: true));
            map.AddSlab(new Slab(at(-1.4f, 4.2f), 0.22f, 0.22f, yaw, floor + 0.95f, floor + 1.3f, SlabLook.Copper));
            map.AddSlab(new Slab(at(-0.6f, 4.2f), 0.18f, 0.18f, yaw, floor + 0.95f, floor + 1.22f, SlabLook.Zinc));
            map.AddSlab(new Slab(at(1.3f, 4.2f), 0.3f, 0.2f, yaw, floor + 0.95f, floor + 1.15f, SlabLook.White));
            map.AddSlab(new Slab(at(-1.6f, 2f), 0.4f, 1.1f, yaw, floor + 0.68f, floor + 0.74f, SlabLook.Dark));
            map.AddSlab(new Slab(at(-1.6f, 2f), 0.06f, 0.9f, yaw, floor, floor + 0.68f, SlabLook.Dark, Solid: true));
            map.AddSlab(new Slab(at(-2.35f, 2f), 0.16f, 1.1f, yaw, floor, floor + 0.44f, SlabLook.Zinc, Solid: true));
            map.AddSlab(new Slab(at(-0.85f, 2f), 0.16f, 1.1f, yaw, floor, floor + 0.44f, SlabLook.Zinc, Solid: true));
            map.AddSlab(new Slab(at(2.3f, 1.6f), 0.5f, 0.35f, yaw, floor, floor + 1.7f, SlabLook.Copper, Solid: true));
        });

        Poi home = map[PoiId.RumahUjang];
        Vector2 into = -MathUtil.Forward(home.Yaw);
        BuildRoom(map, home.Position + (into * 2.2f), into, 2.4f, 4f, "poi.rumahujang", (at, floor, yaw) =>
        {
            // A mattress on the floor, a wardrobe, a small table with a stove: a rented room.
            map.AddSlab(new Slab(at(1.3f, 2.9f), 0.9f, 0.95f, yaw, floor, floor + 0.22f, SlabLook.Copper, Solid: true));
            map.AddSlab(new Slab(at(1.3f, 3.6f), 0.6f, 0.2f, yaw, floor + 0.22f, floor + 0.36f, SlabLook.White));
            map.AddSlab(new Slab(at(-1.75f, 3.5f), 0.5f, 0.32f, yaw, floor, floor + 1.9f, SlabLook.Dark, Solid: true));
            map.AddSlab(new Slab(at(-1.7f, 1.3f), 0.45f, 0.45f, yaw, floor + 0.62f, floor + 0.68f, SlabLook.Dark));
            map.AddSlab(new Slab(at(-1.7f, 1.3f), 0.05f, 0.05f, yaw, floor, floor + 0.62f, SlabLook.Dark, Solid: true));
            map.AddSlab(new Slab(at(-1.7f, 1.3f), 0.2f, 0.16f, yaw, floor + 0.68f, floor + 0.8f, SlabLook.Zinc));
        });
    }

    /// <summary>
    /// The real road network: every toll road and main road of the whole map, and every street
    /// down to the alleys in the city centre, Dramaga, Sentul and Cisarua, cut at its junctions.
    /// One line per stretch: kind (T toll, A arterial, M mountain road, S street, G alley), name,
    /// then its points in metres east,south of Tugu Kujang. Data © OpenStreetMap contributors (ODbL).
    /// </summary>
    private static List<RoadNode> LoadRealStreets(WorldMap map)
    {
        RoadNetwork roads = map.Roads;
        Dictionary<(int, int), RoadNode> junctions = [];
        List<RoadNode> nodes = [];
        RoadNode At(Vector2 p)
        {
            (int, int) key = ((int)MathF.Round(p.X), (int)MathF.Round(p.Y));
            if (!junctions.TryGetValue(key, out RoadNode? node))
            {
                junctions[key] = node = roads.AddNode(p);
                nodes.Add(node);
            }

            return node;
        }

        using Stream stream = typeof(WorldGenerator).Assembly.GetManifestResourceStream("BarudakBogor.World.bogor_streets.txt")
            ?? throw new InvalidOperationException("The street data is missing from the assembly.");
        using StreamReader reader = new(stream);
        while (reader.ReadLine() is { } line)
        {
            string[] parts = line.Split('|');
            if (parts.Length != 3)
            {
                continue;
            }

            string[] pairs = parts[2].Split(' ', StringSplitOptions.RemoveEmptyEntries);
            Vector2[] points = new Vector2[pairs.Length];
            for (int i = 0; i < pairs.Length; i++)
            {
                int comma = pairs[i].IndexOf(',');
                points[i] = new Vector2(
                    float.Parse(pairs[i].AsSpan(0, comma), System.Globalization.CultureInfo.InvariantCulture),
                    float.Parse(pairs[i].AsSpan(comma + 1), System.Globalization.CultureInfo.InvariantCulture));
            }

            if (parts[0] == "G")
            {
                // An alley that runs in the river bed rather than across it was a footpath along
                // the bank; under water it is no use to anyone.
                int wet = points.Count(p => map.Terrain.CorridorClearance(p, out CorridorKind kind) < 3f && kind == CorridorKind.River);
                if (wet * 4 > points.Length)
                {
                    continue;
                }
            }

            RoadNode from = At(points[0]);
            RoadNode to = At(points[^1]);
            if (from != to)
            {
                RoadKind kind = parts[0] switch
                {
                    "T" => RoadKind.Toll,
                    "A" => RoadKind.Arterial,
                    "M" => RoadKind.Mountain,
                    "G" => RoadKind.Gang,
                    _ => RoadKind.Street,
                };
                roads.AddEdge(from, to, points, kind, RoadKey(parts[1], kind));
            }
        }

        return nodes;
    }

    /// <summary>The race track is in the street data under this name.</summary>
    private const string CircuitName = "Sirkuit Sentul";

    /// <summary>Well-known streets get a translated name; the rest keep the name on their street sign.</summary>
    private static string RoadKey(string name, RoadKind kind)
    {
        if (kind == RoadKind.Toll)
        {
            return name.Contains("Jakarta", StringComparison.Ordinal) ? "road.jagorawi"
                : name.Contains("Lingkar Luar", StringComparison.Ordinal) ? "road.borr"
                : name.Contains("Sukabumi", StringComparison.Ordinal) ? "road.bocimi"
                : "road.tol";
        }

        if (name == CircuitName)
        {
            return "road.sirkuit";
        }

        (string Part, string Key)[] known =
        [
            ("Pajajaran", "road.pajajaran"), ("Djuanda", "road.juanda"), ("Juanda", "road.juanda"), ("Otto Iskandar", "road.otista"),
            ("Jalak Harupat", "road.jalakharupat"), ("Sudirman", "road.sudirman"), ("Ahmad Yani", "road.ahmadyani"), ("Pemuda", "road.pemuda"),
            ("Surya Kencana", "road.suryakencana"), ("Suryakencana", "road.suryakencana"), ("Siliwangi", "road.siliwangi"),
            ("Kapten Muslihat", "road.muslihat"), ("Jalan Merdeka", "road.merdeka"), ("Jalan Empang", "road.empang"), ("Jalan Pahlawan", "road.pahlawan"),
            ("Dramaga", "road.dramaga"), ("Sholeh Iskandar", "road.jalanbaru"), ("Soleh Iskandar", "road.jalanbaru"),
            ("Raya Puncak", "road.puncak"), ("Tajur", "road.tajur"),
        ];
        foreach ((string part, string key) in known)
        {
            if (name.Contains(part, StringComparison.OrdinalIgnoreCase))
            {
                return key;
            }
        }

        return name.Length == 0 ? "road.kota" : name;
    }

    /// <summary>Joins any stranded pocket of streets to the rest, so every place can be driven to.</summary>
    private static void Connect(WorldMap map)
    {
        RoadNetwork roads = map.Roads;
        for (int pass = 0; pass < 200; pass++)
        {
            int[] group = new int[roads.Nodes.Count];
            Array.Fill(group, -1);
            List<List<RoadNode>> groups = [];
            foreach (RoadNode seed in roads.Nodes)
            {
                if (group[seed.Id] >= 0 || seed.Edges.Count == 0)
                {
                    continue;
                }

                List<RoadNode> members = [];
                Stack<RoadNode> open = new();
                open.Push(seed);
                group[seed.Id] = groups.Count;
                while (open.TryPop(out RoadNode? node))
                {
                    members.Add(node);
                    foreach (int edgeId in node.Edges)
                    {
                        RoadNode other = roads.Nodes[RoadNetwork.OtherEnd(roads.Edges[edgeId], node.Id)];
                        if (group[other.Id] < 0)
                        {
                            group[other.Id] = groups.Count;
                            open.Push(other);
                        }
                    }
                }

                groups.Add(members);
            }

            if (groups.Count <= 1)
            {
                return;
            }

            List<RoadNode> main = groups.MaxBy(g => g.Count)!;
            List<RoadNode> stranded = groups.Where(g => g != main).MinBy(g => g.Count)!;
            RoadNode? from = null;
            RoadNode? to = null;
            float best = float.MaxValue;
            foreach (RoadNode a in stranded)
            {
                foreach (RoadNode b in main)
                {
                    float d2 = Vector2.DistanceSquared(a.Position, b.Position);
                    if (d2 < best)
                    {
                        best = d2;
                        from = a;
                        to = b;
                    }
                }
            }

            if (from is null || to is null)
            {
                return;
            }

            roads.AddEdge(from, to, RoadKind.Street, "road.kota");
        }
    }

    /// <summary>Carries every road that meets a river over it on a bridge, and notes where roads cross the railway.</summary>
    private static void BuildCrossings(WorldMap map)
    {
        const float step = 2f;
        foreach (RoadEdge edge in map.Roads.Edges)
        {
            // Most roads come nowhere near a river or a track: one cheap look along the road settles it.
            bool near = false;
            for (float d = 0f; d <= edge.Length + step && !near; d += 6f)
            {
                near = map.Terrain.CorridorClearance(edge.Sample(MathF.Min(d, edge.Length), out _)) < 14f;
            }

            if (!near)
            {
                continue;
            }

            foreach (Corridor corridor in map.Corridors)
            {
                bool river = corridor.Kind == CorridorKind.River;
                float reach = river ? corridor.HalfWidth + 7f : corridor.HalfWidth + 0.5f;
                foreach ((List<Vector2> span, float level, float along) in Spans(corridor, reach, edge.Length, d => edge.Sample(d, out _)))
                {
                    if (river)
                    {
                        // The deck sits a touch above the banks it lands on.
                        AddBridge(map, span, (edge.Width * 0.5f) + 1.2f, level + 2.45f, level);
                    }
                    else if (corridor == map.Railway)
                    {
                        // Only the commuter line has trains, so only its crossings have gates.
                        map.AddCrossing(new LevelCrossing(span[span.Count / 2], along));
                    }
                }
            }
        }

        // A railway gets over a river on a bridge of its own.
        foreach (Corridor rail in map.Corridors)
        {
            if (rail.Kind != CorridorKind.Rail)
            {
                continue;
            }

            foreach (Corridor river in map.Corridors)
            {
                if (river.Kind != CorridorKind.River)
                {
                    continue;
                }

                foreach ((List<Vector2> span, float level, _) in Spans(river, river.HalfWidth + 5f, rail.Length, d => rail.Sample(d, out _, out _)))
                {
                    rail.Closest(span[span.Count / 2], 20f, out _, out _, out float track);
                    AddBridge(map, span, 3.2f, MathF.Max(track, level + 2.45f), level);
                }
            }
        }
    }

    private static void AddBridge(WorldMap map, List<Vector2> span, float half, float height, float water)
    {
        for (int i = 0; i < span.Count - 1; i++)
        {
            map.Terrain.AddDeck(span[i], span[i + 1], half, height);
        }

        map.AddBridge(new Bridge([.. span], half, height, water));
    }

    /// <summary>
    /// The stretches of a line (sampled by <paramref name="sample"/>) that go right across
    /// <paramref name="corridor"/>, each with the corridor's level and distance where it enters.
    /// A line that only runs along the bank is not a crossing.
    /// </summary>
    private static List<(List<Vector2> Span, float Level, float Along)> Spans(Corridor corridor, float reach, float length, Func<float, Vector2> sample)
    {
        const float step = 2f;
        List<(List<Vector2>, float, float)> found = [];
        List<Vector2> span = [];
        float nearest = float.MaxValue;
        float level = 0f;
        float along = 0f;
        for (float d = 0f; d <= length + step; d += step)
        {
            Vector2 p = sample(MathF.Min(d, length));
            float at = 0f;
            float here = 0f;
            float lateral = 0f;
            bool inside = d <= length && corridor.Closest(p, reach, out lateral, out at, out here);
            if (inside)
            {
                nearest = MathF.Min(nearest, MathF.Abs(lateral));
                if (span.Count == 0)
                {
                    level = here;
                    along = at;
                }

                span.Add(p);
                continue;
            }

            if (span.Count >= 2 && nearest < corridor.HalfWidth * 0.5f)
            {
                found.Add((span, level, along));
                span = [];
            }
            else
            {
                span.Clear();
            }

            nearest = float.MaxValue;
        }

        return found;
    }

    /// <summary>Bogor Paledang, where the Sukabumi trains leave from: one short platform under a roof.</summary>
    private static void BuildPaledang(WorldMap map)
    {
        Corridor? line = map.Corridors.FirstOrDefault(c => c.NameKey == "rail.sukabumi");
        if (line is null)
        {
            return;
        }

        Vector2 start = line.Sample(34f, out Vector2 heading, out float level);
        Vector2 side = MathUtil.Left(heading);
        side = side.X > 0f ? side : -side;
        Vector2 middle = start + (side * 6.6f);
        float yaw = MathF.Atan2(-side.Y, side.X);
        float top = level + 1f;
        map.Terrain.AddDeck(middle - (heading * 26f), middle + (heading * 26f), 3.6f, top);
        map.AddSlab(new Slab(middle, 3.6f, 30f, yaw, level - 0.6f, top, SlabLook.Concrete));
        map.AddSlab(new Slab(middle, 4.1f, 24f, yaw, top + 3.4f, top + 3.6f, SlabLook.Zinc));
        for (float t = -20f; t <= 20f; t += 10f)
        {
            map.AddSlab(new Slab(middle + (heading * t) + (side * 2.4f), 0.14f, 0.14f, yaw, top, top + 3.4f, SlabLook.Dark));
        }

        map.AddSign(new Sign(middle + (side * 4.2f), top + 4.3f, MathUtil.YawOf(side), 14f, "poi.paledang"));
        map.Reserved.Add((middle, 34f));
    }

    /// <summary>Stasiun Bogor: a hall facing its forecourt, one platform under a zinc canopy, and the buffer stop.</summary>
    private static Building? BuildStation(WorldMap map)
    {
        Vector2 side = StationSide;
        Roadside(map, PoiId.StasiunBogor, "poi.stasiunbogor", Terminus + (TrackHeading * 60f) + (side * 46f), 6f);
        if (map.Railway is not { } rail)
        {
            return null;
        }

        rail.Sample(70f, out _, out float level);
        // The hall stands between the platform and the forecourt, its long side to the road.
        Vector2 hallCentre = Terminus + (TrackHeading * 62f) + (side * 23f);
        Building hall = new(hallCentre, 24f, 8f, MathUtil.YawOf(-side), 7.6f, BuildingStyle.Kampus, 31u, 0f);
        map.Reserved.Add((hallCentre, 27f));
        map.AddSign(new Sign(hallCentre + (side * 8.25f), FoundationHeight(map, hall) + 6.6f, MathUtil.YawOf(side), 22f, "poi.stasiunbogor"));

        // One platform, 130 m long, a metre above the rails.
        const float platformHalf = 4.3f;
        float yaw = MathF.Atan2(-side.Y, side.X);
        float top = level + 1f;
        Vector2 south = Terminus + (TrackHeading * 8f) + (side * 7.5f);
        Vector2 north = south + (TrackHeading * 130f);
        Vector2 middle = (south + north) * 0.5f;
        map.Terrain.AddDeck(south + (TrackHeading * platformHalf), north - (TrackHeading * platformHalf), platformHalf, top);
        map.AddSlab(new Slab(middle, platformHalf, 65f, yaw, level - 0.6f, top, SlabLook.Concrete));
        map.AddSlab(new Slab(middle, platformHalf + 0.5f, 52f, yaw, top + 3.6f, top + 3.8f, SlabLook.Zinc));
        for (float t = -48f; t <= 48f; t += 12f)
        {
            map.AddSlab(new Slab(middle + (TrackHeading * t) + (side * 2.6f), 0.14f, 0.14f, yaw, top, top + 3.6f, SlabLook.Dark));
        }

        map.AddSlab(new Slab(Terminus - (TrackHeading * 3f), 1.7f, 0.5f, yaw, level, level + 1.3f, SlabLook.Dark));
        map.Reserved.Add((middle, 70f));
        return hall;
    }

    /// <summary>
    /// Jittered street lattice inside a district. Every <paramref name="arterialEvery"/>-th line is
    /// an arterial; a fraction of the lesser streets is dropped so blocks are not uniform.
    /// </summary>
    private static List<RoadNode> BuildGrid(
        WorldMap map, ref Rng rng, District district, float spacing, float jitter, RoadKind kind, int arterialEvery, float dropRate, Func<Vector2, bool> excluded, Func<Vector2, Vector2, bool, bool>? allowed)
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
                if (i + 1 < size && lattice[i + 1, j] is { } east && (mainRow || j == half || !rng.Chance(dropRate))
                    && (allowed?.Invoke(node.Position, east.Position, mainRow) ?? true))
                {
                    roads.AddEdge(node, east, mainRow ? RoadKind.Arterial : kind, name);
                }

                if (j + 1 < size && lattice[i, j + 1] is { } south && (mainColumn || i == half || !rng.Chance(dropRate))
                    && (allowed?.Invoke(node.Position, south.Position, mainColumn) ?? true))
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
        // Tugu Kujang stands at the corner of Pajajaran and Otista, just outside the loop.
        RoadNode kujangNode = NearestOf(ring, Vector2.Zero);
        Vector2 outward = Vector2.Normalize(kujangNode.Position - ParkCenter);
        AddPoi(map, PoiId.TuguKujang, "poi.tugukujang", kujangNode.Position + (outward * 22f), kujangNode.Position, MathUtil.YawOf(-outward), 20f);

        // The main gate of the Gardens is on the south side, by the zoology museum.
        RoadNode gateNode = NearestOf(ring, new Vector2(-641f, 173f));
        Vector2 inward = Vector2.Normalize(ParkCenter - gateNode.Position);
        AddPoi(map, PoiId.GerbangKebunRaya, "poi.gerbangkebunraya", gateNode.Position + (inward * 16f), gateNode.Position, MathUtil.YawOf(-inward), 20f);
        // The palace lawn where the deer graze, in the north-west of the Gardens.
        AddPoi(map, PoiId.IstanaRusa, "poi.istanarusa", new Vector2(-800f, -470f), gateNode.Position, 0f, 0f);

        Roadside(map, PoiId.PasarAnyar, "poi.pasaranyar", new Vector2(-1_429f, -1_083f), 14f);
        Roadside(map, PoiId.GangSelot, "poi.gangselot", new Vector2(-2_330f, -1_400f), 5f);
        Shore(map, PoiId.Empang, "poi.empang", map.Ponds[0]);
        Roadside(map, PoiId.RumahUjang, "poi.rumahujang", new Vector2(-1_180f, 800f), 10f);
        Roadside(map, PoiId.WarungBandrek, "poi.warungbandrek", new Vector2(-230f, 470f), 9f);
        Roadside(map, PoiId.LapanganBadminton, "poi.badminton", new Vector2(-1_300f, 1_150f), 14f);
        Roadside(map, PoiId.Karaoke, "poi.karaoke", new Vector2(520f, -820f), 10f);
        Roadside(map, PoiId.SekolahBinaInsani, "poi.binainsani", new Vector2(-1_450f, -2_250f), 16f);
        Roadside(map, PoiId.LapanganStm, "poi.lapanganstm", new Vector2(700f, 500f), 18f);
        Roadside(map, PoiId.TerminalDramaga, "poi.terminaldramaga", new Vector2(-8_200f, -4_420f), 14f);
        Roadside(map, PoiId.KampusIpb, "poi.kampusipb", new Vector2(-8_787f, -4_967f), 16f);
        Shore(map, PoiId.SituGede, "poi.situgede", map.Ponds[1]);
        Roadside(map, PoiId.JalanBaru, "poi.jalanbaru", new Vector2(-2_300f, -4_600f), 12f);
        Roadside(map, PoiId.SirkuitSentul, "poi.sirkuitsentul", new Vector2(5_740f, -7_290f), 16f);
        Roadside(map, PoiId.Ciawi, "poi.ciawi", new Vector2(4_970f, 6_100f), 12f);
        Roadside(map, PoiId.PuncakPass, "poi.puncakpass", new Vector2(16_050f, 9_080f), 14f);
        Roadside(map, PoiId.VillaMisterius, "poi.villamisterius", new Vector2(16_500f, 8_500f), 22f);
        Roadside(map, PoiId.KebunTeh, "poi.kebunteh", new Vector2(15_300f, 9_450f), 12f);
    }

    private enum PlaceKind
    {
        /// <summary>Open ground: a field, a square, a park.</summary>
        Open,
        Mall,
        /// <summary>A public building: museum, palace, town hall, church, mosque.</summary>
        Hall,
        Fountain,
        Gate,
        /// <summary>A roof on posts: the bus terminal.</summary>
        Shelter,
    }

    /// <summary>What sets a public building apart from its neighbours.</summary>
    private enum Feature
    {
        None,
        /// <summary>A row of columns under a pediment.</summary>
        Portico,
        /// <summary>Columns and a dome: the palace.</summary>
        PorticoDome,
        /// <summary>A tower with a spire over the door: the cathedral.</summary>
        Spire,
        /// <summary>A dome and a minaret: the mosque.</summary>
        DomeMinaret,
    }

    /// <param name="Model">The building's own model, when it has one (assets/blender/landmarks.py). The plot is Width x Depth times Scale.</param>
    /// <param name="Board">Where the model keeps a band free for its name: metres in front of the centre, to the left of it, above the ground, and the width of the board.</param>
    private readonly record struct RealPlace(
        PoiId Id, string Key, Vector2 At, PlaceKind Kind, float Width = 0f, float Depth = 0f, float Height = 0f, Feature Feature = Feature.None,
        string? Model = null, float Scale = 1f, Vector4 Board = default);

    /// <summary>The places people know Bogor by, each at its true position.</summary>
    private static readonly RealPlace[] RealPlaces =
    [
        new(PoiId.IstanaBogor, "poi.istanabogor", new(-847f, -378f), PlaceKind.Hall, 64f, 20f, 11.6f, Model: "istana_bogor", Board: new(10.03f, 0f, 8.5f, 14f)),
        new(PoiId.MuseumZoologi, "poi.museumzoologi", new(-900f, 238f), PlaceKind.Hall, 36f, 16f, 9.7f, Model: "museum_bogor", Board: new(8.03f, 0f, 6.8f, 12f)),
        new(PoiId.MuseumTanah, "poi.museumtanah", new(-786f, 265f), PlaceKind.Hall, 36f, 16f, 9.7f, Model: "museum_bogor", Scale: 0.84f, Board: new(8.03f, 0f, 6.8f, 12f)),
        new(PoiId.MuseumPeta, "poi.museumpeta", new(-963f, -1_795f), PlaceKind.Hall, 36f, 16f, 9.7f, Model: "museum_bogor", Scale: 1.1f, Board: new(8.03f, 0f, 6.8f, 12f)),
        new(PoiId.BalaiKota, "poi.balaikota", new(-1_257f, -701f), PlaceKind.Hall, 44f, 18f, 11.9f, Model: "balai_kota", Board: new(8.83f, 0f, 7.3f, 15f)),
        new(PoiId.Katedral, "poi.katedral", new(-1_311f, -500f), PlaceKind.Hall, 18f, 36f, 21f, Model: "katedral_bogor", Board: new(17.63f, 0f, 9.2f, 5f)),
        new(PoiId.MasjidRaya, "poi.masjidraya", new(392f, 645f), PlaceKind.Hall, 34f, 30f, 12f, Model: "masjid_raya", Board: new(13.75f, 1.5f, 10.35f, 8f)),
        new(PoiId.BotaniSquare, "poi.botanisquare", new(254f, -11f), PlaceKind.Mall, 70f, 46f, 18f),
        new(PoiId.BogorTradeMall, "poi.btm", new(-1_040f, 383f), PlaceKind.Mall, 60f, 42f, 24f),
        new(PoiId.YogyaJunction, "poi.yogyajunction", new(-964f, -1_382f), PlaceKind.Mall, 52f, 38f, 18f),
        new(PoiId.PlazaJambuDua, "poi.jambudua", new(464f, -3_608f), PlaceKind.Mall, 60f, 44f, 18f),
        new(PoiId.Ramayana, "poi.ramayana", new(250f, -3_420f), PlaceKind.Mall, 46f, 34f, 18f),
        new(PoiId.Ekalokasari, "poi.ekalokasari", new(1_351f, 2_246f), PlaceKind.Mall, 58f, 42f, 18f),
        new(PoiId.LotteMart, "poi.lottemart", new(-2_811f, -4_853f), PlaceKind.Mall, 64f, 46f, 12f),
        new(PoiId.BogorGreatMall, "poi.bogorgreatmall", new(-3_254f, -5_028f), PlaceKind.Mall, 66f, 46f, 18f),
        new(PoiId.PasarBogor, "poi.pasarbogor", new(-488f, 238f), PlaceKind.Mall, 44f, 30f, 12f),
        new(PoiId.SuryaKencana, "poi.suryakencana", new(-630f, 235f), PlaceKind.Gate),
        new(PoiId.AirMancur, "poi.airmancur", new(-906f, -2_260f), PlaceKind.Fountain),
        new(PoiId.LapanganSempur, "poi.sempur", new(-473f, -1_079f), PlaceKind.Open, 38f),
        new(PoiId.GorPajajaran, "poi.gorpajajaran", new(-808f, -2_683f), PlaceKind.Open, 44f),
        new(PoiId.AlunAlun, "poi.alunalun", new(-1_480f, -700f), PlaceKind.Open, 30f),
        new(PoiId.TamanKencana, "poi.tamankencana", new(-354f, -1_393f), PlaceKind.Open, 24f),
        new(PoiId.TerminalBaranangsiang, "poi.baranangsiang", new(104f, 293f), PlaceKind.Shelter, 46f, 22f, 5.2f),
        new(PoiId.BendungKatulampa, "poi.katulampa", new(3_400f, 4_000f), PlaceKind.Open, 12f),
    ];

    /// <summary>
    /// Puts every real place beside its nearest road and reserves its plot. Returns the buildings,
    /// which are added once the ordinary ones are in.
    /// </summary>
    private static List<Building> PlaceRealPlaces(WorldMap map)
    {
        List<Building> buildings = [];
        foreach (RealPlace place in RealPlaces)
        {
            if (InsideRing(place.At) && map.Roads.Nearest(place.At, 900f) is { } way)
            {
                // Inside the Gardens there are no streets: the place stays where it really is,
                // facing the nearest stretch of the loop.
                AddPoi(map, place.Id, place.Key, place.At, way.Point, MathUtil.YawOf(Vector2.Normalize(way.Point - place.At)), 8f);
            }
            else
            {
                Roadside(map, place.Id, place.Key, place.At, place.Kind == PlaceKind.Open ? MathF.Max(8f, place.Width) : 8f);
            }

            Poi poi = map[place.Id];
            // Away from the road: where the building stands, behind the spot people arrive at.
            Vector2 back = -MathUtil.Forward(poi.Yaw);
            Vector2 across = MathUtil.Left(back);
            float level = map.Terrain.HeightAt(poi.Position);
            switch (place.Kind)
            {
                case PlaceKind.Mall or PlaceKind.Hall:
                    float plotWidth = place.Width * place.Scale;
                    float plotDepth = place.Depth * place.Scale;
                    Vector2 centre = poi.Position + (back * ((plotDepth * 0.5f) + 6f));
                    // Keep off the river bank and the track: slide along the road until clear.
                    float room = (MathF.Max(plotWidth, plotDepth) * 0.71f) + 8f;
                    foreach (float shift in (ReadOnlySpan<float>)[0f, 25f, -25f, 50f, -50f, 80f, -80f, 120f, -120f])
                    {
                        if (map.Terrain.CorridorClearance(centre + (across * shift)) > room)
                        {
                            centre += across * shift;
                            break;
                        }
                    }

                    BuildingStyle style = place.Model is not null ? BuildingStyle.Landmark : place.Kind == PlaceKind.Mall ? BuildingStyle.Mall : BuildingStyle.Kampus;
                    Building building = new(centre, plotWidth * 0.5f, plotDepth * 0.5f, MathUtil.YawOf(back), place.Height * place.Scale, style, (uint)place.Id + 77u, 0f);
                    float ground = FoundationHeight(map, building);
                    buildings.Add(building);
                    map.Reserved.Add((centre, (MathF.Max(plotWidth, plotDepth) * 0.72f) + 4f));
                    if (place.Model is not null)
                    {
                        // The building is a model of its own; the box above is only what one bumps into.
                        map.AddProp(new Prop(place.Model, centre, poi.Yaw, place.Scale, 0f, Landmark: true));
                        Vector2 board = centre - (back * (place.Board.X * place.Scale)) + (MathUtil.Left(-back) * (place.Board.Y * place.Scale));
                        map.AddSign(new Sign(board, map.Terrain.HeightAt(centre) + (place.Board.Z * place.Scale), poi.Yaw, place.Board.W * place.Scale, place.Key));
                        break;
                    }

                    // The roof line as the view draws it: whole storeys of the style's height.
                    float storey = style == BuildingStyle.Mall ? 6f : 3.8f;
                    float roof = ground + (Math.Max(1, (int)MathF.Round(place.Height / storey)) * storey);
                    Vector2 front = centre - (back * (place.Depth * 0.5f));
                    float slabYaw = MathF.Atan2(-across.Y, across.X);
                    bool portico = place.Feature is Feature.Portico or Feature.PorticoDome;
                    // The name goes over the door: on the wall, or on the pediment when there are columns.
                    map.AddSign(new Sign(front - (back * (portico ? 3.3f : 0.25f)), portico ? roof - 0.9f : roof - 1.7f, poi.Yaw,
                        MathF.Min(place.Width * 0.62f, 30f), place.Key));
                    switch (place.Feature)
                    {
                        case Feature.Portico or Feature.PorticoDome:
                            float span = MathF.Min(place.Width * 0.5f, 26f);
                            for (int i = 0; i < 6; i++)
                            {
                                Vector2 column = front - (back * 2.6f) + (across * (((i / 5f) - 0.5f) * span));
                                map.AddSlab(new Slab(column, 0.45f, 0.45f, slabYaw, ground - 1f, roof - 1.8f, SlabLook.White));
                            }

                            map.AddSlab(new Slab(front - (back * 1.5f), (span * 0.5f) + 1.2f, 1.7f, slabYaw, roof - 1.8f, roof, SlabLook.White));
                            if (place.Feature == Feature.PorticoDome)
                            {
                                for (int i = 0; i < 4; i++)
                                {
                                    float size = 7f - (i * 1.6f);
                                    map.AddSlab(new Slab(centre, size, size, slabYaw, roof + (i * 1.5f), roof + ((i + 1) * 1.5f), i == 0 ? SlabLook.White : SlabLook.Copper));
                                }
                            }

                            break;

                        case Feature.Spire:
                            Vector2 tower = front + (back * 3f);
                            map.AddSlab(new Slab(tower, 3f, 3f, slabYaw, roof, roof + 9f, SlabLook.White));
                            for (int i = 0; i < 5; i++)
                            {
                                float size = 2.6f - (i * 0.5f);
                                map.AddSlab(new Slab(tower, size, size, slabYaw, roof + 9f + (i * 2.6f), roof + 9f + ((i + 1) * 2.6f), SlabLook.Dark));
                            }

                            break;

                        case Feature.DomeMinaret:
                            for (int i = 0; i < 5; i++)
                            {
                                float size = 9f - (i * 1.8f);
                                map.AddSlab(new Slab(centre, size, size, slabYaw, roof + (i * 1.6f), roof + ((i + 1) * 1.6f), SlabLook.Copper));
                            }

                            Vector2 minaret = front + (across * ((place.Width * 0.5f) + 4f));
                            map.AddSlab(new Slab(minaret, 1.5f, 1.5f, slabYaw, ground - 1f, roof + 16f, SlabLook.White));
                            map.AddSlab(new Slab(minaret, 2.1f, 2.1f, slabYaw, roof + 12f, roof + 12.6f, SlabLook.White));
                            map.AddSlab(new Slab(minaret, 0.9f, 0.9f, slabYaw, roof + 16f, roof + 19f, SlabLook.Copper));
                            map.Reserved.Add((minaret, 4f));
                            break;
                    }

                    break;

                case PlaceKind.Fountain:
                    Vector2 pool = poi.Position + (back * 9f);
                    float yaw = MathF.Atan2(-across.Y, across.X);
                    map.AddSlab(new Slab(pool, 6f, 6f, yaw, level - 1f, level + 0.7f, SlabLook.Concrete));
                    map.AddSlab(new Slab(pool, 1.1f, 1.1f, yaw, level + 0.7f, level + 3.4f, SlabLook.Concrete));
                    map.AddSlab(new Slab(pool, 2.4f, 2.4f, yaw, level + 3.4f, level + 3.7f, SlabLook.Zinc));
                    map.Reserved.Add((pool, 12f));
                    break;

                case PlaceKind.Gate:
                    // Lawang Suryakencana, the gate of the old Chinese quarter, astride the street.
                    float gateYaw = poi.Yaw + (MathF.PI * 0.5f);
                    map.AddProp(new Prop("lawang_suryakencana", poi.RoadPoint, gateYaw, 1f, 0f, Landmark: true));
                    foreach (float face in (ReadOnlySpan<float>)[0f, MathF.PI])
                    {
                        Vector2 facing = MathUtil.Forward(gateYaw + face);
                        map.AddSign(new Sign(poi.RoadPoint + (facing * 0.26f), map.Terrain.HeightAt(poi.RoadPoint) + 7.75f, gateYaw + face, 8f, place.Key));
                    }

                    break;

                case PlaceKind.Shelter:
                    Vector2 middle = poi.Position + (back * ((place.Depth * 0.5f) + 5f));
                    float turn = MathF.Atan2(-across.Y, across.X);
                    map.AddSlab(new Slab(middle, place.Width * 0.5f, place.Depth * 0.5f, turn, level + place.Height, level + place.Height + 0.25f, SlabLook.Zinc));
                    for (int i = -2; i <= 2; i++)
                    {
                        foreach (float side in (ReadOnlySpan<float>)[-1f, 1f])
                        {
                            Vector2 post = middle + (across * (i * place.Width * 0.22f)) + (back * (side * place.Depth * 0.42f));
                            map.AddSlab(new Slab(post, 0.2f, 0.2f, turn, level - 0.5f, level + place.Height, SlabLook.Dark));
                        }
                    }

                    map.Reserved.Add((middle, MathF.Max(place.Width, place.Depth) * 0.6f));
                    map.AddSign(new Sign(middle - (back * (place.Depth * 0.5f)), level + place.Height + 1.2f, poi.Yaw, 26f, place.Key));
                    break;
            }
        }

        return buildings;
    }

    /// <summary>Snaps a wished-for location to the nearest road and puts the place on its left kerb.</summary>
    private static void Roadside(WorldMap map, PoiId id, string nameKey, Vector2 wish, float clearRadius)
    {
        // Never on a toll road: nobody can stop there.
        RoadHit hit = map.Roads.Nearest(wish, 900f, e => e.Kind != RoadKind.Toll) ?? throw new InvalidOperationException($"No road near {id}.");
        Vector2 left = MathUtil.Left(hit.Direction);
        // Choose the side the wish was on, so a pond-side place stays by its pond.
        float sign = Vector2.Dot(wish - hit.Point, left) >= 0f ? 1f : -1f;
        Vector2 position = hit.Point + (left * sign * ((hit.Edge.Width * 0.5f) + 2.5f + clearRadius));
        AddPoi(map, id, nameKey, position, hit.Point, MathUtil.YawOf(-left * sign), clearRadius);
    }

    /// <summary>A place on the bank of a pond, on the side facing the nearest road.</summary>
    private static void Shore(WorldMap map, PoiId id, string nameKey, Pond pond)
    {
        RoadHit hit = map.Roads.Nearest(pond.Center, 900f, e => e.Kind != RoadKind.Toll) ?? throw new InvalidOperationException($"No road near {id}.");
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

        // Cameras also watch the places where crowds gather.
        foreach (PoiId id in (ReadOnlySpan<PoiId>)[PoiId.PasarAnyar, PoiId.StasiunBogor, PoiId.BalaiKota, PoiId.AlunAlun,
            PoiId.TerminalBaranangsiang, PoiId.PasarBogor, PoiId.BotaniSquare, PoiId.GerbangKebunRaya])
        {
            Poi poi = map[id];
            Vector2 away = poi.RoadPoint - poi.Position;
            Vector2 mast = poi.RoadPoint - (Vector2.Normalize(away) * 1.2f) + (MathUtil.Left(Vector2.Normalize(away)) * 9f);
            if (away.LengthSquared() > 1f && !map.Roads.IsOnRoad(mast, 0.3f))
            {
                map.AddProp(new Prop("cctv", mast, MathUtil.YawOf(away), 1f, 0.2f));
                map.Reserved.Add((mast, 1f));
            }
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
            if (edge.Kind == RoadKind.Toll || edge.NameKey == "road.sirkuit")
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
                    if (!rng.Chance(fill) || map.DistrictAt(p) != district || map.Roads.Nearest(p, 120f) is not { } road
                        || road.Edge.NameKey == "road.sirkuit")
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
        if (InsideRing(b.Center) || NearPond(map, b.Center, b.HalfWidth + b.HalfDepth + 6f))
        {
            return false;
        }

        float reach = MathF.Max(b.HalfWidth, b.HalfDepth) * 1.42f;
        // Keep off the river banks and clear of the track.
        if (map.Terrain.CorridorClearance(b.Center) < reach + 8f)
        {
            return false;
        }

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

        PlaceSignals(map);
    }

    /// <summary>
    /// Traffic lights where main roads meet in town, one for every arm, taking turns; and a
    /// camera mast to watch each of those junctions.
    /// </summary>
    private static void PlaceSignals(WorldMap map)
    {
        List<Vector2> lit = [];
        List<(RoadEdge Edge, Vector2 Post, Vector2 Travel)> arms = [];
        foreach (RoadNode node in map.Roads.Nodes)
        {
            if (map.DistrictAt(node.Position) is null)
            {
                continue;
            }

            int main = 0;
            bool plain = true;
            arms.Clear();
            foreach (int id in node.Edges)
            {
                RoadEdge edge = map.Roads.Edges[id];
                main += edge.Kind == RoadKind.Arterial ? 1 : 0;
                plain &= edge.Kind != RoadKind.Toll && edge.From != edge.To;
                if (edge.Kind is not (RoadKind.Arterial or RoadKind.Street) || edge.Length < 30f)
                {
                    continue;
                }

                // The post stands at the left kerb, 11 m short of the junction, and not in the
                // mouth of another road: an arm with nowhere to put it goes without a light.
                bool arriving = edge.To == node.Id;
                Vector2 point = edge.Sample(arriving ? edge.Length - 11f : 11f, out Vector2 direction);
                Vector2 travel = arriving ? direction : -direction;
                Vector2 post = point + (MathUtil.Left(travel) * ((edge.Width * 0.5f) + 0.7f));
                if (!map.Roads.IsOnRoad(post))
                {
                    arms.Add((edge, post, travel));
                }
            }

            // Only where big roads really cross, and real junctions come as clusters of points
            // (one per carriageway): one set of lights for each.
            if (!plain || main < 3 || arms.Count < 3 || lit.Exists(p => Vector2.DistanceSquared(p, node.Position) < 220f * 220f))
            {
                continue;
            }

            lit.Add(node.Position);
            for (int slot = 0; slot < arms.Count; slot++)
            {
                (RoadEdge edge, Vector2 post, Vector2 travel) = arms[slot];
                float yaw = MathUtil.YawOf(-travel);
                map.AddSignal(new TrafficSignal(post, yaw, node.Id, edge.Id, slot, arms.Count));
                map.AddProp(new Prop("lampu_merah", post, yaw, 1f, 0.15f));
                Vector2 mast = post - (travel * 6f) - (MathUtil.Left(travel) * (edge.Width + 1.6f));
                if (slot == 0 && !map.Roads.IsOnRoad(mast, 0.3f))
                {
                    map.AddProp(new Prop("cctv", mast, MathUtil.YawOf(MathUtil.Left(travel)), 1f, 0.2f));
                }
            }
        }
    }

    private static readonly string[] BannerKeys =
    [
        "banner.dirgahayu", "banner.pecellele", "banner.khitanan", "banner.futsal", "banner.diskon",
        "banner.sampah", "banner.reuni", "banner.kredit", "banner.asinan", "banner.tertib",
    ];

    private static readonly string[] ShopKeys =
    [
        "shop.apotek", "shop.sinarjaya", "shop.fotokopi", "shop.laundry", "shop.bakso",
        "shop.pulsa", "shop.warungnasi", "shop.bengkel", "shop.lapistalas", "shop.tokomas",
    ];

    /// <summary>
    /// What makes a street look lived in: cloth banners between bamboo poles, the light boxes of
    /// the shops, and a big LED screen at the busiest corners. Placed before the houses, which
    /// then keep off them.
    /// </summary>
    /// <summary>
    /// Water plants: lily pads and water hyacinth afloat near the banks of the ponds and in the
    /// slack water at the river's edge, reeds rooted in the shallows. They are scenery only
    /// (no collision), so swimming and fishing are not in their way.
    /// </summary>
    private static void PlaceWaterPlants(WorldMap map)
    {
        Rng rng = new(map.Seed + 4242u);
        Terrain terrain = map.Terrain;
        foreach (Pond pond in map.Ponds)
        {
            // The bigger the pond the more of everything; the middle stays open water.
            int clumps = (int)(pond.Radius * 0.55f);
            for (int i = 0; i < clumps; i++)
            {
                float angle = rng.Range(0f, MathUtil.Tau);
                Vector2 out_ = new(MathF.Cos(angle), MathF.Sin(angle));
                float roll = rng.NextFloat();
                if (roll < 0.4f)
                {
                    // Reeds stand with their feet in the water right at the bank.
                    Vector2 p = pond.Center + (out_ * (pond.Radius * rng.Range(0.93f, 0.99f)));
                    map.AddProp(new Prop("gelagah", p, rng.Range(0f, MathUtil.Tau), rng.Range(0.8f, 1.3f), 0f));
                }
                else
                {
                    Vector2 p = pond.Center + (out_ * (pond.Radius * rng.Range(0.5f, 0.9f)));
                    if (terrain.WaterLevelAt(p) is { } level && level - terrain.GroundAt(p.X, p.Y) > 0.2f)
                    {
                        string model = roll < 0.75f ? "teratai" : "eceng_gondok";
                        map.AddProp(new Prop(model, p, rng.Range(0f, MathUtil.Tau), rng.Range(0.8f, 1.35f), 0f, Afloat: true));
                    }
                }
            }
        }

        foreach (Corridor corridor in terrain.Corridors)
        {
            if (corridor.Kind != CorridorKind.River)
            {
                continue;
            }

            for (float along = 20f; along < corridor.Length - 20f; along += 16f)
            {
                float roll = rng.NextFloat();
                float side = rng.Chance(0.5f) ? 1f : -1f;
                float reach = rng.Range(0.72f, 0.97f);
                float turn = rng.Range(0f, MathUtil.Tau);
                float size = rng.Range(0.8f, 1.3f);
                if (roll > 0.5f)
                {
                    continue;
                }

                Vector2 centre = corridor.Sample(along, out Vector2 direction, out _);
                Vector2 p = centre + (MathUtil.Left(direction) * (side * corridor.HalfWidth * reach));
                if (terrain.WaterLevelAt(p) is not { } level || map.Roads.IsOnRoad(p, 1f))
                {
                    continue;
                }

                float depth = level - terrain.GroundAt(p.X, p.Y);
                if (roll < 0.3f && depth < 0.9f)
                {
                    map.AddProp(new Prop("gelagah", p, turn, size, 0f));
                }
                else if (depth > 0.2f)
                {
                    map.AddProp(new Prop(roll < 0.42f ? "eceng_gondok" : "teratai", p, turn, size, 0f, Afloat: true));
                }
            }
        }
    }

    private static void PlaceStreetLife(WorldMap map)
    {
        Rng rng = new(map.Seed + 9191u);
        bool Free(Vector2 position, float radius)
        {
            if (map.Roads.IsOnRoad(position, radius * 0.5f) || map.IsInsideBuilding(position, radius) || map.Terrain.WaterLevelAt(position) is not null)
            {
                return false;
            }

            foreach ((Vector2 centre, float taken) in map.Reserved)
            {
                if (Vector2.DistanceSquared(centre, position) < (taken + radius) * (taken + radius))
                {
                    return false;
                }
            }

            return true;
        }

        foreach (PoiId id in (ReadOnlySpan<PoiId>)[PoiId.TuguKujang, PoiId.BotaniSquare, PoiId.PasarAnyar, PoiId.StasiunBogor,
            PoiId.SuryaKencana, PoiId.TerminalBaranangsiang, PoiId.AlunAlun])
        {
            Poi poi = map[id];
            if (map.Roads.Nearest(poi.RoadPoint, 40f, e => e.Kind is RoadKind.Arterial or RoadKind.Street) is not { } hit)
            {
                continue;
            }

            foreach (float along in (ReadOnlySpan<float>)[28f, -28f, 44f, -44f, 60f, -60f])
            {
                float at = hit.Distance + along;
                if (at < 8f || at > hit.Edge.Length - 8f)
                {
                    continue;
                }

                // Across the road from the place itself, turned to face whoever comes from it.
                Vector2 point = hit.Edge.Sample(at, out Vector2 direction);
                float side = Vector2.Dot(poi.Position - point, MathUtil.Left(direction)) > 0f ? -1f : 1f;
                Vector2 position = point + (MathUtil.Left(direction) * side * ((hit.Edge.Width * 0.5f) + 3f));
                if (!Free(position, 3.5f))
                {
                    continue;
                }

                map.AddProp(new Prop("videotron", position, MathUtil.YawOf(direction * -MathF.Sign(along)), 1f, 0.7f, Landmark: true));
                map.Reserved.Add((position, 4.5f));
                break;
            }
        }

        int banners = 0;
        int boxes = 0;
        foreach (RoadEdge edge in map.Roads.Edges)
        {
            if (edge.Kind is not (RoadKind.Street or RoadKind.Arterial) || edge.Length < 50f)
            {
                continue;
            }

            Vector2 mid = edge.Sample(edge.Length * 0.5f, out _);
            if (map.DistrictAt(mid)?.Id is not (DistrictId.PusatKota or DistrictId.PasarAnyar or DistrictId.Empang
                or DistrictId.SuryaKencana or DistrictId.GangSelot or DistrictId.Dramaga))
            {
                continue;
            }

            for (float t = 22f; t < edge.Length - 22f; t += 31f)
            {
                float roll = rng.NextFloat();
                float sign = rng.Chance(0.5f) ? 1f : -1f;
                int pick = rng.Range(0, BannerKeys.Length);
                Vector2 point = edge.Sample(t, out Vector2 direction);
                Vector2 left = MathUtil.Left(direction) * sign;
                if (roll < 0.1f && banners < 110)
                {
                    // Along the kerb, facing the road.
                    Vector2 position = point + (left * ((edge.Width * 0.5f) + 1.5f));
                    if (!Free(position, 2.6f))
                    {
                        continue;
                    }

                    float yaw = MathUtil.YawOf(-left);
                    map.AddProp(new Prop("spanduk", position, yaw, 1f, 0f));
                    map.AddSign(new Sign(position, map.Terrain.HeightAt(position) + 1.9f, yaw, 4f, BannerKeys[pick], SignLook.Banner));
                    map.Reserved.Add((position, 2.8f));
                    banners++;
                }
                else if (roll < 0.26f && boxes < 130)
                {
                    // Square to the road, so it is read while walking or driving along it.
                    Vector2 position = point + (left * ((edge.Width * 0.5f) + 0.8f));
                    if (!Free(position, 1f))
                    {
                        continue;
                    }

                    float yaw = MathUtil.YawOf(direction);
                    map.AddProp(new Prop("light_box", position, yaw, 1f, 0.12f));
                    foreach (float face in (ReadOnlySpan<float>)[0f, MathF.PI])
                    {
                        Vector2 facing = MathUtil.Forward(yaw + face);
                        map.AddSign(new Sign(position + (facing * 0.13f), map.Terrain.HeightAt(position) + 3.05f, yaw + face, 1.5f, ShopKeys[pick], SignLook.LightBox));
                    }

                    map.Reserved.Add((position, 1.2f));
                    boxes++;
                }
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

                if (map.Roads.IsOnRoad(p, 2.2f) || map.IsInsideBuilding(p, 1.2f) || NearPond(map, p, 2f) || IsReserved(map, p)
                    || map.Terrain.CorridorClearance(p) < 4f)
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
                    if (map.LandUseAt(p) != LandUse.KebunTeh || map.Roads.IsOnRoad(p, 1.5f) || map.IsInsideBuilding(p, 1f)
                        || map.Terrain.CorridorClearance(p) < 3f)
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
