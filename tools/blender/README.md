# Procedural assets (Blender)

All scripts run headless from the repo root with Blender 5.2: `blender -b --python tools/blender/<script>.py`
(1-3 s each). Every script writes into `tools/blender/out/` (gitignored): one FBX per tier, the raw preview render,
a `.blend` and a `<name>.log` with tri counts. The preview is also copied next to the scripts as `preview-*-vN.png`
(committed, 1280x720, Eevee).

| Script | Outputs in `out/` | Committed preview | Pivot | Anchors |
|---|---|---|---|---|
| `make_bottle.py` (v5) | `bottle_t1..3.fbx`, `bottles.blend`, `bottles.log` | `preview-bottles-v5.png` | base | `MistAnchor`, `attach` (neck) |
| `make_bowl.py` (v2) | `bowl_t1..3.fbx`, `bowls.blend`, `bowls.log` | `preview-bowls-v2.png` | base | `attach` (rim, +X) |
| `make_plants.py` (v6) | `plant_t1_s1/s2`, `plant_t1_s3_a/b/c`, `plant_t1_flat_a/b/c`, `plant_t2_a..e`, `plant_t2_picked`, `plant_t3_a..e`, `plant_t3_picked` (+ compatibility `plant_t1_s3`, `plant_t1`, `plant_t1_flat`, `plant_t2`, `plant_t3`), `plants.blend`, `plants.log` | `preview-plants-v6.png` | ground (t1: patch centre on the bark) | `PickAnchor`, `EmberAnchor` (t3) |
| `make_ingredients.py` (v1) | `ingredient_t1..3.fbx`, `ingredients.blend`, `ingredients.log` | `preview-ingredients-v1.png` | base centre (lying as dropped) | `attach` (bounding-box centre) |
| `make_goggles.py` (v4, round L fit) | `goggles_t1..3.fbx`, `goggles.blend`, `goggles.log` | `preview-goggles-v4.png` | helmet joint `Helmet_attach` | `attach` (joint origin) |

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
| 1 | 370 | 297 | 1284 (stages 114 / 546 / 1284; variants see below) | 684 |
| 2 | 448 | 488 | 484 (variants 316-624, see below) | 1346 |
| 3 | 560 | 488 | 1196 (variants 567-1292, see below) | 1216 |

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

# Ingredient plants (v6)

Outputs (all in `out/`): lichen `plant_t1_s1.fbx`, `plant_t1_s2.fbx`, `plant_t1_s3_a/b/c.fbx`, `plant_t1_flat_a/b/c.fbx`;
Baldr's Tear `plant_t2_a..e.fbx`, `plant_t2_picked.fbx`; Helfern `plant_t3_a..e.fbx`, `plant_t3_picked.fbx`; plus the
compatibility files `plant_t1_s3.fbx`, `plant_t1.fbx`, `plant_t1_flat.fbx`, `plant_t2.fbx`, `plant_t3.fbx`, which are
variant a exported again under the earlier file and object names (`plant_t2` follows the v6 blossom angle of `plant_t2_a`). Also `preview-plants.png`,
`plants.blend`, `plants.log`. The preview is copied to `tools/blender/preview-plants-v6.png` (1280x1340, one row per
plant, rendered by `common.render_panels`): lichen s1, s2, s3a, s3b, s3c on five 0.5 m trunks (2 m ruler in 0.5 m
bands); Baldr's Tear a, b, c, d, e, picked (turned so the blossoms face 30 degrees off the view axis); Helfern a, b, c, d,
e, picked (0.5 m rulers in 0.1 m bands).
Each FBX is one mesh with named materials plus an empty `PickAnchor`; the fern variants a-e also have `EmberAnchor`
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
| `plant_t2_a` | Baldr's Tear, nodding blossom (60 degrees below the horizontal) | 484 |
| `plant_t2_b` | 1.15x taller, wide-open half-open bell (30 degrees), less arched stem, 2 berries, flatter leaves, wider lower mound | 464 |
| `plant_t2_c` | 0.85x, nearly upright blossom (10 degrees) on an almost straight stem, a second closed bud stem, 4 berries, steeper leaves, smaller higher mound | 624 |
| `plant_t2_d` | 1.05x, two open blossoms (45 and 20 degrees; the second on a shorter stem turned round), no bud stems, 6 leaves | 563 |
| `plant_t2_e` | 0.9x, only a closed bud (55 degrees) with the glowing tear on a thread, no bud stems, 2 berries, higher mound | 316 |
| `plant_t2_picked` | snow mound, leaves and a 0.13 m cut stem stub; no blossom, buds or berries | 126 |
| `plant_t3_a` | Helfern as before | 1196 |
| `plant_t3_b` | wider and lower (1.2x spread, 0.9x height), 7 outer and 3 inner fronds, 6 embers, 7 basalt columns | 1292 |
| `plant_t3_c` | 1.15x, 5 outer fronds, larger and taller fiddleheads, 5 basalt columns | 1081 |
| `plant_t3_d` | young: 0.6x, 3 steeper fronds, no inner layer, no spores or embers, cold heart (`helfern_heart_cold`, no emission), 5 columns | 567 |
| `plant_t3_e` | old: 1.1x spread, 8 long drooping fronds with ash-grey charred tips (`helfern_frond_char`, last 40 % of each frond), no inner layer or fiddleheads, 7 embers, larger strong heart (`helfern_heart_strong`, emission 5), 7 columns | 1144 |
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
3. `T2`: mound size, `stem_h`, `nod` (how far the stem arches over), `bell_droop` (blossom angle: degrees the mouth
   axis points below the horizontal), `blossoms` (1 or 2; the second uses `bell_droop2`, `blossom2_h`), `bud_only`
   (closed bud with the tear instead of an open bell), bell length/radius/petals, `bell_open`, bud stem, `bud_stems`, leaves, `leaf_rise`, berries, `cut_h` (picked stub), `scale` (horizontal, vertical).
4. `T3`: `columns`, `crack_r`, `crown_z`, `roots`, `root_len`, `heart_r`, `heart_z`, `ember_anchor_dz` (EmberAnchor
   height above the heart), `stem_r`, `leaflet_droop`, `teeth_min` (no saw teeth on shorter leaflets), `outer` and
   `inner` frond layers (`n`, `phase`, `len`, `angle` start/end elevation, `segs`, `leaflets`, `leaflet_len`, `fwd`,
   `start_r`), `spore_leaflets`, `spore_r`, `crozier_h`, `crozier_az`, `crozier_r`, `crozier_turns`, `crozier_w`,
   `embers`, `heart_mat` (heart material), `char_from` (charred leaflets from this fraction of the frond on, `None` =
   none), `young` (the two fronds of the picked state), `scale` (horizontal, vertical). `SEED` for all.
5. `VARIANTS`: seed offsets and overrides per variant (lichen: `scale`, `density`; others: any knob, nested dicts merge).
Preview-only placement (`X1`, `DX1`, `Y2`, `DX2`, `Y3`, `DX3`, `PATCH_Z`, `on_trunk`), cameras and lights near the end.

# Ingredient items (v1): harvested pickups

What the player carries after harvesting a plant. Each item lies on the ground as dropped: pivot at the base centre
(lowest point at 0), `attach` empty at the bounding-box centre. Materials are prefixed `ingr_`. Strands, petals and
leaflets are double-sided (two opposite faces, kept out of the normal recalculation via `Builder.keep`); closed parts
are normal single-sided meshes. The preview has a 0.1 m ruler in 0.02 m bands.

| File | Name | Size | Look | Tris |
|---|---|---|---|---|
| `ingredient_t1.fbx` | Huldra's Hair tuft | 0.10 x 0.07 x 0.02 m | 14 grey-green lichen strands in two shades fanning out from two twine wraps, pale tips, cut tops behind the knot, a loose twine end | 345 |
| `ingredient_t2.fbx` | Baldr's Tear | 0.11 x 0.04 x 0.05 m | one white bell blossom lying on its side, faint blue glowing cup and teardrop, short stem, two white berries on a twig | 258 |
| `ingredient_t3.fbx` | Hel's Ember spore | 0.09 x 0.08 x 0.02 m | black frond fragment curled into a flat spiral with sawtooth leaflets around three glowing ember spore capsules | 173 |

Knobs at the top of `make_ingredients.py`: `SEED`, `MATS`, `T1` (`strands`, `length`, `width`, `knot_x`, `tail`),
`T2` (`bell_len`, `bell_r`, `petals`, `stem_len`, `berry_r`), `T3` (`curl_r`, `turns`, `leaflets`, `leaflet_len`,
`leaflet_base`, `capsule_r`). Preview placement, camera and lights at the end.

# Veil goggles (v4): fitted to the vanilla head

Outputs `goggles_t1/t2/t3.fbx`, `preview-goggles.png`, `goggles.blend`, `goggles.log`; the preview is copied to
`tools/blender/preview-goggles-v4.png`: the three goggles on the exported player head (grey), front row and 3/4 row.
Pivot = the vanilla helmet joint `Helmet_attach` (under the Head bone), so the vanilla attach places them on the head;
each FBX has an `attach` empty at that origin. Blender frame: the wearer faces -Y, right eye at -X, Z up.

## Head fit

1. In game, `ip_exportmesh head` writes `head_body.obj` (the local player body, baked) and `head_HelmetLeather.obj`
   (the vanilla leather helmet's `attach` mesh in joint space) to `BepInEx/export/`; copy them to `tools/blender/ref/`
   (gitignored: vanilla-derived, used only to measure and for the preview, never exported or committed).
2. Finding: the helmet OBJ is in joint space (metres), but the body OBJ came out in centimetres in the body's own
   space (y 0..1.79 m standing, not around the joint). `REF_ALIGN` maps it into joint space: scale 0.0095 (cm x the
   reported lossy scale 0.95) and offset (-0.129, -1.603, -0.115) m in OBJ axes, chosen so the eyes sit behind the
   helmet's eye holes and the face just behind its face plate (checked in front and side renders; about 1 cm
   uncertainty). The exporter should be fixed to bake into joint space; then set `REF_ALIGN` to scale 1, offset 0.
3. With `ref/` present the script measures the aligned head and rewrites `head_fit.json` (committed): a polar radius
   table around a vertical head axis (31 heights x 72 angles, 5 mm x 5 deg, fine enough for the ears), a face depth grid (front-most face y over x/z) and the
   helmet eye-hole centre. Without `ref/` it reads `head_fit.json`, so `make models` works anywhere (the preview then
   uses a coarse proxy head built from the table).
4. Placement (round L, after the in-game check of v4: T1/T2 sat slightly low, T3 a few mm off the cheeks):
   - T1/T2 sit `RAISE_12` (8 mm) above the measured eye centre (so at z +0.010, next to the helmet eye holes at
     +0.014). Each rim is tilted to follow the eye socket: its axis is the outward normal of a plane fitted to the
     face inside the rim disc, clamped to `TILT_MAX` (15 deg; the fit asks for about 30 deg outward, which looked
     insect-like). The rim centre stays on the eye's line of sight and moves back until the rim's back face is
     `RIM_GAP` (4 mm) in front of the face everywhere inside the tilted disc. Result: rims 4.6 mm closer than v4.
     Temple blocks, arms, shards and strap ends follow the tilted rim frame; the bridges rest on the nose bridge.
   - Straps run along the head surface `STRAP_OFF` (2.5 mm) off it (radius smoothed outwards over 8 deg / the strap's
     half width 1.2 cm, so they ride over the ears), from the temples (`STRAP_Z[0]` = 4 mm above the eyes) round the
     back above the ears (`STRAP_Z[1]` = 3 cm above the eyes).
   - T3: the half-mask's inner face is `MASK_CLEAR` (3 mm) off the head. Wide smoothing (15 deg / 1.8 cm, max of the
     neighbours) only across the eye sockets, none at the temples, cheeks and forehead; the brow bulge thickens the
     front only. The lens bezels, ember rims and lenses are laid out on the curved mask front by arc length round the
     head, so they bend with the mask at the temples instead of floating there.
   - Every run logs the clearance to the reference head per material (`CLEAR ...`: radial gap to an exact ray cast,
     negative = inside the head); `GOGGLES_FIT_DEBUG=1` adds the T3 mask clearance per row.

Measured (joint space, Blender axes, metres; `goggles.log` prints them on every run):

| Value | Measured |
|---|---|
| eye centres | x = +-0.040, z = +0.002 (eyes 0.080 apart; helmet eye holes at x +-0.040, z +0.014) |
| eye surface | y = -0.077 |
| brow ridge front | y = -0.091 (z +0.012) |
| nose bridge | x 0, y = -0.098, z +0.012 |
| head half-width at the temples | 0.071 (head axis at y -0.005) |
| head radius at the back, strap height | 0.100; front at the brow 0.092 |
| rim centres (round L) | T1 (+-0.040, -0.1046, +0.010), T2 (+-0.040, -0.1028, +0.010), tilted 15 deg outward (v4: y -0.109 / -0.107, untilted, z +0.002) |
| clearance to the head | T1 min 2.5 mm, T2 min 0.7 mm, T3 min 2.3 mm (no vertex inside); T3 mask back 2.5 / 3.2 / 14.6 mm (min / median / max, the max is the bridge over the eye socket by the nose) |

| Tier | Name | Biome | Look | Tris |
|---|---|---|---|---|
| 1 | Watchman's Glass | Black Forest | thick jittered bronze rims, 0.057 m saturated resin-amber lenses (alpha 0.6) domed 3.5 mm proud of the rims, 3 riveted bronze clamps per rim, riveted bronze bridge bar with a twine wrap, wood temple blocks, rough leather strap with a bronze buckle and twine wraps | 684 |
| 2 | Mimir's Glass | Mountains | polished silver rims with raised bezels, 0.057 m saturated ice-blue crystal lenses (alpha 0.6) domed 3.5 mm proud, 3 silver clamps per rim with frost-crystal rivets, high silver bridge arch with a small frost crystal, silver temple arms with frost-crystal shards, dark strap with a silver buckle and wolf-pelt trim | 1346 |
| 3 | Allfather's Eye | Ashlands | black flametal half-mask with brow V and cheek guards, two obsidian lenses in flametal bezels with ember rims, small crest plate in the brow V with an ember Ansuz mark, black leather strap starting under the mask edges with 3 riveted flametal plates per side | 1216 |

Knobs at the top of `make_goggles.py`:
1. Fit: `REF_ALIGN`, `EYE`, `RAISE_12`, `RIM_GAP`, `TILT_MAX`, `STRAP_OFF`, `STRAP_Z`, `MASK_CLEAR` (and `head_fit.json`).
2. `MATS`: colour, roughness, metallic, emission, alpha (`bronze`, `amber_lens`, `silver`, `crystal_lens`, `flametal`, `obsidian_lens`, `ember_rim`, `rune_inlay` ...).
3. `T1`: rim segments/radii/depth/`jitter`, strap size, `lens_r`, `lens_dome`, `lens_back`, `clamps` (angles), `buckle_at` (strap point). `T2`: the same plus `bezel_r`, `fur_tufts`, `fur_len`.
   `T3`: `mask_span` (degrees round the head), `mask_z` (relative to the eyes), thickness, `lens_r`, strap, `plates` (degrees back from each
   mask edge), `plate_len`, `rivets`, `rune_z`, `rune_h`, `crest_w`, `crest_h`.
Preview-only: cameras and lights near the end (rows: front, 3/4, side).

# Known gaps

Flat colours only (no textures yet), emission does not bloom in Eevee 5.x, transparent glass and lens materials
need a transparent shader in Unity, the bottle mist is a preview-only volume.
