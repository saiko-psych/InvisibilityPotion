"""Procedural low-poly mead bases (unfermented veil mead), 3 tiers, v1.
Run from the repo root: blender -b --python tools/blender/make_bowl.py

A shallow turned wooden bowl in the manner of Valheim's vanilla mead bases, about 70 % full of a murky, opaque
brew in the tier's hue (dull, no mist, no glow), a few herb specks floating on it and a stirring stick or spoon
resting on the rim.
Tier I   Faint Veil   rough 9-sided bowl, pale wood, moss-green brew, crude stirring stick, lichen specks
Tier II  Deep Veil    smoother 12-sided bowl with a carved bead ring under the rim, steel-blue brew, spoon, petal specks
Tier III Shadow Veil  dark wood 12-sided bowl with a silver rim band, dark violet brew, dark spoon, ember-ash specks
Each FBX: one mesh (pivot at the base centre) and an `attach` empty on the rim (grip point, +X side).
"""
import math, os, random, sys
from mathutils import Vector
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from common import OUT, HERE, Builder, empty, export_fbx, tri_count, write_log, reset, \
    ground, ruler, light, camera, world, render

PREVIEW = os.path.join(HERE, "preview-bowls-v1.png")

# ==== KNOBS (lengths in metres; FBX is Y-up, 1 unit = 1 m, pivot at the base centre) ====
SEED = 3
FILL = 0.70              # brew level as a fraction of the inner depth
LIQUID_SINK = 0.0015     # brew disc reaches this far into the wall (no gap at the rim of the brew)

# Material table: name -> (colour RGBA, roughness, metallic, emission strength). Names = Unity .mat names.
MATS = {
    "bowl_wood_t1":   ((0.36, 0.26, 0.16, 1), 0.9, 0.0, 0.0),
    "bowl_grain_t1":  ((0.27, 0.19, 0.115, 1), 0.92, 0.0, 0.0),
    "bowl_inner_t1":  ((0.25, 0.17, 0.10, 1), 0.9, 0.0, 0.0),
    "bowl_wood_t2":   ((0.42, 0.31, 0.20, 1), 0.75, 0.0, 0.0),
    "bowl_grain_t2":  ((0.34, 0.24, 0.15, 1), 0.78, 0.0, 0.0),
    "bowl_inner_t2":  ((0.28, 0.19, 0.11, 1), 0.8, 0.0, 0.0),
    "bowl_carve_t2":  ((0.21, 0.13, 0.07, 1), 0.85, 0.0, 0.0),
    "bowl_wood_t3":   ((0.17, 0.11, 0.075, 1), 0.7, 0.0, 0.0),
    "bowl_grain_t3":  ((0.12, 0.075, 0.05, 1), 0.72, 0.0, 0.0),
    "bowl_inner_t3":  ((0.10, 0.065, 0.045, 1), 0.75, 0.0, 0.0),
    "bowl_silver":    ((0.62, 0.63, 0.66, 1), 0.3, 0.9, 0.0),
    "bowl_stick":     ((0.47, 0.36, 0.22, 1), 0.9, 0.0, 0.0),
    "bowl_spoon":     ((0.55, 0.42, 0.27, 1), 0.8, 0.0, 0.0),
    "bowl_spoon_t3":  ((0.24, 0.16, 0.10, 1), 0.7, 0.0, 0.0),
    "bowl_brew_t1":   ((0.15, 0.19, 0.075, 1), 0.55, 0.0, 0.0),  # murky moss green
    "bowl_brew_t2":   ((0.085, 0.125, 0.20, 1), 0.55, 0.0, 0.0),   # muddy steel blue
    "bowl_brew_t3":   ((0.10, 0.055, 0.14, 1), 0.55, 0.0, 0.0),  # dark violet
    "bowl_speck_t1":  ((0.36, 0.40, 0.28, 1), 0.9, 0.0, 0.0),    # lichen bits
    "bowl_speck_t2":  ((0.78, 0.82, 0.86, 1), 0.8, 0.0, 0.0),    # petal bits
    "bowl_speck_t3":  ((0.34, 0.12, 0.05, 1), 0.9, 0.0, 0.0),    # ember-ash bits (not emissive)
}

# Profiles: (radius, height) from the bottom centre over the outside to the rim, then down the inside.
# `rim` = index of the outer rim-top point; `inner` = index where the inside starts (rim-top inner edge).
TIERS = {
 "t1": dict(
    segs=9, rot=0.3, vjitter=0.025, grain=0.45, attach_side=1,
    profile=[(0.000, 0.000), (0.033, 0.000), (0.036, 0.006), (0.041, 0.010), (0.058, 0.025), (0.067, 0.044),
             (0.071, 0.057), (0.070, 0.062), (0.060, 0.062), (0.058, 0.054), (0.049, 0.031), (0.030, 0.016),
             (0.000, 0.014)],
    rim=7, inner=8, carve=None, silver=None,
    tool="stick", specks=6, speck_size=(0.004, 0.007)),
 "t2": dict(
    segs=12, rot=0.0, vjitter=0.006, grain=0.5, attach_side=1,
    profile=[(0.000, 0.000), (0.034, 0.000), (0.036, 0.005), (0.041, 0.009), (0.058, 0.023), (0.066, 0.039),
             (0.0695, 0.045), (0.0715, 0.047), (0.0715, 0.051), (0.0695, 0.053),           # carved bead ring
             (0.070, 0.058), (0.069, 0.062), (0.060, 0.062), (0.058, 0.054), (0.049, 0.031), (0.030, 0.016),
             (0.000, 0.014)],
    rim=11, inner=12, carve=(6, 9), silver=None,
    tool="spoon", specks=6, speck_size=(0.004, 0.006)),
 "t3": dict(
    segs=12, rot=0.0, vjitter=0.004, grain=0.5, attach_side=1,
    profile=[(0.000, 0.000), (0.034, 0.000), (0.036, 0.005), (0.041, 0.009), (0.058, 0.023), (0.067, 0.042),
             (0.0695, 0.055), (0.069, 0.062), (0.060, 0.062), (0.058, 0.054), (0.049, 0.031), (0.030, 0.016),
             (0.000, 0.014)],
    rim=7, inner=8, carve=None,
    silver=[(0.0685, 0.0525), (0.0712, 0.0535), (0.0712, 0.0625), (0.0690, 0.0640), (0.0645, 0.0640)],  # rim band
    tool="spoon", specks=5, speck_size=(0.004, 0.006)),
}
# =============================================================================

scene = reset()


def brew_level(t):
    prof = t["profile"]; bottom = prof[-1][1]; top = prof[t["inner"]][1]
    z = bottom + FILL*(top - bottom)
    inside = prof[t["inner"]:]                          # descending heights
    for (r0, z0), (r1, z1) in zip(inside, inside[1:]):
        if z1 <= z <= z0:
            return z, r1 + (r0 - r1)*(z - z1)/(z0 - z1)
    raise ValueError("fill level outside the bowl")


def flake(B, c, size, ang, m, rng):
    """Tiny floating herb fleck: flat three-sided pyramid (closed, so normals stay right)."""
    pts = [c + Vector((math.cos(ang + a)*size*rng.uniform(0.6, 1.0), math.sin(ang + a)*size*rng.uniform(0.6, 1.0), 0))
           for a in (0, 2.2, 4.2)]
    top = c + Vector((0, 0, 0.0008))
    for i in range(3):
        B.face([pts[i], pts[(i+1) % 3], top], m)
    B.face([pts[2], pts[1], pts[0]], m)


def spoon(B, head, rim_pt, out_len, m, rng):
    """Spoon: oval head half-sunk in the brew at `head`, handle over `rim_pt` and out by out_len."""
    d = (rim_pt - head); d.z = 0; d.normalize()
    side = Vector((-d.y, d.x, 0))
    ring = []
    for k in range(8):                                    # oval bowl of the spoon, slightly tilted up the handle
        a = 2*math.pi*k/8
        p = head + d*math.cos(a)*0.017 + side*math.sin(a)*0.011
        p.z += math.cos(a)*0.003
        ring.append(B.v(p))
    top, bot = B.v(head + Vector((0, 0, -0.001))), B.v(head + Vector((0, 0, -0.007)))
    for k in range(8):
        j = (k+1) % 8
        B.face([ring[k], ring[j], top], m); B.face([ring[j], ring[k], bot], m)
    neck = head + d*0.016 + Vector((0, 0, 0.004))
    over = rim_pt + Vector((0, 0, 0.004))
    end = over + d*out_len + Vector((0, 0, out_len*0.25))
    B.tube([neck, over, end], [0.0028, 0.0034, 0.0], 5, m)


def stick(B, a, rim_pt, out_len, m):
    """Crude stirring stick from inside the brew over the rim, with a short twig stub."""
    over = rim_pt + Vector((0, 0, 0.005))
    d = over - a; d.z = 0; d.normalize()
    end = over + d*out_len + Vector((0, 0, out_len*0.3))       # flatter outside the bowl
    mid = a.lerp(over, 0.5) + Vector((0.0, 0.0, 0.002))
    B.tube([a, mid, over, end], [0.0042, 0.0045, 0.0042, 0.0], 5, m)
    stub = end.lerp(over, 0.35)
    B.tube([stub, stub + Vector((0.0, 0.010, 0.010))], [0.0022, 0.0], 3, m)


def make(tier, idx, t):
    rng = random.Random(SEED + idx)
    B = Builder(MATS)
    prof, segs = t["profile"], t["segs"]
    grain_cols = [rng.random() < t["grain"] for _ in range(segs)]
    carve = t["carve"]
    def band_mat(bi, col):
        if bi >= t["inner"]: return f"bowl_inner_{tier}"              # inside (stained by the brew)
        if bi == t["rim"]: return f"bowl_wood_{tier}"                 # rim top
        if carve and carve[0] <= bi < carve[1]: return f"bowl_carve_{tier}"
        return f"bowl_grain_{tier}" if grain_cols[col] else f"bowl_wood_{tier}"
    rings = B.lathe(prof, band_mat, segs, 0.0, rng, t["rot"])
    for ri, ring in enumerate(rings):                      # hand-turned unevenness, per vertex
        if len(ring) == 1 or ri in (0, len(rings) - 1): continue
        for v in ring:
            k = 1 + rng.uniform(-t["vjitter"], t["vjitter"])
            v.co.x *= k; v.co.y *= k
    if t["silver"]:
        B.lathe(t["silver"], "bowl_silver", segs, 0.0, None, t["rot"])
    # brew: shallow cone (centre a little low), reaching into the wall
    z, r = brew_level(t)
    r += LIQUID_SINK
    B.lathe([(0, z - 0.0012), (r*0.55, z - 0.0004), (r, z)], f"bowl_brew_{tier}", segs, 0.0, None, t["rot"])
    # floating specks
    for i in range(t["specks"]):
        a = rng.uniform(0, 2*math.pi); rr = (r - LIQUID_SINK)*math.sqrt(rng.uniform(0.04, 0.55))
        c = Vector((math.cos(a)*rr, math.sin(a)*rr, z + 0.0001))
        flake(B, c, rng.uniform(*t["speck_size"]), rng.uniform(0, 6.3), f"bowl_speck_{tier}", rng)
    # tool resting on the rim, back-left so it does not hide the brew from the front
    rim_r = prof[t["rim"]][0] - 0.004; rim_z = prof[t["rim"]][1]
    ang = math.radians(128)
    rim_pt = Vector((math.cos(ang)*rim_r, math.sin(ang)*rim_r, rim_z))
    if t["tool"] == "stick":
        stick(B, Vector((math.cos(ang + 2.8)*0.030, math.sin(ang + 2.8)*0.030, z - 0.010)), rim_pt, 0.040, "bowl_stick")
    else:
        m = "bowl_spoon_t3" if tier == "t3" else "bowl_spoon"
        head = Vector((math.cos(ang + 3.0)*0.018, math.sin(ang + 3.0)*0.018, z + 0.001))
        spoon(B, head, rim_pt, 0.035, m, rng)
    ob = B.finish("bowl_" + tier)
    side = t["attach_side"]
    empty("attach", (side*prof[t["rim"]][0], 0, rim_z), ob, 0.01)
    return ob


bowls = {}
for i, (k, t) in enumerate(TIERS.items()):
    bowls[k] = make(k, i, t)

log = []
for k, ob in bowls.items():
    vs = [v.co for v in ob.data.vertices]
    log.append(f"TRIS bowl_{k}: {tri_count(ob)}  {max(v.x for v in vs) - min(v.x for v in vs):.3f} m across  "
               f"height {max(v.z for v in vs):.3f} m  brew at {brew_level(TIERS[k])[0]:.3f} m")
    export_fbx(os.path.join(OUT, f"bowl_{k}.fbx"), ob)
write_log("bowls", log)

# ---- preview scene ----
for i, ob in enumerate(bowls.values()):
    ob.location = ((i-1)*0.18, 0, 0); ob.rotation_euler.z = 0.0
ground(2.0, (0.10, 0.065, 0.04, 1), 0.85)
ruler((0.09, 0.115, 0), height=0.1, band=0.02, r=0.005)         # 0.1 m in 0.02 m bands
tgt = (0, 0, 0.03)
light("key", 'AREA', (0.6, -0.7, 0.9), 22, (1.0, 0.75, 0.5), 0.7, tgt)
light("fill", 'AREA', (-0.9, -0.6, 0.5), 5, (0.65, 0.75, 1.0), 1.0, tgt)
light("rim", 'AREA', (0.0, 0.9, 0.6), 10, (1.0, 0.8, 0.6), 0.6, tgt)
camera((0.0, -0.70, 0.40), (0.0, 0, 0.055), 46)
world((0.09, 0.09, 0.10, 1))
render("preview-bowls.png", PREVIEW, "bowls.blend")
