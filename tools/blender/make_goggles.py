"""Procedural low-poly veil goggles, three distinct designs (helmet slot). Run: blender -b --python make_goggles.py

Tier I   Black Forest  Watchman's Glass   crude bronze rims, resin-amber lenses, rough leather strap, wood temple blocks, twine
Tier II  Mountains     Mimir's Glass      polished silver lunettes, pale crystal lenses, wolf-pelt trim, frost-crystal shards
Tier III Ashlands      Allfather's Eye    black flametal half-mask, one obsidian lens (right eye), sealed left socket with an
                                          Ansuz rune (Odin's rune), ember rim, leather strap with chain links
Lore: docs/ideas/2026-10-01-norse-lore-research.md section 2.

Frame: Blender Z-up, the wearer faces -Y (Blender front view), wearer's right eye is at -X.
Pivot = head centre so Unity can parent the model to the head bone. FBX is exported Y-up, 1 unit = 1 m.
"""
import bpy, bmesh, math, os, random, shutil
from mathutils import Vector

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, "out")            # FBX, raw render, .blend (gitignored)
PREVIEW = os.path.join(HERE, "preview-goggles-v1.png")
os.makedirs(OUT, exist_ok=True)

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
          lens_r=0.030, socket_r=0.026, strap_w=0.02, strap_th=0.004, links=3)
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

def basis(axis):
    ax = Vector(axis).normalized()
    ref = Vector((0, 0, 1)) if abs(ax.z) < 0.9 else Vector((1, 0, 0))
    u = (ref - ax*ref.dot(ax)).normalized()
    return ax, u, ax.cross(u)

class Builder:
    def __init__(self, rng=None):
        self.bm = bmesh.new(); self.slots = []; self.rng = rng
    def mi(self, name):
        if name not in self.slots: self.slots.append(name)
        return self.slots.index(name)
    def v(self, co): return self.bm.verts.new(co)
    def face(self, vs, m):
        try:
            f = self.bm.faces.new([x if isinstance(x, bmesh.types.BMVert) else self.v(x) for x in vs])
            f.material_index = self.mi(m); return f
        except ValueError: return None
    def bridge(self, a, b, m, closed=True):
        n = len(a)
        for k in range(n if closed else n-1):
            j = (k+1) % n
            self.face([a[k], a[j], b[j], b[k]], m)
    def ring(self, c, axis, r_in, r_out, depth, segs, m, jitter=0.0, rot=0.0):
        """Rim: annulus extruded along axis (front at c + axis*depth/2). Rectangular cross-section."""
        ax, u, w = basis(axis); c = Vector(c)
        corners = ((r_out, -0.5), (r_out, 0.5), (r_in, 0.5), (r_in, -0.5))   # cross-section
        loops = [[] for _ in corners]
        for k in range(segs):
            a = rot + 2*math.pi*k/segs
            jit = 1 + (self.rng.uniform(-jitter, jitter) if jitter and self.rng else 0)
            dirv = math.cos(a)*u + math.sin(a)*w
            for li, (r, d) in enumerate(corners):
                loops[li].append(self.v(c + dirv*r*jit + ax*depth*d))
        for a_, b_ in zip(loops, loops[1:] + loops[:1]):
            self.bridge(a_, b_, m)
    def disc(self, c, axis, r, segs, m, dome=0.0, rot=0.0):
        ax, u, w = basis(axis); c = Vector(c)
        rim = [self.v(c + r*(math.cos(rot+2*math.pi*k/segs)*u + math.sin(rot+2*math.pi*k/segs)*w)) for k in range(segs)]
        mid = self.v(c + ax*dome)
        for k in range(segs): self.face([rim[k], rim[(k+1) % segs], mid], m)
    def box(self, c, x, y, z, m):
        """Axis vectors x, y, z are full edge vectors."""
        c = Vector(c); x, y, z = Vector(x)/2, Vector(y)/2, Vector(z)/2
        P = [self.v(c + sx*x + sy*y + sz*z) for sx in (-1, 1) for sy in (-1, 1) for sz in (-1, 1)]
        for q in ((0, 1, 3, 2), (4, 6, 7, 5), (0, 4, 5, 1), (2, 3, 7, 6), (0, 2, 6, 4), (1, 5, 7, 3)):
            self.face([P[i] for i in q], m)
    def tube(self, pts, radii, sides, m):
        pts = [Vector(p) for p in pts]; n = None; rings = []
        for i, p in enumerate(pts):
            t = (pts[min(i+1, len(pts)-1)] - pts[max(i-1, 0)]).normalized()
            if n is None:
                ref = Vector((0, 0, 1)) if abs(t.z) < 0.9 else Vector((1, 0, 0))
                n = (ref - t*ref.dot(t)).normalized()
            else: n = (n - t*n.dot(t)).normalized()
            b = t.cross(n); r = radii[i]
            rings.append([self.v(p)] if r <= 0 else
                         [self.v(p + r*(math.cos(2*math.pi*k/sides)*n + math.sin(2*math.pi*k/sides)*b)) for k in range(sides)])
        for a, c in zip(rings, rings[1:]):
            if len(c) == 1:
                for k in range(sides): self.face([a[k], a[(k+1) % sides], c[0]], m)
            else: self.bridge(a, c, m)
    def ribbon(self, pts, width, th, m, jitter=0.0):
        """Strap with a rectangular cross-section, width along Z, thickness horizontal."""
        rows = []
        for i, p in enumerate(pts):
            t = (pts[min(i+1, len(pts)-1)] - pts[max(i-1, 0)]).normalized()
            out = t.cross(Vector((0, 0, 1))).normalized()
            if out.dot(Vector((p.x, p.y, 0))) < 0: out = -out
            wj = width*(1 + (self.rng.uniform(-jitter, jitter) if jitter and self.rng else 0))/2
            up = Vector((0, 0, 1))
            rows.append([self.v(p + out*th/2 + up*wj), self.v(p + out*th/2 - up*wj),
                         self.v(p - out*th/2 - up*wj), self.v(p - out*th/2 + up*wj)])
        for a, b in zip(rows, rows[1:]): self.bridge(a, b, m)
        self.face(rows[0], m); self.face(rows[-1], m)
        return rows
    def bipyramid(self, c, axis, r, h1, h2, sides, m):
        ax, u, w = basis(axis); c = Vector(c)
        ring = [self.v(c + r*(math.cos(2*math.pi*k/sides)*u + math.sin(2*math.pi*k/sides)*w)) for k in range(sides)]
        tip, bot = self.v(c + ax*h1), self.v(c - ax*h2)
        for k in range(sides):
            j = (k+1) % sides
            self.face([ring[k], ring[j], tip], m); self.face([ring[j], ring[k], bot], m)
    def torus(self, c, axis, R, r, segs, sides, m, stretch=1.0):
        ax, u, w = basis(axis); c = Vector(c); rings = []
        for k in range(segs):
            a = 2*math.pi*k/segs
            cen = c + R*(math.cos(a)*u*stretch + math.sin(a)*w)
            d = (math.cos(a)*u + math.sin(a)*w)
            rings.append([self.v(cen + r*(math.cos(2*math.pi*s/sides)*d + math.sin(2*math.pi*s/sides)*ax)) for s in range(sides)])
        for k in range(segs): self.bridge(rings[k], rings[(k+1) % segs], m)
    def finish(self, name):
        bmesh.ops.recalc_face_normals(self.bm, faces=self.bm.faces)
        me = bpy.data.meshes.new(name); self.bm.to_mesh(me); self.bm.free()
        for f in me.polygons: f.use_smooth = False
        for s in self.slots:
            c, ro, mt, em, al = MATS[s]
            me.materials.append(bpy.data.materials.get(s) or mat(s, c, al, ro, mt, em))
        ob = bpy.data.objects.new(name, me); scene.collection.objects.link(ob)
        return ob

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
    # right eye (-X): obsidian lens in a raised flametal bezel with an ember rim
    c, nrm = on_mask(-EYE_X - 0.004, EYE_Z + 0.004, 0.006)
    lr = t["lens_r"]
    B.ring(c, nrm, lr - 0.001, lr + 0.007, 0.012, 12, "flametal")
    B.ring(c + nrm*0.0055, nrm, lr - 0.0015, lr + 0.0005, 0.002, 12, "ember_rim")
    B.disc(c + nrm*0.004, nrm, lr, 12, "obsidian_lens", dome=0.003)
    # left eye (+X): sealed socket plate with the Ansuz rune
    c2, n2 = on_mask(EYE_X + 0.004, EYE_Z + 0.004, 0.005)
    sr = t["socket_r"]
    B.ring(c2, n2, sr - 0.004, sr + 0.004, 0.008, 10, "flametal")
    B.disc(c2 + n2*0.003, n2, sr - 0.003, 10, "flametal")
    ax, u, w = n2, Vector((0, 0, 1)), n2.cross(Vector((0, 0, 1))).normalized()
    if w.x < 0: w = -w
    o = c2 + n2*0.0045
    def bar(p0, p1, wd=0.0025):
        p0 = o + w*p0[0] + u*p0[1]; p1 = o + w*p1[0] + u*p1[1]
        d = (p1 - p0); s = d.cross(n2).normalized()*wd/2
        B.face([p0 - s, p0 + s, p1 + s, p1 - s], "rune_inlay")
        # tiny side walls so the inlay has some depth
        B.face([p0 - s, p1 - s, p1 - s - n2*0.0015, p0 - s - n2*0.0015], "rune_inlay")
        B.face([p0 + s, p1 + s, p1 + s - n2*0.0015, p0 + s - n2*0.0015], "rune_inlay")
    bar((-0.004, -0.014), (-0.004, 0.014))                 # stave
    bar((-0.004, 0.014), (0.008, 0.005)); bar((-0.004, 0.005), (0.008, -0.004))   # Ansuz branches
    # black leather strap from the mask edges around the back, chain links at the temples
    pts = [ell(th, STRAP_A + 0.002, STRAP_B, EYE_Z + 0.004) for th in [th1 + 18 + i*(360 - (th1 - th0) - 36)/22 for i in range(23)]]
    B.ribbon(pts, t["strap_w"], t["strap_th"], "black_leather")
    for side_pts in ((ell(th1 + 3, a, b, EYE_Z + 0.004), pts[0]), (ell(th0 - 3, a, b, EYE_Z + 0.004), pts[-1])):
        p0, p1 = side_pts
        for k in range(t["links"]):
            q = p0.lerp(p1, (k + 0.5)/t["links"])
            d = (p1 - p0).normalized()
            nrm_out = Vector((q.x, q.y, 0)).normalized()
            axis = nrm_out if k % 2 == 0 else Vector((0, 0, 1))
            B.torus(q, axis, 0.0045, 0.0013, 6, 3, "chain", stretch=1.0)
    return B.finish("goggles_t3")

# ---------------------------------------------------------------- build + export
models = {}
for i, (k, fn) in enumerate((("t1", watchman), ("t2", mimir), ("t3", allfather))):
    models[k] = fn(random.Random(SEED + i))

for k, ob in models.items():
    ob.data.calc_loop_triangles()
    print(f"TRIS goggles_{k}: {len(ob.data.loop_triangles)}")
    bpy.ops.object.select_all(action='DESELECT'); ob.select_set(True)
    bpy.context.view_layer.objects.active = ob
    bpy.ops.export_scene.fbx(filepath=os.path.join(OUT, f"goggles_{k}.fbx"),
        use_selection=True, axis_forward='-Z', axis_up='Y',
        global_scale=1.0, apply_unit_scale=True, apply_scale_options='FBX_SCALE_NONE',
        bake_space_transform=True, mesh_smooth_type='FACE', add_leaf_bones=False,
        object_types={'MESH', 'EMPTY'}, use_mesh_modifiers=True)

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

g = bpy.data.objects.new("ground", bpy.data.meshes.new("ground"))
bm = bmesh.new(); bmesh.ops.create_grid(bm, x_segments=1, y_segments=1, size=20.0)
bm.to_mesh(g.data); bm.free(); scene.collection.objects.link(g)
g.data.materials.append(mat("ground", (0.20, 0.19, 0.17, 1), rough=0.9))

def light(name, kind, loc, energy, color, size=1.0, target=(0, 0, HEAD_Z)):
    l = bpy.data.lights.new(name, kind); l.energy = energy; l.color = color
    if kind == 'AREA': l.size = size
    o = bpy.data.objects.new(name, l); o.location = loc
    scene.collection.objects.link(o)
    o.rotation_euler = (Vector(target) - Vector(loc)).to_track_quat('-Z', 'Y').to_euler()
light("key", 'AREA', (-1.0, -1.6, 1.4), 55, (1.0, 0.88, 0.75), 1.2)
light("fill", 'AREA', (1.6, -1.2, 0.6), 15, (0.75, 0.82, 1.0), 1.5)
light("rim", 'AREA', (0.3, 1.6, 1.2), 40, (1.0, 0.9, 0.8), 1.2)
light("sky", 'SUN', (0.0, 0.0, 5.0), 0.6, (0.9, 0.92, 1.0))

cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam"))
cam.data.lens = 35; scene.collection.objects.link(cam)
cam.location = (0.0, -1.06, 0.38)
cam.rotation_euler = (Vector((0.0, 0, HEAD_Z - 0.01)) - cam.location).to_track_quat('-Z', 'Y').to_euler()
scene.camera = cam

w = bpy.data.worlds.new("w"); w.use_nodes = True
w.node_tree.nodes["Background"].inputs[0].default_value = (0.16, 0.155, 0.15, 1)
scene.world = w

scene.view_settings.view_transform = "Standard"
scene.render.resolution_x, scene.render.resolution_y = 1280, 720
scene.render.filepath = os.path.join(OUT, "preview-goggles.png")
scene.render.image_settings.file_format = 'PNG'
for eng in ('BLENDER_EEVEE', 'BLENDER_EEVEE_NEXT', 'BLENDER_WORKBENCH'):
    try: scene.render.engine = eng; break
    except TypeError: continue
bpy.ops.render.render(write_still=True)
shutil.copyfile(scene.render.filepath, PREVIEW)
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT, "goggles.blend"))
