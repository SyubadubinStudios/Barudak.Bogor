"""Cuts Rodin (Hyper3D) characters into the named parts the game animates.

    blender --background --python assets/blender/rig_rodin.py [-- name ...]

The game has no skeletal animation: it swings named child nodes (leg_l, leg_r, arm_l, arm_r,
head) around their origins. A generated character is one fused mesh, so this script

  1. decimates it and stands it on the ground at a real-world height,
  2. slices it at the hips, the neck and (for T-pose characters) the shoulders,
  3. turns T-pose arms down to hang at the sides,
  4. puts each part's origin on its joint and exports root > torso > (head, arm_l, arm_r), leg_l, leg_r.

Characters generated in an A-pose keep their arms fused to the torso and only get legs.
Each part is a single textured primitive, so a whole character costs at most six draw calls.
"""
import math
import os
import sys

import bmesh
import bpy
from mathutils import Matrix, Vector

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SRC = os.path.join(ROOT, "models_hd")
DST = os.path.join(ROOT, "models")

# output name -> (source file relative to models_hd, height in metres)
CHARACTERS = {
    "hero_ujang": ("chars/ujang.glb", 1.72),
    "hero_cecep": ("chars/cecep.glb", 1.68),
    "hero_mumu": ("chars/mumu.glb", 1.60),
    "hero_deden": ("chars/deden.glb", 1.68),
    "hero_aceng": ("chars/aceng.glb", 1.66),
    "npc_bapak": ("chars/npc_bapak.glb", 1.68),
    "npc_ibu": ("chars/npc_ibu.glb", 1.56),
    "npc_anak_sd": ("chars/npc_anak_sd.glb", 1.28),
    "npc_mang_udin": ("npc_mang_udin.glb", 1.68),
    "npc_haji_dadang": ("npc_haji_dadang.glb", 1.66),
    "npc_bu_euis": ("npc_bu_euis.glb", 1.56),
    "npc_guru_bp": ("npc_guru_bp.glb", 1.65),
    "npc_haji_dudung": ("npc_haji_dudung.glb", 1.64),
    "npc_abah_aceng": ("npc_abah_aceng.glb", 1.60),
    "npc_mang_jagung": ("npc_mang_jagung.glb", 1.67),
}

TRIANGLES = 2600
TEXTURE = 512


def load(path, height):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=path)
    meshes = [o for o in bpy.context.scene.objects if o.type == "MESH"]
    bpy.ops.object.select_all(action="DESELECT")
    for o in meshes:
        o.select_set(True)
    bpy.context.view_layer.objects.active = meshes[0]
    if len(meshes) > 1:
        bpy.ops.object.join()
    obj = bpy.context.view_layer.objects.active
    world = obj.matrix_world.copy()
    obj.parent = None
    obj.matrix_world = world
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)

    tris = sum(len(p.vertices) - 2 for p in obj.data.polygons)
    if tris > TRIANGLES:
        mod = obj.modifiers.new("decimate", "DECIMATE")
        mod.ratio = TRIANGLES / tris
        bpy.ops.object.modifier_apply(modifier=mod.name)

    xs = [v.co.x for v in obj.data.vertices]
    ys = [v.co.y for v in obj.data.vertices]
    zs = [v.co.z for v in obj.data.vertices]
    scale = height / (max(zs) - min(zs))
    # Centre on the body, not on the arm span or a backpack: use the feet.
    low = [v.co for v in obj.data.vertices if v.co.z < min(zs) + 0.12 * (max(zs) - min(zs))]
    cx = (min(v.x for v in low) + max(v.x for v in low)) / 2
    cy = (min(ys) + max(ys)) / 2
    for v in obj.data.vertices:
        v.co = Vector(((v.co.x - cx) * scale, (v.co.y - cy) * scale, (v.co.z - min(zs)) * scale))
    obj.data.update()

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
                    node.image.scale(TEXTURE, TEXTURE)
                else:
                    tree.nodes.remove(node)
    return obj, (max(xs) - min(xs)) * scale


def piece(source, name, planes):
    """A copy of the mesh keeping only what lies behind every plane (point, outward normal)."""
    obj = source.copy()
    obj.data = source.data.copy()
    obj.name = name
    bpy.context.scene.collection.objects.link(obj)
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    for point, normal in planes:
        geom = bm.verts[:] + bm.edges[:] + bm.faces[:]
        result = bmesh.ops.bisect_plane(bm, geom=geom, plane_co=point, plane_no=normal, clear_outer=True, clear_inner=False)
        edges = [e for e in result["geom_cut"] if isinstance(e, bmesh.types.BMEdge)]
        if edges:
            # Close the cut so a swinging limb never shows a hole.
            bmesh.ops.holes_fill(bm, edges=edges)
    bm.to_mesh(obj.data)
    bm.free()
    return obj


def shoulder_ball(torso, body, centre, radius, sample):
    """Adds a ball to the torso at a shoulder joint, in the colour of the sleeve.

    The arm is a separate part that swings about this point; however it is cut, turning it
    opens a wedge between its top and the shoulder. The ball fills that wedge from every angle.
    """
    uv_layer = body.data.uv_layers.active
    nearest = min(body.data.vertices, key=lambda v: (v.co - sample).length_squared).index
    uv = next((uv_layer.data[i].uv.copy() for i, loop in enumerate(body.data.loops) if loop.vertex_index == nearest), None) if uv_layer else None
    bm = bmesh.new()
    bm.from_mesh(torso.data)
    made = bmesh.ops.create_uvsphere(bm, u_segments=10, v_segments=6, radius=radius)["verts"]
    for v in made:
        v.co += centre
    layer = bm.loops.layers.uv.active
    if layer is not None and uv is not None:
        fresh = set(made)
        for face in bm.faces:
            if all(v in fresh for v in face.verts):
                for loop in face.loops:
                    loop[layer].uv = uv
    bm.to_mesh(torso.data)
    bm.free()


def join(a, b):
    """Merges b into a."""
    bpy.ops.object.select_all(action="DESELECT")
    a.select_set(True)
    b.select_set(True)
    bpy.context.view_layer.objects.active = a
    bpy.ops.object.join()
    return a


def set_origin(obj, pivot):
    for v in obj.data.vertices:
        v.co -= pivot
    obj.data.update()
    obj.location = pivot


def rig(name, source_file, height):
    body, span = load(os.path.join(SRC, source_file), height)
    h = height
    tpose = span > 0.8 * h
    hip = 0.47 * h
    X, Y, Z = Vector((1, 0, 0)), Vector((0, 1, 0)), Vector((0, 0, 1))

    root = bpy.data.objects.new(name, None)
    bpy.context.scene.collection.objects.link(root)
    parts = []

    leg_l = piece(body, "leg_l", [(Vector((0, 0, hip)), Z), (Vector((0, 0, 0)), -X)])
    leg_r = piece(body, "leg_r", [(Vector((0, 0, hip)), Z), (Vector((0, 0, 0)), X)])
    set_origin(leg_l, Vector((0.05 * h, 0, hip)))
    set_origin(leg_r, Vector((-0.05 * h, 0, hip)))

    if tpose:
        # The neck is the narrowest slice between the chin and the shoulders.
        best, neck = 1e9, 0.86 * h
        for i in range(12):
            z = (0.82 + 0.008 * i) * h
            xs = [v.co.x for v in body.data.vertices if abs(v.co.z - z) < 0.012 * h and abs(v.co.x) < 0.2 * h]
            if len(xs) > 3 and max(xs) - min(xs) < best:
                best, neck = max(xs) - min(xs), z
        arm_verts = [v.co for v in body.data.vertices if abs(v.co.x) > 0.3 * h]
        arm_z = sum(v.z for v in arm_verts) / max(len(arm_verts), 1)
        arm_y = sum(v.y for v in arm_verts) / max(len(arm_verts), 1)
        neck = max(neck, arm_z + 0.045 * h)
        shoulder = 0.118 * h
        torso_edge = shoulder + 0.012 * h
        # The arms of a generated T-pose slope down to the hands, so the joint is measured at the
        # shoulder itself, not at the average height of the arm.
        joint = [v.co for v in body.data.vertices if shoulder < abs(v.co.x) < shoulder + 0.07 * h and v.co.z > 0.6 * h]
        if joint:
            sh_top = max(v.z for v in joint)
            sh_z = (sh_top + min(v.z for v in joint)) / 2
            sh_y = sum(v.y for v in joint) / len(joint)
        else:
            sh_top, sh_z, sh_y = arm_z + 0.04 * h, arm_z, arm_y
        cap = sh_top + 0.012 * h

        torso = piece(body, "torso", [(Vector((0, 0, hip)), -Z), (Vector((0, 0, neck)), Z),
                                      (Vector((torso_edge, 0, 0)), X), (Vector((-torso_edge, 0, 0)), -X)])
        if cap > neck + 0.005 * h:
            # Where the top of the sleeves stands above the neck line, that slice belongs to the
            # arms: the head only takes what lies between the shoulders there.
            head = join(piece(body, "head", [(Vector((0, 0, cap)), -Z)]),
                        piece(body, "head_low", [(Vector((0, 0, neck)), -Z), (Vector((0, 0, cap)), Z),
                                                 (Vector((torso_edge, 0, 0)), X), (Vector((-torso_edge, 0, 0)), -X)]))
        else:
            head = piece(body, "head", [(Vector((0, 0, neck)), -Z)])
        arms = []
        for side, label in ((1, "arm_l"), (-1, "arm_r")):
            # The arm piece starts a little inside the shoulder, so that once it is turned down
            # its rounded top stays buried in the torso and the joint shows no gap.
            arm = piece(body, label, [(Vector((side * (shoulder - 0.032 * h), 0, 0)), -side * X), (Vector((0, 0, hip)), -Z),
                                      (Vector((0, 0, max(cap, neck))), Z)])
            pivot = Vector((side * shoulder, sh_y, sh_z))
            # Outstretched along X: turn it down about the front-back axis. Stopping short of vertical keeps
            # wide sleeves looking like sleeves instead of boxes on the shoulders.
            turn = Matrix.Rotation(side * math.radians(64), 4, "Y")
            for v in arm.data.vertices:
                v.co = pivot + (turn @ (v.co - pivot))
            arm.data.update()
            set_origin(arm, pivot)
            # Tuck the arm in against the ribs: the chest is narrower than the shoulder line.
            arm.location = pivot - Vector((side * 0.02 * h, 0, 0.012 * h))
            arms.append(arm)
            # Fill the joint: a ball as thick as the upper arm, sitting on the pivot.
            # Measured on the arm proper (well clear of the chest, collar and anything slung over the shoulder).
            sleeve = [v.co for v in body.data.vertices if shoulder + 0.03 * h < side * v.co.x < shoulder + 0.08 * h and abs(v.co.z - sh_z) < 0.09 * h]
            if sleeve:
                thick = min(max(v.z for v in sleeve) - min(v.z for v in sleeve), max(v.y for v in sleeve) - min(v.y for v in sleeve))
                radius = min(max(0.5 * thick, 0.052 * h), 0.062 * h)
                top = max(sleeve, key=lambda v: v.z)
                print(f"SHOULDER {name} {label}: radius {radius / h:.3f} h")
                # Level with the shoulder: its crown just under the top of the sleeve.
                centre = Vector((side * (shoulder - 0.012 * h), sh_y, max(sh_z, sh_top - radius)))
                shoulder_ball(torso, body, centre, radius, Vector(top))
        waist = Vector((0, 0, hip))
        set_origin(torso, waist)
        for child in (head, *arms):
            child.parent = torso
        head_pivot = Vector((0, 0, neck))
        set_origin(head, head_pivot)
        head.location = head_pivot - waist
        for arm in arms:
            arm.location = arm.location - waist
        parts = [torso, head, *arms]
    else:
        torso = piece(body, "torso", [(Vector((0, 0, hip)), -Z)])
        set_origin(torso, Vector((0, 0, hip)))
        parts = [torso]

    torso.parent = root
    leg_l.parent = root
    leg_r.parent = root
    parts += [leg_l, leg_r]
    bpy.data.objects.remove(body, do_unlink=True)

    bpy.ops.object.select_all(action="DESELECT")
    root.select_set(True)
    tris = 0
    for part in parts:
        part.select_set(True)
        tris += sum(len(p.vertices) - 2 for p in part.data.polygons)
    out = os.path.join(DST, name + ".glb")
    bpy.ops.export_scene.gltf(filepath=out, export_format="GLB", use_selection=True,
                              export_image_format="JPEG", export_jpeg_quality=82,
                              export_animations=False, export_yup=True)
    print(f"RESULT {name}: {'T-pose' if tpose else 'A-pose'}, {len(parts)} parts, {tris} tris, {os.path.getsize(out) // 1024} KB")


# rig_bones.py imports load() from this file, so the batch only runs when it is the script itself.
if __name__ == "__main__":
    wanted = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else list(CHARACTERS)
    for character in wanted:
        source, metres = CHARACTERS.get(character, ("", 0))
        if source and os.path.exists(os.path.join(SRC, source)):
            rig(character, source, metres)

    # Preview sheet: every rigged character side by side, front view.
    if os.environ.get("RIG_PREVIEW"):
        bpy.ops.wm.read_factory_settings(use_empty=True)
        x = 0.0
        for character in list(CHARACTERS)[:8]:
            path = os.path.join(DST, character + ".glb")
            if os.path.exists(path):
                before = set(bpy.context.scene.objects)
                bpy.ops.import_scene.gltf(filepath=path)
                for o in set(bpy.context.scene.objects) - before:
                    if o.parent is None:
                        o.location.x = x
                x += 1.0
        scene = bpy.context.scene
        cam_data = bpy.data.cameras.new("cam")
        cam_data.type = "ORTHO"
        cam_data.ortho_scale = x + 0.2
        cam = bpy.data.objects.new("cam", cam_data)
        scene.collection.objects.link(cam)
        cam.location = ((x - 1.0) / 2, -10, 1.05)
        cam.rotation_euler = (math.radians(90), 0, 0)
        scene.camera = cam
        scene.render.engine = "BLENDER_WORKBENCH"
        scene.display.shading.light = "FLAT"
        scene.display.shading.color_type = "TEXTURE"
        scene.render.resolution_x = 4200
        scene.render.resolution_y = 700
        scene.render.filepath = os.environ["RIG_PREVIEW"]
        bpy.ops.render.render(write_still=True)
