using System.Numerics;
using ThreeNet;

namespace BarudakBogor.Game.Rendering;

/// <summary>A drawable piece of a model: one geometry, one material, and where it sits in the model.</summary>
public readonly record struct ModelPrimitive(Geometry Geometry, Material Material, Matrix4x4 Transform);

/// <summary>
/// A character with a skeleton: one skinned mesh and the clips that move it. Clips are null
/// when the file lacks them.
/// </summary>
public sealed record RiggedModel(Node Root, AnimationClip? Idle, AnimationClip? Walk, AnimationClip? Run, AnimationClip? Sit);

/// <summary>
/// Loads each GLB once into a hidden prototype. Moving things (vehicles, people, animals) get a
/// <see cref="Node.Clone"/> that shares the GPU buffers; static scenery is drawn with instancing
/// from the prototype's primitives.
/// </summary>
public sealed class ModelLibrary
{
    private readonly Scene _scene;
    private readonly Node _holder;
    private readonly Dictionary<string, Node?> _prototypes = [];
    private readonly Dictionary<string, ModelPrimitive[]> _primitives = [];

    public ModelLibrary(Scene scene)
    {
        _scene = scene;
        _holder = scene.CreateNode(null, "prototypes");
        _holder.Visible = false;
    }

    public static string ModelDirectory => Path.Combine(AppContext.BaseDirectory, "Assets", "Models");

    private Node? Prototype(string name)
    {
        if (_prototypes.TryGetValue(name, out Node? cached))
        {
            return cached;
        }

        Node? prototype = null;
        string path = Path.Combine(ModelDirectory, name + ".glb");
        if (File.Exists(path))
        {
            try
            {
                prototype = _scene.CreateNode(_holder, name);
                _scene.LoadGltf(path, prototype);
            }
            catch (ThreeNetException)
            {
                prototype?.Remove();
                prototype = null;
            }
        }

        _prototypes[name] = prototype;
        return prototype;
    }

    /// <summary>A visible copy of a model under <paramref name="parent"/>, or null when the asset is missing.</summary>
    public Node? Spawn(string name, Node? parent = null)
    {
        if (Prototype(name) is not { } prototype)
        {
            return null;
        }

        Node copy = prototype.Clone(parent);
        copy.Visible = true;
        return copy;
    }

    /// <summary>
    /// A skinned, animated copy of a character (<c>name_rig.glb</c>, made by <c>rig_bones.py</c>),
    /// or null when there is none. Unlike <see cref="Spawn"/> this imports the file each time:
    /// a skinned mesh is deformed in place, so two characters cannot share one, and a clip
    /// drives the bones of one import only. Keep what this returns and reuse it.
    /// </summary>
    public RiggedModel? SpawnRigged(string name, Node parent)
    {
        string path = Path.Combine(ModelDirectory, name + "_rig.glb");
        if (!File.Exists(path))
        {
            return null;
        }

        Node root = _scene.CreateNode(parent, name + "_rig");
        try
        {
            int before = _scene.Animations.Count;
            ImportResult result = _scene.LoadGltf(path, root);
            if (result.SkinCount == 0)
            {
                root.Remove();
                return null;
            }

            Dictionary<string, AnimationClip> clips = [];
            foreach (AnimationClip clip in _scene.Animations.Skip(before))
            {
                clips[clip.Name] = clip;
            }

            return new RiggedModel(root, clips.GetValueOrDefault("idle"), clips.GetValueOrDefault("walk"), clips.GetValueOrDefault("run"), clips.GetValueOrDefault("sit"));
        }
        catch (ThreeNetException)
        {
            root.Remove();
            return null;
        }
    }

    /// <summary>Finds a named part (wheel, leg, head...) anywhere below a spawned copy.</summary>
    public static Node? FindPart(Node root, string name)
    {
        foreach (Node child in root.Children)
        {
            if (child.Name == name)
            {
                return child;
            }

            if (FindPart(child, name) is { } nested)
            {
                return nested;
            }
        }

        return null;
    }

    /// <summary>Flattened primitives of a model for instanced drawing.</summary>
    public ModelPrimitive[] Primitives(string name)
    {
        if (_primitives.TryGetValue(name, out ModelPrimitive[]? cached))
        {
            return cached;
        }

        List<ModelPrimitive> list = [];
        if (Prototype(name) is { } prototype)
        {
            Collect(prototype, Matrix4x4.Identity, list, isRoot: true);
        }

        return _primitives[name] = [.. list];
    }

    private static void Collect(Node node, Matrix4x4 parent, List<ModelPrimitive> into, bool isRoot)
    {
        Matrix4x4 world = isRoot
            ? parent
            : Matrix4x4.CreateScale(node.Scale) * Matrix4x4.CreateFromQuaternion(node.Rotation) * Matrix4x4.CreateTranslation(node.Position) * parent;
        if (node.Geometry is { } geometry && node.Material is { } material)
        {
            into.Add(new ModelPrimitive(geometry, material, world));
        }

        foreach (Node child in node.Children)
        {
            Collect(child, world, into, isRoot: false);
        }
    }
}
