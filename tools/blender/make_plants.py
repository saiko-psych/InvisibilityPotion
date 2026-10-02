"""Procedural low-poly ingredient plants, one per biome. Run: blender -b --python make_plants.py

Tier I   Black Forest  Huldra's Hair (Huldrelokk)   rare creeping lichen on fir/pine bark (like Ashvine on walls): thin
                                                     runners, leaf tufts, short hanging beards; trunk and flat variants
Tier II  Mountains     Baldr's Tear (Baldrsgrat)    nodding white-blue snow flower with mistletoe berries
Tier III Ashlands      Hel's Ember Fern (Helfern)   black fern, two frond layers, fiddleheads, ember spore capsules,
                                                     glowing heart in a charred root crown on cracked basalt
Lore: docs/ideas/2026-10-01-norse-lore-research.md section 3.
"""
import math, os, random, shutil, sys
from mathutils import Vector
from mathutils.bvhtree import BVHTree
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from common import OUT, HERE, Builder as _Builder, bezier, empty, export_fbx, tri_count, write_log, reset, \
    ground, ruler, light, camera, world, render_panels

PREVIEW = os.path.join(HERE, "preview-plants-v4.png")

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
)
# =============================================================================

scene = reset()

def Builder():
    return _Builder(MATS)

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

def huldra_skeleton(rng):
    """Full-grown layout in (u, z) surface coordinates with all random choices made up front."""
    t = T1; step = t["step"]
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

def huldra(skel, stage, flat=False, name=None):
    """Mesh for growth stage `stage` (0-based) of the skeleton."""
    t = T1; B = Builder(); surf = surface_fn(None if flat else t["trunk_r"]); off = t["surface_off"]
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

def baldr(rng):
    t = T2; B = Builder()
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
    stem = bezier(ctrl, 9)
    B.tube(stem, [t["stem_r"]]*8 + [t["stem_r"]*0.7], 4, "baldr_stem")
    tip = stem[-1]; ax = (stem[-1] - stem[-2]).normalized()
    ax = (ax + Vector((0, -0.6, -0.8))).normalized()   # face the mouth a little to the viewer
    bell(B, tip, ax, t["bell_len"], t["bell_r"], t["petals"], 1.0, rng)
    # second shorter stem with a closed bud
    h2 = t["bud_stem_h"]
    stem2 = bezier([(0.012, 0.01, 0.02), (0.02, 0.02, h2*0.5), (0.0, 0.035, h2*0.95), (-0.025, 0.045, h2*0.92)], 7)
    B.tube(stem2, [t["stem_r"]*0.8]*6 + [t["stem_r"]*0.6], 4, "baldr_stem")
    ax2 = ((stem2[-1] - stem2[-2]).normalized() + Vector((0, 0, -1.2))).normalized()
    bell(B, stem2[-1], ax2, t["bell_len"]*0.6, t["bell_r"]*0.55, t["petals"], 0.0, rng, glow=False)
    # narrow frosted leaves from the base, arching outwards
    for i in range(t["leaves"]):
        ang = 2*math.pi*i/t["leaves"] + rng.uniform(-0.3, 0.3)
        d = Vector((math.cos(ang), math.sin(ang), 0)); side = Vector((-d.y, d.x, 0))
        L = rng.uniform(*t["leaf_len"]); w = t["leaf_w"]
        ctrl = [Vector((0, 0, 0.025)) + d*0.008, d*L*0.15 + Vector((0, 0, L*0.55)), d*L*0.45 + Vector((0, 0, L*0.75)), d*L*0.7 + Vector((0, 0, L*0.55))]
        c = bezier(ctrl, 6)
        wid = [w*0.6, w, w, w*0.8, w*0.45]
        for k in range(len(c)-2):
            B.face([c[k] - side*wid[k], c[k] + side*wid[k], c[k+1] + side*wid[k+1], c[k+1] - side*wid[k+1]], "baldr_leaf")
        B.face([c[-2] - side*wid[-1], c[-2] + side*wid[-1], c[-1]], "baldr_leaf")
    # mistletoe-like berry cluster at the stem base on short forked twigs
    bc = Vector((-0.02, -0.05, 0.04))
    B.tube([Vector((0, 0, 0.025)), bc + Vector((0.008, 0, 0.004))], [0.003, 0.002], 3, "baldr_stem")
    for i in range(t["berries"]):
        a = 2*math.pi*i/t["berries"] + 0.4
        p = bc + Vector((math.cos(a)*0.014, math.sin(a)*0.014, 0.006*(i % 2)))
        B.ico(p, (t["berry_r"],)*3, "baldr_berry", 1, 0.06, rng)
    ob = B.finish("plant_t2")
    pick = empty("PickAnchor", (0.0, 0.0, H*0.35), ob)
    return ob, [pick]

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

def frond(B, rng, L, d, start, a0, a1, segs, nl, l_len, spores, fwd):
    """Arching frond along azimuth d from start; returns (tips, rachis points)."""
    t = T3
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

def helfern(rng):
    t = T3; B = Builder()
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
    B.ico((0, 0, t["heart_z"]), (t["heart_r"],)*3, "helfern_heart", 1, 0.12, rng)
    # fronds: outer arching layer, inner steeper layer (offset half a step)
    tips = []
    for layer, spores in ((t["outer"], t["spore_leaflets"]), (t["inner"], None)):
        n = layer["n"]
        for f in range(n):
            az = 2*math.pi*(f + layer["phase"])/n + rng.uniform(-0.2, 0.2)
            d = Vector((math.cos(az), math.sin(az), 0))
            a0, a1 = (math.radians(x) for x in layer["angle"]); a0 -= math.radians(rng.uniform(0, 8))
            start = d*layer["start_r"] + Vector((0, 0, cz))
            tp, _ = frond(B, rng, rng.uniform(*layer["len"]), d, start, a0, a1, layer["segs"], layer["leaflets"],
                          layer["leaflet_len"], spores, layer["fwd"])
            tips += tp
    # two young fiddleheads in the centre
    for k, (h, az) in enumerate(zip(t["crozier_h"], t["crozier_az"])):
        d = Vector((math.cos(math.radians(az)), math.sin(math.radians(az)), 0))
        crozier(B, d*0.014 + Vector((0, 0, cz - 0.004)), d, h, t["crozier_r"]*(1 - 0.2*k), t["crozier_turns"], t["crozier_w"])
    for i in range(t["embers"]):
        tip = tips[rng.randrange(len(tips))]
        B.octa(tip + Vector((0, 0, 0.004)), 0.004, "helfern_ember")
    ob = B.finish("plant_t3")
    pick = empty("PickAnchor", (0.0, 0.0, cz), ob)
    ember = empty("EmberAnchor", (0.0, 0.0, t["heart_z"] + t["ember_anchor_dz"]), ob)
    return ob, [pick, ember]

# ---------------------------------------------------------------- build + export
plants = {}
skel = huldra_skeleton(random.Random(SEED))
for st in range(3):
    plants[f"t1_s{st + 1}"] = huldra(skel, st)
plants["t1_flat"] = huldra(skel, 2, flat=True, name="plant_t1_flat")     # full stage on a flat surface
for i, (k, fn) in enumerate((("t2", baldr), ("t3", helfern)), start=1):
    plants[k] = fn(random.Random(SEED + i))

log = []
for k, (ob, kids) in plants.items():
    xs = [v.co.x for v in ob.data.vertices]; ys = [v.co.y for v in ob.data.vertices]
    zs = [v.co.z for v in ob.data.vertices]
    log.append(f"TRIS plant_{k}: {tri_count(ob)}  z {min(zs):.3f}..{max(zs):.3f} m  spread {max(xs)-min(xs):.2f} x {max(ys)-min(ys):.2f} m")
    export_fbx(os.path.join(OUT, f"plant_{k}.fbx"), ob)
shutil.copyfile(os.path.join(OUT, "plant_t1_s3.fbx"), os.path.join(OUT, "plant_t1.fbx"))   # alias of the full stage
log.append("plant_t1.fbx = copy of plant_t1_s3.fbx")
write_log("plants", log)

# ---- preview (1280 x 1080), top row: two full patches overlapping on a trunk | stages 1-3 on three trunks;
# bottom row: full flat patch on a plank | Baldr's Tear + Helfern ----
from common import mat
import bpy
PATCH_Z, TR = 1.30, T1["trunk_r"]
def trunk_ref(x, name):
    bpy.ops.mesh.primitive_cylinder_add(vertices=24, radius=TR, depth=2.2, location=(x, TR, 1.1))
    o = bpy.context.active_object; o.name = name; o.data.materials.append(trunk_mat); return o
trunk_mat = mat("trunk_ref", (0.20, 0.19, 0.18, 1), rough=0.9)
def on_trunk(ob, x, z, turn=0.0):
    """Place a trunk-curved patch on the trunk whose axis is at (x, TR), turned `turn` radians around that axis."""
    from mathutils import Matrix
    ob.rotation_euler.z = turn
    ob.location = Vector((x, TR, z)) + Matrix.Rotation(turn, 3, 'Z') @ Vector((0, -TR, 0))
# A: two full patches on one trunk, the second turned 0.3 m round the trunk and 0.12 m lower
XA, XB, XP = -3.0, -6.0, -9.0
trunkA = trunk_ref(XA, "trunk_a")
pA1 = huldra(skel, 2, name="plant_t1_s3_left")[0]; on_trunk(pA1, XA, PATCH_Z, -0.55)
pA2 = huldra(skel, 2, name="plant_t1_s3_right")[0]; on_trunk(pA2, XA, PATCH_Z - 0.12, 0.65)
# B: stages 1-3 side by side on three trunks
trunksB = [trunk_ref(XB + (k - 1)*0.62, f"trunk_b{k}") for k in range(3)]
for k in range(3): on_trunk(plants[f"t1_s{k + 1}"][0], XB + (k - 1)*0.62, PATCH_Z)
# C: flat full patch on a plank
pF = plants["t1_flat"][0]; pF.location = (XP, 0, PATCH_Z)
bpy.ops.mesh.primitive_cube_add(size=1.0, location=(XP, 0.02, PATCH_Z))
plank = bpy.context.active_object; plank.name = "plank_ref"; plank.scale = (0.60, 0.04, 0.80)
plank.data.materials.append(mat("plank_ref", (0.36, 0.27, 0.18, 1), rough=0.85))
plants["t2"][0].location = (-0.26, 0, 0); plants["t2"][0].rotation_euler.z = -1.0
plants["t3"][0].location = (0.24, 0.10, 0); plants["t3"][0].rotation_euler.z = 0.75
g = ground()
rB = ruler((XB - 0.98, 0.05, 0), height=2.0, band=0.5, r=0.015)      # 2 m in 0.5 m bands
r2 = ruler((-0.05, 0.25, 0))                                          # 0.5 m in 0.1 m bands
L_far = [light("key", 'AREA', (1.5, -2.0, 2.0), 160, (1.0, 0.85, 0.7), 1.5),
         light("fill", 'AREA', (-2.2, -1.5, 1.0), 40, (0.7, 0.8, 1.0), 2.0),
         light("rim", 'AREA', (0.0, 2.0, 1.6), 80, (1.0, 0.9, 0.8), 1.5)]
def side_lights(x, tag, e=70):
    tz = (x, 0, PATCH_Z)
    return [light("key_" + tag, 'AREA', (x + 1.4, -2.0, 2.6), e, (1.0, 0.85, 0.7), 1.5, tz),
            light("fill_" + tag, 'AREA', (x - 2.0, -1.6, 1.4), e*0.26, (0.7, 0.8, 1.0), 2.0, tz)]
LA, LB, LC = side_lights(XA, "a", 55), side_lights(XB, "b", 85), side_lights(XP, "c", 55)
sky = light("sky", 'SUN', (0.0, 0.0, 5.0), 1.2, (0.9, 0.92, 1.0))
world((0.16, 0.155, 0.15, 1))
both = [g, sky]
render_panels([
    dict(height=540, panels=[
        dict(width=480, cam=((XA, -0.95, PATCH_Z + 0.12), (XA, 0, PATCH_Z + 0.10), 40), show=both + [trunkA, pA1, pA2] + LA),
        dict(width=800, cam=((XB, -1.9, PATCH_Z + 0.14), (XB, 0, PATCH_Z + 0.12), 42),
             show=both + trunksB + [plants[f"t1_s{k + 1}"][0] for k in range(3)] + LB)]),
    dict(height=540, panels=[
        dict(width=480, cam=((XP, -0.95, PATCH_Z + 0.1), (XP, 0, PATCH_Z + 0.04), 40), show=both + [plank, pF] + LC),
        dict(width=800, cam=((0.06, -1.6, 0.55), (0.06, 0, 0.20), 40),
             show=both + [plants["t2"][0], plants["t3"][0], r2] + L_far)]),
], "preview-plants.png", PREVIEW, "plants.blend")
