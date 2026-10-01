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
