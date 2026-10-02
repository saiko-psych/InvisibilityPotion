"""Procedural low-poly Viking veil-mead flasks (finished meads), 3 tiers, v5.
Run from the repo root: blender -b --python tools/blender/make_bottle.py
Icon mode: blender -b --python tools/blender/make_bottle.py -- --icon
  renders only the tier III flask on a dark radial gradient at 256x256 into InvisibilityPotion/Package/icon.png
  (the Thunderstore package icon); no FBX export, no preview.

Tier I   Faint Veil   0.16 m  squat moss-green flask, tan cork, twine rings
Tier II  Deep Veil    0.22 m  steel-blue flask, iron neck band + cap, leather label band with a raised Hagalaz rune
Tier III Shadow Veil  0.26 m  cut-glass violet decanter on a slim silver foot, silver plaque with an Ansuz rune,
                              silver stopper with a dark crystal
Each FBX: the flask mesh (pivot at the base), a `mist_tN` inner mesh, empties `MistAnchor` (mist centre, for the
Unity particle system) and `attach` (neck, the grip point VisEquipment looks for). Blender front = -Y (label side).
"""
import math, os, random, sys
from mathutils import Vector
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bpy
from common import OUT, HERE, Builder, mat, r_at, empty, export_fbx, tri_count, write_log, reset, \
    ground, ruler, light, camera, world, render

PREVIEW = os.path.join(HERE, "preview-bottles-v5.png")
ICON_MODE = "--icon" in (sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else [])
ICON = os.path.join(HERE, "..", "..", "InvisibilityPotion", "Package", "icon.png")
ICON_SIZE = 256
ICON_INNER = (0.16, 0.12, 0.22)   # gradient centre colour (dim violet behind the flask)
ICON_OUTER = (0.02, 0.02, 0.03)   # gradient edge colour

# ==== KNOBS (lengths in metres; FBX is Y-up, 1 unit = 1 m, pivot at base) ====
SEED = 7                 # deterministic; each tier uses SEED + tier index
MIST_INSET = 0.005       # mist volume is this much inside the glass (no z-fight)
MIST_FILL = 0.62         # mist fill height as fraction of bottle height
MIST_DENSITY = 6.0
MIST_ANISOTROPY = 0.3
GLASS_ALPHA = 0.45
GLASS_ROUGH = 0.5
BASE_DARKEN = 0.45       # thick dark glass bottom (tiers 1+2) colour multiplier
DARK_BASE_HEIGHT = 0.0105
LABEL_OFFSET = 0.0012    # label band stands this far off the glass
RUNE_HEIGHT = 0.0010     # raised rune bars above the label / plaque
RUNE_WIDTH = 0.0026

# Material table: name -> (colour RGBA, roughness, metallic, emission strength[, alpha]). Names = Unity .mat names.
MATS = {
    "bottle_cork":    ((0.45, 0.33, 0.20, 1), 0.9, 0.0, 0.0),
    "bottle_twine":   ((0.35, 0.28, 0.18, 1), 0.95, 0.0, 0.0),
    "bottle_iron":    ((0.16, 0.16, 0.17, 1), 0.4, 0.8, 0.0),
    "bottle_silver":  ((0.62, 0.63, 0.66, 1), 0.3, 0.9, 0.0),
    "bottle_crystal": ((0.08, 0.03, 0.13, 1), 0.2, 0.0, 0.5),
    "bottle_label":   ((0.16, 0.10, 0.06, 1), 0.85, 0.0, 0.0),   # dark leather
    "bottle_rune":    ((0.78, 0.72, 0.58, 1), 0.6, 0.0, 0.0),    # bone-white raised rune (tier 2)
    "bottle_rune_dark": ((0.05, 0.03, 0.07, 1), 0.4, 0.0, 0.0),  # dark rune on the silver plaque (tier 3)
}

# Runes as strokes ((x0, y0), (x1, y1)) in a unit box -1..1 (scaled by the tier's rune size).
RUNES = {
    "hagalaz": [((-0.5, -1), (-0.5, 1)), ((0.5, -1), (0.5, 1)), ((-0.5, 0.35), (0.5, -0.35))],
    "ansuz":   [((-0.45, -1), (-0.45, 1)), ((-0.45, 1), (0.55, 0.45)), ((-0.45, 0.35), (0.55, -0.2))],
}

# Per tier: profiles are (radius, height[, twist in segment steps]) lists, bottom -> top. Edit freely.
TIERS = {
 "t1": dict(  # Faint Veil, 0.16 m: humble squat hand-blown flask, plain cork, twine
    segs=10, rot=0.0, jitter=0.03, glass=(0.153, 0.255, 0.119, 1), dark_base=True, mist_emit=3.0,
    height=0.16, attach_z=0.132,
    profile_points=[            # glass
        (0.000, 0.000), (0.032, 0.000), (0.044, 0.008), (0.048, 0.050), (0.040, 0.085),
        (0.022, 0.108), (0.0135, 0.122), (0.0135, 0.142), (0.0175, 0.150), (0.015, 0.153)],
    parts=[
        ("bottle_cork", [(0.000, 0.132), (0.0115, 0.132), (0.0145, 0.162), (0.000, 0.162)]),
        ("bottle_twine", [(0.0125, 0.127), (0.0158, 0.130), (0.0125, 0.133)]),
        ("bottle_twine", [(0.0125, 0.137), (0.0158, 0.140), (0.0125, 0.143)]),
    ],
    label=None),
 "t2": dict(  # Deep Veil, 0.22 m: slim shoulders, iron neck band, iron-capped cork, leather label with Hagalaz
    segs=10, rot=0.0, jitter=0.03, glass=(0.090, 0.150, 0.250, 1), dark_base=True, mist_emit=3.0,
    height=0.22, attach_z=0.185,
    profile_points=[
        (0.000, 0.000), (0.040, 0.000), (0.054, 0.010), (0.058, 0.070), (0.048, 0.115),
        (0.030, 0.145), (0.0165, 0.168), (0.0165, 0.208), (0.021, 0.216), (0.0185, 0.220)],
    parts=[
        ("bottle_cork", [(0.000, 0.192), (0.0145, 0.192), (0.0165, 0.222), (0.000, 0.222)]),
        ("bottle_iron", [(0.0185, 0.221), (0.0185, 0.231), (0.000, 0.231)]),                  # flat cap
        ("bottle_iron", [(0.0155, 0.176), (0.0212, 0.176), (0.0212, 0.196), (0.0155, 0.196)]),  # neck band
    ],
    # label band: wraps `cols` faces centred on the front, from z0 to z1; rune drawn on the middle face
    label=dict(kind="band", cols=3, z0=0.056, z1=0.088, rune="hagalaz", rune_size=0.0105, rune_mat="bottle_rune")),
 "t3": dict(  # Shadow Veil, 0.26 m: cut-glass decanter, slim silver foot, plaque, band, stopper, crystal
    segs=8, rot=math.pi/8, jitter=0.0, glass=(0.221, 0.119, 0.255, 1), dark_base=False, mist_emit=4.0,
    height=0.26, attach_z=0.190,
    profile_points=[            # twist 0.5 turns a band into triangle facets (lower bevel, shoulder)
        (0.000, 0.016), (0.029, 0.016, 0.5), (0.052, 0.040), (0.054, 0.120), (0.040, 0.146, 0.5),
        (0.019, 0.168), (0.019, 0.206), (0.024, 0.216), (0.021, 0.220)],
    parts=[
        ("bottle_silver", [(0.000, 0.000), (0.038, 0.000), (0.040, 0.0035), (0.030, 0.007), (0.017, 0.011),
                           (0.017, 0.014), (0.032, 0.018), (0.0405, 0.0245), (0.037, 0.0255)]),         # slim foot
        ("bottle_silver", [(0.0180, 0.183), (0.0225, 0.186), (0.0225, 0.196), (0.0180, 0.199)]),   # neck band
        ("bottle_silver", [(0.000, 0.214), (0.014, 0.214), (0.0175, 0.228), (0.012, 0.240), (0.000, 0.240)]),  # stopper
        ("bottle_crystal", [(0.000, 0.2405), (0.0067, 0.2433), (0.0095, 0.250), (0.0067, 0.2567), (0.000, 0.2595)]),
    ],
    label=dict(kind="plaque", z0=0.058, z1=0.106, width=0.030, thick=0.0014, mat="bottle_silver",
               rune="ansuz", rune_size=0.014, rune_mat="bottle_rune_dark")),
}
# =============================================================================

scene = reset()


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


def mist_profile(pts, height, segs):
    """Inner volume: the glass profile shrunk to the inscribed polygon minus MIST_INSET, up to MIST_FILL."""
    k = math.cos(math.pi/segs)
    z0, zf = pts[0][1] + 0.010, MIST_FILL * height
    mid = [(p[0]*k - MIST_INSET, p[1]) for p in pts if z0 < p[1] < zf and p[0] > 0]
    return ([(0, z0), (r_at(pts, z0)*k - MIST_INSET, z0)] + mid +
            [(r_at(pts, zf)*k - MIST_INSET, zf), (0, zf)])


def surface(rings, zs, col, z):
    """Point on the glass surface at column col and height z (lerp along the vertical edge)."""
    for bi in range(len(zs) - 1):
        if zs[bi] <= z <= zs[bi+1] and len(rings[bi]) > 1 and len(rings[bi+1]) > 1:
            t = (z - zs[bi])/(zs[bi+1] - zs[bi])
            return rings[bi][col].co.lerp(rings[bi+1][col].co, t)
    raise ValueError(f"z {z} outside the glass")


def front_cols(segs, rot, n):
    """n face columns centred on the front (-Y). Column c spans vertices c .. c+1."""
    centre = (-math.pi/2 - rot)/(2*math.pi/segs) - 0.5          # fractional column index facing -Y
    c0 = round(centre - (n - 1)/2)
    return [(c0 + i) % segs for i in range(n)]


def out_dir(p):
    return Vector((p.x, p.y, 0)).normalized()


def label_band(B, rings, zs, segs, rot, L):
    """Leather strip wrapped around L['cols'] front faces, LABEL_OFFSET off the glass, closed edges."""
    cols = front_cols(segs, rot, L["cols"])
    levels = [L["z0"]] + [z for z in zs if L["z0"] < z < L["z1"]] + [L["z1"]]
    verts = []                                                  # verts[col_vertex][level] -> (outer, inner)
    for c in cols + [(cols[-1] + 1) % segs]:
        col = []
        for z in levels:
            p = surface(rings, zs, c, z); d = out_dir(p)
            col.append((p + d*(LABEL_OFFSET + 0.0012), p + d*0.0004))
        verts.append(col)
    for i in range(len(verts) - 1):
        a, b = verts[i], verts[i+1]
        for li in range(len(levels) - 1):
            B.face([a[li][0], b[li][0], b[li+1][0], a[li+1][0]], "bottle_label")
        B.face([a[0][1], b[0][1], b[0][0], a[0][0]], "bottle_label")            # bottom edge
        B.face([a[-1][0], b[-1][0], b[-1][1], a[-1][1]], "bottle_label")        # top edge
    for col in (verts[0], verts[-1]):                                           # side ends
        for li in range(len(levels) - 1):
            B.face([col[li][1], col[li][0], col[li+1][0], col[li+1][1]], "bottle_label")
    # rune on the middle face
    mid = cols[len(cols)//2]
    zc = (L["z0"] + L["z1"])/2
    pa, pb = surface(rings, zs, mid, zc), surface(rings, zs, (mid + 1) % segs, zc)
    c = (pa + pb)/2
    up = surface(rings, zs, mid, zc + 0.005).lerp(surface(rings, zs, (mid + 1) % segs, zc + 0.005), 0.5) - c
    n = out_dir(c); right = pb - pa
    n = right.cross(up).normalized()
    if n.dot(out_dir(c)) < 0: n = -n
    origin = c + n*(LABEL_OFFSET + 0.0012)
    rune(B, origin, n, up, L)


def plaque(B, rings, zs, segs, rot, L):
    """Thin flat plate on the front facet with the rune on it."""
    mid = front_cols(segs, rot, 1)[0]
    zc = (L["z0"] + L["z1"])/2
    pa, pb = surface(rings, zs, mid, zc), surface(rings, zs, (mid + 1) % segs, zc)
    c = (pa + pb)/2
    up = surface(rings, zs, mid, L["z1"]).lerp(surface(rings, zs, (mid + 1) % segs, L["z1"]), 0.5) - \
         surface(rings, zs, mid, L["z0"]).lerp(surface(rings, zs, (mid + 1) % segs, L["z0"]), 0.5)
    h = up.length; up.normalize()
    n = (pb - pa).cross(up).normalized()
    if n.dot(out_dir(c)) < 0: n = -n
    right = up.cross(n)
    B.box(c + n*(L["thick"]/2 + 0.0002), right*L["width"], up*h, n*L["thick"], L["mat"])
    # a thin raised rim on the plaque edge reads better than a flat plate
    rim = L["thick"]*0.8; t = 0.0016
    top = c + n*(L["thick"] + 0.0002 + rim/2)
    for dx, dy, sx, sy in ((0, h/2 - t/2, L["width"], t), (0, -h/2 + t/2, L["width"], t),
                           (L["width"]/2 - t/2, 0, t, h - 2*t), (-L["width"]/2 + t/2, 0, t, h - 2*t)):
        B.box(top + right*dx + up*dy, right*sx, up*sy, n*rim, L["mat"])
    rune(B, c + n*(L["thick"] + 0.0002), n, up, L)


def rune(B, origin, n, up, L):
    s = L["rune_size"]
    lines = [((x0*s, y0*s), (x1*s, y1*s)) for (x0, y0), (x1, y1) in RUNES[L["rune"]]]
    B.strokes(origin, n, up, lines, RUNE_WIDTH, RUNE_HEIGHT, L["rune_mat"])


def make(tier, idx, t):
    rng = random.Random(SEED + idx)
    segs, rot, color = t["segs"], t["rot"], t["glass"]
    glass, base = f"bottle_glass_{tier}", f"bottle_base_{tier}"
    mats = dict(MATS)
    mats[glass] = (color, GLASS_ROUGH, 0.0, 0.0, GLASS_ALPHA)
    mats[base] = (tuple(c*BASE_DARKEN for c in color[:3]) + (1,), GLASS_ROUGH, 0.0, 0.0, 0.9)
    B = Builder(mats)
    prof = t["profile_points"]
    zs = [p[1] for p in prof]
    band_mats = [base if t["dark_base"] and zs[i+1] <= DARK_BASE_HEIGHT else glass for i in range(len(prof) - 1)]
    rings = B.lathe(prof, band_mats, segs, t["jitter"], rng, rot)
    for key, part in t["parts"]:
        B.lathe(part, key, segs, t["jitter"], rng, rot)
    L = t["label"]
    if L and L["kind"] == "band": label_band(B, rings, zs, segs, rot, L)
    if L and L["kind"] == "plaque": plaque(B, rings, zs, segs, rot, L)
    ob = B.finish("bottle_" + tier)
    # misty inner volume (Principled Volume: preview only, does not survive FBX)
    M = Builder({})
    M.lathe(mist_profile(prof, t["height"], segs), "m", segs, rot=rot)
    M.slots = []
    mist = M.finish("mist_" + tier)
    mist.data.polygons.foreach_set("material_index", [0]*len(mist.data.polygons))
    mc = tuple(min(1.0, c*2.2) for c in color[:3]) + (1,)
    mist.data.materials.append(mist_mat(f"bottle_mist_{tier}", mc, t["mist_emit"]))
    mist.parent = ob
    empty("MistAnchor", (0, 0, (prof[0][1] + 0.010 + MIST_FILL*t["height"]) / 2), ob, 0.01)
    empty("attach", (0, 0, t["attach_z"]), ob, 0.01)
    return ob


def render_icon(ob):
    """Tier III flask alone, transparent film, then composited over a radial gradient with numpy (bundled with Blender)."""
    import numpy as np
    light("key", 'AREA', (0.6, -0.8, 0.7), 14, (1.0, 0.78, 0.55), 0.6, (0, 0, 0.13))
    light("fill", 'AREA', (-0.8, -0.6, 0.3), 4, (0.65, 0.75, 1.0), 1.0, (0, 0, 0.13))
    light("rim", 'AREA', (0.0, 0.9, 0.6), 16, (0.85, 0.7, 1.0), 0.6, (0, 0, 0.13))
    camera((0, -0.62, 0.17), (0, 0, 0.13), 70)
    world((0.05, 0.05, 0.06, 1))
    bpy.context.scene.render.film_transparent = True
    render("icon_raw.png", res=(ICON_SIZE, ICON_SIZE), volumetrics=(0.3, 1.0))
    img = bpy.data.images.load(os.path.join(OUT, "icon_raw.png"))
    w, h = img.size
    px = np.array(img.pixels[:], dtype=np.float32).reshape(h, w, 4)
    yy, xx = np.mgrid[0:h, 0:w]
    r = np.clip(np.hypot((xx - w/2)/(w/2), (yy - h*0.55)/(h/2)) / 1.1, 0, 1)[..., None]
    bg = np.array(ICON_INNER)*(1 - r) + np.array(ICON_OUTER)*r
    a = px[..., 3:4]
    out = np.concatenate([px[..., :3]*a + bg*(1 - a), np.ones_like(a)], axis=2)   # pixels are straight alpha
    icon = bpy.data.images.new("icon", w, h, alpha=True)
    icon.pixels[:] = out.ravel()
    icon.filepath_raw = os.path.abspath(ICON)
    icon.file_format = 'PNG'
    icon.save()
    print(f"ICON written: {os.path.abspath(ICON)} ({w}x{h})")


bottles = {}
for i, (k, t) in enumerate(TIERS.items()):
    if ICON_MODE and k != "t3": continue
    bottles[k] = make(k, i, t)

if ICON_MODE:
    render_icon(bottles["t3"])
    sys.exit(0)

log = []
for k, ob in bottles.items():
    hz = max((ob.matrix_world @ v.co).z for v in ob.data.vertices)
    log.append(f"TRIS bottle_{k}: {tri_count(ob)} (incl. mist mesh)  height {hz:.3f} m")
    export_fbx(os.path.join(OUT, f"bottle_{k}.fbx"), ob)
write_log("bottles", log)

# ---- preview scene ----
for i, ob in enumerate(bottles.values()):
    ob.location = ((i-1)*0.2, 0, 0)
ground(2.0, (0.10, 0.065, 0.04, 1), 0.85)
ruler((0.315, 0.10, 0), height=0.3, band=0.05, r=0.006)        # 0.3 m in 0.05 m bands
tgt = (0, 0, 0.11)
light("key", 'AREA', (0.8, -0.8, 0.8), 20, (1.0, 0.72, 0.45), 0.7, tgt)    # ~3200 K
light("fill", 'AREA', (-1.0, -0.6, 0.4), 3, (0.65, 0.75, 1.0), 1.0, tgt)
light("rim", 'AREA', (0.0, 1.0, 0.7), 12, (1.0, 0.8, 0.6), 0.6, tgt)
camera((0, -1.35, 0.13), (0, 0, 0.115), 70)
world((0.09, 0.09, 0.10, 1))
render("preview-bottles.png", PREVIEW, "bottles.blend", volumetrics=(1.1, 1.6))
