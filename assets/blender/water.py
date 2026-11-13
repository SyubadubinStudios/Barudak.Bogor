"""Barudak Bogor - life in the water: fish, turtle, crocodile, duck, and the plants of ponds
and river banks. Animals face -Y, left = +X. The game floats them: the origin of an animal
rides a little under the surface (Population.Draught), the origin of a floating plant is the
surface itself, and reeds stand on the bed."""
import math
from mathutils import Vector
from common import asset


def V(*a):
    return Vector(a)


@asset("ikan", "animal", center=True)
def ikan(A):
    """A big koi, seen from above through the water. The tail fin is `tail`."""
    B = A.part("body")
    B.sphere("cat_orange", (0, 0, 0.09), 0.1, segs=8, rings=5, scale=(0.62, 2.3, 0.8))
    B.sphere("cat_white", (0, -0.1, 0.1), 0.085, segs=6, rings=4, scale=(0.6, 1.2, 0.75))      # pale head
    B.sphere("cat_white", (0.01, 0.07, 0.125), 0.06, segs=6, rings=3, scale=(0.75, 1.2, 0.5))  # saddle patch
    B.sphere("red", (-0.01, -0.02, 0.135), 0.045, segs=6, rings=3, scale=(0.8, 1.3, 0.5))
    B.blade("chicken_orange", (0, -0.04, 0.15), (0, 0.1, 0.19), 0.02, 0.1, thick=0.01, up=(1, 0, 0))   # back fin
    for sx in (-1, 1):
        B.box("eye", (sx * 0.045, -0.17, 0.105), (0.012, 0.016, 0.016))
        B.blade("chicken_orange", (sx * 0.05, -0.08, 0.07), (sx * 0.15, -0.02, 0.05), 0.04, 0.08, thick=0.008)
    piv = V(0, 0.2, 0.09)
    T = A.part("tail", pivot=tuple(piv))
    T.limb("cat_orange", tuple(piv - V(0, 0.03, 0)), tuple(piv + V(0, 0.05, 0)), 0.035, 0.02, segs=5)
    T.blade("chicken_orange", tuple(piv + V(0, 0.03, 0)), tuple(piv + V(0, 0.2, 0)), 0.03, 0.2, thick=0.01, up=(1, 0, 0))


@asset("kurakura", "animal", center=True)
def kurakura(A):
    """Pond turtle. Flippers are leg_fl/fr/rl/rr so the walk cycle paddles them."""
    B = A.part("body")
    B.dome("turtle_shell", (0, 0, 0.07), 0.2, segs=8, rings=6, scale=(0.9, 1.1, 0.62), smooth=False)
    B.cyl("turtle_belly", (0, 0, 0.055), 0.19, 0.04, segs=8, scale=(0.9, 1.1, 1))
    B.dome("turtle_skin", (0, 0, 0.1), 0.1, segs=6, rings=4, scale=(0.9, 1.1, 0.95), smooth=False)   # shell crown
    neck = V(0, -0.2, 0.08)
    H = A.part("head", pivot=tuple(neck))
    H.limb("turtle_skin", tuple(neck + V(0, 0.04, 0)), tuple(neck + V(0, -0.07, 0.03)), 0.035, 0.03, segs=5)
    H.sphere("turtle_skin", tuple(neck + V(0, -0.1, 0.04)), 0.045, segs=6, rings=4, scale=(0.9, 1.2, 0.8))
    for sx in (-1, 1):
        H.box("eye", tuple(neck + V(sx * 0.035, -0.115, 0.055)), (0.01, 0.014, 0.014))
    for nm, sx, sy in (("fl", 1, -1), ("fr", -1, -1), ("rl", 1, 1), ("rr", -1, 1)):
        top = (sx * 0.14, sy * 0.13, 0.06)
        P = A.part("leg_" + nm, pivot=top)
        P.blade("turtle_skin", top, (sx * 0.27, sy * 0.2, 0.03), 0.07, 0.1, thick=0.02)
    piv = V(0, 0.21, 0.06)
    T = A.part("tail", pivot=tuple(piv))
    T.limb("turtle_skin", tuple(piv), tuple(piv + V(0, 0.07, -0.01)), 0.02, 0.005, segs=4)


@asset("buaya", "animal", center=True)
def buaya(A):
    """A crocodile adrift: only the back, the eyes and the nostrils show above the water.
    The long tail is `tail`; the head is part of the body so it never nods."""
    B = A.part("body")
    B.sphere("croc", (0, 0.1, 0.16), 0.3, segs=8, rings=5, scale=(0.75, 2.2, 0.5))
    B.sphere("croc_belly", (0, 0.1, 0.1), 0.28, segs=8, rings=4, scale=(0.72, 2.1, 0.35))
    # head: a long flat snout
    B.tbox("croc", (0, -0.78, 0.14), (0.3, 0.5, 0.14), top=(0.8, 0.95))
    B.tbox("croc", (0, -1.2, 0.12), (0.2, 0.45, 0.09), top=(0.85, 1.0))
    B.box("croc_belly", (0, -1.05, 0.075), (0.2, 0.75, 0.03))
    for sx in (-1, 1):
        B.sphere("croc", (sx * 0.09, -0.62, 0.23), 0.05, segs=6, rings=4)                 # eye bumps
        B.box("yellow", (sx * 0.125, -0.635, 0.24), (0.02, 0.04, 0.03))
        B.box("eye", (sx * 0.134, -0.64, 0.24), (0.012, 0.012, 0.03))
        B.box("croc", (sx * 0.04, -1.39, 0.175), (0.04, 0.05, 0.03))                       # nostrils
        for i in range(3):                                                                 # teeth
            B.box("cat_white", (sx * 0.1, -0.95 - i * 0.15, 0.085), (0.015, 0.03, 0.035))
    for i in range(5):                                                                     # back ridges
        for sx in (-1, 1):
            B.tbox("croc_dark", (sx * 0.07, -0.34 + i * 0.22, 0.315 - abs(i - 2) * 0.016), (0.06, 0.13, 0.05), top=(0.3, 0.5))
    for nm, sx, sy in (("fl", 1, -1), ("fr", -1, -1), ("rl", 1, 1), ("rr", -1, 1)):
        top = (sx * 0.2, 0.1 + sy * 0.42, 0.12)
        P = A.part("leg_" + nm, pivot=top)
        P.limb("croc", top, (sx * 0.36, 0.1 + sy * 0.42, 0.03), 0.06, 0.045, segs=5)
        P.box("croc", (sx * 0.38, 0.06 + sy * 0.42, 0.02), (0.12, 0.14, 0.035))
    piv = V(0, 0.68, 0.16)
    T = A.part("tail", pivot=tuple(piv))
    T.limb("croc", tuple(piv - V(0, 0.08, 0)), tuple(piv + V(0, 1.15, -0.04)), 0.15, 0.02, segs=6)
    for i in range(6):
        T.tbox("croc_dark", (0, 0.76 + i * 0.16, 0.27 - i * 0.022), (0.035, 0.1, 0.09 - i * 0.008), top=(0.4, 0.5))


@asset("bebek", "animal", center=True)
def bebek(A):
    """A white duck on the water; its feet are under the surface, so it has none."""
    B = A.part("body")
    B.sphere("cow_white", (0, 0.02, 0.1), 0.13, segs=8, rings=5, scale=(0.85, 1.45, 0.8))
    for sx in (-1, 1):
        B.sphere("goat", (sx * 0.09, 0.04, 0.13), 0.09, segs=6, rings=4, scale=(0.3, 1.45, 0.75))   # folded wings
    neck = V(0, -0.12, 0.15)
    H = A.part("head", pivot=tuple(neck))
    H.limb("cow_white", tuple(neck - V(0, 0, 0.03)), tuple(neck + V(0, -0.03, 0.13)), 0.045, 0.035, segs=6)
    H.sphere("cow_white", tuple(neck + V(0, -0.045, 0.16)), 0.052, segs=6, rings=4)
    H.tbox("chicken_orange", tuple(neck + V(0, -0.115, 0.15)), (0.05, 0.08, 0.022), top=(0.9, 0.9))
    for sx in (-1, 1):
        H.box("eye", tuple(neck + V(sx * 0.042, -0.065, 0.175)), (0.01, 0.013, 0.013))
    piv = V(0, 0.19, 0.14)
    T = A.part("tail", pivot=tuple(piv))
    T.blade("cow_white", tuple(piv - V(0, 0.03, 0.01)), tuple(piv + V(0, 0.09, 0.06)), 0.1, 0.04, thick=0.02)


@asset("teratai", "prop")
def teratai(A):
    """A patch of lily pads with two flowers. The origin is the water surface."""
    B = A.part("body")
    pads = ((0.0, 0.0, 0.5, "leaf"), (0.85, 0.3, 0.42, "leaf_light"), (-0.7, 0.55, 0.38, "leaf"),
            (0.2, -0.85, 0.44, "leaf_light"), (-0.75, -0.5, 0.33, "leaf_dark"), (0.9, -0.55, 0.3, "leaf"),
            (-0.1, 0.95, 0.3, "leaf_light"))
    for i, (x, y, r, col) in enumerate(pads):
        B.cyl(col, (x, y, 0.035 + 0.004 * (i % 3)), r, 0.02, segs=9, rot=(0, 0, 37 * i))
        # the notch every lily pad has: a dark wedge towards the stalk
        a = math.radians(50 * i)
        B.blade("pond_dark", (x, y, 0.047 + 0.004 * (i % 3)), (x + r * 0.98 * math.cos(a), y + r * 0.98 * math.sin(a), 0.047 + 0.004 * (i % 3)),
                0.01, r * 0.3, thick=0.004)
    for x, y, s in ((0.38, 0.42, 1.0), (-0.42, -0.1, 0.8)):
        B.cyl("lotus_pink", (x, y, 0.06 + 0.07 * s), 0.05 * s, 0.14 * s, segs=7, r2=0.17 * s, smooth=False)
        B.cyl("cow_white", (x, y, 0.07 + 0.08 * s), 0.04 * s, 0.14 * s, segs=6, r2=0.1 * s, rot=(0, 0, 25), smooth=False)
        B.sphere("yellow", (x, y, 0.1 + 0.07 * s), 0.035 * s, segs=5, rings=3)


@asset("eceng_gondok", "prop")
def eceng_gondok(A):
    """A raft of water hyacinth: glossy rosettes on swollen stalks, a few lilac flower spikes."""
    B = A.part("body")
    spots = ((0.0, 0.0), (0.55, 0.2), (-0.45, 0.4), (0.2, -0.55), (-0.5, -0.35))
    for i, (x, y) in enumerate(spots):
        B.sphere("leaf_light", (x, y, 0.04), 0.1, segs=5, rings=3, scale=(1, 1, 0.7), smooth=False)   # float bulbs
        for k in range(4):
            a = 2 * math.pi * k / 4 + i
            d = V(math.cos(a), math.sin(a), 0)
            tip = V(x, y, 0.06) + d * 0.3 + V(0, 0, 0.16 + 0.05 * (k % 2))
            B.limb("leaf_light", (x, y, 0.05), tuple(V(x, y, 0.06) + d * 0.14 + V(0, 0, 0.1)), 0.025, 0.015, segs=3)
            B.blade("leaf" if (i + k) % 2 else "veg_green", tuple(V(x, y, 0.15) + d * 0.1), tuple(tip + d * 0.06), 0.2, 0.13, thick=0.012)
        if i % 2 == 0:
            B.limb("leaf_light", (x, y, 0.08), (x, y, 0.34), 0.014, 0.01, segs=4)
            B.cyl("hyacinth", (x, y, 0.4), 0.05, 0.16, segs=6, r2=0.02, smooth=False)
            B.sphere("yellow", (x + 0.03, y, 0.4), 0.018, segs=4, rings=3)


@asset("gelagah", "prop")
def gelagah(A):
    """Reeds and cattails standing in the shallows; the origin is the bed, a foot under water."""
    B = A.part("body")
    for i in range(13):
        a = 2.4 * i
        r = 0.12 + 0.11 * (i % 4)
        base = V(r * math.cos(a), r * math.sin(a), 0)
        h = 1.5 + 0.22 * ((i * 7) % 5)
        lean = 0.12 + 0.1 * (i % 3)
        tip = base + V(lean * math.cos(a) * 2.2, lean * math.sin(a) * 2.2, h)
        B.blade("reed" if i % 3 else "leaf_light", tuple(base), tuple(tip), 0.07, 0.012, thick=0.012,
                up=(-math.sin(a), math.cos(a), 0))
    for i, (x, y, h) in enumerate(((0.05, 0.04, 2.1), (-0.16, 0.12, 1.8), (0.14, -0.15, 1.95))):
        B.limb("reed", (x, y, 0), (x * 1.3, y * 1.3, h), 0.016, 0.01, segs=4)
        B.cyl("reed_head", (x * 1.27, y * 1.27, h - 0.2), 0.04, 0.26, segs=6)
