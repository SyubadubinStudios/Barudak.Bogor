"""Barudak Bogor - props, trees, buildings and landmarks. Front faces -Y."""
import math
from mathutils import Vector
from common import asset


def V(*a):
    return Vector(a)


def ring(n, r, z=0.0, a0=0.0):
    return [V(r * math.cos(a0 + 2 * math.pi * i / n), r * math.sin(a0 + 2 * math.pi * i / n), z) for i in range(n)]


# =============================================================================
# landmarks
@asset("tugu_kujang", "landmark")
def tugu_kujang(A):
    B = A.part("body")
    B.cyl("concrete", (0, 0, 0.3), 7.0, 0.6, segs=16)
    B.cyl("concrete", (0, 0, 0.9), 5.5, 0.6, segs=16)
    B.cyl("concrete_white", (0, 0, 1.5), 4.0, 0.6, segs=16)
    rz = -30                                              # one flat face towards the front
    B.cyl("stone", (0, 0, 2.4), 3.3, 1.2, segs=3, rot=(0, 0, rz), smooth=False)
    B.cyl("concrete_white", (0, 0, 11.0), 2.5, 16.0, r2=0.95, segs=3, rot=(0, 0, rz), smooth=False)
    B.cyl("stone", (0, 0, 19.2), 1.25, 0.4, segs=3, rot=(0, 0, rz), smooth=False)
    B.cyl("gold", (0, 0, 19.55), 0.45, 0.3, segs=6)
    # emblem plates on the three faces
    for k in range(3):
        a = math.radians(-90 + 120 * k)
        B.box("gold", (1.05 * math.cos(a), 1.05 * math.sin(a), 6.2), (1.0, 0.12, 1.3),
              rot=(-5.5, 0, math.degrees(a) + 90))
    # the kujang blade (stylised), seen broadside from the front
    z0, k = 19.6, 1.0
    prof = [(-0.16, 0), (0.16, 0), (0.22, 0.9), (0.85, 1.7), (1.2, 2.7), (1.05, 3.7), (0.55, 4.6),
            (-0.15, 5.4), (0.0, 4.5), (-0.3, 3.9), (-0.12, 3.5), (-0.5, 2.9), (-0.3, 2.45),
            (-0.65, 1.75), (-0.38, 1.3), (-0.22, 0.9)]
    B.prism("gold", [(x * k, z0 + z * k) for x, z in prof], 0.22, plane="xz")


@asset("gerbang_kebun_raya", "landmark")
def gerbang_kebun_raya(A):
    B = A.part("body")
    px = 2.65
    for sx in (-1, 1):
        x = sx * px
        B.box("stone", (x, 0, 0.25), (1.4, 1.4, 0.5))
        B.box("concrete_white", (x, 0, 2.3), (1.1, 1.1, 3.6))
        B.box("concrete_white", (x, 0, 4.2), (1.4, 1.4, 0.25))
        B.box("concrete_white", (x, 0, 4.42), (1.15, 1.15, 0.2))
        B.box("stone", (x, -0.56, 2.6), (0.6, 0.04, 1.6))                    # recessed panel
        # statue-like seated figure
        B.tbox("stone", (x, 0, 4.95), (0.85, 0.75, 0.85), top=(0.65, 0.7))
        B.box("stone", (x, -0.3, 4.68), (0.9, 0.35, 0.3))
        B.tbox("stone", (x, -0.02, 5.6), (0.42, 0.42, 0.45), top=(0.8, 0.8))
        B.cyl("stone", (x, 0, 5.92), 0.2, 0.22, r2=0.05, segs=6)
        # wing wall
        w = sx * 3.6
        B.box("concrete_white", (w, 0, 1.1), (0.85, 0.5, 2.2))
        B.box("stone", (w, 0, 2.25), (0.95, 0.6, 0.12))
        B.sphere("emit_lamp", (sx * 3.75, 0, 2.5), 0.18, segs=6, rings=4)
    # iron gate leaves (hinged at the pillars)
    half = px - 0.55
    for nm, sx in (("gate_l", 1), ("gate_r", -1)):
        hinge = (sx * half, 0, 0)
        G = A.part(nm, pivot=hinge)
        n = 8
        for i in range(n):
            t = (i + 0.5) / n                                # 0 at centre .. 1 at hinge
            x = sx * half * t
            h = 3.3 - 0.9 * t * t
            G.box("iron_dark", (x, 0, h / 2 + 0.1), (0.05, 0.05, h))
            G.cyl("gold", (x, 0, h + 0.2), 0.05, 0.22, r2=0.0, segs=4)
        for z, hh in ((0.25, 0.1), (1.3, 0.06), (2.3, 0.06)):
            G.box("iron_dark", (sx * half / 2, 0, z), (half, 0.07, hh))
        G.box("iron_dark", (sx * half / 2, 0, 0.75), (half - 0.1, 0.03, 0.9))


# =============================================================================
# trees
@asset("pohon_beringin", "tree")
def pohon_beringin(A):
    B = A.part("body")
    B.limb("bark", (0, 0, 0), (0, 0, 5.6), 1.15, 0.85, segs=8)
    for i, p in enumerate(ring(4, 2.6, 0.0, 0.3)):
        B.limb("bark", (p.x * 0.2, p.y * 0.2, 2.0), tuple(p), 0.5, 0.16, segs=4)
    for i, p in enumerate(ring(3, 3.4, 7.6, 0.9)):
        B.limb("bark", (0, 0, 5.2), tuple(p), 0.5, 0.25, segs=4)
    for i, p in enumerate(ring(5, 4.3, 0.0, 0.1)):
        q = V(p.x * (0.8 + 0.25 * (i % 2)), p.y * (0.8 + 0.25 * (i % 2)), 0)
        B.limb("bark_light", (q.x, q.y, 6.8), (q.x * 1.02, q.y * 1.02, 0), 0.07, 0.05, segs=4)
    B.sphere("leaf", (0, 0, 10.2), 4.8, segs=7, rings=4, scale=(1.15, 1.15, 0.78), smooth=False)
    for i, p in enumerate(ring(4, 4.3, 8.4, 0.5)):
        B.sphere("leaf_dark" if i % 2 else "leaf", tuple(p), 3.3, segs=6, rings=4,
                 scale=(1, 1, 0.72), rot=(0, 0, 25 * i), smooth=False)
    B.sphere("leaf_light", (0.8, -0.6, 12.4), 2.4, segs=6, rings=4, scale=(1.2, 1.2, 0.66), smooth=False)


@asset("pohon_kelapa", "tree")
def pohon_kelapa(A):
    B = A.part("body")
    pts = [V(0, 0, 0), V(0.25, 0, 2.8), V(0.6, 0, 5.6), V(0.8, 0, 8.0)]
    rr = [0.24, 0.19, 0.16, 0.13]
    for i in range(3):
        B.limb("bark_light", tuple(pts[i]), tuple(pts[i + 1]), rr[i], rr[i + 1], segs=6)
    T = pts[-1]
    B.sphere("leaf_dark", tuple(T), 0.3, segs=5, rings=3, smooth=False)
    for i in range(8):
        a = 2 * math.pi * i / 8 + 0.2
        d = V(math.cos(a), math.sin(a), 0)
        lift = 0.9 if i % 2 else 0.45
        m = T + d * 1.7 + V(0, 0, lift)
        e = T + d * 3.2 + V(0, 0, lift - 1.3)
        B.blade("leaf", tuple(T), tuple(m), 0.35, 0.8, 0.04)
        B.blade("leaf", tuple(m), tuple(e), 0.8, 0.1, 0.04)
    for i in range(3):
        a = 2 * math.pi * i / 3
        B.sphere("coconut", (T.x + 0.26 * math.cos(a), T.y + 0.26 * math.sin(a), T.z - 0.28), 0.17, segs=5, rings=3)


@asset("pohon_pinus", "tree")
def pohon_pinus(A):
    B = A.part("body")
    B.limb("bark", (0, 0, 0), (0, 0, 11.0), 0.24, 0.08, segs=5)
    for i, (z, r, h) in enumerate(((3.0, 2.3, 3.2), (5.2, 1.9, 2.9), (7.2, 1.45, 2.7), (9.1, 1.0, 2.9))):
        B.cyl("pine_green" if i % 2 == 0 else "leaf_dark", (0, 0, z + h / 2), r, h, r2=0.0, segs=7,
              rot=(0, 0, 25 * i), smooth=False)


@asset("pohon_kecil", "tree")
def pohon_kecil(A):
    B = A.part("body")
    B.limb("bark", (0, 0, 0), (0, 0, 2.6), 0.14, 0.09, segs=5)
    B.limb("bark", (0, 0, 2.2), (0.6, 0.15, 3.0), 0.07, 0.04, segs=4)
    B.limb("bark", (0, 0, 2.3), (-0.5, -0.2, 3.1), 0.07, 0.04, segs=4)
    B.sphere("leaf", (0, 0, 3.8), 1.45, segs=6, rings=4, scale=(1, 1, 0.82), smooth=False)
    B.sphere("leaf_light", (0.8, 0.2, 3.3), 0.95, segs=6, rings=4, scale=(1, 1, 0.8), rot=(0, 0, 30), smooth=False)
    B.sphere("leaf_dark", (-0.7, -0.3, 3.35), 1.05, segs=6, rings=4, scale=(1, 1, 0.8), rot=(0, 0, 50), smooth=False)


@asset("semak_teh", "tree")
def semak_teh(A):
    B = A.part("body")
    B.dome("tea_green", (0, 0, 0), 1.0, segs=8, rings=6, scale=(0.75, 0.62, 0.72), smooth=False)
    B.dome("leaf_light", (0.05, 0, 0.42), 1.0, segs=6, rings=4, scale=(0.5, 0.4, 0.38), rot=(0, 0, 20), smooth=False)


@asset("bambu", "tree")
def bambu(A):
    B = A.part("body")
    for i in range(8):
        a = 2 * math.pi * i / 8 + 0.37 * i
        r0 = 0.15 + 0.12 * (i % 3)
        h = 5.4 + 0.45 * ((i * 5) % 4)
        lean = 0.9 + 0.3 * (i % 2)
        b = V(r0 * math.cos(a), r0 * math.sin(a), 0)
        t = V((r0 + lean) * math.cos(a), (r0 + lean) * math.sin(a), h)
        B.limb("bamboo", tuple(b), tuple(t), 0.055, 0.025, segs=5)
        d = V(math.cos(a), math.sin(a), 0)
        s = V(-math.sin(a), math.cos(a), 0)
        B.blade("leaf_light" if i % 2 else "leaf", tuple(t - V(0, 0, 0.4)), tuple(t + d * 1.0 + s * 0.5 - V(0, 0, 0.5)), 0.9, 0.1, 0.03)
        m = b + (t - b) * 0.68
        B.blade("leaf", tuple(m), tuple(m + d * 0.7 - s * 0.6 + V(0, 0, 0.25)), 0.7, 0.08, 0.03)


# =============================================================================
# small props
@asset("lampu_jalan", "prop")
def lampu_jalan(A):
    B = A.part("body")
    B.box("concrete", (0, 0, 0.15), (0.45, 0.45, 0.3))
    B.limb("steel", (0, 0, 0.3), (0, 0, 6.5), 0.1, 0.06, segs=6)
    B.limb("steel", (0, 0, 6.45), (0, -0.9, 6.95), 0.055, 0.045, segs=5)
    B.limb("steel", (0, -0.9, 6.95), (0, -1.75, 7.0), 0.045, 0.04, segs=5)
    B.tbox("iron_dark", (0, -2.0, 6.98), (0.32, 0.8, 0.14), top=(0.7, 0.85))
    B.box("emit_lamp", (0, -2.02, 6.895), (0.24, 0.6, 0.04))


@asset("rambu_angkot", "prop")
def rambu_angkot(A):
    B = A.part("body")
    B.cyl("concrete", (0, 0, 0.05), 0.16, 0.1, segs=8)
    B.limb("steel", (0, 0, 0.1), (0, 0, 2.6), 0.03, 0.03, segs=5)
    B.box("sign_blue", (0, -0.04, 2.25), (0.6, 0.03, 0.6))
    B.box("white", (0, -0.058, 2.25), (0.42, 0.01, 0.36))
    B.box("angkot_green", (0, -0.066, 2.27), (0.3, 0.01, 0.16))             # little angkot pictogram
    B.box("glass", (0.02, -0.07, 2.3), (0.2, 0.01, 0.06))
    for x in (-0.09, 0.09):
        B.box("black", (x, -0.07, 2.18), (0.06, 0.01, 0.06))
    B.box("white", (0, -0.04, 1.8), (0.5, 0.02, 0.18))                      # blank route plate
    B.box("sign_blue", (0, -0.052, 1.8), (0.44, 0.005, 0.03))


@asset("tas_misterius", "prop")
def tas_misterius(A):
    B = A.part("body")
    z = 0.15
    B.cyl("bag_olive", (0, 0, z), 0.17, 0.6, axis="x", segs=8, scale=(1, 1, 0.9))
    for x in (-0.3, 0.3):
        B.cyl("bag_strap", (x, 0, z), 0.13, 0.02, axis="x", segs=8, scale=(1, 1, 0.9))
    for x in (-0.14, 0.14):
        B.cyl("bag_strap", (x, 0, z), 0.176, 0.04, axis="x", segs=8, scale=(1, 1, 0.9))
        for sy in (-1, 1):
            B.limb("bag_strap", (x, sy * 0.1, z + 0.12), (x * 0.5, sy * 0.04, z + 0.24), 0.012, segs=4)
    for sy in (-1, 1):
        B.limb("bag_strap", (-0.07, sy * 0.04, z + 0.24), (0.07, sy * 0.04, z + 0.24), 0.012, segs=4)
    B.box("brass", (0, 0, z + 0.153), (0.46, 0.015, 0.01))                  # zipper
    B.box("brass", (0.24, 0, z + 0.16), (0.03, 0.02, 0.02))
    B.box("sign_red", (0.1, -0.165, z + 0.02), (0.1, 0.01, 0.06))           # mystery tag


# =============================================================================
# buildings / street furniture
@asset("halte", "building", center=True)
def halte(A):
    B = A.part("body")
    B.box("concrete", (0, 0, 0.1), (4.2, 1.9, 0.2))
    for sx in (-1, 1):
        B.limb("steel", (sx * 1.9, 0.75, 0.2), (sx * 1.9, 0.75, 2.45), 0.05, segs=4)
        B.limb("steel", (sx * 1.9, -0.7, 0.2), (sx * 1.9, -0.7, 2.68), 0.05, segs=4)
        B.box("glass", (sx * 1.9, 0.02, 1.5), (0.03, 1.35, 1.3))
    B.slab("zinc", (-1.15, 2.76), (1.05, 2.42), 4.5, 0.07)
    B.box("sign_blue", (0, -1.16, 2.72), (4.5, 0.05, 0.3))
    B.box("white", (0, -1.19, 2.72), (2.0, 0.01, 0.16))
    B.box("glass", (0, 0.76, 1.6), (3.75, 0.03, 1.2))
    B.box("sign_blue", (0, 0.76, 0.75), (3.75, 0.04, 0.5))
    B.box("wood", (0, 0.4, 0.65), (3.1, 0.42, 0.06))
    for x in (-1.3, 0, 1.3):
        B.box("steel", (x, 0.4, 0.41), (0.06, 0.36, 0.42))
    B.box("billboard_face", (1.2, 0.74, 1.6), (1.0, 0.02, 0.9))             # route map panel
    B.cyl("cart_green", (-2.3, -0.5, 0.45), 0.2, 0.7, r2=0.24, segs=6)        # bin


@asset("warung", "building", center=True)
def warung(A):
    B = A.part("body")
    B.box("concrete", (0, 0, 0.06), (3.1, 2.6, 0.12))
    B.box("wall_green", (0, 1.21, 1.3), (3.0, 0.08, 2.36))
    for sx in (-1, 1):
        B.box("wall_green", (sx * 1.46, 0, 1.3), (0.08, 2.5, 2.36))
        B.limb("wood", (sx * 1.46, -1.22, 0.12), (sx * 1.46, -1.22, 2.42), 0.06, segs=4)
        B.limb("wood", (sx * 1.4, -2.35, 0), (sx * 1.4, -2.35, 1.98), 0.035, segs=4)
    B.box("wood", (-0.45, -1.12, 0.6), (2.0, 0.34, 0.96))                    # counter
    B.box("wood_light", (-0.45, -1.14, 1.1), (2.1, 0.44, 0.05))
    B.box("wood", (0, -1.22, 2.36), (3.0, 0.1, 0.14))
    B.slab("zinc", (-1.45, 2.42), (1.4, 2.86), 3.4, 0.06)
    B.slab("tarp_orange", (-1.4, 2.34), (-2.45, 1.98), 3.1, 0.03)
    # shelves + goods
    cols = ("sign_red", "cart_yellow", "blue", "veg_green", "orange", "white", "veg_purple")
    for j, z in enumerate((1.25, 1.75)):
        B.box("wood_light", (0, 1.05, z), (2.7, 0.28, 0.04))
        for i in range(7):
            B.box(cols[(i + j * 3) % 7], (-1.1 + i * 0.37, 1.05, z + 0.14), (0.24, 0.18, 0.24))
    for i in range(6):                                                       # hanging sachets
        B.box(cols[(i * 2) % 7], (-1.2 + i * 0.3, -1.25, 2.02), (0.14, 0.02, 0.5))
    for i in range(3):                                                       # jars
        B.cyl("glass", (-1.1 + i * 0.3, -1.14, 1.24), 0.09, 0.22, segs=6)
        B.cyl("sign_red", (-1.1 + i * 0.3, -1.14, 1.36), 0.095, 0.03, segs=6)
    B.cyl("blue", (1.05, -0.7, 0.36), 0.14, 0.44, segs=8)                     # water gallon
    B.sphere("emit_lamp", (0.3, -0.6, 2.35), 0.07, segs=5, rings=3)
    B.box("wood", (0.1, -1.9, 0.45), (1.9, 0.3, 0.05))                       # bench
    for x in (-0.7, 0.9):
        B.box("wood", (x, -1.9, 0.21), (0.06, 0.28, 0.42))
    B.box("sign_yellow", (0, -2.47, 1.9), (1.6, 0.02, 0.22))


@asset("pos_ronda", "building")
def pos_ronda(A):
    B = A.part("body")
    for sx in (-1, 1):
        for sy in (-1, 1):
            B.limb("wood_dark", (sx * 1.0, sy * 1.0, 0), (sx * 1.0, sy * 1.0, 2.35), 0.07, segs=4)
        B.box("wall_cream", (sx * 1.0, 0, 0.9), (0.06, 2.0, 0.7))
        B.box("red", (sx * 1.01, 0, 1.2), (0.065, 2.0, 0.1))
    B.box("wood", (0, 0, 0.5), (2.2, 2.2, 0.1))
    B.box("wall_cream", (0, 1.0, 0.9), (2.0, 0.06, 0.7))
    B.box("red", (0, 1.01, 1.2), (2.0, 0.065, 0.1))
    B.box("wood", (0, -1.35, 0.25), (1.0, 0.4, 0.08))                        # step
    B.box("wood", (0, -1.35, 0.1), (0.9, 0.3, 0.2))
    B.box("wood_dark", (0, 0, 2.32), (2.3, 2.3, 0.08))
    B.prism("roof_tile", [(-1.55, 2.32), (1.55, 2.32), (0, 3.15)], 2.9, plane="xz")
    B.box("white", (0, -1.47, 2.5), (1.3, 0.03, 0.26))                       # name board
    B.box("red", (0, -1.49, 2.5), (1.1, 0.01, 0.08))
    B.limb("wood_dark", (0.7, -1.15, 2.3), (0.7, -1.15, 2.05), 0.008, segs=4)   # kentongan
    B.cyl("wood_light", (0.7, -1.15, 1.75), 0.075, 0.6, segs=6)
    B.box("black", (0.7, -1.23, 1.75), (0.02, 0.01, 0.4))
    B.sphere("emit_lamp", (0, 0, 2.2), 0.07, segs=5, rings=3)


@asset("villa_puncak", "building", center=True)
def villa_puncak(A):
    B = A.part("body")
    B.box("stone", (0, 0, 0.2), (10.3, 8.3, 0.4))
    B.box("wall_villa", (0, 0, 1.9), (10, 8, 3.0))
    B.box("wall_moss", (0, 0, 0.75), (10.04, 8.04, 0.7))
    B.box("wall_villa", (0, 0.6, 4.9), (9, 6.6, 3.0))
    B.box("wall_moss", (0, 0.6, 3.5), (9.04, 6.64, 0.25))
    B.tbox("roof_dark", (0, 0.6, 7.3), (10.2, 7.8, 1.9), top=(0.42, 0.04))
    B.box("roof_dark", (0, 0.6, 6.42), (10.3, 7.9, 0.14))
    B.box("stone", (3.0, 1.6, 8.0), (0.8, 0.8, 2.4))
    B.box("roof_dark", (3.0, 1.6, 9.24), (1.0, 1.0, 0.12))
    # porch
    B.box("stone", (0, -4.8, 0.15), (6.4, 1.8, 0.3))
    B.box("stone", (0, -5.9, 0.08), (3.0, 0.5, 0.16))
    B.slab("roof_dark", (-4.0, 3.45), (-5.9, 2.85), 6.8, 0.12)
    for x in (-2.9, -1.0, 1.0, 2.9):
        B.limb("concrete_white", (x, -5.5, 0.3), (x, -5.5, 2.9), 0.13, segs=6)
    # terrace railing on top of the ground floor
    B.box("iron_dark", (0, -3.9, 4.25), (9.8, 0.06, 0.06))
    for i in range(9):
        B.box("iron_dark", (-4.8 + i * 1.2, -3.9, 3.85), (0.05, 0.05, 0.85))
    # door + windows (front)
    B.box("door_wood", (0, -4.01, 1.6), (1.3, 0.08, 2.4))
    B.box("brass", (0.45, -4.06, 1.55), (0.06, 0.04, 0.14))
    for x in (-3.4, 3.4):
        B.box("wall_moss", (x, -4.01, 2.0), (1.7, 0.06, 1.8))
        B.box("window_dark", (x, -4.03, 2.0), (1.4, 0.06, 1.5))
        B.box("wall_moss", (x, -4.06, 2.0), (0.06, 0.04, 1.5))
    for i, x in enumerate((-3.0, 0.0, 3.0)):
        B.box("wall_moss", (x, -2.71, 4.9), (1.5, 0.06, 1.8))
        B.box("emit_window_eerie" if i == 2 else "window_dark", (x, -2.73, 4.9), (1.2, 0.06, 1.5))
        B.box("wall_moss", (x, -2.76, 4.9), (0.06, 0.04, 1.5))
    B.box("door_wood", (-3.75, -2.8, 4.8), (0.6, 0.05, 1.5), rot=(0, 14, 0))      # hanging shutter
    B.box("door_wood", (3.72, -2.8, 4.9), (0.6, 0.05, 1.5))
    # side + back windows
    for sx in (-1, 1):
        for y in (-2.0, 1.8):
            B.box("window_dark", (sx * 5.0, y, 2.0), (0.08, 1.3, 1.4))
            B.box("window_dark", (sx * 4.5, y * 0.8 + 0.6, 4.9), (0.08, 1.2, 1.4))
    for x in (-2.5, 2.5):
        B.box("window_dark", (x, 4.0, 2.0), (1.3, 0.08, 1.4))
        B.box("window_dark", (x, 3.9, 4.9), (1.2, 0.08, 1.4))
    # creeping vines
    B.box("leaf_dark", (-5.03, -3.2, 2.4), (0.06, 1.4, 4.0))
    B.box("leaf_dark", (-4.3, -4.03, 2.9), (1.2, 0.06, 1.0))


@asset("saung_pancing", "building", center=True)
def saung_pancing(A):
    B = A.part("body")
    for sx in (-1, 0, 1):
        for sy in (-1, 1):
            B.limb("bamboo_dry", (sx * 1.4, sy * 1.4, 0), (sx * 1.4, sy * 1.4, 2.7 if sx else 1.0), 0.06, segs=5)
    B.box("bamboo_dry", (0, 0, 1.0), (3.0, 3.0, 0.08))
    for i in range(6):
        B.box("bamboo", (-1.25 + i * 0.5, 0, 1.045), (0.04, 3.0, 0.01))
    for z in (1.45, 1.8):
        B.limb("bamboo", (-1.4, 1.4, z), (1.4, 1.4, z), 0.03, segs=4)
        for sx in (-1, 1):
            B.limb("bamboo", (sx * 1.4, -1.4, z), (sx * 1.4, 1.4, z), 0.03, segs=4)
    B.tbox("thatch", (0, 0, 3.2), (3.9, 3.9, 1.2), top=(0.5, 0.04))
    B.box("thatch", (0, 0, 2.64), (4.0, 4.0, 0.1))
    B.box("wood_light", (0.7, 0.9, 1.3), (1.2, 0.5, 0.06))                   # bench
    B.box("wood_light", (0.7, 0.9, 1.15), (1.1, 0.06, 0.26))
    # jetty + ladder to the front
    B.box("bamboo_dry", (0, -2.3, 0.97), (1.0, 1.6, 0.06))
    for sx in (-1, 1):
        B.limb("bamboo_dry", (sx * 0.45, -3.0, 0), (sx * 0.45, -3.0, 1.3), 0.045, segs=5)
    B.limb("bamboo", (1.1, -1.3, 1.05), (1.25, -2.9, 2.5), 0.02, 0.008, segs=4)      # fishing rod
    B.cyl("blue", (-0.9, -1.0, 1.2), 0.16, 0.3, r2=0.2, segs=7)               # bucket


@asset("gapura_gang", "building")
def gapura_gang(A):
    B = A.part("body")
    for sx in (-1, 1):
        x = sx * 1.3
        B.box("concrete", (x, 0, 0.1), (0.52, 0.52, 0.2))
        for i in range(6):
            B.box("red" if i % 2 == 0 else "white", (x, 0, 0.425 + i * 0.45), (0.4, 0.4, 0.45))
        B.box("white", (x, 0, 2.96), (0.52, 0.52, 0.12))
        B.cyl("red", (x, 0, 3.47), 0.3, 0.5, r2=0.0, segs=4, rot=(0, 0, 45), smooth=False)
        B.sphere("gold", (x, 0, 3.74), 0.06, segs=5, rings=3)
    B.box("red", (0, 0, 3.17), (3.1, 0.3, 0.3))
    B.box("white", (0, -0.16, 3.17), (2.1, 0.02, 0.2))
    B.box("white", (0, 0.16, 3.17), (2.1, 0.02, 0.2))
    B.prism("white", [(-1.05, 3.32), (1.05, 3.32), (0.8, 3.6), (0.35, 3.8), (-0.35, 3.8), (-0.8, 3.6)], 0.2, plane="xz")
    B.prism("red", [(-0.7, 3.4), (0.7, 3.4), (0.3, 3.7), (-0.3, 3.7)], 0.24, plane="xz")
    B.limb("steel", (0, 0, 3.8), (0, 0, 4.5), 0.015, segs=4)                  # flag
    B.box("red", (0.18, 0, 4.42), (0.36, 0.01, 0.12))
    B.box("white", (0.18, 0, 4.3), (0.36, 0.01, 0.12))


@asset("papan_reklame", "building")
def papan_reklame(A):
    B = A.part("body")
    for sx in (-1, 1):
        B.box("concrete", (sx * 1.8, 0.2, 0.2), (0.7, 0.7, 0.4))
        B.limb("steel", (sx * 1.8, 0.2, 0.4), (sx * 1.8, 0.2, 8.0), 0.13, 0.11, segs=6)
        B.limb("steel", (sx * 1.8, 0.2, 3.0), (-sx * 1.8, 0.2, 4.6), 0.04, segs=4)
    B.box("iron_dark", (0, 0, 6.5), (6.2, 0.16, 3.2))
    B.uvquad("billboard_face", (-3.0, -0.085, 5.0), (3.0, -0.085, 5.0), (3.0, -0.085, 8.0), (-3.0, -0.085, 8.0))
    B.box("steel", (0, -0.4, 4.86), (6.2, 0.6, 0.05))                        # catwalk
    B.box("steel", (0, -0.68, 5.3), (6.2, 0.03, 0.03))
    for i in range(4):
        x = -2.25 + i * 1.5
        B.limb("steel", (x, 0, 8.1), (x, -0.7, 8.35), 0.02, segs=4)
        B.box("emit_lamp", (x, -0.75, 8.33), (0.3, 0.14, 0.08), rot=(35, 0, 0))


@asset("tenda_pasar", "building")
def tenda_pasar(A):
    B = A.part("body")
    for sx in (-1, 1):
        for sy in (-1, 1):
            B.limb("steel", (sx * 1.4, sy * 1.4, 0), (sx * 1.4, sy * 1.4, 2.1), 0.03, segs=4)
    B.cyl("tarp_blue", (0, 0, 2.5), 2.1, 0.8, r2=0.05, segs=4, rot=(0, 0, 45), smooth=False)
    B.box("tarp_orange", (0, 0, 2.03), (2.98, 2.98, 0.16))
    B.box("wood", (0, 0.3, 0.78), (2.5, 1.3, 0.06))
    for sx in (-1, 1):
        for sy in (-1, 1):
            B.box("wood", (sx * 1.15, 0.3 + sy * 0.55, 0.375), (0.07, 0.07, 0.75))
    produce = (("chili", -0.85), ("veg_green", 0.0), ("veg_orange", 0.85))
    for col, x in produce:
        B.tbox("wood_light", (x, 0.2, 0.93), (0.6, 0.5, 0.24), top=(1.12, 1.12))
        for i in range(6):
            B.sphere(col, (x - 0.18 + 0.18 * (i % 3), 0.08 + 0.22 * (i // 3), 1.07 + 0.02 * (i % 2)), 0.11, segs=5, rings=3)
    B.tbox("wood_light", (-0.7, -0.8, 0.14), (0.6, 0.45, 0.28), top=(1.1, 1.1))      # crates on the ground
    for i in range(5):
        B.sphere("veg_purple", (-0.9 + 0.1 * i, -0.8, 0.31), 0.09, segs=5, rings=3, scale=(1, 2.2, 1))
    B.tbox("wood_light", (0.4, -0.85, 0.14), (0.6, 0.45, 0.28), top=(1.1, 1.1))
    for i in range(4):
        B.blade("yellow", (0.22 + 0.12 * i, -1.02, 0.3), (0.26 + 0.12 * i, -0.68, 0.36), 0.09, 0.05, 0.07)
    B.cyl("white", (1.2, -0.8, 0.3), 0.22, 0.6, r2=0.26, segs=7)              # rice sack
    B.limb("steel", (0.9, 0.3, 2.0), (0.9, 0.3, 1.6), 0.008, segs=4)          # hanging scale
    B.cyl("aluminium", (0.9, 0.3, 1.5), 0.13, 0.05, segs=7)
