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
    private const int Tile = 64;
    private const int AtlasSize = AtlasTiles * Tile;

    /// <summary>Tiles per facade variant: lit window, dark window, plain wall, ground floor.</summary>
    public const int TilesPerVariant = 4;

    public const int RoofTerracotta = 56;
    public const int RoofConcrete = 57;
    public const int RoofZinc = 58;
    public const int RoofDark = 59;

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

        PaintRoof(color, RoofTerracotta, 0xA9502F, 0x8C3F25, ridges: 8);
        PaintRoof(color, RoofConcrete, 0x8E8C86, 0x7C7A75, ridges: 0);
        PaintRoof(color, RoofZinc, 0x9AA3A6, 0x7F898C, ridges: 16);
        PaintRoof(color, RoofDark, 0x3B302B, 0x2C2420, ridges: 8);

        Texture atlas = scene.CreateTexture(AtlasSize, AtlasSize, color);
        atlas.SetSampler(WrapMode.ClampToEdge, WrapMode.ClampToEdge, true, true, 4);
        Texture glowAtlas = scene.CreateTexture(AtlasSize, AtlasSize, glow);
        glowAtlas.SetSampler(WrapMode.ClampToEdge, WrapMode.ClampToEdge, true, true, 1);
        Buildings = scene.CreateMaterial(MaterialOptions.Pbr(Vector4.One, 0f, 0.86f) with
        {
            BaseColorMap = atlas,
            EmissiveMap = glowAtlas,
            Emissive = Vector3.One,
            EmissiveIntensity = 0f,
        });

        RoadMain = Road(scene, lanes: 1, edgeLines: false, paving: false);
        RoadToll = Road(scene, lanes: 2, edgeLines: true, paving: false);
        RoadGang = Road(scene, lanes: 0, edgeLines: false, paving: true);
        Asphalt = Road(scene, lanes: 0, edgeLines: false, paving: false);
        Water = scene.CreateMaterial(MaterialOptions.Pbr(new Vector4(0.05f, 0.16f, 0.17f, 0.82f), 0f, 0.08f) with
        {
            AlphaMode = AlphaMode.Blend,
            DepthWrite = false,
        });
    }

    public Material Buildings { get; }

    public Material RoadMain { get; }

    public Material RoadToll { get; }

    public Material RoadGang { get; }

    public Material Asphalt { get; }

    public Material Water { get; }

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
    public void SetConditions(float wetness, float windowGlow)
    {
        float roughness = MathUtil.Lerp(0.92f, 0.3f, wetness);
        foreach (Material road in (ReadOnlySpan<Material>)[RoadMain, RoadToll, RoadGang, Asphalt])
        {
            road.Update(options => options with { Roughness = roughness });
        }

        Buildings.Update(options => options with { EmissiveIntensity = windowGlow * 1.2f });
    }

    private static Material Road(Scene scene, int lanes, bool edgeLines, bool paving)
    {
        const int width = 64;
        const int height = 128;
        byte[] pixels = new byte[width * height * 4];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float grain = Hash.Unit(x, y, 77u);
                float shade = paving
                    ? 0.42f + (grain * 0.05f) - ((x % 8 == 0 || (y + ((x / 8 % 2) * 8)) % 16 == 0) ? 0.08f : 0f)
                    : 0.17f + (grain * 0.05f) + (0.03f * Noise.Value(x / 9f, y / 9f, 5u));
                Vector3 c = new(shade, shade, shade * 1.03f);
                bool dash = y < height / 2;
                bool line = (lanes == 1 && dash && x is >= 31 and <= 32)
                    || (lanes == 2 && dash && (x is 21 or 22 or 42 or 43))
                    || (edgeLines && (x is 3 or 4 or 59 or 60));
                if (line)
                {
                    c = Vector3.Lerp(c, new Vector3(0.86f, 0.85f, 0.78f), 0.9f - (grain * 0.25f));
                }

                Put(pixels, width, x, y, c);
            }
        }

        Texture texture = scene.CreateTexture(width, height, pixels);
        texture.SetSampler(WrapMode.ClampToEdge, WrapMode.Repeat, true, true, 8);
        return scene.CreateMaterial(MaterialOptions.Pbr(Vector4.One, 0f, 0.92f) with { BaseColorMap = texture });
    }

    private static void PaintRoof(byte[] atlas, int tile, int light, int dark, int ridges)
    {
        Vector3 a = Rgb(light);
        Vector3 b = Rgb(dark);
        (int ox, int oy) = Origin(tile);
        for (int y = 0; y < Tile; y++)
        {
            for (int x = 0; x < Tile; x++)
            {
                float t = ridges > 0 ? (x % ridges) / (float)ridges : Hash.Unit(x / 4, y / 4, 31u);
                Vector3 c = Vector3.Lerp(a, b, (t * 0.7f) + (Hash.Unit(x, y, 9u) * 0.3f));
                Put(atlas, AtlasSize, ox + x, oy + y, c);
            }
        }
    }

    private static void PaintFacade(byte[] atlas, byte[] glow, int tile, Variant variant, int part, uint seed)
    {
        Vector3 wall = Rgb(variant.Wall);
        Vector3 trim = Rgb(variant.Trim);
        Vector3 glass = Rgb(variant.Glass);
        Vector3 lamp = new(1f, 0.78f, 0.46f);
        (int ox, int oy) = Origin(tile);
        bool window = part <= 1;
        bool lit = part == 0;
        bool ground = part == 3;

        // Window rectangle inside the tile, by building kind.
        (int x0, int y0, int x1, int y1) = variant.Kind switch
        {
            3 => (3, 4, 61, 56),
            7 => (6, 22, 58, 40),
            5 => (10, 14, 54, 46),
            4 => (20, 20, 44, 44),
            _ => (16, 14, 48, 46),
        };

        for (int y = 0; y < Tile; y++)
        {
            for (int x = 0; x < Tile; x++)
            {
                // Plaster: soft blotches plus rain streaks running down from the top, as Bogor walls have.
                float blotch = Noise.Value(x / 13f, y / 13f, seed);
                float streak = Noise.Value(x / 2.5f, y / 40f, seed + 3u) * MathUtil.Saturate(1f - (y / 40f));
                Vector3 c = wall * (0.9f + (blotch * 0.14f) - (streak * 0.12f));
                Vector3 emit = Vector3.Zero;

                if (variant.Kind == 5 && y > 54)
                {
                    c = trim * (0.9f + (blotch * 0.15f));
                }

                if (window && x >= x0 && x < x1 && y >= y0 && y < y1)
                {
                    bool frame = x - x0 < 2 || x1 - x <= 2 || y - y0 < 2 || y1 - y <= 2 || x == (x0 + x1) / 2 || (variant.Kind != 3 && y == (y0 + y1) / 2);
                    float sky = 1f - ((y - y0) / (float)(y1 - y0));
                    c = frame ? trim * 0.9f : glass * (0.75f + (sky * 0.6f));
                    if (lit && !frame)
                    {
                        emit = lamp * (0.55f + (0.45f * Hash.Unit(x / 8, y / 8, seed)));
                    }
                }
                else if (window && variant.Kind is 0 or 1 or 6 && y >= y1 && y < y1 + 3 && x >= x0 - 2 && x < x1 + 2)
                {
                    c = trim;
                }

                if (ground)
                {
                    switch (variant.Kind)
                    {
                        case 0 or 2:
                            // Rolling shutter under a striped awning, with a glowing shop behind it at night.
                            if (y < 14)
                            {
                                c = (x / 8) % 2 == 0 ? trim : Vector3.One * 0.92f;
                            }
                            else if (x is >= 6 and < 58)
                            {
                                c = new Vector3(0.36f, 0.4f, 0.42f) * (y % 4 == 0 ? 0.7f : 1f);
                                emit = lamp * 0.35f;
                            }

                            break;
                        case 3 or 7:
                            if (y > 8 && x is >= 4 and < 60)
                            {
                                c = glass * 1.2f;
                                emit = lamp * 0.5f;
                            }

                            break;
                        default:
                            // A door and a small window.
                            if (x is >= 10 and < 26 && y > 18)
                            {
                                c = trim * 0.8f;
                            }
                            else if (x is >= 36 and < 54 && y is >= 20 and < 42)
                            {
                                c = glass;
                                emit = lamp * 0.4f;
                            }

                            break;
                    }
                }

                // Grime at the foot of the wall.
                if (ground || part == 2)
                {
                    c *= 1f - (0.18f * MathUtil.Saturate((y - 50) / 14f));
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
