"""Procedural low-poly Viking mead flask. Run: blender -b --python make_bottle.py"""
import bpy, bmesh, math, os, random
from mathutils import Vector

OUT = os.path.dirname(os.path.abspath(__file__))

# ==== KNOBS (all lengths in metres, Y-up on export, 1 unit = 1 m) ============
SEGMENTS = 10          # sides around the axis (lower = fewer tris)
JITTER = 0.03          # +-3% radius jitter per profile ring (hand-blown look)
SEED = 7               # deterministic; each tier uses SEED + tier index
BEVEL_WIDTH = 0.0      # >0 adds a bevel modifier on sharp (>50 deg) edges, costs tris

# profile_points: (radius, height) bottom -> lip. Squat flask: 0.22 m tall, belly 0.13 m wide
# at ~35% height, neck 0.165..0.22 (25%), neck 0.035 m wide, lip flared to 0.045 m.
profile_points = [
    (0.000, 0.000),    # base centre (pivot)
    (0.045, 0.000),    # base edge
    (0.059, 0.010),    # end of thick dark glass bottom (first 0.01 m)
    (0.065, 0.077),    # belly (widest, 0.13 m, 35% height)
    (0.056, 0.118),    # upper belly
    (0.038, 0.148),    # soft shoulder
    (0.0175, 0.167),   # neck start (0.035 m wide)
    (0.0175, 0.208),   # neck end
    (0.0225, 0.216),   # flared lip (0.045 m wide)
    (0.020, 0.220),    # lip top
]
DARK_BASE_HEIGHT = 0.0105                                    # glass below this uses the dark base material
cork_profile = [(0.000, 0.190), (0.0150, 0.190), (0.0185, 0.230), (0.000, 0.230)]  # sticks out 1 cm
wax_profile = [(0.0235, 0.212), (0.0235, 0.226), (0.0205, 0.234), (0.000, 0.234)]  # tier II/III only
# liquid: 55% fill, inset 0.004+ m from the glass so it never z-fights
liquid_profile = [(0.000, 0.012), (0.040, 0.012), (0.060, 0.077), (0.050, 0.121), (0.000, 0.121)]
rope_heights = [0.177, 0.188, 0.2005]    # irregular spacing of the neck rope rings
ROPE_R_IN, ROPE_R_OUT, ROPE_HALF_H = 0.0160, 0.0198, 0.0045

TIERS = {  # name: (glass colour, has wax seal) -- colours already 15% darker than v2
    "t1": ((0.153, 0.255, 0.119, 1.0), False),   # moss green
    "t2": ((0.119, 0.187, 0.289, 1.0), True),    # cold steel blue
    "t3": ((0.221, 0.119, 0.255, 1.0), True),    # dusky violet
}
CORK_COLOR = (0.45, 0.33, 0.20, 1.0)
ROPE_COLOR = (0.35, 0.28, 0.18, 1.0)
WAX_COLOR = (0.22, 0.04, 0.04, 1.0)
GLASS_ALPHA = 0.45
GLASS_ROUGH = 0.5
LIQUID_BRIGHTNESS = 1.1
LIQUID_ALPHA = 0.9
BASE_DARKEN = 0.45        # thick glass bottom colour multiplier
# =============================================================================

bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene

def mat(name, color, alpha=1.0, rough=0.55, emit=0.0):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    b = m.node_tree.nodes["Principled BSDF"]
    b.inputs["Base Color"].default_value = color
    b.inputs["Roughness"].default_value = rough
    b.inputs["Metallic"].default_value = 0.0
    b.inputs["Alpha"].default_value = alpha
    if emit:
        b.inputs["Emission Color"].default_value = color
        b.inputs["Emission Strength"].default_value = emit
    m.diffuse_color = color
    if alpha < 1: m.surface_render_method = 'BLENDED'
    return m

def lathe(bm, profile, mi, rng=None, dark_below=None):
    rings = []
    for r, z in profile:
        if r == 0:
            rings.append([bm.verts.new((0, 0, z))])
        else:
            k = 1 + (rng.uniform(-JITTER, JITTER) if rng else 0)
            rings.append([bm.verts.new((r*k*math.cos(2*math.pi*i/SEGMENTS),
                                        r*k*math.sin(2*math.pi*i/SEGMENTS), z))
                          for i in range(SEGMENTS)])
    for a, b in zip(rings, rings[1:]):
        for i in range(SEGMENTS):
            j = (i+1) % SEGMENTS
            if len(a) == 1:   vs = [a[0], b[j], b[i]]
            elif len(b) == 1: vs = [a[i], a[j], b[0]]
            else:             vs = [a[i], a[j], b[j], b[i]]
            try:
                f = bm.faces.new(vs); f.material_index = mi
                if dark_below is not None and all(v.co.z <= dark_below for v in vs): f.material_index = 4
            except ValueError: pass

def finish(bm, name):
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    me = bpy.data.meshes.new(name); bm.to_mesh(me); bm.free()
    for f in me.polygons: f.use_smooth = False
    ob = bpy.data.objects.new(name, me); scene.collection.objects.link(ob)
    return ob

def make(tier, idx, color, wax):
    rng = random.Random(SEED + idx)
    bm = bmesh.new()
    lathe(bm, profile_points, 0, rng, DARK_BASE_HEIGHT)
    lathe(bm, cork_profile, 1, rng)
    for z in rope_heights:
        h = ROPE_HALF_H * (1 + rng.uniform(-0.2, 0.2))
        lathe(bm, [(ROPE_R_IN, z-h), (ROPE_R_OUT, z), (ROPE_R_IN, z+h)], 2, rng)
    if wax: lathe(bm, wax_profile, 3, rng)
    ob = finish(bm, "bottle_"+tier)
    me = ob.data
    me.materials.append(mat(tier+"_glass", color, GLASS_ALPHA, GLASS_ROUGH))
    me.materials.append(mat("cork", CORK_COLOR, rough=0.9))
    me.materials.append(mat("rope", ROPE_COLOR, rough=0.95))
    me.materials.append(mat("wax", WAX_COLOR, rough=0.6))
    me.materials.append(mat(tier+"_base", tuple(c*BASE_DARKEN for c in color[:3])+(1.0,), 0.9, GLASS_ROUGH))
    if BEVEL_WIDTH > 0:
        bv = ob.modifiers.new("bevel", 'BEVEL'); bv.width = BEVEL_WIDTH
        bv.segments = 1; bv.limit_method = 'ANGLE'; bv.angle_limit = math.radians(50)
    # inner liquid, same jitter seed shape but smaller
    bm = bmesh.new(); lathe(bm, liquid_profile, 0)
    lq = finish(bm, "liquid_"+tier)
    lc = tuple(min(1.0, c*LIQUID_BRIGHTNESS) for c in color[:3]) + (1.0,)
    lq.data.materials.append(mat(tier+"_liquid", lc, LIQUID_ALPHA, 0.3))
    lq.parent = ob
    return ob, lq

objs = {}
for i, (k, (c, wax)) in enumerate(TIERS.items()):
    ob, lq = make(k, i, c, wax)
    ob.location = ((i-1)*0.2, 0, 0)
    objs[k] = (ob, lq)

dg = bpy.context.evaluated_depsgraph_get()
for k, (ob, lq) in objs.items():
    n = 0
    for o in (ob, lq):
        me = o.evaluated_get(dg).to_mesh(); me.calc_loop_triangles()
        n += len(me.loop_triangles); o.evaluated_get(dg).to_mesh_clear()
    print(f"TRIS bottle_{k}: {n}")
    loc = ob.location.copy(); ob.location = (0, 0, 0)
    bpy.ops.object.select_all(action='DESELECT')
    ob.select_set(True); lq.select_set(True)
    bpy.context.view_layer.objects.active = ob
    bpy.ops.export_scene.fbx(filepath=os.path.join(OUT, f"bottle_{k}.fbx"),
        use_selection=True, axis_forward='-Z', axis_up='Y',
        global_scale=1.0, apply_unit_scale=True, apply_scale_options='FBX_SCALE_NONE',
        bake_space_transform=True, mesh_smooth_type='FACE', add_leaf_bones=False,
        object_types={'MESH'}, use_mesh_modifiers=True)
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
cam.location = (0, -1.3, 0.12)
cam.rotation_euler = (Vector((0, 0, 0.11)) - cam.location).to_track_quat('-Z', 'Y').to_euler()
scene.camera = cam

w = bpy.data.worlds.new("w"); w.use_nodes = True
w.node_tree.nodes["Background"].inputs[0].default_value = (0.03, 0.03, 0.035, 1)
scene.world = w

scene.view_settings.view_transform = "Standard"
scene.render.resolution_x, scene.render.resolution_y = 1280, 720
scene.render.filepath = os.path.join(OUT, "preview.png")
scene.render.image_settings.file_format = 'PNG'
for eng in ('BLENDER_EEVEE', 'BLENDER_EEVEE_NEXT', 'BLENDER_WORKBENCH'):
    try: scene.render.engine = eng; break
    except TypeError: continue
bpy.ops.render.render(write_still=True)
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT, "bottles.blend"))
