# Vanilla concealment effects to borrow (Valheim 1.0.16)

Research for the veil visuals (`InvisibilityPotion/Visuals/FogVeil.cs`). Sources:
- decompile: `tools/decompiled/assembly_valheim/` (file:line)
- bundles: read with UnityPy from `valheim_Data/StreamingAssets/SoftRef/Bundles/c4210710` (almost all characters and VFX) and `d59cfac` (Ashlands heat haze)
- Jötunn shader and prefab lists

The bundle tools resolved each material's shader by path ID. They resolved the core shaders below without any ambiguity. Nothing here has been tested in game. **(unverified)** marks guesses about how something looks.

## 1. Portal teleport: no dissolve exists

- `Player.TeleportTo` (Player.cs:5888) and `UpdateTeleport` (Player.cs:5915) only start a timer, wait 2 s, then move the transform. No renderer, material or effect is touched.
- The teleporting player sees the black loading screen: `Hud.UpdateBlackScreen` (Hud.cs:571) uses `IsTeleporting()`, and `ShowTeleportAnimation` (Player.cs:5972) picks the teleport spinner. Other players just see the body jump.
- Vanilla's only "hide a character" trick is `Character.SetVisible` (Character.cs:4299). It moves `LODGroup.localReferencePoint` to (999999,…) so the LOD culls everything. `Player.cs:847` uses it when the camera is within 2 m. `LodFadeInOut.cs` uses the same trick. **Risk:** vanilla toggles it every frame (Character.cs:823, Player.cs:847), so it needs a Harmony postfix to stick.
- `Custom/Player` properties: `_Cutoff`, `_MainTex`, `_SkinBumpMap`, `_SkinColor`, `_ChestTex`, `_ChestBumpMap`, `_ChestMetal`, `_LegsTex`, `_LegsBumpMap`, `_LegsMetal`, `_BumpScale`, `_Glossiness`, `_MetalGlossiness`, `_SnowCover`. All passes are opaque. There is no `_Color`, `_EmissionColor`, alpha or dissolve, so an alpha fade on the vanilla body material is impossible.
- Generic helpers that exist: `MaterialFader` (MaterialFader.cs) animates any float, color or vector property through a MaterialPropertyBlock over time. `EffectFade` fades particles, lights and audio.
- **Pitfall:** `VisEquipment` writes `_SnowCover` through `MaterialMan.instance.SetValue` (VisEquipment.cs:237, 1447). `MaterialMan` calls `SetPropertyBlock` on the player's renderers (MaterialMan.cs:88), which overwrites any property block we set ourselves. Go through `MaterialMan.instance.SetValue/ResetValue` or use material instances.

## 2. Ghostly creatures and their materials

| Prefab | Body material / shader | Why it reads as ghostly |
|---|---|---|
| `Ghost` | `Ghost_mat` / `Custom/Creature` (opaque, `_Cutoff` 0.63, `_EmissionColor` 2.83 white, has `_EmissionMap`) | Cutout plus strong emission, and **`black_smoke` particles emitted from the body mesh** (see 4). This is our current "cursed" look. |
| `Wraith` | `wraith` / `Custom/Creature` (opaque metal) | Only the `wraith_smoke` particles (`Custom/LitParticles`) on bones. The body itself is not translucent. |
| `Ghost_Void` | `Ghost_void_mat` (black, Custom/Creature) + skeleton with `ghost` / **Standard, Transparent** (`_Mode` 2, `_Color` (0,0,0,1), `_EmissionColor` (0.1,0.15,0.26), ZWrite 0) | Truly alpha-blended. Two body-mesh smoke emitters using `ghost_smoke`. |
| **`ShadowPerson`** (humanoid, player skeleton, `$enemy_shadowperson`) | `ShadowPerson 1` / **Standard, Transparent premultiplied** (`_Mode` 3, `_Color` (0,0,0,**0.34**), queue 3000) | A see-through dark silhouette. Every `SP_*` armor and weapon variant reuses the same material. |
| **Spiritcaller summons** (`Wolf_/Boar_/Moose_/Bjorn_spiritcaller`), **`FallenWarrior`** (+ `FW_*` armor variants) | `*_mat_spiritcaller`, `FallenWarrior` / **`Custom/Fallen Warrior`** | Transparent shader made for humanoids (see below). |

**`Custom/Fallen Warrior`** has a depth pre-pass that writes depth only (ColorMask 0), then a color pass with `Blend DstColor One`, queue Transparent. That makes it a self-occluding, light-brightening glow shell. Its properties are `_TintColor` (HDR), `_Cutoff`, `_MainTex`, `_SkinBumpMap`, `_SkinColor`, `_ChestTex`, `_ChestBumpMap`, `_LegsTex`, `_LegsBumpMap`, `_BumpScale`. These are the same texture slots `Custom/Player` and `VisEquipment` use (VisEquipment.cs:257-271), so the shader can replace the player body shader directly and keep armor textures. Values: `FallenWarrior` `_TintColor` = (5.96, 1.41, 0) (fiery), spirit animals (3.0, 0.64, 1.18) (magenta). Because the blend is multiplicative-additive, `_TintColor` near 0 makes it invisible, and small values give a faint shimmer that brightens the background **(unverified look; it may vanish at night and saturate in snow)**.

Can a player wear a Wraith/Ghost material without looking "cursed"? `Ghost_mat` and `wraith` are opaque `Custom/Creature`, so no. The translucent options are `ShadowPerson 1` (dark) and `Custom/Fallen Warrior` (light).

## 3. Distortion

- `Custom/Distortion` properties: `_Color`, `_MainTex`, **`_NormalTex`**, `_Glossiness`, `_Metallic`, `_NormalScale`, `_RefractionIntensity`, **`_WaveVel`**, `_DepthFade`. It starts with a GrabPass, then forward passes with `Blend SrcAlpha OneMinusSrcAlpha`, ZWrite off, queue Transparent-100.
- `Aspect_mat` has **no textures assigned**, so refraction comes from mesh normals only and gives no moving ripple. That likely explains why our current distortion looks static.
- `staff_shield_shard` (on the `vfx_StaffShield` sphere, the Dvergr/Staff of Protection bubble around the player) is the same shader with `_MainTex` + `_NormalTex`, `_NormalScale` 0.2, `_WaveVel` 5, `_RefractionIntensity` 0.1 and `_Color` (1,0,0,0.52) with blue HDR emission. **Copy its `_NormalTex` into our distortion material to get an animated ripple.**
- Particle distortion: `heathaze_distortion` (`Particles/Standard Unlit2`, keywords `EFFECT_BUMP _NORMALMAP`, `_DistortionEnabled` 1, `_DistortionStrength` 30, `_DistortionBlend` 1, `_BumpMap` set). It is used by `vfx_Ashlands_HeatDistortion` (bundle d59cfac). `shockwave_distortion` is similar (strength -15). Other players can see these, and they can be emitted from a mesh.
- Screen-space: `HeatDistortImageEffect` (HeatDistortImageEffect.cs) uses `Hidden/CameraHeatDistort` with `_Intensity`, `_DistortionStrength`, vignette, `_NoiseTexture`, `_Color`. It is a component on the game camera, `GameCamera.m_heatDistortImageEffect` (GameCamera.cs:133,144), and is driven only for the local player (Character.cs:1022-1031). It works as **local "you are veiled" feedback** (a vignette shimmer) but others never see it. Conflict: Ashlands heat writes `enabled` and `m_intensity` every frame, so we would need a postfix on `UpdateHeatEffects`.

## 4. Mist and body smoke

- Mistlands mist comes from particles, not a volume shader. `ParticleMist` (ParticleMist.cs) emits around the local player, with `Mister`/`Demister` as areas. `vfx_mistlands_mist` uses `heavymist_mistlands` (`Lux Lit Particles/ Tess Bumped`), meant for big world-space clouds. The Demister ball (`demister_ball`) is a lit sphere with sparks, not a fog.
- **The best vanilla body fog is the Ghost's `Visual/black_smoke` emitter:**
  - ParticleSystem shape **14 = SkinnedMeshRenderer** (spawns on the body mesh), placement mode 1, world simulation
  - rate 150/s, lifetime 2 s, size 0.5–0.6, start color white with alpha 0.33, gravity -0.003…-0.008 (slow rise)
  - material `slowwispysmoke_gradient_alphablend` / `Custom/Gradient Mapped Particle (Unlit)` (`_SrcBlend` 5, `_DstBlend` 10, `_GradientAsAlpha` 1, `_GradientChannel` 3, `_SoftParticles` 1, `_FejdFog` 1)

  `Ghost_Void` has two emitters of the same kind using `ghost_smoke` (`Custom/LitParticles`, `_TintColor` 0.43 grey, alpha blend). One of them (`Particles (1)`) is a 500/s cloud of tiny specks.
- `ShadowPerson`: `Breath Particles` (cone, `slowwispysmoke_nearfade_hard`, Lux Lit) at the head, and `Bits/Smoke` (`slowwispysmoke_gradient`, additive variant: Src 3, Dst 1).
- `Wraith`: `evil_smoke _local`/`_bottom` sphere emitters on bones with `wraith_smoke`.
- Despawn puffs that suit a "vanish" moment: `vfx_odin_despawn` (`grave_fog` smoke burst), `fx_raven_despawn`, `vfx_ghost_spawn` (`wraith_smoke`), `fx_summon_spirit_spawn` (ground swirl plus `ghost_trail`).
- Compared with `swamp_mist` (our current fog, a large ground billboard with `_CameraFadingEnabled` and soft-particle distance 2), the Ghost setup clings to the silhouette instead of floating at bone points.

## 5. Status-effect visual pattern

`StatusEffect.TriggerStartEffects` (StatusEffect.cs:116-128) creates `m_startEffects` at the center point, parented to the character and scaled by radius×2 (`EffectList` flags `m_attach`, `m_follow`, `m_scale`, `m_childTransform`; EffectList.cs:14-52). They are removed on stop. Matched by name **(unverified)**:
- `vfx_Wet` (drops)
- `vfx_Tared` (tar drops/blobs)
- `vfx_Frost` (box-shaped `frost` soft cloud with start alpha 0.084, the closest to a cold mist aura)
- `vfx_Burning`
- `vfx_Smoked`
- `vfx_StaffShield` (distortion sphere)

None of them swap materials. The only vanilla material changes on characters are `LevelEffects` (`_Hue`/`_Saturation`/`_Value`/`_EmissionColor`, LevelEffects.cs:60-85) and the `MaterialMan` blocks. Pattern for us: put the veil in a status effect's `m_startEffects` so vanilla syncs and cleans it up on every client.

## 6. Recommendations

**(a) Tier II "wrapped in mist"**
1. **Clone the Ghost's `black_smoke`** (from prefab `Ghost`, child `Visual/black_smoke`). Re-parent it and point `shape.skinnedMeshRenderer` at the player's `VisEquipment.m_bodyModel`. Tint `startColor` grey-blue with alpha about 0.2–0.35 and set rate 60–150. Keep its material. Risks: the armor meshes are separate SMRs, so you may need one emitter per mesh. Shape type 14 needs a readable mesh, and the player body mesh may not be Read/Write-enabled **(unverified)**. Fallback is to keep bone emitters with this material.
2. Use the same bone emitters we have now, but swap `swamp_mist` for `ghost_smoke` / `wraith_smoke` (`Custom/LitParticles`, `_TintColor`, soft-particle distance 0.1). The smoke would hug the body more tightly at minimal code cost.
3. Clone `vfx_Frost`'s `soft cloud` (`frost`, Lux Lit) as a faint box-shaped aura. It is the cheapest option but the least body-shaped.

**(b) Tier III "nearly invisible"**
1. **Swap the body and armor shaders to `Custom/Fallen Warrior`.** Use material instances, take the shader from `Wolf_spiritcaller`'s `M` renderer, and set `_TintColor` to about (0.15–0.4, same, +blue). The textures carry over. It reads as "faint spirit outline". Risks: the look is unverified, it is light-only (gone at night), and `_Cutoff` hair/cape need checking.
2. **Swap to `ShadowPerson 1`** (Standard premultiplied) with `_Color.a` at 0.1–0.2. Vanilla already uses it on player-shaped armor (`SP_*`). It reads as a "dark see-through silhouette". Risks: Standard ignores `_ChestTex`/`_LegsTex`, so armor detail is lost (fine for this tier). Transparent sorting against our fog particles is also a concern.
3. **`Custom/Distortion` with `staff_shield_shard`'s `_NormalTex`** (`_WaveVel` 3–5, `_RefractionIntensity` 0.1–0.5, `_Color.a` low). Optionally add a `heathaze_distortion` body emitter as in (a1). It gives a predator-style shimmer. Risks: GrabPass cost per renderer, and with 0.1 intensity it is nearly invisible against flat backgrounds.

Optional local feedback for both tiers: drive `GameCamera.m_heatDistortImageEffect` at low `m_intensity` with a blue `m_color` while the potion is active, and postfix it against Ashlands heat.
