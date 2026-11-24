using System.Numerics;
using BarudakBogor.Numerics;
using BarudakBogor.World;
using ThreeNet;

namespace BarudakBogor.Game.Rendering;

/// <summary>
/// Every surface texture of the world is painted here at start-up: one atlas holds all the
/// building facades (so a whole chunk of town is a single draw call), plus the road surfaces.
/// Nothing is loaded from disk, which keeps the download small and the look consistent.
/// </summary>
public sealed class WorldTextures
{
    public const int AtlasTiles = 8;
    private const int Tile = 128;
    private const int AtlasSize = AtlasTiles * Tile;

    /// <summary>Tiles per facade variant: lit window, dark window, plain wall, ground floor.</summary>
    public const int TilesPerVariant = 4;

    public const int RoofTerracotta = 56;
    public const int RoofConcrete = 57;
    public const int RoofZinc = 58;
    public const int RoofDark = 59;
    public const int PlasterWhite = 60;
    public const int Copper = 61;
    public const int AwningStripe = 62;
    public const int TankBlue = 63;

    private readonly record struct Variant(int Wall, int Trim, int Glass, int Kind);

    // Kind: 0 shophouse, 1 house, 2 kiosk, 3 glass office, 4 kampung, 5 campus, 6 villa, 7 mall.
    private static readonly Variant[] Variants =
    [
        new(0xE9DFC4, 0x9B4A2E, 0x2B4A5C, 0), new(0xBFD6DE, 0x35607A, 0x26404F, 0),
        new(0xCFE0BC, 0x4D7A3A, 0x2B4A5C, 0), new(0xF0C9A8, 0xA8552F, 0x30424C, 0),
        new(0xF2EEE2, 0x6B7B52, 0x33505E, 1), new(0xF3E3A6, 0x8A6A2A, 0x33505E, 1),
        new(0x3FB8AE, 0xE8B43A, 0x1F3A40, 2),
        new(0x6F93A8, 0xC9D6DC, 0x3E6E88, 3), new(0xB9B6AD, 0x7C7A72, 0x2F4756, 3),
        new(0xB4AEA2, 0x6E665A, 0x2A3840, 4), new(0xE2B7B0, 0x8E5A52, 0x2A3840, 4),
        new(0xEFEAD9, 0xA2452F, 0x2E4A58, 5),
        new(0xD8D2C2, 0x4A3526, 0x1E2A30, 6),
        new(0xD9C7A3, 0xB5503A, 0x355869, 7),
    ];

    private Shader? _ground;
    private bool _groundFailed;

    /// <summary>The ground and road surface shader, or null when it did not compile on this machine.</summary>
    public Shader? GroundShader(Scene scene)
    {
        if (_ground is null && !_groundFailed)
        {
            try
            {
                _ground = scene.CreateShader(Rendering.GroundShader.Source, name: "barudak.ground");
            }
            catch (ThreeNetException)
            {
                _groundFailed = true;
            }
        }

        return _ground;
    }

    public WorldTextures(Scene scene)
    {
        byte[] color = new byte[AtlasSize * AtlasSize * 4];
        byte[] glow = new byte[AtlasSize * AtlasSize * 4];
        for (int i = 3; i < glow.Length; i += 4)
        {
            glow[i] = 255;
        }

        for (int v = 0; v < Variants.Length; v++)
        {
            for (int t = 0; t < TilesPerVariant; t++)
            {
                PaintFacade(color, glow, (v * TilesPerVariant) + t, Variants[v], t, (uint)((v * 7) + t));
            }
        }

        PaintRoof(color, RoofTerracotta, 0xA9502F, 0x82371E, ridges: 12, courses: 16);
        PaintRoof(color, RoofConcrete, 0x8C8A84, 0x76746F, ridges: 0, courses: 0);
        PaintRoof(color, RoofZinc, 0x98A2A6, 0x7A8488, ridges: 24, courses: 0);
        PaintRoof(color, RoofDark, 0x3C312C, 0x29211D, ridges: 12, courses: 16);
        PaintRoof(color, PlasterWhite, 0xF4F1E8, 0xE2DDD0, ridges: 0, courses: 0);
        PaintRoof(color, Copper, 0x5CA088, 0x3B7D6A, ridges: 6, courses: 0);
        PaintAwning(color, AwningStripe);
        PaintRoof(color, TankBlue, 0x2665A8, 0x1C4E86, ridges: 0, courses: 16);

        Texture atlas = scene.CreateTexture(AtlasSize, AtlasSize, color);
        atlas.SetSampler(WrapMode.ClampToEdge, WrapMode.ClampToEdge, true, true, 8);
        Texture glowAtlas = scene.CreateTexture(AtlasSize, AtlasSize, glow);
        glowAtlas.SetSampler(WrapMode.ClampToEdge, WrapMode.ClampToEdge, true, true, 1);
        Buildings = scene.CreateMaterial(MaterialOptions.Pbr(Vector4.One, 0f, 0.88f) with
        {
            BaseColorMap = atlas,
            EmissiveMap = glowAtlas,
            Emissive = Vector3.One,
            EmissiveIntensity = 0f,
        });

        RoadMain = Road(scene, lanes: 1, edgeLines: true, paving: false, yellowCenter: true);
        RoadToll = Road(scene, lanes: 2, edgeLines: true, paving: false, yellowCenter: false);
        RoadGang = Road(scene, lanes: 0, edgeLines: false, paving: true, yellowCenter: false);
        Asphalt = Road(scene, lanes: 0, edgeLines: false, paving: false, yellowCenter: false);
        Sidewalk = PaintSidewalk(scene);
        Rail = Track(scene);
        _scene = scene;
        // The Ciliwung runs brown-green; the ponds are clearer.
        Water = CreateWater(pond: false, mud: 0.75f, default, 0f);
    }

    public Material Buildings { get; }

    public Material RoadMain { get; }

    public Material RoadToll { get; }

    public Material RoadGang { get; }

    public Material Asphalt { get; }

    /// <summary>Bogor pedestrian sidewalk: concrete paving slabs with iconic black-and-white painted kanstin curb.</summary>
    public Material Sidewalk { get; }

    /// <summary>Railway: ballast, sleepers and two rails.</summary>
    public Material Rail { get; }

    public Material Water { get; }

    private readonly Scene _scene;
    private readonly List<Material> _waters = [];
    private Shader? _waterShader;
    private bool _waterFailed;

    /// <summary>A water surface: the river ribbon, or a pond of the given centre and radius.</summary>
    public Material CreateWater(bool pond, float mud, Vector2 centre, float radius)
    {
        if (_waterShader is null && !_waterFailed)
        {
            try
            {
                _waterShader = _scene.CreateShader(WaterShader.Source, name: "barudak.water");
            }
            catch (ThreeNetException)
            {
                // Plain tinted water is still water.
                _waterFailed = true;
            }
        }

        Material material = _scene.CreateMaterial(MaterialOptions.Pbr(new Vector4(0.05f, 0.16f, 0.17f, 0.82f), 0f, 0.06f) with
        {
            AlphaMode = AlphaMode.Blend,
            DepthWrite = false,
            Shader = _waterShader,
            Custom0 = new Vector4(0f, pond ? 1f : 0f, mud, radius),
            Custom1 = new Vector4(centre.X, centre.Y, 0f, 0f),
        });
        _waters.Add(material);
        return material;
    }

    public static int VariantCount => Variants.Length;

    /// <summary>Facade variants a building style may use.</summary>
    public static (int First, int Count) VariantsOf(BuildingStyle style) => style switch
    {
        BuildingStyle.Ruko => (0, 4),
        BuildingStyle.Rumah => (4, 2),
        BuildingStyle.Kios => (6, 1),
        BuildingStyle.Kantor => (7, 2),
        BuildingStyle.Kampung => (9, 2),
        BuildingStyle.Kampus => (11, 1),
        BuildingStyle.Villa => (12, 1),
        _ => (13, 1),
    };

    /// <summary>UV rectangle of an atlas tile, inset so neighbouring tiles never bleed in.</summary>
    public static (Vector2 Min, Vector2 Max) TileUv(int tile)
    {
        const float inset = 2.5f / AtlasSize;
        float size = 1f / AtlasTiles;
        Vector2 origin = new((tile % AtlasTiles) * size, (tile / AtlasTiles) * size);
        return (origin + new Vector2(inset), origin + new Vector2(size - inset));
    }

    /// <summary>Wet roads are smoother; night and blackouts decide how many windows glow.</summary>
    public void SetConditions(float wetness, float windowGlow, float rain = 0f, Shader? shader = null)
    {
        float roughness = MathUtil.Lerp(0.94f, 0.28f, wetness);
        Vector4 custom = new(wetness, 1f, rain, 0f);
        foreach (Material road in (ReadOnlySpan<Material>)[RoadMain, RoadToll, RoadGang, Asphalt])
        {
            road.Update(options => options with { Roughness = roughness, Shader = shader, Custom0 = custom });
        }

        Sidewalk.Update(options => options with { Roughness = MathUtil.Lerp(0.95f, 0.45f, wetness) });

        foreach (Material water in _waters)
        {
            water.Update(options => options with { Custom0 = new Vector4(rain, options.Custom0.Y, options.Custom0.Z, options.Custom0.W) });
        }

        Buildings.Update(options => options with { EmissiveIntensity = windowGlow * 1.25f });
    }

    private static Material Road(Scene scene, int lanes, bool edgeLines, bool paving, bool yellowCenter)
    {
        const int width = 128;
        const int height = 256;
        byte[] pixels = new byte[width * height * 4];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float grain = Hash.Unit(x, y, 77u);
                float patch = Noise.Value(x / 18f, y / 22f, 5u);
                Vector3 c;
                if (paving)
                {
                    // Indonesian conblock interlocking paving stones for Bogor alleys.
                    int row = y / 12;
                    int bx = (x + ((row & 1) * 8)) % 16;
                    int by = y % 12;
                    bool mortar = bx == 0 || by == 0;
                    float stoneTint = Hash.Unit((x + ((row & 1) * 8)) / 16, row, 19u);
                    float shade = (0.41f + (stoneTint * 0.08f) + (grain * 0.04f)) * (mortar ? 0.72f : 1f);
                    c = new Vector3(shade, shade * 0.99f, shade * 0.96f);
                }
                else
                {
                    // Asphalt with subtle darker wheel tracks in each lane and dusty gutter edges.
                    float u = x / (float)(width - 1);
                    float trackL = MathF.Exp(-MathF.Pow((u - 0.26f) * 9f, 2f));
                    float trackR = MathF.Exp(-MathF.Pow((u - 0.74f) * 9f, 2f));
                    float tracks = (trackL + trackR) * 0.032f;
                    float gutter = MathUtil.SmoothStep(0.42f, 0.5f, MathF.Abs(u - 0.5f));
                    float shade = 0.165f + (grain * 0.045f) + (0.035f * patch) - tracks;
                    c = new Vector3(shade, shade, shade * 1.03f);
                    c = Vector3.Lerp(c, new Vector3(0.24f, 0.22f, 0.19f), gutter * 0.55f);
                }

                bool dash = y < height / 2 - 16;
                bool centerLine = lanes == 1 && dash && x is >= 62 and <= 65;
                bool tollLines = lanes == 2 && dash && (x is (>= 41 and <= 44) or (>= 83 and <= 86));
                bool edgeLine = edgeLines && (x is (>= 6 and <= 9) or (>= 118 and <= 121));
                if (centerLine)
                {
                    // Indonesian arterial roads use warm amber-yellow center markings.
                    Vector3 paint = yellowCenter ? new Vector3(0.9f, 0.74f, 0.22f) : new Vector3(0.88f, 0.87f, 0.82f);
                    c = Vector3.Lerp(c, paint, 0.88f - (grain * 0.22f));
                }
                else if (tollLines || edgeLine)
                {
                    c = Vector3.Lerp(c, new Vector3(0.88f, 0.87f, 0.82f), 0.88f - (grain * 0.22f));
                }

                Put(pixels, width, x, y, c);
            }
        }

        Texture texture = scene.CreateTexture(width, height, pixels);
        texture.SetSampler(WrapMode.ClampToEdge, WrapMode.Repeat, true, true, 8);
        return scene.CreateMaterial(MaterialOptions.Pbr(Vector4.One, 0f, 0.94f) with { BaseColorMap = texture });
    }

    private static Material PaintSidewalk(Scene scene)
    {
        // U in [0..0.28]: Bogor's iconic black-and-white painted kanstin curb.
        // U in [0.28..1]: concrete pedestrian tiles with tactile yellow guiding strip.
        const int width = 128;
        const int height = 256;
        byte[] pixels = new byte[width * height * 4];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float grain = Hash.Unit(x, y, 53u);
                Vector3 c;
                if (x < 34)
                {
                    bool whiteBand = (y / 64) % 2 == 0;
                    float baseTone = whiteBand ? 0.82f : 0.13f;
                    float grime = 0.9f + (grain * 0.12f) - (Noise.Value(x / 10f, y / 16f, 29u) * 0.12f);
                    c = new Vector3(baseTone, baseTone, baseTone * (whiteBand ? 0.97f : 1.02f)) * grime;
                }
                else
                {
                    bool seam = (x % 24 == 0) || (y % 32 == 0);
                    float tileShade = 0.56f + (Hash.Unit(x / 24, y / 32, 41u) * 0.08f) + (grain * 0.05f);
                    c = new Vector3(tileShade, tileShade * 0.98f, tileShade * 0.94f) * (seam ? 0.78f : 1f);
                    // Yellow tactile guiding tiles (guiding block) found along Bogor city sidewalks.
                    if (x is >= 54 and <= 68)
                    {
                        bool rib = (x % 4 == 0) || (y % 32 == 0);
                        c = new Vector3(0.86f, 0.72f, 0.2f) * (rib ? 0.82f : 0.96f);
                    }
                }

                Put(pixels, width, x, y, c);
            }
        }

        Texture texture = scene.CreateTexture(width, height, pixels);
        texture.SetSampler(WrapMode.ClampToEdge, WrapMode.Repeat, true, true, 8);
        return scene.CreateMaterial(MaterialOptions.Pbr(Vector4.One, 0f, 0.95f) with { BaseColorMap = texture });
    }

    private static Material Track(Scene scene)
    {
        const int width = 64;
        const int height = 128;
        byte[] pixels = new byte[width * height * 4];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float grain = Hash.Unit(x, y, 91u);
                // Crushed stone, rust-stained between the rails.
                Vector3 c = new Vector3(0.36f, 0.34f, 0.31f) * (0.75f + (grain * 0.5f));
                if (x is >= 8 and < 56 && y % 16 < 6)
                {
                    c = new Vector3(0.2f, 0.16f, 0.13f) * (0.85f + (grain * 0.3f));
                }

                if (x is (>= 18 and <= 20) or (>= 43 and <= 45))
                {
                    c = new Vector3(0.62f, 0.63f, 0.66f) * (0.9f + (grain * 0.15f));
                }

                Put(pixels, width, x, y, c);
            }
        }

        Texture texture = scene.CreateTexture(width, height, pixels);
        texture.SetSampler(WrapMode.ClampToEdge, WrapMode.Repeat, true, true, 8);
        return scene.CreateMaterial(MaterialOptions.Pbr(Vector4.One, 0f, 0.85f) with { BaseColorMap = texture });
    }

    private static void PaintRoof(byte[] atlas, int tile, int light, int dark, int ridges, int courses = 0)
    {
        Vector3 a = Rgb(light);
        Vector3 b = Rgb(dark);
        (int ox, int oy) = Origin(tile);
        for (int y = 0; y < Tile; y++)
        {
            for (int x = 0; x < Tile; x++)
            {
                float t = ridges > 0 ? (x % ridges) / (float)ridges : Hash.Unit(x / 6, y / 6, 31u);
                float course = courses > 0 ? ((y % courses) / (float)courses) * 0.28f : 0f;
                float moss = Noise.Value(x / 18f, y / 18f, (uint)(tile + 5)) > 0.72f ? 0.14f : 0f;
                Vector3 c = Vector3.Lerp(a, b, MathUtil.Saturate((t * 0.65f) + course + (Hash.Unit(x, y, 9u) * 0.22f)));
                c = Vector3.Lerp(c, new Vector3(0.22f, 0.3f, 0.18f), moss);
                Put(atlas, AtlasSize, ox + x, oy + y, c);
            }
        }
    }

    private static void PaintAwning(byte[] atlas, int tile)
    {
        (int ox, int oy) = Origin(tile);
        Vector3 green = Rgb(0x227A48);
        Vector3 cream = Rgb(0xEFECE2);
        for (int y = 0; y < Tile; y++)
        {
            for (int x = 0; x < Tile; x++)
            {
                bool stripe = (x / 16) % 2 == 0;
                float shade = 0.88f + (Hash.Unit(x, y, 47u) * 0.08f) - ((y / (float)Tile) * 0.14f);
                Put(atlas, AtlasSize, ox + x, oy + y, (stripe ? green : cream) * shade);
            }
        }
    }

    private static void PaintFacade(byte[] atlas, byte[] glow, int tile, Variant variant, int part, uint seed)
    {
        Vector3 wall = Rgb(variant.Wall);
        Vector3 trim = Rgb(variant.Trim);
        Vector3 glass = Rgb(variant.Glass);
        Vector3 lamp = new(1f, 0.79f, 0.48f);
        (int ox, int oy) = Origin(tile);
        bool window = part <= 1;
        bool lit = part == 0;
        bool ground = part == 3;

        // Window rectangle inside the 128x128 tile, by building kind.
        (int x0, int y0, int x1, int y1) = variant.Kind switch
        {
            3 => (6, 8, 122, 112),
            7 => (12, 42, 116, 82),
            5 => (18, 26, 110, 94),
            4 => (38, 38, 90, 88),
            _ => (30, 26, 98, 92),
        };

        for (int y = 0; y < Tile; y++)
        {
            for (int x = 0; x < Tile; x++)
            {
                // Plaster: soft blotches plus rain streaks running down from the top, as Bogor walls have.
                float blotch = Noise.Value(x / 24f, y / 24f, seed);
                float fineGrain = Hash.Unit(x, y, seed + 11u) * 0.04f;
                float streak = Noise.Value(x / 5f, y / 75f, seed + 3u) * MathUtil.Saturate(1f - (y / 78f));
                Vector3 c = wall * (0.9f + (blotch * 0.14f) + fineGrain - (streak * 0.14f));
                Vector3 emit = Vector3.Zero;

                // Subtle horizontal floor cornice band at the top of each storey.
                if (y < 6 && variant.Kind is 0 or 3 or 5 or 7)
                {
                    c = Vector3.Lerp(c, trim, 0.45f) * (y < 2 ? 0.84f : 1.04f);
                }

                if (variant.Kind == 5 && y > 108)
                {
                    c = trim * (0.9f + (blotch * 0.15f));
                }

                if (window && x >= x0 && x < x1 && y >= y0 && y < y1)
                {
                    bool outerFrame = x - x0 < 4 || x1 - x <= 4 || y - y0 < 4 || y1 - y <= 4;
                    bool mullion = Math.Abs(x - ((x0 + x1) / 2)) < 2
                        || (variant.Kind == 3 && (Math.Abs(x - (x0 + ((x1 - x0) / 4))) < 2 || Math.Abs(x - (x0 + ((x1 - x0) * 3 / 4))) < 2))
                        || (variant.Kind != 3 && Math.Abs(y - ((y0 + y1) / 2)) < 2);
                    if (outerFrame || mullion)
                    {
                        c = trim * (outerFrame ? 0.88f : 0.96f);
                    }
                    else
                    {
                        float sky = 1f - ((y - y0) / (float)(y1 - y0));
                        float diag = ((x - x0) + (y - y0)) % 42 < 7 ? 0.12f : 0f;
                        c = glass * (0.68f + (sky * 0.62f) + diag);
                        if (lit)
                        {
                            float curtain = y - y0 < 14 ? 0.6f : 1f;
                            emit = lamp * curtain * (0.58f + (0.42f * Hash.Unit(x / 16, y / 16, seed)));
                        }
                    }
                }
                else if (window && variant.Kind is 0 or 1 or 6 && y >= y1 && y < y1 + 6 && x >= x0 - 4 && x < x1 + 4)
                {
                    // Projecting window sill with drop shadow underneath.
                    c = y < y1 + 4 ? trim * 1.05f : wall * 0.65f;
                }
                else if (window && variant.Kind == 0 && (seed & 1) == 0 && x is >= 102 and < 122 && y is >= 74 and < 94)
                {
                    // Compact AC outdoor unit mounted beside ruko windows.
                    bool fan = ((x - 110) * (x - 110)) + ((y - 84) * (y - 84)) < 36;
                    c = fan ? new Vector3(0.22f, 0.24f, 0.26f) : new Vector3(0.84f, 0.83f, 0.79f);
                }

                if (ground)
                {
                    switch (variant.Kind)
                    {
                        case 0 or 2:
                            // Rolling shutter under a striped awning, with a glowing shop behind it at night.
                            if (y < 26)
                            {
                                c = ((x / 16) % 2 == 0 ? trim : Vector3.One * 0.94f) * (y > 22 ? 0.72f : 1f);
                            }
                            else if (x is >= 10 and < 118)
                            {
                                bool slat = y % 6 < 2;
                                c = new Vector3(0.35f, 0.39f, 0.42f) * (slat ? 0.74f : 1.02f);
                                // Warm shop interior peeking through bottom/upper display strip at night.
                                if (y is >= 34 and < 92)
                                {
                                    emit = lamp * 0.36f;
                                }
                            }

                            break;
                        case 3 or 7:
                            if (y > 14 && x is >= 8 and < 120)
                            {
                                bool bar = (x % 28 < 3) || (y is >= 14 and < 18);
                                c = bar ? trim * 0.85f : glass * 1.18f;
                                if (!bar)
                                {
                                    emit = lamp * 0.52f;
                                }
                            }

                            break;
                        default:
                            // A panelled wooden door and a framed house window.
                            if (x is >= 18 and < 52 && y > 34)
                            {
                                bool panel = (x - 18 < 4 || 52 - x <= 4 || y - 34 < 4);
                                c = trim * (panel ? 0.68f : 0.84f);
                            }
                            else if (x is >= 70 and < 110 && y is >= 38 and < 84)
                            {
                                bool frame = x - 70 < 3 || 110 - x <= 3 || y - 38 < 3 || 84 - y <= 3 || Math.Abs(x - 90) < 2;
                                c = frame ? trim * 0.9f : glass * 0.95f;
                                if (!frame)
                                {
                                    emit = lamp * 0.45f;
                                }
                            }

                            break;
                    }
                }

                // Tropical damp grime & moss at the foot of the wall.
                if (ground || part == 2)
                {
                    float foot = MathUtil.Saturate((y - 100) / 28f);
                    c *= 1f - (0.22f * foot);
                    c = Vector3.Lerp(c, new Vector3(0.24f, 0.28f, 0.2f), foot * 0.16f);
                }

                Put(atlas, AtlasSize, ox + x, oy + y, c);
                Put(glow, AtlasSize, ox + x, oy + y, emit);
            }
        }
    }

    private static (int X, int Y) Origin(int tile) => ((tile % AtlasTiles) * Tile, (tile / AtlasTiles) * Tile);

    private static Vector3 Rgb(int hex) => new(((hex >> 16) & 255) / 255f, ((hex >> 8) & 255) / 255f, (hex & 255) / 255f);

    private static void Put(byte[] pixels, int width, int x, int y, Vector3 color)
    {
        int i = ((y * width) + x) * 4;
        pixels[i] = (byte)(Math.Clamp(color.X, 0f, 1f) * 255f);
        pixels[i + 1] = (byte)(Math.Clamp(color.Y, 0f, 1f) * 255f);
        pixels[i + 2] = (byte)(Math.Clamp(color.Z, 0f, 1f) * 255f);
        pixels[i + 3] = 255;
    }
}
