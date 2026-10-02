"""Procedural low-poly veil goggles, three distinct designs (helmet slot). Run: blender -b --python make_goggles.py

Tier I   Black Forest  Watchman's Glass   crude bronze rims, resin-amber lenses, rough leather strap, wood temple blocks, twine
Tier II  Mountains     Mimir's Glass      polished silver lunettes, pale crystal lenses, wolf-pelt trim, frost-crystal shards
Tier III Ashlands      Allfather's Eye    black flametal half-mask, two obsidian lenses with ember rims, small Ansuz rune
                                          (Odin's rune) on the brow, black leather strap with riveted flametal plates
Lore: docs/ideas/2026-10-01-norse-lore-research.md section 2.

Frame: the space of the vanilla helmet joint `Helmet_attach` (under the Head bone), Blender Z-up, the wearer faces -Y,
wearer's right eye is at -X. Pivot = the joint origin, so the vanilla attach places the goggles on the head.
FBX is exported Y-up, 1 unit = 1 m. Fit: see "Head fit" in README.md; numbers live in head_fit.json.
"""
import bpy, bmesh, json, math, os, random, sys
from mathutils import Vector
from mathutils.bvhtree import BVHTree
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from common import OUT, HERE, Builder as _Builder, basis, mat, empty, export_fbx, tri_count, write_log, reset, \
    ground, light, camera, world, render, render_panels

PREVIEW = os.path.join(HERE, "preview-goggles-v4.png")
REF = os.path.join(HERE, "ref")                    # vanilla reference meshes from the game (gitignored, never exported)
FIT_JSON = os.path.join(HERE, "head_fit.json")     # measurements from the reference (committed; used when ref/ is absent)

# ==== KNOBS (metres) =========================================================
SEED = 5
# Reference alignment: head_body.obj comes in centimetres in the body's own space, not in joint space, so it is scaled
# and moved (OBJ axes: x, y up, z front) until its eyes sit behind the eye holes of head_HelmetLeather.obj, which is
# in joint space. See README "Head fit".
REF_ALIGN = dict(scale=0.0095, offset=(-0.129, -1.603, -0.115))
EYE = (0.040, 0.002)          # eye centre x (half the eye distance) and height, from the helmet eye holes / eye slits
RAISE_12 = 0.008              # T1/T2 sit this much above the measured eye centre (in-game: the measured eyes are ~8 mm low)
RIM_GAP = 0.004               # T1/T2 rim back face this far in front of the face everywhere inside the (tilted) rim disc
TILT_MAX = 15.0               # T1/T2 rim plane follows the eye-socket surface, tilted at most this many degrees off -y
STRAP_OFF = 0.0025            # strap inner face off the head surface
STRAP_Z = (0.004, 0.030)      # strap height above the eyes at the temples and at the back of the head (above the ears)
MASK_CLEAR = 0.003            # T3 mask inner face off the smoothed head surface

# Material table: name -> (colour RGBA, roughness, metallic, emission strength, alpha)
MATS = {
    # Tier I
    "bronze":        ((0.42, 0.26, 0.12, 1), 0.55, 0.8, 0.0, 1.0),
    "amber_lens":    ((0.88, 0.42, 0.02, 1), 0.15, 0.0, 0.0, 0.6),   # saturated resin amber
    "rough_leather": ((0.26, 0.16, 0.09, 1), 0.9, 0.0, 0.0, 1.0),
    "wood":          ((0.33, 0.22, 0.12, 1), 0.85, 0.0, 0.0, 1.0),
    "twine":         ((0.50, 0.42, 0.28, 1), 0.95, 0.0, 0.0, 1.0),
    # Tier II
    "silver":        ((0.72, 0.74, 0.78, 1), 0.22, 0.95, 0.0, 1.0),
    "crystal_lens":  ((0.30, 0.72, 1.00, 1), 0.08, 0.0, 0.0, 0.6),   # saturated ice-blue crystal
    "dark_leather":  ((0.12, 0.11, 0.11, 1), 0.75, 0.0, 0.0, 1.0),
    "wolf_pelt":     ((0.60, 0.58, 0.54, 1), 0.95, 0.0, 0.0, 1.0),
    "frost_crystal": ((0.62, 0.82, 0.95, 1), 0.15, 0.0, 0.35, 0.85),
    # Tier III
    "flametal":      ((0.07, 0.065, 0.07, 1), 0.35, 0.9, 0.0, 1.0),
    "obsidian_lens": ((0.03, 0.02, 0.05, 1), 0.05, 0.0, 0.0, 0.88),
    "ember_rim":     ((0.90, 0.24, 0.03, 1), 0.5, 0.0, 1.5, 1.0),
    "rune_inlay":    ((0.80, 0.22, 0.04, 1), 0.5, 0.0, 1.2, 1.0),
    "black_leather": ((0.06, 0.05, 0.05, 1), 0.7, 0.0, 0.0, 1.0),
    "chain":         ((0.20, 0.19, 0.20, 1), 0.4, 0.9, 0.0, 1.0),
}

T1 = dict(rim_segs=10, rim_r=(0.025, 0.0325), rim_depth=0.016, jitter=0.04, strap_w=0.024, strap_th=0.004,
          lens_r=0.0285, lens_dome=0.0035, lens_back=0.0015,   # lens 0.057 m, 3.5 mm proud of the rim
          clamps=(35, 150, 265), buckle_at=-7)                               # clamp angles (deg), buckle strap point
T2 = dict(rim_segs=14, rim_r=(0.0255, 0.0315), rim_depth=0.012, bezel_r=0.0335, strap_w=0.018, strap_th=0.003,
          fur_tufts=40, fur_len=0.022,
          lens_r=0.0285, lens_dome=0.0035, lens_back=0.0015,
          clamps=(60, 180, 300), buckle_at=-7)
T3 = dict(mask_span=(-150, -30), mask_z=(-0.034, 0.040), mask_th=0.005,
          lens_r=0.030, strap_w=0.02, strap_th=0.004,
          plates=(6, 32, 58), plate_len=0.022, rivets=2,     # plate positions: degrees back from each mask edge
          rune_z=0.032, rune_h=0.011, crest_w=0.016, crest_h=0.018)   # crest plate + Ansuz mark on the brow centre
# =============================================================================

scene = reset()

def Builder(rng=None):
    return _Builder(MATS, rng)

# ---------------------------------------------------------------- head fit
def load_ref():
    """Import the reference meshes (aligned body, helmet) if ref/ exists; they are never exported."""
    if not os.path.exists(os.path.join(REF, "head_body.obj")): return None, None
    bpy.ops.wm.obj_import(filepath=os.path.join(REF, "head_body.obj")); body = bpy.context.selected_objects[0]
    bpy.ops.wm.obj_import(filepath=os.path.join(REF, "head_HelmetLeather.obj")); helm = bpy.context.selected_objects[0]
    s_ = REF_ALIGN["scale"]; ox, oy, oz = REF_ALIGN["offset"]
    body.scale = (s_, s_, s_); body.location = (ox, -oz, oy)       # OBJ (x, y, z) -> Blender (x, -z, y)
    body.name, helm.name = "ref_head_body", "ref_helmet_leather"
    bpy.context.view_layer.update()
    return body, helm

def measure(body, helm):
    """Measure the aligned head in joint space and write head_fit.json."""
    bm = bmesh.new(); bm.from_mesh(body.data); bm.transform(body.matrix_world); tree = BVHTree.FromBMesh(bm)
    cy = -0.005                                                     # vertical head axis at (0, cy)
    heights = [round(-0.045 + 0.005*i, 3) for i in range(31)]     # 5 mm x 5 deg: fine enough for the ears and brow
    angles = list(range(-180, 180, 5))
    radii = []
    for h in heights:
        row = []
        for th in angles:
            d = Vector((math.cos(math.radians(th)), math.sin(math.radians(th)), 0))
            hit = tree.ray_cast(Vector((0, cy, h)) + d*0.4, -d)[0]
            row.append(round((Vector((hit.x, hit.y - cy, 0))).length, 4) if hit else 0.05)
        radii.append(row)
    xs = [round(-0.08 + 0.01*i, 3) for i in range(17)]; zs = [round(-0.05 + 0.01*i, 3) for i in range(12)]
    def fy(x, z):
        hit = tree.ray_cast(Vector((x, -0.5, z)), Vector((0, 1, 0)))[0]
        return round(hit.y, 4) if hit else 0.0                      # beside the head: nothing in front
    face = [[fy(x, z) for x in xs] for z in zs]
    hb = bmesh.new(); hb.from_mesh(helm.data); hb.transform(helm.matrix_world)
    holes = [e for e in hb.edges if e.is_boundary and all(v.co.y < -0.05 and abs(v.co.z) < 0.06 and 0.005 < v.co.x < 0.08 for v in e.verts)]
    hv = {v for e in holes for v in e.verts}
    hole = sum((v.co for v in hv), Vector())/max(1, len(hv))
    fit = dict(align=REF_ALIGN, cy=cy, heights=heights, angles=angles, radii=radii, face_xs=xs, face_zs=zs, face_y=face,
               helmet_eye_hole=[round(hole.x, 4), round(hole.y, 4), round(hole.z, 4)])
    with open(FIT_JSON, "w") as f:                                  # one table row per line
        f.write("{\n" + ",\n".join(f' "{k}": ' + ("[\n  " + ",\n  ".join(json.dumps(r) for r in v) + "\n ]"
                                                     if isinstance(v, list) and v and isinstance(v[0], list) else json.dumps(v))
                                     for k, v in fit.items()) + "\n}\n")
    return fit

def lerp_table(xs, ys, table, x, y):
    """Bilinear lookup; table[j][i] at (xs[i], ys[j]); clamped."""
    def idx(v, a):
        v = min(max(v, a[0]), a[-1]); i = min(int((v - a[0])/(a[1] - a[0])), len(a) - 2)
        return i, (v - a[i])/(a[i+1] - a[i])
    i, fx = idx(x, xs); j, fy = idx(y, ys)
    return ((table[j][i]*(1 - fx) + table[j][i+1]*fx)*(1 - fy) + (table[j+1][i]*(1 - fx) + table[j+1][i+1]*fx)*fy)

def r_head(th, z):
    a = FIT["angles"]; th = (th - a[0]) % 360 + a[0]
    rows = [r + [r[0]] for r in FIT["radii"]]                      # wrap the angle
    return lerp_table(a + [a[0] + 360], FIT["heights"], rows, th, z)

def r_smooth(th, z, dth=15, dz=0.012):
    """Head radius smoothed outwards (max of neighbours), so straps and masks bridge eye sockets and crevices."""
    return max(r_head(th + a, z + b) for a in (-dth, 0, dth) for b in (-dz, 0, dz))

def head_pt(th, z, off, dth=15, dz=0.012):
    d = Vector((math.cos(math.radians(th)), math.sin(math.radians(th)), 0))
    return Vector((0, FIT["cy"], z)) + d*(r_smooth(th, z, dth, dz) + off), d

def face_y(x, z):
    """Front-most face surface (y) at (x, z), from the face depth grid."""
    return lerp_table(FIT["face_xs"], FIT["face_zs"], FIT["face_y"], x, z)

def face_front(cx, cz, r):
    """Front-most face y inside the disc of radius r around (cx, cz)."""
    pts = [(cx + r*f*math.cos(a), cz + r*f*math.sin(a)) for f in (0, 0.5, 1) for a in [k*math.pi/4 for k in range(8)]]
    return min(face_y(x, z) for x, z in pts)

REF_BODY, REF_HELM = load_ref()
FIT_CHECK = {"t3 mask back": []}                  # vertex groups whose clearance to the reference head is logged
FIT = measure(REF_BODY, REF_HELM) if REF_BODY else json.load(open(FIT_JSON))
EX, EZ = EYE

def strap_pt(th, off, ez=None):
    back = (1 + math.sin(math.radians(th)))/2
    return head_pt(th, (EZ if ez is None else ez) + STRAP_Z[0] + (STRAP_Z[1] - STRAP_Z[0])*back**1.5, off, 8, 0.012)

def strap_path(th_start, th_end, off, n=24, ez=None):
    """Strap centre line hugging the head from th_start to th_end (degrees; -90 front, 90 back), rising from
    STRAP_Z[0] above the eyes at the temples to STRAP_Z[1] at the back (above the ears)."""
    return [strap_pt(th_start + (th_end - th_start)*i/n, off, ez)[0] for i in range(n + 1)]

def rim_frame(sx, ez, r_out, depth):
    """T1/T2 rim for the eye at x = sx*EX, height ez: (centre, fwd, M). The rim plane follows the eye-socket surface:
    fwd is the outward normal of a plane fitted to the face inside the rim disc (tilt clamped to TILT_MAX); the centre
    stays on the eye's line of sight (x, z fixed) and moves back until the rim's back face is RIM_GAP in front of the
    face everywhere inside the disc. M maps the untilted rim frame (x right, y back, z up) to the tilted one."""
    import numpy as np
    ex = sx*EX
    smp = [(ex + r_out*f*math.cos(a), ez + r_out*f*math.sin(a)) for f in (0.0, 0.4, 0.75, 1.0)
           for a in [k*math.pi/6 for k in range(12)]]
    fit = [(x, z, face_y(x, z)) for x, z in smp if abs(x) <= 0.062]
    A = np.array([[1, x - ex, z - ez] for x, z, _ in fit]); yv = np.array([y for _, _, y in fit])
    _, b, c = np.linalg.lstsq(A, yv, rcond=None)[0]
    fwd = Vector((b, -1.0, c)).normalized()
    ang = math.degrees(fwd.angle(Vector((0, -1, 0))))
    if ang > TILT_MAX: fwd = Vector((0, -1, 0)).slerp(fwd, TILT_MAX/ang).normalized()
    yax = -fwd; xax = (Vector((1, 0, 0)) - yax*yax.x).normalized(); zax = xax.cross(yax)
    from mathutils import Matrix
    M = Matrix((xax, yax, zax)).transposed()
    yc = min(face_y(ex + o.x, ez + o.z) - RIM_GAP - o.y
             for o in [M @ Vector((r_out*f*math.cos(a), depth/2, r_out*f*math.sin(a)))
                       for f in (0.0, 0.5, 0.8, 1.0) for a in [k*math.pi/8 for k in range(16)]])
    return Vector((ex, yc, ez)), fwd, M

def rim_y(r_out, depth):
    """Old untilted rim centre plane (v4: back face 4 mm in front of the face), kept for the log comparison."""
    return face_front(EX, EZ, r_out) - 0.004 - depth/2

# ---------------------------------------------------------------- lenses, clamps, buckles
def lens(B, c, fwd, r, dome, back, segs, m):
    """Closed lens: domed front (centre `dome` in front of the edge) and a flatter back; edge plane at c."""
    ax, u, w = basis(fwd); c = Vector(c)
    rim = [B.v(c + r*(math.cos(2*math.pi*k/segs)*u + math.sin(2*math.pi*k/segs)*w)) for k in range(segs)]
    front, rear = B.v(c + ax*dome), B.v(c - ax*back)
    for k in range(segs):
        j = (k+1) % segs
        B.face([rim[k], rim[j], front], m); B.face([rim[j], rim[k], rear], m)

def clamps(B, c, fwd, r_in, r_out, depth, angles, m, rivet_m, rng=None):
    """Clamp straps over the rim front at the given angles, each with a domed rivet."""
    ax, u, w = basis(fwd); c = Vector(c)
    for a in angles:
        d = math.cos(math.radians(a))*u + math.sin(math.radians(a))*w; tg = ax.cross(d)
        mid = c + d*(r_in + r_out)/2 + ax*(depth/2 + 0.0012)
        B.box(mid, d*(r_out - r_in + 0.004), tg*0.007, ax*0.0024, m)
        B.disc(mid + ax*0.0012 + d*0.001, ax, 0.0022, 5, rivet_m, dome=0.0018)

def buckle(B, pts, i, width, th, m):
    """Rectangular buckle frame around the strap at point i, with a tongue across."""
    p = pts[i]; tg = (pts[i+1] - pts[i-1]).normalized()
    out = tg.cross(Vector((0, 0, 1))).normalized()
    if out.dot(Vector((p.x, p.y, 0))) < 0: out = -out
    up = Vector((0, 0, 1)); L = 0.016; H = width + 0.006; bw = 0.0025; c = p + out*(th/2 + 0.0015)
    B.box(c + up*(H/2 - bw/2), tg*L, up*bw, out*0.003, m)
    B.box(c - up*(H/2 - bw/2), tg*L, up*bw, out*0.003, m)
    B.box(c + tg*(L/2 - bw/2), tg*bw, up*H, out*0.003, m)
    B.box(c - tg*(L/2 - bw/2), tg*bw, up*H, out*0.003, m)
    B.box(c + out*0.001, tg*L*0.9, up*0.0018, out*0.002, m)      # tongue

# ---------------------------------------------------------------- Tier I
def watchman(rng):
    t = T1; B = Builder(rng); ez = EZ + RAISE_12
    r_in, r_out = t["rim_r"]; ex = EX; ends = {}
    for sx in (-1, 1):
        c, fwd, M = rim_frame(sx, ez, r_out, t["rim_depth"])
        ends[sx] = c + M @ Vector((sx*(r_out + 0.014), 0.024, 0))
        B.ring(c, fwd, r_in, r_out, t["rim_depth"], t["rim_segs"], "bronze", t["jitter"], rot=rng.uniform(0, 1))
        lens(B, c + fwd*(t["rim_depth"]/2 + 0.0005), fwd, t["lens_r"], t["lens_dome"], t["lens_back"], t["rim_segs"], "amber_lens")
        clamps(B, c, fwd, r_in, r_out, t["rim_depth"], [a if sx > 0 else 180 - a for a in t["clamps"]], "bronze", "bronze")
        # rough wood block at the temple, riveted between rim and strap
        B.box(c + M @ Vector((sx*(r_out + 0.007), 0.010, 0.0)), M @ Vector((0.016, 0, 0)), M @ Vector((0, 0.03, 0)),
              M @ Vector((0, 0, 0.028)), "wood")
        B.box(c + M @ Vector((sx*(r_out + 0.007), -0.006, 0.0)), M @ Vector((0.006, 0, 0)), M @ Vector((0, 0.004, 0)),
              M @ Vector((0, 0, 0.007)), "bronze")
    # bronze bridge: a flat riveted bar over the nose between the rims
    bz = ez + 0.012; by = face_y(0, bz) - 0.0035                     # rests on the nose bridge
    B.box((0, by, bz), (2*(ex - r_out*0.75), 0, 0), (0, 0.005, 0), (0, 0, 0.007), "bronze")
    for sx in (-1, 1):
        B.disc((sx*(ex - r_out*0.95), by - 0.0025, bz), (0, -1, 0), 0.002, 5, "bronze", dome=0.0016)
    B.tube([(-0.006, by - 0.003, bz + 0.0045), (0.0, by - 0.006, bz + 0.0015), (0.006, by - 0.003, bz + 0.0045)],
           [0.0022]*3, 4, "twine")                                    # twine wrap on the bridge
    # leather strap around the head with a bronze buckle on the right side
    pts = strap_path(-140, -400, STRAP_OFF + t["strap_th"]/2, ez=ez)
    pts = [ends[-1]] + pts + [ends[1]]
    B.ribbon(pts, t["strap_w"], t["strap_th"], "rough_leather", jitter=0.15)
    buckle(B, pts, t["buckle_at"], t["strap_w"], t["strap_th"], "bronze")
    # twine wraps on the strap behind each block
    for i in (2, len(pts)-3):
        p = pts[i]; tng = (pts[i+1] - pts[i-1]).normalized()
        B.ribbon([p - tng*0.003, p + tng*0.003], t["strap_w"]*1.12, t["strap_th"]*2.2, "twine")
    return B.finish("goggles_t1")

# ---------------------------------------------------------------- Tier II
def mimir(rng):
    t = T2; B = Builder(rng); ez = EZ + RAISE_12
    r_in, r_out = t["rim_r"]
    ex = EX; ends, inner = {}, {}
    for sx in (-1, 1):
        c, fwd, M = rim_frame(sx, ez, t["bezel_r"], t["rim_depth"])
        ends[sx] = c + M @ Vector((sx*(r_out + 0.022), 0.018, 0))
        inner[sx] = c + M @ Vector((-sx*r_out*0.8, -t["rim_depth"]/2, 0.012))
        B.ring(c, fwd, r_in, r_out, t["rim_depth"], t["rim_segs"], "silver")
        B.ring(c + fwd*0.004, fwd, r_out - 0.001, t["bezel_r"], 0.004, t["rim_segs"], "silver")   # raised bezel
        lens(B, c + fwd*(t["rim_depth"]/2 + 0.0005), fwd, t["lens_r"], t["lens_dome"], t["lens_back"], t["rim_segs"], "crystal_lens")
        clamps(B, c, fwd, r_in, t["bezel_r"], t["rim_depth"], [a if sx > 0 else 180 - a for a in t["clamps"]], "silver", "frost_crystal")
        # silver temple arm from rim to strap, with a frost shard sweeping back and up
        side = M @ Vector((sx*(r_out + 0.010), 0.004, 0.004))
        B.tube([c + M @ Vector((sx*r_out*0.9, 0.0, 0.004)), c + side, c + M @ Vector((sx*(r_out + 0.022), 0.018, 0.002))],
               [0.004, 0.0045, 0.004], 6, "silver")
        B.bipyramid(c + side + M @ Vector((sx*0.004, 0.006, 0.012)), M @ Vector((sx*0.35, 0.8, 0.75)), 0.006, 0.040, 0.008, 6, "frost_crystal")
        B.bipyramid(c + side + M @ Vector((sx*0.006, 0.010, 0.004)), M @ Vector((sx*0.6, 0.9, 0.2)), 0.004, 0.024, 0.006, 5, "frost_crystal")
    # silver bridge: a high arch with a small frost crystal set in the middle
    bz = ez + 0.014; by = min(face_y(0, bz) - 0.004, inner[-1].y, inner[1].y)   # arch rests on the nose bridge
    B.tube([inner[-1], (0, by, bz), inner[1]], [0.0032, 0.0036, 0.0032], 6, "silver")
    B.bipyramid((0, by - 0.004, bz + 0.002), (0, -1, 0), 0.004, 0.005, 0.002, 6, "frost_crystal")
    # dark leather strap with wolf-pelt trim along the top edge
    pts = strap_path(-140, -400, STRAP_OFF + t["strap_th"]/2, ez=ez)
    pts = [ends[-1]] + pts + [ends[1]]
    B.ribbon(pts, t["strap_w"], t["strap_th"], "dark_leather")
    buckle(B, pts, t["buckle_at"], t["strap_w"], t["strap_th"], "silver")
    # fur: short pelt band (ribbon) plus tufts (4-sided cones) leaning back
    fur_pts = [p + Vector((0, 0, t["strap_w"]*0.55)) + Vector((p.x, p.y - FIT["cy"], 0)).normalized()*0.0045
               for p in pts[2:-2]]                                  # out by the band's half thickness: no part in the head
    B.ribbon(fur_pts, 0.010, t["strap_th"]*4.0, "wolf_pelt")
    n = t["fur_tufts"]
    for i in range(n):
        u = (i + 0.5)/n*(len(fur_pts) - 1); j = min(int(u), len(fur_pts) - 2)
        p = fur_pts[j].lerp(fur_pts[j+1], u - j); tng = (fur_pts[j+1] - fur_pts[j]).normalized()
        outv = Vector((p.x, p.y - FIT["cy"], 0)).normalized()
        L = t["fur_len"]*rng.uniform(0.7, 1.15)
        tip = p + Vector((0, 0, L*0.45)) + outv*L*0.6 + Vector((0, L*0.45, 0)) + tng*rng.uniform(-0.25, 0.25)*L  # brushed back
        r = 0.0075
        base = [p + tng*r, p + outv*r*0.8, p - tng*r, p - outv*r*0.2]   # inner corner stays outside the head
        for k in range(4): B.face([base[k], base[(k+1) % 4], tip], "wolf_pelt")
    return B.finish("goggles_t2")

# ---------------------------------------------------------------- Tier III
def allfather(rng):
    t = T3; B = Builder(rng)
    th0, th1 = t["mask_span"]; z0, z1 = t["mask_z"]
    cols = 20
    zs = [z0, z0*0.35 + z1*0.0, z1*0.6, z1, z1 + 0.006]
    bulge = [0.0, 0.004, 0.006, 0.010, 0.004]      # brow ridge pushed forward
    def mask_pt(th, z):
        """Mask inner surface, MASK_CLEAR out: the head smoothed over the eye sockets (wide smoothing at the front,
        narrow at the temples and below the eyes, so the edges and the cheek guards lie on the face)."""
        front = max(0.0, 1 - abs(th + 90)/50)                       # 1 at the nose, 0 from 50 deg off the front
        socket = front if -0.008 <= z <= 0.020 else 0.0             # wide smoothing only across the eye sockets
        dth = max(15*socket, 6*front)                              # a little at the nose so the notch clears it
        dz = 0.018*socket
        return head_pt(th, EZ + z, MASK_CLEAR, dth, dz)
    front, back = [], []
    for zi, z in enumerate(zs):
        fr, bk = [], []
        for c in range(cols+1):
            th = th0 + (th1 - th0)*c/cols
            cx = abs(c/cols - 0.5)*2                 # 0 at centre, 1 at the sides
            zz = z
            if zi == 0:
                if cx < 0.15: zz += 0.024*(1 - cx/0.15)          # nose notch
                elif cx < 0.55: zz -= 0.010*math.sin(math.pi*(cx - 0.15)/0.4)   # cheek guards under the eyes
                else: zz += 0.018*(cx - 0.55)/0.45               # sweeps up toward the temples
            if zi >= 3:
                zz -= 0.012*cx**2                                 # brow follows the head
                if cx < 0.3: zz -= 0.010*(1 - cx/0.3)             # stern V between the brows
            p, nrm = mask_pt(th, zz)
            q = p + nrm*(t["mask_th"] + bulge[zi]*(1 - 0.6*cx))      # the bulge thickens the front only
            fr.append(B.v(q)); bk.append(B.v(p)); FIT_CHECK["t3 mask back"].append(p)
        front.append(fr); back.append(bk)
    for zi in range(len(zs)-1):
        B.bridge(front[zi], front[zi+1], "flametal", closed=False)
        B.bridge(back[zi+1], back[zi], "flametal", closed=False)
    for zi in (0, len(zs)-1):
        B.bridge(back[zi], front[zi], "flametal", closed=False)
    for c in (0, cols):
        for zi in range(len(zs)-1):
            B.face([front[zi][c], back[zi][c], back[zi+1][c], front[zi+1][c]], "flametal")
    def bulge_at(z, th):
        """Front bulge of the mask at height z (interpolated over the rows) and angle th."""
        cx = min(1.0, abs((th - th0)/(th1 - th0) - 0.5)*2)
        if z <= zs[0]: b = bulge[0]
        elif z >= zs[-1]: b = bulge[-1]
        else:
            i = max(k for k in range(len(zs) - 1) if zs[k] <= z); f = (z - zs[i])/(zs[i+1] - zs[i])
            b = bulge[i]*(1 - f) + bulge[i+1]*f
        return b*(1 - 0.6*cx)
    def mask_th_at(x, z):
        """Angle at which the mask passes x at height z (bisection)."""
        lo, hi = (-180.0, -90.0) if x < 0 else (-90.0, 0.0)
        for _ in range(30):
            mid = (lo + hi)/2
            if mask_pt(mid, z)[0].x < x: lo = mid                   # x grows with the angle on both halves
            else: hi = mid
        return (lo + hi)/2
    def on_mask_th(th, z, extra):
        p, d = mask_pt(th, z)
        return p + d*(t["mask_th"] + bulge_at(z, th) + extra), d
    def on_mask(x, z, extra):
        """Point on the mask front at x and height z above the eyes."""
        return on_mask_th(mask_th_at(x, z), z, extra)
    def surf(c_th, cz, u, v, h):
        """Point u metres round the mask (arc length) and v metres up from (c_th, cz), h off the mask front: shapes
        laid out this way stay round on the curved mask instead of stretching round the temple."""
        r = (mask_pt(c_th, cz)[0] - Vector((0, FIT["cy"], EZ + cz))).length
        return on_mask_th(c_th + math.degrees(u/r), cz + v, h)[0]
    def conform_ring(c_th, cz, r_in, r_out, h0, h1, segs, m):
        """Annulus lying on the curved mask front (back face h0, front face h1 off it), so no edge floats."""
        loops = [[], [], [], []]
        for k in range(segs):
            a = 2*math.pi*k/segs
            for li, (r, h) in enumerate(((r_out, h0), (r_out, h1), (r_in, h1), (r_in, h0))):
                loops[li].append(B.v(surf(c_th, cz, r*math.cos(a), r*math.sin(a), h)))
        for a_, b_ in zip(loops, loops[1:] + loops[:1]): B.bridge(a_, b_, m)
    def conform_disc(c_th, cz, r, h, dome, segs, m):
        rim = [B.v(surf(c_th, cz, r*math.cos(2*math.pi*k/segs), r*math.sin(2*math.pi*k/segs), h)) for k in range(segs)]
        mid = B.v(surf(c_th, cz, 0, 0, h + dome))
        for k in range(segs): B.face([rim[k], rim[(k+1) % segs], mid], m)
    # both eyes: obsidian lens in a raised flametal bezel with an ember rim, all following the mask round the temple
    lr = t["lens_r"]
    for sx in (-1, 1):
        c_th = mask_th_at(sx*EX, 0.002)
        conform_ring(c_th, 0.002, lr - 0.001, lr + 0.007, -0.002, 0.008, 12, "flametal")
        conform_ring(c_th, 0.002, lr - 0.0015, lr + 0.0005, 0.007, 0.0095, 12, "ember_rim")
        conform_disc(c_th, 0.002, lr, 0.0075, 0.003, 12, "obsidian_lens")
    # small flametal crest plate in the brow V carrying an Ansuz mark (stave + two branches) in ember inlay
    c, nrm = on_mask(0.0, t["rune_z"], 0.0)
    c = c + nrm*0.0005
    up = Vector((0, 0, 1)); rt = up.cross(nrm).normalized()
    B.box(c + nrm*0.0015, rt*t["crest_w"], up*t["crest_h"], nrm*0.003, "flametal")
    c = c + nrm*0.003
    h = t["rune_h"]/2
    B.strokes(c, nrm, (0, 0, 1), [((-0.0025, -h), (-0.0025, h)), ((-0.0025, h), (0.0035, h*0.35)),
                                  ((-0.0025, h*0.25), (0.0035, -h*0.4))], 0.0018, 0.0012, "rune_inlay")
    # black leather strap: starts under the mask edges (hidden), hugs the head round the back; riveted plates
    off = STRAP_OFF + t["strap_th"]/2
    pts = strap_path(th1 - 8, th0 + 360 + 8, off, 26)
    B.ribbon(pts, t["strap_w"], t["strap_th"], "black_leather")
    for side, base in ((1, th1), (-1, th0 + 360)):
        for k_off in t["plates"]:
            th = base + side*k_off
            p, out = strap_pt(th, off)
            tng = (strap_pt(th + 1, off)[0] - strap_pt(th - 1, off)[0]).normalized()
            L = t["plate_len"] * (1.25 if k_off == t["plates"][0] else 1.0)   # the joint plate overlaps the mask edge
            pc = p + out*(t["strap_th"]/2 + 0.0015)
            B.box(pc, tng*L, Vector((0, 0, t["strap_w"] + 0.004)), out*0.003, "flametal")
            for k in range(t["rivets"]):
                q = pc + tng*L*(0.3 if k else -0.3) + out*0.0015
                B.disc(q, out, 0.0022, 5, "chain", dome=0.0018)
    return B.finish("goggles_t3")

# ---------------------------------------------------------------- build + export
models = {}
for i, (k, fn) in enumerate((("t1", watchman), ("t2", mimir), ("t3", allfather))):
    models[k] = fn(random.Random(SEED + i))
    empty("attach", (0, 0, 0), models[k], 0.02)        # head centre = pivot; VisEquipment looks for this name

log = [f"HEAD FIT (joint space, Blender axes: x = wearer's left, -y = front, z = up; metres)",
       f"eye centres x = +-{EX:.3f}, z = {EZ:+.3f}, eye surface y = {face_y(EX, EZ):+.4f}",
       f"brow ridge front y = {face_y(EX, EZ + 0.010):+.4f} (z {EZ + 0.010:+.3f}); nose bridge y = {face_y(0, EZ + 0.01):+.4f} (x 0, z {EZ + 0.01:+.3f})",
       f"head half-width at the temples = {r_head(0, EZ + 0.01):.3f} / {r_head(180, EZ + 0.01):.3f} (axis at y {FIT['cy']:+.3f})",
       f"head radius at the back (strap height) = {r_head(90, EZ + STRAP_Z[1]):.3f}; front at the brow = {r_head(-90, EZ + 0.01):.3f}",
       f"helmet eye-hole centre = {FIT['helmet_eye_hole']}",
       f"v4 rim centre planes (untilted, 4 mm gap): T1 y = {rim_y(T1['rim_r'][1], T1['rim_depth']):+.4f}, T2 y = {rim_y(T2['bezel_r'], T2['rim_depth']):+.4f}",
       *[f"T{n} rim (right eye, raised {RAISE_12*1000:.0f} mm): centre {tuple(round(v, 4) for v in c)}, tilt {math.degrees(f.angle(Vector((0, -1, 0)))):.1f} deg, "
         f"normal {tuple(round(v, 3) for v in f)}"
         for n, (c, f, _) in ((1, rim_frame(1, EZ + RAISE_12, T1['rim_r'][1], T1['rim_depth'])),
                              (2, rim_frame(1, EZ + RAISE_12, T2['bezel_r'], T2['rim_depth'])))],
       f"source: {'ref/ meshes (re-measured)' if REF_BODY else 'head_fit.json'}"]
if REF_BODY:                                      # clearance to the reference head (negative = inside the head)
    hb_ = bmesh.new(); hb_.from_mesh(REF_BODY.data); hb_.transform(REF_BODY.matrix_world); HEAD = BVHTree.FromBMesh(hb_)
    def signed(co):
        """Radial gap: distance from the head axis minus the head surface radius at the same angle and height, the
        surface found by an exact ray cast from outside (the body has inner meshes, so nearest-point tests fail)."""
        d = Vector((co.x, co.y - FIT["cy"], 0)); r = d.length; d.normalize()
        hit = HEAD.ray_cast(Vector((0, FIT["cy"], co.z)) + d*0.4, -d)[0]
        return r - (Vector((hit.x, hit.y - FIT["cy"], 0)).length if hit else 0.0)
    for k, ob in models.items():
        d = sorted(signed(ob.matrix_world @ v.co) for v in ob.data.vertices)
        log.append(f"CLEAR goggles_{k}: min {d[0]*1000:+.1f} mm over {len(d)} verts, {sum(1 for x in d if x < 0)} inside the head")
        per = {}
        for poly in ob.data.polygons:
            m_ = ob.data.materials[poly.material_index].name
            for vi in poly.vertices:
                co = ob.matrix_world @ ob.data.vertices[vi].co
                per[m_] = min(per.get(m_, (1.0, None)), (signed(co), tuple(round(c_, 3) for c_ in co)))
        log.append("  min per material: " + ", ".join(f"{m_} {v*1000:+.1f}" + (f" at {at}" if v < 0 else "")
                                                       for m_, (v, at) in sorted(per.items())))
    if os.environ.get("GOGGLES_FIT_DEBUG"):                       # per-row mask clearance table
        pts = FIT_CHECK["t3 mask back"]; cols = 21
        for zi in range(len(pts)//cols):
            log.append("  row %d z %+.3f: " % (zi, pts[zi*cols].z) + " ".join("%+5.1f" % (signed(p)*1000) for p in pts[zi*cols:(zi+1)*cols]))
    for name, pts in FIT_CHECK.items():
        d = sorted(signed(p) for p in pts)
        log.append(f"CLEAR {name}: min {d[0]*1000:+.1f} / median {d[len(d)//2]*1000:+.1f} / max {d[-1]*1000:+.1f} mm")
for k, ob in models.items():
    log.append(f"TRIS goggles_{k}: {tri_count(ob)}")
    export_fbx(os.path.join(OUT, f"goggles_{k}.fbx"), ob)
write_log("goggles", log)

# ---- preview: the goggles on the exported head (front row, 3/4 row); proxy head from head_fit.json without ref/ ----
head_mat = mat("head_ref", (0.42, 0.42, 0.44, 1), rough=0.8)
if REF_BODY:
    head = REF_BODY; REF_HELM.hide_render = True; REF_HELM.hide_viewport = True
    head.data.materials.clear(); head.data.materials.append(head_mat)
else:                                           # proxy: rings from the measured polar table
    hb = bmesh.new(); rings = []
    for h in FIT["heights"]:
        rings.append([hb.verts.new((math.cos(math.radians(a))*r, FIT["cy"] + math.sin(math.radians(a))*r, h))
                      for a, r in zip(FIT["angles"], FIT["radii"][FIT["heights"].index(h)])])
    for a_, b_ in zip(rings, rings[1:]):
        for k in range(len(a_)):
            j = (k + 1) % len(a_); hb.faces.new([a_[k], a_[j], b_[j], b_[k]])
    head = bpy.data.objects.new("head_proxy", bpy.data.meshes.new("head_proxy")); hb.to_mesh(head.data); hb.free()
    scene.collection.objects.link(head); head.data.materials.append(head_mat)
tgt = (0, -0.02, 0.0)
lights = [light("key", 'AREA', (-0.5, -0.8, 0.6), 14, (1.0, 0.88, 0.75), 0.8, tgt),
          light("fill", 'AREA', (0.8, -0.6, 0.2), 4, (0.75, 0.82, 1.0), 1.0, tgt),
          light("rim", 'AREA', (0.2, 0.8, 0.6), 10, (1.0, 0.9, 0.8), 0.8, tgt)]
world((0.16, 0.155, 0.15, 1))
cams = [((0.0, -0.62, 0.05), (0.0, -0.02, 0.005), 55), ((0.47, -0.40, 0.10), (0.0, 0.0, 0.0), 55),
        ((0.60, -0.06, 0.02), (0.0, -0.06, 0.005), 70)]                     # front, 3/4, side (fit check)
render_panels([dict(height=420, panels=[dict(width=w, cam=cam, show=[head, ob] + lights)
                                        for w, ob in zip((427, 427, 426), models.values())]) for cam in cams],
              "preview-goggles.png", PREVIEW, "goggles.blend")
