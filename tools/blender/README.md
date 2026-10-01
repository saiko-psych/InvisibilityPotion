# Procedural veil-mead flasks spike (v4)

Run: `blender -b --python make_bottle.py` (about 3 s warm, about 11 s first run). Outputs `bottle_t1/t2/t3.fbx`
(Y-up, 1 unit = 1 m, pivot at base), `preview.png` (1280x720, Eevee with volumetrics), `bottles.blend`.
Each FBX holds the flask, a `mist_tN` inner mesh and an empty `MistAnchor` at the mist centre (for a Unity particle system).
Principled Volume does not export to FBX; in Unity the mist mesh needs its own material or particles.

| Tier | Name | Height | Look | Tris (incl. mist mesh) |
|---|---|---|---|---|
| 1 | Faint Veil | 0.16 m | squat moss-green flask, tan cork, 2 twine rings | 370 |
| 2 | Deep Veil | 0.22 m | steel-blue, iron neck band + rune ring, iron-capped cork | 420 |
| 3 | Shadow Veil | 0.26 m | 8-sided violet decanter, silver cup + 2 bands + stopper + dark crystal | 472 |

Knobs at the top of `make_bottle.py`:
1. `TIERS[...]["profile_points"]` and `["parts"]` (cork, bands, stopper...): (radius, height) lists in metres; `segs`, `jitter`, `SEED`.
2. Mist: `MIST_FILL`, `MIST_INSET`, `MIST_DENSITY`, `MIST_ANISOTROPY`, per-tier `mist_emit`.
3. Colours: per-tier `glass`, the `MATS` table (cork, twine, iron, silver, crystal), `GLASS_ALPHA`, `GLASS_ROUGH`, `BASE_DARKEN`.

# Ingredient plants (v1)

Run from the repo root: `blender -b --python tools/blender/make_plants.py` (about 1 s). Outputs in `tools/blender/out/`
(gitignored): `plant_t1/t2/t3.fbx` (Y-up, 1 unit = 1 m, pivot at ground level), `preview-plants.png`, `plants.blend`.
The preview is copied to `tools/blender/preview-plants-v1.png` (1280x720, Eevee, 0.5 m ruler in 0.1 m bands).
Each FBX is one mesh with named materials plus an empty `PickAnchor` (where a hand grabs it); tier 3 also has
`EmberAnchor` (0.38 m above the centre) for a spark particle system. Stones and the snow mound sink about 1 cm below 0.
Leaf cards and strands are single-sided quads: they need a double-sided (cull off) shader in Unity.

| Tier | Name | Biome | Height | Look | Tris |
|---|---|---|---|---|---|
| 1 | Huldra's Hair (Huldrelokk) | Black Forest | 0.33 m | pale grey-green lichen strands hanging from a dead branch over a mossy stone, cold glint at the tips | 464 |
| 2 | Baldr's Tear (Baldrsgrat) | Mountains | 0.44 m | nodding white snowdrop-like bell with a glowing blue cup and teardrop, closed bud, frosted leaves, 3 white mistletoe berries, snow mound | 484 |
| 3 | Hel's Ember Fern (Helfern) | Ashlands | 0.49 m | 7 charcoal fronds with sawtooth leaflets, ember spore patches on the leaflet undersides, ember dots, hex basalt columns over glowing cracks | 705 |

Knobs at the top of `make_plants.py`:
1. `MATS`: colour, roughness, metallic, emission per material (`huldra_*`, `baldr_*`, `helfern_*`).
2. `T1`: stone size, branch/arm control points and radii, strand count/length/radius/segments, `glint_segs`, leaf cards.
3. `T2`: mound size, `stem_h`, `nod` (how far the head bends over), bell length/radius/petals, bud stem, leaves, berries.
4. `T3`: basalt `columns`, `fronds`, `frond_len`, `frond_angle` (start/end elevation), `leaflets` per side,
   `leaflet_len`, `leaflet_fwd`, `leaflet_droop`, `spore_scale`, `embers`, `ember_anchor_h`. `SEED` for all.
Preview-only placement: `POS`, `ROT` (turn the flower toward the camera), camera and lights near the end.

# Veil goggles (v1)

Run from the repo root: `blender -b --python tools/blender/make_goggles.py` (about 1 s). Outputs in `tools/blender/out/`
(gitignored): `goggles_t1/t2/t3.fbx`, `preview-goggles.png`, `goggles.blend`; the preview is copied to
`tools/blender/preview-goggles-v1.png` (grey reference heads 0.22 m wide, turned 30 degrees so the strap shows).
Real scale for a ~1.8 m character. Pivot = head centre (parent to the head bone). In Blender the wearer faces -Y and
the right eye is at -X; check the facing once after the Unity import (rotate the attach point 180 degrees if it is
backwards). Eyes 0.065 m apart, lenses about 0.05 m, strap ellipse just outside the 0.22 x 0.25 m head.

| Tier | Name | Biome | Look | Tris |
|---|---|---|---|---|
| 1 | Watchman's Glass | Black Forest | crude jittered bronze rims, resin-amber lenses (alpha), wood temple blocks, rough leather strap, twine wraps | 490 |
| 2 | Mimir's Glass | Mountains | polished silver rims with bezels, pale crystal lenses (alpha), silver temple arms with frost-crystal shards, dark strap with wolf-pelt trim | 1112 |
| 3 | Allfather's Eye | Ashlands | black flametal half-mask with brow V and cheek guards, obsidian lens with ember rim over the right eye, sealed left socket with an Ansuz rune inlay, black strap on chain links | 1124 |

Knobs at the top of `make_goggles.py`:
1. Fit: `HEAD`, `EYE_Z`, `EYE_X`, `LENS_Y`, `STRAP_A`, `STRAP_B`.
2. `MATS`: colour, roughness, metallic, emission, alpha (`bronze`, `amber_lens`, `silver`, `crystal_lens`, `flametal`, `obsidian_lens`, `ember_rim`, `rune_inlay` ...).
3. `T1`: rim segments/radii/depth/`jitter`, strap size. `T2`: rims, `bezel_r`, strap, `fur_tufts`, `fur_len`.
   `T3`: mask ellipse, `mask_span` (degrees), `mask_z`, thickness, `lens_r`, `socket_r`, strap, chain `links`.
Preview-only: `HEAD_Z`, `SPACING`, `PREVIEW_TURN`, camera and lights near the end.

Known gaps (both scripts): flat colours only (no textures yet), emission does not bloom in Eevee 5.x, transparent
lens materials need a transparent shader in Unity, anchors are only in the plant FBX files.
