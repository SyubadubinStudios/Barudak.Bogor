"""Collapses the flat-colour materials of the low-poly models into one palette texture.

    blender --background --python assets/blender/bake_palette.py [-- name ...]

The Blender-built models use one flat material per colour, and the engine draws one call per
material: an angkot body alone is 13 calls, a walking villager 18. This script rewrites each
GLB in assets/models so that every mesh object has a single material that looks its colours
up in a small palette image. Each face's UVs sit on the centre of its colour swatch, so the
picture is identical while the draw calls drop to one per moving part.

Run it after build_all.py. Models that already carry a texture (the Rodin ones) and models
that are already baked are left alone.
"""
import os
import sys

import bpy

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
MODELS = os.path.join(ROOT, "models")
GRID = 8          # swatches per side
SWATCH = 8        # pixels per swatch
SIZE = GRID * SWATCH

# The game swaps a picture onto the blank face of the billboard and of the LED screen, which must stay its own primitive.
KEEP = {"papan_reklame", "videotron"}


def linear_to_srgb(c):
    return 12.92 * c if c <= 0.0031308 else 1.055 * (c ** (1 / 2.4)) - 0.055


def material_props(material):
    """Extract (base_rgb, roughness, metallic, emit_rgb) from a Principled BSDF material."""
    bsdf = next((n for n in material.node_tree.nodes if n.type == "BSDF_PRINCIPLED"), None)
    if bsdf is None:
        return ((0.8, 0.8, 0.8), 0.8, 0.0, (0.0, 0.0, 0.0))
    base = tuple(bsdf.inputs["Base Color"].default_value[:3])
    rough = float(bsdf.inputs["Roughness"].default_value)
    metal = float(bsdf.inputs["Metallic"].default_value)
    emission = tuple(bsdf.inputs["Emission Color"].default_value[:3])
    strength = float(bsdf.inputs["Emission Strength"].default_value)
    if strength > 0.0 and max(emission) > 0.0:
        emit = tuple(min(1.0, e * min(strength, 1.0)) for e in emission)
        base = tuple(min(1.0, max(b, e)) for b, e in zip(base, emit))
    else:
        emit = (0.0, 0.0, 0.0)
    return (base, rough, metal, emit)


def has_texture(obj):
    return any(n.type == "TEX_IMAGE" for m in obj.data.materials if m and m.node_tree for n in m.node_tree.nodes)


def bake(path):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=path)
    meshes = [o for o in bpy.context.scene.objects if o.type == "MESH"]
    if not meshes or any(has_texture(o) for o in meshes) or os.path.basename(path)[:-4] in KEEP:
        return None

    before = sum(len(o.data.materials) for o in meshes)
    swatches = []

    def swatch(props):
        base, rough, metal, emit = props
        key = (
            tuple(round(c, 3) for c in base),
            round(rough, 2),
            round(metal, 2),
            tuple(round(c, 3) for c in emit),
        )
        if key not in swatches:
            swatches.append(key)
        return swatches.index(key)

    default_props = ((0.8, 0.8, 0.8), 0.8, 0.0, (0.0, 0.0, 0.0))
    assignments = []
    for obj in meshes:
        slots = [swatch(material_props(m)) if m else swatch(default_props) for m in obj.data.materials]
        assignments.append((obj, slots or [swatch(default_props)]))
    if len(swatches) > GRID * GRID:
        raise RuntimeError(f"{os.path.basename(path)} uses {len(swatches)} swatches; raise GRID")

    has_emit = any(max(s[3]) > 0.0 for s in swatches)

    color_img = bpy.data.images.new("palette", SIZE, SIZE, alpha=False)
    mr_img = bpy.data.images.new("palette_mr", SIZE, SIZE, alpha=False)
    mr_img.colorspace_settings.name = "Non-Color"
    emit_img = bpy.data.images.new("palette_emit", SIZE, SIZE, alpha=False) if has_emit else None

    color_px = [0.0] * (SIZE * SIZE * 4)
    mr_px = [0.0] * (SIZE * SIZE * 4)
    emit_px = [0.0] * (SIZE * SIZE * 4) if has_emit else None

    for index, (base, rough, metal, emit) in enumerate(swatches):
        cx, cy = index % GRID, index // GRID
        srgb = [linear_to_srgb(c) for c in base]
        emit_srgb = [linear_to_srgb(c) for c in emit]
        for y in range(cy * SWATCH, (cy + 1) * SWATCH):
            for x in range(cx * SWATCH, (cx + 1) * SWATCH):
                i = (y * SIZE + x) * 4
                color_px[i:i + 4] = [srgb[0], srgb[1], srgb[2], 1.0]
                # glTF metallicRoughnessTexture: R=unused/AO, G=Roughness, B=Metallic
                mr_px[i:i + 4] = [1.0, rough, metal, 1.0]
                if emit_px is not None:
                    emit_px[i:i + 4] = [emit_srgb[0], emit_srgb[1], emit_srgb[2], 1.0]

    color_img.pixels = color_px
    color_img.pack()
    mr_img.pixels = mr_px
    mr_img.pack()
    if emit_img is not None and emit_px is not None:
        emit_img.pixels = emit_px
        emit_img.pack()

    palette = bpy.data.materials.new("palette")
    palette.use_nodes = True
    tree = palette.node_tree
    bsdf = next(n for n in tree.nodes if n.type == "BSDF_PRINCIPLED")

    color_tex = tree.nodes.new("ShaderNodeTexImage")
    color_tex.image = color_img
    color_tex.interpolation = "Closest"
    tree.links.new(color_tex.outputs["Color"], bsdf.inputs["Base Color"])

    mr_tex = tree.nodes.new("ShaderNodeTexImage")
    mr_tex.image = mr_img
    mr_tex.interpolation = "Closest"
    sep = tree.nodes.new("ShaderNodeSeparateColor")
    tree.links.new(mr_tex.outputs["Color"], sep.inputs["Color"])
    tree.links.new(sep.outputs["Green"], bsdf.inputs["Roughness"])
    tree.links.new(sep.outputs["Blue"], bsdf.inputs["Metallic"])

    if emit_img is not None:
        emit_tex = tree.nodes.new("ShaderNodeTexImage")
        emit_tex.image = emit_img
        emit_tex.interpolation = "Closest"
        tree.links.new(emit_tex.outputs["Color"], bsdf.inputs["Emission Color"])
        bsdf.inputs["Emission Strength"].default_value = 2.2

    for obj, slots in assignments:
        mesh = obj.data
        layer = mesh.uv_layers.active or mesh.uv_layers.new(name="palette")
        for polygon in mesh.polygons:
            index = slots[min(polygon.material_index, len(slots) - 1)]
            u = ((index % GRID) + 0.5) / GRID
            v = ((index // GRID) + 0.5) / GRID
            for loop in polygon.loop_indices:
                layer.data[loop].uv = (u, v)
        mesh.materials.clear()
        mesh.materials.append(palette)
        for polygon in mesh.polygons:
            polygon.material_index = 0

    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.export_scene.gltf(filepath=path, export_format="GLB", use_selection=True, export_image_format="AUTO",
                              export_animations=False, export_yup=True)
    return before, len(meshes), len(swatches)


names = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else sorted(f[:-4] for f in os.listdir(MODELS) if f.endswith(".glb"))
total_before = total_after = 0
for name in names:
    result = bake(os.path.join(MODELS, name + ".glb"))
    if result is None:
        print(f"SKIP {name}")
        continue
    before, after, colours = result
    total_before += before
    total_after += after
    print(f"BAKED {name}: {before} -> {after} draw calls, {colours} colours, {os.path.getsize(os.path.join(MODELS, name + '.glb')) // 1024} KB")
print(f"TOTAL {total_before} -> {total_after}")
