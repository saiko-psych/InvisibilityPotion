# Procedural assets (Blender)

All scripts run headless from the repo root with Blender 5.2: `blender -b --python tools/blender/<script>.py`
(1-3 s each). Every script writes into `tools/blender/out/` (gitignored): one FBX per tier, the raw preview render,
a `.blend` and a `<name>.log` with tri counts. The preview is also copied next to the scripts as `preview-*-vN.png`
(committed, 1280x720, Eevee).

| Script | Outputs in `out/` | Committed preview | Pivot | Anchors |
|---|---|---|---|---|
| `make_bottle.py` (v5) | `bottle_t1..3.fbx`, `bottles.blend`, `bottles.log` | `preview-bottles-v5.png` | base | `MistAnchor`, `attach` (neck) |
| `make_bowl.py` (v1) | `bowl_t1..3.fbx`, `bowls.blend`, `bowls.log` | `preview-bowls-v1.png` | base | `attach` (rim, +X) |
| `make_plants.py` (v2) | `plant_t1..3.fbx`, `plants.blend`, `plants.log` | `preview-plants-v2.png` | ground | `PickAnchor`, `EmberAnchor` (t3) |
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
  `strokes` for runes). Preview helpers: `ground`, `ruler`, `light`, `camera`, `world`, `render`; `tri_count` and
  `write_log` for the log.

## Tri counts

| Tier | Bottle (incl. 80-tri mist mesh) | Bowl | Plant | Goggles |
|---|---|---|---|---|
| 1 | 370 | 277 | 654 | 490 |
| 2 | 448 | 451 | 484 | 1112 |
| 3 | 560 | 447 | 592 | 1216 |

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

# Mead bases (v1): unfermented

A shallow turned wooden bowl in the manner of the vanilla mead bases, 0.14 m across and 0.062 m to the rim, about
70 % full of a murky opaque brew in the tier hue (no mist, no glow), a few herb flecks floating on it and a
stirring stick or spoon resting on the rim (back left, it reaches 0.075-0.085 m). Outer faces alternate two wood
shades per column (grain), the inside is darker (stained). Pivot at the base centre, `attach` on the rim at +X.

| Tier | Look |
|---|---|
| 1 | rough 9-sided bowl with per-vertex unevenness, pale wood, moss-green brew, pale lichen flecks, crude stirring stick with a twig stub |
| 2 | smoother 12-sided bowl with a carved dark bead ring under the rim, steel-blue brew, white petal flecks, wooden spoon |
| 3 | dark wood 12-sided bowl with a silver rim band, dark violet brew, ember-ash flecks (not emissive), dark spoon |

Knobs at the top of `make_bowl.py`: `FILL`, `LIQUID_SINK`, `SEED`, the `MATS` table, and per tier `segs`, `rot`,
`vjitter` (unevenness), `grain` (share of dark columns), `profile` with `rim`/`inner` indices, `carve` (band range),
`silver` (rim band profile), `tool` (`stick` or `spoon`), `specks`, `speck_size`, `attach_side`.

# Ingredient plants (v2)

Outputs `plant_t1/t2/t3.fbx` (pivot at ground level), `preview-plants.png`, `plants.blend`.
The preview is copied to `tools/blender/preview-plants-v2.png` (1280x720, Eevee, 0.5 m ruler in 0.1 m bands).
Each FBX is one mesh with named materials plus an empty `PickAnchor` (where a hand grabs it); tier 3 also has
`EmberAnchor` (0.40 m above the centre) for a spark particle system. Stones and the snow mound sink about 1 cm below 0.
Leaf cards and strands are single-sided quads: they need a double-sided (cull off) shader in Unity.

| Tier | Name | Biome | Height | Look | Tris |
|---|---|---|---|---|---|
| 1 | Huldra's Hair (Huldrelokk) | Black Forest | 0.31 m | dense lichen beard: 38 flat ribbon clumps (half of them crossed pairs) wrapping over a stout dead branch, strips spilling over a mossy stone, moss tufts, 4 faint tip glints | 654 |
| 2 | Baldr's Tear (Baldrsgrat) | Mountains | 0.44 m | nodding white snowdrop-like bell with a glowing blue cup and teardrop, closed bud, frosted leaves, 3 white mistletoe berries, snow mound | 484 |
| 3 | Hel's Ember Fern (Helfern) | Ashlands | 0.46 m (0.71 m across) | 6 charcoal fronds rising steeply (76-86 deg) and arching outwards to 15 deg at the tips, none upright, sawtooth leaflets, small ember spore patches on the outer leaflet undersides, ember dots, hex basalt columns over glowing cracks | 592 |

Knobs at the top of `make_plants.py`:
1. `MATS`: colour, roughness, metallic, emission per material (`huldra_*`, `baldr_*`, `helfern_*`).
2. `T1`: stone size, branch/arm control points and radii, `strands`, `strand_len`, `strand_w`, `strand_segs`, `drapes`, `drape_w`, `glints`, `tufts`, `tuft_len`.
3. `T2`: mound size, `stem_h`, `nod` (how far the head bends over), bell length/radius/petals, bud stem, leaves, berries.
4. `T3`: basalt `columns`, `fronds`, `frond_len`, `frond_angle` (start/end elevation), `leaflets` per side,
   `leaflet_len`, `leaflet_fwd`, `leaflet_droop`, `spore_scale`, `spore_from` (first leaflet with spores), `embers`, `ember_anchor_h`. `SEED` for all.
Preview-only placement: `POS`, `ROT` (turn the flower toward the camera), camera and lights near the end.

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
