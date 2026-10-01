"""Procedural low-poly Viking veil-mead flasks, 3 tiers. Run: blender -b --python make_bottle.py"""
import bpy, bmesh, math, os, random
from mathutils import Vector

OUT = os.path.dirname(os.path.abspath(__file__))

# ==== KNOBS (lengths in metres; FBX is Y-up, 1 unit = 1 m, pivot at base) ====
SEED = 7                 # deterministic; each tier uses SEED + tier index
MIST_INSET = 0.005       # mist volume is this much smaller than the glass (no z-fight)
MIST_FILL = 0.62         # mist fill height as fraction of bottle height
MIST_DENSITY = 6.0
MIST_ANISOTROPY = 0.3
GLASS_ALPHA = 0.45
GLASS_ROUGH = 0.5
BASE_DARKEN = 0.45       # thick dark glass bottom (tiers 1+2) colour multiplier
DARK_BASE_HEIGHT = 0.0105

# Material table: key -> (colour RGBA, roughness, metallic, emission strength)
MATS = {
    "cork":    ((0.45, 0.33, 0.20, 1), 0.9, 0.0, 0.0),
    "twine":   ((0.35, 0.28, 0.18, 1), 0.95, 0.0, 0.0),
    "iron":    ((0.16, 0.16, 0.17, 1), 0.4, 0.8, 0.0),
    "silver":  ((0.62, 0.63, 0.66, 1), 0.3, 0.9, 0.0),
    "crystal": ((0.08, 0.03, 0.13, 1), 0.2, 0.0, 0.5),
}

# Per tier: profiles are (radius, height) lists, bottom -> top. Edit freely.
TIERS = {
 "t1": dict(  # Faint Veil, 0.16 m: humble squat hand-blown flask, plain cork, twine
    segs=10, jitter=0.03, glass=(0.153, 0.255, 0.119, 1), dark_base=True, mist_emit=3.0,
    height=0.16,
    profile_points=[            # glass
        (0.000, 0.000), (0.032, 0.000), (0.044, 0.008), (0.048, 0.050), (0.040, 0.085),
        (0.022, 0.108), (0.0135, 0.122), (0.0135, 0.142), (0.0175, 0.150), (0.015, 0.153)],
    parts=[
        ("cork", [(0.000, 0.132), (0.0115, 0.132), (0.0145, 0.162), (0.000, 0.162)]),
        ("twine", [(0.0125, 0.127), (0.0158, 0.130), (0.0125, 0.133)]),
        ("twine", [(0.0125, 0.137), (0.0158, 0.140), (0.0125, 0.143)]),
    ]),
 "t2": dict(  # Deep Veil, 0.22 m: slim shoulders, iron neck band with rune ring, iron-capped cork
    segs=10, jitter=0.03, glass=(0.090, 0.150, 0.250, 1), dark_base=True, mist_emit=3.0,
    height=0.22,
    profile_points=[
        (0.000, 0.000), (0.040, 0.000), (0.054, 0.010), (0.058, 0.070), (0.048, 0.115),
        (0.030, 0.145), (0.0165, 0.168), (0.0165, 0.208), (0.021, 0.216), (0.0185, 0.220)],
    parts=[
        ("cork", [(0.000, 0.192), (0.0145, 0.192), (0.0165, 0.222), (0.000, 0.222)]),
        ("iron", [(0.0185, 0.221), (0.0185, 0.231), (0.000, 0.231)]),                  # flat cap
        ("iron", [(0.0155, 0.176), (0.0212, 0.176), (0.0212, 0.196), (0.0155, 0.196)]),  # neck band
        ("iron", [(0.0205, 0.1835), (0.0230, 0.186), (0.0205, 0.1885)]),               # rune ring
    ]),
 "t3": dict(  # Shadow Veil, 0.26 m: faceted decanter, silver cup + bands + stopper, crystal
    segs=8, jitter=0.012, glass=(0.221, 0.119, 0.255, 1), dark_base=False, mist_emit=4.0,
    height=0.26,
    profile_points=[
        (0.000, 0.000), (0.040, 0.000), (0.052, 0.011), (0.058, 0.055), (0.050, 0.105),
        (0.030, 0.145), (0.019, 0.168), (0.019, 0.206), (0.024, 0.216), (0.021, 0.220)],
    parts=[
        ("silver", [(0.043, 0.000), (0.055, 0.012), (0.0605, 0.052), (0.0590, 0.055)]),  # base cup
        ("silver", [(0.0180, 0.178), (0.0225, 0.181), (0.0225, 0.191), (0.0180, 0.194)]),  # band 1
        ("silver", [(0.0180, 0.198), (0.0225, 0.201), (0.0225, 0.209), (0.0180, 0.212)]),  # band 2
        ("silver", [(0.000, 0.214), (0.014, 0.214), (0.0175, 0.228), (0.012, 0.240), (0.000, 0.240)]),  # stopper
        ("crystal", [(0.000, 0.2405), (0.0067, 0.2433), (0.0095, 0.250), (0.0067, 0.2567), (0.000, 0.2595)]),
    ]),
}
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
    if alpha < 1: m.surface_render_method = 'BLENDED'
    return m

def mist_mat(name, color, emit):
    m = bpy.data.materials.new(name); m.use_nodes = True
    nt = m.node_tree
    for n in list(nt.nodes):
        if n.type == 'BSDF_PRINCIPLED': nt.nodes.remove(n)
    out = nt.nodes["Material Output"]
    v = nt.nodes.new("ShaderNodeVolumePrincipled")
    v.inputs["Color"].default_value = color
    v.inputs["Density"].default_value = MIST_DENSITY
    v.inputs["Anisotropy"].default_value = MIST_ANISOTROPY
    if emit:
        v.inputs["Emission Strength"].default_value = emit
        v.inputs["Emission Color"].default_value = color
    nt.links.new(v.outputs["Volume"], out.inputs["Volume"])
    m.diffuse_color = color
    return m

def lathe(bm, profile, mi, segs, jitter=0.0, rng=None, dark_below=None):
    rings = []
    for r, z in profile:
        if r == 0:
            rings.append([bm.verts.new((0, 0, z))])
        else:
            k = 1 + (rng.uniform(-jitter, jitter) if rng else 0)
            rings.append([bm.verts.new((r*k*math.cos(2*math.pi*i/segs),
                                        r*k*math.sin(2*math.pi*i/segs), z))
                          for i in range(segs)])
    for a, b in zip(rings, rings[1:]):
        for i in range(segs):
            j = (i+1) % segs
            if len(a) == 1:   vs = [a[0], b[j], b[i]]
            elif len(b) == 1: vs = [a[i], a[j], b[0]]
            else:             vs = [a[i], a[j], b[j], b[i]]
            try:
                f = bm.faces.new(vs); f.material_index = mi
                if dark_below is not None and all(v.co.z <= dark_below for v in vs):
                    f.material_index = 1
            except ValueError: pass

def finish(bm, name):
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    me = bpy.data.meshes.new(name); bm.to_mesh(me); bm.free()
    for f in me.polygons: f.use_smooth = False
    ob = bpy.data.objects.new(name, me); scene.collection.objects.link(ob)
    return ob

def r_at(pts, z):
    for (r0, z0), (r1, z1) in zip(pts, pts[1:]):
        if z0 <= z <= z1 and z1 > z0:
            return r0 + (r1-r0)*(z-z0)/(z1-z0)
    return pts[-1][0]

def mist_profile(pts, height):
    z0, zf = 0.010, MIST_FILL * height
    mid = [(r - MIST_INSET, z) for r, z in pts if z0 < z < zf and r > 0]
    return ([(0, z0), (r_at(pts, z0) - MIST_INSET, z0)] + mid +
            [(r_at(pts, zf) - MIST_INSET, zf), (0, zf)])

def make(tier, idx, t):
    rng = random.Random(SEED + idx)
    segs, jit, color = t["segs"], t["jitter"], t["glass"]
    bm = bmesh.new()
    lathe(bm, t["profile_points"], 0, segs, jit, rng, DARK_BASE_HEIGHT if t["dark_base"] else None)
    keys = []
    for key, prof in t["parts"]:
        if key not in keys: keys.append(key)
        lathe(bm, prof, 2 + keys.index(key), segs, jit, rng)
    ob = finish(bm, "bottle_"+tier)
    me = ob.data
    me.materials.append(mat(tier+"_glass", color, GLASS_ALPHA, GLASS_ROUGH))
    me.materials.append(mat(tier+"_base", tuple(c*BASE_DARKEN for c in color[:3])+(1,), 0.9, GLASS_ROUGH))
    for k in keys:
        c, ro, me_, em = MATS[k]
        me.materials.append(mat(tier+"_"+k, c, 1.0, ro, me_, em))
    # misty inner volume
    bm = bmesh.new(); lathe(bm, mist_profile(t["profile_points"], t["height"]), 0, segs)
    mist = finish(bm, "mist_"+tier)
    mc = tuple(min(1.0, c*2.2) for c in color[:3]) + (1,)
    mist.data.materials.append(mist_mat(tier+"_mist", mc, t["mist_emit"]))
    mist.parent = ob
    # empty for Unity particle attachment
    a = bpy.data.objects.new("MistAnchor", None); scene.collection.objects.link(a)
    a.empty_display_type = 'PLAIN_AXES'; a.empty_display_size = 0.01
    a.location = (0, 0, (0.010 + MIST_FILL*t["height"]) / 2)
    a.parent = ob
    return ob, [mist, a]

objs = {}
for i, (k, t) in enumerate(TIERS.items()):
    ob, kids = make(k, i, t)
    ob.location = ((i-1)*0.2, 0, 0)
    objs[k] = (ob, kids)

dg = bpy.context.evaluated_depsgraph_get()
for k, (ob, kids) in objs.items():
    n = 0
    for o in [ob] + [x for x in kids if x.type == 'MESH']:
        me = o.evaluated_get(dg).to_mesh(); me.calc_loop_triangles()
        n += len(me.loop_triangles); o.evaluated_get(dg).to_mesh_clear()
    print(f"TRIS bottle_{k}: {n}")
    loc = ob.location.copy(); ob.location = (0, 0, 0)
    bpy.ops.object.select_all(action='DESELECT')
    for o in [ob] + kids: o.select_set(True)
    bpy.context.view_layer.objects.active = ob
    bpy.ops.export_scene.fbx(filepath=os.path.join(OUT, f"bottle_{k}.fbx"),
        use_selection=True, axis_forward='-Z', axis_up='Y',
        global_scale=1.0, apply_unit_scale=True, apply_scale_options='FBX_SCALE_NONE',
        bake_space_transform=True, mesh_smooth_type='FACE', add_leaf_bones=False,
        object_types={'MESH', 'EMPTY'}, use_mesh_modifiers=True)
    ob.location = loc

# ---- preview scene ----
g = bpy.data.objects.new("ground", bpy.data.meshes.new("ground"))
bm = bmesh.new(); bmesh.ops.create_grid(bm, x_segments=1, y_segments=1, size=2.0)
bm.to_mesh(g.data); bm.free(); scene.collection.objects.link(g)
g.data.materials.append(mat("ground", (0.10, 0.065, 0.04, 1), rough=0.85))

def light(name, kind, loc, energy, color, size=1.0):
    l = bpy.data.lights.new(name, kind); l.energy = energy; l.color = color
    if kind == 'AREA': l.size = size
    o = bpy.data.objects.new(name, l); o.location = loc
    scene.collection.objects.link(o)
    o.rotation_euler = (Vector((0, 0, 0.11)) - Vector(loc)).to_track_quat('-Z', 'Y').to_euler()
light("key", 'AREA', (0.8, -0.8, 0.8), 20, (1.0, 0.72, 0.45), 0.7)    # ~3200 K
light("fill", 'AREA', (-1.0, -0.6, 0.4), 3, (0.65, 0.75, 1.0), 1.0)
light("rim", 'AREA', (0.0, 1.0, 0.7), 12, (1.0, 0.8, 0.6), 0.6)

cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam"))
cam.data.lens = 70; scene.collection.objects.link(cam)
cam.location = (0, -1.35, 0.13)
cam.rotation_euler = (Vector((0, 0, 0.115)) - cam.location).to_track_quat('-Z', 'Y').to_euler()
scene.camera = cam

w = bpy.data.worlds.new("w"); w.use_nodes = True
w.node_tree.nodes["Background"].inputs[0].default_value = (0.09, 0.09, 0.10, 1)
scene.world = w

scene.view_settings.view_transform = "Standard"
scene.render.resolution_x, scene.render.resolution_y = 1280, 720
scene.render.filepath = os.path.join(OUT, "preview.png")
scene.render.image_settings.file_format = 'PNG'
for eng in ('BLENDER_EEVEE', 'BLENDER_EEVEE_NEXT', 'BLENDER_WORKBENCH'):
    try: scene.render.engine = eng; break
    except TypeError: continue
try:
    e = scene.eevee
    e.volumetric_tile_size = "1"
    e.use_volume_custom_range = True      # default 100 m range is far too coarse for a 0.1 m mist
    e.volumetric_start, e.volumetric_end = 1.1, 1.6
    e.volumetric_samples = 128
except Exception as e: print("volumetric_tile_size n/a:", e)
bpy.ops.render.render(write_still=True)
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT, "bottles.blend"))
