"""Procedural low-poly ingredient plants, one per biome. Run: blender -b --python make_plants.py

Tier I   Black Forest  Huldra's Hair (Huldrelokk)   rare creeping lichen on fir/pine bark (like Ashvine on walls): thin
                                                     runners, leaf tufts, short hanging beards; trunk and flat variants
Tier II  Mountains     Baldr's Tear (Baldrsgrat)    nodding white-blue snow flower with mistletoe berries
Tier III Ashlands      Hel's Ember Fern (Helfern)   black fern, two frond layers, fiddleheads, ember spore capsules,
                                                     glowing heart in a charred root crown on cracked basalt
Lore: docs/ideas/2026-10-01-norse-lore-research.md section 3.
"""
import math, os, random, shutil, sys
from mathutils import Vector, Matrix
from mathutils.bvhtree import BVHTree
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from common import OUT, HERE, Builder as _Builder, bezier, empty, export_fbx, tri_count, write_log, reset, \
    ground, ruler, light, camera, world, render_panels

PREVIEW = os.path.join(HERE, "preview-plants-v5.png")

# ==== KNOBS (lengths in metres; Blender Z-up, FBX exported Y-up, 1 unit = 1 m, pivot at ground) ====
SEED = 11                # deterministic; each plant uses SEED + tier index

# Material table: name -> (colour RGBA, roughness, metallic, emission strength)
MATS = {
    # Tier I: Huldra's Hair (grey-green, pale tips)
    "huldra_runner":  ((0.27, 0.30, 0.24, 1), 0.9, 0.0, 0.0),   # creeping runners
    "huldra_strand":  ((0.34, 0.42, 0.30, 1), 0.85, 0.0, 0.0),  # pale grey-green leaves and strands
    "huldra_strand_shade": ((0.24, 0.30, 0.21, 1), 0.9, 0.0, 0.0),  # second shade
    "huldra_tip":     ((0.55, 0.60, 0.47, 1), 0.8, 0.0, 0.0),   # pale tips
    "huldra_glint":   ((0.45, 0.68, 0.72, 1), 0.5, 0.0, 0.7),   # faint cold shimmer at a few strand tips
    # Tier II: Baldr's Tear
    "baldr_snow":    ((0.72, 0.75, 0.81, 1), 0.7, 0.0, 0.0),
    "baldr_stem":    ((0.33, 0.43, 0.38, 1), 0.8, 0.0, 0.0),
    "baldr_leaf":    ((0.50, 0.62, 0.64, 1), 0.65, 0.0, 0.0),   # frosted blue-green
    "baldr_petal":   ((0.80, 0.86, 0.94, 1), 0.5, 0.0, 0.0),
    "baldr_glow":    ((0.22, 0.50, 0.95, 1), 0.4, 0.0, 1.2),    # blossom centre
    "baldr_berry":   ((0.90, 0.90, 0.84, 1), 0.35, 0.0, 0.0),
    # Tier III: Hel's Ember Fern
    "helfern_basalt": ((0.07, 0.065, 0.065, 1), 0.85, 0.0, 0.0),
    "helfern_crack":  ((0.55, 0.12, 0.02, 1), 0.6, 0.0, 1.0),   # lava glow between the columns
    "helfern_root":   ((0.035, 0.028, 0.026, 1), 0.9, 0.0, 0.0),  # charred root crown
    "helfern_heart":  ((1.00, 0.42, 0.06, 1), 0.4, 0.0, 3.0),   # glowing core in the crown
    "helfern_heart_dim": ((0.45, 0.14, 0.03, 1), 0.5, 0.0, 0.6),  # picked state: the heart barely glows
    "helfern_stem":   ((0.06, 0.05, 0.05, 1), 0.8, 0.0, 0.0),
    "helfern_frond":  ((0.055, 0.05, 0.05, 1), 0.75, 0.0, 0.0),  # charcoal leaflets
    "helfern_crozier": ((0.22, 0.06, 0.03, 1), 0.7, 0.0, 0.4),  # young fiddleheads, faint ember tint
    "helfern_spore":  ((0.95, 0.30, 0.04, 1), 0.5, 0.0, 1.6),   # ember spore capsules under the leaflets
    "helfern_ember":  ((1.00, 0.33, 0.04, 1), 0.5, 0.0, 2.5),   # ember dots
}

T1 = dict(  # Huldra's Hair: dense creeping lichen on bark (like Ashvine on walls), 3 growth stages, pivot = patch centre
    trunk_r=0.25, surface_off=0.0025, pick_off=0.01,
    # skeleton: main runners radiate from the centre (headings in degrees, in growth order), then branch
    headings=(95, 265, 30, 150, 215, 325, 60, 120, 185, 355),
    reach_up=0.27, reach_side=0.19, reach_down=0.15, step=0.04, branches_per_runner=2, runner_w=0.008, ridge_until=0.07,
    node_step=0.03, leaves_min=1, leaves_max=4, rosette=14, leaf_len=0.034, leaf_w=0.016, mature_dist=0.07,
    beard_zmax=0.06, beard_p=0.2, beard_strands=(1, 3), beard_len=(0.04, 0.09), beard_w=(0.005, 0.009),
    beard_delay=0.02, glints=6,
    # growth stages s1..s3: number of runners (in heading order) and fraction of each runner's length
    stage_runners=(2, 7, 10), stage_reach=(0.38, 0.68, 1.0),
)
T2 = dict(  # Baldr's Tear, ~0.45 m
    mound_r=0.11, mound_h=0.035,
    stem_h=0.43, stem_r=0.0055, nod=0.62,     # nod: how far the flower head bends over (0 = upright)
    bell_len=0.065, bell_r=0.032, petals=6,
    bud_stem_h=0.25,
    leaves=5, leaf_len=(0.17, 0.26), leaf_w=0.014,
    berries=3, berry_r=0.013,
    bell_open=1.0, bud_stems=1, leaf_rise=1.0, cut_h=0.13,   # cut_h: stub height of the picked state
    scale=(1.0, 1.0),                                         # variant scale (horizontal, vertical) about the pivot
)
T3 = dict(  # Hel's Ember Fern, ~0.45 m
    columns=6, crack_r=0.125, crown_z=0.068, roots=5, root_len=(0.10, 0.15),
    heart_r=0.020, heart_z=0.074, ember_anchor_dz=0.10,
    stem_r=0.0045, leaflet_droop=0.3, teeth_min=0.040,   # no saw teeth on leaflets shorter than this
    outer=dict(n=6, phase=0.0, len=(0.50, 0.55), angle=(80, 12), segs=5, leaflets=8, leaflet_len=0.11, fwd=0.45, start_r=0.026),
    inner=dict(n=4, phase=0.5, len=(0.30, 0.34), angle=(88, 50), segs=4, leaflets=5, leaflet_len=0.08, fwd=0.2, start_r=0.016),
    spore_leaflets=(2, 3, 4, 5), spore_r=0.0045,   # outer fronds: one capsule under each of these leaflets (alternating sides)
    crozier_h=(0.13, 0.10), crozier_az=(-60, 150), crozier_r=0.024, crozier_turns=1.35, crozier_w=0.006,
    embers=3,
    young=dict(n=2, phase=0.25, len=(0.12, 0.15), angle=(85, 55), segs=3, leaflets=4, leaflet_len=0.045, fwd=0.2, start_r=0.016),
    scale=(1.0, 1.0),                                         # variant scale (horizontal, vertical) about the pivot
)
# Variants: per plant, variant -> (seed offset added to that plant's seed, overrides of its knob table).
# "a" is the reference model (identical to the single model of earlier versions). Lichen overrides are `scale` (size of
# the patch) and `density` (leaves and nodes); the others are knob overrides, nested dicts (fern layers) merge.
VARIANTS = {
    "t1": {"a": (0, dict(scale=1.0, density=1.0)),
           "b": (101, dict(scale=0.85, density=1.15)),          # small and dense
           "c": (202, dict(scale=1.15, density=0.85))},         # large and looser
    "t2": {"a": (0, {}),
           "b": (101, dict(scale=(1.15, 1.15), bell_open=1.7, berries=2, leaf_rise=0.85, mound_r=0.12, mound_h=0.030)),
           "c": (202, dict(scale=(0.85, 0.85), bud_stems=2, berries=4, leaf_rise=1.15, mound_r=0.10, mound_h=0.042))},
    "t3": {"a": (0, {}),
           "b": (101, dict(scale=(1.2, 0.9), outer=dict(n=7), inner=dict(n=3), embers=6, columns=7)),
           "c": (202, dict(scale=(1.15, 1.15), outer=dict(n=5), columns=5,
                           crozier_h=(0.21, 0.17), crozier_r=0.032, crozier_w=0.0085))},
}
# =============================================================================

scene = reset()

def Builder():
    return _Builder(MATS)

def finish_plant(B, name, anchors, t):
    """Finish the mesh, apply the variant scale (horizontal, vertical) about the pivot, add the anchor empties."""
    ob = B.finish(name)
    sh, sv = t.get("scale", (1.0, 1.0))
    if (sh, sv) != (1.0, 1.0):
        for v in ob.data.vertices: v.co = Vector((v.co.x*sh, v.co.y*sh, v.co.z*sv))
    return ob, [empty(n, (p[0]*sh, p[1]*sh, p[2]*sv), ob) for n, p in anchors]

# ---------------------------------------------------------------- Tier I
# Frame: the patch lies on a surface facing -Y (outward normal; Unity +Z after the FBX export), pivot = patch centre on
# the surface (origin). Curved variant: a vertical trunk of radius trunk_r whose axis is at (0, +trunk_r, 0).
# Flat variant: the plane y = 0 (plank, wall, cultivation).
# Growth: one skeleton of runners radiating from the centre is generated once; stage s keeps the first
# T1["stage_runners"][s] runners (with their branches) cut at T1["stage_reach"][s] of their length, so every stage
# contains the previous one and swapping the models in place reads as growth.
def surface_fn(trunk_r):
    def surf(u, z, off=0.0):
        """Point at arc length u (sideways) and height z, `off` metres out from the surface; plus the normal."""
        if not trunk_r:
            return Vector((u, -off, z)), Vector((0, -1, 0))
        th = u/trunk_r; n = Vector((math.sin(th), -math.cos(th), 0))
        return Vector((0, trunk_r, z)) + n*(trunk_r + off), n
    return surf

def strip(B, pts, widths, side, mats):
    """Flat ribbon along pts, width along the vector side (one per point or one for all)."""
    rows = []
    for i, p in enumerate(pts):
        sd = side[i] if isinstance(side, list) else side
        rows.append((p - sd*widths[i]/2, p + sd*widths[i]/2))
    for i in range(len(rows) - 1):
        (a0, a1), (b0, b1) = rows[i], rows[i+1]
        B.face([a0, a1, b1, b0], mats[i])

def face_to(B, vs, m, n):
    """Face whose normal points along n (the mesh is finished without normal recalculation)."""
    a, b, c = (Vector(v) for v in vs[:3])
    if (b - a).cross(c - a).dot(n) < 0: vs = list(reversed(vs))
    B.face(vs, m)

def huldra_skeleton(rng, t=T1):
    """Full-grown layout in (u, z) surface coordinates with all random choices made up front."""
    step = t["step"]
    runners = []
    for k, h0 in enumerate(t["headings"]):
        h = math.radians(h0 + rng.uniform(-12, 12))
        s, c = math.sin(h), math.cos(h)
        rz = t["reach_up"] if s > 0 else t["reach_down"]
        reach = math.hypot(t["reach_side"]*c, rz*s)*rng.uniform(0.65, 1.0)     # irregular silhouette
        pts = [(math.cos(h)*0.008, math.sin(h)*0.008)]; dist = [0.008]
        while dist[-1] < reach:
            h += math.radians(rng.uniform(-22, 22))
            u, z = pts[-1]
            pts.append((u + math.cos(h)*step, z + math.sin(h)*step)); dist.append(dist[-1] + step)
        runners.append(dict(pts=pts, dist=dist, rank=k, main=True, L=dist[-1]))
        for b in range(t["branches_per_runner"]):              # side branches off this runner
            if len(pts) < 4: break
            i0 = rng.randint(2, len(pts) - 2)
            hb = math.atan2(pts[i0][1] - pts[i0-1][1], pts[i0][0] - pts[i0-1][0]) + math.radians(rng.choice((-1, 1))*rng.uniform(35, 65))
            bp, bd = [pts[i0]], [dist[i0]]
            n = max(2, int((dist[-1] - dist[i0])*rng.uniform(0.5, 0.8)/step))
            for _ in range(n):
                hb += math.radians(rng.uniform(-20, 20))
                bp.append((bp[-1][0] + math.cos(hb)*step*0.85, bp[-1][1] + math.sin(hb)*step*0.85)); bd.append(bd[-1] + step*0.85)
            runners.append(dict(pts=bp, dist=bd, rank=k, main=False, L=dist[-1]))
    # nodes (leaf clusters, beards) every node_step along each runner, with their random attributes
    nodes = []
    R = max(t["reach_up"], t["reach_side"])
    for ri, r in enumerate(runners):
        d = r["dist"][0] + (0.0 if r["main"] else t["node_step"]*0.5)
        while d <= r["dist"][-1]:
            u, z = path_at(r, d)
            near = 1 - min(1.0, math.hypot(u, z)/R)                 # 1 at the centre, 0 at the rim
            nl = max(1, round(t["leaves_min"] + (t["leaves_max"] - t["leaves_min"])*near**1.3))
            leaves = [dict(ang=rng.uniform(0, 2*math.pi), lift=math.radians(rng.uniform(8, 35)),
                           L=t["leaf_len"]*rng.uniform(0.7, 1.15)*(0.7 + 0.7*near), w=t["leaf_w"]*rng.uniform(0.8, 1.2)*(0.8 + 0.5*near),
                           shade=rng.random() < 0.45) for _ in range(nl)]
            beard = None
            if z < t["beard_zmax"] and rng.random() < t["beard_p"]*(0.5 + near):
                beard = [dict(L=rng.uniform(*t["beard_len"]), w=rng.uniform(*t["beard_w"]), sway=rng.uniform(-0.005, 0.005),
                              lift=rng.uniform(0.004, 0.010), du=rng.uniform(-0.005, 0.005), glint=False)
                         for _ in range(rng.randint(*t["beard_strands"]))]
            nodes.append(dict(r=ri, d=d, u=u, z=z, leaves=leaves, beard=beard, rank=r["rank"], L=r["L"]))
            d += t["node_step"]
    # centre rosette: the germination point, a dense ring of leaves present from the first stage on
    rosette = [dict(ang=2*math.pi*k/t["rosette"] + rng.uniform(-0.25, 0.25), lift=math.radians(rng.uniform(10, 30)),
                    L=t["leaf_len"]*rng.uniform(0.9, 1.25), w=t["leaf_w"]*rng.uniform(0.9, 1.2), shade=k % 2 == 0)
               for k in range(t["rosette"])]
    nodes.insert(0, dict(r=0, d=0.0, u=0.0, z=0.0, leaves=rosette, rank=0, L=runners[0]["L"],
                         beard=[dict(L=rng.uniform(*t["beard_len"]), w=rng.uniform(*t["beard_w"]), sway=rng.uniform(-0.005, 0.005),
                                     lift=rng.uniform(0.004, 0.010), du=rng.uniform(-0.008, 0.008), glint=False) for _ in range(2)]))
    strands = [s for n in nodes if n["beard"] for s in n["beard"]]
    for s in rng.sample(strands, min(t["glints"], len(strands))): s["glint"] = True
    return runners, nodes

def path_at(r, d):
    """(u, z) at distance d along runner r (clamped)."""
    ds, ps = r["dist"], r["pts"]
    if d <= ds[0]: return ps[0]
    for i in range(len(ds) - 1):
        if ds[i] <= d <= ds[i+1]:
            f = (d - ds[i])/(ds[i+1] - ds[i])
            return (ps[i][0] + (ps[i+1][0] - ps[i][0])*f, ps[i][1] + (ps[i+1][1] - ps[i][1])*f)
    return ps[-1]

def huldra(skel, stage, flat=False, name=None, t=T1):
    """Mesh for growth stage `stage` (0-based) of the skeleton."""
    B = Builder(); surf = surface_fn(None if flat else t["trunk_r"]); off = t["surface_off"]
    runners, nodes = skel
    nr, g = t["stage_runners"][stage], t["stage_reach"][stage]
    for r in runners:
        if r["rank"] >= nr: continue
        cut = g*r["L"]
        ds = [d for d in r["dist"] if d < cut]
        if len(ds) < 1: continue
        ds = ds + [cut]
        if ds[-1] - ds[0] < 0.004: continue
        P, N = zip(*[surf(*path_at(r, d), off) for d in ds])
        n = len(P); w0 = t["runner_w"]*(1.0 if r["main"] else 0.7)
        rows = []
        for i in range(n):
            tg = (P[min(i+1, n-1)] - P[max(i-1, 0)]).normalized()
            sd = tg.cross(N[i]).normalized()
            w = max(0.0012, w0*(1 - 0.75*ds[i]/cut)*(0.55 + 0.45*g))   # runners thicken as the patch grows
            rows.append((P[i] - sd*w/2, P[i] + N[i]*w*0.5, P[i] + sd*w/2, N[i]))
        for i, ((al, at, ar, an), (bl, bt, br, bn)) in enumerate(zip(rows, rows[1:])):
            if r["main"] and ds[i] < t["ridge_until"]:      # raised ridge only near the centre, flat beyond
                face_to(B, [al, bl, bt, at], "huldra_runner", an)
                face_to(B, [at, bt, br, ar], "huldra_runner", an)
            else:
                face_to(B, [al, bl, br, ar], "huldra_runner", an)
    for nd in nodes:
        if nd["rank"] >= nr: continue
        cut = g*nd["L"]
        if nd["d"] > cut: continue
        m = min(1.0, 0.4 + (cut - nd["d"])/t["mature_dist"])      # young leaves at the growth front are small
        p, nrm = surf(nd["u"], nd["z"], off)
        tu = (surf(nd["u"] + 0.001, nd["z"], off)[0] - p).normalized(); tz = nrm.cross(tu).normalized()
        if tz.z < 0: tz = -tz
        for li, lf in enumerate(nd["leaves"]):
            if m < 0.45 and li >= 2 and nd["d"] > 0: break
            d = tu*math.cos(lf["ang"]) + tz*math.sin(lf["ang"])
            ax = (d*math.cos(lf["lift"]) + nrm*math.sin(lf["lift"])).normalized()
            sd = ax.cross(nrm).normalized(); L, lw = lf["L"]*m, lf["w"]*m
            base = p + nrm*0.0015; mid = base + ax*L*0.45; tip = base + ax*L
            face_to(B, [base, mid - sd*lw/2, mid + sd*lw/2], "huldra_strand_shade" if lf["shade"] else "huldra_strand", nrm)
            face_to(B, [mid + sd*lw/2, mid - sd*lw/2, tip], "huldra_tip", nrm)
        if nd["beard"] and nd["d"] <= cut - t["beard_delay"]:
            bm = min(1.0, 0.4 + (cut - t["beard_delay"] - nd["d"])/t["mature_dist"])
            for s in nd["beard"]:
                L = s["L"]*bm; segs = 3
                a = p + nrm*s["lift"] + tu*s["du"]
                pts = [p + nrm*0.002] + [a + Vector((0, 0, -L*k/segs)) + tu*s["sway"]*math.sin(math.pi*k/segs) for k in range(segs + 1)]
                wd = [s["w"]*f for f in (0.8, 1.0, 0.85, 0.6, 0.3)]
                mats = ["huldra_strand_shade", "huldra_strand", "huldra_strand", "huldra_glint" if s["glint"] else "huldra_tip"]
                for i in range(len(pts) - 1):              # double-sided card: both windings
                    q = [pts[i] - tu*wd[i]/2, pts[i] + tu*wd[i]/2, pts[i+1] + tu*wd[i+1]/2, pts[i+1] - tu*wd[i+1]/2]
                    face_to(B, q, mats[i], nrm); face_to(B, q, mats[i], -nrm)
    ob = B.finish(name or f"plant_t1_s{stage + 1}", recalc=False)
    pick = empty("PickAnchor", surf(0, 0, t["pick_off"])[0], ob)
    return ob, [pick]

# ---------------------------------------------------------------- Tier II
def bell(B, base, axis, length, radius, petals, open_=1.0, rng=None, glow=True):
    """Hanging teardrop bell: base at the stem tip, axis points out of the mouth."""
    ax = Vector(axis).normalized()
    ref = Vector((0, 0, 1)) if abs(ax.z) < 0.9 else Vector((1, 0, 0))
    u = (ref - ax*ref.dot(ax)).normalized(); v = ax.cross(u)
    prof = [(0.25, 0.0), (0.85, 0.30), (1.0, 0.62), (0.95*open_ + 0.35*(1-open_), 1.0)]   # (radius frac, length frac)
    half = math.pi/petals*(0.62 if glow else 0.95)   # open flowers leave gaps that show the glowing cup
    for p in range(petals):
        a0 = 2*math.pi*p/petals
        rows = []
        for rf, lf in prof:
            r = radius*rf; L = length*lf
            row = []
            for k, da in enumerate((-half*(0.5 if lf in (0.0, 1.0) else 1.0), 0.0, half*(0.5 if lf in (0.0, 1.0) else 1.0))):
                a = a0 + da; rr = r*(1.08 if k == 1 else 1.0)
                row.append(Vector(base) + ax*L + rr*(math.cos(a)*u + math.sin(a)*v))
            rows.append(row)
        rows[-1] = [rows[-1][1] + ax*length*0.12]*1   # pointed petal tip
        for r0, r1 in zip(rows[:-2], rows[1:-1]):
            B.face([r0[0], r0[1], r1[1], r1[0]], "baldr_petal")
            B.face([r0[1], r0[2], r1[2], r1[1]], "baldr_petal")
        r1 = rows[-2]; tip = rows[-1][0]
        B.face([r1[0], r1[1], tip], "baldr_petal"); B.face([r1[1], r1[2], tip], "baldr_petal")
    # green cap where the bell meets the stem
    B.tube([Vector(base) - ax*0.006, Vector(base) + ax*0.012], [0.004, radius*0.3], 6, "baldr_stem")
    if glow:
        # glowing pistil ending in a teardrop that hangs just out of the mouth ("Baldr's tear")
        B.tube([Vector(base) + ax*length*0.1, Vector(base) + ax*length*0.4, Vector(base) + ax*length*0.72],
               [radius*0.25, radius*0.55, radius*0.5], 6, "baldr_glow", cap=True)   # inner glowing cup
        B.tube([Vector(base) + ax*length*0.3, Vector(base) + ax*length*1.2], [0.0025, 0.0025], 3, "baldr_glow")
        tear = Vector(base) + ax*length*1.3
        B.tube([tear - ax*0.012, tear - ax*0.004, tear + ax*0.006, tear + ax*0.011],
               [0.0, 0.0055, 0.006, 0.0], 5, "baldr_glow")

def baldr(rng, t=T2, name="plant_t2", picked=False):
    """picked=True: what remains after harvesting (mound, leaves, a cut stem)."""
    B = Builder()
    # snow mound (lathe, jittered)
    segs = 9; prof = [(0.0, t["mound_h"]), (0.05, t["mound_h"]*0.85), (0.09, t["mound_h"]*0.45), (t["mound_r"], 0.0), (t["mound_r"]*0.9, -0.01)]
    rings = []
    for r, z in prof:
        if r == 0: rings.append([B.bm.verts.new((0, 0, z))]); continue
        rings.append([B.bm.verts.new((r*(1+rng.uniform(-0.12, 0.12))*math.cos(2*math.pi*k/segs),
                                      r*(1+rng.uniform(-0.12, 0.12))*math.sin(2*math.pi*k/segs), z + rng.uniform(-0.004, 0.004)))
                      for k in range(segs)])
    for a, c in zip(rings, rings[1:]):
        for k in range(segs):
            j = (k+1) % segs
            B.face([a[0], c[k], c[j]] if len(a) == 1 else [a[k], c[k], c[j], a[j]], "baldr_snow")
    H = t["stem_h"]; nod = t["nod"]
    # main stem: up, then arches over so the bell nods
    ctrl = [(0.0, 0.0, 0.02), (0.005, 0.0, H*0.45), (0.0, 0.0, H*0.85), (0.035*nod/0.6, 0.0, H*1.0), (0.07*nod/0.6, 0.0, H*0.97)]
    if picked:      # cut stem: a short straight stub with a slanted, capped end
        B.tube([Vector((0.0, 0.0, 0.02)), Vector((0.003, 0.0, t["cut_h"]*0.6)), Vector((0.005, 0.0, t["cut_h"]))],
               [t["stem_r"]]*3, 4, "baldr_stem", cap=True)
    else:
        stem = bezier(ctrl, 9)
        B.tube(stem, [t["stem_r"]]*8 + [t["stem_r"]*0.7], 4, "baldr_stem")
        tip = stem[-1]; ax = (stem[-1] - stem[-2]).normalized()
        ax = (ax + Vector((0, -0.6, -0.8))).normalized()   # face the mouth a little to the viewer
        bell(B, tip, ax, t["bell_len"], t["bell_r"], t["petals"], t["bell_open"], rng)
        # shorter stems with closed buds (the second one turned round the stem)
        h2 = t["bud_stem_h"]
        for b in range(t["bud_stems"]):
            rot = Matrix.Rotation(2.3*b, 3, 'Z'); hb = h2*(1 - 0.18*b)
            stem2 = [rot @ p for p in bezier([(0.012, 0.01, 0.02), (0.02, 0.02, hb*0.5), (0.0, 0.035, hb*0.95), (-0.025, 0.045, hb*0.92)], 7)]
            B.tube(stem2, [t["stem_r"]*0.8]*6 + [t["stem_r"]*0.6], 4, "baldr_stem")
            ax2 = ((stem2[-1] - stem2[-2]).normalized() + Vector((0, 0, -1.2))).normalized()
            bell(B, stem2[-1], ax2, t["bell_len"]*0.6, t["bell_r"]*0.55, t["petals"], 0.0, rng, glow=False)
    # narrow frosted leaves from the base, arching outwards
    for i in range(t["leaves"]):
        ang = 2*math.pi*i/t["leaves"] + rng.uniform(-0.3, 0.3)
        d = Vector((math.cos(ang), math.sin(ang), 0)); side = Vector((-d.y, d.x, 0))
        L = rng.uniform(*t["leaf_len"]); w = t["leaf_w"]
        rs = t["leaf_rise"]                # >1 steeper leaves, <1 flatter
        ctrl = [Vector((0, 0, 0.025)) + d*0.008, d*L*0.15/rs + Vector((0, 0, L*0.55*rs)), d*L*0.45/rs + Vector((0, 0, L*0.75*rs)), d*L*0.7/rs + Vector((0, 0, L*0.55*rs))]
        c = bezier(ctrl, 6)
        wid = [w*0.6, w, w, w*0.8, w*0.45]
        for k in range(len(c)-2):
            B.face([c[k] - side*wid[k], c[k] + side*wid[k], c[k+1] + side*wid[k+1], c[k+1] - side*wid[k+1]], "baldr_leaf")
        B.face([c[-2] - side*wid[-1], c[-2] + side*wid[-1], c[-1]], "baldr_leaf")
    # mistletoe-like berry cluster at the stem base on short forked twigs
    if picked: return finish_plant(B, name, [("PickAnchor", (0.0, 0.0, t["cut_h"]*0.5))], t)
    bc = Vector((-0.02, -0.05, 0.04))
    B.tube([Vector((0, 0, 0.025)), bc + Vector((0.008, 0, 0.004))], [0.003, 0.002], 3, "baldr_stem")
    for i in range(t["berries"]):
        a = 2*math.pi*i/t["berries"] + 0.4
        p = bc + Vector((math.cos(a)*0.014, math.sin(a)*0.014, 0.006*(i % 2)))
        B.ico(p, (t["berry_r"],)*3, "baldr_berry", 1, 0.06, rng)
    return finish_plant(B, name, [("PickAnchor", (0.0, 0.0, H*0.35))], t)

# ---------------------------------------------------------------- Tier III
def leaflet(B, qa, qb, out, tan, ll, m, teeth):
    """Leaflet triangle qa-tip-qb with one saw tooth on each edge (teeth point toward the tip)."""
    tip = (qa + qb)/2 + out*ll
    B.face([qa, tip, qb], m)
    if teeth:
        for e0, sgn in ((qa, -1), (qb, 1)):
            p0, p1 = e0.lerp(tip, 0.25), e0.lerp(tip, 0.62)
            B.face([p0, p1 + tan*sgn*ll*0.16 - out*ll*0.02, p1], m)
    return tip

def frond(B, rng, L, d, start, a0, a1, segs, nl, l_len, spores, fwd, t=T3):
    """Arching frond along azimuth d from start; returns (tips, rachis points)."""
    p = start.copy(); pts = [p.copy()]
    for s in range(segs):
        a = a0 + (a1 - a0)*(s + 0.5)/segs
        p = p + (d*math.cos(a) + Vector((0, 0, math.sin(a))))*L/segs; pts.append(p.copy())
    B.tube(pts, [t["stem_r"]*(1 - 0.85*s/segs) for s in range(segs)] + [0.0], 3, "helfern_stem")
    side = Vector((-d.y, d.x, 0))
    def at(s):
        k = s*segs; j = min(int(k), segs-1)
        return pts[j].lerp(pts[j+1], k - j), (pts[j+1] - pts[j]).normalized()
    s0, s1 = 0.16, 0.97; ds = (s1 - s0)/nl
    tips = []
    for i in range(nl):
        s = s0 + ds*i
        qa, tan = at(s); qb, _ = at(s + ds*0.9)
        nf = tan.cross(side).normalized()                  # upper side of the frond
        ll = l_len*(1 - 0.75*(i/(nl - 1)))*min(1.0, 0.55 + 2.5*s)   # largest near the base, shrinking to the tip
        for sg in (1, -1):
            out = (side*sg + tan*fwd - nf*t["leaflet_droop"]).normalized()
            tip = leaflet(B, qa, qb, out, tan, ll, "helfern_frond", ll > t["teeth_min"])
            tips.append(tip)
            if spores and i in spores and sg == (1 if i % 2 else -1):
                c = (qa + qb + tip)/3 - nf*0.006           # glowing capsule under the leaflet
                B.octa(c, t["spore_r"]*rng.uniform(0.8, 1.15), "helfern_spore")
    return tips, pts

def crozier(B, base, d, h, R0, turns, r):
    """Fiddlehead: stem rising to h, then curling forward (along d) into a tightening spiral."""
    up = Vector((0, 0, 1))
    top = base + up*h
    pts = [base, base + up*h*0.5 + d*0.004, top]
    n = 9; c = top + d*R0
    for i in range(1, n + 1):
        f = i/n; ph = f*turns*2*math.pi; rr = R0*(1 - 0.72*f)
        pts.append(c + rr*(-math.cos(ph)*d + math.sin(ph)*up))
    radii = [r, r*0.95, r*0.9] + [r*(0.85 - 0.5*i/n) for i in range(1, n)] + [0.0]
    B.tube(pts, radii, 3, "helfern_crozier")

def helfern(rng, t=T3, name="plant_t3", picked=False):
    """picked=True: root crown and basalt with a dim heart and two short young fronds."""
    B = Builder()
    # cracked basalt: hex columns over a lava-glow plate
    B.prism((0, 0, 0), t["crack_r"], 0.012, 6, "helfern_crack", rot=0.2)
    nc = t["columns"]
    centers = [(0.085*math.cos(2*math.pi*k/nc + 0.3), 0.085*math.sin(2*math.pi*k/nc + 0.3)) for k in range(nc)]
    for x, y in centers:
        B.prism((x, y, 0), rng.uniform(0.033, 0.041), rng.uniform(0.028, 0.048), 6, "helfern_basalt", rot=rng.uniform(0, 1))
    B.prism((0, 0, 0), 0.045, 0.040, 6, "helfern_basalt", rot=0.4)      # centre column under the crown
    # charred root crown: open cup holding the heart, roots crawling over the columns into the cracks
    cz = t["crown_z"]
    B.lathe([(0.016, cz + 0.006), (0.034, cz - 0.002), (0.042, cz - 0.018), (0.038, cz - 0.034)],
            "helfern_root", 7, 0.08, rng, 0.2)
    for k in range(t["roots"]):
        az = 2*math.pi*k/t["roots"] + rng.uniform(-0.25, 0.25)
        d = Vector((math.cos(az), math.sin(az), 0)); L = rng.uniform(*t["root_len"])
        bend = Vector((-d.y, d.x, 0))*rng.uniform(-0.02, 0.02)
        pts = [d*0.03 + Vector((0, 0, cz - 0.012)), d*L*0.45 + bend + Vector((0, 0, cz - 0.004 + rng.uniform(0, 0.006))),
               d*L*0.8 - bend + Vector((0, 0, 0.040)), d*L + Vector((0, 0, 0.006))]
        B.tube(pts, [0.012, 0.009, 0.006, 0.002], 3, "helfern_root")
    # glowing heart in the crown
    B.ico((0, 0, t["heart_z"]), (t["heart_r"],)*3, "helfern_heart_dim" if picked else "helfern_heart", 1, 0.12, rng)
    # fronds: outer arching layer, inner steeper layer (offset half a step)
    tips = []
    layers = ((t["young"], None),) if picked else ((t["outer"], t["spore_leaflets"]), (t["inner"], None))
    for layer, spores in layers:
        n = layer["n"]
        for f in range(n):
            az = 2*math.pi*(f + layer["phase"])/n + rng.uniform(-0.2, 0.2)
            d = Vector((math.cos(az), math.sin(az), 0))
            a0, a1 = (math.radians(x) for x in layer["angle"]); a0 -= math.radians(rng.uniform(0, 8))
            start = d*layer["start_r"] + Vector((0, 0, cz))
            tp, _ = frond(B, rng, rng.uniform(*layer["len"]), d, start, a0, a1, layer["segs"], layer["leaflets"],
                          layer["leaflet_len"], spores, layer["fwd"], t)
            tips += tp
    if picked: return finish_plant(B, name, [("PickAnchor", (0.0, 0.0, cz))], t)
    # two young fiddleheads in the centre
    for k, (h, az) in enumerate(zip(t["crozier_h"], t["crozier_az"])):
        d = Vector((math.cos(math.radians(az)), math.sin(math.radians(az)), 0))
        crozier(B, d*0.014 + Vector((0, 0, cz - 0.004)), d, h, t["crozier_r"]*(1 - 0.2*k), t["crozier_turns"], t["crozier_w"])
    for i in range(t["embers"]):
        tip = tips[rng.randrange(len(tips))]
        B.octa(tip + Vector((0, 0, 0.004)), 0.004, "helfern_ember")
    return finish_plant(B, name, [("PickAnchor", (0.0, 0.0, cz)),
                                  ("EmberAnchor", (0.0, 0.0, t["heart_z"] + t["ember_anchor_dz"]))], t)

# ---------------------------------------------------------------- build + export
def merged(base, over):
    out = dict(base)
    for k, v in over.items():
        out[k] = merged(base[k], v) if isinstance(v, dict) and isinstance(base.get(k), dict) else v
    return out

def lichen_params(scale, density):
    t = dict(T1)
    for k in ("reach_up", "reach_side", "reach_down", "leaf_len", "leaf_w"): t[k] = T1[k]*scale
    t["beard_len"] = tuple(x*scale for x in T1["beard_len"])
    t["node_step"] = T1["node_step"]/density
    t["leaves_max"] = T1["leaves_max"]*density
    t["rosette"] = round(T1["rosette"]*density)
    return t

plants = {}
for v, (off, o) in VARIANTS["t1"].items():
    tv = lichen_params(o["scale"], o["density"])
    sk = huldra_skeleton(random.Random(SEED + off), tv)
    if v == "a":                                                  # growth stages: one variant
        for st in range(2):
            plants[f"t1_s{st + 1}"] = huldra(sk, st, t=tv)
        skel = sk
    plants[f"t1_s3_{v}"] = huldra(sk, 2, name=f"plant_t1_s3_{v}", t=tv)
    plants[f"t1_flat_{v}"] = huldra(sk, 2, flat=True, name=f"plant_t1_flat_{v}", t=tv)   # full stage on a flat surface
for k, fn, base, i in (("t2", baldr, T2, 1), ("t3", helfern, T3, 2)):
    for v, (off, o) in VARIANTS[k].items():
        plants[f"{k}_{v}"] = fn(random.Random(SEED + i + off), merged(base, o), f"plant_{k}_{v}")
    plants[f"{k}_picked"] = fn(random.Random(SEED + i), base, f"plant_{k}_picked", picked=True)

log = []
for k, (ob, kids) in plants.items():
    xs = [v.co.x for v in ob.data.vertices]; ys = [v.co.y for v in ob.data.vertices]
    zs = [v.co.z for v in ob.data.vertices]
    log.append(f"TRIS plant_{k}: {tri_count(ob)}  z {min(zs):.3f}..{max(zs):.3f} m  spread {max(xs)-min(xs):.2f} x {max(ys)-min(ys):.2f} m")
    export_fbx(os.path.join(OUT, f"plant_{k}.fbx"), ob)
# compatibility files: variant a exported again under the earlier file and object names (stable for Unity imports)
for fname, src, objname in (("plant_t1_s3", "t1_s3_a", "plant_t1_s3"), ("plant_t1", "t1_s3_a", "plant_t1_s3"),
                            ("plant_t1_flat", "t1_flat_a", "plant_t1_flat"), ("plant_t2", "t2_a", "plant_t2"),
                            ("plant_t3", "t3_a", "plant_t3")):
    ob = plants[src][0]; old = ob.name
    ob.name = objname; ob.data.name = objname
    export_fbx(os.path.join(OUT, f"{fname}.fbx"), ob)
    ob.name = old; ob.data.name = old
    log.append(f"{fname}.fbx = plant_{src} (object named {objname})")
write_log("plants", log)

# ---- preview (1280 x 1260), one row per plant:
# lichen s1 s2 s3a s3b s3c on five 0.5 m trunks | Baldr's Tear a b c picked | Helfern a b c picked ----
from common import mat
import bpy
PATCH_Z, TR = 1.30, T1["trunk_r"]
trunk_mat = mat("trunk_ref", (0.20, 0.19, 0.18, 1), rough=0.9)
def on_trunk(ob, x, z, turn=0.0):
    """Place a trunk-curved patch on the trunk whose axis is at (x, TR), turned `turn` radians around that axis."""
    ob.rotation_euler.z = turn
    ob.location = Vector((x, TR, z)) + Matrix.Rotation(turn, 3, 'Z') @ Vector((0, -TR, 0))
row1 = ["t1_s1", "t1_s2", "t1_s3_a", "t1_s3_b", "t1_s3_c"]
X1, DX1, Y2, DX2, Y3, DX3 = -10.0, 0.58, 4.0, 0.45, 8.0, 0.95
trunks = []
for k, key in enumerate(row1):
    x = X1 + (k - 2)*DX1
    bpy.ops.mesh.primitive_cylinder_add(vertices=24, radius=TR, depth=2.2, location=(x, TR, 1.1))
    o = bpy.context.active_object; o.name = f"trunk_ref_{k}"; o.data.materials.append(trunk_mat); trunks.append(o)
    on_trunk(plants[key][0], x, PATCH_Z)
row2 = ["t2_a", "t2_b", "t2_c", "t2_picked"]
for k, key in enumerate(row2):
    plants[key][0].location = ((k - 1.5)*DX2, Y2, 0); plants[key][0].rotation_euler.z = -1.0
row3 = ["t3_a", "t3_b", "t3_c", "t3_picked"]
for k, key in enumerate(row3):
    plants[key][0].location = ((k - 1.5)*DX3, Y3, 0); plants[key][0].rotation_euler.z = 0.75
for key in ("t1_flat_a", "t1_flat_b", "t1_flat_c"):              # not in this preview (see v4 for the plank view)
    plants[key][0].location = (0, -30, 0)
g = ground(40.0)
r1 = ruler((X1 - 2.5*DX1, 0.0, 0), height=2.0, band=0.5, r=0.015)
r2 = ruler((-2.25*DX2, Y2 + 0.1, 0))
r3 = ruler((-1.95*DX3, Y3 - 0.3, 0))
def row_lights(c, tag, e=160):
    c = Vector(c)
    return [light("key_" + tag, 'AREA', tuple(c + Vector((1.5, -2.0, 2.0))), e, (1.0, 0.85, 0.7), 1.5, tuple(c)),
            light("fill_" + tag, 'AREA', tuple(c + Vector((-2.2, -1.5, 1.0))), e*0.25, (0.7, 0.8, 1.0), 2.0, tuple(c)),
            light("rim_" + tag, 'AREA', tuple(c + Vector((0.0, 2.0, 1.6))), e*0.5, (1.0, 0.9, 0.8), 1.5, tuple(c))]
L1 = row_lights((X1, 0, PATCH_Z), "lichen", 110)
L2 = row_lights((0, Y2, 0.2), "baldr")
L3 = row_lights((0, Y3, 0.2), "fern")
sky = light("sky", 'SUN', (0.0, 0.0, 5.0), 1.2, (0.9, 0.92, 1.0))
world((0.16, 0.155, 0.15, 1))
both = [g, sky]
render_panels([
    dict(height=420, panels=[dict(width=1280, cam=((X1 - 0.05, -3.55, PATCH_Z + 0.12), (X1 - 0.05, 0, PATCH_Z + 0.04), 42),
         show=both + trunks + [plants[k][0] for k in row1] + [r1] + L1)]),
    dict(height=420, panels=[dict(width=1280, cam=((-0.05, Y2 - 2.2, 0.5), (-0.05, Y2, 0.22), 40),
         show=both + [plants[k][0] for k in row2] + [r2] + L2)]),
    dict(height=420, panels=[dict(width=1280, cam=((-0.1, Y3 - 4.3, 1.1), (-0.1, Y3, 0.20), 40),
         show=both + [plants[k][0] for k in row3] + [r3] + L3)]),
], "preview-plants.png", PREVIEW, "plants.blend")
