# Procedural mead flask spike (v3)

Run: `blender -b --python make_bottle.py` (about 2 s). Outputs `bottle_t1/t2/t3.fbx` (Y-up, 1 unit = 1 m,
pivot at base; each FBX holds the flask plus its `liquid_*` child), `preview.png` (1280x720, Eevee) and
`bottles.blend` (open in the Blender GUI).

Flask: 0.22 m tall, 0.13 m belly at 35% height, neck 25% and 0.035 m wide, lip 0.045 m, cork +1 cm, 3 rope rings.

Triangle counts (flask + liquid): t1 = 390, t2 = 440 (with wax seal), t3 = 440 (with wax seal).

Knobs at the top of `make_bottle.py`:
1. `profile_points` (plus `cork_profile`, `liquid_profile`, `rope_heights`): (radius, height) in metres; `SEGMENTS`, `JITTER`, `SEED` for faceting and hand-blown irregularity.
2. `GLASS_ALPHA` / `GLASS_ROUGH` / `LIQUID_BRIGHTNESS` / `LIQUID_ALPHA` / `BASE_DARKEN` (see-through, matte sheen, liquid tint, dark glass bottom).
3. `TIERS` (glass colours), `CORK_COLOR`, `ROPE_COLOR`, `WAX_COLOR`; `BEVEL_WIDTH` (costs tris).
