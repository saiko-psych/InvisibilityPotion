"""Procedural low-poly ingredient pickup items (what the player carries after harvesting), v1.
Run from the repo root: blender -b --python tools/blender/make_ingredients.py

Tier I   Huldra's Hair tuft   loose bundle of grey-green lichen strands tied with twine, a few pale tips
Tier II  Baldr's Tear         one white-blue bell blossom on a short stem with two white berries, faint blue glow in the cup
Tier III Hel's Ember spore    a black fern frond fragment curled around three glowing ember spore capsules
All items lie on the ground as dropped. Pivot at the base (lowest point at z = 0, centred in x/y); `attach` empty at
the item's bounding-box centre. Strands, petals and leaflets are double-sided (two opposite faces).
"""
import math, os, random, sys
from mathutils import Vector
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from common import OUT, HERE, Builder, bezier, empty, export_fbx, tri_count, write_log, reset, \
    ground, ruler, light, camera, world, render

PREVIEW = os.path.join(HERE, "preview-ingredients-v1.png")

# ==== KNOBS (metres) ====
SEED = 21
MATS = {
    "ingr_lichen":       ((0.36, 0.43, 0.31, 1), 0.85, 0.0, 0.0),
    "ingr_lichen_shade": ((0.26, 0.32, 0.23, 1), 0.9, 0.0, 0.0),
    "ingr_lichen_tip":   ((0.62, 0.66, 0.55, 1), 0.8, 0.0, 0.0),
    "ingr_twine":        ((0.50, 0.40, 0.25, 1), 0.95, 0.0, 0.0),
    "ingr_petal":        ((0.82, 0.88, 0.96, 1), 0.5, 0.0, 0.0),
    "ingr_glow":         ((0.25, 0.55, 1.00, 1), 0.4, 0.0, 1.2),
    "ingr_stem":         ((0.33, 0.45, 0.38, 1), 0.8, 0.0, 0.0),
    "ingr_berry":        ((0.92, 0.92, 0.86, 1), 0.35, 0.0, 0.0),
    "ingr_frond":        ((0.06, 0.055, 0.055, 1), 0.75, 0.0, 0.0),
    "ingr_spore":        ((0.95, 0.24, 0.03, 1), 0.5, 0.0, 1.3),
}
T1 = dict(strands=14, length=(0.06, 0.09), width=(0.005, 0.008), knot_x=-0.035, tail=0.012)
T2 = dict(bell_len=0.045, bell_r=0.022, petals=6, stem_len=0.05, berry_r=0.007)
T3 = dict(curl_r=0.034, turns=1.15, leaflets=11, leaflet_len=0.026, leaflet_base=0.006, capsule_r=0.0075)
# ======================

scene = reset()


def card(B, q, m):
    """Double-sided quad or triangle: both windings, excluded from the normal recalculation."""
    if not hasattr(B, "keep"): B.keep = []
    B.keep += [f for f in (B.face(list(q), m), B.face(list(reversed(q)), m)) if f]


def ribbon(B, pts, widths, side, mats):
    for i in range(len(pts) - 1):
        a, b = pts[i], pts[i+1]
        card(B, [a - side*widths[i]/2, a + side*widths[i]/2, b + side*widths[i+1]/2, b - side*widths[i+1]/2], mats[i])


def tuft(rng):
    t = T1; B = Builder(MATS)
    knot = Vector((t["knot_x"], 0, 0.009))
    for i in range(t["strands"]):
        a = math.radians(rng.uniform(-28, 28)); L = rng.uniform(*t["length"])
        d = Vector((math.cos(a), math.sin(a), 0))
        lift = rng.uniform(0.0, 0.006)
        pts = [knot + Vector((0, rng.uniform(-0.003, 0.003), rng.uniform(-0.002, 0.002)))]
        for k in range(1, 5):
            f = k/4
            pts.append(knot + d*L*f + Vector((0, 0, -0.007*f + lift*math.sin(math.pi*f)))
                       + Vector((-d.y, d.x, 0))*0.006*math.sin(2*math.pi*f + i))
        w = rng.uniform(*t["width"])
        m = "ingr_lichen" if i % 3 else "ingr_lichen_shade"
        ribbon(B, pts, [w*0.7, w, w*0.9, w*0.7, w*0.35], Vector((-d.y, d.x, 0)), [m, m, m, "ingr_lichen_tip" if i % 2 else m])
        if i % 4 == 0:                                   # cut tops sticking out behind the knot
            p0 = knot; p1 = knot + Vector((-t["tail"], rng.uniform(-0.004, 0.004), -0.003))
            ribbon(B, [p0, p1], [w*0.8, w*0.6], Vector((0, 1, 0)), ["ingr_lichen_shade"])
    # twine: two wraps around the bundle and a loose end
    for k, dx in enumerate((-0.002, 0.003)):
        B.torus(knot + Vector((dx, 0, 0)), (1, 0, 0), 0.0085 - 0.001*k, 0.0016, 8, 3, "ingr_twine", stretch=1.0)
    B.tube([knot + Vector((0.003, 0.008, 0)), knot + Vector((0.006, 0.02, -0.004)), knot + Vector((0.004, 0.03, -0.008))],
           [0.0013, 0.0012, 0.0], 3, "ingr_twine")
    return B


def blossom(rng):
    t = T2; B = Builder(MATS)
    base = Vector((-0.01, 0, 0.022)); ax = Vector((1, 0, -0.25)).normalized()
    up = Vector((0, 0, 1)); u = (up - ax*up.dot(ax)).normalized(); v = ax.cross(u)
    L, R = t["bell_len"], t["bell_r"]
    prof = [(0.25, 0.0), (0.85, 0.30), (1.0, 0.62), (0.95, 1.0)]
    half = math.pi/t["petals"]*0.62
    for p in range(t["petals"]):
        a0 = 2*math.pi*p/t["petals"]
        rows = []
        for rf, lf in prof:
            rows.append([base + ax*L*lf + R*rf*(math.cos(a0 + da)*u + math.sin(a0 + da)*v) for da in (-half, 0, half)])
        tip = rows[-1][1] + ax*L*0.12
        for r0, r1 in zip(rows[:-2], rows[1:-1]):
            card(B, [r0[0], r0[1], r1[1], r1[0]], "ingr_petal"); card(B, [r0[1], r0[2], r1[2], r1[1]], "ingr_petal")
        r1 = rows[-2]
        card(B, [r1[0], r1[1], tip], "ingr_petal"); card(B, [r1[1], r1[2], tip], "ingr_petal")
    # glowing cup and teardrop
    B.tube([base + ax*L*0.1, base + ax*L*0.4, base + ax*L*0.72], [R*0.25, R*0.55, R*0.5], 6, "ingr_glow", cap=True)
    tear = base + ax*L*1.25
    B.tube([tear - ax*0.009, tear - ax*0.003, tear + ax*0.004, tear + ax*0.008], [0.0, 0.004, 0.0045, 0.0], 5, "ingr_glow")
    # short stem with a green cap, two berries on a twig
    stem = bezier([base - ax*0.004, base - ax*0.02 + Vector((0, 0.004, -0.01)), base - ax*t["stem_len"] + Vector((0, 0.01, -0.018))], 4)
    B.tube(stem, [0.0035, 0.003, 0.0028, 0.0025], 4, "ingr_stem", cap=True)
    B.tube([base - ax*0.004, base + ax*0.008], [0.003, R*0.3], 6, "ingr_stem")
    tw = stem[2]
    for k, off in enumerate((Vector((0.004, -0.016, -0.004)), Vector((-0.006, -0.02, -0.005)))):
        B.tube([tw, tw + off*0.8], [0.0015, 0.0012], 3, "ingr_stem")
        B.ico(tw + off, (t["berry_r"],)*3, "ingr_berry", 1, 0.05, rng)
    return B


def ember_frond(rng):
    t = T3; B = Builder(MATS)
    # rachis: a flat spiral lying on the ground, curling inwards around the capsules
    n = 11; pts = []
    for i in range(n):
        f = i/(n - 1); ph = f*t["turns"]*2*math.pi; r = t["curl_r"]*(1 - 0.62*f)
        pts.append(Vector((math.cos(ph)*r, math.sin(ph)*r, 0.004 + 0.010*f)))
    B.tube(pts, [0.0028*(1 - 0.6*i/(n - 1)) for i in range(n - 1)] + [0.0], 3, "ingr_frond")
    # sawtooth leaflets on the outer side, angled up so they read from above, shrinking toward the tip
    for i in range(t["leaflets"]):
        s = (i + 0.5)/t["leaflets"]*(n - 2)
        j = int(s); q = pts[j].lerp(pts[j+1], s - j); tan = (pts[j+1] - pts[j]).normalized()
        out = Vector((q.x, q.y, 0)).normalized()
        ll = t["leaflet_len"]*(1 - 0.6*i/t["leaflets"])
        for sg, side in ((1, out), (-1, -out)):
            if sg < 0 and i < t["leaflets"] - 3: continue     # inner leaflets only near the tip (room for the capsules)
            d = (side + tan*0.4 + Vector((0, 0, 0.45))).normalized()
            qa, qb = q - tan*t["leaflet_base"], q + tan*t["leaflet_base"]; tip = q + d*ll
            card(B, [qa, tip, qb], "ingr_frond")
            p0, p1 = qb.lerp(tip, 0.3), qb.lerp(tip, 0.65)
            card(B, [p0, p1 + tan*ll*0.18, p1], "ingr_frond")      # saw tooth
    # three glowing spore capsules nestled inside the curl
    for k in range(3):
        a = 2*math.pi*k/3 + 0.5
        B.ico((math.cos(a)*0.011, math.sin(a)*0.011, t["capsule_r"]), (t["capsule_r"]*rng.uniform(0.9, 1.1),)*3,
              "ingr_spore", 1, 0.1, rng)
    return B


items = {}
for i, (k, fn) in enumerate((("t1", tuft), ("t2", blossom), ("t3", ember_frond))):
    B = fn(random.Random(SEED + i))
    ob = B.finish(f"ingredient_{k}")
    vs = [v.co for v in ob.data.vertices]
    lo = Vector((min(v.x for v in vs), min(v.y for v in vs), min(v.z for v in vs)))
    hi = Vector((max(v.x for v in vs), max(v.y for v in vs), max(v.z for v in vs)))
    shift = Vector(((lo.x + hi.x)/2, (lo.y + hi.y)/2, lo.z))           # pivot: base centre
    for v in ob.data.vertices: v.co -= shift
    ob.data.update()
    empty("attach", (0, 0, (hi.z - lo.z)/2), ob, 0.01)
    items[k] = (ob, hi - lo)

log = []
for k, (ob, size) in items.items():
    log.append(f"TRIS ingredient_{k}: {tri_count(ob)}  size {size.x:.3f} x {size.y:.3f} x {size.z:.3f} m")
    export_fbx(os.path.join(OUT, f"ingredient_{k}.fbx"), ob)
write_log("ingredients", log)

# ---- preview scene ----
for i, (ob, _) in enumerate(items.values()):
    ob.location = ((i - 1)*0.115, 0, 0); ob.rotation_euler.z = (-0.3, 0.4, 0.0)[i]
ground(2.0, (0.20, 0.16, 0.11, 1), 0.9)
ruler((0.175, 0.07, 0), height=0.1, band=0.02, r=0.004)          # 0.1 m in 0.02 m bands
tgt = (0, 0, 0.01)
light("key", 'AREA', (0.5, -0.6, 0.7), 14, (1.0, 0.85, 0.7), 0.6, tgt)
light("fill", 'AREA', (-0.7, -0.4, 0.4), 4, (0.7, 0.8, 1.0), 0.8, tgt)
light("rim", 'AREA', (0.0, 0.7, 0.5), 8, (1.0, 0.9, 0.8), 0.5, tgt)
camera((0.01, -0.38, 0.26), (0.01, 0.025, 0.0), 45)
world((0.10, 0.10, 0.11, 1))
render("preview-ingredients.png", PREVIEW, "ingredients.blend")
