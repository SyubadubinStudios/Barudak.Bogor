"""Barudak Bogor - street furniture of a busy Indonesian road: traffic lights, CCTV masts,
cloth banners, shop light boxes and the big LED screen at a junction. Front faces -Y.

The lit parts are drawn by the game: the lamps of the traffic light (so they can change), the
words on the banner and the light box (name boards), the picture on the screen."""
from common import asset


@asset("lampu_merah", "prop")
def lampu_merah(A):
    """Kerb-side signal: a striped post and one head. Lamp centres at 3.55 / 3.2 / 2.85 m, 0.2 m in front."""
    B = A.part("body")
    B.cyl("concrete", (0, 0, 0.1), 0.2, 0.2, segs=8)
    for i in range(6):                                                    # black and yellow post
        B.limb("sign_yellow" if i % 2 else "black", (0, 0, 0.2 + i * 0.4), (0, 0, 0.6 + i * 0.4), 0.07, 0.07, segs=6)
    B.limb("steel", (0, 0, 2.6), (0, 0, 4.0), 0.055, 0.05, segs=6)
    B.box("iron_dark", (0, -0.1, 3.2), (0.38, 0.22, 1.16))               # the head
    B.box("black", (0, -0.1, 3.2), (0.5, 0.03, 1.3))                     # backing plate
    for z in (3.55, 3.2, 2.85):
        B.cyl("window_dark", (0, -0.215, z), 0.12, 0.02, axis="y", segs=10)
        B.box("iron_dark", (0, -0.29, z + 0.15), (0.3, 0.18, 0.025), rot=(-12, 0, 0))    # visor
    B.box("sign_blue", (0, -0.04, 2.3), (0.5, 0.02, 0.3))                # "belok kiri langsung"
    B.box("white", (0, -0.052, 2.3), (0.4, 0.005, 0.18))


@asset("cctv", "prop")
def cctv(A):
    B = A.part("body")
    B.box("concrete", (0, 0, 0.15), (0.4, 0.4, 0.3))
    B.limb("steel", (0, 0, 0.3), (0, 0, 6.2), 0.085, 0.06, segs=6)
    B.limb("steel", (0, 0, 5.9), (0, -1.5, 6.1), 0.045, 0.04, segs=5)    # arm over the kerb
    B.box("white", (0.0, 0.14, 4.3), (0.34, 0.2, 0.5))                   # control cabinet
    B.box("steel", (0.0, 0.03, 4.3), (0.26, 0.02, 0.4))
    for x, y, yaw in ((0.0, -1.45, 0), (0.18, -0.6, -35), (-0.18, -0.6, 35)):
        B.box("white", (x, y - 0.12, 5.92), (0.14, 0.42, 0.14), rot=(18, 0, yaw))       # camera housing
        B.box("white", (x, y - 0.14, 6.0), (0.18, 0.5, 0.02), rot=(18, 0, yaw))         # sun shield
        B.cyl("window_dark", (x, y - 0.32, 5.86), 0.05, 0.03, axis="y", segs=8, rot=(18, 0, yaw))
    B.dome("window_dark", (0, -1.0, 5.93), 0.12, segs=8, rings=3, scale=(1, 1, -1))     # dome camera
    B.cyl("white", (0, -1.0, 5.98), 0.13, 0.08, segs=8)
    B.box("sign_blue", (0, -0.09, 3.0), (0.5, 0.02, 0.34))               # "kawasan ber-CCTV"
    B.box("white", (0, -0.102, 3.0), (0.38, 0.005, 0.2))


@asset("spanduk", "prop")
def spanduk(A):
    """A cloth banner between two bamboo poles. The words go on the plain band, 1.9 m up, 4 m wide."""
    B = A.part("body")
    for sx in (-1, 1):
        B.limb("bamboo_dry", (sx * 2.25, 0, 0), (sx * 2.25, 0, 3.0), 0.045, 0.035, segs=5)
        B.limb("bamboo_dry", (sx * 2.25, 0.5, 0), (sx * 2.25, 0, 1.6), 0.03, 0.025, segs=4)   # prop
        for z in (1.5, 2.3):
            B.limb("white", (sx * 2.25, 0, z), (sx * 2.1, 0, z), 0.008, segs=3)               # ties
    B.box("cloth_white", (0, 0, 1.9), (4.2, 0.02, 0.92))
    B.box("cloth_red", (0, -0.012, 2.32), (4.2, 0.005, 0.08))
    B.box("cloth_red", (0, -0.012, 1.48), (4.2, 0.005, 0.08))
    for sx in (-1, 1):                                                    # wind slits, as on every real one
        B.box("window_dark", (sx * 1.2, -0.013, 2.33), (0.12, 0.004, 0.03))


@asset("light_box", "prop")
def light_box(A):
    """A shop's light box on its own post. The words go on the face, 3.05 m up, 1.5 m wide."""
    B = A.part("body")
    B.cyl("concrete", (0, 0, 0.06), 0.16, 0.12, segs=8)
    B.limb("iron_dark", (0, 0, 0.12), (0, 0, 2.7), 0.045, 0.04, segs=6)
    B.box("iron_dark", (0, 0, 3.05), (1.72, 0.24, 0.62))
    for sy in (-1, 1):
        B.box("emit_sign", (0, sy * 0.122, 3.05), (1.62, 0.01, 0.52))
    B.box("sign_red", (0, 0, 2.68), (1.72, 0.2, 0.1))


@asset("videotron", "building")
def videotron(A):
    """LED screen on a single column. The screen is the one bare quad, 6 x 3.4 m, centre 7.9 m up."""
    B = A.part("body")
    B.box("concrete", (0, 0.3, 0.25), (1.4, 1.4, 0.5))
    B.limb("steel", (0, 0.3, 0.5), (0, 0.3, 6.1), 0.36, 0.3, segs=8)
    B.box("steel", (0, 0.25, 6.15), (3.0, 0.5, 0.3))
    B.box("iron_dark", (0, 0.12, 7.9), (6.4, 0.5, 3.8))
    B.uvquad("billboard_face", (-3.0, -0.135, 6.2), (3.0, -0.135, 6.2), (3.0, -0.135, 9.6), (-3.0, -0.135, 9.6))
    for x in (-2.4, -0.8, 0.8, 2.4):                                     # cooling louvres on the back
        B.box("steel", (x, 0.375, 7.9), (1.1, 0.02, 3.0))
    B.box("steel", (0, -0.5, 6.02), (6.4, 0.7, 0.05))                    # service catwalk
    B.box("steel", (0, -0.84, 6.5), (6.4, 0.03, 0.03))
    for x in (-3.15, 3.15):
        B.limb("steel", (x, -0.84, 6.05), (x, -0.84, 6.5), 0.015, segs=4)
