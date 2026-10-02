# Procedural assets (Blender)

All scripts run headless from the repo root with Blender 5.2: `blender -b --python tools/blender/<script>.py`
(1-3 s each). Every script writes into `tools/blender/out/` (gitignored): one FBX per tier, the raw preview render,
a `.blend` and a `<name>.log` with tri counts. The preview is also copied next to the scripts as `preview-*-vN.png`
(committed, 1280x720, Eevee).

| Script | Outputs in `out/` | Committed preview | Pivot | Anchors |
|---|---|---|---|---|
| `make_bottle.py` (v5) | `bottle_t1..3.fbx`, `bottles.blend`, `bottles.log` | `preview-bottles-v5.png` | base | `MistAnchor`, `attach` (neck) |
| `make_bowl.py` (v2) | `bowl_t1..3.fbx`, `bowls.blend`, `bowls.log` | `preview-bowls-v2.png` | base | `attach` (rim, +X) |
| `make_plants.py` (v5) | `plant_t1_s1/s2`, `plant_t1_s3_a/b/c`, `plant_t1_flat_a/b/c`, `plant_t2_a/b/c`, `plant_t2_picked`, `plant_t3_a/b/c`, `plant_t3_picked` (+ compatibility `plant_t1_s3`, `plant_t1`, `plant_t1_flat`, `plant_t2`, `plant_t3`), `plants.blend`, `plants.log` | `preview-plants-v5.png` | ground (t1: patch centre on the bark) | `PickAnchor`, `EmberAnchor` (t3) |
| `make_goggles.py` (v2) | `goggles_t1..3.fbx`, `goggles.blend`, `goggles.log` | `preview-goggles-v2.png` | head centre | `attach` (head centre) |

## Shared conventions (`common.py`)

- FBX: Y-up, 1 unit = 1 m, flat shading, meshes + empties only; the root object's transform is zeroed during the
  export, so the pivot is the object origin. Blender front (-Y) ends up as Unity +Z (check once after import).
- Anchor empties are created with `common.empty(name, loc, parent)`. In the `.blend` they carry a unique temporary
  name (`MistAnchor__bottle_t1`); `common.export_fbx` renames each to its exact name only while its own FBX is
  written and fails loudly on a clash, so no `.001` suffix can reach Unity.
- Materials are named exactly as the Unity `.mat` files. Table entries are `(colour RGBA, roughness, metallic,
  emission[, alpha])`. Bottles and bowls prefix theirs (`bottle_*`, `bowl_*`); plants use `huldra_*`, `baldr_*`,
  `helfern_*`; goggles use plain names (`bronze`, `silver`, ...).
- `common.Builder(mats, rng)` holds one bmesh with named material slots and the primitives (lathe with optional
  half-step ring twist for cut-glass facets, tube, ico, prism, ring, disc, box, ribbon, bipyramid, torus, raised
  `strokes` for runes). Preview helpers: `ground`, `ruler`, `light`, `camera`, `world`, `render`, `render_panels`
  (several camera views side by side, optionally in rows, in one PNG); `tri_count` and
  `write_log` for the log.

## Tri counts

| Tier | Bottle (incl. 80-tri mist mesh) | Bowl | Plant | Goggles |
|---|---|---|---|---|
| 1 | 370 | 297 | 1284 (stages 114 / 546 / 1284; variants see below) | 490 |
| 2 | 448 | 488 | 484 | 1112 |
| 3 | 560 | 488 | 1196 | 1216 |

# Veil-mead flasks (v5): finished meads

Each FBX holds the flask, a `mist_tN` inner mesh, an empty `MistAnchor` at the mist centre (for the Unity particle
system) and an empty `attach` at the neck (grip point). Principled Volume does not export to FBX; in Unity the mist
mesh needs its own material or particles. Labels face Blender -Y.

| Tier | Name | Height | Look |
|---|---|---|---|
| 1 | Faint Veil | 0.16 m | squat moss-green flask, tan cork, 2 twine rings |
| 2 | Deep Veil | 0.22 m | steel-blue, iron neck band, iron-capped cork, dark leather label band with a raised bone-white Hagalaz rune |
| 3 | Shadow Veil | 0.26 m | 8-panel violet cut-glass decanter (triangle facets on the lower bevel and shoulder) on a slim silver goblet foot, silver plaque with a dark Ansuz rune, silver neck band, stopper and dark crystal |

Knobs at the top of `make_bottle.py`:
1. `TIERS[...]`: `profile_points` (radius, height[, twist]) and `parts` per material in metres; `segs`, `rot`,
   `jitter`, `attach_z`, `label` (`band`: `cols`, `z0`, `z1`; `plaque`: `width`, `thick`; both: `rune`, `rune_size`,
   `rune_mat`); `SEED`.
2. Runes: `RUNES` (strokes in a -1..1 box), `RUNE_WIDTH`, `RUNE_HEIGHT`, `LABEL_OFFSET`.
3. Mist: `MIST_FILL`, `MIST_INSET`, `MIST_DENSITY`, `MIST_ANISOTROPY`, per-tier `mist_emit`.
4. Colours: per-tier `glass`, the `MATS` table, `GLASS_ALPHA`, `GLASS_ROUGH`, `BASE_DARKEN`.

# Mead bases (v2): unfermented

A shallow turned wooden bowl in the manner of the vanilla mead bases, 0.14 m across and 0.062 m to the rim, about
70 % full of a murky opaque brew in the tier hue (no mist, no glow). The brew surface shows 2-3 thin spiral swirl
ribbons in a lighter tint of the brew (like stirred cream), lifted 0.8 mm, and a faint concentric ripple ring. No
spoon, stick or floating bits. Outer faces alternate two wood shades per column (grain), the inside is darker
(stained). Pivot at the base centre, `attach` on the rim at +X.

| Tier | Look |
|---|---|
| 1 | rough 9-sided bowl with per-vertex unevenness, pale wood, moss-green brew, 2 swirls |
| 2 | smoother 12-sided bowl with a carved dark bead ring under the rim, steel-blue brew, 3 swirls |
| 3 | dark wood 12-sided bowl with a silver rim band, dark violet brew, 3 swirls |

Knobs at the top of `make_bowl.py`: `FILL`, `LIQUID_SINK`, `SWIRL_W` (start/end width), `SWIRL_H`, `SWIRL_SEGS`,
`SWIRL_TURN`, `RIPPLE_R`, `RIPPLE_W`, `RIPPLE_SEGS`, `SEED`, the `MATS` table (`bowl_swirl_tN`, `bowl_ripple_tN` are
the tints), and per tier `segs`, `rot`, `vjitter` (unevenness), `grain` (share of dark columns), `profile` with
`rim`/`inner` indices, `carve` (band range), `silver` (rim band profile), `swirls`, `attach_side`.

# Ingredient plants (v5)

Outputs (all in `out/`): lichen `plant_t1_s1.fbx`, `plant_t1_s2.fbx`, `plant_t1_s3_a/b/c.fbx`, `plant_t1_flat_a/b/c.fbx`;
Baldr's Tear `plant_t2_a/b/c.fbx`, `plant_t2_picked.fbx`; Helfern `plant_t3_a/b/c.fbx`, `plant_t3_picked.fbx`; plus the
compatibility files `plant_t1_s3.fbx`, `plant_t1.fbx`, `plant_t1_flat.fbx`, `plant_t2.fbx`, `plant_t3.fbx`, which are
variant a exported again under the earlier file and object names (meshes identical to v4). Also `preview-plants.png`,
`plants.blend`, `plants.log`. The preview is copied to `tools/blender/preview-plants-v5.png` (1280x1260, one row per
plant, rendered by `common.render_panels`): lichen s1, s2, s3a, s3b, s3c on five 0.5 m trunks (2 m ruler in 0.5 m
bands); Baldr's Tear a, b, c, picked; Helfern a, b, c, picked (0.5 m rulers in 0.1 m bands).
Each FBX is one mesh with named materials plus an empty `PickAnchor`; the fern variants a-c also have `EmberAnchor`
(the picked fern does not). Leaves and cards are single-sided except the lichen beards (two opposite faces); use a
double-sided (cull off) shader in Unity.

Variants come from the `VARIANTS` table: per plant, variant -> (seed offset added to that plant's seed, overrides).
Variant a is the reference model. Picked states use the base knobs and the plant's own seed.

| File | Look | Tris |
|---|---|---|
| `plant_t1_s1` | lichen sprout (variant a skeleton) | 114 |
| `plant_t1_s2` | lichen half-grown | 546 |
| `plant_t1_s3_a` / `plant_t1_flat_a` | full patch, scale 1.0, density 1.0 | 1284 |
| `plant_t1_s3_b` / `plant_t1_flat_b` | other runner layout (seed +101), scale 0.85, leaf density +15 % | 1082 |
| `plant_t1_s3_c` / `plant_t1_flat_c` | other runner layout (seed +202), scale 1.15, leaf density -15 % | 1270 |
| `plant_t2_a` | Baldr's Tear as before | 484 |
| `plant_t2_b` | 1.15x taller, wide-open bell, 2 berries, flatter leaves, wider lower mound | 464 |
| `plant_t2_c` | 0.85x, a second closed bud stem, 4 berries, steeper leaves, smaller higher mound | 624 |
| `plant_t2_picked` | snow mound, leaves and a 0.13 m cut stem stub; no blossom, buds or berries | 126 |
| `plant_t3_a` | Helfern as before | 1196 |
| `plant_t3_b` | wider and lower (1.2x spread, 0.9x height), 7 outer and 3 inner fronds, 6 embers, 7 basalt columns | 1292 |
| `plant_t3_c` | 1.15x, 5 outer fronds, larger and taller fiddleheads, 5 basalt columns | 1081 |
| `plant_t3_picked` | basalt base, root crown and roots, dim heart (`helfern_heart_dim`), 2 short young fronds | 334 |

Tier 1 convention (all stages and the flat variant): the pivot is the patch centre on the bark, +Y up, and the patch's
outward normal is Unity +Z (Blender -Y). The curved files fit a trunk of 0.5 m diameter whose axis is 0.25 m behind the
pivot (Unity -Z); `plant_t1_flat` is the full stage on a plane (plank, wall, cultivation). Everything sits 2.5 mm off
the surface. `PickAnchor` is at the patch centre, 1 cm out. Nothing else is on the trunk (no stubs, collar or stone).

Tier 1 growth: one skeleton of 10 main runners radiating from the centre (in growth order) with 2 branches each is
generated once. Stage s keeps the first `stage_runners[s]` runners with their branches, cut at `stage_reach[s]` of their
length; leaves at the growth front are smaller and beards appear a little behind it. Each stage therefore contains
the previous one and grows outward from the same centre, so swapping the model in place reads as growth. Leaf clusters
overlap into a continuous mat in the centre (up to 4 leaves per node plus a 14-leaf rosette at the germination point)
and thin out to single leaves on the runner tips, so the silhouette is irregular and the edges sparse: neighbouring
patches about 0.3 m apart overlap naturally.

| Stage | File | Runners | Reach | Tris |
|---|---|---|---|---|
| 1 sprout | `plant_t1_s1.fbx` | 2 | 38 % | 114 |
| 2 half-grown | `plant_t1_s2.fbx` | 7 | 68 % | 546 |
| 3 full (harvestable) | `plant_t1_s3.fbx` = `plant_t1.fbx`, `plant_t1_flat.fbx` | 10 | 100 % | 1284 |

| Tier | Name | Biome | Size | Look | Tris |
|---|---|---|---|---|---|
| 1 | Huldra's Hair (Huldrelokk) | Black Forest | 0.34 x 0.40 m patch (full stage) | rare dense creeping lichen on fir/pine bark like Ashvine on walls: thin runners radiating from a centre and branching, leaf clusters in two grey-green shades with paler tips forming a mat in the centre, many short double-sided hanging beard tufts, 6 faint cold tip glints; 3 growth stages | 114 / 546 / 1284 |
| 2 | Baldr's Tear (Baldrsgrat) | Mountains | 0.44 m | unchanged: nodding white snowdrop-like bell with a glowing blue cup and teardrop, closed bud, frosted leaves, 3 white mistletoe berries, snow mound | 484 |
| 3 | Hel's Ember Fern (Helfern) | Ashlands | 0.43 m (0.75 m across) | 6 large charcoal fronds arching out (80 to 12 deg) and 4 smaller steeper inner fronds, sawtooth leaflets shrinking toward the tips, glowing ember spore capsules under the outer leaflets, two red-brown fiddleheads curling in the centre, glowing heart in a charred root crown, gnarled roots crawling over hex basalt columns into lava-glow cracks, a few ember dots | 1196 |

Knobs at the top of `make_plants.py`:
1. `MATS`: colour, roughness, metallic, emission per material (`huldra_*`, `baldr_*`, `helfern_*`).
2. `T1`: `trunk_r`, `surface_off`, `pick_off`; skeleton `headings` (growth order), `reach_up`, `reach_side`,
   `reach_down`, `step`, `branches_per_runner`, `runner_w`, `ridge_until` (raised runners near the centre);
   `node_step`, `leaves_min`, `leaves_max`, `rosette`, `leaf_len`, `leaf_w`, `mature_dist`; beards `beard_zmax`,
   `beard_p`, `beard_strands`, `beard_len`, `beard_w`, `beard_delay`, `glints`; stages `stage_runners`, `stage_reach`.
3. `T2`: mound size, `stem_h`, `nod` (how far the head bends over), bell length/radius/petals, `bell_open`, bud stem,
   `bud_stems`, leaves, `leaf_rise`, berries, `cut_h` (picked stub), `scale` (horizontal, vertical).
4. `T3`: `columns`, `crack_r`, `crown_z`, `roots`, `root_len`, `heart_r`, `heart_z`, `ember_anchor_dz` (EmberAnchor
   height above the heart), `stem_r`, `leaflet_droop`, `teeth_min` (no saw teeth on shorter leaflets), `outer` and
   `inner` frond layers (`n`, `phase`, `len`, `angle` start/end elevation, `segs`, `leaflets`, `leaflet_len`, `fwd`,
   `start_r`), `spore_leaflets`, `spore_r`, `crozier_h`, `crozier_az`, `crozier_r`, `crozier_turns`, `crozier_w`,
   `embers`, `young` (the two fronds of the picked state), `scale` (horizontal, vertical). `SEED` for all.
5. `VARIANTS`: seed offsets and overrides per variant (lichen: `scale`, `density`; others: any knob, nested dicts merge).
Preview-only placement (`X1`, `DX1`, `Y2`, `DX2`, `Y3`, `DX3`, `PATCH_Z`, `on_trunk`), cameras and lights near the end.

# Veil goggles (v2)

Outputs `goggles_t1/t2/t3.fbx`, `preview-goggles.png`, `goggles.blend`; the preview is copied to
`tools/blender/preview-goggles-v2.png` (grey reference heads 0.22 m wide, turned 30 degrees so the strap shows).
Real scale for a ~1.8 m character. Pivot = head centre (parent to the head bone); each FBX has an `attach` empty there. In Blender the wearer faces -Y and
the right eye is at -X; check the facing once after the Unity import (rotate the attach point 180 degrees if it is
backwards). Eyes 0.065 m apart, lenses about 0.05 m, strap ellipse just outside the 0.22 x 0.25 m head.

| Tier | Name | Biome | Look | Tris |
|---|---|---|---|---|
| 1 | Watchman's Glass | Black Forest | crude jittered bronze rims, resin-amber lenses (alpha), wood temple blocks, rough leather strap, twine wraps | 490 |
| 2 | Mimir's Glass | Mountains | polished silver rims with bezels, pale crystal lenses (alpha), silver temple arms with frost-crystal shards, dark strap with wolf-pelt trim | 1112 |
| 3 | Allfather's Eye | Ashlands | black flametal half-mask with brow V and cheek guards, two obsidian lenses in flametal bezels with ember rims, small crest plate in the brow V with an ember Ansuz mark, black leather strap starting under the mask edges with 3 riveted flametal plates per side | 1216 |

Knobs at the top of `make_goggles.py`:
1. Fit: `HEAD`, `EYE_Z`, `EYE_X`, `LENS_Y`, `STRAP_A`, `STRAP_B`.
2. `MATS`: colour, roughness, metallic, emission, alpha (`bronze`, `amber_lens`, `silver`, `crystal_lens`, `flametal`, `obsidian_lens`, `ember_rim`, `rune_inlay` ...).
3. `T1`: rim segments/radii/depth/`jitter`, strap size. `T2`: rims, `bezel_r`, strap, `fur_tufts`, `fur_len`.
   `T3`: mask ellipse, `mask_span` (degrees), `mask_z`, thickness, `lens_r`, strap, `plates` (degrees back from each
   mask edge), `plate_len`, `rivets`, `rune_z`, `rune_h`, `crest_w`, `crest_h`.
Preview-only: `HEAD_Z`, `SPACING`, `PREVIEW_TURN`, camera and lights near the end.

# Known gaps

Flat colours only (no textures yet), emission does not bloom in Eevee 5.x, transparent glass and lens materials
need a transparent shader in Unity, the bottle mist is a preview-only volume.
