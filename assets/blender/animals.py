"""Barudak Bogor - animals. Parts: body, head (origin at neck), leg_fl/fr/rl/rr
(origin at top of leg), tail (origin at rump). Facing -Y, left = +X."""
import math
from mathutils import Vector
from common import asset, quadruped


def V(*a):
    return Vector(a)


def ears(q, col, size=(0.05, 0.02, 0.1), spread=0.4, out=20, top=(0.3, 0.6), back=0.3):
    H, hc, up, fwd = q["head"], q["head_c"], q["up"], q["fwd"]
    for sx in (-1, 1):
        p = hc + V(sx * q["hw"] * spread, 0, 0) + up * (q["hh"] * 0.5 + size[2] * 0.4) - fwd * q["hl"] * back
        H.tbox(col, tuple(p), size, top=top, rot=(0, sx * out, 0))


def horn_chain(H, col, pts, r0, r1, segs=5):
    n = len(pts) - 1
    for i in range(n):
        a = r0 + (r1 - r0) * i / n
        b = r0 + (r1 - r0) * (i + 1) / n
        H.limb(col, tuple(pts[i]), tuple(pts[i + 1]), a, b, segs=segs)


def patch(q, c, col, t, phi, side, r, oy=0.0):
    """Flat coloured blob on the body barrel. t in -1..1 along the body (front = -1),
    phi = elevation angle in degrees (0 = flank, 90 = back)."""
    L, W, D = c["L"], c["W"], c["D"]
    rad = max(0.0, 1 - t ** 4) ** 0.25
    ph = math.radians(phi)
    x = side * W / 2 * rad * math.cos(ph)
    z = q["zc"] + D / 2 * rad * math.sin(ph)
    q["body"].sphere(col, (x, oy + L / 2 * t, z), r, segs=6, rings=3, scale=(0.08, 1.25, 1.0),
                     rot=(0, -phi if side > 0 else 180 + phi, 0))


@asset("rusa", "animal", center=True)
def rusa(A):
    c = dict(L=1.2, W=0.36, D=0.46, Hs=1.0, leg=0.042, col="deer", leg_col="deer", hoof="hoof",
             neck=0.5, neck_ang=62, neck_r=0.105, head=(0.15, 0.16, 0.32), pitch=22,
             snout=(0.55, 0.55), tail=(0.16, -35, 0.04), tail_col="deer_spot")
    q = quadruped(A, c)
    ears(q, "deer", size=(0.07, 0.02, 0.13), spread=0.75, out=55)
    H, hc, up, fwd = q["head"], q["head_c"], q["up"], q["fwd"]
    for sx in (-1, 1):
        b = hc + V(sx * 0.045, 0, 0) + up * 0.08 - fwd * 0.08
        p1 = b + V(sx * 0.1, 0.1, 0.22)
        p2 = p1 + V(sx * 0.1, -0.03, 0.25)
        horn_chain(H, "antler", [b, p1, p2], 0.02, 0.008, segs=4)
        H.limb("antler", tuple(b + V(sx * 0.03, 0.03, 0.07)), tuple(b + V(sx * 0.05, -0.14, 0.2)), 0.013, 0.005, segs=4)
        H.limb("antler", tuple(p1), tuple(p1 + V(sx * -0.02, 0.14, 0.14)), 0.012, 0.005, segs=4)
    for i, (t, phi) in enumerate(((-0.5, 50), (-0.2, 30), (0.1, 55), (0.42, 35), (0.68, 55), (-0.1, 75))):
        for sx in (-1, 1):
            patch(q, c, "deer_spot", t, phi, sx, 0.035)


@asset("tupai", "animal", center=True)
def tupai(A):
    c = dict(L=0.19, W=0.075, D=0.085, Hs=0.1, leg=0.012, col="squirrel", hoof="squirrel", hoof_h=0.012,
             neck=0.035, neck_ang=35, neck_r=0.03, head=(0.055, 0.052, 0.075), pitch=15,
             snout=(0.5, 0.55), eye=0.012)
    q = quadruped(A, c)
    ears(q, "squirrel", size=(0.018, 0.008, 0.025), spread=0.6, out=15)
    q["body"].sphere("cat_white", (0, -0.02, q["zc"] - 0.03), 0.03, segs=5, rings=3, scale=(0.9, 2.2, 0.5))
    r = q["rump"]
    T = A.part("tail", pivot=tuple(r))
    pts = [r, r + V(0, 0.05, 0.05), r + V(0, 0.06, 0.13), r + V(0, 0.01, 0.185)]
    T.limb("squirrel", tuple(pts[0]), tuple(pts[1]), 0.016, 0.034, segs=5)
    T.limb("squirrel", tuple(pts[1]), tuple(pts[2]), 0.034, 0.04, segs=5)
    T.limb("squirrel", tuple(pts[2]), tuple(pts[3]), 0.04, 0.012, segs=5)


@asset("monyet", "animal", center=True)
def monyet(A):
    c = dict(L=0.46, W=0.21, D=0.25, Hs=0.42, leg=0.038, col="monkey", hoof="monkey_face", hoof_h=0.03,
             neck=0.09, neck_ang=55, neck_r=0.07, head=(0.14, 0.15, 0.16), pitch=10,
             snout=(0.75, 0.7), eye=0.018, head_col="monkey")
    q = quadruped(A, c)
    H, hc, up, fwd = q["head"], q["head_c"], q["up"], q["fwd"]
    H.tbox("monkey_face", tuple(hc + fwd * 0.075 - up * 0.01), (0.1, 0.1, 0.04), top=(0.7, 0.7), rot=(100, 0, 0))
    for sx in (-1, 1):
        H.box("monkey_face", tuple(hc + V(sx * 0.075, 0.03, 0.02)), (0.02, 0.04, 0.045))
    r = q["rump"]
    T = A.part("tail", pivot=tuple(r))
    horn_chain(T, "monkey", [r, r + V(0, 0.14, 0.06), r + V(0, 0.26, -0.05), r + V(0, 0.34, -0.2)], 0.018, 0.008)


@asset("kucing", "animal", center=True)
def kucing(A):
    c = dict(L=0.38, W=0.14, D=0.17, Hs=0.27, leg=0.024, col="cat_orange", hoof="cat_white", hoof_h=0.025, nose="udder",
             neck=0.06, neck_ang=45, neck_r=0.05, head=(0.115, 0.1, 0.105), pitch=8,
             snout=(0.7, 0.7), eye=0.016)
    q = quadruped(A, c)
    ears(q, "cat_orange", size=(0.04, 0.012, 0.045), spread=0.62, out=8, top=(0.1, 0.5))
    q["body"].sphere("cat_white", (0, -0.09, q["zc"] - 0.045), 0.05, segs=5, rings=3, scale=(0.9, 1.6, 0.6))
    q["head"].box("cat_white", tuple(q["head_c"] + q["fwd"] * 0.045 - q["up"] * 0.025), (0.06, 0.03, 0.035))
    r = q["rump"]
    T = A.part("tail", pivot=tuple(r))
    horn_chain(T, "cat_orange", [r, r + V(0, 0.08, 0.08), r + V(0, 0.1, 0.2), r + V(0, 0.06, 0.28)], 0.016, 0.01)
    for t in (-0.45, 0.0, 0.45):                         # tabby stripes
        for sx in (-1, 1):
            patch(q, c, "orange", t, 62, sx, 0.035)


@asset("kerbau", "animal", center=True)
def kerbau(A):
    c = dict(L=2.0, W=0.8, D=0.9, Hs=1.32, leg=0.13, col="buffalo", hoof="hoof",
             neck=0.42, neck_ang=8, neck_r=0.27, head=(0.34, 0.33, 0.58), pitch=28,
             snout=(0.7, 0.7), tail=(0.75, -80, 0.035), eye=0.04)
    q = quadruped(A, c)
    ears(q, "buffalo", size=(0.16, 0.03, 0.1), spread=1.0, out=80, back=0.45)
    H, hc, up, fwd = q["head"], q["head_c"], q["up"], q["fwd"]
    for sx in (-1, 1):
        b = hc + V(sx * 0.1, 0, 0) + up * 0.14 - fwd * 0.2
        horn_chain(H, "horn", [b, b + V(sx * 0.3, 0.08, 0.03), b + V(sx * 0.54, 0.26, 0.1),
                               b + V(sx * 0.62, 0.48, 0.2), b + V(sx * 0.52, 0.66, 0.3)], 0.075, 0.015)
    q["tail"].sphere("horn", tuple(q["rump"] + V(0, 0.14, -0.74)), 0.06, segs=5, rings=3, scale=(1, 1, 1.6))


@asset("sapi", "animal", center=True)
def sapi(A):
    c = dict(L=1.75, W=0.66, D=0.78, Hs=1.3, leg=0.105, col="cow_brown", leg_col="cow_brown", hoof="hoof",
             neck=0.42, neck_ang=18, neck_r=0.2, head=(0.27, 0.28, 0.5), pitch=28,
             snout=(0.75, 0.7), tail=(0.75, -80, 0.03), eye=0.035, head_col="cow_white")
    q = quadruped(A, c)
    ears(q, "cow_brown", size=(0.13, 0.03, 0.09), spread=1.0, out=80, back=0.45)
    H, hc, up, fwd = q["head"], q["head_c"], q["up"], q["fwd"]
    H.box("cow_brown", tuple(hc + up * 0.13 - fwd * 0.12), (0.24, 0.26, 0.06), rot=(28, 0, 0))
    for sx in (-1, 1):
        b = hc + V(sx * 0.08, 0, 0) + up * 0.13 - fwd * 0.2
        horn_chain(H, "antler", [b, b + V(sx * 0.12, 0, 0.05), b + V(sx * 0.17, -0.03, 0.16)], 0.03, 0.01)
    for t, phi, sx, r in ((-0.45, 25, 1, 0.16), (0.25, 45, 1, 0.19), (0.62, 5, 1, 0.13), (-0.2, 50, -1, 0.2),
                          (0.5, 25, -1, 0.16), (-0.6, 0, -1, 0.12), (0.05, 90, 1, 0.15)):
        patch(q, c, "cow_white", t, phi, sx, r)
    q["body"].sphere("udder", (0, 0.42, q["zc"] - 0.36), 0.13, segs=6, rings=4, scale=(1, 1.2, 0.8))
    q["tail"].sphere("cow_white", tuple(q["rump"] + V(0, 0.14, -0.74)), 0.055, segs=5, rings=3, scale=(1, 1, 1.6))


@asset("kambing", "animal", center=True)
def kambing(A):
    c = dict(L=0.85, W=0.3, D=0.38, Hs=0.66, leg=0.04, col="goat", hoof="hoof",
             neck=0.25, neck_ang=58, neck_r=0.085, head=(0.13, 0.15, 0.27), pitch=25,
             snout=(0.6, 0.6), tail=(0.12, 40, 0.03), eye=0.02)
    q = quadruped(A, c)
    ears(q, "goat_dark", size=(0.1, 0.02, 0.05), spread=1.0, out=100, back=0.35)
    H, hc, up, fwd = q["head"], q["head_c"], q["up"], q["fwd"]
    for sx in (-1, 1):
        b = hc + V(sx * 0.035, 0, 0) + up * 0.07 - fwd * 0.08
        horn_chain(H, "horn", [b, b + V(sx * 0.02, 0.07, 0.1), b + V(sx * 0.05, 0.17, 0.13)], 0.02, 0.006)
    H.tbox("goat_dark", tuple(hc + fwd * 0.08 - up * 0.13), (0.04, 0.04, 0.1), top=(2.0, 2.0))   # beard
    patch(q, c, "goat_dark", -0.1, 80, 1, 0.1)
    patch(q, c, "goat_dark", 0.55, 30, -1, 0.09)
    patch(q, c, "goat_dark", 0.45, 20, 1, 0.08)


@asset("anjing", "animal", center=True)
def anjing(A):
    """Kampung dog: lean, tan with a white chest, pricked ears, curled-up tail."""
    c = dict(L=0.62, W=0.19, D=0.23, Hs=0.5, leg=0.03, col="dog_tan", hoof="cat_white", hoof_h=0.035,
             neck=0.17, neck_ang=48, neck_r=0.065, head=(0.125, 0.12, 0.23), pitch=12,
             snout=(0.5, 0.55), eye=0.02)
    q = quadruped(A, c)
    ears(q, "dog_tan", size=(0.05, 0.015, 0.08), spread=0.62, out=10, top=(0.1, 0.5))
    q["body"].sphere("cat_white", (0, -0.25, q["zc"] - 0.02), 0.085, segs=6, rings=4, scale=(0.85, 0.8, 1.0))
    r = q["rump"]
    T = A.part("tail", pivot=tuple(r))
    pts = [r, r + V(0, 0.06, 0.07), r + V(0, 0.07, 0.15), r + V(0, 0.02, 0.21), r + V(0, -0.05, 0.2),
           r + V(0, -0.08, 0.15)]
    horn_chain(T, "dog_tan", pts[:5], 0.022, 0.014)
    T.limb("cat_white", tuple(pts[4]), tuple(pts[5]), 0.014, 0.008, segs=5)


@asset("ayam", "animal", center=True)
def ayam(A):
    """Kampung rooster. Its two legs are leg_fl / leg_fr so the walk cycle drives them."""
    B = A.part("body")
    B.sphere("chicken_brown", (0, 0, 0.2), 0.1, segs=8, rings=5, scale=(0.8, 1.35, 0.95), rot=(-15, 0, 0))
    for sx in (-1, 1):                                                       # wings
        B.sphere("squirrel", (sx * 0.072, 0.02, 0.21), 0.07, segs=6, rings=4, scale=(0.25, 1.3, 0.75),
                 rot=(-15, 0, 0))
    neck = V(0, -0.12, 0.33)
    B.limb("chicken_orange", (0, -0.06, 0.22), tuple(neck), 0.055, 0.03, segs=6)
    H = A.part("head", pivot=tuple(neck))
    H.sphere("chicken_orange", (0, -0.13, 0.35), 0.036, segs=6, rings=4, scale=(0.9, 1.1, 1.0))
    H.limb("chicken_leg", (0, -0.16, 0.352), (0, -0.205, 0.342), 0.013, 0.002, segs=4)       # beak
    H.prism("red", [(-0.175, 0.375), (-0.09, 0.37), (-0.1, 0.405), (-0.14, 0.415), (-0.165, 0.4)], 0.012)  # comb
    H.tbox("red", (0, -0.155, 0.318), (0.016, 0.022, 0.04), top=(0.5, 0.5))                   # wattle
    for sx in (-1, 1):
        H.box("eye", (sx * 0.031, -0.14, 0.357), (0.008, 0.012, 0.012))
    for nm, sx in (("fl", 1), ("fr", -1)):
        x = sx * 0.04
        top = (x, 0.01, 0.15)
        P = A.part("leg_" + nm, pivot=top)
        P.limb("chicken_brown", top, (x, 0.01, 0.09), 0.028, 0.014, segs=5)
        P.limb("chicken_leg", (x, 0.01, 0.09), (x, 0.0, 0.012), 0.009, 0.008, segs=4)
        P.box("chicken_leg", (x, -0.02, 0.006), (0.035, 0.08, 0.012))
    piv = V(0, 0.11, 0.24)
    T = A.part("tail", pivot=tuple(piv))
    for dx, dy, dz, w in ((0, 0.08, 0.15, 0.05), (0.012, 0.15, 0.12, 0.045), (-0.012, 0.18, 0.05, 0.04),
                          (0, 0.17, -0.03, 0.035)):
        T.blade("chicken_green", tuple(piv), tuple(piv + V(dx, dy, dz)), w, w * 0.4, thick=0.008, up=(1, 0, 0))
