"""Barudak Bogor - vehicles. Front faces -Y, left side is +X."""
import math
from common import asset, quadruped


def greenhouse(P, paint, fb, ft, rt, rb, wb, wt, pillars=(), roof_t=0.05):
    """Glass cabin with painted roof and pillars. fb/ft/rt/rb = (y, z) of front-base,
    front-top, rear-top, rear-base; wb/wt = width at belt line / roof."""
    P.prism("glass", [(fb[0], fb[1], wb), (rb[0], rb[1], wb), (rt[0], rt[1], wt), (ft[0], ft[1], wt)])
    P.prism(paint, [(ft[0] - 0.04, ft[1] - 0.01, wt + 0.05), (rt[0] + 0.04, rt[1] - 0.01, wt + 0.05),
                    (rt[0] + 0.02, rt[1] + roof_t, wt - 0.02), (ft[0] - 0.02, ft[1] + roof_t, wt - 0.02)])
    for sx in (-1, 1):
        P.limb(paint, (sx * wb / 2, fb[0], fb[1]), (sx * wt / 2, ft[0], ft[1]), 0.04, segs=4)
        P.limb(paint, (sx * wb / 2, rb[0], rb[1]), (sx * wt / 2, rt[0], rt[1]), 0.04, segs=4)
        for y in pillars:
            P.limb(paint, (sx * wb / 2, y, fb[1]), (sx * wt / 2, y, ft[1]), 0.04, segs=4)


def wheels4(A, W, yf, yr, r=0.28, w=0.18, arch=0.03):
    x = W / 2 - w / 2 + 0.01
    A.wheel("wheel_fl", (x, yf, r), r, w)
    A.wheel("wheel_fr", (-x, yf, r), r, w)
    A.wheel("wheel_rl", (x, yr, r), r, w)
    A.wheel("wheel_rr", (-x, yr, r), r, w)
    B = A.get("body")
    for y in (yf, yr):
        B.cyl("black", (0, y, r + arch), r + 0.08, W + 0.012, axis="x", segs=10)


def lights(B, W, yf, yr, z=0.68, zr=0.75, plate=True):
    for sx in (-1, 1):
        B.box("emit_headlight", (sx * (W / 2 - 0.22), yf - 0.005, z), (0.26, 0.03, 0.14))
        B.box("emit_taillight", (sx * (W / 2 - 0.12), yr + 0.005, zr), (0.14, 0.03, 0.22))
    B.box("black", (0, yf - 0.005, z), (W - 0.75, 0.03, 0.12))
    B.box("bumper", (0, yf - 0.02, z - 0.28), (W + 0.02, 0.1, 0.16))
    B.box("bumper", (0, yr + 0.02, z - 0.28), (W + 0.02, 0.1, 0.16))
    if plate:
        B.box("white", (0, yf - 0.075, z - 0.28), (0.34, 0.01, 0.1))
        B.box("white", (0, yr + 0.075, z - 0.28), (0.34, 0.01, 0.1))


def mirrors(B, W, y, z):
    for sx in (-1, 1):
        B.box("black", (sx * (W / 2 + 0.07), y, z), (0.12, 0.04, 0.14))


@asset("angkot", "vehicle", center=True)
def angkot(A):
    B = A.part("body")
    W = 1.55
    B.prism("angkot_green", [(-1.95, 0.32), (-1.95, 0.88), (-1.84, 1.0), (1.95, 1.0), (1.95, 0.32)], W)
    greenhouse(B, "angkot_green", (-1.82, 1.0), (-1.42, 1.72), (1.86, 1.72), (1.93, 1.0),
               W - 0.02, W - 0.16, pillars=(-0.85, 0.2, 1.1))
    B.box("angkot_stripe", (0, 0.03, 0.82), (W + 0.02, 3.8, 0.12))
    lights(B, W, -1.95, 1.95)
    mirrors(B, W, -1.6, 1.15)
    # open passenger doorway on the kerb (left, +X) side, door slid back
    lean = -math.degrees(math.atan2(0.07, 0.72))
    B.box("interior_dark", (W / 2 - 0.05, -0.32, 0.72), (0.12, 0.95, 0.56))
    B.box("interior_dark", (W / 2 - 0.085, -0.32, 1.34), (0.12, 0.95, 0.68), rot=(0, lean, 0))
    B.box("steel", (W / 2 + 0.04, -0.32, 0.4), (0.14, 0.9, 0.04))            # step
    B.box("angkot_green", (W / 2 + 0.035, 0.7, 0.72), (0.04, 0.95, 0.56))     # slid door
    B.box("angkot_stripe", (W / 2 + 0.04, 0.7, 0.82), (0.04, 0.95, 0.12))
    B.box("glass", (W / 2 + 0.0, 0.7, 1.34), (0.04, 0.85, 0.56), rot=(0, lean, 0))
    # bench seats visible through the doorway
    B.box("seat_black", (0.45, -0.32, 0.75), (0.5, 0.9, 0.08))
    # roof sign + route board
    B.box("emit_sign", (0, -1.15, 1.83), (0.62, 0.2, 0.12))
    B.box("black", (0, -1.15, 1.775), (0.7, 0.26, 0.02))
    B.box("sign_yellow", (0, -1.83, 1.05), (0.5, 0.02, 0.08))
    wheels4(A, W, -1.25, 1.2)


@asset("mobil_keluarga", "vehicle", center=True)
def mobil_keluarga(A):
    B = A.part("body")
    W = 1.7
    B.prism("car_silver", [(-2.2, 0.3), (-2.2, 0.72), (-2.08, 0.86), (-1.25, 1.0), (2.15, 1.0),
                           (2.2, 0.85), (2.2, 0.3)], W)
    greenhouse(B, "car_silver", (-1.2, 1.0), (-0.5, 1.6), (1.85, 1.62), (2.15, 1.0),
               W - 0.02, W - 0.24, pillars=(0.05, 1.05))
    lights(B, W, -2.2, 2.2, z=0.66, zr=0.8)
    mirrors(B, W, -0.95, 1.08)
    B.box("black", (0, 0, 0.36), (W + 0.02, 4.3, 0.1))                      # sill
    for sx in (-1, 1):                                                       # roof rails, handles
        B.box("black", (sx * 0.6, 0.7, 1.7), (0.04, 1.9, 0.04))
        for y in (-0.3, 0.75):
            B.box("black", (sx * (W / 2 + 0.005), y, 0.88), (0.02, 0.14, 0.03))
        B.box("black", (sx * (W / 2 + 0.003), 0.03, 0.66), (0.01, 0.015, 0.6))
        B.box("black", (sx * (W / 2 + 0.003), -1.2, 0.66), (0.01, 0.015, 0.6))
        B.box("black", (sx * (W / 2 + 0.003), 1.08, 0.66), (0.01, 0.015, 0.6))
    wheels4(A, W, -1.4, 1.35, r=0.31, w=0.2)


@asset("motor", "vehicle", center=True)
def motor(A):
    B = A.part("body")
    yf, yr, r = -0.66, 0.62, 0.25
    A.wheel("wheel_f", (0, yf, r), r, 0.09)
    A.wheel("wheel_r", (0, yr, r), r, 0.1)
    B.box("moto_red", (0, yf, r + 0.3), (0.12, 0.42, 0.05))                   # front fender
    for sx in (-1, 1):
        B.limb("chrome", (sx * 0.07, yf, r), (sx * 0.07, -0.44, 0.9), 0.02, segs=4)
    B.slab("moto_red", (-0.5, 0.33), (-0.4, 0.9), 0.38, 0.06)                 # leg shield
    B.tbox("moto_red", (0, -0.43, 0.97), (0.3, 0.22, 0.2), top=(0.8, 0.7))    # head cowl
    B.box("emit_headlight", (0, -0.545, 0.97), (0.17, 0.03, 0.1))
    B.cyl("black", (0, -0.38, 1.06), 0.02, 0.64, axis="x", segs=5)            # handlebar
    for sx in (-1, 1):
        B.limb("black", (sx * 0.24, -0.38, 1.06), (sx * 0.29, -0.4, 1.17), 0.008, segs=4)
        B.box("black", (sx * 0.29, -0.41, 1.19), (0.1, 0.02, 0.07))
    B.box("black", (0, -0.2, 0.33), (0.32, 0.5, 0.06))                        # foot board
    B.prism("moto_red", [(-0.05, 0.36), (0.0, 0.7), (0.85, 0.78), (0.9, 0.62), (0.5, 0.42)], 0.3)
    B.prism("seat_black", [(-0.02, 0.7), (0.0, 0.79), (0.7, 0.87), (0.76, 0.79)], 0.26)
    B.box("engine_grey", (0, 0.22, 0.3), (0.22, 0.42, 0.2))
    B.cyl("chrome", (-0.17, 0.62, 0.34), 0.05, 0.5, axis="y", segs=6)         # exhaust
    B.box("emit_taillight", (0, 0.9, 0.7), (0.14, 0.03, 0.06))
    B.slab("black", (0.7, 0.58), (0.95, 0.46), 0.1, 0.03)                     # rear fender
    B.box("white", (0, 0.955, 0.5), (0.16, 0.01, 0.08))
    for sx in (-1, 1):
        B.limb("steel", (sx * 0.1, yr, r), (sx * 0.1, 0.45, 0.62), 0.015, segs=4)


@asset("bajaj", "vehicle", center=True)
def bajaj(A):
    B = A.part("body")
    W = 1.3
    paint = "bajaj_blue"
    B.box(paint, (0, 0.375, 0.6), (W, 1.85, 0.5))                           # tub  y -0.55..1.3
    B.prism(paint, [(-0.65, -0.55), (-0.3, -1.3), (0.3, -1.3), (0.65, -0.55)], 0.6, x=0.65, plane="xy")
    B.prism(paint, [(-0.55, 0.95), (-1.25, 0.95), (-0.55, 1.12)], 0.9)        # scuttle
    B.box("sign_yellow", (0, 0.375, 0.62), (W + 0.02, 1.85, 0.08))
    B.box("interior_dark", (0, 0.3, 0.855), (W - 0.1, 1.6, 0.01))
    for sx in (-1, 1):                                                       # half doors / sides
        B.box(paint, (sx * (W / 2 - 0.02), 0.6, 0.95), (0.04, 1.4, 0.3))
        B.prism("canvas_black", [(0.5, 1.1), (1.28, 1.1), (0.86, 1.68), (0.5, 1.68)], 0.03, x=sx * (W / 2 - 0.02))
        B.limb(paint, (sx * 0.6, -0.55, 0.85), (sx * 0.56, -0.42, 1.62), 0.03, segs=4)
        B.limb(paint, (sx * 0.62, 0.0, 0.85), (sx * 0.62, 0.0, 1.66), 0.025, segs=4)
    B.box(paint, (0, 1.28, 0.95), (W, 0.04, 0.3))
    B.slab("glass", (-0.55, 1.1), (-0.42, 1.62), 1.14, 0.03)                  # windscreen
    B.prism("canvas_black", [(-0.5, 1.62), (-0.5, 1.68), (0.86, 1.74), (1.32, 1.12), (1.32, 1.06),
                             (1.27, 1.06), (0.84, 1.68)], W - 0.02)           # roof + back
    B.box("glass", (0, 1.12, 1.42), (0.6, 0.02, 0.2), rot=(-36, 0, 0))
    B.box("emit_headlight", (0, -1.3, 0.78), (0.2, 0.03, 0.14))
    B.box("bumper", (0, -1.31, 0.42), (0.66, 0.06, 0.1))
    B.box("bumper", (0, 1.32, 0.42), (W + 0.02, 0.06, 0.1))
    for sx in (-1, 1):
        B.box("emit_taillight", (sx * 0.52, 1.305, 0.72), (0.12, 0.02, 0.14))
        B.box("black", (sx * 0.68, -0.5, 1.25), (0.1, 0.03, 0.14))
    B.box("seat_black", (0, -0.2, 0.92), (0.5, 0.4, 0.12))                    # driver seat
    B.box("seat_black", (0, 0.05, 1.12), (0.5, 0.08, 0.5))
    B.box("seat_black", (0, 0.8, 0.92), (1.1, 0.45, 0.12))                    # bench
    B.box("seat_black", (0, 1.06, 1.14), (1.1, 0.08, 0.5), rot=(-12, 0, 0))
    B.limb("black", (0, -0.75, 0.95), (0, -0.55, 1.18), 0.02, segs=4)
    B.cyl("black", (0, -0.55, 1.18), 0.018, 0.5, axis="x", segs=5)
    A.wheel("wheel_f", (0, -1.0, 0.21), 0.21, 0.11)
    A.wheel("wheel_rl", (W / 2 - 0.06, 0.82, 0.21), 0.21, 0.11)
    A.wheel("wheel_rr", (-W / 2 + 0.06, 0.82, 0.21), 0.21, 0.11)
    B.cyl("black", (0, 0.82, 0.24), 0.29, W + 0.012, axis="x", segs=10)


def pickup(A, paint):
    B = A.part("body")
    W = 1.55
    B.prism(paint, [(-2.0, 0.32), (-2.0, 0.82), (-1.9, 0.95), (-0.62, 0.95), (-0.62, 0.32)], W)
    greenhouse(B, paint, (-1.88, 0.95), (-1.52, 1.62), (-0.72, 1.62), (-0.64, 0.95), W - 0.02, W - 0.16)
    B.box("bumper", (0, 0.7, 0.42), (0.9, 2.6, 0.14))
    B.box(paint, (0, 0.7, 0.56), (W, 2.62, 0.1))
    for sx in (-1, 1):
        B.box(paint, (sx * (W / 2 - 0.02), 0.7, 0.76), (0.04, 2.62, 0.3))
    B.box(paint, (0, 1.99, 0.76), (W, 0.04, 0.3))
    B.box(paint, (0, -0.59, 0.76), (W, 0.04, 0.3))
    lights(B, W, -2.0, 2.01, z=0.66, zr=0.72)
    mirrors(B, W, -1.62, 1.1)
    wheels4(A, W, -1.3, 1.25)
    return B, W


@asset("mobil_tahu_bulat", "vehicle", center=True)
def mobil_tahu_bulat(A):
    B, W = pickup(A, "pickup_white")
    # canopy on four posts
    for sx in (-1, 1):
        for y in (-0.5, 1.92):
            B.limb("steel", (sx * 0.7, y, 0.9), (sx * 0.7, y, 1.96), 0.025, segs=4)
    B.box("tarp_orange", (0, 0.71, 1.98), (W + 0.12, 2.75, 0.06))
    # sign boards along both sides + rear
    for sx in (-1, 1):
        B.box("sign_yellow", (sx * (W / 2 + 0.05), 0.71, 1.78), (0.03, 2.3, 0.3))
        B.box("sign_red", (sx * (W / 2 + 0.07), 0.71, 1.78), (0.01, 1.7, 0.12))
    B.box("sign_yellow", (0, 2.07, 1.78), (1.3, 0.03, 0.3))
    B.box("sign_red", (0, 2.09, 1.78), (0.9, 0.01, 0.12))
    # stove + wok at the back
    B.box("steel", (0, 1.4, 0.86), (0.9, 0.8, 0.5))
    B.cyl("iron_dark", (0, 1.4, 1.18), 0.18, 0.14, r2=0.4, segs=10)
    B.cyl("broth", (0, 1.4, 1.245), 0.37, 0.01, segs=10)
    for i in range(6):
        a = i * 1.05
        B.sphere("tofu", (0.2 * math.cos(a), 1.4 + 0.2 * math.sin(a), 1.27), 0.065, segs=5, rings=3)
    B.sphere("tofu", (0, 1.4, 1.28), 0.065, segs=5, rings=3)
    # table with a tray pile and a gas bottle
    B.box("wood", (0, 0.35, 0.86), (1.1, 0.9, 0.5))
    B.box("aluminium", (0, 0.35, 1.13), (0.9, 0.7, 0.04))
    for ix in range(4):
        for iy in range(3):
            B.sphere("tofu", (-0.27 + ix * 0.18, 0.17 + iy * 0.18, 1.2), 0.075, segs=5, rings=3)
    for ix in range(3):
        B.sphere("tofu", (-0.18 + ix * 0.18, 0.35, 1.31), 0.075, segs=5, rings=3)
    B.cyl("cart_green", (0.5, -0.3, 0.8), 0.13, 0.36, segs=8)
    # speaker horn on the cab roof (the famous jingle)
    B.box("steel", (0, -1.1, 1.71), (0.12, 0.12, 0.08))
    B.cyl("aluminium", (0, -1.2, 1.87), 0.2, 0.34, axis="y", r2=0.05, segs=10)
    B.cyl("iron_dark", (0, -0.98, 1.87), 0.07, 0.12, axis="y", segs=6)


@asset("mobil_bakso", "vehicle", center=True)
def mobil_bakso(A):
    B, W = pickup(A, "pickup_blue")
    # counter cabinet
    B.box("wood_light", (0, 0.72, 0.88), (W - 0.06, 2.5, 0.56))
    B.box("aluminium", (0, 0.72, 1.17), (W, 2.56, 0.03))
    # roof on posts
    for sx in (-1, 1):
        for y in (-0.5, 1.92):
            B.limb("steel", (sx * 0.72, y, 1.18), (sx * 0.72, y, 2.02), 0.025, segs=4)
    B.box("pickup_white", (0, 0.71, 2.05), (W + 0.1, 2.7, 0.06))
    # awning on the kerb side (+X)
    B.xslab("tarp_blue", (W / 2 + 0.03, 2.03), (W / 2 + 0.85, 1.78), 2.6, 0.03, y=0.71)
    for y in (-0.5, 1.92):
        B.limb("steel", (W / 2, y, 1.5), (W / 2 + 0.8, y, 1.78), 0.015, segs=4)
    # glass display case with frame
    cx, cy = 0.3, 1.35
    B.box("glass", (cx, cy, 1.47), (0.8, 0.95, 0.56))
    B.box("aluminium", (cx, cy, 1.76), (0.84, 0.99, 0.03))
    for sx in (-1, 1):
        for sy in (-1, 1):
            B.limb("aluminium", (cx + sx * 0.4, cy + sy * 0.475, 1.18), (cx + sx * 0.4, cy + sy * 0.475, 1.76), 0.018, segs=4)
    B.box("sign_red", (cx + 0.405, cy, 1.66), (0.01, 0.7, 0.12))
    # tray of bakso + bowls on the counter
    B.box("aluminium", (0.35, 0.45, 1.2), (0.6, 0.5, 0.03))
    for ix in range(3):
        for iy in range(3):
            B.sphere("bakso", (0.18 + ix * 0.17, 0.3 + iy * 0.15, 1.27), 0.07, segs=5, rings=3)
    for i in range(3):
        B.cyl("white", (-0.45, 1.5, 1.21 + i * 0.045), 0.07, 0.04, r2=0.1, segs=6)
    # stock pot with broth
    B.cyl("aluminium", (-0.35, 0.3, 1.4), 0.27, 0.44, segs=10)
    B.cyl("broth", (-0.35, 0.3, 1.625), 0.24, 0.01, segs=10)
    B.box("black", (-0.35, 0.3, 1.5), (0.62, 0.04, 0.03))
    # front sign above the cab
    B.box("sign_red", (0, -0.6, 2.24), (1.3, 0.05, 0.32))
    B.box("sign_yellow", (0, -0.63, 2.24), (1.0, 0.01, 0.12))
    for sx in (-1, 1):
        B.box("steel", (sx * 0.55, -0.6, 2.09), (0.04, 0.04, 0.06))


def cart_base(A, paint, wheel_r=0.28):
    B = A.part("body")
    B.box(paint, (0, 0, 0.72), (0.64, 1.02, 0.5))
    B.box("wood_light", (0, 0, 0.985), (0.7, 1.08, 0.03))
    B.limb("steel", (0, -0.42, 0.47), (0, -0.42, 0), 0.02, segs=4)
    B.box("steel", (0, 0.05, wheel_r), (0.7, 0.04, 0.04))
    for sx in (-1, 1):
        B.limb("wood", (sx * 0.27, 0.45, 0.9), (sx * 0.27, 0.86, 0.98), 0.02, segs=4)
        B.limb("steel", (sx * 0.27, 0.05, wheel_r), (sx * 0.27, 0.05, 0.47), 0.015, segs=4)
    B.cyl("wood", (0, 0.86, 0.98), 0.02, 0.6, axis="x", segs=5)
    A.spoked_wheel("wheel_l", (0.37, 0.05, wheel_r), wheel_r, 0.04, rim="rubber", spoke="chrome", n=3)
    A.spoked_wheel("wheel_r", (-0.37, 0.05, wheel_r), wheel_r, 0.04, rim="rubber", spoke="chrome", n=3)
    return B


@asset("gerobak_cilok", "vehicle")
def gerobak_cilok(A):
    B = cart_base(A, "cart_green")
    B.box("cart_yellow", (0, -0.515, 0.74), (0.5, 0.01, 0.22))               # name panel
    B.cyl("aluminium", (0, -0.22, 1.15), 0.21, 0.3, segs=10)                  # steamer
    B.cyl("aluminium", (0, -0.22, 1.35), 0.22, 0.1, r2=0.04, segs=10)
    B.cyl("black", (0, -0.22, 1.42), 0.025, 0.04, segs=5)
    B.box("glass", (0, 0.3, 1.1), (0.5, 0.32, 0.2))                           # small display
    B.box("aluminium", (0, 0.3, 1.205), (0.52, 0.34, 0.015))
    B.cyl("sign_red", (0.24, 0.02, 1.08), 0.035, 0.16, segs=6)                # sauces
    B.cyl("broth", (0.14, 0.02, 1.08), 0.035, 0.16, segs=6)
    B.limb("steel", (-0.26, 0.05, 1.0), (-0.26, 0.05, 2.1), 0.015, segs=4)    # umbrella
    B.cyl("red", (-0.1, 0.02, 2.1), 0.8, 0.28, r2=0.02, segs=8, smooth=False)
    B.cyl("yellow", (-0.1, 0.02, 2.19), 0.42, 0.16, r2=0.01, segs=8, smooth=False)
    B.limb("steel", (-0.26, 0.05, 2.0), (-0.1, 0.02, 2.2), 0.012, segs=4)


@asset("gerobak_bandrek", "vehicle")
def gerobak_bandrek(A):
    B = cart_base(A, "cart_blue")
    B.box("cart_yellow", (0, -0.515, 0.74), (0.5, 0.01, 0.22))
    for sx in (-1, 1):
        for sy in (-1, 1):
            B.limb("wood", (sx * 0.31, sy * 0.5, 1.0), (sx * 0.31, sy * 0.5, 1.86), 0.02, segs=4)
    B.prism("zinc", [(-0.48, 1.84), (0, 2.04), (0.48, 1.84), (0.48, 1.87), (0, 2.08), (-0.48, 1.87)],
            1.3, x=0.0, plane="xz")
    B.box("sign_yellow", (0, -0.52, 1.76), (0.66, 0.02, 0.16))
    B.box("emit_lamp", (0, 0.1, 1.76), (0.09, 0.09, 0.13))                    # lantern
    B.limb("iron_dark", (0, 0.1, 1.82), (0, 0.1, 1.98), 0.006, segs=4)
    # stove + kettle
    B.box("iron_dark", (0, -0.22, 1.05), (0.3, 0.3, 0.1))
    B.sphere("aluminium", (0, -0.22, 1.22), 0.16, segs=8, rings=5, scale=(1, 1, 0.8))
    B.limb("aluminium", (0.12, -0.22, 1.2), (0.3, -0.22, 1.33), 0.03, 0.018, segs=5)
    B.cyl("black", (0, -0.22, 1.36), 0.03, 0.04, segs=5)
    B.limb("black", (-0.1, -0.22, 1.32), (0, -0.22, 1.47), 0.01, segs=4)
    B.limb("black", (0.1, -0.22, 1.32), (0, -0.22, 1.47), 0.01, segs=4)
    # cups, jar of ginger, thermos
    for i, (x, y) in enumerate(((-0.2, 0.2), (-0.08, 0.2), (0.04, 0.2), (-0.14, 0.32), (-0.02, 0.32))):
        B.cyl("white", (x, y, 1.04), 0.035, 0.08, r2=0.045, segs=6)
    B.cyl("glass", (0.2, 0.3, 1.1), 0.07, 0.2, segs=6)
    B.cyl("red", (0.2, 0.3, 1.21), 0.075, 0.03, segs=6)
    B.cyl("red", (0.2, 0.08, 1.14), 0.06, 0.28, segs=6)
    B.cyl("white", (0.2, 0.08, 1.3), 0.045, 0.05, segs=6)


@asset("delman", "vehicle", center=True)
def delman(A):
    B = A.part("body")
    # ---- cart
    cy = 1.2
    B.box("wood", (0, cy, 0.78), (1.2, 1.5, 0.08))
    for sx in (-1, 1):
        B.box("delman_red", (sx * 0.58, cy, 1.0), (0.05, 1.5, 0.38))
        B.box("yellow", (sx * 0.61, cy, 1.0), (0.012, 1.3, 0.1))
        B.limb("wood", (sx * 0.42, 0.5, 0.82), (sx * 0.34, -1.55, 1.0), 0.03, segs=4)   # shafts
        for y in (0.5, 1.9):
            B.limb("wood_dark", (sx * 0.58, y, 1.18), (sx * 0.58, y, 2.16), 0.025, segs=4)
        B.box("emit_lamp", (sx * 0.66, 0.5, 1.3), (0.08, 0.08, 0.14))
    B.box("delman_red", (0, 0.47, 1.0), (1.2, 0.05, 0.38))
    B.box("delman_red", (0, 1.93, 0.92), (1.2, 0.05, 0.22))
    B.box("steel", (0, 2.02, 0.5), (0.5, 0.2, 0.03))                         # rear step
    B.limb("steel", (0, 1.95, 0.76), (0, 2.02, 0.5), 0.015, segs=4)
    B.box("seat_black", (0, 0.78, 1.04), (1.06, 0.4, 0.1))                   # driver bench
    B.box("seat_black", (0, 1.62, 1.04), (1.06, 0.45, 0.1))                  # passenger bench
    B.box("seat_black", (0, 1.86, 1.25), (1.06, 0.06, 0.34))
    B.tbox("delman_green", (0, cy, 2.24), (1.42, 1.8, 0.16), top=(0.8, 0.85))
    B.box("yellow", (0, cy, 2.15), (1.46, 1.84, 0.05))
    B.box("iron_dark", (0, cy, 0.6), (1.5, 0.05, 0.05))                      # axle
    B.limb("iron_dark", (0.3, cy, 0.6), (0.3, cy, 0.76), 0.02, segs=4)
    B.limb("iron_dark", (-0.3, cy, 0.6), (-0.3, cy, 0.76), 0.02, segs=4)
    A.spoked_wheel("wheel_l", (0.76, cy, 0.6), 0.6, 0.08, n=4)
    A.spoked_wheel("wheel_r", (-0.76, cy, 0.6), 0.6, 0.08, n=4)
    # ---- horse (head merged into body, legs are separate parts)
    q = quadruped(A, dict(L=1.45, W=0.54, D=0.66, Hs=1.36, leg=0.085, col="horse", hoof="hoof",
                          neck=0.62, neck_ang=52, neck_r=0.17, head=(0.2, 0.25, 0.52), pitch=55,
                          snout=(0.65, 0.6), tail=(0.75, -72, 0.06), tail_col="horse_mane"),
                  oy=-1.2, head_in_body=True)
    hc, up, fwd, n1 = q["head_c"], q["up"], q["fwd"], q["neck_top"]
    for sx in (-1, 1):                                                       # ears
        B.tbox("horse", (sx * 0.07, hc.y + 0.2, hc.z + 0.2), (0.05, 0.04, 0.12), top=(0.3, 0.5))
    B.slab("horse_mane", (-1.68, 1.22), (n1.y + 0.1, n1.z + 0.08), 0.06, 0.16)  # mane
    B.box("delman_red", (0, -1.25, 1.345), (0.4, 0.4, 0.04))                 # saddle pad
    B.cyl("iron_dark", (0, -1.72, 1.2), 0.24, 0.07, axis="y", segs=8, rot=(35, 0, 0))  # collar
    B.box("iron_dark", (0, -1.2, 1.05), (0.52, 0.06, 0.6))                   # girth


@asset("becak", "vehicle", center=True)
def becak(A):
    """Cycle rickshaw: passenger tub + canopy in FRONT between two wheels, driver behind."""
    B = A.part("body")
    r, yf, yr = 0.33, -0.5, 0.87
    # ---- passenger tub
    B.box("wood", (0, -0.82, 0.37), (0.74, 0.62, 0.04))                       # foot board
    B.slab("red", (-1.2, 0.52), (-1.12, 0.36), 0.74, 0.03)                    # toe board
    B.box("red", (0, -0.28, 0.5), (0.78, 0.56, 0.3))                          # seat box
    B.box("yellow", (0, -0.565, 0.5), (0.6, 0.012, 0.14))
    B.box("seat_black", (0, -0.3, 0.685), (0.66, 0.48, 0.07))                 # bench cushion
    B.slab("blue", (0.0, 0.62), (0.07, 1.14), 0.78, 0.04)                     # back panel
    B.slab("seat_black", (-0.04, 0.72), (0.02, 1.1), 0.66, 0.06)              # back cushion
    B.slab("yellow", (0.01, 0.75), (0.06, 1.05), 0.5, 0.012, off=-0.026)
    for sx in (-1, 1):
        B.prism("red", [(-0.82, 0.36), (0.0, 0.36), (0.07, 1.12), (-0.1, 1.12), (-0.82, 0.6)], 0.03, x=sx * 0.385)
        B.box("yellow", (sx * 0.403, -0.4, 0.5), (0.012, 0.7, 0.07))
        B.box("blue", (sx * 0.403, -0.22, 0.8), (0.012, 0.3, 0.2))
        B.box("yellow", (sx * 0.45, yf, r + 0.37), (0.1, 0.5, 0.02))          # mudguards
    B.cyl("iron_dark", (0, yf, r), 0.02, 0.9, axis="x", segs=5)               # front axle
    # ---- folding canopy over the passengers
    B.slab("blue", (-0.78, 1.5), (-0.3, 1.66), 0.86, 0.03)
    B.slab("blue", (-0.3, 1.66), (0.08, 1.62), 0.86, 0.03)
    B.slab("blue", (0.08, 1.62), (0.14, 1.12), 0.86, 0.03)
    B.box("yellow", (0, -0.78, 1.47), (0.88, 0.03, 0.08))                     # fringe
    for sx in (-1, 1):
        for y, z in ((-0.78, 1.5), (-0.3, 1.66)):
            B.limb("steel", (sx * 0.42, 0.08, 1.12), (sx * 0.42, y, z), 0.012, segs=4)
    # ---- driver: handlebar behind the seat back, bicycle frame, saddle, pedals
    B.cyl("chrome", (0, 0.24, 1.1), 0.018, 0.72, axis="x", segs=5)
    for sx in (-1, 1):
        B.limb("chrome", (sx * 0.3, 0.06, 1.0), (sx * 0.3, 0.24, 1.1), 0.014, segs=4)
        B.box("black", (sx * 0.33, 0.24, 1.1), (0.1, 0.045, 0.045))
    bb = (0, 0.42, 0.3)
    B.limb("blue", (0, 0.0, 0.42), bb, 0.025, segs=5)                         # down tube
    B.limb("blue", (0, 0.05, 0.8), (0, 0.53, 0.82), 0.025, segs=5)            # top tube
    B.limb("blue", bb, (0, 0.55, 0.94), 0.025, segs=5)                        # seat tube
    for sx in (-1, 1):
        B.limb("blue", (sx * 0.04, 0.42, 0.3), (sx * 0.06, yr, r), 0.015, segs=4)
        B.limb("blue", (sx * 0.03, 0.53, 0.82), (sx * 0.06, yr, r), 0.015, segs=4)
        B.limb("black", (sx * 0.08, 0.42, 0.3), (sx * 0.08, 0.42 + sx * 0.11, 0.3 - sx * 0.09), 0.012, segs=4)
        B.box("black", (sx * 0.13, 0.42 + sx * 0.11, 0.3 - sx * 0.09), (0.1, 0.07, 0.02))
    B.cyl("chrome", (-0.055, 0.42, 0.3), 0.09, 0.012, axis="x", segs=8)       # chainring
    B.tbox("seat_black", (0, 0.6, 0.97), (0.22, 0.3, 0.07), top=(0.8, 0.9))   # saddle
    B.box("yellow", (0, yr + 0.02, r * 2 + 0.03), (0.07, 0.5, 0.02))          # rear mudguard
    A.spoked_wheel("wheel_fl", (0.45, yf, r), r, 0.05, rim="rubber", spoke="chrome", n=4)
    A.spoked_wheel("wheel_fr", (-0.45, yf, r), r, 0.05, rim="rubber", spoke="chrome", n=4)
    A.spoked_wheel("wheel_r", (0, yr, r), r, 0.05, rim="rubber", spoke="chrome", n=4)


@asset("bemo", "vehicle", center=True)
def bemo(A):
    """Three-wheeled microbus: rounded cab over ONE front wheel, open-backed passenger box."""
    B = A.part("body")
    W, r = 1.3, 0.24
    paint = "bemo_blue"
    yf, yr = -0.98, 0.75
    B.box(paint, (0, 0.375, 0.46), (W, 1.95, 0.32))                           # tub  y -0.6..1.35
    B.box(paint, (0, -0.31, 0.785), (W, 0.58, 0.33))                          # cab lower half
    B.prism(paint, [(-0.65, -0.6), (-0.62, -1.2), (-0.45, -1.4), (0.45, -1.4), (0.62, -1.2), (0.65, -0.6)],
            0.5, x=0.7, plane="xy")                                           # nose above the wheel
    greenhouse(B, "white", (-1.17, 0.95), (-0.9, 1.6), (-0.04, 1.6), (-0.02, 0.95), 1.2, 1.08)
    # passenger box: low sides, side benches, roof on posts, open back
    for sx in (-1, 1):
        B.box(paint, (sx * (W / 2 - 0.02), 0.665, 0.885), (0.04, 1.37, 0.53))
        B.box("white", (sx * (W / 2 + 0.002), 0.375, 0.9), (0.01, 1.95, 0.06))
        for y in (0.66, 1.32):
            B.limb("white", (sx * 0.62, y, 1.15), (sx * 0.59, y, 1.62), 0.03, segs=4)
        B.box("seat_black", (sx * 0.44, 0.7, 0.73), (0.34, 1.25, 0.22))
        B.box("seat_black", (sx * 0.585, 0.7, 1.0), (0.05, 1.25, 0.3))
        B.box("emit_taillight", (sx * 0.57, 1.355, 0.5), (0.1, 0.02, 0.12))
        B.box("emit_headlight", (sx * 0.27, -1.405, 0.8), (0.16, 0.03, 0.14))
        B.box("black", (sx * (W / 2 + 0.003), -0.04, 0.72), (0.01, 0.015, 0.44))    # door seam
        B.box("black", (sx * (W / 2 + 0.005), -0.2, 0.86), (0.02, 0.12, 0.03))      # handle
        B.limb("black", (sx * 0.085, yf, r), (sx * 0.085, yf, 0.5), 0.02, segs=4)   # fork
    B.box("interior_dark", (0, 0.665, 0.625), (W - 0.08, 1.37, 0.012))
    B.box("white", (0, 0.665, 1.65), (1.26, 1.46, 0.06))                      # rear roof
    B.box("black", (0, -1.405, 0.8), (0.3, 0.03, 0.1))                        # grille
    B.box("bumper", (0, -1.42, 0.52), (0.84, 0.06, 0.09))
    B.box("white", (0, -1.455, 0.52), (0.3, 0.01, 0.07))
    B.box("bumper", (0, 1.38, 0.34), (0.9, 0.14, 0.05))                       # rear step
    mirrors(B, 1.2, -1.0, 1.12)
    B.cyl("black", (0, yr, r + 0.07), r + 0.06, W + 0.012, axis="x", segs=10)
    A.wheel("wheel_f", (0, yf, r), r, 0.12)
    A.wheel("wheel_rl", (W / 2 - 0.05, yr, r), r, 0.12)
    A.wheel("wheel_rr", (-W / 2 + 0.05, yr, r), r, 0.12)


@asset("truk", "vehicle", center=True)
def truk(A):
    """Light two-axle truck: yellow cab, green wooden bed, tarpaulin-covered load."""
    B = A.part("body")
    W, r = 1.9, 0.4
    yf, yr = -1.78, 1.45
    cab, bed = "truck_yellow", "truck_green"
    B.prism(cab, [(-2.55, 0.5), (-2.55, 1.22), (-2.45, 1.38), (-1.12, 1.38), (-1.12, 0.5)], W)
    greenhouse(B, cab, (-2.43, 1.38), (-2.23, 2.12), (-1.22, 2.12), (-1.14, 1.38), W - 0.02, W - 0.2)
    lights(B, W, -2.55, 2.55, z=0.92, zr=0.86)
    mirrors(B, W, -2.25, 1.65)
    B.box(cab, (0, -2.2, 2.19), (W - 0.3, 0.3, 0.05), rot=(-12, 0, 0))        # sun visor
    for sx in (-1, 1):
        B.box("black", (sx * (W / 2 + 0.003), -1.3, 0.95), (0.01, 0.015, 0.8))
        B.box("black", (sx * (W / 2 + 0.005), -1.45, 1.25), (0.02, 0.14, 0.03))
    # chassis + bed
    B.box("bumper", (0, 0.725, 0.72), (0.9, 3.65, 0.22))
    B.box("bumper", (0, 2.5, 0.85), (W, 0.1, 0.2))
    B.box("wood", (0, 0.755, 0.9), (W, 3.59, 0.1))                            # bed floor
    for sx in (-1, 1):
        B.box(bed, (sx * (W / 2 - 0.025), 0.755, 1.28), (0.05, 3.59, 0.66))
        for z in (1.17, 1.39):                                                # plank gaps
            B.box("wood_dark", (sx * (W / 2 + 0.002), 0.755, z), (0.01, 3.59, 0.02))
        for y in (-0.9, -0.05, 0.8, 1.65, 2.45):                              # stakes
            B.box("wood_dark", (sx * (W / 2 + 0.012), y, 1.27), (0.03, 0.08, 0.7))
        B.box("black", (sx * (W / 2 - 0.16), yr, 0.835), (0.32, 1.0, 0.03))   # mudguard
        B.box("black", (sx * (W / 2 - 0.17), yr + 0.5, 0.68), (0.3, 0.02, 0.3))
    B.box(bed, (0, 2.525, 1.28), (W, 0.05, 0.66))                             # tailgate
    B.box(bed, (0, -1.015, 1.45), (W, 0.05, 1.0))                             # headboard
    B.tbox("tarp_blue", (0, 0.77, 1.98), (W - 0.12, 3.35, 0.78), top=(0.8, 0.9))   # covered load
    for y in (-0.3, 0.77, 1.84):                                              # ropes
        B.box("black", (0, y, 2.372), (1.44, 0.03, 0.015))
        for sx in (-1, 1):
            B.limb("black", (sx * 0.715, y, 2.37), (sx * 0.895, y, 1.6), 0.012, segs=4)
    B.cyl("steel", (0.62, -0.3, 0.64), 0.16, 0.6, axis="y", segs=8)           # fuel tank
    B.cyl("iron_dark", (0, yr, r), 0.06, W - 0.3, axis="x", segs=6)           # rear axle
    B.cyl("black", (0, yf, r + 0.09), r + 0.08, W + 0.012, axis="x", segs=10)
    xf, xr = W / 2 - 0.13 + 0.01, W / 2 - 0.16 + 0.01
    A.wheel("wheel_fl", (xf, yf, r), r, 0.26)
    A.wheel("wheel_fr", (-xf, yf, r), r, 0.26)
    A.wheel("wheel_rl", (xr, yr, r), r, 0.32)
    A.wheel("wheel_rr", (-xr, yr, r), r, 0.32)


@asset("sedan", "vehicle", center=True)
def sedan(A):
    """Taxi sedan with a roof light box."""
    B = A.part("body")
    W = 1.7
    paint = "taxi_blue"
    B.prism(paint, [(-2.12, 0.28), (-2.12, 0.6), (-2.02, 0.72), (-0.95, 0.86), (1.35, 0.88),
                    (2.06, 0.84), (2.12, 0.72), (2.12, 0.28)], W)
    greenhouse(B, paint, (-0.92, 0.86), (-0.3, 1.3), (0.8, 1.3), (1.38, 0.88),
               W - 0.02, W - 0.3, pillars=(0.25,))
    lights(B, W, -2.12, 2.12, z=0.52, zr=0.6)
    mirrors(B, W, -0.75, 0.95)
    B.box("black", (0, 0, 0.33), (W + 0.02, 4.1, 0.08))                       # sill
    for sx in (-1, 1):
        for y in (-0.9, 0.22, 1.25):
            B.box("black", (sx * (W / 2 + 0.003), y, 0.6), (0.01, 0.015, 0.5))
        for y in (-0.05, 1.0):
            B.box("black", (sx * (W / 2 + 0.005), y, 0.78), (0.02, 0.14, 0.03))
        B.box("white", (sx * (W / 2 + 0.004), -0.35, 0.6), (0.01, 0.42, 0.2))  # door emblem
    B.box("black", (0, 0.2, 1.36), (0.56, 0.22, 0.02))                        # taxi light box
    B.box("emit_sign", (0, 0.2, 1.41), (0.46, 0.16, 0.09))
    wheels4(A, W, -1.35, 1.32, r=0.3, w=0.2, arch=0.09)
