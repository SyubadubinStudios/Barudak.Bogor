"""Turns raw Rodin (Hyper3D) GLBs into game-ready ones.

    blender --background --python assets/blender/optimize_rodin.py

Reads assets/models_hd/*.glb and writes assets/models/<name>.glb: the mesh is decimated to a
triangle budget, stood on the ground with its footprint centred on the origin, scaled to a
real-world height, and its textures are shrunk (base colour only, 512 px JPEG).
"""
import os
import bpy
from mathutils import Vector

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SRC = os.path.join(ROOT, "models_hd")
DST = os.path.join(ROOT, "models")

# name -> (height in metres, triangle budget, texture size)
TARGETS = {
    # npc_mang_udin is rigged by rig_rodin.py instead.
    # npc_haji_dadang is rigged by rig_rodin.py instead.
    # npc_bu_euis is rigged by rig_rodin.py instead.
    # npc_guru_bp is rigged by rig_rodin.py instead.
    # npc_haji_dudung is rigged by rig_rodin.py instead.
    # npc_abah_aceng is rigged by rig_rodin.py instead.
    # npc_mang_jagung is rigged by rig_rodin.py instead.
    # tugu_kujang_hd was generated too, but the procedural Blender monument reads better in game.
    # "tugu_kujang_hd": (25.0, 6000, 1024),
    "tas_misterius_hd": (0.42, 1800, 512),
}


def process(name, height, budget, tex):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=os.path.join(SRC, name + ".glb"))
    meshes = [o for o in bpy.context.scene.objects if o.type == "MESH"]
    bpy.ops.object.select_all(action="DESELECT")
    for o in meshes:
        o.select_set(True)
    bpy.context.view_layer.objects.active = meshes[0]
    if len(meshes) > 1:
        bpy.ops.object.join()
    obj = bpy.context.view_layer.objects.active
    # Detach from any imported parent, keeping the world transform, then bake it in.
    world = obj.matrix_world.copy()
    obj.parent = None
    obj.matrix_world = world
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)

    tris = sum(len(p.vertices) - 2 for p in obj.data.polygons)
    if tris > budget:
        mod = obj.modifiers.new("decimate", "DECIMATE")
        mod.ratio = budget / tris
        bpy.ops.object.modifier_apply(modifier=mod.name)

    corners = [Vector(c) for c in obj.bound_box]
    low = Vector((min(c.x for c in corners), min(c.y for c in corners), min(c.z for c in corners)))
    high = Vector((max(c.x for c in corners), max(c.y for c in corners), max(c.z for c in corners)))
    scale = height / (high.z - low.z)
    offset = Vector(((low.x + high.x) / 2, (low.y + high.y) / 2, low.z))
    for v in obj.data.vertices:
        v.co = (v.co - offset) * scale
    obj.data.update()

    # Keep only the base colour texture, shrunk; the game lights these simply.
    for mat in obj.data.materials:
        tree = mat.node_tree
        bsdf = next(n for n in tree.nodes if n.type == "BSDF_PRINCIPLED")
        for socket in ("Normal", "Metallic", "Roughness"):
            for link in list(bsdf.inputs[socket].links):
                tree.links.remove(link)
        bsdf.inputs["Metallic"].default_value = 0.0
        bsdf.inputs["Roughness"].default_value = 0.85
        used = {l.from_node for l in bsdf.inputs["Base Color"].links}
        for node in list(tree.nodes):
            if node.type == "TEX_IMAGE":
                if node in used and node.image:
                    node.image.scale(tex, tex)
                else:
                    tree.nodes.remove(node)

    obj.name = name
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    out = os.path.join(DST, name + ".glb")
    bpy.ops.export_scene.gltf(filepath=out, export_format="GLB", use_selection=True,
                              export_image_format="JPEG", export_jpeg_quality=82,
                              export_animations=False, export_yup=True)
    final = sum(len(p.vertices) - 2 for p in obj.data.polygons)
    dims = obj.dimensions
    print(f"RESULT {name}: {tris} -> {final} tris, {dims.x:.2f} x {dims.y:.2f} x {dims.z:.2f} m, {os.path.getsize(out) // 1024} KB")


for name, (height, budget, tex) in TARGETS.items():
    if os.path.exists(os.path.join(SRC, name + ".glb")):
        process(name, height, budget, tex)
