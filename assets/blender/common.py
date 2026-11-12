"""Barudak Bogor - shared helpers for the procedural low-poly asset generators.

Conventions (the game code depends on these):
  * metres, Blender Z-up, asset FRONT faces Blender -Y (glTF +Z after export)
  * therefore the asset's LEFT side is Blender +X, its RIGHT side is -X
  * origin at the centre of the footprint on the ground (lowest point z = 0)
  * one root Empty named like the asset, child meshes ("body" + moving parts whose
    object origin sits on the pivot)
  * flat Principled materials only, shared by name across all assets
"""
import bpy, bmesh, math, os
from mathutils import Matrix, Vector, Euler

SCENE_NAME = "BarudakBogor_Assets"
HERE = os.path.dirname(os.path.abspath(__file__))
ROOT_DIR = os.path.dirname(os.path.dirname(HERE))
MODELS_DIR = os.path.join(ROOT_DIR, "assets", "models")
IMAGES_DIR = os.path.join(ROOT_DIR, "docs", "images")

BUDGET = {"vehicle": 2500, "character": 900, "animal": 700, "prop": 600,
          "building": 2000, "landmark": 2000, "tree": 400}
FAMILY_ORDER = ["vehicle", "character", "animal", "prop", "building", "tree", "landmark"]

# name: (sRGB hex, roughness, metallic, emission strength)
PALETTE = {
    # generic
    "glass": ("#2a3644", 0.1, 0.0, 0), "black": ("#141414", 0.8, 0, 0),
    "rubber": ("#1b1b1d", 0.9, 0, 0), "hub_grey": ("#9a9da3", 0.45, 0.6, 0),
    "chrome": ("#c8ccd2", 0.25, 0.9, 0), "steel": ("#7d848c", 0.45, 0.7, 0),
    "iron_dark": ("#26282b", 0.6, 0.6, 0), "aluminium": ("#b9bec4", 0.35, 0.8, 0),
    "bumper": ("#2b2d30", 0.7, 0, 0), "interior_dark": ("#1d1f24", 0.9, 0, 0),
    "engine_grey": ("#55595f", 0.6, 0.4, 0), "seat_black": ("#222226", 0.8, 0, 0),
    "white": ("#ecece6", 0.7, 0, 0), "concrete": ("#b5b3aa", 0.9, 0, 0),
    "concrete_white": ("#e6e3d8", 0.85, 0, 0), "asphalt": ("#3a3b3e", 0.95, 0, 0),
    "red": ("#c22a25", 0.6, 0, 0), "yellow": ("#e8b923", 0.6, 0, 0),
    "blue": ("#2468b4", 0.6, 0, 0), "orange": ("#e07a1f", 0.7, 0, 0),
    "gold": ("#d9a520", 0.3, 0.9, 0), "brass": ("#b8892b", 0.4, 0.8, 0),
    # emissive
    "emit_headlight": ("#fff3c4", 0.3, 0, 3.0), "emit_taillight": ("#ff2a1a", 0.3, 0, 1.5),
    "emit_lamp": ("#ffe3a0", 0.3, 0, 5.0), "emit_sign": ("#ffe45c", 0.4, 0, 1.5),
    "emit_window_eerie": ("#b6d67a", 0.4, 0, 1.2),
    # vehicles
    "angkot_green": ("#1f9a45", 0.5, 0.1, 0), "angkot_stripe": ("#bfe86a", 0.5, 0.1, 0),
    "car_silver": ("#c3c7cc", 0.35, 0.3, 0), "bajaj_blue": ("#1f6fd0", 0.5, 0.1, 0),
    "canvas_black": ("#232326", 0.9, 0, 0), "moto_red": ("#c8281f", 0.4, 0.2, 0),
    "pickup_white": ("#e4e5e2", 0.5, 0.1, 0), "pickup_blue": ("#2c4f9e", 0.5, 0.1, 0),
    "sign_yellow": ("#f4c81e", 0.6, 0, 0), "sign_red": ("#d2281e", 0.6, 0, 0),
    "tarp_blue": ("#2f6fc4", 0.8, 0, 0), "tarp_orange": ("#e2681c", 0.8, 0, 0),
    "tofu": ("#c98d3a", 0.8, 0, 0), "bakso": ("#b9a595", 0.8, 0, 0),
    "broth": ("#a4712c", 0.4, 0, 0), "cart_green": ("#2f8f5a", 0.7, 0, 0),
    "cart_blue": ("#3277b8", 0.7, 0, 0), "cart_yellow": ("#e9c53a", 0.7, 0, 0),
    "delman_red": ("#a5261f", 0.6, 0, 0), "delman_green": ("#1f5a35", 0.7, 0, 0),
    "taxi_blue": ("#a6dcf5", 0.4, 0.2, 0), "bemo_blue": ("#74bde8", 0.5, 0.1, 0),
    "truck_yellow": ("#e6b422", 0.5, 0.1, 0), "truck_green": ("#2a7a48", 0.75, 0, 0),
    # wood / nature
    "wood": ("#8a5a2f", 0.85, 0, 0), "wood_dark": ("#55361c", 0.9, 0, 0),
    "wood_light": ("#b98d55", 0.85, 0, 0), "bamboo": ("#a9b25a", 0.7, 0, 0),
    "bamboo_dry": ("#c6a85a", 0.8, 0, 0), "thatch": ("#8b7340", 0.95, 0, 0),
    "bark": ("#5d4630", 0.95, 0, 0), "bark_light": ("#8d7b62", 0.95, 0, 0),
    "leaf": ("#2f7a34", 0.9, 0, 0), "leaf_dark": ("#1f5a2a", 0.9, 0, 0),
    "leaf_light": ("#5da33a", 0.9, 0, 0), "tea_green": ("#3f8f2f", 0.9, 0, 0),
    "pine_green": ("#1f5540", 0.9, 0, 0), "coconut": ("#6d5a2a", 0.8, 0, 0),
    "chili": ("#c8201a", 0.6, 0, 0), "veg_green": ("#58a02c", 0.7, 0, 0),
    "veg_orange": ("#e8861c", 0.7, 0, 0), "veg_purple": ("#6a2f78", 0.6, 0, 0),
    # buildings
    "roof_tile": ("#8f3f2a", 0.85, 0, 0), "roof_dark": ("#3b3338", 0.85, 0, 0),
    "zinc": ("#8f979c", 0.5, 0.6, 0), "wall_cream": ("#d8cfae", 0.9, 0, 0),
    "wall_villa": ("#a8a898", 0.95, 0, 0), "wall_moss": ("#6f7a62", 0.95, 0, 0),
    "wall_green": ("#7fb38a", 0.9, 0, 0), "window_dark": ("#16191f", 0.2, 0, 0),
    "door_wood": ("#4a2f1c", 0.8, 0, 0), "billboard_face": ("#f2f2f2", 0.6, 0, 0),
    "sign_blue": ("#1d5fb8", 0.5, 0, 0), "bag_olive": ("#4b5234", 0.9, 0, 0),
    "bag_strap": ("#23261c", 0.9, 0, 0), "stone": ("#8d8d86", 0.95, 0, 0),
    # characters
    "skin": ("#c68e62", 0.8, 0, 0), "skin_light": ("#dcaa80", 0.8, 0, 0),
    "skin_dark": ("#a8744a", 0.8, 0, 0), "hair": ("#18140f", 0.8, 0, 0),
    "eye": ("#101010", 0.5, 0, 0), "shoe": ("#1e1e20", 0.8, 0, 0),
    "shoe_white": ("#dddddd", 0.8, 0, 0), "sandal": ("#3d2a1a", 0.8, 0, 0),
    "cloth_white": ("#eeeeea", 0.85, 0, 0), "cloth_grey": ("#7b8087", 0.85, 0, 0),
    "cloth_navy": ("#1f2c4a", 0.85, 0, 0), "cloth_black": ("#1c1c20", 0.85, 0, 0),
    "cloth_khaki": ("#a8935f", 0.85, 0, 0), "cloth_red": ("#c0261f", 0.85, 0, 0),
    "cloth_blue": ("#3a6fb0", 0.85, 0, 0), "cloth_lightblue": ("#a9c8e6", 0.85, 0, 0),
    "cloth_green": ("#2e9b4c", 0.85, 0, 0), "cloth_yellow": ("#e3b62a", 0.85, 0, 0),
    "cloth_orange": ("#d9742a", 0.85, 0, 0), "cloth_pink": ("#e39ab0", 0.85, 0, 0),
    "cloth_purple": ("#6f4a8e", 0.85, 0, 0), "cloth_brown": ("#6b4a2f", 0.85, 0, 0),
    "cloth_olive": ("#5d6a3a", 0.85, 0, 0), "cloth_teal": ("#1f8a88", 0.85, 0, 0),
    "denim": ("#34507a", 0.85, 0, 0), "sarong": ("#7a2f4a", 0.85, 0, 0),
    "ojol_green": ("#16a34a", 0.7, 0, 0), "towel": ("#f0efe6", 0.95, 0, 0),
    # animals
    "deer": ("#b0703a", 0.9, 0, 0), "deer_spot": ("#f1e6cf", 0.9, 0, 0),
    "antler": ("#cdbb96", 0.8, 0, 0), "squirrel": ("#8a5326", 0.9, 0, 0),
    "monkey": ("#8c7a5e", 0.9, 0, 0), "monkey_face": ("#c79a86", 0.8, 0, 0),
    "cat_orange": ("#e08a2c", 0.9, 0, 0), "cat_white": ("#f2ead8", 0.9, 0, 0),
    "buffalo": ("#45464b", 0.9, 0, 0), "horn": ("#2b2925", 0.6, 0, 0),
    "cow_brown": ("#8a5230", 0.9, 0, 0), "cow_white": ("#efe9dc", 0.9, 0, 0),
    "udder": ("#e0a8a0", 0.8, 0, 0), "goat": ("#d8d0bd", 0.9, 0, 0),
    "goat_dark": ("#5b4a3a", 0.9, 0, 0), "hoof": ("#22201e", 0.8, 0, 0),
    "horse": ("#6b3f22", 0.9, 0, 0), "horse_mane": ("#1d1612", 0.9, 0, 0),
    "nose": ("#1a1614", 0.6, 0, 0),
    "dog_tan": ("#c79a5e", 0.9, 0, 0), "chicken_brown": ("#a8622a", 0.9, 0, 0),
    "chicken_orange": ("#dd8f2e", 0.9, 0, 0), "chicken_green": ("#1c4a38", 0.6, 0, 0),
    "chicken_leg": ("#d6a436", 0.7, 0, 0),
}

AX = {"z": Matrix.Identity(4),
      "x": Matrix.Rotation(math.pi / 2, 4, "Y"),
      "y": Matrix.Rotation(-math.pi / 2, 4, "X")}


def srgb(h):
    h = h.lstrip("#")
    lin = lambda c: c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4
    return tuple(lin(int(h[i:i + 2], 16) / 255) for i in (0, 2, 4)) + (1.0,)


def get_mat(name):
    hexc, rough, metal, emit = PALETTE[name]
    col = srgb(hexc)
    m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    try:
        if not m.use_nodes:
            m.use_nodes = True
    except Exception:
        pass
    nt = m.node_tree
    bsdf = next((n for n in nt.nodes if n.type == "BSDF_PRINCIPLED"), None)
    if bsdf is None:
        nt.nodes.clear()
        bsdf = nt.nodes.new("ShaderNodeBsdfPrincipled")
        out = nt.nodes.new("ShaderNodeOutputMaterial")
        nt.links.new(bsdf.outputs[0], out.inputs[0])
    ins = {i.identifier: i for i in bsdf.inputs}
    ins["Base Color"].default_value = col
    ins["Roughness"].default_value = rough
    ins["Metallic"].default_value = metal
    if emit:
        ins["Emission Color"].default_value = col
        ins["Emission Strength"].default_value = emit
    m.diffuse_color = col          # viewport / workbench colour
    m.roughness = rough
    m.metallic = metal
    return m


def trs(c, rot=(0, 0, 0), scale=(1, 1, 1)):
    R = Euler([math.radians(a) for a in rot], "XYZ").to_matrix().to_4x4()
    return Matrix.Translation(Vector(c)) @ R @ Matrix.Diagonal((scale[0], scale[1], scale[2], 1.0))


class Part:
    """One mesh object of an asset. Geometry is authored in asset space; the object
    origin ends up at `pivot`."""

    def __init__(self, name, pivot=(0, 0, 0), parent=None):
        self.name, self.pivot, self.parent = name, Vector(pivot), parent
        self.bm = bmesh.new()
        self.slots = []
        self.uv = None

    def _mi(self, mat):
        if mat not in PALETTE:
            raise KeyError("unknown material " + mat)
        if mat not in self.slots:
            self.slots.append(mat)
        return self.slots.index(mat)

    def _fin(self, verts, mat, M=None, smooth=False, smooth_fn=None):
        verts = [v for v in verts if v.is_valid]
        faces = set(f for v in verts for f in v.link_faces)
        mi = self._mi(mat)
        for f in faces:
            f.material_index = mi
            if smooth_fn:
                f.normal_update()
                f.smooth = smooth_fn(f)
            else:
                f.smooth = smooth
        if M is not None:
            bmesh.ops.transform(self.bm, matrix=M, verts=verts)
        return verts

    # ---- primitives -------------------------------------------------------
    def box(self, mat, c, size, rot=(0, 0, 0)):
        v = bmesh.ops.create_cube(self.bm, size=1.0)["verts"]
        return self._fin(v, mat, trs(c, rot, size))

    def tbox(self, mat, c, size, top=(1, 1), shift=(0, 0), rot=(0, 0, 0)):
        """Box whose top face (local +Z) is scaled by `top` and shifted by `shift` metres."""
        v = bmesh.ops.create_cube(self.bm, size=1.0)["verts"]
        for q in v:
            if q.co.z > 0:
                q.co.x = q.co.x * top[0] + shift[0] / size[0]
                q.co.y = q.co.y * top[1] + shift[1] / size[1]
        return self._fin(v, mat, trs(c, rot, size))

    def cyl(self, mat, c, r, h, axis="z", segs=8, r2=None, rot=(0, 0, 0),
            scale=(1, 1, 1), smooth=True, caps=True):
        """Cylinder / cone. r at the low end of the axis, r2 at the high end."""
        v = bmesh.ops.create_cone(self.bm, cap_ends=caps, cap_tris=False, segments=segs,
                                  radius1=r, radius2=r if r2 is None else r2, depth=h)["verts"]
        M = trs(c, rot, scale) @ AX[axis]
        fn = (lambda f: abs(f.normal.z) < 0.9) if smooth else None
        return self._fin(v, mat, M, smooth_fn=fn)

    def limb(self, mat, p0, p1, r0, r1=None, segs=6, smooth=True, caps=True):
        """Tapered cylinder from p0 to p1."""
        p0, p1 = Vector(p0), Vector(p1)
        d = p1 - p0
        v = bmesh.ops.create_cone(self.bm, cap_ends=caps, cap_tris=False, segments=segs,
                                  radius1=r0, radius2=r0 if r1 is None else r1,
                                  depth=d.length)["verts"]
        M = Matrix.Translation((p0 + p1) / 2) @ Vector((0, 0, 1)).rotation_difference(d).to_matrix().to_4x4()
        fn = (lambda f: abs(f.normal.z) < 0.9) if (smooth and segs > 4) else None
        return self._fin(v, mat, M, smooth_fn=fn)

    def sphere(self, mat, c, r, segs=6, rings=4, scale=(1, 1, 1), rot=(0, 0, 0), smooth=True):
        v = bmesh.ops.create_uvsphere(self.bm, u_segments=segs, v_segments=rings, radius=r)["verts"]
        return self._fin(v, mat, trs(c, rot, scale), smooth=smooth)

    def barrel(self, mat, c, size, segs=8, rot=(0, 0, 0), smooth=True):
        """Rounded barrel (animal torso) with its long axis along Y. size = (W, L, D)."""
        v = bmesh.ops.create_uvsphere(self.bm, u_segments=segs, v_segments=6, radius=1.0)["verts"]
        for q in v:
            t = q.co.z
            if abs(t) > 0.99:
                continue
            k, tt = ((1.0, 0.0) if abs(t) < 0.1 else (0.97 / 0.866, 0.6) if abs(t) < 0.7 else (0.62 / 0.5, 0.92))
            q.co.x *= k
            q.co.y *= k
            q.co.z = math.copysign(tt, t)
        M = trs(c, rot, (size[0] / 2, size[1] / 2, size[2] / 2)) @ AX["y"]
        return self._fin(v, mat, M, smooth=smooth)

    def dome(self, mat, c, r, segs=8, rings=4, scale=(1, 1, 1), rot=(0, 0, 0), smooth=True):
        """Upper half of a sphere (open underneath). `rings` counts the full sphere."""
        v = bmesh.ops.create_uvsphere(self.bm, u_segments=segs, v_segments=rings, radius=r)["verts"]
        low = [q for q in v if q.co.z < -1e-4]
        bmesh.ops.delete(self.bm, geom=low, context="VERTS")
        return self._fin(v, mat, trs(c, rot, scale), smooth=smooth)

    def prism(self, mat, profile, width=None, x=0.0, plane="yz", smooth=False):
        """Extrude a 2D profile. plane 'yz' extrudes along X, 'xz' along Y, 'xy' along Z.
        Profile points are (a, b) or (a, b, width_at_point)."""
        bm = self.bm

        def co(e, a, b):
            return {"yz": (x + e, a, b), "xz": (a, x + e, b), "xy": (a, b, x + e)}[plane]
        L, R = [], []
        for p in profile:
            w = (p[2] if len(p) > 2 else width) / 2.0
            L.append(bm.verts.new(co(-w, p[0], p[1])))
            R.append(bm.verts.new(co(w, p[0], p[1])))
        n = len(profile)
        faces = [bm.faces.new((L[i], L[(i + 1) % n], R[(i + 1) % n], R[i])) for i in range(n)]
        faces.append(bm.faces.new(L))
        faces.append(bm.faces.new(list(reversed(R))))
        bmesh.ops.recalc_face_normals(bm, faces=faces)
        return self._fin(L + R, mat, smooth=smooth)

    def slab(self, mat, p0, p1, width, thick, x=0.0, off=0.0):
        """Slanted board in the YZ plane from p0=(y,z) to p1=(y,z), `width` along X."""
        dy, dz = p1[0] - p0[0], p1[1] - p0[1]
        a = math.atan2(dz, dy)
        c = (x, (p0[0] + p1[0]) / 2 - math.sin(a) * off, (p0[1] + p1[1]) / 2 + math.cos(a) * off)
        return self.box(mat, c, (width, math.hypot(dy, dz), thick), rot=(math.degrees(a), 0, 0))

    def xslab(self, mat, p0, p1, depth, thick, y=0.0):
        """Slanted board in the XZ plane from p0=(x,z) to p1=(x,z), `depth` along Y."""
        dx, dz = p1[0] - p0[0], p1[1] - p0[1]
        a = math.atan2(dz, dx)
        c = ((p0[0] + p1[0]) / 2, y, (p0[1] + p1[1]) / 2)
        return self.box(mat, c, (math.hypot(dx, dz), depth, thick), rot=(0, -math.degrees(a), 0))

    def blade(self, mat, p0, p1, w0, w1, thick=0.02, up=(0, 0, 1)):
        """Flat tapered board from p0 to p1 (palm fronds, leaves, straps)."""
        bm = self.bm
        p0, p1 = Vector(p0), Vector(p1)
        d = p1 - p0
        side = d.cross(Vector(up))
        if side.length < 1e-6:
            side = Vector((1, 0, 0))
        side.normalize()
        n = side.cross(d).normalized()
        vs = []
        for p, w in ((p0, w0), (p1, w1)):
            for sx, sn in ((-1, -1), (1, -1), (1, 1), (-1, 1)):
                vs.append(bm.verts.new(p + side * (sx * w / 2) + n * (sn * thick / 2)))
        faces = [bm.faces.new(vs[0:4]), bm.faces.new(vs[4:8])]
        for i in range(4):
            j = (i + 1) % 4
            faces.append(bm.faces.new((vs[i], vs[j], vs[4 + j], vs[4 + i])))
        bmesh.ops.recalc_face_normals(bm, faces=faces)
        return self._fin(vs, mat)

    def uvquad(self, mat, bl, br, tr, tl):
        """Single quad with 0..1 UVs (for faces the game may texture at runtime)."""
        bm = self.bm
        if self.uv is None:
            self.uv = bm.loops.layers.uv.new("UVMap")
        vs = [bm.verts.new(p) for p in (bl, br, tr, tl)]
        f = bm.faces.new(vs)
        for l, uv in zip(f.loops, ((0, 0), (1, 0), (1, 1), (0, 1))):
            l[self.uv].uv = uv
        f.material_index = self._mi(mat)
        return vs

    def tris(self):
        return sum(len(f.verts) - 2 for f in self.bm.faces)


class Asset:
    def __init__(self, name, family, budget=None, center=False):
        self.name, self.family = name, family
        self.budget = budget or BUDGET[family]
        self.center = center
        self.parts = []
        self.root = None
        self.stats = {}

    def part(self, name="body", pivot=(0, 0, 0), parent=None):
        p = Part(name, pivot, parent)
        self.parts.append(p)
        return p

    def get(self, name):
        return next(p for p in self.parts if p.name == name)

    def scale_all(self, s):
        for p in self.parts:
            bmesh.ops.scale(p.bm, vec=(s, s, s), verts=p.bm.verts)
            p.pivot *= s

    # ---- wheels -------------------------------------------------------------
    def wheel(self, name, pos, r, w, hub="hub_grey", segs=10):
        P = self.part(name, pivot=pos)
        P.cyl("rubber", pos, r, w, axis="x", segs=segs)
        P.cyl(hub, pos, r * 0.55, w + 0.02, axis="x", segs=8)
        return P

    def spoked_wheel(self, name, pos, r, w=0.05, rim="wood_dark", spoke="wood", n=4, segs=12):
        P = self.part(name, pivot=pos)
        P.cyl(rim, pos, r, w, axis="x", segs=segs, caps=False)
        P.cyl(rim, pos, r * 0.9, w, axis="x", segs=segs, caps=False)
        for i in range(n):
            P.box(spoke, pos, (w * 0.5, r * 1.84, r * 0.07), rot=(180.0 * i / n, 0, 0))
        P.cyl("iron_dark", pos, r * 0.14, w * 2.2, axis="x", segs=6)
        return P

    # ---- realise in Blender ---------------------------------------------------
    def realise(self, coll):
        allv = [v.co for p in self.parts for v in p.bm.verts]
        mn = Vector((min(v.x for v in allv), min(v.y for v in allv), min(v.z for v in allv)))
        mx = Vector((max(v.x for v in allv), max(v.y for v in allv), max(v.z for v in allv)))
        off = Vector((0, 0, -mn.z))
        if self.center:
            off.x, off.y = -(mn.x + mx.x) / 2, -(mn.y + mx.y) / 2
        for p in self.parts:
            bmesh.ops.translate(p.bm, vec=off, verts=p.bm.verts)
            # parts authored without a pivot (e.g. "body") keep their origin on the root
            p.pivot = p.pivot + off if p.pivot.length > 1e-9 else Vector((0, 0, 0))
        mn, mx = mn + off, mx + off
        root = bpy.data.objects.new(self.name, None)
        if root.name != self.name:
            bpy.data.objects.remove(root)
            raise RuntimeError("an object named %r already exists outside %s" % (self.name, SCENE_NAME))
        root.empty_display_size = 0.3
        coll.objects.link(root)
        self.root = root
        objs = {}
        tris = 0
        for p in self.parts:
            tris += p.tris()
            bmesh.ops.translate(p.bm, vec=-p.pivot, verts=p.bm.verts)
            me = bpy.data.meshes.new("bb_%s_%s" % (self.name, p.name))
            p.bm.to_mesh(me)
            p.bm.free()
            for s in p.slots:
                me.materials.append(get_mat(s))
            ob = bpy.data.objects.new("%s.%s" % (self.name, p.name), me)
            ob["bb_part"] = p.name
            coll.objects.link(ob)
            objs[p.name] = ob
        for p in self.parts:
            ob = objs[p.name]
            if p.parent:
                ob.parent = objs[p.parent]
                ob.location = p.pivot - self.get(p.parent).pivot
            else:
                ob.parent = root
                ob.location = p.pivot
        self.objs = objs
        self.stats = dict(name=self.name, family=self.family, tris=tris, budget=self.budget,
                          size=tuple(round(a, 3) for a in (mx - mn)),
                          min=tuple(round(a, 3) for a in mn), max=tuple(round(a, 3) for a in mx),
                          parts=[p.name for p in self.parts])
        root["bb_family"] = self.family
        root["bb_size"] = list(mx - mn)
        root["bb_min"] = list(mn)
        root["bb_max"] = list(mx)
        return root


# ---------------------------------------------------------------------------
# registry
REGISTRY = []   # (name, family, kwargs, fn)


def asset(name, family, **kw):
    def deco(fn):
        REGISTRY[:] = [r for r in REGISTRY if r[0] != name]
        REGISTRY.append((name, family, kw, fn))
        return fn
    return deco


# ---------------------------------------------------------------------------
# scene / export
def ensure_scene():
    sc = bpy.data.scenes.get(SCENE_NAME) or bpy.data.scenes.new(SCENE_NAME)
    win = bpy.context.window
    if win is not None and win.scene != sc:
        win.scene = sc
    return sc


def remove_asset(sc, name):
    root = sc.objects.get(name)
    if root is None:
        return
    for ob in list(root.children_recursive) + [root]:
        me = ob.data
        bpy.data.objects.remove(ob)
        if me is not None and me.users == 0:
            bpy.data.meshes.remove(me)


def _override(sc):
    """Run operators against `sc`. When the window already shows the scene nothing is
    overridden (overriding the view layer of a live window can crash Blender)."""
    win = bpy.context.window
    if win is not None and win.scene == sc:
        return bpy.context.temp_override()
    kw = dict(scene=sc, view_layer=sc.view_layers[0])
    if win is not None:
        kw["window"] = win
    return bpy.context.temp_override(**kw)


def export_asset(sc, a):
    os.makedirs(MODELS_DIR, exist_ok=True)
    path = os.path.join(MODELS_DIR, a.name + ".glb")
    renamed, displaced = [], []
    for ob in a.root.children_recursive:
        final = ob["bb_part"]
        other = bpy.data.objects.get(final)
        if other is not None and other is not ob:
            displaced.append((other, other.name))
            other.name = "__bb_tmp_" + final
        renamed.append((ob, ob.name))
        ob.name = final
    try:
        with _override(sc):
            for ob in sc.objects:
                ob.select_set(False)
            a.root.select_set(True)
            for ob in a.root.children_recursive:
                ob.select_set(True)
            want = dict(filepath=path, export_format="GLB", use_selection=True, use_active_scene=True, export_apply=True,
                        export_yup=True, export_cameras=False, export_lights=False,
                        export_animations=False, export_skins=False, export_morph=False,
                        export_extras=False, export_materials="EXPORT", export_texcoords=True,
                        export_normals=True)
            props = bpy.ops.export_scene.gltf.get_rna_type().properties.keys()
            bpy.ops.export_scene.gltf(**{k: v for k, v in want.items() if k in props})
            for ob in sc.objects:
                ob.select_set(False)
    finally:
        for ob, n in renamed:
            ob.name = n
        for ob, n in displaced:
            ob.name = n
    a.stats["bytes"] = os.path.getsize(path)
    return path


def build(names=None, families=None, export=True, verbose=True):
    """Build (and export) the registered assets; returns the stats list."""
    sc = ensure_scene()
    coll = sc.collection
    out = []
    for name, family, kw, fn in REGISTRY:
        if names and name not in names:
            continue
        if families and family not in families:
            continue
        remove_asset(sc, name)
        a = Asset(name, family, **kw)
        fn(a)
        a.realise(coll)
        sc.view_layers[0].update()
        if export:
            export_asset(sc, a)
        out.append(a.stats)
    layout(sc)
    if verbose:
        print_table(out)
    return out


def print_table(stats):
    print("%-20s %-9s %5s/%-5s %-22s %7s  %s" % ("name", "family", "tris", "max", "size x,y,z (m)", "KB", "flags"))
    for s in stats:
        flags = []
        if s["tris"] > s["budget"]:
            flags.append("OVER-BUDGET")
        if abs(s["min"][2]) > 1e-4:
            flags.append("NOT-GROUNDED")
        cx, cy = (s["min"][0] + s["max"][0]) / 2, (s["min"][1] + s["max"][1]) / 2
        if abs(cx) > 0.08 * max(1, s["size"][0]) or abs(cy) > 0.08 * max(1, s["size"][1]):
            flags.append("offcentre(%.2f,%.2f)" % (cx, cy))
        kb = s.get("bytes", 0) / 1024.0
        if kb > 150:
            flags.append("BIG")
        print("%-20s %-9s %5d/%-5d %-22s %7.1f  %s" % (
            s["name"], s["family"], s["tris"], s["budget"],
            "%.2f x %.2f x %.2f" % s["size"], kb, " ".join(flags)))


ROW_WIDTH = {"vehicle": 12.0, "character": 6.5, "animal": 6.0, "tree": 60.0, "prop": 18.0,
             "building": 30.0, "landmark": 60.0}


def layout(sc):
    """Tidy grid: one block per family (rows wrap at ROW_WIDTH), blocks stacked along +Y."""
    y = 0.0
    for fam in FAMILY_ORDER:
        roots = [sc.objects.get(n) for n, f, _, _ in REGISTRY if f == fam]
        roots = [r for r in roots if r is not None]
        if not roots:
            continue
        gap = 0.5 if fam in ("character", "animal") else 1.2
        rows, row, x = [], [], 0.0
        for r in roots:
            w = r["bb_size"][0]
            if row and x + w > ROW_WIDTH[fam]:
                rows.append(row)
                row, x = [], 0.0
            row.append((r, x))
            x += w + gap
        rows.append(row)
        for row in rows:                      # first row nearest the camera (-Y)
            depth = max(r["bb_size"][1] for r, _ in row)
            y += depth / 2
            for r, x in row:
                cx = (r["bb_min"][0] + r["bb_max"][0]) / 2
                cy = (r["bb_min"][1] + r["bb_max"][1]) / 2
                r.location = (x + r["bb_size"][0] / 2 - cx, y - cy, 0)
            y += depth / 2 + gap
        y += 4.0


# ---------------------------------------------------------------------------
# preview renders
def render_preview(sc, families, filename, res=(1280, 720), view=(0.55, -1.0, 0.5), names=None):
    os.makedirs(IMAGES_DIR, exist_ok=True)
    roots = [o for o in sc.objects if o.parent is None and o.get("bb_family")]
    show = [r for r in roots if r["bb_family"] in families and (not names or r.name in names)]
    if not show:
        return None
    hidden = []
    for r in roots:
        vis = r in show
        for o in [r] + list(r.children_recursive):
            hidden.append((o, o.hide_render))
            o.hide_render = not vis
    mn = Vector((1e9, 1e9, 1e9))
    mx = -mn
    for r in show:
        for i in range(3):
            mn[i] = min(mn[i], r.location[i] + r["bb_min"][i])
            mx[i] = max(mx[i], r.location[i] + r["bb_max"][i])
    cam_d = bpy.data.cameras.new("bb_preview_cam")
    cam = bpy.data.objects.new("bb_preview_cam", cam_d)
    sc.collection.objects.link(cam)
    d = Vector(view).normalized()
    cam.rotation_euler = d.to_track_quat("Z", "Y").to_euler()
    cam_d.type = "ORTHO"
    right = Vector((0, 0, 1)).cross(d).normalized() * -1
    up = d.cross(right).normalized() * -1
    corners = [Vector((x, y, z)) for x in (mn.x, mx.x) for y in (mn.y, mx.y) for z in (mn.z, mx.z)]
    us = [c.dot(right) for c in corners]
    vs = [c.dot(up) for c in corners]
    cu, cv = (min(us) + max(us)) / 2, (min(vs) + max(vs)) / 2
    su, sv = (max(us) - min(us)) * 1.06, (max(vs) - min(vs)) * 1.08
    cam_d.ortho_scale = max(su, sv * res[0] / res[1])
    centre = right * cu + up * cv
    dist = (mx - mn).length + 50
    cam.location = centre + d * (dist + max(c.dot(d) for c in corners))
    cam_d.clip_start, cam_d.clip_end = 0.1, dist * 3 + 500
    old = (sc.camera, sc.render.engine, sc.render.resolution_x, sc.render.resolution_y,
           sc.render.resolution_percentage, sc.render.filepath)
    sc.camera = cam
    try:
        sc.render.engine = "BLENDER_WORKBENCH"
    except TypeError:
        pass
    sh = sc.display.shading
    sh.light = "STUDIO"
    sh.color_type = "MATERIAL"
    sh.show_cavity = True
    sh.show_shadows = True
    sh.show_object_outline = True
    sc.render.resolution_x, sc.render.resolution_y = res
    sc.render.resolution_percentage = 100
    sc.render.image_settings.file_format = "PNG"
    path = os.path.join(IMAGES_DIR, filename)
    sc.render.filepath = path
    if sc.world is None:
        sc.world = bpy.data.worlds.get("bb_world") or bpy.data.worlds.new("bb_world")
    try:
        sc.world.color = (0.62, 0.72, 0.78)
    except Exception:
        pass
    with _override(sc):
        bpy.ops.render.render(write_still=True)
    sc.camera = old[0]
    sc.render.filepath = old[5]
    bpy.data.objects.remove(cam)
    bpy.data.cameras.remove(cam_d)
    for o, h in hidden:
        o.hide_render = h
    return path


# ---------------------------------------------------------------------------
# shared creature builder (animals + the delman horse)
def quadruped(A, c, oy=0.0, head_in_body=False, prefix=""):
    """Generic four-legged animal facing -Y. Returns anchors for species details.
    c keys: L body length, W width, D depth, Hs back height, leg (top radius),
            col, leg_col, hoof, neck (length), neck_ang (deg above horizontal), neck_r,
            head (w, h, l), pitch (deg nose-down), snout (taper), tail (len, ang, r) or None"""
    L, W, D, Hs = c["L"], c["W"], c["D"], c["Hs"]
    col = c["col"]
    B = A.get("body") if any(p.name == "body" for p in A.parts) else A.part("body")
    zc = Hs - D / 2
    B.barrel(col, (0, oy, zc), (W, L, D))
    # legs
    lr = c["leg"]
    legs = {}
    for nm, sx, sy in (("fl", 1, -1), ("fr", -1, -1), ("rl", 1, 1), ("rr", -1, 1)):
        x = sx * (W / 2 - lr * 1.1)
        y = oy + sy * L * c.get("leg_y", 0.3)
        top = (x, y, Hs - D * 0.55)
        P = A.part(prefix + "leg_" + nm, pivot=top)
        hoof_h = c.get("hoof_h", lr * 1.2)
        P.limb(c.get("leg_col", col), top, (x, y, hoof_h), lr, lr * 0.6, segs=5)
        P.limb(c.get("hoof", "hoof"), (x, y, hoof_h), (x, y - lr * 0.2, 0), lr * 0.65, lr * 0.8, segs=5)
        legs[nm] = P
    # neck + head
    a = math.radians(c["neck_ang"])
    n0 = Vector((0, oy - L * 0.36, Hs - D * 0.35))
    n1 = n0 + Vector((0, -math.cos(a), math.sin(a))) * c["neck"]
    nr = c.get("neck_r", D * 0.28)
    B.limb(col, n0 + Vector((0, 0.3 * math.cos(a), -0.3 * math.sin(a))) * nr, n1, nr, nr * 0.75, segs=6)
    H = B if head_in_body else A.part("head", pivot=tuple(n1))
    hw, hh, hl = c["head"]
    p = math.radians(c.get("pitch", 20))
    fwd = Vector((0, -math.cos(p), -math.sin(p)))
    upv = Vector((0, -math.sin(p), math.cos(p)))
    hc = n1 + fwd * hl * 0.3 + upv * hh * 0.1
    H.tbox(c.get("head_col", col), tuple(hc), (hw, hh, hl), top=c.get("snout", (0.6, 0.6)),
           rot=(90 + math.degrees(p), 0, 0))
    tip = hc + fwd * hl * 0.5
    sn = c.get("snout", (0.6, 0.6))
    H.box(c.get("nose", "nose"), tuple(tip + upv * hh * sn[1] * 0.2), (hw * sn[0] * 0.4, hl * 0.05, hh * sn[1] * 0.3),
          rot=(math.degrees(p), 0, 0))
    eye = c.get("eye", hw * 0.12)
    for sx in (-1, 1):
        H.box("eye", tuple(hc + Vector((sx * hw * 0.42, 0, 0)) + upv * hh * 0.18 + fwd * hl * 0.02),
              (eye * 0.6, eye, eye))
    # tail
    T = None
    rump = Vector((0, oy + L * 0.46, Hs - D * 0.3))
    if c.get("tail"):
        tl, ta, tr = c["tail"]
        ta = math.radians(ta)
        T = B if (head_in_body or c.get("tail_in_body")) else A.part("tail", pivot=tuple(rump))
        T.limb(c.get("tail_col", col), tuple(rump), tuple(rump + Vector((0, math.cos(ta), math.sin(ta))) * tl),
               tr, tr * 0.5, segs=5)
    return dict(body=B, head=H, legs=legs, tail=T, neck_top=n1, head_c=hc, fwd=fwd, up=upv,
                rump=rump, zc=zc, hw=hw, hh=hh, hl=hl)
