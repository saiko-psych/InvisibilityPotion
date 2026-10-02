"""Procedural low-poly ingredient plants, one per biome. Run: blender -b --python make_plants.py

Tier I   Black Forest  Huldra's Hair (Huldrelokk)   pale lichen strands hanging from a dead branch over a mossy stone
Tier II  Mountains     Baldr's Tear (Baldrsgrat)    nodding white-blue snow flower with mistletoe berries
Tier III Ashlands      Hel's Ember Fern (Helfern)   black fern with ember spore dust on cracked basalt
Lore: docs/ideas/2026-10-01-norse-lore-research.md section 3.
"""
import bpy, bmesh, math, os, random, shutil
from mathutils import Vector, Matrix
from mathutils.bvhtree import BVHTree

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, "out")            # FBX, raw render, .blend (gitignored)
PREVIEW = os.path.join(HERE, "preview-plants-v2.png")
os.makedirs(OUT, exist_ok=True)

# ==== KNOBS (lengths in metres; Blender Z-up, FBX exported Y-up, 1 unit = 1 m, pivot at ground) ====
SEED = 11                # deterministic; each plant uses SEED + tier index

# Material table: name -> (colour RGBA, roughness, metallic, emission strength)
MATS = {
    # Tier I: Huldra's Hair
    "huldra_stone":  ((0.24, 0.24, 0.22, 1), 0.9, 0.0, 0.0),
    "huldra_moss":   ((0.17, 0.22, 0.10, 1), 0.95, 0.0, 0.0),
    "huldra_branch": ((0.20, 0.15, 0.11, 1), 0.9, 0.0, 0.0),
    "huldra_strand": ((0.55, 0.60, 0.50, 1), 0.85, 0.0, 0.0),   # pale grey-green lichen
    "huldra_strand_shade": ((0.44, 0.49, 0.41, 1), 0.9, 0.0, 0.0),  # second shade for depth
    "huldra_tuft":   ((0.24, 0.30, 0.14, 1), 0.95, 0.0, 0.0),
    "huldra_glint":  ((0.40, 0.62, 0.66, 1), 0.5, 0.0, 0.7),   # cold shimmer at strand tips
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
    "helfern_stem":   ((0.06, 0.05, 0.05, 1), 0.8, 0.0, 0.0),
    "helfern_frond":  ((0.055, 0.05, 0.05, 1), 0.75, 0.0, 0.0),  # charcoal leaflets
    "helfern_spore":  ((0.50, 0.11, 0.02, 1), 0.8, 0.0, 0.9),   # spore dust under/along leaflet edges
    "helfern_ember":  ((1.00, 0.33, 0.04, 1), 0.5, 0.0, 2.5),   # ember dots
}

T1 = dict(  # Huldra's Hair, ~0.3 m: a dense lichen beard on a stout dead branch over a mossy stone
    stone_size=(0.13, 0.10, 0.075), stone_sink=0.015,
    branch=[(0.07, 0.04, 0.0), (0.065, 0.035, 0.13), (0.05, 0.02, 0.25), (0.035, 0.01, 0.31)],
    branch_r=[0.028, 0.024, 0.019, 0.0],
    arm=[(0.06, 0.025, 0.25), (-0.01, 0.012, 0.285), (-0.10, 0.0, 0.295), (-0.17, -0.005, 0.28)],
    arm_r=[0.016, 0.014, 0.011, 0.0],
    strands=38, strand_len=(0.09, 0.22), strand_w=(0.006, 0.014), strand_segs=3,
    drapes=8, drape_w=(0.010, 0.020),    # strips spilling over the stone
    glints=4,                            # strands whose last segment uses huldra_glint
    tufts=8, tuft_len=0.03,              # small moss cards on the stone top
)
T2 = dict(  # Baldr's Tear, ~0.45 m
    mound_r=0.11, mound_h=0.035,
    stem_h=0.43, stem_r=0.0055, nod=0.62,     # nod: how far the flower head bends over (0 = upright)
    bell_len=0.065, bell_r=0.032, petals=6,
    bud_stem_h=0.25,
    leaves=5, leaf_len=(0.17, 0.26), leaf_w=0.014,
    berries=3, berry_r=0.013,
)
T3 = dict(  # Hel's Ember Fern, ~0.5 m
    columns=6, fronds=6, frond_len=(0.50, 0.58), frond_angle=(64, -18),  # start/end elevation (deg); no upright frond
    leaflets=11, leaflet_len=0.11,      # leaflets per side; length of the longest one
    leaflet_fwd=0.45, leaflet_droop=0.3,
    spore_scale=0.32, spore_drop=0.002, spore_from=3, embers=6,   # spores only from leaflet spore_from on
    ember_anchor_h=0.30,
)
# =============================================================================

bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene

def mat(name, color, alpha=1.0, rough=0.55, metal=0.0, emit=0.0):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    b = m.node_tree.nodes["Principled BSDF"]
    b.inputs["Base Color"].default_value = color
    b.inputs["Roughness"].default_value = rough
    b.inputs["Metallic"].default_value = metal
    b.inputs["Alpha"].default_value = alpha
    if emit:
        b.inputs["Emission Color"].default_value = color
        b.inputs["Emission Strength"].default_value = emit
    m.diffuse_color = color
    m.use_backface_culling = False
    if alpha < 1: m.surface_render_method = 'BLENDED'
    return m

class Builder:
    """One mesh with named material slots."""
    def __init__(self):
        self.bm = bmesh.new(); self.slots = []
    def mi(self, name):
        if name not in self.slots: self.slots.append(name)
        return self.slots.index(name)
    def face(self, vs, m):
        try:
            f = self.bm.faces.new([self.bm.verts.new(v) if not isinstance(v, bmesh.types.BMVert) else v for v in vs])
            f.material_index = self.mi(m); return f
        except ValueError: return None
    def tube(self, pts, radii, sides, mats, cap=False):
        """Tube along pts. radii 0 -> single tip vertex. mats: name or one name per segment."""
        pts = [Vector(p) for p in pts]
        if isinstance(mats, str): mats = [mats]*(len(pts)-1)
        n = None; rings = []
        for i, p in enumerate(pts):
            t = (pts[min(i+1, len(pts)-1)] - pts[max(i-1, 0)]).normalized()
            if n is None:
                ref = Vector((0, 0, 1)) if abs(t.z) < 0.9 else Vector((1, 0, 0))
                n = (ref - t*ref.dot(t)).normalized()
            else:
                n = (n - t*n.dot(t)).normalized()
            b = t.cross(n)
            r = radii[i]
            if r <= 0: rings.append([self.bm.verts.new(p)])
            else: rings.append([self.bm.verts.new(p + r*(math.cos(2*math.pi*k/sides)*n + math.sin(2*math.pi*k/sides)*b))
                                for k in range(sides)])
        for s, (a, c) in enumerate(zip(rings, rings[1:])):
            for k in range(sides):
                j = (k+1) % sides
                if len(c) == 1: vs = [a[k], a[j], c[0]]
                elif len(a) == 1: vs = [a[0], c[j], c[k]]
                else: vs = [a[k], a[j], c[j], c[k]]
                self.face(vs, mats[s])
        if cap and len(rings[-1]) > 2: self.face(rings[-1], mats[-1])
        return rings
    def ico(self, center, scale, m, subdiv=1, jitter=0.0, rng=None, top_mat=None, top_dot=0.5):
        res = bmesh.ops.create_icosphere(self.bm, subdivisions=subdiv, radius=1.0)
        vs = res["verts"]
        for v in vs:
            k = 1 + (rng.uniform(-jitter, jitter) if rng else 0)
            v.co = Vector((v.co.x*scale[0]*k, v.co.y*scale[1]*k, v.co.z*scale[2]*k)) + Vector(center)
        faces = {f for v in vs for f in v.link_faces}
        for f in faces:
            f.material_index = self.mi(m)
            f.normal_update()
            if top_mat and f.normal.z > top_dot: f.material_index = self.mi(top_mat)
        return vs
    def prism(self, center, r, h, sides, m, top_mat=None, rot=0.0, z0=0.0):
        c = Vector(center)
        bot = [self.bm.verts.new(c + Vector((r*math.cos(rot+2*math.pi*k/sides), r*math.sin(rot+2*math.pi*k/sides), z0)))
               for k in range(sides)]
        top = [self.bm.verts.new(v.co + Vector((0, 0, h - z0))) for v in bot]
        for k in range(sides):
            j = (k+1) % sides
            self.face([bot[k], bot[j], top[j], top[k]], m)
        self.face(top, top_mat or m)
    def octa(self, center, r, m):
        c = Vector(center)
        ax = [Vector((r, 0, 0)), Vector((0, r, 0)), Vector((-r, 0, 0)), Vector((0, -r, 0))]
        top = self.bm.verts.new(c + Vector((0, 0, r*1.2))); bot = self.bm.verts.new(c - Vector((0, 0, r*1.2)))
        ring = [self.bm.verts.new(c + a) for a in ax]
        for k in range(4):
            j = (k+1) % 4
            self.face([ring[k], ring[j], top], m); self.face([ring[j], ring[k], bot], m)
    def finish(self, name):
        bmesh.ops.recalc_face_normals(self.bm, faces=self.bm.faces)
        me = bpy.data.meshes.new(name); self.bm.to_mesh(me); self.bm.free()
        for f in me.polygons: f.use_smooth = False
        for s in self.slots:
            c, ro, mt, em = MATS[s]
            me.materials.append(bpy.data.materials.get(s) or mat(s, c, 1.0, ro, mt, em))
        ob = bpy.data.objects.new(name, me); scene.collection.objects.link(ob)
        return ob

def empty(name, loc, parent):
    a = bpy.data.objects.new(name, None); scene.collection.objects.link(a)
    a.empty_display_type = 'PLAIN_AXES'; a.empty_display_size = 0.03
    a.location = loc; a.parent = parent
    return a

def bezier(pts, n):
    """Catmull-Rom through pts, n samples."""
    P = [Vector(p) for p in pts]; P = [P[0]] + P + [P[-1]]
    out = []
    for i in range(n):
        u = i/(n-1)*(len(P)-3); s = min(int(u), len(P)-4); t = u - s
        p0, p1, p2, p3 = P[s:s+4]
        out.append(0.5*((2*p1) + (-p0+p2)*t + (2*p0-5*p1+4*p2-p3)*t*t + (-p0+3*p1-3*p2+p3)*t**3))
    return out

# ---------------------------------------------------------------- Tier I
def strip(B, pts, widths, side, mats):
    """Flat ribbon along pts, width along the horizontal vector side (twisted a little per point)."""
    rows = []
    for i, p in enumerate(pts):
        sd = side[i] if isinstance(side, list) else side
        rows.append((p - sd*widths[i]/2, p + sd*widths[i]/2))
    for i in range(len(rows) - 1):
        (a0, a1), (b0, b1) = rows[i], rows[i+1]
        B.face([a0, a1, b1, b0], mats[i])

def huldra(rng):
    t = T1; B = Builder()
    sx, sy, sz = t["stone_size"]; sc = Vector((0, 0, sz - t["stone_sink"]))
    B.ico(sc, (sx, sy, sz), "huldra_stone", 1, 0.10, rng, "huldra_moss", 0.55)
    B.ico((0.10, -0.06, 0.02), (0.05, 0.045, 0.035), "huldra_stone", 1, 0.15, rng, "huldra_moss", 0.6)
    B.bm.faces.ensure_lookup_table()
    stones = BVHTree.FromBMesh(B.bm)                 # only the stones exist so far
    def on_stone(direction, lift=0.003):
        d = Vector(direction).normalized()
        hit, nrm, _, _ = stones.ray_cast(sc + d*0.5, -d)
        return (hit + nrm*lift, nrm) if hit else (sc + d*Vector((sx, sy, sz)).length, d)
    def stone_floor(p):
        hit, _, _, _ = stones.ray_cast(p, Vector((0, 0, -1)))
        return hit.z if hit else 0.0
    br = bezier(t["branch"], 6)
    B.tube(br, [t["branch_r"][min(int(i*len(t["branch_r"])/len(br)), 3)] for i in range(len(br)-1)] + [0.0], 6, "huldra_branch")
    arm = bezier(t["arm"], 6)
    B.tube(arm, [0.016, 0.015, 0.013, 0.011, 0.008, 0.0], 6, "huldra_branch")
    # hanging beard: each clump is two crossed ribbons that start on top of the branch, wrap over it and hang down
    hang = [(arm, 0.15, 0.95, 0.014)]
    glint_ids = set(rng.sample(range(t["strands"]), t["glints"]))
    for i in range(t["strands"]):
        path, u0, u1, r = hang[0]
        u = rng.uniform(u0, u1)*(len(path) - 1); j = min(int(u), len(path) - 2)
        c = path[j].lerp(path[j+1], u - j)
        tng = (path[j+1] - path[j]).normalized()
        out = tng.cross(Vector((0, 0, 1))).normalized()*(1 if i % 2 else -1)
        top = c + Vector((0, 0, r*1.05)); a = c + out*r*1.1 - Vector((0, 0, r*0.3))
        L = rng.uniform(*t["strand_len"])
        L = max(0.04, min(L, a.z - stone_floor(a) - 0.012))
        segs = t["strand_segs"]; sway = rng.uniform(0.008, 0.02)
        pts = [top] + [a + Vector((0, 0, -L*s/segs)) + out*sway*math.sin(2*math.pi*s/segs) for s in range(segs + 1)]
        w = rng.uniform(*t["strand_w"])
        widths = [w*0.8, w] + [w*(1 - 0.4*s/segs) for s in range(1, segs)] + [w*0.45]   # ragged, not pointed
        m = "huldra_strand" if i % 3 else "huldra_strand_shade"
        mats = [m]*segs + ["huldra_glint" if i in glint_ids else m]
        strip(B, pts, widths, tng, mats)
        if i % 2 == 0:   # crossed second ribbon so the clump never reads edge-on
            strip(B, pts[1:], widths[1:], out, mats[1:])
    # strips spilling over the stone, following its surface outwards and down
    for i in range(t["drapes"]):
        th = math.pi + math.radians(rng.uniform(-120, 120))     # mostly under the beard (arm reaches to -X)
        p0, p1 = rng.uniform(55, 80), rng.uniform(-5, 30)
        pts = [on_stone((math.cos(ph)*math.cos(th)*sx, math.cos(ph)*math.sin(th)*sy, math.sin(ph)*sz), 0.008)[0]
               for ph in (math.radians(p0 + (p1 - p0)*k/4) for k in range(5))]
        side = Vector((-math.sin(th), math.cos(th), 0))
        w = rng.uniform(*t["drape_w"])
        strip(B, pts, [w, w*1.1, w*0.9, w*0.7, w*0.45], side, ["huldra_strand_shade" if i % 2 else "huldra_strand"]*4)
    # moss tufts: three splayed little cards on the stone top
    for i in range(t["tufts"]):
        th = rng.uniform(0, 2*math.pi); ph = math.radians(rng.uniform(35, 75))
        c, nrm = on_stone((math.cos(ph)*math.cos(th)*sx, math.cos(ph)*math.sin(th)*sy, math.sin(ph)*sz), 0.0)
        for k in range(3):
            aa = th + 2*math.pi*k/3 + rng.uniform(-0.3, 0.3)
            d = (Vector((math.cos(aa), math.sin(aa), 0)) + nrm*1.2).normalized()*t["tuft_len"]*rng.uniform(0.7, 1.2)
            sd = Vector((-math.sin(aa), math.cos(aa), 0))*0.007
            B.face([c - sd, c + sd, c + d], "huldra_tuft")
    ob = B.finish("plant_t1")
    pick = empty("PickAnchor", arm[2] + Vector((0, 0, -0.05)), ob)
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
def helfern(rng):
    t = T3; B = Builder()
    # cracked basalt: hex columns with glowing gaps, glow plane beneath
    B.prism((0, 0, 0), 0.10, 0.012, 8, "helfern_crack", rot=0.2)
    centers = [(0, 0)] + [(0.075*math.cos(2*math.pi*k/(t["columns"]-1)+0.3), 0.075*math.sin(2*math.pi*k/(t["columns"]-1)+0.3))
                          for k in range(t["columns"]-1)]
    for i, (x, y) in enumerate(centers):
        h = 0.055 if i == 0 else rng.uniform(0.03, 0.05)
        B.prism((x, y, 0), 0.043 if i == 0 else rng.uniform(0.033, 0.04), h, 6, "helfern_basalt", rot=rng.uniform(0, 1))
    leaf_tips = []
    n = t["fronds"]
    for f in range(n):
        az = 2*math.pi*f/n + rng.uniform(-0.25, 0.25)
        d = Vector((math.cos(az), math.sin(az), 0))
        L = rng.uniform(*t["frond_len"])
        a0, a1 = map(math.radians, t["frond_angle"]); a0 -= rng.uniform(0, 10)/57.3
        segs = 6; p = Vector((0, 0, 0.05)) + d*0.012; pts = [p.copy()]
        for s in range(segs):
            a = a0 + (a1 - a0)*(s + 0.5)/segs
            p = p + (d*math.cos(a) + Vector((0, 0, math.sin(a))))*L/segs; pts.append(p.copy())
        B.tube(pts, [0.0045*(1 - 0.85*s/segs) for s in range(segs)] + [0.0], 3, "helfern_stem")
        side = Vector((-d.y, d.x, 0))
        def at(s):
            k = s*segs; j = min(int(k), segs-1)
            return pts[j].lerp(pts[j+1], k - j), (pts[j+1] - pts[j]).normalized()
        nl = t["leaflets"]; s0, s1 = 0.14, 0.97; ds = (s1 - s0)/nl
        for i in range(nl):
            s = s0 + ds*i
            qa, tan = at(s); qb, _ = at(s + ds*0.9)
            nf = tan.cross(side).normalized()                      # upper (adaxial) side of the frond
            ll = t["leaflet_len"]*(1 - 0.8*s)*min(1.0, 0.5 + 2.0*s)
            for sg in (1, -1):
                out = (side*sg + tan*t["leaflet_fwd"] - nf*t["leaflet_droop"]).normalized()
                tipv = (qa + qb)/2 + out*ll
                B.face([qa, tipv, qb], "helfern_frond")
                # spore dust (sori) on the underside: smaller glowing triangle just behind the leaflet
                if i < t["spore_from"]: leaf_tips.append(tipv); continue
                c = (qa + qb + tipv)/3 + out*ll*0.12; back = -nf*t["spore_drop"]
                B.face([c + (v - c)*t["spore_scale"] + back for v in (qa, tipv, qb)], "helfern_spore")
                leaf_tips.append(tipv)
    for i in range(t["embers"]):
        tip = leaf_tips[rng.randrange(len(leaf_tips))]
        B.octa(tip + Vector((0, 0, 0.003)), 0.005, "helfern_ember")
    ob = B.finish("plant_t3")
    pick = empty("PickAnchor", (0.0, 0.0, 0.09), ob)
    ember = empty("EmberAnchor", (0.0, 0.0, t["ember_anchor_h"]), ob)
    return ob, [pick, ember]

# ---------------------------------------------------------------- build + export
plants = {}
for i, (k, fn) in enumerate((("t1", huldra), ("t2", baldr), ("t3", helfern))):
    ob, kids = fn(random.Random(SEED + i))
    for a in kids: a.name = f"{a.name}_{k}"          # unique in the .blend; renamed exactly on export
    plants[k] = (ob, kids)

def tris(ob):
    me = ob.data; me.calc_loop_triangles(); return len(me.loop_triangles)

for k, (ob, kids) in plants.items():
    print(f"TRIS plant_{k}: {tris(ob)}  height {max((ob.matrix_world @ v.co).z for v in ob.data.vertices):.3f} m")
    for a in kids: a.name = a.name.rsplit("_", 1)[0]  # PickAnchor / EmberAnchor exactly
    bpy.ops.object.select_all(action='DESELECT')
    for o in [ob] + kids: o.select_set(True)
    bpy.context.view_layer.objects.active = ob
    bpy.ops.export_scene.fbx(filepath=os.path.join(OUT, f"plant_{k}.fbx"),
        use_selection=True, axis_forward='-Z', axis_up='Y',
        global_scale=1.0, apply_unit_scale=True, apply_scale_options='FBX_SCALE_NONE',
        bake_space_transform=True, mesh_smooth_type='FACE', add_leaf_bones=False,
        object_types={'MESH', 'EMPTY'}, use_mesh_modifiers=True)
    for a in kids: a.name = f"{a.name}_{k}"

POS = {"t1": (-0.64, 0.0), "t2": (0.0, 0.0), "t3": (0.70, 0.1)}
ROT = {"t1": 0.0, "t2": -1.0, "t3": 0.75}      # preview only (export happened above)
for k, (ob, kids) in plants.items():
    ob.location = POS[k] + (0,); ob.rotation_euler.z = ROT[k]

# ---- preview scene ----
g = bpy.data.objects.new("ground", bpy.data.meshes.new("ground"))
bm = bmesh.new(); bmesh.ops.create_grid(bm, x_segments=1, y_segments=1, size=20.0)
bm.to_mesh(g.data); bm.free(); scene.collection.objects.link(g)
g.data.materials.append(mat("ground", (0.20, 0.19, 0.17, 1), rough=0.9))

# 0.5 m ruler, alternating 0.1 m bands
R = Builder()
for i in range(5):
    R.prism((0, 0, 0), 0.012, 0.1*(i+1), 4, "ruler_a" if i % 2 == 0 else "ruler_b", rot=math.pi/4, z0=0.1*i)
MATS["ruler_a"] = ((0.85, 0.85, 0.82, 1), 0.6, 0.0, 0.0); MATS["ruler_b"] = ((0.05, 0.05, 0.05, 1), 0.6, 0.0, 0.0)
ruler = R.finish("ruler_0.5m"); ruler.location = (-0.32, 0.12, 0)

def light(name, kind, loc, energy, color, size=1.0, target=(0, 0, 0.2)):
    l = bpy.data.lights.new(name, kind); l.energy = energy; l.color = color
    if kind == 'AREA': l.size = size
    o = bpy.data.objects.new(name, l); o.location = loc
    scene.collection.objects.link(o)
    o.rotation_euler = (Vector(target) - Vector(loc)).to_track_quat('-Z', 'Y').to_euler()
light("key", 'AREA', (1.5, -2.0, 2.0), 160, (1.0, 0.85, 0.7), 1.5)
light("fill", 'AREA', (-2.2, -1.5, 1.0), 40, (0.7, 0.8, 1.0), 2.0)
light("rim", 'AREA', (0.0, 2.0, 1.6), 80, (1.0, 0.9, 0.8), 1.5)
light("sky", 'SUN', (0.0, 0.0, 5.0), 1.2, (0.9, 0.92, 1.0))

cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam"))
cam.data.lens = 42; scene.collection.objects.link(cam)
cam.location = (0.06, -2.3, 0.75)
cam.rotation_euler = (Vector((0.06, 0, 0.21)) - cam.location).to_track_quat('-Z', 'Y').to_euler()
scene.camera = cam

w = bpy.data.worlds.new("w"); w.use_nodes = True
w.node_tree.nodes["Background"].inputs[0].default_value = (0.16, 0.155, 0.15, 1)
scene.world = w

scene.view_settings.view_transform = "Standard"
scene.render.resolution_x, scene.render.resolution_y = 1280, 720
scene.render.filepath = os.path.join(OUT, "preview-plants.png")
scene.render.image_settings.file_format = 'PNG'
for eng in ('BLENDER_EEVEE', 'BLENDER_EEVEE_NEXT', 'BLENDER_WORKBENCH'):
    try: scene.render.engine = eng; break
    except TypeError: continue
bpy.ops.render.render(write_still=True)
shutil.copyfile(scene.render.filepath, PREVIEW)
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT, "plants.blend"))
