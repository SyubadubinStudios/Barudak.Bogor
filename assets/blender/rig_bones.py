"""Gives the T-pose Rodin characters a skeleton, skin weights and animation clips.

    blender --background --python assets/blender/rig_bones.py [-- name ...]

Writes assets/models/<name>_rig.glb: one skinned mesh, twelve bones and four clips
(idle, walk, run, sit). The game uses it for the player; the part-sliced <name>.glb from
rig_rodin.py stays, for the crowd, where a skinned mesh per person would cost too much.

The weights are painted by rule, not by Blender's automatic weighting, which gives up on
generated meshes that are not watertight: a vertex belongs to the bones whose stretch of the
body it lies in, blended over a few centimetres at each joint.

Bones: hips > spine > chest > (head, arm_l > forearm_l, arm_r > forearm_r), hips > leg_l > shin_l,
hips > leg_r > shin_r. The character faces -Y in Blender (+Z in glTF) and its left is +X.

Set RIG_PREVIEW to a PNG path to render the last character in a few poses.
"""
import math
import os
import sys

import bpy
from mathutils import Matrix, Vector

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from rig_rodin import CHARACTERS, DST, SRC, load  # noqa: E402

FPS = 30
ARM_DOWN = math.radians(74)


def clamp01(t):
    return max(0.0, min(1.0, t))


def ss(a, b, x):
    t = clamp01((x - a) / (b - a))
    return t * t * (3 - 2 * t)


def measure(body, h):
    """Where the joints are, read off the mesh the same way rig_rodin.py does."""
    verts = [v.co for v in body.data.vertices]
    best, neck = 1e9, 0.86 * h
    for i in range(12):
        z = (0.82 + 0.008 * i) * h
        xs = [v.x for v in verts if abs(v.z - z) < 0.012 * h and abs(v.x) < 0.2 * h]
        if len(xs) > 3 and max(xs) - min(xs) < best:
            best, neck = max(xs) - min(xs), z
    arm = [v for v in verts if abs(v.x) > 0.3 * h]
    arm_z = sum(v.z for v in arm) / len(arm)
    arm_y = sum(v.y for v in arm) / len(arm)
    neck = max(neck, arm_z + 0.045 * h)
    shoulder = 0.118 * h
    tip = max(abs(v.x) for v in verts)
    # The arms slope down to the hands: the shoulder joint is measured at the shoulder, the wrist at the hand.
    joint = [v for v in verts if shoulder < abs(v.x) < shoulder + 0.07 * h and v.z > 0.6 * h]
    sh_z = (max(v.z for v in joint) + min(v.z for v in joint)) / 2 if joint else arm_z
    hand = [v for v in verts if abs(v.x) > tip - 0.06 * h]
    hand_z = sum(v.z for v in hand) / len(hand)
    knee = 0.27 * h
    shin = [v for v in verts if v.x > 0.01 * h and abs(v.z - knee) < 0.03 * h and v.z < 0.4 * h]
    leg_x = sum(v.x for v in shin) / len(shin) if shin else 0.05 * h
    leg_y = sum(v.y for v in shin) / len(shin) if shin else 0.0
    return {
        "hip": 0.47 * h, "knee": knee, "neck": neck, "arm_z": arm_z, "arm_y": arm_y, "sh_z": sh_z, "hand_z": hand_z,
        "shoulder": shoulder, "elbow": shoulder + 0.44 * (tip - shoulder), "tip": tip,
        "leg_x": min(max(leg_x, 0.035 * h), 0.08 * h), "leg_y": leg_y,
    }


def build_armature(name, m, h):
    data = bpy.data.armatures.new(name + "_bones")
    rig = bpy.data.objects.new(name + "_rig", data)
    bpy.context.scene.collection.objects.link(rig)
    bpy.ops.object.select_all(action="DESELECT")
    bpy.context.view_layer.objects.active = rig
    rig.select_set(True)
    bpy.ops.object.mode_set(mode="EDIT")

    def bone(label, head, tail, parent=None):
        b = data.edit_bones.new(label)
        b.head, b.tail, b.parent = Vector(head), Vector(tail), parent
        return b

    hip, knee, neck = m["hip"], m["knee"], m["neck"]
    hips = bone("hips", (0, 0, hip), (0, 0, hip + 0.07 * h))
    spine = bone("spine", (0, 0, hip + 0.07 * h), (0, 0, 0.6 * h), hips)
    chest = bone("chest", (0, 0, 0.6 * h), (0, 0, neck), spine)
    bone("head", (0, 0, neck), (0, 0, h), chest)
    for side, tag in ((1, "l"), (-1, "r")):
        elbow_z = m["sh_z"] + 0.44 * (m["hand_z"] - m["sh_z"])
        upper = bone("arm_" + tag, (side * m["shoulder"], m["arm_y"], m["sh_z"]), (side * m["elbow"], m["arm_y"], elbow_z), chest)
        bone("forearm_" + tag, (side * m["elbow"], m["arm_y"], elbow_z), (side * m["tip"], m["arm_y"], m["hand_z"]), upper)
        thigh = bone("leg_" + tag, (side * m["leg_x"], m["leg_y"], hip), (side * m["leg_x"], m["leg_y"], knee), hips)
        bone("shin_" + tag, (side * m["leg_x"], m["leg_y"], knee), (side * m["leg_x"], m["leg_y"], 0.03 * h), thigh)
    bpy.ops.object.mode_set(mode="OBJECT")
    return rig


def weights_of(co, m, h):
    x, z = co.x, co.z
    ax = abs(x)
    tag = "l" if x >= 0 else "r"
    out = {}

    # Arms: everything beyond the shoulder line at arm height (the T-pose holds them level).
    arm = ss(m["shoulder"] - 0.03 * h, m["shoulder"] + 0.035 * h, ax) if z > min(m["arm_z"], m["hand_z"]) - 0.14 * h else 0.0
    if arm > 0:
        fore = ss(m["elbow"] - 0.03 * h, m["elbow"] + 0.03 * h, ax)
        out["arm_" + tag] = arm * (1 - fore)
        out["forearm_" + tag] = arm * fore
    rest = 1 - arm
    if rest <= 0:
        return out

    # Legs: below the hip line, split left and right with a soft seam so a skirt stretches instead of tearing.
    leg = 1 - ss(m["hip"] - 0.06 * h, m["hip"] + 0.01 * h, z)
    if leg > 0:
        left = ss(-0.015 * h, 0.015 * h, x)
        lower = 1 - ss(m["knee"] - 0.035 * h, m["knee"] + 0.035 * h, z)
        for t, share in (("l", left), ("r", 1 - left)):
            out["leg_" + t] = out.get("leg_" + t, 0) + rest * leg * share * (1 - lower)
            out["shin_" + t] = out.get("shin_" + t, 0) + rest * leg * share * lower
    column = rest * (1 - leg)
    if column > 0:
        head = ss(m["neck"] - 0.015 * h, m["neck"] + 0.02 * h, z)
        chest = ss(0.55 * h, 0.66 * h, z)
        spine = ss(m["hip"], m["hip"] + 0.09 * h, z)
        out["head"] = column * head
        out["chest"] = column * (1 - head) * chest
        out["spine"] = column * (1 - head) * (1 - chest) * spine
        out["hips"] = column * (1 - head) * (1 - chest) * (1 - spine)
    return out


def skin(body, rig, m, h):
    groups = {b.name: body.vertex_groups.new(name=b.name) for b in rig.data.bones}
    for v in body.data.vertices:
        w = weights_of(v.co, m, h)
        total = sum(w.values())
        # glTF keeps four influences per vertex: the four strongest.
        top = sorted(w.items(), key=lambda kv: -kv[1])[:4]
        total = sum(value for _, value in top)
        for label, value in top:
            if value / total > 0.004:
                groups[label].add([v.index], value / total, "REPLACE")
    body.parent = rig
    mod = body.modifiers.new("armature", "ARMATURE")
    mod.object = rig


class Animator:
    def __init__(self, rig, h):
        self.rig, self.h = rig, h
        self.rest = {b.name: b.matrix_local.to_3x3() for b in rig.data.bones}
        self.last = {}
        rig.animation_data_create()
        for pb in rig.pose.bones:
            pb.rotation_mode = "QUATERNION"

    def turn(self, bone, frame, rx=0.0, ry=0.0, rz=0.0):
        """Turns a bone about the character's own axes (X right-left, Y back, Z up), relative to its parent."""
        rest = self.rest[bone]
        world = Matrix.Rotation(rx, 3, "X") @ Matrix.Rotation(ry, 3, "Y") @ Matrix.Rotation(rz, 3, "Z")
        q = (rest.inverted() @ world @ rest).to_quaternion()
        if bone in self.last and q.dot(self.last[bone]) < 0:
            q.negate()
        self.last[bone] = q.copy()
        pb = self.rig.pose.bones[bone]
        pb.rotation_quaternion = q
        pb.keyframe_insert("rotation_quaternion", frame=frame)

    def lift(self, frame, dz):
        pb = self.rig.pose.bones["hips"]
        # The hips bone points up, so its local Y is the character's up.
        pb.location = Vector((0, dz, 0))
        pb.keyframe_insert("location", frame=frame)

    def clip(self, name, seconds, pose, steps):
        action = bpy.data.actions.new(name)
        self.rig.animation_data.action = action
        self.last = {}
        frames = round(seconds * FPS)
        for i in range(steps + 1):
            pose(self, round(i * frames / steps), i / steps)
        track = self.rig.animation_data.nla_tracks.new()
        track.name = name
        track.strips.new(name, 0, action)
        self.rig.animation_data.action = None
        for pb in self.rig.pose.bones:
            pb.rotation_quaternion = (1, 0, 0, 0)
            pb.location = (0, 0, 0)

    def arms(self, frame, swing_l, swing_r, bend_l, bend_r, spread=0.0):
        for side, tag, swing, bend in ((1, "l", swing_l, bend_l), (-1, "r", swing_r, bend_r)):
            # Down from the T-pose first, then swung about the shoulder; a forward swing is a negative turn about X.
            self.turn("arm_" + tag, frame, rx=swing, ry=side * (ARM_DOWN - spread))
            self.turn("forearm_" + tag, frame, rz=-side * bend)

    def legs(self, frame, thigh_l, thigh_r, knee_l, knee_r):
        self.turn("leg_l", frame, rx=thigh_l)
        self.turn("leg_r", frame, rx=thigh_r)
        self.turn("shin_l", frame, rx=knee_l)
        self.turn("shin_r", frame, rx=knee_r)


def idle(a, frame, u):
    breath = math.sin(u * math.tau)
    a.turn("hips", frame)
    a.turn("spine", frame, rx=0.01 * breath)
    a.turn("chest", frame, rx=0.015 * breath)
    a.turn("head", frame, rx=-0.01 * breath, ry=0.0, rz=0.04 * math.sin(u * math.tau * 0.5 + 0.7) ** 3)
    a.arms(frame, 0.02 * breath, 0.02 * breath, 0.16, 0.16, spread=0.02 * breath)
    a.legs(frame, 0.0, 0.0, 0.0, 0.0)
    a.lift(frame, 0.0)


def stride(swing, knee, arm, elbow, lean, bob, twist):
    def pose(a, frame, u):
        s = math.sin(u * math.tau)
        c = math.cos(u * math.tau)
        # The left leg leads when s > 0. A knee folds while its leg is coming forward.
        a.legs(frame, -swing * s - 0.06, swing * s - 0.06, knee * max(0.0, c) ** 1.5 + 0.12, knee * max(0.0, -c) ** 1.5 + 0.12)
        a.arms(frame, arm * s, -arm * s, elbow + 0.3 * elbow * max(0.0, -s), elbow + 0.3 * elbow * max(0.0, s))
        a.turn("hips", frame, rx=lean * 0.4, rz=twist * s)
        a.turn("spine", frame, rx=lean * 0.3, rz=-twist * s)
        a.turn("chest", frame, rx=lean * 0.3, rz=-twist * s)
        a.turn("head", frame, rx=-lean * 0.5, rz=twist * s)
        # Lowest with the legs spread, highest as one passes the other.
        a.lift(frame, -bob * a.h * (0.5 - 0.5 * math.cos(u * 2 * math.tau)))
    return pose


def sit(a, frame, u):
    a.turn("hips", frame, rx=-0.06)
    a.turn("spine", frame, rx=0.05)
    a.turn("chest", frame, rx=0.05)
    a.turn("head", frame)
    a.arms(frame, -0.85, -0.85, 0.45, 0.45, spread=0.1)
    a.legs(frame, -1.35, -1.35, 1.3, 1.3)
    a.lift(frame, 0.0)


def rig_character(name, source, height):
    body, span = load(os.path.join(SRC, source), height)
    if span <= 0.8 * height:
        print(f"SKIP {name}: not a T-pose")
        return None
    body.name = name
    bpy.context.scene.render.fps = FPS
    m = measure(body, height)
    rig = build_armature(name, m, height)
    skin(body, rig, m, height)

    a = Animator(rig, height)
    a.clip("idle", 3.0, idle, 12)
    a.clip("walk", 1.0, stride(swing=0.5, knee=0.85, arm=0.42, elbow=0.22, lean=0.03, bob=0.012, twist=0.07), 16)
    a.clip("run", 0.7, stride(swing=0.85, knee=1.6, arm=0.8, elbow=1.25, lean=0.2, bob=0.028, twist=0.12), 16)
    a.clip("sit", 1.0, sit, 1)

    bpy.ops.object.select_all(action="DESELECT")
    rig.select_set(True)
    body.select_set(True)
    out = os.path.join(DST, name + "_rig.glb")
    bpy.ops.export_scene.gltf(filepath=out, export_format="GLB", use_selection=True,
                              export_image_format="JPEG", export_jpeg_quality=82,
                              export_animations=True, export_animation_mode="NLA_TRACKS",
                              export_skins=True, export_yup=True)
    tris = sum(len(p.vertices) - 2 for p in body.data.polygons)
    print(f"RESULT {name}: {len(rig.data.bones)} bones, {tris} tris, {os.path.getsize(out) // 1024} KB")
    return rig, body


def preview(rig, body, path):
    """The character in five poses, seen from front-left."""
    scene = bpy.context.scene
    shots = [("idle", 0), ("walk", 8), ("walk", 22), ("run", 5), ("sit", 0)]
    actions = {t.name: t.strips[0].action for t in rig.animation_data.nla_tracks}
    for t in rig.animation_data.nla_tracks:
        t.mute = True
    copies = []
    for i, (clip, frame) in enumerate(shots):
        rig.animation_data.action = actions[clip]
        scene.frame_set(frame)
        bpy.context.view_layer.update()
        deps = bpy.context.evaluated_depsgraph_get()
        mesh = bpy.data.meshes.new_from_object(body.evaluated_get(deps))
        copy = bpy.data.objects.new(f"shot{i}", mesh)
        copy.location = (i * 1.2, 0, 0)
        scene.collection.objects.link(copy)
        copies.append(copy)
    body.hide_render = True
    cam_data = bpy.data.cameras.new("cam")
    cam_data.type = "ORTHO"
    cam_data.ortho_scale = len(shots) * 1.2 + 0.4
    cam = bpy.data.objects.new("cam", cam_data)
    scene.collection.objects.link(cam)
    centre = Vector(((len(shots) - 1) * 0.6, 0, 0.9))
    cam.location = centre + Vector((6, -8, 1.5))
    cam.rotation_euler = (centre - cam.location).to_track_quat("-Z", "Y").to_euler()
    scene.camera = cam
    scene.render.engine = "BLENDER_WORKBENCH"
    scene.display.shading.light = "FLAT"
    scene.display.shading.color_type = "TEXTURE"
    scene.render.resolution_x = 1800
    scene.render.resolution_y = 620
    scene.render.filepath = path
    bpy.ops.render.render(write_still=True)


if __name__ == "__main__":
    wanted = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else list(CHARACTERS)
    made = None
    for character in wanted:
        source_file, metres = CHARACTERS[character]
        if os.path.exists(os.path.join(SRC, source_file)):
            made = rig_character(character, source_file, metres) or made
    if made and os.environ.get("RIG_PREVIEW"):
        preview(*made, os.environ["RIG_PREVIEW"])
