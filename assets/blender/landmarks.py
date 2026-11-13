"""Barudak Bogor - the buildings people know Bogor by. Front faces -Y.

Each one fills the plot the game reserves for it (width along X, depth along Y, origin at the
centre of the footprint) and stands on a plinth that reaches below the ground, so a plot that
is not quite level leaves no gap. The name board is painted on by the game: every model keeps
a plain band free for it (see RealPlaces in WorldGenerator.cs for its height).
"""
import math
from common import asset


def plinth(B, w, d, top=0.6, mat="stone"):
    B.box(mat, (0, 0, (top - 1.6) / 2), (w, d, top + 1.6))


def colonnade(B, xs, y, z0, h, r=0.42, mat="concrete_white"):
    for x in xs:
        B.box(mat, (x, y, z0 + 0.15), (r * 2.7, r * 2.7, 0.3))
        B.cyl(mat, (x, y, z0 + 0.3 + (h - 0.6) / 2), r, h - 0.6, segs=6, r2=r * 0.86)
        B.box(mat, (x, y, z0 + h - 0.15), (r * 2.7, r * 2.7, 0.3))


def point(B, mat, x, y, z, w, h, side=False):
    """The pointed head of a gothic or moorish opening: a triangle w wide and h high whose
    base is at z. On a wall facing -Y, or (side) on a wall facing X."""
    if side:
        B.prism(mat, [(y - w / 2, z), (y + w / 2, z), (y, z + h)], 0.1, x=x, plane="yz")
    else:
        B.prism(mat, [(x - w / 2, z), (x + w / 2, z), (x, z + h)], 0.1, x=y, plane="xz")


def spread(n, half):
    return [(-half + 2 * half * i / (n - 1)) for i in range(n)]


def windows(B, xs, y, z, w, h, frame="concrete", glass="window_dark", depth=0.08):
    """A row of windows on a wall facing -Y (y is the wall face)."""
    for x in xs:
        B.box(frame, (x, y - depth / 2, z), (w + 0.3, depth, h + 0.3))
        B.box(glass, (x, y - depth, z), (w, depth, h))


def side_windows(B, x, ys, z, w, h, glass="window_dark"):
    for sx in (-1, 1):
        for y in ys:
            B.box(glass, (sx * x, y, z), (0.1, w, h))


def hip_roof(B, mat, c, size, top=(0.45, 0.08), eave=0.18):
    x, y, z = c
    B.box(mat, (x, y, z + eave / 2), (size[0], size[1], eave))
    B.tbox(mat, (x, y, z + eave + size[2] / 2), (size[0] - 0.3, size[1] - 0.3, size[2]), top=top)


def steps(B, y, w, n=4, rise=0.15, run=0.45, mat="stone"):
    """Stairs climbing toward +Y, their top edge at y."""
    for i in range(n):
        B.box(mat, (0, y - run * (i + 0.5), rise * (n - i) / 2), (w, run, rise * (n - i)))


# =============================================================================
@asset("istana_bogor", "landmark", budget=3600)
def istana_bogor(A):
    """Istana Bogor: a white palace with a columned porch, a cupola and two wings. 64 x 20 m."""
    B = A.part("body")
    plinth(B, 64, 20)
    # main block, galleries, wings
    B.box("concrete_white", (0, 1, 5.1), (26, 14, 9.0))
    B.box("concrete", (0, 1, 9.75), (26.6, 14.6, 0.3))                       # cornice
    hip_roof(B, "roof_grey", (0, 1, 9.9), (27.2, 15.2, 2.0), top=(0.5, 0.12))
    for sx in (-1, 1):
        B.box("concrete_white", (sx * 16.5, 2, 2.85), (7.2, 8, 4.5))        # gallery
        B.box("concrete", (sx * 16.5, 2, 5.2), (7.4, 8.4, 0.2))
        B.box("concrete_white", (sx * 26, 1.5, 3.85), (12, 12, 6.5))        # wing
        B.box("concrete", (sx * 26, 1.5, 7.2), (12.5, 12.5, 0.25))
        hip_roof(B, "roof_grey", (sx * 26, 1.5, 7.3), (13, 13, 1.6), top=(0.4, 0.1))
        for x in (-2.2, 0, 2.2):                                              # gallery arches
            B.box("window_dark", (sx * 16.5 + x, -2.03, 2.6), (1.3, 0.08, 2.9))
            B.cyl("window_dark", (sx * 16.5 + x, -2.03, 4.05), 0.65, 0.08, axis="y", segs=8, smooth=False)
        windows(B, [sx * 26 + x for x in (-3.6, 0, 3.6)], -4.5, 3.6, 1.5, 3.0)
        side_windows(B, 32.0, (-1.5, 2.5), 3.6, 1.5, 3.0)
        B.box("door_wood", (sx * 26, -4.56, 2.0), (1.5, 0.1, 2.6))
    windows(B, [-10.8, 10.8], -6, 3.2, 1.5, 3.0)
    windows(B, [-10.8, 10.8], -6, 7.3, 1.5, 1.9)
    side_windows(B, 13.0, (-2.5, 1, 4.5), 3.2, 1.5, 3.0)
    for x in (-5.4, -1.8, 1.8, 5.4):                                          # tall doors behind the porch
        B.box("window_dark", (x, -6.05, 3.4), (1.7, 0.1, 4.6))
    # porch: six columns under a pediment
    B.box("stone", (0, -8, 0.75), (18, 4, 0.3))
    steps(B, -10, 12, n=3, rise=0.3)
    colonnade(B, spread(6, 7.6), -9.3, 0.9, 7.1)
    B.box("concrete_white", (0, -8, 8.5), (17.6, 4, 1.0))                     # frieze: the name goes here
    B.box("concrete", (0, -8, 9.1), (18.2, 4.4, 0.2))
    B.prism("concrete_white", [(-9.1, 9.2), (9.1, 9.2), (0, 11.6)], 4.2, x=-8.0, plane="xz")
    B.prism("concrete", [(-6.4, 9.5), (6.4, 9.5), (0, 11.15)], 0.1, x=-10.12, plane="xz")
    # cupola
    B.cyl("concrete_white", (0, 1, 12.6), 3.4, 1.6, segs=8)
    B.cyl("concrete", (0, 1, 13.5), 3.7, 0.2, segs=8)
    for i in range(8):
        a = math.pi * 2 * (i + 0.5) / 8
        B.box("window_dark", (3.3 * math.cos(a), 1 + 3.3 * math.sin(a), 12.6), (0.9, 0.3, 1.0), rot=(0, 0, math.degrees(a) + 90))
    B.dome("roof_grey", (0, 1, 13.6), 3.3, segs=8, rings=6, scale=(1, 1, 0.8))
    B.cyl("concrete_white", (0, 1, 16.6), 0.6, 0.9, segs=6)
    B.cyl("gold", (0, 1, 17.6), 0.3, 1.2, r2=0.0, segs=6)
    # the flag
    B.limb("steel", (0, -9.6, 11.6), (0, -9.6, 15.6), 0.05, segs=4)
    B.box("red", (0.8, -9.6, 15.3), (1.6, 0.03, 0.5))
    B.box("white", (0.8, -9.6, 14.8), (1.6, 0.03, 0.5))


@asset("museum_bogor", "landmark", budget=2400)
def museum_bogor(A):
    """A Dutch-era hall with a four column porch, used for the museums. 36 x 16 m."""
    B = A.part("body")
    plinth(B, 36, 16, top=0.5)
    B.box("concrete_white", (0, 2, 3.9), (35, 11.4, 6.8))
    B.box("concrete", (0, 2, 7.4), (35.5, 11.9, 0.22))
    hip_roof(B, "roof_tile", (0, 2, 7.5), (36, 12.4, 2.6), top=(0.62, 0.06))
    B.box("stone", (0, 2, 1.0), (35.1, 11.5, 1.0))                            # dado
    xs = [-14.5, -11.2, -7.9, 7.9, 11.2, 14.5]
    windows(B, xs, -3.7, 4.3, 1.5, 3.0)
    side_windows(B, 17.5, (-0.5, 4.5), 4.3, 1.5, 3.0)
    windows(B, [-11.2, -3.7, 3.7, 11.2], 7.75, 4.3, 1.5, 3.0)
    # porch
    B.box("concrete_white", (0, -5.3, 3.9), (14, 3.3, 6.8))
    B.box("door_wood", (0, -6.98, 2.6), (2.4, 0.1, 4.0))
    B.box("window_dark", (0, -6.98, 5.2), (2.4, 0.1, 1.0))
    windows(B, [-4.6, 4.6], -6.95, 4.0, 1.3, 3.0)
    B.box("stone", (0, -7.5, 0.4), (15, 1.0, 0.8))
    colonnade(B, [-6.1, -2.4, 2.4, 6.1], -7.45, 0.8, 5.5, r=0.38)
    B.box("concrete_white", (0, -6.4, 6.8), (15, 3.2, 1.0))                   # frieze
    B.box("concrete", (0, -6.4, 7.4), (15.5, 3.6, 0.2))
    B.prism("concrete_white", [(-7.75, 7.5), (7.75, 7.5), (0, 9.7)], 3.4, x=-6.3, plane="xz")
    B.cyl("window_dark", (0, -8.02, 8.35), 0.55, 0.08, axis="y", segs=8, smooth=False)
    steps(B, -8.0, 9, n=3, rise=0.26)


@asset("balai_kota", "landmark", budget=2800)
def balai_kota(A):
    """Balai Kota Bogor: the old town hall, long and low with a porch and a clock turret. 44 x 18 m."""
    B = A.part("body")
    plinth(B, 44, 18, top=0.5)
    B.box("concrete_white", (0, 2.5, 4.0), (43, 12, 7.0))
    B.box("concrete", (0, 2.5, 7.6), (43.5, 12.5, 0.22))
    hip_roof(B, "roof_tile", (0, 2.5, 7.7), (44, 13, 2.8), top=(0.7, 0.06))
    B.box("wall_cream", (0, 2.5, 1.0), (43.1, 12.1, 1.0))
    xs = [-19.5, -16.4, -13.3, -10.2, 10.2, 13.3, 16.4, 19.5]
    windows(B, xs, -3.5, 4.4, 1.4, 3.2)
    side_windows(B, 21.5, (0, 5), 4.4, 1.5, 3.2)
    windows(B, [-15, -5, 5, 15], 8.5, 4.4, 1.5, 3.2)
    for x in xs:                                                              # shutters
        for s in (-1, 1):
            B.box("wall_green", (x + s * 1.05, -3.62, 4.4), (0.55, 0.06, 3.2))
    # middle pavilion and porch
    B.box("concrete_white", (0, -4.6, 4.9), (17, 4.0, 8.8))
    B.box("concrete", (0, -4.6, 9.4), (17.5, 4.5, 0.25))
    hip_roof(B, "roof_tile", (0, -3.0, 9.5), (18, 8.0, 2.4), top=(0.5, 0.08))
    B.box("door_wood", (0, -6.65, 2.6), (2.6, 0.1, 4.2))
    windows(B, [-5.6, 5.6], -6.6, 4.0, 1.5, 3.2)
    B.box("stone", (0, -7.8, 0.4), (19, 2.2, 0.8))
    colonnade(B, spread(6, 8.4), -8.1, 0.8, 6.0, r=0.36)
    B.box("concrete_white", (0, -7.6, 7.3), (18.4, 2.4, 1.0))                 # frieze
    B.box("concrete", (0, -7.6, 7.9), (18.9, 2.8, 0.2))
    steps(B, -8.9, 10, n=3, rise=0.26)
    # clock turret
    B.box("concrete_white", (0, -3.0, 12.8), (3.0, 3.0, 2.6))
    B.cyl("white", (0, -4.52, 13.0), 0.9, 0.06, axis="y", segs=10, smooth=False)
    B.box("black", (0, -4.57, 13.25), (0.08, 0.04, 0.6))
    B.box("black", (0.2, -4.57, 13.0), (0.45, 0.04, 0.08))
    B.cyl("roof_tile", (0, -3.0, 15.0), 2.4, 1.8, r2=0.0, segs=4, rot=(0, 0, 45), smooth=False)
    B.limb("steel", (0, -3.0, 15.8), (0, -3.0, 18.6), 0.04, segs=4)
    B.box("red", (0.7, -3.0, 18.35), (1.4, 0.03, 0.45))
    B.box("white", (0.7, -3.0, 17.9), (1.4, 0.03, 0.45))


@asset("katedral_bogor", "landmark", budget=2600)
def katedral_bogor(A):
    """Katedral Santa Perawan Maria: a neo-gothic church, tower and spire over the door. 18 x 36 m."""
    B = A.part("body")
    plinth(B, 18, 36, top=0.5)
    # aisles, nave, apse
    B.box("wall_cream", (0, 2.5, 3.5), (17, 27, 6.0))
    B.box("roof_dark", (0, 2.5, 6.6), (17.5, 27.5, 0.2))
    for sx in (-1, 1):
        B.xslab("roof_dark", (sx * 8.75, 6.6), (sx * 5.6, 8.4), 27.4, 0.18, y=2.5)
    B.box("wall_cream", (0, 2.5, 8.0), (11.2, 27, 9.0))
    B.prism("roof_dark", [(-6.1, 12.4), (6.1, 12.4), (0, 17.6)], 27.6, x=2.5, plane="xz")
    B.cyl("wall_cream", (0, 16, 5.5), 5.6, 10.0, segs=8)
    B.cyl("roof_dark", (0, 16, 12.2), 5.9, 3.4, r2=0.0, segs=8)
    # pointed windows down both sides
    for sx in (-1, 1):
        for y in (-7.5, -3, 1.5, 6, 10.5):
            B.box("window_dark", (sx * 8.52, y, 3.5), (0.08, 1.2, 3.0))
            point(B, "window_dark", sx * 8.52, y, 5.0, 1.2, 1.0, side=True)
            B.box("wall_cream", (sx * 8.8, y + 2.25, 3.4), (0.7, 0.7, 6.4))   # buttress
            B.box("window_dark", (sx * 5.62, y, 10.2), (0.08, 1.0, 2.0))
    # west front
    B.box("wall_cream", (0, -12.2, 7.0), (12, 2.4, 13.0))
    B.prism("wall_cream", [(-6, 13.5), (6, 13.5), (0, 18.2)], 2.4, x=-12.2, plane="xz")
    for sx in (-1, 1):
        B.box("wall_cream", (sx * 6.3, -12.8, 7.6), (1.3, 1.3, 14.2))
        B.cyl("roof_dark", (sx * 6.3, -12.8, 16.0), 0.95, 2.6, r2=0.0, segs=4, rot=(0, 0, 45), smooth=False)
        B.box("window_dark", (sx * 3.9, -13.42, 4.0), (1.1, 0.08, 3.4))
        point(B, "window_dark", sx * 3.9, -13.42, 5.7, 1.1, 1.0)
    # tower
    B.box("wall_cream", (0, -14.6, 11.2), (6.0, 6.0, 21.4))
    B.box("concrete", (0, -14.6, 16.0), (6.4, 6.4, 0.3))
    B.box("concrete", (0, -14.6, 21.9), (6.5, 6.5, 0.3))
    B.box("door_wood", (0, -17.63, 2.5), (2.6, 0.1, 4.0))
    point(B, "door_wood", 0, -17.63, 4.5, 2.6, 1.9)
    B.cyl("window_dark", (0, -17.63, 13.4), 1.5, 0.1, axis="y", segs=10, smooth=False)     # rose window
    B.cyl("gold", (0, -17.66, 13.4), 0.4, 0.1, axis="y", segs=6, smooth=False)
    for a in range(4):                                                        # belfry openings on four sides
        ang = math.pi / 2 * a
        dx, dy = math.sin(ang), -math.cos(ang)
        B.box("window_dark", (dx * 3.02, -14.6 + dy * 3.02, 19.0), (1.6 if dx == 0 else 0.08, 0.08 if dx == 0 else 1.6, 3.6))
    B.cyl("roof_dark", (0, -14.6, 27.6), 4.3, 11.0, r2=0.0, segs=4, rot=(0, 0, 45), smooth=False)
    for sx in (-1, 1):
        for sy in (-1, 1):
            B.cyl("roof_dark", (sx * 2.7, -14.6 + sy * 2.7, 23.3), 0.5, 2.4, r2=0.0, segs=4, rot=(0, 0, 45), smooth=False)
    B.box("gold", (0, -14.6, 34.0), (0.16, 0.16, 2.0))
    B.box("gold", (0, -14.6, 34.3), (1.0, 0.16, 0.16))
    steps(B, -18.0, 6, n=3, rise=0.17)


@asset("masjid_raya", "landmark", budget=3000)
def masjid_raya(A):
    """Masjid Raya Bogor: a wide prayer hall under a great dome, with a minaret. 34 x 30 m."""
    B = A.part("body")
    plinth(B, 34, 30, top=0.6)
    B.box("concrete_white", (1.5, 1.5, 5.1), (27, 25, 9.0))
    B.box("dome_green", (1.5, 1.5, 9.75), (27.6, 25.6, 0.5))                  # parapet band
    B.box("concrete_white", (1.5, 1.5, 10.2), (27.2, 25.2, 0.4))
    # arcades: pointed arches all round
    for x in (-8.5, -4.5, 7.5, 11.5):
        B.box("window_dark", (x, -11.05, 3.6), (2.4, 0.1, 5.2))
        point(B, "window_dark", x, -11.05, 6.2, 2.4, 1.7)
    for sx, x in ((-1, -12.05), (1, 15.05)):
        for y in (-6, -1, 4, 9):
            B.box("window_dark", (x, y, 3.6), (0.1, 2.4, 5.2))
            point(B, "window_dark", x, y, 6.2, 2.4, 1.7, side=True)
    # the gate
    B.box("concrete_white", (1.5, -12.2, 6.0), (9.5, 3.0, 11.0))
    B.box("dome_green", (1.5, -12.2, 11.7), (10, 3.4, 0.4))
    B.box("window_dark", (1.5, -13.72, 3.4), (4.6, 0.1, 5.6))
    point(B, "window_dark", 1.5, -13.72, 6.2, 4.6, 3.0)
    B.box("door_wood", (1.5, -13.6, 2.3), (3.0, 0.1, 3.4))
    for sx in (-1, 1):
        B.cyl("dome_green", (1.5 + sx * 4.4, -13.3, 12.6), 0.7, 1.4, segs=6)
        B.dome("gold", (1.5 + sx * 4.4, -13.3, 13.3), 0.8, segs=6, rings=4)
    steps(B, -13.9, 8, n=3, rise=0.2)
    # the dome
    B.cyl("concrete_white", (1.5, 1.5, 11.6), 8.2, 2.4, segs=12)
    for i in range(12):
        a = math.pi * 2 * (i + 0.5) / 12
        B.box("window_dark", (1.5 + 8.05 * math.cos(a), 1.5 + 8.05 * math.sin(a), 11.6), (1.5, 0.3, 1.3), rot=(0, 0, math.degrees(a) + 90))
    B.cyl("dome_green", (1.5, 1.5, 12.95), 8.5, 0.3, segs=12)
    B.dome("dome_green", (1.5, 1.5, 13.1), 8.0, segs=12, rings=8, scale=(1, 1, 0.95))
    B.cyl("gold", (1.5, 1.5, 21.2), 0.35, 1.2, segs=6)
    B.sphere("gold", (1.5, 1.5, 22.2), 0.6, segs=6, rings=4)
    B.cyl("gold", (1.5, 1.5, 23.2), 0.2, 1.4, r2=0.0, segs=6)
    for sx in (-1, 1):
        for sy in (-1, 1):
            c = (1.5 + sx * 11.4, 1.5 + sy * 10.4)
            B.cyl("concrete_white", (c[0], c[1], 11.0), 1.7, 1.2, segs=8)
            B.dome("dome_green", (c[0], c[1], 11.6), 1.7, segs=8, rings=6)
    # the minaret stands clear of the hall, at the left of the front
    mx, my = -14.6, -12.4
    B.box("concrete_white", (mx, my, 2.6), (3.6, 3.6, 4.0))
    B.cyl("concrete_white", (mx, my, 14.0), 1.5, 19.0, r2=1.25, segs=8)
    for z in (17.5, 23.5):
        B.cyl("dome_green", (mx, my, z), 2.2, 0.35, segs=8)
        B.cyl("concrete_white", (mx, my, z + 0.55), 2.1, 0.7, segs=8, caps=False)
    B.cyl("concrete_white", (mx, my, 26.0), 1.05, 4.0, segs=8)
    for a in range(4):
        ang = math.pi / 2 * a
        B.box("window_dark", (mx + 1.0 * math.sin(ang), my - 1.0 * math.cos(ang), 26.2), (0.7 if a % 2 == 0 else 0.12, 0.12 if a % 2 == 0 else 0.7, 2.2))
    B.cyl("dome_green", (mx, my, 28.1), 1.4, 0.25, segs=8)
    B.dome("dome_green", (mx, my, 28.2), 1.25, segs=8, rings=6, scale=(1, 1, 1.5))
    B.cyl("gold", (mx, my, 30.6), 0.12, 1.6, r2=0.0, segs=5)


@asset("lawang_suryakencana", "landmark", budget=2200)
def lawang_suryakencana(A):
    """Lawang Suryakencana: the gate of the old Chinese quarter, spanning the street. The opening is 11.6 m wide and 6 m high."""
    B = A.part("body")
    half = 6.5
    for sx in (-1, 1):
        x = sx * half
        B.box("stone", (x, 0, 0.1), (2.2, 2.2, 1.8))
        B.cyl("red", (x, 0, 4.2), 0.55, 6.6, segs=8)
        B.cyl("gold", (x, 0, 1.15), 0.68, 0.3, segs=8)
        B.cyl("gold", (x, 0, 7.3), 0.68, 0.3, segs=8)
        # the outer post and its little roof
        ox = sx * (half + 2.6)
        B.box("stone", (ox, 0, 0.1), (1.4, 1.4, 1.4))
        B.cyl("red", (ox, 0, 3.2), 0.36, 5.0, segs=6)
        B.box("red", (sx * (half + 1.3), 0, 5.3), (3.2, 0.5, 0.6))
        B.box("dome_green", (sx * (half + 1.3), 0, 4.75), (2.4, 0.3, 0.35))
        B.tbox("roof_green", (sx * (half + 1.5), 0, 6.15), (4.6, 2.6, 0.9), top=(0.72, 0.18))
        B.box("gold", (sx * (half + 1.5), 0, 6.66), (3.5, 0.3, 0.14))
        # a white tiger at each foot, as at the real gate
        tx = sx * (half + 1.3)
        B.box("stone", (tx, -2.0, 0.45), (1.3, 2.0, 0.9))
        B.barrel("white", (tx, -2.0, 1.45), (0.75, 1.6, 0.8), segs=6)
        B.sphere("white", (tx, -2.95, 1.85), 0.42, segs=6, rings=4)
        for ex in (-0.22, 0.22):
            B.cyl("white", (tx + ex, -2.9, 2.25), 0.12, 0.2, r2=0.0, segs=4)
        for lx in (-0.25, 0.25):
            B.box("white", (tx + lx, -2.7, 1.0), (0.22, 0.25, 0.5))
        B.limb("white", (tx, -1.2, 1.5), (tx, -0.85, 2.2), 0.08, 0.04, segs=4)
    # the beams over the street, and the board the name is painted on
    B.box("red", (0, 0, 6.4), (half * 2 + 1.4, 0.7, 0.7))
    B.box("dome_green", (0, 0, 7.0), (half * 2 + 0.6, 0.5, 0.5))
    B.box("red", (0, 0, 8.6), (half * 2 + 1.4, 0.7, 0.6))
    B.box("sign_blue", (0, 0, 7.75), (8.4, 0.45, 1.25))
    B.box("gold", (0, 0, 7.75), (8.8, 0.3, 1.5))
    for x in (-5.2, 5.2):
        B.box("gold", (x, 0, 7.75), (1.0, 0.5, 0.9))
    # the three roofs
    B.tbox("roof_green", (0, 0, 9.5), (half * 2 + 4.6, 3.4, 1.2), top=(0.8, 0.16))
    B.box("gold", (0, 0, 10.16), (half * 2 + 1.6, 0.36, 0.16))
    B.box("red", (0, 0, 10.7), (6.4, 0.6, 1.0))
    B.tbox("roof_green", (0, 0, 11.75), (9.6, 3.0, 1.1), top=(0.74, 0.16))
    B.box("gold", (0, 0, 12.36), (6.2, 0.34, 0.16))
    for sx in (-1, 1):
        B.cyl("gold", (sx * 3.3, 0, 12.75), 0.2, 0.7, r2=0.0, segs=4)        # ridge ornaments
        B.cyl("gold", (sx * (half + 1.0), 0, 10.55), 0.2, 0.7, r2=0.0, segs=4)
    B.sphere("gold", (0, 0, 12.75), 0.34, segs=6, rings=4)
