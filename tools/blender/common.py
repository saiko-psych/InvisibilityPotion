"""Shared helpers for the procedural asset scripts (Blender 5.2, run headless with `blender -b --python <script>`).

Conventions (spec 2026-10-02-plan4-assets-design.md section 5.1):
- Blender is Z-up; every FBX is exported Y-up, 1 unit = 1 m, flat shaded, the root object's transform zeroed.
- Pivot per asset: bottles, bowls and plants at the base, goggles at the head centre.
- Anchor empties (`MistAnchor`, `PickAnchor`, `EmberAnchor`, `attach`) live in the .blend under unique temporary
  names and are renamed to their exact name only while their own FBX is written, so no `.001` suffix can leak.
- Materials are named exactly as the Unity `.mat` files; a material table entry is
  (colour RGBA, roughness, metallic, emission strength[, alpha]).
"""
import bpy, bmesh, math, os, shutil
from mathutils import Vector

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, "out")            # FBX, raw renders, .blend, logs (gitignored)
os.makedirs(OUT, exist_ok=True)

ANCHOR_KEY = "anchor_name"                 # custom property holding an empty's exact export name


def reset():
    """Empty factory scene; returns it."""
    bpy.ops.wm.read_factory_settings(use_empty=True)
    return bpy.context.scene


# ---------------------------------------------------------------- materials
def mat(name, color, alpha=1.0, rough=0.55, metal=0.0, emit=0.0):
    """Flat-colour Principled material; alpha < 1 renders blended. Double-sided."""
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


def mat_from(name, spec):
    """Material from a table entry (colour, roughness, metallic, emission[, alpha]); reused if it exists."""
    m = bpy.data.materials.get(name)
    if m: return m
    c, ro, mt, em = spec[:4]
    al = spec[4] if len(spec) > 4 else 1.0
    return mat(name, c, al, ro, mt, em)


# ---------------------------------------------------------------- geometry helpers
def basis(axis):
    """(axis, u, w): unit axis plus two perpendicular unit vectors."""
    ax = Vector(axis).normalized()
    ref = Vector((0, 0, 1)) if abs(ax.z) < 0.9 else Vector((1, 0, 0))
    u = (ref - ax*ref.dot(ax)).normalized()
    return ax, u, ax.cross(u)


def bezier(pts, n):
    """Catmull-Rom through pts, n samples."""
    P = [Vector(p) for p in pts]; P = [P[0]] + P + [P[-1]]
    out = []
    for i in range(n):
        u = i/(n-1)*(len(P)-3); s = min(int(u), len(P)-4); t = u - s
        p0, p1, p2, p3 = P[s:s+4]
        out.append(0.5*((2*p1) + (-p0+p2)*t + (2*p0-5*p1+4*p2-p3)*t*t + (-p0+3*p1-3*p2+p3)*t**3))
    return out


def r_at(pts, z):
    """Radius of a (radius, height[, ...]) profile at height z (linear between points)."""
    for a, b in zip(pts, pts[1:]):
        (r0, z0), (r1, z1) = a[:2], b[:2]
        if z0 <= z <= z1 and z1 > z0:
            return r0 + (r1-r0)*(z-z0)/(z1-z0)
    return pts[-1][0]


class Builder:
    """One bmesh with named material slots; `mats` is the material table used by finish()."""
    def __init__(self, mats, rng=None):
        self.bm = bmesh.new(); self.slots = []; self.mats = mats; self.rng = rng

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

    def tube(self, pts, radii, sides, mats, cap=False):
        """Tube along pts. radius 0 -> single tip vertex. mats: name or one name per segment."""
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
            if r <= 0: rings.append([self.v(p)])
            else: rings.append([self.v(p + r*(math.cos(2*math.pi*k/sides)*n + math.sin(2*math.pi*k/sides)*b))
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

    def lathe(self, profile, mats, segs, jitter=0.0, rng=None, rot=0.0, z_jitter=0.0):
        """Surface of revolution around Z. profile: (radius, height[, twist]) bottom -> top; radius 0 = pole vertex;
        twist (in segment steps, e.g. 0.5) rotates that ring so the band to its neighbours becomes triangles
        (cut-glass facets). mats: name, one name per band, or a callable (band, column) -> name.
        jitter scales each ring's radius by 1 +- jitter. Returns the rings (lists of BMVerts)."""
        rings = []
        step = 2*math.pi/segs
        for p in profile:
            r, z = p[0], p[1]; tw = p[2] if len(p) > 2 else 0.0
            if r == 0:
                rings.append([self.v((0, 0, z))]); continue
            k = 1 + (rng.uniform(-jitter, jitter) if rng and jitter else 0)
            ring = []
            for i in range(segs):
                a = rot + (i + tw)*step
                dz = rng.uniform(-z_jitter, z_jitter) if rng and z_jitter else 0.0
                ring.append(self.v((r*k*math.cos(a), r*k*math.sin(a), z + dz)))
            rings.append(ring)
        tws = [(p[2] if len(p) > 2 else 0.0) for p in profile]
        for bi, (a, b) in enumerate(zip(rings, rings[1:])):
            for i in range(segs):
                m = mats if isinstance(mats, str) else (mats(bi, i) if callable(mats) else mats[bi])
                j = (i+1) % segs
                if len(a) == 1: self.face([a[0], b[j], b[i]], m)
                elif len(b) == 1: self.face([a[i], a[j], b[0]], m)
                elif abs(tws[bi+1] - tws[bi]) < 1e-6: self.face([a[i], a[j], b[j], b[i]], m)
                elif tws[bi+1] > tws[bi]:          # upper ring shifted forward half a step
                    self.face([a[i], a[j], b[i]], m); self.face([a[j], b[j], b[i]], m)
                else:                              # upper ring shifted back
                    self.face([a[i], a[j], b[j]], m); self.face([a[i], b[j], b[i]], m)
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
        bot = [self.v(c + Vector((r*math.cos(rot+2*math.pi*k/sides), r*math.sin(rot+2*math.pi*k/sides), z0)))
               for k in range(sides)]
        top = [self.v(v.co + Vector((0, 0, h - z0))) for v in bot]
        for k in range(sides):
            j = (k+1) % sides
            self.face([bot[k], bot[j], top[j], top[k]], m)
        self.face(top, top_mat or m)

    def octa(self, center, r, m):
        c = Vector(center)
        ax = [Vector((r, 0, 0)), Vector((0, r, 0)), Vector((-r, 0, 0)), Vector((0, -r, 0))]
        top = self.v(c + Vector((0, 0, r*1.2))); bot = self.v(c - Vector((0, 0, r*1.2)))
        ring = [self.v(c + a) for a in ax]
        for k in range(4):
            j = (k+1) % 4
            self.face([ring[k], ring[j], top], m); self.face([ring[j], ring[k], bot], m)

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

    def strokes(self, origin, normal, up, lines, width, height, m):
        """Raised bars (e.g. a rune) on a surface: lines are ((x0, y0), (x1, y1)) in metres in the plane
        spanned by right = up x normal and up; each bar is a closed box `height` proud of origin."""
        n = Vector(normal).normalized(); up = (Vector(up) - n*Vector(up).dot(n)).normalized()
        right = up.cross(n)
        for (x0, y0), (x1, y1) in lines:
            p0 = Vector(origin) + right*x0 + up*y0; p1 = Vector(origin) + right*x1 + up*y1
            d = p1 - p0; L = d.length; d.normalize()
            s = d.cross(n)
            self.box((p0 + p1)/2 + n*height/2, d*(L + width*0.6), s*width, n*height, m)

    def finish(self, name, collection=None):
        bmesh.ops.recalc_face_normals(self.bm, faces=self.bm.faces)
        me = bpy.data.meshes.new(name); self.bm.to_mesh(me); self.bm.free()
        for f in me.polygons: f.use_smooth = False
        for s in self.slots: me.materials.append(mat_from(s, self.mats[s]))
        ob = bpy.data.objects.new(name, me); (collection or bpy.context.scene.collection).objects.link(ob)
        return ob


# ---------------------------------------------------------------- anchors + export
def empty(name, loc, parent, size=0.03):
    """Anchor empty parented to `parent`. Lives under a unique temporary name; export_fbx renames it to `name`."""
    a = bpy.data.objects.new(f"{name}__{parent.name}", None); bpy.context.scene.collection.objects.link(a)
    a.empty_display_type = 'PLAIN_AXES'; a.empty_display_size = size
    a.location = loc; a.parent = parent
    a[ANCHOR_KEY] = name
    return a


def tri_count(root):
    """Triangles of root plus all mesh descendants (evaluated, so modifiers count)."""
    dg = bpy.context.evaluated_depsgraph_get(); n = 0
    for o in [root] + list(root.children_recursive):
        if o.type != 'MESH': continue
        ev = o.evaluated_get(dg); me = ev.to_mesh(); me.calc_loop_triangles()
        n += len(me.loop_triangles); ev.to_mesh_clear()
    return n


def export_fbx(path, root):
    """Export root and all descendants (meshes + empties) as FBX: Y-up, 1 unit = 1 m, flat shading.
    The root's own transform is zeroed during the export (pivot = object origin); anchors get their exact names."""
    kids = list(root.children_recursive)
    anchors = [o for o in kids if ANCHOR_KEY in o]
    temp = [o.name for o in anchors]
    for o in anchors:
        o.name = o[ANCHOR_KEY]
        if o.name != o[ANCHOR_KEY]:
            raise RuntimeError(f"anchor name clash: wanted {o[ANCHOR_KEY]!r}, got {o.name!r}")
    loc, rot, scl = root.location.copy(), root.rotation_euler.copy(), root.scale.copy()
    root.location = (0, 0, 0); root.rotation_euler = (0, 0, 0); root.scale = (1, 1, 1)
    bpy.context.view_layer.update()
    try:
        bpy.ops.object.select_all(action='DESELECT')
        for o in [root] + kids: o.select_set(True)
        bpy.context.view_layer.objects.active = root
        bpy.ops.export_scene.fbx(filepath=path,
            use_selection=True, axis_forward='-Z', axis_up='Y',
            global_scale=1.0, apply_unit_scale=True, apply_scale_options='FBX_SCALE_NONE',
            bake_space_transform=True, mesh_smooth_type='FACE', add_leaf_bones=False,
            object_types={'MESH', 'EMPTY'}, use_mesh_modifiers=True)
    finally:
        for o, t in zip(anchors, temp): o.name = t
        root.location, root.rotation_euler, root.scale = loc, rot, scl


def write_log(name, lines):
    """Print lines and write them to out/<name>.log (tri counts and sizes per exported model)."""
    for l in lines: print(l)
    with open(os.path.join(OUT, f"{name}.log"), "w") as f:
        f.write("\n".join(lines) + "\n")


# ---------------------------------------------------------------- preview scene
def ground(size=20.0, color=(0.20, 0.19, 0.17, 1), rough=0.9):
    g = bpy.data.objects.new("ground", bpy.data.meshes.new("ground"))
    bm = bmesh.new(); bmesh.ops.create_grid(bm, x_segments=1, y_segments=1, size=size)
    bm.to_mesh(g.data); bm.free(); bpy.context.scene.collection.objects.link(g)
    g.data.materials.append(mat("ground", color, rough=rough))
    return g


def ruler(loc, height=0.5, band=0.1, r=0.012):
    """Scale reference: square post of `height` m in alternating `band` m light/dark bands."""
    R = Builder({"ruler_a": ((0.85, 0.85, 0.82, 1), 0.6, 0.0, 0.0), "ruler_b": ((0.05, 0.05, 0.05, 1), 0.6, 0.0, 0.0)})
    n = int(round(height/band))
    for i in range(n):
        R.prism((0, 0, 0), r, band*(i+1), 4, "ruler_a" if i % 2 == 0 else "ruler_b", rot=math.pi/4, z0=band*i)
    ob = R.finish(f"ruler_{height:g}m"); ob.location = loc
    return ob


def light(name, kind, loc, energy, color, size=1.0, target=(0, 0, 0.2)):
    l = bpy.data.lights.new(name, kind); l.energy = energy; l.color = color
    if kind == 'AREA': l.size = size
    o = bpy.data.objects.new(name, l); o.location = loc
    bpy.context.scene.collection.objects.link(o)
    o.rotation_euler = (Vector(target) - Vector(loc)).to_track_quat('-Z', 'Y').to_euler()
    return o


def camera(loc, target, lens):
    scene = bpy.context.scene
    cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam"))
    cam.data.lens = lens; scene.collection.objects.link(cam)
    cam.location = loc
    cam.rotation_euler = (Vector(target) - cam.location).to_track_quat('-Z', 'Y').to_euler()
    scene.camera = cam
    return cam


def world(color):
    w = bpy.data.worlds.new("w"); w.use_nodes = True
    w.node_tree.nodes["Background"].inputs[0].default_value = color
    bpy.context.scene.world = w
    return w


def render(png_name, preview=None, blend_name=None, res=(1280, 720), volumetrics=None):
    """Render out/<png_name> with Eevee, copy it to `preview` (a path next to the scripts), save out/<blend_name>.
    volumetrics: optional (start, end) clip range in metres for Eevee volumes (needed for small mist meshes)."""
    scene = bpy.context.scene
    scene.view_settings.view_transform = "Standard"
    scene.render.resolution_x, scene.render.resolution_y = res
    scene.render.filepath = os.path.join(OUT, png_name)
    scene.render.image_settings.file_format = 'PNG'
    for eng in ('BLENDER_EEVEE', 'BLENDER_EEVEE_NEXT', 'BLENDER_WORKBENCH'):
        try: scene.render.engine = eng; break
        except TypeError: continue
    if volumetrics:
        try:
            e = scene.eevee
            e.volumetric_tile_size = "1"
            e.use_volume_custom_range = True      # the default 100 m range is far too coarse for 0.1 m volumes
            e.volumetric_start, e.volumetric_end = volumetrics
            e.volumetric_samples = 128
        except Exception as ex: print("volumetric settings n/a:", ex)
    bpy.ops.render.render(write_still=True)
    if preview: shutil.copyfile(scene.render.filepath, preview)
    if blend_name: bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT, blend_name))


def render_panels(panels, png_name, preview=None, blend_name=None, height=720):
    """Render several camera views side by side into one PNG (out/<png_name>, copied to `preview`).
    panels: dicts with `width` (px), `cam` = (location, target, lens) and `show` = the objects visible in that view
    (every other mesh, empty and light is hidden for the panel). Panel widths should add up to the image width."""
    import numpy as np
    scene = bpy.context.scene
    cam = scene.camera or camera((0, -1, 0), (0, 0, 0), 50)
    togglable = [o for o in scene.objects if o.type in ('MESH', 'LIGHT', 'EMPTY')]
    parts = []
    for i, p in enumerate(panels):
        show = set()
        for o in p["show"]:
            show.add(o); show.update(o.children_recursive)
        for o in togglable: o.hide_render = o not in show
        loc, tgt, lens = p["cam"]
        cam.location = loc; cam.data.lens = lens
        cam.rotation_euler = (Vector(tgt) - Vector(loc)).to_track_quat('-Z', 'Y').to_euler()
        path = os.path.join(OUT, f"_panel{i}.png")
        render(f"_panel{i}.png", res=(p["width"], height))
        img = bpy.data.images.load(path)
        parts.append(np.array(img.pixels[:], dtype=np.float32).reshape(height, p["width"], 4))
        bpy.data.images.remove(img); os.remove(path)
    for o in togglable: o.hide_render = False
    full = np.concatenate(parts, axis=1)
    W = full.shape[1]
    out = bpy.data.images.new("panels", W, height, alpha=True)
    out.pixels = full.ravel().tolist()
    out.filepath_raw = os.path.join(OUT, png_name); out.file_format = 'PNG'
    out.save()
    if preview: shutil.copyfile(out.filepath_raw, preview)
    if blend_name: bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT, blend_name))
