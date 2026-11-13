"""Turns OpenStreetMap extracts into the road, river and railway data of the game.

Data (c) OpenStreetMap contributors, ODbL. Run the three queries beside this file through an
Overpass API server and save the answers next to it as city.json, major.json and local.json:

    city.overpassql   every street of the city centre
    major.overpassql  toll roads, main roads, rivers and railways of the whole map
    local.overpassql  the streets of Dramaga, Sentul and Cisarua

then:  python tools/osm/build_streets.py

It writes bogor_streets.txt (copy it over src/BarudakBogor.Core/World/bogor_streets.txt),
cisadane.txt and rail.txt (the point lists pasted into WorldGenerator.cs as CisadanePoints and
SouthRailPoints). Coordinates are metres east and south of Tugu Kujang.
"""
import os
HERE = os.path.dirname(os.path.abspath(__file__))
"""OpenStreetMap extracts -> bogor_streets.txt (roads of the whole map) and corridors.txt (river, rail)."""
import json, math, collections, sys

LAT0, LON0 = -6.60142, 106.80506
RIM = 19300.0


def xy(g):
    return (round((g['lon'] - LON0) * 110582.0, 1), round(-(g['lat'] - LAT0) * 110574.0, 1))


def length(pts):
    return sum(math.dist(a, b) for a, b in zip(pts, pts[1:]))


def dp(pts, eps):
    if len(pts) < 3:
        return list(pts)
    a, b = pts[0], pts[-1]
    dx, dy = b[0] - a[0], b[1] - a[1]
    n = math.hypot(dx, dy) or 1
    best, bi = 0, 0
    for i, p in enumerate(pts[1:-1], 1):
        d = abs((p[0] - a[0]) * dy - (p[1] - a[1]) * dx) / n
        if d > best:
            best, bi = d, i
    if best < eps:
        return [a, b]
    return dp(pts[:bi + 1], eps)[:-1] + dp(pts[bi:], eps)


def inside(p):
    return abs(p[0]) <= RIM and abs(p[1]) <= RIM


def clip(pts):
    """Pieces of a line that lie inside the map; the cut ends are put on the rim."""
    out, cur = [], []
    for i, p in enumerate(pts):
        if inside(p):
            if not cur and i > 0:
                cur.append(cross(pts[i - 1], p))
            cur.append(p)
        else:
            if cur:
                cur.append(cross(p, cur[-1]))
                out.append(cur)
                cur = []
    if cur:
        out.append(cur)
    return [c for c in out if len(c) > 1]


def cross(outer, inner):
    lo, hi = 0.0, 1.0
    for _ in range(30):
        m = (lo + hi) / 2
        q = (outer[0] + (inner[0] - outer[0]) * m, outer[1] + (inner[1] - outer[1]) * m)
        if inside(q):
            hi = m
        else:
            lo = m
    return (round(outer[0] + (inner[0] - outer[0]) * hi, 1), round(outer[1] + (inner[1] - outer[1]) * hi, 1))


# Districts of the game at their real places.
CITY = ((-600, -500), 2450)
AREAS = {'dramaga': ((-8630, -4800), 1150), 'sentul': ((5740, -7290), 1150), 'puncak': ((16000, 8900), 1300), 'situgede': ((-6800, -4700), 1150)}

core = json.load(open(os.path.join(HERE, 'city.json'), encoding='utf-8'))['elements']
major = json.load(open(os.path.join(HERE, 'major.json'), encoding='utf-8'))['elements']
local = json.load(open(os.path.join(HERE, 'local.json'), encoding='utf-8'))['elements']

MAJOR = {'motorway': 'T', 'motorway_link': 'T', 'trunk': 'A', 'trunk_link': 'A', 'primary': 'A', 'primary_link': 'A',
         'secondary': 'A', 'secondary_link': 'A', 'tertiary': 'A', 'tertiary_link': 'A'}
MINOR = {'unclassified': 'S', 'residential': 'S', 'living_street': 'G', 'service': 'S', 'raceway': 'R'}

ways = {}
for e in core + major + local:
    if e['type'] != 'way' or 'geometry' not in e:
        continue
    t = e.get('tags', {})
    h = t.get('highway')
    if h is None or e['id'] in ways:
        continue
    if t.get('tunnel') or t.get('access') == 'private':
        continue
    pts = [xy(g) for g in e['geometry']]
    name = t.get('name', '')
    if h in MAJOR:
        k = MAJOR[h]
        if h.startswith('tertiary'):
            # Minor through roads: only the city's (as before) and those of the three districts.
            ok = all(-3300 < p[0] < 2000 and -3950 < p[1] < 3300 for p in pts) or any(
                all(math.dist(p, c) < r * 1.6 for p in pts) for c, r in AREAS.values())
            if not ok:
                continue
        if h.startswith('secondary') and not all(-12500 < p[0] < 19300 and -11500 < p[1] < 13500 for p in pts):
            continue
    elif h in MINOR:
        k = MINOR[h]
        c, r = CITY
        ok = any(math.dist(p, c) < r for p in pts) and all(math.dist(p, c) < 3000 for p in pts)
        for c, r in AREAS.values():
            ok = ok or all(math.dist(p, c) < r for p in pts)
        if h == 'raceway':
            ok = True
        if not ok:
            continue
    else:
        continue
    for piece in clip(pts):
        ways.setdefault(e['id'], []).append((k, name, piece, h))

flat = [w for ws in ways.values() for w in ws]
print(len(flat), 'ways, km', round(sum(length(w[2]) for w in flat) / 1000))

# ---- twin carriageways of the toll roads: keep one of each pair -------------------------------
CELL = 60.0


def cells(p):
    return (int(p[0] // CELL), int(p[1] // CELL))


def dist_to_seg(p, a, b):
    dx, dy = b[0] - a[0], b[1] - a[1]
    n = dx * dx + dy * dy
    t = 0 if n == 0 else max(0, min(1, ((p[0] - a[0]) * dx + (p[1] - a[1]) * dy) / n))
    return math.dist(p, (a[0] + dx * t, a[1] + dy * t)), t


class Index:
    def __init__(self):
        self.grid = collections.defaultdict(list)

    def add(self, key, pts):
        for i in range(len(pts) - 1):
            a, b = pts[i], pts[i + 1]
            n = max(1, int(math.dist(a, b) // (CELL / 2)))
            for s in range(n + 1):
                q = (a[0] + (b[0] - a[0]) * s / n, a[1] + (b[1] - a[1]) * s / n)
                self.grid[cells(q)].append((key, i, a, b))

    def near(self, p, reach, skip=None):
        best = None
        cx, cy = cells(p)
        r = int(reach // CELL) + 1
        for x in range(cx - r, cx + r + 1):
            for y in range(cy - r, cy + r + 1):
                for key, i, a, b in self.grid.get((x, y), ()):
                    if key == skip:
                        continue
                    d, t = dist_to_seg(p, a, b)
                    if d <= reach and (best is None or d < best[0]):
                        best = (d, key, i, t, a, b)
        return best


kept = []
index = Index()
toll = sorted([w for w in flat if w[3] == 'motorway'], key=lambda w: -length(w[2]))
dropped = 0
for w in toll:
    pts = w[2]
    # Densify, so a long straight is judged along its length and not at its two ends.
    dense = [pts[0]]
    for p in pts[1:]:
        q = dense[-1]
        n = max(1, int(math.dist(q, p) // 40))
        dense += [(round(q[0] + (p[0] - q[0]) * s / n, 1), round(q[1] + (p[1] - q[1]) * s / n, 1)) for s in range(1, n + 1)]
    near = [index.near(p, 38.0) is not None for p in dense]
    # Only the stretches that have no twin yet are kept (with one point of overlap, to tie on to).
    runs, cur = [], []
    for i, p in enumerate(dense):
        if not near[i]:
            if not cur and i > 0:
                cur.append(dense[i - 1])
            cur.append(p)
        elif cur:
            cur.append(p)
            runs.append(cur)
            cur = []
    if cur:
        runs.append(cur)
    runs = [r for r in runs if length(r) > 60]
    if not runs:
        dropped += 1
    for r in runs:
        index.add(len(kept), r)
        kept.append((w[0], w[1], r, w[3]))
print('toll carriageways kept', len(kept), 'dropped', dropped)
flat = kept + [w for w in flat if w[3] != 'motorway']

# ---- junctions --------------------------------------------------------------------------------
def split_all(lines):
    use = collections.Counter()
    for k, n, pts, h in lines:
        for i, p in enumerate(pts):
            use[p] += 2 if i in (0, len(pts) - 1) else 1
    segs = []
    for k, n, pts, h in lines:
        cur = [pts[0]]
        for p in pts[1:]:
            if p == cur[-1]:
                continue
            cur.append(p)
            if use[p] > 1:
                segs.append((k, n, cur, h))
                cur = [p]
        if len(cur) > 1:
            segs.append((k, n, cur, h))
    return segs


segs = split_all(flat)

# A loose end left by a dropped carriageway (a ramp, a link) is tied to the road beside it.
for _ in range(3):
    deg = collections.Counter()
    for k, n, pts, h in segs:
        deg[pts[0]] += 1
        deg[pts[-1]] += 1
    index = Index()
    for i, s in enumerate(segs):
        index.add(i, s[2])
    inserts = collections.defaultdict(list)
    moved = 0
    for i, (k, n, pts, h) in enumerate(segs):
        for end in (0, -1):
            e = pts[end]
            if deg[e] != 1 or abs(e[0]) > RIM - 5 or abs(e[1]) > RIM - 5:
                continue
            reach = 90.0 if k == 'T' else (45.0 if k == 'A' else 0.0)
            hit = index.near(e, reach, skip=i) if reach else None
            if hit is None:
                continue
            d, key, si, t, a, b = hit
            q = a if t < 0.02 else b if t > 0.98 else (round(a[0] + (b[0] - a[0]) * t, 1), round(a[1] + (b[1] - a[1]) * t, 1))
            if q not in (a, b):
                inserts[key].append((si, t, q))
            pts[end] = q
            moved += 1
    for key, items in inserts.items():
        k, n, pts, h = segs[key]
        for si, t, q in sorted(items, key=lambda it: (-it[0], -it[1])):
            pts.insert(si + 1, q)
    segs = split_all([s for s in segs if len(s[2]) > 1 and length(s[2]) > 0.5])
    print('tied', moved, 'loose ends')
    if not moved:
        break

# The circuit is reached through its paddock gate: tie it to the nearest public road.
race = [s for s in segs if s[0] == 'R']
if race:
    index = Index()
    for i, s in enumerate(segs):
        if s[0] != 'R':
            index.add(i, s[2])
    best = None
    for s in race:
        for p in s[2]:
            hit = index.near(p, 600.0)
            if hit and (best is None or hit[0] < best[0][0]):
                best = (hit, p)
    hit, p = best
    d, key, si, t, a, b = hit
    q = (round(a[0] + (b[0] - a[0]) * t, 1), round(a[1] + (b[1] - a[1]) * t, 1))
    segs[key][2].insert(si + 1, q)
    segs.append(('S', 'Sirkuit Sentul', [p, q], 'service'))
    segs = split_all(segs)
    print('circuit gate', round(d), 'm')

# ---- keep the connected whole, prune what just stops ------------------------------------------
def components(segs):
    adj = collections.defaultdict(list)
    for i, s in enumerate(segs):
        adj[s[2][0]].append(i)
        adj[s[2][-1]].append(i)
    comp = {}
    c = 0
    for i in range(len(segs)):
        if i in comp:
            continue
        st = [i]
        while st:
            j = st.pop()
            if j in comp:
                continue
            comp[j] = c
            st.extend(adj[segs[j][2][0]])
            st.extend(adj[segs[j][2][-1]])
        c += 1
    return comp


comp = components(segs)
size = collections.Counter()
for i, c in comp.items():
    size[c] += length(segs[i][2])
main = size.most_common(1)[0][0]
print('components', len(size), 'main km', round(size[main] / 1000), 'others km', round((sum(size.values()) - size[main]) / 1000))
keep = [s for i, s in enumerate(segs) if comp[i] == main]


def sheltered(p):
    """A road may end here: in town or in one of the districts, or at the edge of the map."""
    if abs(p[0]) > RIM - 5 or abs(p[1]) > RIM - 5:
        return True
    if math.dist(p, CITY[0]) < 2300:
        return True
    return any(math.dist(p, c) < r * 0.9 for c, r in AREAS.values())


changed = True
while changed:
    changed = False
    deg = collections.Counter()
    for k, n, pts, h in keep:
        deg[pts[0]] += 1
        deg[pts[-1]] += 1
    nxt = []
    for s in keep:
        pts = s[2]
        loose = [e for e in (pts[0], pts[-1]) if deg[e] == 1 and not sheltered(e)]
        if loose and s[0] != 'R':
            changed = True
        else:
            nxt.append(s)
    keep = nxt

out = []
tot = collections.Counter()
for k, n, pts, h in keep:
    pts = dp(pts, 3.0)
    ln = length(pts)
    if ln < 1:
        continue
    # The climb to Puncak is the mountain road of the game.
    if k == 'A' and 'Raya Puncak' in n and min(p[0] for p in pts) > 9000:
        k = 'M'
    if k == 'R':
        k = 'S'
        n = 'Sirkuit Sentul'
    tot[k] += ln
    out.append('%s|%s|%s' % (k, n.replace('|', ' '), ' '.join('%.0f,%.0f' % p for p in pts)))
# A main road that still ends in the open (its far half was not surveyed) is a side street.
rows = [o.split('|') for o in out]
ends = lambda r: (r[2].split(' ')[0], r[2].split(' ')[-1])
for _ in range(20):
    deg = collections.Counter()
    for r in rows:
        a, b = ends(r)
        if a != b:
            deg[a] += 1
            deg[b] += 1
    hit = 0
    for r in rows:
        if r[0] in 'AT':
            for e in ends(r):
                p = tuple(float(v) for v in e.split(','))
                if deg[e] == 1 and not sheltered(p):
                    print('demoted', r[0], r[1], e)
                    r[0] = 'S'
                    hit += 1
                    break
    if not hit:
        break
out = ['|'.join(r) for r in rows]
open(os.path.join(HERE, 'bogor_streets.txt'), 'w', encoding='utf-8', newline='\n').write('\n'.join(out) + '\n')
print(len(out), 'stretches;', {k: round(v / 1000) for k, v in tot.items()}, 'km')

# ---- river and railway ------------------------------------------------------------------------
def chain(lines, start):
    lines = [list(l) for l in lines]
    cur, outp = start, []
    while lines:
        bi, bd, rev = None, 1e18, False
        for i, w in enumerate(lines):
            for r, e in ((False, w[0]), (True, w[-1])):
                d = math.dist(cur, e)
                if d < bd:
                    bi, bd, rev = i, d, r
        if bd > 250:
            break
        w = lines.pop(bi)
        if rev:
            w = w[::-1]
        outp += w if not outp else w[1:]
        cur = outp[-1]
    return outp


def fmt(pts):
    return ', '.join('new(%.0ff, %.0ff)' % p for p in pts)


rivers = [[xy(g) for g in e['geometry']] for e in major if e['type'] == 'way' and e.get('tags', {}).get('waterway') == 'river'
          and e['tags'].get('name') in ('Sungai Cisadane', 'Cisadane', 'Ci Sadane')]
south = max((l for l in rivers), key=lambda l: max(p[1] for p in l))
start = max(south, key=lambda p: p[1])
line = chain(rivers, start)
pieces = clip(line)
line = max(pieces, key=length)
print('CISADANE', round(length(line)), 'm', line[0], line[-1])
open(os.path.join(HERE, 'cisadane.txt'), 'w').write(fmt(dp(line, 45.0)))
print('cisadane pts', len(dp(line,45.0)))

rails = [[xy(g) for g in e['geometry']] for e in major if e['type'] == 'way' and e.get('tags', {}).get('railway') == 'rail'
         and 'Padalarang' in e['tags'].get('name', '') and e['tags'].get('service') is None]
paledang = (-1591.4, -394.6)
first = min(rails, key=lambda l: min(math.dist(p, paledang) for p in l))
start = min(first, key=lambda p: math.dist(p, paledang))
# Southward only: from Paledang on.
line = chain(rails, min((l[0] for l in rails), key=lambda p: math.dist(p, (-1586, -789))))
i0 = min(range(len(line)), key=lambda i: math.dist(line[i], paledang))
line = max(clip(line[i0:]), key=length)
if line[0][1] > line[-1][1]:
    line = line[::-1]
print('RAIL SOUTH', round(length(line)), 'm', line[0], line[-1])
open(os.path.join(HERE, 'rail.txt'), 'w').write(fmt(dp(line, 12.0)))
print('rail pts', len(dp(line,12.0)))
