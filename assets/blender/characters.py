"""Barudak Bogor - characters. One base human, outfits by config.
Parts: torso (origin waist), head (origin neck, child of torso), arm_l / arm_r (origin
shoulder, children of torso), leg_l / leg_r (origin hip, children of root).
Facing -Y, so the character's LEFT limbs are on +X."""
import bmesh
from mathutils import Matrix, Vector
from common import asset

WAIST, NECK, SHOULDER, HIP = 0.98, 1.46, 1.38, 0.88


def human(A, c):
    skin = c.get("skin", "skin")
    shirt, pants = c["shirt"], c["pants"]
    ex = set(c.get("extras", ()))
    chest = c.get("jacket", shirt)
    T = A.part("torso", pivot=(0, 0, WAIST))
    Hd = A.part("head", pivot=(0, 0, NECK), parent="torso")

    # ---- legs
    for nm, sx in (("leg_l", 1), ("leg_r", -1)):
        P = A.part(nm, pivot=(sx * 0.09, 0, HIP))
        P.tbox(pants, (sx * 0.09, 0, 0.665), (0.125, 0.15, 0.43), top=(1.15, 1.1))
        shin = skin if "shorts" in ex else pants
        P.tbox(shin, (sx * 0.09, 0, 0.265), (0.1, 0.125, 0.37), top=(1.2, 1.12))
        if "shorts" in ex and c.get("socks"):
            P.box(c["socks"], (sx * 0.09, 0, 0.14), (0.105, 0.13, 0.12))
        P.tbox(c.get("shoes", "shoe"), (sx * 0.09, -0.04, 0.04), (0.115, 0.25, 0.08), top=(0.9, 0.75), shift=(0, 0.025))

    # ---- torso (pelvis + chest)
    T.box(pants, (0, 0, 0.9), (0.31, 0.185, 0.2))
    T.tbox(chest, (0, 0, 1.2), (0.31, 0.19, 0.44), top=(1.25, 1.05))
    T.box(skin, (0, 0, 1.45), (0.09, 0.09, 0.08))
    if "belt" in ex:
        T.box("black", (0, 0, 0.99), (0.315, 0.195, 0.035))
    if "collar" in ex:
        T.box(c.get("collar", "white"), (0, -0.02, 1.425), (0.17, 0.15, 0.03))
    if "jacket" in c:                                    # open jacket shows the shirt
        T.box(shirt, (0, -0.094, 1.22), (0.07, 0.012, 0.4))
    if "untucked" in ex:
        T.tbox(shirt, (0, 0, 0.9), (0.33, 0.2, 0.17), top=(0.97, 0.97))
    if "tunic" in ex:
        T.tbox(chest, (0, 0, 0.74), (0.37, 0.23, 0.5), top=(0.86, 0.86))
    if "sarong" in ex:
        T.tbox("sarong", (0, 0, 0.84), (0.35, 0.22, 0.34), top=(0.95, 0.95))
        T.box("cloth_yellow", (0, 0, 0.8), (0.355, 0.225, 0.04))
        T.box("sarong", (0.08, -0.115, 0.97), (0.12, 0.04, 0.08))
    if "tie" in ex:
        T.tbox(c.get("tie", "cloth_red"), (0, -0.1, 1.25), (0.05, 0.012, 0.3), top=(0.6, 1))
    if "vest" in ex:
        v = c.get("vest", "cloth_olive")
        for sx in (-1, 1):
            T.tbox(v, (sx * 0.105, -0.005, 1.2), (0.125, 0.205, 0.42), top=(1.25, 1.05), shift=(sx * 0.02, 0))
            T.box("cloth_khaki", (sx * 0.1, -0.11, 1.12), (0.08, 0.012, 0.07))
        T.box(v, (0, 0.06, 1.2), (0.3, 0.09, 0.42))
    if "apron" in ex:
        T.box(c.get("apron", "cloth_white"), (0, -0.1, 1.02), (0.26, 0.012, 0.5))
        T.box(c.get("apron", "cloth_white"), (0, 0, 1.0), (0.32, 0.2, 0.03))
    if "towel" in ex:
        T.box("towel", (0.13, 0, 1.432), (0.11, 0.23, 0.02))
        T.box("towel", (0.13, -0.11, 1.33), (0.11, 0.016, 0.2))
        T.box("towel", (0.13, 0.108, 1.36), (0.11, 0.016, 0.14))
    if "backpack" in ex:
        b = c.get("backpack", "cloth_teal")
        T.tbox(b, (0, 0.165, 1.2), (0.27, 0.13, 0.36), top=(0.85, 0.8))
        T.box("black", (0, 0.235, 1.13), (0.18, 0.02, 0.12))
        for sx in (-1, 1):
            T.box(b, (sx * 0.12, -0.1, 1.27), (0.035, 0.012, 0.3))
            T.box(b, (sx * 0.12, 0, 1.425), (0.035, 0.2, 0.012))
    if "bag" in ex:                                       # small hip / sling bag
        T.box(c.get("bag", "cloth_brown"), (-0.19, -0.02, 0.92), (0.07, 0.17, 0.14))
        T.box(c.get("bag", "cloth_brown"), (0, -0.1, 1.2), (0.3, 0.012, 0.03), rot=(0, 38, 0))
    if "badge" in ex:
        T.box("cloth_yellow", (0.1, -0.1, 1.3), (0.06, 0.012, 0.06))
    if "stripe" in ex:                                    # reflective / jacket stripe
        T.box(c.get("stripe", "white"), (0, 0, 1.27), (0.36, 0.2, 0.035))

    # ---- arms
    long = c.get("sleeve", "short") == "long"
    sleeve = c.get("sleeve_col", chest)
    for nm, sx in (("arm_l", 1), ("arm_r", -1)):
        P = A.part(nm, pivot=(sx * 0.235, 0, SHOULDER), parent="torso")
        x = sx * 0.25
        P.tbox(sleeve, (x, 0, 1.25), (0.09, 0.105, 0.3), top=(1.15, 1.05))
        P.tbox(sleeve if long else skin, (x + sx * 0.005, 0, 0.97), (0.075, 0.085, 0.27), top=(1.15, 1.15))
        P.box(skin, (x + sx * 0.005, -0.005, 0.79), (0.065, 0.09, 0.1))

    # ---- head
    Hd.sphere(skin, (0, 0, 1.585), 0.115, segs=8, rings=5, scale=(0.92, 1.0, 1.08))
    for sx in (-1, 1):
        Hd.box("eye", (sx * 0.042, -0.104, 1.595), (0.024, 0.016, 0.026))
    gear = c.get("gear")
    gcol = c.get("gear_col", "cloth_navy")
    hair = c.get("hair", "hair")
    if gear != "hijab" and gear != "helmet":
        Hd.dome(hair, (0, 0.012, 1.612), 0.124, segs=8, rings=4, scale=(0.95, 1.0, 0.92))
        Hd.box(hair, (0, 0.065, 1.56), (0.2, 0.1, 0.13))
    if gear == "cap":
        Hd.dome(gcol, (0, 0.005, 1.63), 0.128, segs=8, rings=4, scale=(0.97, 1.02, 0.8))
        Hd.box(gcol, (0, -0.16, 1.64), (0.17, 0.12, 0.015), rot=(8, 0, 0))
    elif gear == "bucket":
        Hd.dome(gcol, (0, 0.005, 1.64), 0.125, segs=8, rings=4, scale=(1, 1, 0.75))
        Hd.cyl(gcol, (0, 0.005, 1.645), 0.215, 0.05, r2=0.12, segs=8)
    elif gear == "peci":
        Hd.cyl(gcol, (0, 0.008, 1.715), 0.1, 0.1, segs=8, scale=(0.92, 1.1, 1))
    elif gear == "caping":
        Hd.cyl(gcol, (0, 0, 1.735), 0.27, 0.15, r2=0.01, segs=8, smooth=False)
    elif gear == "hijab":
        Hd.sphere(gcol, (0, 0.035, 1.592), 0.136, segs=8, rings=5, scale=(1, 1, 1.02))
        Hd.tbox(gcol, (0, 0.02, 1.44), (0.35, 0.25, 0.17), top=(0.55, 0.7))
    elif gear == "helmet":
        Hd.sphere(gcol, (0, 0.02, 1.605), 0.15, segs=8, rings=5, scale=(1, 1.05, 0.98))
        Hd.box("glass", (0, -0.12, 1.61), (0.2, 0.05, 0.085))
        Hd.box("black", (0, -0.1, 1.5), (0.03, 0.02, 0.1))

    # ---- scale to height
    s = c.get("height", 1.7) / 1.7
    hs = c.get("head_scale", 1.0)
    if hs != 1.0:
        bmesh.ops.transform(Hd.bm, verts=Hd.bm.verts, matrix=Matrix.Translation((0, 0, NECK)) @
                            Matrix.Scale(hs, 4) @ Matrix.Translation((0, 0, -NECK)))
    if s != 1.0:
        A.scale_all(s)


CAST = {
    # ---- players
    "ujang": dict(shirt="cloth_yellow", pants="denim", shoes="sandal", gear="cap", gear_col="cloth_navy",
                  extras=("collar", "towel", "belt"), collar="cloth_navy"),
    "cecep": dict(shirt="cloth_lightblue", pants="cloth_brown", shoes="sandal", gear="bucket",
                  gear_col="cloth_khaki", extras=("vest", "bag"), vest="cloth_olive", skin="skin_dark"),
    "mumu": dict(shirt="cloth_purple", pants="cloth_navy", shoes="shoe_white", gear="hijab",
                 gear_col="cloth_pink", sleeve="long", extras=("tunic", "backpack"), backpack="cloth_teal",
                 skin="skin_light", height=1.6),
    "deden": dict(shirt="cloth_white", pants="cloth_grey", shoes="shoe_white", sleeve="short",
                  extras=("untucked", "badge", "collar"), height=1.66),
    "aceng": dict(shirt="cloth_white", jacket="cloth_brown", pants="cloth_black", shoes="sandal",
                  gear="peci", gear_col="cloth_black", sleeve="long", extras=("sarong",)),
    # ---- NPCs
    "npc_sopir": dict(shirt="cloth_blue", pants="cloth_black", shoes="sandal", gear="cap",
                      gear_col="cloth_red", extras=("collar", "bag"), bag="cloth_black", skin="skin_dark"),
    "npc_mahasiswa": dict(shirt="cloth_white", jacket="cloth_navy", pants="denim", shoes="shoe_white",
                          sleeve="long", extras=("backpack",), backpack="cloth_black", skin="skin_light"),
    "npc_pedagang": dict(shirt="cloth_orange", pants="cloth_brown", shoes="sandal", gear="caping",
                         gear_col="bamboo_dry", extras=("apron",), skin="skin_dark"),
    "npc_anak_sekolah": dict(shirt="cloth_white", pants="cloth_red", shoes="shoe", gear="cap",
                             gear_col="cloth_red", extras=("shorts", "tie", "backpack", "collar"),
                             socks="cloth_white", backpack="cloth_blue", height=1.35, head_scale=1.18),
    "npc_pegawai": dict(shirt="cloth_lightblue", pants="cloth_navy", shoes="shoe", sleeve="long",
                        extras=("tie", "belt", "collar"), tie="cloth_red"),
    "npc_ojol": dict(shirt="cloth_black", jacket="ojol_green", pants="denim", shoes="shoe",
                     gear="helmet", gear_col="ojol_green", sleeve="long", extras=("stripe",),
                     stripe="cloth_black"),
}


def _make(name, cfg):
    @asset(name, "character")
    def build(A):
        human(A, cfg)
    return build


for _n, _c in CAST.items():
    _make(_n, _c)
