"""Procedural low-poly veil goggles, three distinct designs (helmet slot). Run: blender -b --python make_goggles.py

Tier I   Black Forest  Watchman's Glass   crude bronze rims, resin-amber lenses, rough leather strap, wood temple blocks, twine
Tier II  Mountains     Mimir's Glass      polished silver lunettes, pale crystal lenses, wolf-pelt trim, frost-crystal shards
Tier III Ashlands      Allfather's Eye    black flametal half-mask, two obsidian lenses with ember rims, small Ansuz rune
                                          (Odin's rune) on the brow, black leather strap with riveted flametal plates
Lore: docs/ideas/2026-10-01-norse-lore-research.md section 2.

Frame: Blender Z-up, the wearer faces -Y (Blender front view), wearer's right eye is at -X.
Pivot = head centre so Unity can parent the model to the head bone. FBX is exported Y-up, 1 unit = 1 m.
"""
import bpy, math, os, random, sys
from mathutils import Vector
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from common import OUT, HERE, Builder as _Builder, mat, empty, export_fbx, tri_count, write_log, reset, \
    ground, light, camera, world, render

PREVIEW = os.path.join(HERE, "preview-goggles-v2.png")

# ==== KNOBS (metres) =========================================================
SEED = 5
HEAD = (0.11, 0.125, 0.135)   # head half-extents x (width/2), y (depth/2), z (height/2); preview + strap fit
EYE_Z = 0.0                   # eye height relative to head centre
EYE_X = 0.0325                # half the eye distance (eyes 0.065 m apart)
LENS_Y = -0.134               # lens plane (head front at the eyes is about -0.119)
STRAP_A, STRAP_B = 0.117, 0.131   # strap ellipse half-axes x, y (just outside the head)

# Material table: name -> (colour RGBA, roughness, metallic, emission strength, alpha)
MATS = {
    # Tier I
    "bronze":        ((0.42, 0.26, 0.12, 1), 0.55, 0.8, 0.0, 1.0),
    "amber_lens":    ((0.62, 0.30, 0.04, 1), 0.2, 0.0, 0.0, 0.6),
    "rough_leather": ((0.26, 0.16, 0.09, 1), 0.9, 0.0, 0.0, 1.0),
    "wood":          ((0.33, 0.22, 0.12, 1), 0.85, 0.0, 0.0, 1.0),
    "twine":         ((0.50, 0.42, 0.28, 1), 0.95, 0.0, 0.0, 1.0),
    # Tier II
    "silver":        ((0.72, 0.74, 0.78, 1), 0.22, 0.95, 0.0, 1.0),
    "crystal_lens":  ((0.72, 0.86, 0.95, 1), 0.08, 0.0, 0.0, 0.35),
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

T1 = dict(rim_segs=9, rim_r=(0.023, 0.030), rim_depth=0.016, jitter=0.06, strap_w=0.024, strap_th=0.004)
T2 = dict(rim_segs=14, rim_r=(0.0245, 0.0285), rim_depth=0.011, bezel_r=0.0305, strap_w=0.018, strap_th=0.003,
          fur_tufts=40, fur_len=0.022)
T3 = dict(mask_a=0.125, mask_b=0.142, mask_span=(-152, -28), mask_z=(-0.032, 0.042), mask_th=0.005,
          lens_r=0.030, strap_w=0.02, strap_th=0.004,
          plates=(6, 32, 58), plate_len=0.022, rivets=2,     # plate positions: degrees back from each mask edge
          rune_z=0.036, rune_h=0.011, crest_w=0.016, crest_h=0.018)   # crest plate + Ansuz mark on the brow centre
# =============================================================================

scene = reset()

def Builder(rng=None):
    return _Builder(MATS, rng)

def ell(theta, a=STRAP_A, b=STRAP_B, z=EYE_Z):
    """Point on the strap ellipse. theta in degrees, -90 = front centre, 90 = back."""
    t = math.radians(theta); return Vector((a*math.cos(t), b*math.sin(t), z))

def strap_path(x_side, th0=-42, n=22, a=STRAP_A, b=STRAP_B, z=EYE_Z):
    """Strap points: right temple (x < 0) -> around the back -> left temple. th0: where the ellipse part starts."""
    start, end = -180 - th0, th0 - 360            # e.g. -138 deg -> -402 deg (passes 90 = back)
    pts = [Vector((-x_side, LENS_Y + 0.006, z))]
    pts += [ell(start + (end - start)*i/n, a, b, z) for i in range(n+1)]
    pts.append(Vector((x_side, LENS_Y + 0.006, z)))
    return pts

# ---------------------------------------------------------------- Tier I
def watchman(rng):
    t = T1; B = Builder(rng); fwd = Vector((0, -1, 0))
    r_in, r_out = t["rim_r"]
    for sx in (-1, 1):
        c = Vector((sx*EYE_X, LENS_Y, EYE_Z))
        B.ring(c, fwd, r_in, r_out, t["rim_depth"], t["rim_segs"], "bronze", t["jitter"], rot=rng.uniform(0, 1))
        B.disc(c + fwd*0.0, fwd, r_in + 0.001, t["rim_segs"], "amber_lens", dome=0.004)
        # rough wood block at the temple, riveted between rim and strap
        B.box(c + Vector((sx*(r_out + 0.007), 0.010, 0.0)), (0.016, 0, 0), (0, 0.03, 0), (0, 0, 0.026), "wood")
        B.box(c + Vector((sx*(r_out + 0.007), -0.006, 0.0)), (0.006, 0, 0), (0, 0.004, 0), (0, 0, 0.006), "bronze")
    # bronze nose bridge: low arch between the rims
    B.tube([(-EYE_X + r_out*0.7, LENS_Y, EYE_Z + 0.012), (0, LENS_Y - 0.004, EYE_Z + 0.02),
            (EYE_X - r_out*0.7, LENS_Y, EYE_Z + 0.012)], [0.0035]*3, 5, "bronze")
    # leather strap around the head
    xs = EYE_X + r_out + 0.014
    pts = strap_path(xs)
    pts[0] = Vector((-xs, LENS_Y + 0.024, EYE_Z)); pts[-1] = Vector((xs, LENS_Y + 0.024, EYE_Z))
    B.ribbon(pts, t["strap_w"], t["strap_th"], "rough_leather", jitter=0.15)
    # twine wraps on the strap behind each block and around the bridge
    for i in (2, 3, len(pts)-4, len(pts)-3):
        p = pts[i]; tng = (pts[i+1] - pts[i-1]).normalized()
        B.ribbon([p - tng*0.003, p + tng*0.003], t["strap_w"]*1.12, t["strap_th"]*2.2, "twine")
    B.tube([(-0.006, LENS_Y - 0.003, EYE_Z + 0.022), (0.0, LENS_Y - 0.009, EYE_Z + 0.017),
            (0.006, LENS_Y - 0.003, EYE_Z + 0.022)], [0.0025]*3, 4, "twine")
    return B.finish("goggles_t1")

# ---------------------------------------------------------------- Tier II
def mimir(rng):
    t = T2; B = Builder(rng); fwd = Vector((0, -1, 0))
    r_in, r_out = t["rim_r"]
    for sx in (-1, 1):
        c = Vector((sx*EYE_X, LENS_Y, EYE_Z))
        B.ring(c, fwd, r_in, r_out, t["rim_depth"], t["rim_segs"], "silver")
        B.ring(c + fwd*0.004, fwd, r_out - 0.001, t["bezel_r"], 0.004, t["rim_segs"], "silver")   # raised bezel
        B.disc(c + fwd*0.001, fwd, r_in + 0.001, t["rim_segs"], "crystal_lens", dome=0.004)
        # silver temple arm from rim to strap, with a frost shard sweeping back and up
        side = Vector((sx*(r_out + 0.010), 0.004, 0.004))
        B.tube([c + Vector((sx*r_out*0.9, 0.0, 0.004)), c + side, c + Vector((sx*(r_out + 0.022), 0.018, 0.002))],
               [0.004, 0.0045, 0.004], 6, "silver")
        B.bipyramid(c + side + Vector((sx*0.004, 0.006, 0.012)), Vector((sx*0.35, 0.8, 0.75)), 0.006, 0.040, 0.008, 6, "frost_crystal")
        B.bipyramid(c + side + Vector((sx*0.006, 0.010, 0.004)), Vector((sx*0.6, 0.9, 0.2)), 0.004, 0.024, 0.006, 5, "frost_crystal")
    # silver bridge: thin high arch
    B.tube([(-EYE_X + r_out*0.85, LENS_Y, EYE_Z + 0.014), (0, LENS_Y - 0.002, EYE_Z + 0.026),
            (EYE_X - r_out*0.85, LENS_Y, EYE_Z + 0.014)], [0.0022]*3, 6, "silver")
    # dark leather strap with wolf-pelt trim along the top edge
    xs = EYE_X + r_out + 0.022
    pts = strap_path(xs)
    pts[0] = Vector((-xs, LENS_Y + 0.018, EYE_Z)); pts[-1] = Vector((xs, LENS_Y + 0.018, EYE_Z))
    B.ribbon(pts, t["strap_w"], t["strap_th"], "dark_leather")
    # fur: short pelt band (ribbon) plus tufts (4-sided cones) leaning back
    fur_pts = [p + Vector((0, 0, t["strap_w"]*0.55)) for p in pts[2:-2]]
    B.ribbon(fur_pts, 0.010, t["strap_th"]*4.0, "wolf_pelt")
    n = t["fur_tufts"]
    for i in range(n):
        u = (i + 0.5)/n*(len(fur_pts) - 1); j = min(int(u), len(fur_pts) - 2)
        p = fur_pts[j].lerp(fur_pts[j+1], u - j); tng = (fur_pts[j+1] - fur_pts[j]).normalized()
        outv = Vector((p.x, p.y, 0)).normalized()
        L = t["fur_len"]*rng.uniform(0.7, 1.15)
        tip = p + Vector((0, 0, L*0.45)) + outv*L*0.6 + Vector((0, L*0.45, 0)) + tng*rng.uniform(-0.25, 0.25)*L  # brushed back
        r = 0.0075
        base = [p + tng*r, p + outv*r*0.8, p - tng*r, p - outv*r*0.8]
        for k in range(4): B.face([base[k], base[(k+1) % 4], tip], "wolf_pelt")
    return B.finish("goggles_t2")

# ---------------------------------------------------------------- Tier III
def allfather(rng):
    t = T3; B = Builder(rng)
    a, b = t["mask_a"], t["mask_b"]; th0, th1 = t["mask_span"]; z0, z1 = t["mask_z"]
    cols = 20
    zs = [z0, z0*0.35 + z1*0.0, z1*0.6, z1, z1 + 0.006]
    bulge = [0.0, 0.004, 0.006, 0.010, 0.004]      # brow ridge pushed forward
    front, back = [], []
    for zi, z in enumerate(zs):
        fr, bk = [], []
        for c in range(cols+1):
            th = th0 + (th1 - th0)*c/cols
            p = ell(th, a, b, z); nrm = Vector((p.x/a**2, p.y/b**2, 0)).normalized()
            cx = abs(c/cols - 0.5)*2                 # 0 at centre, 1 at the sides
            zz = z
            if zi == 0:
                if cx < 0.15: zz += 0.024*(1 - cx/0.15)          # nose notch
                elif cx < 0.55: zz -= 0.010*math.sin(math.pi*(cx - 0.15)/0.4)   # cheek guards under the eyes
                else: zz += 0.018*(cx - 0.55)/0.45               # sweeps up toward the temples
            if zi >= 3:
                zz -= 0.012*cx**2                                 # brow follows the head
                if cx < 0.3: zz -= 0.010*(1 - cx/0.3)             # stern V between the brows
            q = Vector((p.x, p.y, zz)) + nrm*bulge[zi]*(1 - 0.6*cx)
            fr.append(B.v(q)); bk.append(B.v(q - nrm*t["mask_th"]))
        front.append(fr); back.append(bk)
    for zi in range(len(zs)-1):
        B.bridge(front[zi], front[zi+1], "flametal", closed=False)
        B.bridge(back[zi+1], back[zi], "flametal", closed=False)
    for zi in (0, len(zs)-1):
        B.bridge(back[zi], front[zi], "flametal", closed=False)
    for c in (0, cols):
        for zi in range(len(zs)-1):
            B.face([front[zi][c], back[zi][c], back[zi+1][c], front[zi+1][c]], "flametal")
    def on_mask(x, z, extra):
        th = math.degrees(math.atan2(-math.sqrt(max(0.0, 1 - (x/a)**2))*b, x))
        p = ell(th, a, b, z); nrm = Vector((p.x/a**2, p.y/b**2, 0)).normalized()
        return p + nrm*extra, nrm
    # both eyes: obsidian lens in a raised flametal bezel with an ember rim
    lr = t["lens_r"]
    for sx in (-1, 1):
        c, nrm = on_mask(sx*(EYE_X + 0.004), EYE_Z + 0.004, 0.006)
        B.ring(c, nrm, lr - 0.001, lr + 0.007, 0.012, 12, "flametal")
        B.ring(c + nrm*0.0055, nrm, lr - 0.0015, lr + 0.0005, 0.002, 12, "ember_rim")
        B.disc(c + nrm*0.004, nrm, lr, 12, "obsidian_lens", dome=0.003)
    # small flametal crest plate in the brow V carrying an Ansuz mark (stave + two branches) in ember inlay
    c, nrm = on_mask(0.0, t["rune_z"], 0.0)
    c = c + nrm*0.0075                                # brow bulge at the centre is about 0.006
    up = Vector((0, 0, 1)); rt = up.cross(nrm).normalized()
    B.box(c + nrm*0.0015, rt*t["crest_w"], up*t["crest_h"], nrm*0.003, "flametal")
    c = c + nrm*0.003
    h = t["rune_h"]/2
    B.strokes(c, nrm, (0, 0, 1), [((-0.0025, -h), (-0.0025, h)), ((-0.0025, h), (0.0035, h*0.35)),
                                  ((-0.0025, h*0.25), (0.0035, -h*0.4))], 0.0018, 0.0012, "rune_inlay")
    # black leather strap: starts under the mask edges (hidden), runs round the back; riveted plates at the joints
    a0, a1 = th1 - 8, th0 + 360 + 8
    pts = [ell(a0 + (a1 - a0)*i/26, STRAP_A + 0.002, STRAP_B, EYE_Z + 0.004) for i in range(27)]
    B.ribbon(pts, t["strap_w"], t["strap_th"], "black_leather")
    for side, base in ((1, th1), (-1, th0 + 360)):
        for off in t["plates"]:
            th = base + side*off
            p = ell(th, STRAP_A + 0.002, STRAP_B, EYE_Z + 0.004)
            tng = (ell(th + 1, STRAP_A + 0.002, STRAP_B) - ell(th - 1, STRAP_A + 0.002, STRAP_B)).normalized()
            out = Vector((p.x/(STRAP_A + 0.002)**2, p.y/STRAP_B**2, 0)).normalized()
            L = t["plate_len"] * (1.25 if off == t["plates"][0] else 1.0)   # the joint plate overlaps the mask edge
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

log = []
for k, ob in models.items():
    log.append(f"TRIS goggles_{k}: {tri_count(ob)}")
    export_fbx(os.path.join(OUT, f"goggles_{k}.fbx"), ob)
write_log("goggles", log)

# ---- preview scene: each pair on a grey head ellipsoid on a neck stub ----
HEAD_Z = 0.30
PREVIEW_TURN = math.radians(-30)   # turn the heads so the strap side shows (preview only)
SPACING = 0.33
head_mat = mat("head_ref", (0.24, 0.25, 0.27, 1), rough=0.8)
for i, (k, ob) in enumerate(models.items()):
    x = (i - 1)*SPACING
    ob.location = (x, 0, HEAD_Z); ob.rotation_euler.z = PREVIEW_TURN
    bpy.ops.mesh.primitive_uv_sphere_add(segments=24, ring_count=14, radius=1.0, location=(x, 0.0, HEAD_Z))
    h = bpy.context.active_object; h.name = f"head_ref_{k}"; h.scale = HEAD; h.rotation_euler.z = PREVIEW_TURN; h.data.materials.append(head_mat)
    bpy.ops.mesh.primitive_cylinder_add(vertices=12, radius=0.055, depth=HEAD_Z - 0.05, location=(x, 0.01, (HEAD_Z - 0.05)/2))
    n = bpy.context.active_object; n.name = f"neck_ref_{k}"; n.data.materials.append(head_mat)

ground()
light("key", 'AREA', (-1.0, -1.6, 1.4), 55, (1.0, 0.88, 0.75), 1.2, target=(0, 0, HEAD_Z))
light("fill", 'AREA', (1.6, -1.2, 0.6), 15, (0.75, 0.82, 1.0), 1.5, target=(0, 0, HEAD_Z))
light("rim", 'AREA', (0.3, 1.6, 1.2), 40, (1.0, 0.9, 0.8), 1.2, target=(0, 0, HEAD_Z))
light("sky", 'SUN', (0.0, 0.0, 5.0), 0.6, (0.9, 0.92, 1.0), target=(0, 0, HEAD_Z))
camera((0.0, -1.06, 0.38), (0.0, 0, HEAD_Z - 0.01), 35)
world((0.16, 0.155, 0.15, 1))
render("preview-goggles.png", PREVIEW, "goggles.blend")
