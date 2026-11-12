using System.Collections.Concurrent;
using System.Numerics;
using BarudakBogor.Numerics;
using BarudakBogor.World;
using ThreeNet;
using ThreeNet.Interop;

namespace BarudakBogor.Game.Rendering;

/// <summary>
/// Streams the world around the player. Near chunks carry everything (ground, roads, buildings,
/// vegetation); a ring of coarse tiles beyond them gives the mountains their silhouette. Meshes
/// are built on worker threads and uploaded a few per frame, so driving never hitches.
/// </summary>
public sealed class WorldRenderer : IDisposable
{
    private const float FarTile = 2_048f;
    private const int FarCells = 16;

    private sealed class Loaded
    {
        public required Node Root;
        public readonly List<Geometry> Geometries = [];
        public Material? Material;
        public Texture? Texture;
    }

    private readonly Scene _scene;
    private readonly WorldMap _map;
    private readonly ModelLibrary _models;
    private readonly WorldTextures _textures;
    private readonly Dictionary<(int, int), Loaded> _chunks = [];
    private readonly Dictionary<(int, int), Loaded> _farTiles = [];
    private readonly HashSet<(int, int)> _pending = [];
    private readonly HashSet<(int, int)> _farPending = [];
    private readonly ConcurrentQueue<ChunkData> _ready = new();
    private readonly ConcurrentQueue<(int X, int Z, MeshData Mesh, byte[] Pixels)> _farReady = new();
    private readonly List<(int, int)> _scratch = [];
    private readonly Material? _billboard;
    private float _lastWetness = -1f;
    private float _lastGlow = -1f;
    private bool _disposed;

    public WorldRenderer(Scene scene, WorldMap map, ModelLibrary models)
    {
        _scene = scene;
        _map = map;
        _models = models;
        _textures = new WorldTextures(scene);
        string poster = Path.Combine(AppContext.BaseDirectory, "Assets", "Art", "billboard_bandrek.jpg");
        if (File.Exists(poster))
        {
            _billboard = scene.CreateMaterial(MaterialOptions.Pbr(Vector4.One, 0f, 0.7f) with
            {
                BaseColorMap = scene.LoadTexture(poster),
                CullMode = CullMode.None,
            });
        }

        foreach (Pond pond in map.Ponds)
        {
            Node water = scene.AddMesh(Disc(pond.Radius * 1.04f), _textures.Water, name: pond.NameKey);
            water.Position = new Vector3(pond.Center.X, pond.Level, pond.Center.Y);
            water.CastShadow = false;
        }
    }

    /// <summary>Chunks kept loaded in every direction (3 = a 7 x 7 block, about 770 m of view).</summary>
    public int Radius { get; set; } = 3;

    /// <summary>Chunks within this many rings carry buildings and vegetation.</summary>
    public int DetailRadius { get; set; } = 3;

    public int LoadedChunks => _chunks.Count;

    public bool IsBusy => _pending.Count > 0 || !_ready.IsEmpty;

    public void SetConditions(float wetness, float windowGlow)
    {
        if (MathF.Abs(wetness - _lastWetness) > 0.02f || MathF.Abs(windowGlow - _lastGlow) > 0.02f)
        {
            _lastWetness = wetness;
            _lastGlow = windowGlow;
            _textures.SetConditions(wetness, windowGlow);
        }
    }

    /// <summary>Schedules, uploads and unloads chunks for the current focus point.</summary>
    public void Update(Vector2 focus, int uploadsPerFrame = 2)
    {
        (int cx, int cz) = WorldMap.ChunkOf(focus);
        for (int ring = 0; ring <= Radius; ring++)
        {
            for (int z = cz - ring; z <= cz + ring; z++)
            {
                for (int x = cx - ring; x <= cx + ring; x++)
                {
                    // Walk the rings outward so the ground under the player always comes first.
                    if (Math.Max(Math.Abs(x - cx), Math.Abs(z - cz)) != ring || _chunks.ContainsKey((x, z)) || !_pending.Add((x, z)))
                    {
                        continue;
                    }

                    int chunkX = x;
                    int chunkZ = z;
                    bool detail = ring <= DetailRadius;
                    Task.Run(() => _ready.Enqueue(ChunkBuilder.Build(_map, chunkX, chunkZ, detail)));
                }
            }
        }

        for (int i = 0; i < uploadsPerFrame && _ready.TryDequeue(out ChunkData? data); i++)
        {
            _pending.Remove((data.X, data.Z));
            if (Math.Max(Math.Abs(data.X - cx), Math.Abs(data.Z - cz)) <= Radius + 1 && !_chunks.ContainsKey((data.X, data.Z)))
            {
                _chunks[(data.X, data.Z)] = Upload(data);
            }
        }

        Unload(_chunks, cx, cz, Radius + 1);
        UpdateFarTiles(focus);
    }

    /// <summary>Loads everything around a point before returning (start-up, fast travel, screenshots).</summary>
    public void LoadNow(Vector2 focus)
    {
        Update(focus, 0);
        while (_pending.Count > 0)
        {
            if (_ready.IsEmpty)
            {
                Thread.Sleep(2);
            }

            Update(focus, 64);
        }

        while (_farPending.Count > 0)
        {
            Thread.Sleep(2);
            UpdateFarTiles(focus);
        }
    }

    private Loaded Upload(ChunkData data)
    {
        Node root = _scene.CreateNode(null, "chunk");
        root.Position = data.Origin;
        Loaded loaded = new() { Root = root };

        Texture ground = _scene.CreateTexture(ChunkData.GroundTextureSize, ChunkData.GroundTextureSize, data.GroundPixels);
        ground.SetSampler(WrapMode.ClampToEdge, WrapMode.ClampToEdge, true, false, 1);
        Material material = _scene.CreateMaterial(MaterialOptions.Pbr(Vector4.One, 0f, 0.95f) with { BaseColorMap = ground });
        loaded.Texture = ground;
        loaded.Material = material;
        AddMesh(loaded, data.Terrain, material, castShadow: false);
        AddMesh(loaded, data.RoadMain, _textures.RoadMain, castShadow: false);
        AddMesh(loaded, data.RoadToll, _textures.RoadToll, castShadow: false);
        AddMesh(loaded, data.RoadGang, _textures.RoadGang, castShadow: false);
        AddMesh(loaded, data.Asphalt, _textures.Asphalt, castShadow: false);
        AddMesh(loaded, data.Buildings, _textures.Buildings, castShadow: true);

        foreach ((string model, List<Matrix4x4> transforms) in data.Instances)
        {
            ModelPrimitive[] primitives = _models.Primitives(model);
            Matrix4x4[] matrices = new Matrix4x4[transforms.Count];
            foreach (ModelPrimitive primitive in primitives)
            {
                Material skin = primitive.Material;
                if (model == "papan_reklame" && _billboard is not null && primitive.Geometry.Counts.Vertices <= 6)
                {
                    // The blank face of the billboard is the only single quad of the model.
                    skin = _billboard;
                }

                for (int i = 0; i < matrices.Length; i++)
                {
                    matrices[i] = primitive.Transform * transforms[i];
                }

                Node node = _scene.AddMesh(primitive.Geometry, skin, root);
                node.SetInstances(matrices);
                if (model == "semak_teh")
                {
                    // Thousands of bushes per hillside: beyond a few hundred metres the green ground is enough.
                    node.SetLevelsOfDetail((primitive.Geometry, 0f), (null, 420f));
                }

                // Bushes and lamp posts are too small for their shadows to be worth a pass.
                node.CastShadow = model is not ("semak_teh" or "lampu_jalan" or "rambu_angkot");
            }
        }

        return loaded;
    }

    private void AddMesh(Loaded loaded, MeshData mesh, Material material, bool castShadow)
    {
        if (mesh.IsEmpty)
        {
            return;
        }

        Geometry geometry = mesh.Build(_scene);
        loaded.Geometries.Add(geometry);
        Node node = _scene.AddMesh(geometry, material, loaded.Root);
        node.CastShadow = castShadow;
    }

    private void Unload(Dictionary<(int, int), Loaded> set, int cx, int cz, int keep)
    {
        _scratch.Clear();
        foreach ((int x, int z) in set.Keys)
        {
            if (Math.Max(Math.Abs(x - cx), Math.Abs(z - cz)) > keep)
            {
                _scratch.Add((x, z));
            }
        }

        foreach ((int, int) key in _scratch)
        {
            Release(set[key]);
            set.Remove(key);
        }
    }

    private static void Release(Loaded loaded)
    {
        loaded.Root.Remove();
        foreach (Geometry geometry in loaded.Geometries)
        {
            geometry.Destroy();
        }

        loaded.Material?.Destroy();
        loaded.Texture?.Destroy();
    }

    // ------------------------------------------------------------------ far terrain

    private void UpdateFarTiles(Vector2 focus)
    {
        int cx = (int)MathF.Floor(focus.X / FarTile);
        int cz = (int)MathF.Floor(focus.Y / FarTile);
        for (int z = cz - 2; z <= cz + 2; z++)
        {
            for (int x = cx - 2; x <= cx + 2; x++)
            {
                if (_farTiles.ContainsKey((x, z)) || MathF.Abs((x + 0.5f) * FarTile) > Terrain.HalfSize + FarTile
                    || MathF.Abs((z + 0.5f) * FarTile) > Terrain.HalfSize + FarTile || !_farPending.Add((x, z)))
                {
                    continue;
                }

                int tileX = x;
                int tileZ = z;
                Task.Run(() => _farReady.Enqueue(BuildFarTile(tileX, tileZ)));
            }
        }

        while (_farReady.TryDequeue(out (int X, int Z, MeshData Mesh, byte[] Pixels) tile))
        {
            _farPending.Remove((tile.X, tile.Z));
            if (_disposed || _farTiles.ContainsKey((tile.X, tile.Z)))
            {
                continue;
            }

            Node root = _scene.CreateNode(null, "far");
            root.Position = new Vector3(tile.X * FarTile, 0f, tile.Z * FarTile);
            Loaded loaded = new() { Root = root };
            Texture texture = _scene.CreateTexture(FarCells * 2, FarCells * 2, tile.Pixels);
            texture.SetSampler(WrapMode.ClampToEdge, WrapMode.ClampToEdge, true, false, 1);
            loaded.Texture = texture;
            loaded.Material = _scene.CreateMaterial(MaterialOptions.Pbr(Vector4.One, 0f, 1f) with { BaseColorMap = texture });
            AddMesh(loaded, tile.Mesh, loaded.Material, castShadow: false);
            _farTiles[(tile.X, tile.Z)] = loaded;
        }

        Unload(_farTiles, cx, cz, 3);
    }

    private (int, int, MeshData, byte[]) BuildFarTile(int tileX, int tileZ)
    {
        Terrain terrain = _map.Terrain;
        float cell = FarTile / FarCells;
        Vector2 origin = new(tileX * FarTile, tileZ * FarTile);
        MeshData mesh = new();
        int row = FarCells + 1;
        float[] heights = new float[row * row];
        for (int j = 0; j <= FarCells; j++)
        {
            for (int i = 0; i <= FarCells; i++)
            {
                // Sit below the detailed ground: take the lowest nearby height and sink it a little more.
                Vector2 p = origin + new Vector2(i * cell, j * cell);
                float low = float.MaxValue;
                for (int dz = -1; dz <= 1; dz++)
                {
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        low = MathF.Min(low, terrain.CoarseHeightAt(p.X + (dx * cell * 0.5f), p.Y + (dz * cell * 0.5f)));
                    }
                }

                heights[(j * row) + i] = low - 9f;
            }
        }

        for (int j = 0; j <= FarCells; j++)
        {
            for (int i = 0; i <= FarCells; i++)
            {
                float h = heights[(j * row) + i];
                float dx = heights[(j * row) + Math.Min(i + 1, FarCells)] - heights[(j * row) + Math.Max(i - 1, 0)];
                float dz = heights[(Math.Min(j + 1, FarCells) * row) + i] - heights[(Math.Max(j - 1, 0) * row) + i];
                mesh.Add(new Vector3(i * cell, h, j * cell), Vector3.Normalize(new Vector3(-dx, 2f * cell, -dz)), new Vector2(i / (float)FarCells, j / (float)FarCells));
            }
        }

        for (int j = 0; j < FarCells; j++)
        {
            for (int i = 0; i < FarCells; i++)
            {
                uint a = (uint)((j * row) + i);
                uint d = a + (uint)row;
                mesh.Triangle(a, d, a + 1);
                mesh.Triangle(a + 1, d, d + 1);
            }
        }

        int size = FarCells * 2;
        byte[] pixels = new byte[size * size * 4];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                Vector2 p = origin + new Vector2((x + 0.5f) * FarTile / size, (y + 0.5f) * FarTile / size);
                Vector3 c = _map.LandUseAt(p) switch
                {
                    LandUse.Sawah => new Vector3(0.3f, 0.46f, 0.2f),
                    LandUse.Hutan => new Vector3(0.12f, 0.25f, 0.14f),
                    LandUse.KebunTeh => new Vector3(0.19f, 0.38f, 0.17f),
                    LandUse.Taman => new Vector3(0.2f, 0.4f, 0.18f),
                    _ => new Vector3(0.42f, 0.4f, 0.36f),
                } * (0.88f + (0.2f * Noise.Value(p.X / 300f, p.Y / 300f, 4u)));
                int k = ((y * size) + x) * 4;
                pixels[k] = (byte)(c.X * 255f);
                pixels[k + 1] = (byte)(c.Y * 255f);
                pixels[k + 2] = (byte)(c.Z * 255f);
                pixels[k + 3] = 255;
            }
        }

        return (tileX, tileZ, mesh, pixels);
    }

    private Geometry Disc(float radius)
    {
        const int sides = 40;
        Vertex[] vertices = new Vertex[sides + 1];
        uint[] indices = new uint[sides * 3];
        vertices[0] = new Vertex(Vector3.Zero, Vector3.UnitY, new Vector2(0.5f, 0.5f));
        for (int i = 0; i < sides; i++)
        {
            float angle = i * MathUtil.Tau / sides;
            vertices[i + 1] = new Vertex(new Vector3(MathF.Cos(angle) * radius, 0f, MathF.Sin(angle) * radius), Vector3.UnitY, new Vector2(MathF.Cos(angle), MathF.Sin(angle)));
            indices[i * 3] = 0;
            indices[(i * 3) + 1] = (uint)(((i + 1) % sides) + 1);
            indices[(i * 3) + 2] = (uint)(i + 1);
        }

        return _scene.CreateGeometry(vertices, indices);
    }

    public void Dispose() => _disposed = true;
}
