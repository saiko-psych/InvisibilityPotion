"""Procedural low-poly mead bases (unfermented veil mead), 3 tiers, v2.
Run from the repo root: blender -b --python tools/blender/make_bowl.py

A shallow turned wooden bowl in the manner of Valheim's vanilla mead bases, about 70 % full of a murky, opaque
brew in the tier's hue (dull, no mist, no glow). The surface shows 2-3 thin raised swirl ribbons in a lighter tint of the
brew (like stirred cream) and a faint concentric ripple ring.
Tier I   Faint Veil   rough 9-sided bowl, pale wood, moss-green brew
Tier II  Deep Veil    smoother 12-sided bowl with a carved bead ring under the rim, steel-blue brew
Tier III Shadow Veil  dark wood 12-sided bowl with a silver rim band, dark violet brew
Each FBX: one mesh (pivot at the base centre) and an `attach` empty on the rim (grip point, +X side).
"""
import math, os, random, sys
from mathutils import Vector
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from common import OUT, HERE, Builder, empty, export_fbx, tri_count, write_log, reset, \
    ground, ruler, light, camera, world, render

PREVIEW = os.path.join(HERE, "preview-bowls-v2.png")

# ==== KNOBS (lengths in metres; FBX is Y-up, 1 unit = 1 m, pivot at the base centre) ====
SEED = 3
FILL = 0.70              # brew level as a fraction of the inner depth
LIQUID_SINK = 0.0015     # brew disc reaches this far into the wall (no gap at the rim of the brew)
SWIRL_W = (0.0045, 0.0015)   # swirl ribbon width at its start and end
SWIRL_H = 0.0008         # swirl lift above the brew
SWIRL_SEGS = 10
SWIRL_TURN = (0.55, 0.85)  # how far each swirl winds (fraction of a full turn)
RIPPLE_R = 0.62          # ripple ring radius as a fraction of the brew radius
RIPPLE_W = 0.0025
RIPPLE_SEGS = 16

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
    "bowl_brew_t1":   ((0.15, 0.19, 0.075, 1), 0.55, 0.0, 0.0),  # murky moss green
    "bowl_brew_t2":   ((0.085, 0.125, 0.20, 1), 0.55, 0.0, 0.0),   # muddy steel blue
    "bowl_brew_t3":   ((0.10, 0.055, 0.14, 1), 0.55, 0.0, 0.0),  # dark violet
    "bowl_swirl_t1":  ((0.30, 0.36, 0.17, 1), 0.6, 0.0, 0.0),   # lighter tints of the brew (stirred cream)
    "bowl_swirl_t2":  ((0.22, 0.30, 0.42, 1), 0.6, 0.0, 0.0),
    "bowl_swirl_t3":  ((0.25, 0.15, 0.30, 1), 0.6, 0.0, 0.0),
    "bowl_ripple_t1": ((0.19, 0.24, 0.10, 1), 0.5, 0.0, 0.0),   # faint ripple ring, just above the brew colour
    "bowl_ripple_t2": ((0.13, 0.18, 0.26, 1), 0.5, 0.0, 0.0),
    "bowl_ripple_t3": ((0.15, 0.085, 0.18, 1), 0.5, 0.0, 0.0),
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
    swirls=2),
 "t2": dict(
    segs=12, rot=0.0, vjitter=0.006, grain=0.5, attach_side=1,
    profile=[(0.000, 0.000), (0.034, 0.000), (0.036, 0.005), (0.041, 0.009), (0.058, 0.023), (0.066, 0.039),
             (0.0695, 0.045), (0.0715, 0.047), (0.0715, 0.051), (0.0695, 0.053),           # carved bead ring
             (0.070, 0.058), (0.069, 0.062), (0.060, 0.062), (0.058, 0.054), (0.049, 0.031), (0.030, 0.016),
             (0.000, 0.014)],
    rim=11, inner=12, carve=(6, 9), silver=None,
    swirls=3),
 "t3": dict(
    segs=12, rot=0.0, vjitter=0.004, grain=0.5, attach_side=1,
    profile=[(0.000, 0.000), (0.034, 0.000), (0.036, 0.005), (0.041, 0.009), (0.058, 0.023), (0.067, 0.042),
             (0.0695, 0.055), (0.069, 0.062), (0.060, 0.062), (0.058, 0.054), (0.049, 0.031), (0.030, 0.016),
             (0.000, 0.014)],
    rim=7, inner=8, carve=None,
    silver=[(0.0685, 0.0525), (0.0712, 0.0535), (0.0712, 0.0625), (0.0690, 0.0640), (0.0645, 0.0640)],  # rim band
    swirls=3),
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


def swirl(B, c, z, r0, r1, a0, turn, m):
    """Thin spiral ribbon lifted just above the brew surface from radius r0 to r1, winding `turn` of a full circle."""
    rows = []
    for i in range(SWIRL_SEGS + 1):
        f = i/SWIRL_SEGS; a = a0 + turn*2*math.pi*f; r = r0 + (r1 - r0)*f
        p = c + Vector((math.cos(a)*r, math.sin(a)*r, z))
        rad = Vector((math.cos(a), math.sin(a), 0))
        w = (SWIRL_W[0] + (SWIRL_W[1] - SWIRL_W[0])*f)/2*math.sin(math.pi*min(1.0, 0.15 + f))   # soft start, thin tail
        lift = Vector((0, 0, SWIRL_H*(1 - 0.6*f)))
        rows.append((B.v(p - rad*w + lift), B.v(p + rad*w + lift)))
    for (a_l, a_r), (b_l, b_r) in zip(rows, rows[1:]):
        B.face([a_l, b_l, b_r, a_r], m)

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
    # stirred swirls: spirals winding outwards from near the centre, in a lighter tint of the brew
    rb = r - LIQUID_SINK
    a0 = rng.uniform(0, 2*math.pi)
    for k in range(t["swirls"]):
        start = a0 + 2*math.pi*k/t["swirls"] + rng.uniform(-0.3, 0.3)
        swirl(B, Vector((rng.uniform(-0.004, 0.004), rng.uniform(-0.004, 0.004), 0)), z + 0.0002,
              rb*rng.uniform(0.08, 0.18), rb*rng.uniform(0.70, 0.85), start, rng.uniform(*SWIRL_TURN), f"bowl_swirl_{tier}")
    # faint concentric ripple ring
    n = RIPPLE_SEGS; rr = rb*RIPPLE_R
    inner = [B.v((math.cos(2*math.pi*i/n)*(rr - RIPPLE_W/2), math.sin(2*math.pi*i/n)*(rr - RIPPLE_W/2), z + 0.0001)) for i in range(n)]
    outer = [B.v((math.cos(2*math.pi*i/n)*(rr + RIPPLE_W/2), math.sin(2*math.pi*i/n)*(rr + RIPPLE_W/2), z + 0.0001)) for i in range(n)]
    B.bridge(inner, outer, f"bowl_ripple_{tier}")
    rim_z = prof[t["rim"]][1]
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
