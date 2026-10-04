# In-game testing checklist

Run `make run`, press F5 for the console. Run `devcommands` once per session only where vanilla cheat commands are needed (Jötunn commands such as `ip_state` work without it). Update this file per plan.

## Plan 1 – skeleton
- [x] Log shows `InvisibilityPotion 0.1.0 loaded` and `Patch health: … 0 missing`
- [x] `make run` lands in the world `testing` without touching the menu
- [x] `ip_state` prints patched targets and `missing: none`
- [x] Release build (`make package`) contains no `ip_state` command and no auto-join

## Test matrix (from the spec, filled in from plan 2 on)

| Setup | Tier I | Tier II | Tier III |
|---|---|---|---|
| Singleplayer | | | |
| Self-hosted, I am host | | | |
| Dedicated, I own the zone | | | |
| Dedicated, another player owns the zone | | | |
| Player joins while I am hidden | | | |

## Plan 2 – Tier III hidden from other players

Singleplayer (checkable now):
- [ ] `ip_give 3`, then `ip_state` prints `hiddenFromPlayers=True`; after the effect ends or a reveal it prints `False`
- [ ] Log shows `Patch health: … 0 missing` with `EnemyHud.TestShow`, `ZNet.UpdatePlayerList`, `ZDOMan.SendZDOs` patched, and no `SendZDOs transpiler: pattern not found` error

Two clients (passed 2026-10-03 on the dedicated server):
- [x] Second client: no nameplate over the hidden player
- [x] Second client: no map pin for the hidden player (even with "share position" on)
- [x] Second client: the hidden player is not rendered near their real position while hidden
- [ ] After a reveal or the end of the effect, the player appears on the second client within one send interval
- [ ] AI in the second client's zone still ignores the hidden player
- [ ] With `AllowPvpInvisibility = false` on the server, tier III hides from enemies only
- [ ] T3 drink vfx is visible to others once at the drinking spot (intended)

## Plan 2 – Round C: veil look tuning (Debug console)

> Historical round record. Current defaults: T1 body Off, T2/T3 Distortion (the `LookDefaultsRevision` 3 migration moves a T1 Distortion to Off); fog look per tier in `[Fog.TierN]`, see Round I. The per-tier text below describes the state when this round ran.

Per-tier defaults: T1 fog light (density 0.35), body Off; T2 body Ghost + fog dense (0.7); T3 body Distortion, no fog. Config: `[TierN] BodyVeilMode/FogEnabled/FogDensity` (admin), look in `[Fog]` and `[Veil]` (local, not synced).

- `ip_fog` prints the fog look; `ip_fog <rate|size|life|speed|alpha|drift> <v>`, `ip_fog color r g b`, `ip_fog alphamode both|material|vertex`, `ip_fog anchor <Head|Chest|Hips|LeftHand|RightHand|LeftFoot|RightFoot> on|off|radius <v>|offset x y z`; `ip_fog save` writes `[Fog]`, `ip_fog reset` re-reads the file. Every change re-applies at once.
- `ip_veil` prints per-tier modes; `ip_veil <none|cutoff|hide|tint|ghost|distortion> [tier]` overrides your current tier (none = Off), `ip_veil off [tier|all]` clears; `ip_veil look distortion <strength> [alpha]`, `ip_veil look ghost <alpha> [emission]`, `ip_veil save` writes `[Veil]`.
- Log lines `veil ghost/distortion/fog: material ... properties: ...` list the shader properties once; report them if a value has no visible effect.
- [ ] T1: thin wisps on head, chest, hips, hands, feet; no glowing sphere
- [ ] T2: ghost body plus denser fog, clearly different from T3
- [ ] T3: distortion without blue tint, nearly invisible; no fog
- [ ] Drinking a potion plays the mead sound/particles; the end of the effect plays the stop effect
- [ ] Listen host: a remote T3 player is not drawn at their real position

## Plan 2 – Round E: per-tier fog, tuning window (task 10d)

> Historical round record. Current defaults: T1 body Off, T2/T3 Distortion (the `LookDefaultsRevision` 3 migration moves a T1 Distortion to Off); see Round I for the current fog defaults. The defaults below are those of round E.

Defaults: T1 body Off + light flattened fog (alpha 0.5, size 1.5, rate 4, life 5, no drift); T2 body Distortion (0.1, alpha 0.08) + two fog layers (dense thin inner, wide faint outer on head/chest/hips); T3 body Distortion (0.03, alpha 0.02), no fog. Look per tier in `[Fog.Tier1..3]` (local, not synced), `[Fog] FogAlphaMode` global, `[Veil]` ghost look only. `[TierN] BodyVeilMode` stays admin. Old keys (`[Fog] FogRate/Anchor.*`, `[TierN] FogEnabled/FogDensity`, `[Veil] Distortion*`, stray `[General]` keys) are removed on the first start; the log lists them. A tier II still on the old default Ghost is switched to Distortion once.

`make build`/`make package`/`make run` refuse while Valheim runs (`FORCE=1` overrides).

- `ip_fogui` opens the tuning window: tabs Tier 1/2/3, sliders for every fog value, outer layer, body mode buttons, distortion strength/alpha, per-anchor rows with live particle counts. Changes apply automatically after 0.2 s. `Save` writes all tiers (and an overridden body mode) to the config, `Reset` re-reads the file and clears overrides, `Give Tn` applies the tab's tier. F7 switches the mouse between window and game (while the window owns it, the player cannot move or attack). `Close` or `ip_fogui` again restores the cursor.
- Console still works: `ip_fog [t1|t2|t3] <key> <value>`, `ip_fog [tN] color r g b`, `ip_fog [tN] anchor <name> on|off|radius v|offset x y z`, `ip_fog [tN] save`, `ip_fog reset`; `ip_veil look distortion <strength> [alpha] [tier]`.
- [ ] T1: soft, light fog spread sideways around head, shoulders, chest, hips, hands, the whole legs and feet; no plume, no cloud above the head
- [ ] T1 dynamic colour: fog tints with the environment (darker at night, warmer at dusk); `Dynamic colour` off gives the plain configured grey-white
- [ ] T2: light distortion plus a dense thin fog close to the body and a fainter, wider layer mostly sideways
- [ ] T3: weak distortion, no fog, no trail; rain splashes and wet/tarred particles on the body are hidden while it lasts and come back afterwards
- [ ] Stale fog: let an effect expire, then move sliders / `ip_fog` values / `Reset`: no fog appears. Log must not show `destroyed stray fog object` (if it does, report the line)
- [ ] Particle readout: counts per anchor roughly rate x lifetime; the warning appears above 200
- [ ] `Save`, restart, the saved values come back; the config has `[Fog.Tier1..3]` and no old `FogRate`/`FogDensity` keys
- [ ] Log: `Patch health: … 0 missing` (Debug includes `PlayerController.TakeInput`); no `bone for anchor` warnings (if any, report the anchor names)

### Round E, part 2: vanilla-based looks (task 10e)

Defaults are unchanged (T1 Off + fog, T2 Distortion + fog, T3 Distortion, no fog). New options, all in `ip_fogui` and saved by `Save`:
- Body modes `Shadow` (copy of the ShadowPerson material, dark see-through, `[Veil] ShadowColor`, default alpha 0.12) and `Spirit` (`Custom/Fallen Warrior` per original material, armour textures kept, `[Veil] SpiritColor` × `SpiritStrength`, default 0.6,0.7,0.8 × 0.25). Console: `ip_veil shadow|spirit [tier]`, `ip_veil look shadow <alpha>`, `ip_veil look spirit <strength> [r g b]`.
- Distortion ripple: `_NormalTex`/`_NormalScale`/`_WaveVel` borrowed from `staff_shield_shard`; `[Fog.TierN] DistortionWave` (-1 = borrowed value). Console: `ip_veil look distortion <strength> [alpha] [wave] [tier]`, `ip_fog [tN] wave <v>`.
- Fog material per tier: `[Fog.TierN] FogMaterial` = `swamp_mist` | `ghost_smoke` | `wraith_smoke` | `slowwispysmoke`. Console: `ip_fog [tN] material <name>`.
- Fog emitter per tier: `[Fog.TierN] FogEmitterMode` = `Bones` | `Mesh` (one emitter on the body surface, `MeshOffset` metres off the skin, rate = Rate × enabled anchors). Console: `ip_fog [tN] emitter mesh`, `ip_fog [tN] meshoffset <v>`.

- [ ] Log once per look: `veil shadow: borrowed material 'ShadowPerson 1' from …`, `veil spirit: shader 'Custom/Fallen Warrior' from material …`, `veil distortion: ripple from 'staff_shield_shard' …`, `veil fog: borrowed material … for FogMaterial …`. Report any `not found … using Tint/swamp_mist` warning
- [ ] T3 compare (tab Tier 3, `Give T3`): `Distortion` (now with a moving ripple), `Spirit` (faint light shell, armour visible, check at night and in snow), `Shadow` (dark see-through silhouette). Report which one reads best as "nearly invisible"
- [ ] Spirit strength 0 → invisible; 1–3 → clearly glowing; tint sliders change the colour. Hair/beard/cape keep their cut-out shape
- [ ] Shadow alpha 0 → invisible, 0.3 → like the vanilla ShadowPerson
- [ ] Distortion wave 0 → static (as before), 5 → ripple moves; `use borrowed` restores the vanilla value
- [ ] Fog material buttons: each of the four changes the look of T1/T2 fog; no pink/black squares (if so, report the material name)
- [ ] Emitter `Mesh`: fog clings to the body surface, follows the animation; the readout shows `Mesh` counts. If the log says `Mesh emitter not possible (… not readable)`, the fog stays on Bones (report the line)
- [ ] After every mode/material switch and when the effect ends: the body is restored exactly (armour textures, hair, skin colour), no leftover fog; change armour while veiled in Spirit mode, then let the effect end and check the new armour shows correctly
- [ ] `Save`, restart: `[Veil] ShadowColor/SpiritColor/SpiritStrength` and `[Fog.TierN] FogMaterial/FogEmitterMode/MeshOffset/DistortionWave` come back

## Plan 2 – Round F: trails, outer ring, potion burst, tuning window v2 (task 10f)

New defaults (the first start resets `[Fog.Tier1]` and `[Fog.Tier2]` once to them and switches a `[Tier1] BodyVeilMode` still on `Off` to `Distortion`; the log says `Config migration (look defaults revision 1)`; tier III is untouched):
- T1: Distortion 0.04 (alpha 0.03) plus small wisps on the body surface (`FogEmitterMode Mesh`, `MeshRate 18`; Bones fallback 2.5 per bone), size 0.28, life 4 s, alpha 0.3, `Trail = true`: the wisps stay behind when you move. No outer layer.
- T2: Distortion 0.1 (alpha 0.08), dense thin inner layer on the bones (rate 10, size 0.45, life 2 s, alpha 0.4, follows the body) plus a wide flat outer ring on Chest, Hips, Head, LeftHand, RightHand (`OuterRadiusMultiplier 6` ≈ 1.5 m around the chest, `OuterSpreadY 0.25`, alpha ×0.75, size ×3.5 ≈ 1.5 m puffs, rate ×0.5, life ×1.5, `OuterTrail = true`).
- T3 unchanged (Distortion 0.03, alpha 0.02, no fog).
- New `[Fog.TierN]` keys: `Trail`, `MeshRate` (0 = Rate × enabled anchors), `OuterAnchors` (comma list), `OuterSpreadY`, `OuterLifetimeFactor`, `OuterTrail`. Console: `ip_fog [tN] trail on|off`, `outertrail on|off`, `outeranchors Chest,Hips,Head`, `outerspready <v>`, `outerlife <v>`, `meshrate <v>`.
- Potions borrow the drink/expire effects of `[Veil] PotionVfxSource` (default `MeadFrostResist`, needs a restart).
- `ip_fogui` v2: scaled window (`[Dev] TuningWindowScale`, 0 = screen height / 1080, Scale -/+ in the header), drag by the title, resize with the grip bottom right (size and position are remembered in `[Dev] TuningWindowRect`). Every value is one row: label, big slider (mouse wheel steps), `-`, typed field (Enter or clicking elsewhere applies), `+`, `R` (default). Shift = fine (step/10), Ctrl = coarse (step×10) on `-`/`+` and the wheel. Labels turn yellow with `*` when the value differs from the saved file. Foldouts: Fog inner, Fog outer (with outer anchor checkboxes), Anchors (compact radius/x/y/z fields), Look. Footer: `Save`, `Reset` (re-read file), `Defaults Tn`, `Undo` (one level, after Reset/Defaults/Copy), `Give Tn`, `Close`, `Copy Tn to T…`, `Preview follows tab`. Status line shows `Pending…` or `Applied hh:mm:ss`.

What to compare:
- [ ] Log: `Config migration (look defaults revision 1)` once (not on the second start); `potion effects: copied … effects from <name>` names `MeadFrostResist`'s effect (or a warning naming the fallback); `Patch health: … 0 missing` (now includes `VisEquipment.SetChestEquipped` and `VisEquipment.SetLegEquipped`)
- [ ] T1 standing: faint shimmer of the body plus small wisps on the body surface that keep its shape (no blob). If the log says `Mesh emitter not possible`, the wisps sit on the 13 bones instead (report the line)
- [ ] T1 walking/running: a clear wisp trail behind you that fades within ~4 s; standing still, the wisps gather on the body again
- [ ] T2 standing: dense thin fog close to the body plus a clearly visible wide, flat ring (about 1.5 m around the chest/hips, at head and hand height); report if the ring is still invisible or a solid wall
- [ ] T2 walking: the inner fog moves with you, the outer ring leaves a shorter trail than T1
- [ ] Drinking any tier: whitish/bluish burst instead of the red health burst; the expire effect matches
- [ ] Armour swap: in T2 (Distortion) take off / change chest and legs armour, let the effect expire: the body shows exactly the armour you now wear (no old chest/leg textures, no blue glass). Repeat once with Spirit and once with Shadow via `ip_veil`
- [ ] Window: scale fits the screen (text readable at 1440p/4K), `Scale +/-` changes it; drag and resize, close, reopen, restart: the window comes back at the same place and size
- [ ] Window rows: dragging the slider, wheel over the slider, `-`/`+` with and without Shift/Ctrl, typing a value plus Enter, typing then clicking elsewhere, `R` → each changes the fog after ~0.2 s; the label turns yellow; after `Save` the yellow marks disappear
- [ ] `Copy T2 to T1`, then `Undo` → T1 is back; `Defaults T1` then `Save` writes the defaults; `Preview follows tab` on → switching tabs gives that tier after ~0.6 s
- [ ] After `Close`, game keys work at once (no stuck text focus), the camera captures the mouse again
- [ ] `ip_fog t2 outeranchors Chest,Hips` and `ip_fog t1 trail off` still work and show up in the window

## Plan 2 – Round I: matte fog, tuned tier II, fog after expiry (task 10i)

New defaults (the first start resets `[Fog.Tier1]` and `[Fog.Tier2]` once more; the log says `Config migration (look defaults revision 4)` and lists every reset value; your saved tier II values are the new defaults, so only `Emission` changes there; tier III is untouched; `[Tier1] BodyVeilMode` is no longer touched by the migration):
- All tiers: `Emission = 0` (matte, lit by the scene only; no glow at night). The material's `_Color` is the configured colour clamped to 0..1, `_EmissionColor` is black.
- T1: alpha 0.18, colour 0.8,0.82,0.85 (greyer, fainter).
- T2: your saved tuning: size 0.685, alpha 0.035, colour 0.775 grey, spread 1.125/1/1.025, drift -0.066 (sinks slightly); rate 12, life 2.5, speed 0.05, outer ring and ground field unchanged.
- Cleanup: `Remove` now stops and clears every particle system before destroying it, sweeps stray `ip_fog*` children and logs `veil removed T<n> on <name>: N emitters destroyed, M strays swept` (plus the vanilla particle renderers shown again after a body-hiding mode, by name).
- Diagnostics: `ip_fog strays` logs every veil particle system in the scene (name, three parents, active, particles, emitting, age, `tracked` / `NOT TRACKED`). `ip_fog dump` without a veil runs the stray scan. The dump's material line now also shows `_LightNormalFactor`, `_BumpScale`, `_SkyMask` and the shader keywords.

What to compare:
- [ ] Log: `Config migration (look defaults revision 4)` once (not on the second start)
- [ ] T1 and T2 by day and at night: the fog reads as matte mist, rather barely visible than glowing. If it still glows, attach an `ip_fog dump` block (material line) from the log
- [ ] **T2 active: run `ip_fog dump` standing and once walking** and attach the `fog dump` block
- [ ] Let T2 expire normally (no window open): after `SE_Invisibility T2 cleaned up` the log shows `veil removed T2 ...: 15 emitters destroyed, 0 strays swept`. Wait 10 s, run **`ip_fog strays`**: expect `0 veil particle systems in the scene`. If fog is still visible, run it again while looking at it and attach both blocks plus the `veil removed` line
- [ ] Repeat the expiry check once with `ip_fogui` open and tuning tier II while it runs out
- [ ] Note what you see when T2 ends: if something reappears that was hidden during T2 (rain drips, torch flames, wet effect), compare it with the vanilla particle names in the `veil removed` line

## Plan 2 – Round H: ground fog field, tier I normal body, outer-layer diagnostics (task 10h)

> Historical round record; current defaults in Round I (matte fog, tier I alpha 0.18, tier II = your saved tuning).

New defaults (the first start resets `[Fog.Tier1]` and `[Fog.Tier2]` once more; the log says `Config migration (look defaults revision 3)`, lists every reset value and `[Tier1] BodyVeilMode Distortion -> Off`; tier III is untouched):
- New emitter kind **ground fog field** (`[Fog.TierN] Ground*` keys, window foldout "Fog ground field", console `ip_fog [tN] ground on|off`, `groundrate`, `grounddistance`, `groundsize`, `groundgrow`, `groundlife`, `groundalpha`, `groundradius`, `groundheight`, `grounddrift`): flat fog patches lying parallel to the ground at the feet (0.15 m up), in world space, so they stay where they were emitted and grow (0.8 m → 2.4 m over 7 s). They are emitted 4 per second plus 2 per metre walked, so walking lays a field of fog along the path. They fade in quickly and fade out over the last 40 % of their life.
- T1: the **normal player model** (`[Tier1] BodyVeilMode = Off`, no distortion) in a thin, close fog layer that follows the body (rate 5, size 0.45, life 2 s, alpha 0.22, `SpreadY 0.5`, `Trail = false`, emission 0.25), plus the ground field. No outer layer.
- T2: inner cloud unchanged; ground field denser and wider (rate 6, 3 per metre, size 1.0, grow 3.5, alpha 0.3, radius 0.9). Outer ring kept on for the diagnostics, and its particles now lie flat (new key `OuterHorizontal = true`, window "Outer flat", console `ip_fog t2 outerflat on|off`): the camera-facing 1.1–2.3 m particles were each about four times taller than the 0.4 m thick disc, so the "flat ring" was drawn as a tall blob merging with the body cloud.
- Diagnostics: `ip_fog dump` (and `ip_state` while veiled) logs every fog emitter of your veil: object, layer (Inner/Outer/Ground), playing, particle count/max, rate, sizes, lifetime, simulation space, render mode, material values (`_Color`, `_EmissionColor`, fades), renderer enabled/visible, bounds, emitter position vs. the player, camera distance, and the average alpha/size/height of the live particles. Every veil build also logs `veil fog spawned T<n> ...` with one line per emitter.

What to compare:
- [ ] Log: `Config migration (look defaults revision 3)` once (not on the second start), including `[Tier1] BodyVeilMode Distortion -> Off`
- [ ] T1 standing: the normal character (armour, skin, no glass/shimmer) wrapped in a thin whitish fog; a small pool of fog forms at the feet
- [ ] T1 walking/running: a field of flat fog patches stays on the ground along the path and spreads, fading within ~7 s; **no plume/exhaust trail** behind the body
- [ ] T2 standing and walking: the inner cloud as in round G plus a denser, wider ground field along the path
- [ ] T2 outer ring: a flat ring of fog around the hips, best seen with the camera looking down; compare `ip_fog t2 outerflat off` (old camera-facing look). Visible or not? Either way, with T2 active and the ring expected, run **`ip_fog dump`** while standing still, then once while walking, and attach the `fog dump` block from `BepInEx/LogOutput.log` (the lines starting with `ip_fog_outer` matter most)
- [ ] Ground field on slopes and stairs: report patches that cut into the terrain or float; `ip_fog t1 groundheight 0.3` raises them
- [ ] Window: the "Fog ground field" foldout changes the field live; `Solo outer` now hides the ground field too
- [ ] T3 unchanged (Distortion 0.03, alpha 0.02, no fog)

## Plan 2 – Round G: tier I/II looks, outer disc, fog brightness, draggable sliders (task 10g)

> Historical round record; current defaults in Round I.


New defaults (the first start resets `[Fog.Tier1]` and `[Fog.Tier2]` once more; the log says `Config migration (look defaults revision 2)`, lists every reset value and each obsolete key with its old value, e.g. `[Fog.Tier2] OuterRadiusMultiplier = 6 is obsolete … and removed`; tier III is untouched):
- All tiers: `DynamicColor = false` (the 50 % blend with the environment fog colour made the fog dark in rain and at night). New key `Emission` (0..1, T1/T2 0.25, T3 0): the fog glows with its own colour × Emission, so it stays whitish in the dark. The fog material also fades less near the camera and near surfaces (`_CameraFadeDistance` 0.3–1 m instead of 1–5 m, `_ZFadeDistance` 0.25 instead of 1).
- T1: body clearly visible and shimmering (`DistortionColor` alpha 0.5, Distortion 0.04). Fog on all 13 bones (`FogEmitterMode Bones`, rate 6, size 0.6, life 3.5 s, alpha 0.35, speed 0.03, drift 0.03, `Trail = true`, `MeshRate 0`). No outer layer. `Mesh` stays selectable.
- T2: Distortion 0.1 (alpha 0.08). Inner fog on all 13 bones, rate 12, size 0.7, life 2.5 s, alpha 0.45, `SpreadY 0.4`, follows the body. Outer disc on `Hips`: `OuterRadius 1.4` m, `OuterAlpha 0.35`, `OuterRate 14`/s, `OuterSize 1.8` m, `OuterLifetime 3` s, `OuterSpreadY 0.15`, `OuterTrail = true`.
- Outer keys are absolute now: `OuterRadius` (m), `OuterAlpha`, `OuterRate` (/s per outer anchor), `OuterSize` (m), `OuterLifetime` (s) replace `OuterRadiusMultiplier`/`OuterAlphaFactor`/`OuterRateFactor`/`OuterSizeFactor`/`OuterLifetimeFactor`. The outer layer spawns whenever `OuterEnabled` and `OuterRate > 0`, even with the inner `Rate` at 0. Console: `ip_fog t2 outerradius 1.4`, `outeralpha`, `outerrate`, `outersize`, `outerlife`, `emission 0.25`.
- `ip_fogui`: the slider is a new control: press anywhere on the track and drag. Inner section has `Emission (glow)`; the outer section has absolute rows and a `Solo outer (inner off; not saved, all tiers)` toggle that hides the inner layer to isolate the disc. `Distortion alpha (body opacity)` now goes up to 1.

What to compare:
- [ ] Log: `Config migration (look defaults revision 2)` once (not on the second start), with the obsolete-key lines; `Config: removed N obsolete keys` lists them with their values
- [ ] T1 standing: body clearly visible (shimmering, not ghost-like); whitish fog hugging the whole body
- [ ] T1 walking/running: a strong whitish fog trail that fades within ~4 s
- [ ] T2 standing: one fog cloud from head to feet (not single blobs at chest/hips/head/hands), whitish, also in rain and in the evening
- [ ] T2 outer disc: a wide flat disc (~1.4 m around the hips) clearly visible from the side and from above (zoom the camera out, look down); with `Solo outer` on only the disc is left
- [ ] T2 walking: the inner cloud moves with you, the disc leaves a lighter trail
- [ ] `Emission`: in the dark or in rain set it 0 → fog turns grey/dark, 0.25 → whitish, 1 → bright; `DynamicColor` on → fog takes on the environment tint
- [ ] Window sliders: press on the thumb and drag left/right → the value follows the mouse; press anywhere on the track → jumps there and keeps following while dragging; release outside the window → the slider lets go. Wheel, `-`/`+`, typed field and `R` work as before
- [ ] T3 unchanged (Distortion 0.03, alpha 0.02, no fog)
- [ ] Given a Greydwarf set on fire by your torch hit while hidden, do re-hide (T2) and wait, expect no re-reveal from the burn ticks (burning/poison/freezing hits are ignored by the reveal hook)
- Dev-only: saving from the Debug tuning window (or `ip_veil save`) while connected to a server writes the server's synced admin values into the local config file.

## Plan 2 – gameplay core (tasks 2, 4-10)

Format: given / do / expect. `[x]` = confirmed in-game (Round A 2026-10-01, Rounds B-E); unchecked = open.

Config and dev commands (Task 2):
- [x] Given a running world, do edit `[Tier1] Duration` in the config file and run `ip_reload_config`, expect the new value in the log and in `ip_state`
- [x] Given the game started, do read the log, expect `Patch health: N targets patched, 0 missing` (patch list complete)

Status effect and hiding (Tasks 4, 5):
- [x] Given a fresh world, do `ip_give 2`, expect `ip_state` shows `zdo=2` (hidden) and the effect icon
- [x] Given tier II active, do wait for expiry, expect state cleared and the body/fog restored
- [x] Given tier III active, do die (`suicide` after `devcommands`), expect state and veil cleaned up and nothing left after respawn
- [x] Given tier I active, do hit an enemy, expect the effect ends on the own hit (tier I has no re-hide)

Enemy perception and aggro (Tasks 6, 7):
- [x] Given tier II active and a Greydwarf chasing, do stay hidden, expect it drops the target after the loss time (re-hide path)
- [ ] Given a sleeping monster (Troll/Boar camp), do walk past with tier I/II, expect it stays asleep. NOT confirmed: not tested yet
- [ ] Given tier I active, do crouch and then stand up, expect visibility ramps down over about 3 s (known behaviour, Task 7 finding)
- Note: `AggroLossTime` values above 30 cannot extend vanilla behaviour (Task 6 finding); the config description says so.

Reveal triggers (Task 8):
- [x] Given tier II active, do hit an enemy, expect `Revealed` (DamageDealt) in the log, and the veil off, back after `RehideDelay`
- [x] Given tier II active, do draw a bow, expect a BowDraw reveal
- [x] Given tier II active, do take damage, expect a DamageTaken reveal
- [x] Given tier II active, do block, expect a Block reveal

Visuals (Task 9, rounds C-F):
- [x] T3 look (Distortion 0.03 / alpha 0.02, no fog) approved by the user
- [ ] Round F looks (T1 wisps with trail, T2 outer ring, potion burst, tuning window v2): pending round F, see the Round F section above
- Note: Ghost/Distortion/Shadow/Spirit body modes also hide the player's own torch and weapon particles (Task 10d finding).

Cleanup stress list (Task 9; every exit must go through `SE_Invisibility.Stop()`):
- [ ] Given tier II active, do change armour, expect the veil persists and the armour shows correctly after expiry (pending round F)
- [x] Given tier III active, do die, expect no leftover fog or distortion after respawn (confirmed in round A for the state; the round-C+ visuals are covered by the Round F armour/expiry check)
- [ ] Given tier II active, do log out to the menu and back in, expect no veil on the next login and the log shows `cleaned up`
- [ ] Given tier I active, do `ip_give 3`, expect a clean upgrade (no leftover tier I fog)
- [ ] Given a veil, do wait for expiry, expect no `ip_fog*` child left on the player

Potions and recipes (Task 10):
- [x] Given the Mead Ketill (`piece_MeadCauldron`), do open it, expect the three recipes visible
- [x] Given a crafted mead, do drink, expect it drinks, plays the drink sound and particles
- [x] Given a higher tier active, do drink a lower tier, expect it is refused
- Note: the networked mead start vfx reveals a drinker's spot once at drink time (ruling from Task 10c, intended).

## Known limitations and notes (final fix wave)

- Recipes: the `Recipe` entries are read once at startup from the local config file, apply locally and are not server-controlled yet; `ip_reload_config` does not apply them. The entry is still marked admin-only.
- The first hit from hiding on an unalerted monster gets the vanilla backstab bonus (`CanSeeTarget` is false for a hidden attacker). Intended.
- Out-of-range `IP_Tier` values in a ZDO (not 1..3) count as not hidden and never throw.

## Plan 3 backlog

- [ ] Recipes: rebuild the three recipes' `m_resources` in `ObjectDB.instance.m_recipes` on config sync / `Changed`, so the admin recipe is authoritative.
- [ ] Reveal on projectile launch (crossbow, thrown weapons, bombs), not only on hit.
- [ ] Remote tier III player's torch `Light` still lights the surroundings under the forced Hide mode.
- [ ] Veil code (VeilController/FogVeil) should not run on a headless dedicated server.
- [ ] Balancing of recipes and fermenter output (plan 4).
- [ ] Two-client check: inject an out-of-range `IP_Tier` with a debug command and confirm nothing throws.

## Plan 4 – first integration (custom models)

Setup: `make build` (Debug), `make run`. Everything below also writes to the BepInEx log; paste the `assets:` lines into the chat.

Startup log:
- [ ] `assets: manifest resources: ...` lists `InvisibilityPotion.Assets.ip_assets` and `....ip_assets.windows` (S5); `assets: loaded 'ip_assets' (... 30 prefabs, 73 materials)`
- [ ] One `assets: item ...` line per mead, base and goggles: note the template components (S4) and the layer
- [ ] `assets: T1..T3 mist on 1 anchor(s)` (one MistAnchor per bottle: the attach wrapper holds the only model)
- [ ] `assets: <item>: material ...: JVLmock_Custom/X -> Custom/X (resolved by the plugin)` per item material; any `not found; using Custom/Creature` warning names a wrong shader guess (expected candidate: `Custom/Vegetation`)
- [ ] `assets: 21 plant prefabs registered`, `assets: goggles VeilGoggles_T1..3 registered`, `assets: bundle unloaded`
- [ ] After joining: `assets: shader check at OnPrefabsRegistered: N material(s) still had a JVLmock_ shader` (N > 0 means Jötunn had not fixed the plant materials yet; the plugin did)

Items (meads and bases):
- [ ] Inventory icons of the three bases (bowls) and three meads (bottles) show the new models (icons are rendered with the resolved shaders)
- [ ] Craft a base at the Mead Ketill; put it into the fermenter; the fermenter accepts it and yields 4 meads of the tier
- [ ] Drop a mead and a base: they fall, rest upright-ish on the ground, can be picked up (collider size ok, not floating, not sinking)
- [ ] Glass: does `Custom/Distortion` read as tinted glass (alpha 0.45) or invisible/opaque? Screenshot against the sky and against terrain (S2)
- [ ] Mist inside the finished bottles: visible, stays inside the glass, tier colour, not flickering against the glass (sorting)
- [ ] Item stand: hang a mead and a base; the model shows (needs the `attach` child) and sits sensibly (grip at the neck / rim). Note the orientation
- [ ] Hand-held: consumables are not equipped in vanilla, so no held bottle is expected; drinking still plays the burst
- [ ] Colours: compare with `tools/blender/preview-*.png` (the materials carry Blender's linear values, which Unity treats as sRGB: if the game renders in linear space everything looks darker and more saturated than the preview, and the setup needs a linear-to-sRGB conversion)
- [ ] Existing meads from earlier rounds in the inventory still exist (same prefab names) and now show the bottle

Plants and goggles (look checks):
- [ ] `ip_spawn <name>` for each: `Plant_T1_S1`, `Plant_T1_S2`, `Plant_T1_S3`, `Plant_T1_S3_a/b/c`, `Plant_T1_Flat`, `Plant_T1_Flat_a/b/c`, `Plant_T2`, `Plant_T2_a/b/c`, `Plant_T2_picked`, `Plant_T3`, `Plant_T3_a/b/c`, `Plant_T3_picked` (spawned on the ground 5 m ahead, facing you; tier 1 is meant for bark and will stand upright)
- [ ] Lichen beards and leaves visible from both sides (Vegetation shader cull-off), no pink, glowing parts glow (`helfern_heart`, `helfern_ember`, `baldr_glow`)
- [ ] `ip_spawn VeilGoggles_T1` (and T2, T3): dropped goggles visible on the ground
- [ ] `ip_give goggles 1` (2, 3): goggles in the inventory with an icon, equip in the helmet slot: they show on the head; note position and facing (Blender -Y front should be the face side; if backwards or offset, the `attach` origin needs a rotation/offset); hair stays visible
- [ ] `ip_components VeilGoggles_T1`, `ip_components MeadHealthMinor`, `ip_components HelmetLeather`: paste the logs (S4: compare the vanilla child names/components with ours)
- [ ] `ip_shaderdump MeadInvisibility_T2` and `ip_shaderdump Plant_T2`: shaders resolved, render queue of the glass
- [ ] `ip_bundle`: every material listed with a `Custom/...` shader, none `JVLmock_...`, all `supported True`

## Plan 4 – round J (fix round 2)

Setup: `make build` (Debug, game closed), `make run`. Paste the `assets:`, `veil fog:`, `auto fog dump` and `ip_exportmesh` lines.

Startup log:
- [ ] `assets: loaded 'ip_assets' (... 33 prefabs, 83 materials)`
- [ ] `assets: item MeadInvisibility_TN <- MeadBottle_TN ...: scale x1.6, ... box (...)` and `MeadBaseInvisibility_TN ...: scale x2.4`: the box size is the round-I size times the factor (round I: compare with the previous log, lines 33–1119)
- [ ] `assets: prop Plant_* : scale x1.5` for every plant
- [ ] `assets: T1..T3 cork wisp on 1 anchor(s) at (...)` (the y value is the top of the bottle)
- [ ] `assets: item VeilIngredient_T1..3 <- Ingredient_T1..3 ...: scale x1.5` and `assets: ingredient VeilIngredient_TN registered (material, stack 20, ...)`
- [ ] `Config migration (look defaults revision 5): ...; FogMaterial -> soft on N tier(s)` once (revision 4 tuning kept), then `veil fog: soft material from 'swamp_mist' ...: _MainTex dust02 -> ip_fog_sprite ..., _NormalTex wave-normal -> ip_fog_flat_normal`

Sizes and looks (screenshots next to vanilla `MeadHealthMinor` / `MeadBaseHealthMinor`, `ip_spawn MeadHealthMinor`):
- [ ] Bases (bowls) about as big as the vanilla mead bases; meads (bottles) about the vanilla potion size; both rest on the ground (collider fits, no floating, no sinking)
- [ ] Bottle glass opaque and slightly lighter than the Blender colour, not broken/see-through; no interior mist visible
- [ ] Cork wisp: a thin, slow wisp rising from the cork in the tier colour; on the ground, on an item stand, and in the inventory icon (should not show up as a blob)
- [ ] Item stand: hang a mead and a base; the bigger size carries over; note the orientation
- [ ] Plants 1.5x bigger; `ip_spawn Plant_T2` stands on the ground (no 0.3 m float); `ip_spawn MeadHealthMinor` still drops from 0.3 m (the spawn message says `rigidbody` / `no rigidbody`)
- [ ] Plant visibility, pickability and spawn rules are plan 5: not checked here

Ingredients:
- [ ] `ip_give ingredient 1` (2, 3, optional amount): "Huldra's Hair", "Baldr's Tear", "Hel's Ember Spore" with icons, stack to 20, descriptions shown
- [ ] `ip_spawn VeilIngredient_T2`: visible on the ground, can be picked up; ribbons/petals visible from both sides (Creature shader may be single-sided)

Goggles v3:
- [ ] `ip_give goggles 1` (2, 3), equip: lenses clearly visible on T1 and T2; note how far the goggles sit from the head
- [ ] `ip_exportmesh head`: writes `BepInEx/export/head_body.obj` and `head_HelmetLeather.obj`; the log names the helmet joint and whether BakeMesh or the bind-pose fallback was used. `ip_exportmesh VeilGoggles_T1` writes `VeilGoggles_T1.obj`. Hand the files to the Blender fitting step; if a mesh line says `not exported (read failed ...)`, paste it

Fog (tiers I and II; drink or `ip_give 1` / `ip_give 2`):
- [ ] Fog colour neutral grey/white, no brownish/yellowish tint; not brighter than round I
- [ ] Ground field reads as a soft volume (two layers, 0.15 m and 0.45 m, overlapping patches of varying size), not as flat discs
- [ ] 2.5 s after the veil appears the log carries `auto fog dump at ...` ... `auto fog dump end` without running a command: check the `ip_fog_outer` lines (particles n/max, renderer `visible`, bounds, camera distance) for the outer ring, and the two `Ground` / `GroundUpper` emitters (particles ≤ max, sum of the two max ≤ 300)
- [ ] `ip_fog material swamp_mist` vs `ip_fog material soft` (current tier): the dust texture is the only difference; if `soft` renders as hard-edged squares, report it (`_AlphaChannel` assumption, `docs/decompile-notes.md`)

## Plan 4 – round K (tier II outer ring)

Setup: `make build` (Debug, game closed), `make run`, drink a tier II mead or `ip_give 2`. Paste the `Config migration`, `veil fog spawned` and `auto fog dump` lines.

- [ ] `Config migration: [Fog.Tier2] Outer... = ... is reset (outer ring)` for each changed Outer* key, then `Config migration (look defaults revision 6): ...; N [Fog.Tier2] Outer* values reset to the ring defaults` once; the other tier II keys (inner cloud, ground field, anchors) keep their values
- [ ] `veil fog spawned T2` lists `ip_fog_outer [Outer] Hips: rate 18/s, ring radius 2.5 m, band 0.1 x r, offset y -0.2 m, rotation 8 deg/s, Follow, horizontal`
- [ ] Auto fog dump, `ip_fog_outer` line: `space Local`, `velocity True` and `; shape Circle r 2.5 thickness 0.25 rotation (90, 0, 0) jitter 0.25, orbital 0.14 rad/s (8 deg/s)`; the live particles sit about 0.6–1 m above the player (hips minus 0.2 m)
- [ ] A ring of fog about 2.5 m around the player at hip height, clearly a second layer apart from the body cloud (a ring, not a filled disc)
- [ ] Walking and running: the ring stays around the player (no patches left behind along the path)
- [ ] Standing still: the ring turns slowly (about one turn in 45 s)
- [ ] Ground field unchanged (patches stay along the path as in round J); tier I has no outer ring; tier III unchanged
- [ ] Tuning window (`ip_fogui`, outer section): `Rotation deg/s`, `Offset Y m` and `Band height (x radius)` rows change the ring live; `ip_fog outerrotation 20` and `ip_fog outeroffsety 0` work too

## Plan 4 – round L (tier I and II as real fog)

Setup: `make build` (Debug, game closed), `make run`, `ip_give 2`, later `ip_end` and `ip_give 1`. Paste the `Config migration`, `veil fog soft material`, `veil fog spawned` and `auto fog dump` lines.

- [ ] `Config migration: [Fog.Tier1] ... is reset` / `[Fog.Tier2] ... is reset` for each changed key, then `Config migration (look defaults revision 7): N [Fog.Tier1]/[Fog.Tier2] values reset to the new defaults` once; tier III keeps its values
- [ ] `veil fog: soft material ... _MainTex ... -> ip_fog_sprite (128x128 white, gaussian alpha, centre 0.8, noise +-10 %)`
- [ ] `ip_give 2` prints `applied T2; end with: ip_end`; `ip_end` prints `veil ended (1 effect(s) removed)`, the fog and the body veil go away; a second `ip_end` prints `veil ended (no InvisibilityPotion effect was active)`
- [ ] Tier II `veil fog spawned T2`: 13 inner emitters (`rate 14/s, size 0.8 m, alpha 0.35`), two ground emitters (`alpha 0.18`), and `ip_fog_outer [Outer] Hips: rate 10/s, Volume radius 3 m, height 0.35 x r, offset y 0 m, rotation 3 deg/s, Follow, camera-facing, size 3.2 m, alpha 0.1`
- [ ] Auto fog dump, `ip_fog_outer` line: `shape Sphere r 3 scale (1, 0.35, 1)`, `startSize 2.56..3.84`, `lifetime 4.8..7.2`, `speed 0.03`, `space Local`, `; outer shape Volume: Sphere ... random direction 1`, renderer `mode Billboard`, `maxParticleSize 10`
- [ ] Tier II look: a light fog in a wide area (about 3 m) around the player, a soft volume about hip height (+-1 m), not a ring of discs; denser fog close to the body that wraps the whole body head to feet
- [ ] No hard disc edges: single sprites fade out softly, overlapping sprites do not show stacked circles
- [ ] Walking and running: the volume and the body cloud stay with the player; the ground field still lays patches along the path, lighter than before
- [ ] Tier I (`ip_end`, `ip_give 1`): the normal body is wrapped as a whole in a light fog (not only hands and feet), plus a fainter fog volume (radius 2.4 m) and a light ground field; `veil fog spawned T1` lists 13 inner emitters `rate 10/s, size 0.75 m, alpha 0.25` and `ip_fog_outer ... Volume radius 2.4 m`, alpha 0.07
- [ ] Tier III unchanged
- [ ] Tuning window (`ip_fogui`, outer section): `Outer shape [Volume] Ring` switches live between the volume and the round J ring; `Height (x radius)` flattens the volume; `ip_fog outershape ring` works too

## Plan 4 – round M (subtle haze left behind)

Setup: game closed, `make build`, `make run`, `ip_give 2`, later `ip_end` and `ip_give 1`. Test in daylight and at night. Paste the `Config migration`, `veil fog spawned` (including the `material` lines) and `auto fog dump` lines.

- [ ] `Config migration (look defaults revision 8): N [Fog.Tier1]/[Fog.Tier2] values reset to the new defaults` once (each reset key logged before); tier III keeps its values
- [ ] Tier II `veil fog spawned T2`: 13 inner emitters `rate 14/s, size 0.8 m, alpha 0.12 ..., max 10`; two ground emitters `alpha 0.12` / upper, `max 40` and `max 20`; `ip_fog_outer [Outer] Hips: rate 4/s, Volume radius 3 m, height 0.35 x r, ..., rotation 0 deg/s, Trail, camera-facing, size 3.2 m, alpha 0.06 ..., max 40`
- [ ] The `material 'ip_fog_mat' (...)` lines: `_Color (0.55, 0.57, 0.6, a)`, `_EmissionColor (0, 0, 0, 1)`, `check ok` (no `WARNING`); note the `blend [...]` and `keywords [...]` lists for the next round
- [ ] Auto fog dump, `ip_fog_outer` line: `particles n/40`, `rate 4/s + 2/m`, `startSize 2.56..3.84`, `lifetime 7.2..10.8`, `speed 0.03`, `space World`, `size over life True`, `velocity False`
- [ ] Daylight: tier II reads as a subtle grey haze around the player, no white blob; the body is wrapped but not hidden behind a bright cloud
- [ ] Night: no glow; the haze is darker than the sky, not lit up
- [ ] Walking: the fog volume stays behind along the path and fades out over about 9 s (not moving rigidly with the player); standing still slowly fills the spot around the player
- [ ] Running: note whether the trail breaks up (the volume is capped at 40 live particles per emitter)
- [ ] Tier I (`ip_end`, `ip_give 1`): the same behaviour, fainter (`alpha 0.09` inner, `0.045` volume, `0.1` ground), grey
- [ ] Tier III unchanged
- [ ] Tuning window (`ip_fogui`, outer section): new row `Rate /m walked (trail only)`; the outer alpha slider steps by 0.005

## Plan 4 – items round L (goggles fit, sizes, durability, wind)

Setup: `make build` (Debug, game closed), `make run`. Paste the `assets: item`, `assets: prop` and `ip_matdump` lines.

Startup log:
- [ ] `assets: item MeadInvisibility_TN <- MeadBottle_TN ...: scale world x2.4 / attach x2, durability False ...`; bases `world x3 / attach x3`; `VeilGoggles_TN ... world x2.2 / attach x1, durability False`; `VeilIngredient_TN ... world x8 / attach x8`
- [ ] `assets: prop Plant_T2*/Plant_T3*: scale x2.2`, `Plant_T1*: scale x4.5`

Goggles (`ip_give goggles 1`, 2, 3; equip; front and side screenshots):
- [ ] T1 and T2 sit higher than in round K, the lenses in front of the eyes (not below them), the rims close to the face and tilted slightly outward with the eye sockets; nothing sticks into the head
- [ ] T3: the mask lies on the face (about 3 mm off, also at the cheeks); the lens rims bend with the mask at the temples, nothing floats; the strap lies on the head all the way round
- [ ] No durability bar on the goggles in the inventory or hotbar
- [ ] Drop goggles: on the ground they are about 2.2x the worn size (clearly visible); picked up and worn again: worn size unchanged
- [ ] Goggles on an armour stand: worn size

Sizes:
- [ ] Bottles on the ground a bit bigger than in round K (x2.4); on an item stand still x2.0
- [ ] Ingredients (`ip_spawn VeilIngredient_T1`, 2, 3) clearly visible on the ground (x8)
- [ ] Plants `Plant_T2`, `Plant_T3` x2.2; lichen `Plant_T1*` x4.5

Wind (assumption, `docs/assets.md`):
- [ ] Huldra lichen strands and Baldr leaves/petals sway a little in the wind like vanilla bushes (stems stay); the fern (`Plant_T3`) does not sway. If nothing moves or the leaves tear away from the stems, note it
- [ ] `ip_matdump Bush01`, `ip_matdump shrub_2`, `ip_matdump Pickable_Thistle` and `ip_matdump clutter grass`: paste the full output (shader names and the `_Ripple*` / wind values) so the next round can copy the real names and values; also `ip_matdump Plant_T2` to see what our foliage carries after Jötunn's shader swap

## Plan 5 – hidden plants and veil goggles (first slice)

Setup: `make build` (Debug, game closed), `make run`. Paste every `plants:`, `lichen:`, `cultivation:`, `goggles:` and `Config migration` line plus the output of the commands below. Wild Baldr's Tear and Hel's Ember Fern only appear in zones generated after this build (explored zones never get new vegetation); test them with `ip_spawn` in `testing` and in a new world.

Config (`[Plants]`, `[Goggles]`, server-synced, admin only; user decisions 2026-10-02):
- `LichenTreeChance = 0.025` (1 in 40 eligible Black Forest firs/pines), `LichenStageMinutes = 120` (S1 → S2 → S3; a pick resets to S1, ripe again after 240 min), `LichenRevealDistance = 40`
- `BaldrZoneChance = 0.167` (1 in 6 new Mountains zones, groups of 1–2 within 4 m), `HelFernZoneChance = 0.0667` (1 in 15 new Ashlands zones, groups of 3–6 within 6 m); read at startup
- `GroundRegrowMinutes = 240`
- `YieldT1 = 1-2`, `YieldT2 = 1-1`, `YieldT3 = 2-4` (min-max, rolled per pick on the ZDO owner); ground plants then scale it: `round(roll × (0.75 + 0.5 × size position in the range))`, at least 1
- Size per ground plant instance: Baldr's Tear 0.8–1.3×, Hel's Ember Fern 0.7–1.6× (wild: `VegetationConfig.ScaleMin/Max`; `ip_spawn`: rolled; stored as `IP_PlantScale`); model variants a/b/c mixed per instance by position (mixed prefabs `IP_BaldrsTear`, `IP_HelsEmberFern`; `_a/_b/_c` are fixed variants for `ip_spawn`)
- `HuldraCultivable = true`; `[Goggles] RecipeT1..3`, `RevealHiddenPlayers = true`; `[TierN] Recipe` gains `VeilIngredient_TN:2`

Startup log:
- [ ] `goggles: material FlametalNew: m_name ...` (and `Flametal`): which one is the Ashlands bar (S2); `assets: goggles VeilGoggles_TN registered (... recipe ... at forge|blackforge)` for all three, no `[Goggles] RecipeTN ... unknown item` error
- [ ] `plants: pick effects from Pickable_Thistle (...)`
- [ ] `plants: lichen S1..S3: mesh bounds (...) (thin axis z), want vertical` – the rotation must be `none`; `plants: IP_Lichen template built`
- [ ] `plants: IP_BaldrsTear_a/b/c`, `IP_BaldrsTear` (mixed), `IP_HelsEmberFern_a/b/c`, `IP_HelsEmberFern` built; `plants: vegetation IP_BaldrsTear: biome Mountain, max 0.167 per zone, group 1-2 r 4 m, ..., scale 0.8-1.3` and `IP_HelsEmberFern: biome AshLands, max 0.067, group 3-6 r 6 m, scale 0.7-1.6`
- [ ] `plants: 21 Plant_* props gated: ...`
- [ ] `plants: tree FirTree (eligible): TreeBase ..., LODGroup ..., children ...; colliders: ...` for each tree (S1: paste them all, also the `inspect only` ones); `lichen: TreeLichen on N tree prefabs: ...`
- [ ] `cultivation: source sapling_carrot: ...`, `cultivation: sapling visuals replaced (...)`, `cultivation: IP_HuldraSapling cloned from ..., piece table _CultivatorPieceTable, added True, enabled True, grow 240 min ...`; `cultivation: S1 flat a: mesh bounds ... want flat` rotation line
- [ ] After loading the world: `plants: 2 of N ZoneSystem vegetation entries are ours: ...`; on the first Black Forest tree `lichen: first tree start: world seed ..., biome ..., server ...` (S3)
- [ ] `Config migration: [TierN] Recipe ... (old default) -> ...,VeilIngredient_TN:2` once for an old config file; a customised recipe gives `[TierN] Recipe '...' is customised and has no VeilIngredient_TN; kept as is`

Goggles:
- [ ] Forge: Watchman's Glass (Bronze 5, Resin 4, Troll hide 2); Mimir's Glass (Silver 5, Crystal 2, Wolf pelt 3, Watchman's Glass); Black forge: Allfather's Eye (Flametal 5, Black core 1, Obsidian 2, Mimir's Glass); II and III consume the previous goggles
- [ ] Equip each: `goggles: level 0 -> N` in the log within 0.5 s; unequip → `-> 0`; `ip_state` shows `goggles: level N (helmet N, override off), zdo IP_Goggles=N`
- [ ] After a relog with goggles worn: `goggles: first tick with a local player: helmet VeilGoggles_TN, level N` (S8)
- [ ] `ip_goggles 2`, `ip_goggles off` fake and restore the level without items

Huldra's Hair (lichen on trees):
- [ ] In a Black Forest without goggles: trees look and hover exactly like vanilla (screenshot pair with/without goggles at the same tree); `ip_plants` lists lichen trees and `lichen trees within 64 m: X of Y eligible trees (~2.5 %)`
- [ ] `ip_plants nearest`: a map pin `IP IP_Lichen` (or tree name) on the nearest plant; the line gives distance and compass direction; `ip_plants unpin` removes it
- [ ] With goggles I (or `ip_goggles 1`): the patch is visible on the trunk within 40 m, flush with the bark (paste the `lichen: FirTree at ...: patch at r=... via ...` lines), hover shows `Huldra's Hair [E] Pick up`; pick → 1–2 Huldra's Hair drop, `plants: harvested $ip_plant_huldrashair T1 ...`, patch drops to S1 and hover shows `(growing)`
- [ ] `ip_grow` on the patch: S1 → S2 → S3 visibly; `ip_grow 3` makes it ripe
- [ ] `ip_lichen roll` / `force` / `off` / `clear` on a looked-at fir: patch appears/disappears at once; the `decision:` text matches
- [ ] Beyond 40 m the patch is not drawn; distant billboard trees show no floating lichen (S5)
- [ ] Restart the world: the same trees carry lichen (compare `ip_plants` positions)
- [ ] Log out to the menu, log back in: `ip_plants` still lists lichen trees; the log has `lichen (OnPrefabsRegistered): TreeLichen on N tree prefabs (...); ZNetScene FirTree has TreeLichen: True`
- [ ] Fell a lichen tree: the lichen goes with it, nothing extra drops

Ground plants:
- [ ] `ip_spawn IP_BaldrsTear_a` (b, c) and `ip_spawn IP_BaldrsTear 5`: invisible without goggles and with goggles I; visible and pickable with II; sizes differ (0.8–1.3×), mixed prefab shows different variants; `ip_plants` shows `scale`, `variant`
- [ ] Pick: 1 Baldr's Tear (bigger plants may give 1 too), picked model stays, hover gone; `ip_grow` or `GroundRegrowMinutes` later ripe again
- [ ] `ip_spawn IP_HelsEmberFern 5`: hidden below goggles III including the ember light; with III visible and pickable, yield 2–4 (scaled by size, up to 5)
- [ ] `ip_spawn Plant_T2` (look-check prop): hidden below goggles II too
- [ ] `plants: vegetation IP_HelsEmberFern: vegetation mask 0-0.5 (keeps ferns off lava)`; no fern stands in lava
- [ ] New world (or unexplored zones): `ip_veg` prints both entries; walk Mountains/Ashlands zones and sample with `ip_plants 200` (S6: about 1 in 6 Mountains zones with 1–2 Baldr's Tear, 1 in 15 Ashlands zones with 3–6 ferns)

Cultivation:
- [ ] With Huldra's Hair in the inventory the Cultivator lists `Huldra's Hair sprout` (Misc) costing 1 (S7: note when it became available)
- [ ] Not placeable on raw ground, placeable on cultivated ground in any biome; the sprout is invisible without goggles; hover with goggles shows the vanilla plant status
- [ ] Garden: hitting a grown Huldra's Hair (goggles on) with any tool removes it, nothing drops; the sapling log line says `fallback: none needed` or `BoxCollider added on the root`
- [ ] `ip_grow` on the sprout: it grows within 10 s into `IP_HuldraGround_a/b/c` (S3 ripe); pick → 1–2, replays S1 → S2 → S3 (`ip_grow`)
- [ ] Under forest canopy: status `no sun`? (S7 risk, note it)

Meads:
- [ ] Every mead base needs 2 of its tier's veil ingredient at the Mead ketill

Two clients (passed 2026-10-03 on the dedicated server): goggles III wearer sees a tier-III-hidden player's nameplate and veiled body at the real position, a non-wearer does not; no map pin for either; a pick by client A updates client B within 5 s; a double pick yields once.

## Plan 5 – round N (station levels, tree sapling, crop sprouts, yaw, wind)

Setup: `make build` (Debug, game closed), `make run`. Paste every `cultivation:`, `lichen: planted`, `wind:` line and `Patch health`.

Startup log:
- [ ] `Patch health: N targets patched, 0 missing` (one more than before: `Player.UpdatePlacementGhost`)
- [ ] `assets: goggles VeilGoggles_T1 registered (... at forge level 1)`, `T2 ... at forge level 3`, `T3 ... at blackforge level 2`
- [ ] `cultivation: IP_HuldraSapling cloned from ... (Plant removed, ...; colliders N: ...), ..., tree sapling: within 1 m of FirTree/Pinetree_01/FirTree_big (hashes ...)`
- [ ] `cultivation: IP_BaldrSapling cloned from ... biome Mountain, grow 240 min to IP_BaldrsTear (scale 0.8-1.3), cost 1 VeilIngredient_T2, VeilSight 2` and the same for `IP_FernSapling` (AshLands, IP_HelsEmberFern, 0.7-1.6, T3, VeilSight 3)
- [ ] After loading the world: `wind:` lines for `Bush01`, `shrub_2`, `Pickable_Thistle`, `IP_BaldrsTear`, `IP_HelsEmberFern` (paste them; our foliage must show `_RippleDistance = 0.3`, `_RippleSpeed = 100`, `_SwayDistance = 0.5`, `_SwaySpeed = 20`, `_Height = 2` on shader `Custom/Vegetation`)

Goggles:
- [ ] Watchman's Glass needs a forge (level 1); Mimir's Glass is only listed at forge level 3 (two forge upgrades); Allfather's Eye only at black forge level 2 (one upgrade)

Huldra's Hair on a tree:
- [ ] Cultivator, `Huldra's Hair sprout`: the ghost is red on open ground and on birches/oaks/beeches, `Needs a fir or pine trunk` on click (centre message, not `Invalid placement`); green within about 1 m of a fir (`FirTree`), a pine (`Pinetree_01`) or a big fir (`FirTree_big`); aiming at the trunk itself works
- [ ] On a tree that already carries lichen (goggles on, or `ip_lichen force` first): red, `Huldra's Hair already grows on this tree`
- [ ] Place it: the sprout vanishes after about 1.5 s, the cost is used, `cultivation: IP_HuldraSapling at ... -> FirTree at ...: plant requested` and `lichen: planted on FirTree at ... : force 1, stage 1 (S1), patch created`; with goggles I the S1 patch sits on the trunk at once; `ip_plants` shows it at stage 1/3, next in 120 min; `ip_grow` → S2 → S3, pick works
- [ ] Works outside the Black Forest too (a pine in the Mountains)
- [ ] Ghost on the trunk (round N follow-up): aiming at a fir/pine trunk, the ghost is the upright bark patch (as on wild lichen trees, not lying flat) stuck to the bark, facing outward, at the aimed height (it stops at about 0.6 m and 2.2 m above the base) and follows around the trunk as you walk around; log once `cultivation: sapling ghost snapped to FirTree ...: aim camera ray on the trunk ... -> h=... m angle ..., ghost at ... (trunk CapsuleCollider '...')`. After placing, the S1 patch appears exactly where the ghost was (`plant requested at h=... angle ...`, `lichen: FirTree ... (planted): patch at ..., h=... angle ..., via CapsuleCollider ... (ClosestPoint)`), also after a relog and on a second client; wild lichen trees keep their default spot (h=1.3)
- [ ] Without goggles the tree looks vanilla
- [ ] Old world with a ground Huldra sprout from the first slice: on load it logs `...: NoTree; cost refunded, sapling removed` and drops 1 Huldra's Hair

Baldr's Tear / Hel's Ember Fern sprouts:
- [ ] With Baldr's Tear in the inventory the Cultivator lists `Baldr's Tear sprout`; outside the Mountains the placement says `$msg_wrongbiome` (wrong biome); on raw Mountains ground `needs cultivated ground`; on cultivated Mountains ground it places; invisible below goggles II
- [ ] Hover with goggles II: vanilla plant status (no `too cold`); `ip_grow` → a grown `IP_BaldrsTear` (random variant and size, `ip_plants` shows scale/variant/yaw), pick works, regrows like a wild one
- [ ] Same for `Hel's Ember Fern sprout` in the Ashlands (goggles III, no `too hot`, ember light on the grown fern)

Yaw:
- [ ] `ip_spawn IP_BaldrsTear_a 5` and `ip_spawn IP_HelsEmberFern 5`: the plants face different directions; `ip_plants` prints `yaw N` per plant, identical after a relog

Wind:
- [ ] Baldr's Tear leaves/petals, the fern fronds and Huldra's Hair strands sway gently in wind (compare with a nearby vanilla bush); no plant flies apart or sinks; stems stay still


## Plan 5 – plants variation round (blossom angles, fern ages, embedded groups)

Preview: `tools/blender/preview-plants-v6.png`. Bundles rebuilt; the log lists `Plant_T2_d/e`, `Plant_T3_d/e` among the plant prefabs.

- [ ] Log: `plants: IP_BaldrsTear built (T2, ..., variants a/b/c/d/e mixed by position ...)`, same for `IP_HelsEmberFern`; `IP_BaldrsTear_a..e` and `IP_HelsEmberFern_a..e` exist (`ip_prefabs IP_BaldrsTear_`)
- [ ] Log: `plants: vegetation IP_BaldrsTear: ... ground offset -0.03 m, ground tilt chance 1` (and the fern); `ip_veg` prints `ground offset -0.03, ground tilt chance 1` for both
- [ ] Goggles II, `ip_spawn IP_BaldrsTear_a`, `_b`, `_c` side by side: the blossom nods steeply (a), half-open at about 30 degrees (b), nearly level on an almost straight stem (c); `_d` has two open blossoms, `_e` a single closed bud with the blue tear hanging from it
- [ ] Goggles III, `ip_spawn IP_HelsEmberFern_d`: small young fern (3 fronds), dark heart, no ember light; `_e`: wide old fern, 8 drooping fronds with grey charred tips, bright heart and a stronger ember light
- [ ] `ip_spawn IP_HelsEmberFern 5` on a slope: the five ferns stand on a loose ring 1.5-4 m round the spot 5 m ahead (not in a row), each tilted with the slope, slightly sunk (no floating edge of the basalt base), different sizes and mixed variants; the console/log line lists per plant `(x,z) tilt N deg scale S`
- [ ] Same with `ip_spawn IP_BaldrsTear 4` on a Mountain slope: snow mounds follow the slope, no mound hangs in the air
- [ ] `ip_spawn Troll 2` and `ip_spawn VeilIngredient_T2 3` still spawn in a row 5 m ahead (ring only for plants)
- [ ] New Mountains / Ashlands zones (explore new ground): wild groups look embedded (tilted with the terrain, bases slightly in the ground) and mix variants a-e; older plants may show another variant than before (5 variants instead of 3, cosmetic)

## Plan 5 – carry weight while invisible

Config `[TierN] CarryWeightMultiplier` (admin, synced, 0..1, defaults T1 0.75, T2 0.6, T3 0.5; 1 = off). Applies to the full limit including Megingjord and the world carry-weight setting.
- [ ] Log: `Patch health: ... targets patched, 0 missing` includes `Player.GetMaxCarryWeight` (`ip_state` `patched:` line)
- [ ] Inventory open, `ip_give 1`: the weight text drops from `x/300` to `x/225` at once; `ip_state` prints `carry: max 225 (multiplier 0.75; ...)`
- [ ] Same with Megingjord worn (450 -> 338 for T1), also when the belt is put on after drinking
- [ ] Carrying more than the reduced limit: vanilla encumbered icon and slow walk; at expiry (or `ip_end`) the limit returns at once and the icon goes
- [ ] T2 reveal (hit an enemy): the penalty stays during the Revealed phase (whole effect)
- [ ] Set `[Tier1] CarryWeightMultiplier = 1`, `ip_reload_config`: the reload line prints `carry T1/T2/T3 1.00/...` and the active T1 penalty is gone without re-drinking

## Plan 5 – tool use reveals

Config `[General] RevealOnToolUse` (admin, synced, default true). Reveal reason in the log: `reveal marked: ToolUse`. T1 ends, T2/T3 reveal and re-hide after `RehideDelay`.
- [ ] Log: patch health includes `Player.PlacePiece`; `ip_state` prints `reveal on: ... toolUse=True`
- [ ] T2: swing an axe at a tree (and into the air) -> `ToolUse` reveal on the swing, re-hide after `RehideDelay`
- [ ] T2: swing a pickaxe at a rock/ore -> `ToolUse` reveal
- [ ] T2: place a hammer piece -> reveal; repair and remove with the hammer -> no reveal
- [ ] T2: hoe (level, pave, raise) -> reveal per placement
- [ ] T2: cultivator (cultivate ground, plant a sapling, also the Huldra sapling on a tree) -> reveal
- [ ] T1: one axe swing ends the effect
- [ ] Picking plants by hand (berries, thistle, our hidden plants) -> no reveal
- [ ] Staff cast still reveals as `StaffCast`; a sword swing that misses does not reveal
- [ ] `[General] RevealOnToolUse = false`, `ip_reload_config` (line ends `reveal on tool use False`): axe, pickaxe, hammer, hoe, cultivator no longer reveal, without re-drinking

## Plan 5 – round P (instant, wide, strong fog volume)

Setup: game closed, `make build`, `make run`, `ip_give 2` in daylight, later `ip_end` and `ip_give 1`. Paste the `Config migration`, `veil fog spawned` and `auto fog dump` lines.

- [ ] `Config migration (look defaults revision 10): N [Fog.Tier1]/[Fog.Tier2] values reset to the new defaults` once, each old value logged before (your ip_fogui save `OuterAlpha 1, OuterSize 6, OuterRate 5` included); tier III keeps its values
- [ ] Config file: `[Fog]` starts with the plain-words overview (body cloud, wide fog, ground fog); the `Outer*`, `Ground*` and body cloud keys have one-line plain descriptions; `[Fog.Tier2] OuterBurst = 45`, `[Fog.Tier1] OuterBurst = 30`
- [ ] Tier II `veil fog spawned T2`, `ip_fog_outer [Outer] Hips` line: `rate 10/s, Volume radius 4 m, height 0.35 x r, ..., Trail, camera-facing, + 4/m, burst 45, alive at spawn 45, size 4 m, alpha 0.7 ..., max 80`
- [ ] Right after `ip_give 2`: the wide fog is there at once (no build-up over seconds), visible by day, about 4 m around the player
- [ ] Over the first 10 s the field does not blink out at once (the burst puffs fade at different times while the rate keeps it filled)
- [ ] Walking and running: the fog follows the player immediately (puffs per metre), older puffs stay behind and fade
- [ ] `ip_fog outeralpha 0.5` (look change rebuild): the fog is rebuilt full at once, no pop-in; `ip_fog` prints `burst 45 (45 emitted)`
- [ ] Tier I (`ip_end`, `ip_give 1`): the same, lighter (`alpha 0.5`, `radius 3.5 m`, `burst 30`), tier II visibly stronger
- [ ] Tuning window (`ip_fogui`, "Wide fog" section): new row `Burst at start (volume)`; radius and size sliders reach 6 m
- [ ] Night: the fog stays grey and matte (no glow)

## Plan 5 – round Q (fog follows you, stamina drain, Veil Broken, Veil Cooldown)

Setup: game closed, `make build`, `make run`. Spawn real meads to drink: `ip_spawn MeadInvisibility_T1 3`, `ip_spawn MeadInvisibility_T2 3`, `ip_spawn MeadInvisibility_T3 2` and pick them up (`ip_give` bypasses the drink check but also starts the cooldown). Paste the `Config migration`, `cooldown reference`, `veil fog spawned`, `auto fog dump` and `ip_state` lines.

Config and log
- [ ] `Config migration (look defaults revision 11): ...` once (tiers I/II reset, old values logged); `[Fog.Tier1/2] OuterFollowShare = 0.4` with the description "Share of the wide fog that moves with you; the rest stays behind as a trail", `OuterBurst` T1 40, T2 60
- [ ] `Config migration (gameplay defaults revision 2): [TierN] DebuffStaminaRegenMultiplier 0.5 -> 0.25, DebuffDuration 20 -> 15, Cooldown 0 -> 30/60/90 (revision 3; files at the revision-2 values 90/180/240 move too)` once (values you changed yourself stay); `[General] DrainStaminaOnAttackReveal = true`
- [ ] `cooldown reference: MeadHealthMinor -> ... category '...', ttl ..., cooldownIcon ..., healthOverTimeDuration ...` (the vanilla data the Veil Cooldown mirrors; paste it)

Wide fog (tier II, then tier I)
- [ ] `veil fog spawned T2`: two outer lines, `ip_fog_outer_follow [Outer] Hips follow: rate 4/s, Volume radius 2.8 m, ..., space Local, ..., burst 24, alive at spawn 24, max 48` and `ip_fog_outer [Outer] Hips trail: rate 6/s, radius 4 m, space World, + 4/m, burst 36, alive at spawn 36, max 72`; fade-in 0.08
- [ ] Right after drinking: the wide fog is at full strength in the first frame (no fade-in over ~2 s), around you at once
- [ ] Walking and sprinting: you never run out of the fog (the follow part stays around you), and a trail of fog stays behind and fades
- [ ] Auto fog dump: both systems listed with live particles (follow `space Local`, trail `space World`)
- [ ] `ip_fog outerfollowshare 0` (or the config key): one trail system as in round P; `1`: only the follow system, no trail
- [ ] Tier I: the same, lighter (burst 16 + 24)

Stamina drain and Veil Broken (tier II)
- [ ] Hidden, hit a creature: stamina bar drops to 0 at once; status bar shows "Veil Broken" (red cracked veil icon) with a 15 s countdown; stamina regenerates at about a quarter of the normal speed; log `veil broken by an own action: stamina emptied`
- [ ] While still revealed, hit again: the debuff restarts at 15 s but stamina is not emptied again
- [ ] After re-hiding: bow draw, staff cast, axe/pickaxe swing and hammer placement each empty the stamina once
- [ ] Revealed by taking damage or by blocking a hit: Veil Broken appears, stamina is not emptied
- [ ] Tier I: one hit ends the veil, empties stamina and shows Veil Broken
- [ ] `[General] DrainStaminaOnAttackReveal = false`, `ip_reload_config`: own attacks no longer empty stamina (debuff still applies)
- [ ] Hover the Veil Broken icon in the inventory status list: name "Veil Broken", tooltip "Your veil was broken. Stamina regenerates slowly."

Veil Cooldown
- [ ] Drink tier II: "Veil Cooldown" appears in the status bar with the cooldown overlay and a 3:00 countdown (the clock icon), next to "Deep Veil"
- [ ] During the cooldown, try tier I, II and III meads: each is refused with the vanilla "can't consume" message and stays in the inventory
- [ ] `ip_state`: `cooldown: 1xx.xs of 180s left (config T1 30, T2 60, T3 90); veil broken: ...; stamina .../...`
- [ ] After the cooldown runs out (shorten it: `[Tier2] Cooldown = 20`, `ip_reload_config`, drink again after the old one ends) any mead can be drunk again; an upgrade T1 -> T3 after the T1 cooldown works
- [ ] `[TierN] Cooldown = 0`: no cooldown effect, drinking is only limited by the tier rules as before
- [ ] Relog: the cooldown is gone (status effects are not saved, as for vanilla potions)

## Plan 5 – round R (even wide fog that keeps world rotation, Veil Broken until the veil returns, harsher debuff)

Setup: game closed, `make build`, `make run`. Real meads as in round Q (`ip_spawn MeadInvisibility_T2 3` etc.). Paste the `Config migration`, `veil fog spawned`, `auto fog dump` and `ip_state` lines.

Config and log
- [ ] `Config migration (look defaults revision 12): ...` once (tiers I/II reset, old values logged); `[Fog.Tier1] OuterBurst = 50`, `[Fog.Tier2] OuterBurst = 70`; `OuterFollowShare` description mentions "drift slowly around you; 60 % of OuterBurst starts there"
- [ ] `Config migration (gameplay defaults revision 4): [TierN] DebuffStaminaRegenMultiplier 0.25 -> 0.15` once (a value you changed stays); new keys `[TierN] DebuffEitrRegenMultiplier = 0.25`, `DebuffHealthRegenMultiplier = 0.5`, `DebuffSpeedModifier = -0.2` (T1) / `-0.3` (T2, T3); `DebuffDuration` description says it applies to tier I and re-hiding tiers last until the veil returns

Wide fog (tier II, then tier I)
- [ ] `veil fog spawned T2`: `ip_fog_outer_follow ... follow: rate 4/s, Volume radius 2.8 m, ..., space Local, ..., burst 42 (follow share 0.4, follow burst fraction 0.6, ...), follower position only (world rotation), alive at spawn 42, size 3.4 m, ..., max 56`; `ip_fog_outer ... trail: ..., burst 28, follower position only (world rotation), ..., max 84`
- [ ] Auto fog dump: both outer lines show `follower position only (world rotation), emitter rotation (0.0, 0.0, 0.0)`; the follow line `orbital 0.08 rad/s (4.7 deg/s, velocity space Local)` (tier II)
- [ ] Standing and turning in small steps (mouse left/right, A/D strafing back and forth): the cloud around you stays put as one continuous cloud, no spotlight-like swinging, no holes on one side
- [ ] Right after drinking: the instant cloud is evenly spread around you (no clusters, no empty quadrant)
- [ ] Standing still for 10 s: the follow puffs drift slowly around you (the cloud does not look frozen), slowly enough not to read as a vortex
- [ ] Walking and sprinting: you stay inside fog, the trail still stays behind and fades
- [ ] Tier I: the same, lighter (burst 30 follow + 20 trail)

Veil Broken (tier II, then tier III, then tier I)
- [ ] Tier II, hit a creature: "Veil Broken" shows a 12 s countdown (RehideDelay), not 15 s; it disappears exactly when the veil returns (the fog body is back, `ip_state` phase Hidden)
- [ ] While revealed, hit again: the countdown restarts at 12 s together with the re-hide timer, and both still end together
- [ ] Tier III: the countdown is 8 s and ends with the re-hide
- [ ] Tier I: one hit ends the veil, Veil Broken runs 15 s (DebuffDuration)
- [ ] While Veil Broken runs: visibly slower walking and running (tier II/III 30 %, tier I 20 %), stamina regenerates very slowly (15 %), health regeneration halved, eitr regenerates at a quarter (staff build)
- [ ] Inventory, "Active effects" page (Texts dialog): Veil Broken lists the tooltip "Your veil was broken: you move slower ..." plus `Health regen -50%`, `Stamina regen -85%`, `Eitr regen -75%`, `Movement -30%` (vanilla wording)
- [ ] `ip_state`: `veil broken: 9.3s of 12s left, stamina regen x0.15, eitr regen x0.25, health regen x0.50, speed -0.30` and three `veil broken config T1/T2/T3: 15s / 12s / 8s ...` lines
- [ ] `[Tier2] DebuffSpeedModifier = -2` (out of range), restart: BepInEx clamps it to -0.9; `DebuffDuration = 0`, `ip_reload_config`: no Veil Broken at all on tier II
- [ ] Status bar: the three tier effects still show their own names and icons (Faint Veil, Deep Veil, Shadow Veil)

## Plan 5 – round S (ground-heavy fog pyramid, lighter tier II, harsher Veil Broken, no sprinting)

Setup: game closed, `make build`, `make run`. Real meads as in round Q (`ip_spawn MeadInvisibility_T2 3` etc.). Paste the `Patch health`, `Config migration`, `veil fog spawned`, `auto fog dump` and `ip_state` lines.

Config and log
- [ ] `Patch health: ... targets patched, 0 missing` (the list now includes `Player.CheckRun`)
- [ ] `Config migration (look defaults revision 13): ...` once (tiers I/II reset); `[Fog.Tier2] OuterAlpha = 0.45`, `OuterRadius = 4.5`, `OuterSize = 3.6`, `OuterBurst = 60`, `Alpha = 0.14`; `[Fog.Tier1] OuterAlpha = 0.35`, `Alpha = 0.12`; new key `OuterHeightSigma = 0.9` in both, its description in plain words ("how high it rises above the ground")
- [ ] `Config migration (gameplay defaults revision 5): ...` once: `DebuffStaminaRegenMultiplier 0.15 -> 0.1`, `DebuffEitrRegenMultiplier 0.25 -> 0.15`, `DebuffHealthRegenMultiplier 0.5 -> 0.35`, `[Tier1] DebuffSpeedModifier -0.2 -> -0.35`, `[Tier2]/[Tier3] -0.3 -> -0.5`, `[Tier1] DebuffDuration 15 -> 20` (values you changed stay)

Wide fog shape (tier II, then tier I)
- [ ] `veil fog spawned T2`: both outer lines read `rate 4/s + 0/m (local)` (follow) and `rate 6/s + 4/m (world)` (trail), `ground spread 3.15 m` / `4.5 m with density (1 - r/R)^2 per m2, height half-Gaussian sigma 0.9 m (floor 0.1 m, cap 3 sigma) ...`, burst 36 (follow) and 24 (trail)
- [ ] Auto fog dump: each outer line ends `explicit emission: ..., emitted N so far`; the live line shows `avg height above the player` well below 1 m and `... % below sigma (half-Gaussian: 68 %)` near 60..75 %; `rate 0/s + 0/m` on the Unity emission is expected (the volume emits by itself)
- [ ] Look from the side (third person, zoomed out): the fog is a low, wide mound, densest at your feet, thinning upwards and outwards; no ball around the hips
- [ ] Tier II: your character is clearly visible through the fog (silhouette, armour colours); the fog is a hint, not a cover
- [ ] Walking and sprinting (before any reveal): fog keeps up, the trail lies low along the path and fades
- [ ] Standing still 10 s: the follow puffs still drift slowly; nothing builds up into a dense wall
- [ ] `ip_fog t2 outersigma 0.4`: a flat ground fog; `ip_fog t2 outersigma 2`: a tall cloud; `ip_fogui` shows the row "Height sigma m (ground-heavy)" for the Volume shape
- [ ] Tier I: the same shape, lighter

Veil Broken (tier II, then tier I)
- [ ] Tier II, hit a creature: Veil Broken runs until the veil returns (12 s); walking is at half speed; holding sprint does not sprint (no run animation, no stamina drain from running)
- [ ] Stamina regenerates at 10 %, health at 35 %, eitr at 15 % (staff build)
- [ ] Inventory, "Active effects": tooltip "... you move much slower, cannot sprint ...", plus `Health regen -65%`, `Stamina regen -90%`, `Eitr regen -85%`, `Movement -50%`
- [ ] `ip_state`: `veil broken: ... stamina regen x0.10, eitr regen x0.15, health regen x0.35, speed -0.50`
- [ ] Tier I: one hit ends the veil, Veil Broken runs 20 s, movement 35 % slower, no sprinting
- [ ] When Veil Broken ends (veil back, or tier I after 20 s): sprinting works again at once

## Plan 5 – round T (much wider, vanilla-like wide fog)

Setup: game closed, `make build`, `make run`. Real meads as in round Q (`ip_spawn MeadInvisibility_T2 3` etc.). If possible, compare with a real Black Forest or Swamp mist patch nearby. Paste the `Config migration`, `veil fog spawned` and `auto fog dump` lines.

Config and log
- [ ] `Config migration (look defaults revision 14): ...` once (tiers I/II reset); `[Fog.Tier1] OuterRadius = 7`, `OuterHeightSigma = 0.8`, `OuterSize = 4.5`, `OuterLifetime = 12`, `OuterBurst = 60`, `OuterAlpha = 0.3`; `[Fog.Tier2] OuterRadius = 9`, `OuterHeightSigma = 1`, `OuterSize = 5.5`, `OuterLifetime = 14`, `OuterBurst = 80`, `OuterAlpha = 0.4`; the descriptions of these keys name the tier defaults and say the fog "fades out softly" at the edge
- [ ] `veil fog spawned T2`: follow line `rate 4/s + 0/m (local), ground spread 6.3 m with Gaussian density per m2 (sigma_r 2.835 m, cut at R)`, trail line `rate 6/s + 4/m (world), ground spread 9 m ... (sigma_r 4.05 m ...)`; burst 48 (follow) and 32 (trail); max 80 and 120; `fade in 0.15 / out 0.35, grow x1.35, spin +-4 deg/s`; `drift 0.05 m/s horizontal`; `overlap cap: ~N puffs over the player, ..., centre alpha xF ..., column opacity <= 0.5` on both lines with N about 46 and F about 0.27
- [ ] `veil fog spawned T1`: ground spread 4.9 m (follow) / 7 m (trail), burst 36 / 24, `overlap cap` with N about 41 and F about 0.4
- [ ] Auto fog dump (outer lines): `avg ground distance from the player` around 3 m (T1) / 4 m (T2); `avg vertex alpha (x material ... = X)` with X roughly 0.08..0.14 (OuterAlpha x the capped Gaussian factor x the life curve; T1 about 0.10, T2 about 0.11); `avg size` 3.5..6 m; `% below sigma` near 60..75 %

Look (tier II, then tier I; third person, zoomed out, by day and at dusk)
- [ ] The fog spreads far: clearly beyond the player, up to 7 m (T1) / 9 m (T2), low over the ground
- [ ] The edge has no line or ring: the fog gets thinner and fainter and dissolves into the ground
- [ ] The centre is not a white/grey blob: your character stays clearly visible (silhouette, armour colours); tier II a little stronger than tier I in the outer part
- [ ] The puffs read like vanilla mist: large, very soft, low contrast; they drift very slowly sideways, grow a little and turn very slowly; no visible popping in or out (slow fade in and fade out)
- [ ] Standing still 20 s: the patch stays even, no dense wall builds up; walking/sprinting: the trail lies low along the path and fades slowly
- [ ] Frame rate inside the fog is fine (up to 200 puffs per tier, large sprites close to the camera)
- [ ] `ip_fogui`: the wide-fog rows Radius (up to 14 m), Size (up to 9 m), Lifetime (up to 20 s) and Burst (up to 150) reach the new values

## Plan 5 – dry-land plants, zone chances, item stands (2026-10-03)

Setup: game closed, `make build`, `make run`. Wild plants need newly generated zones (a new world or unexplored land).

Config and log
- [ ] `Config migration (plant defaults revision 1): [Plants] BaldrZoneChance 0.167 -> 0.2, [Plants] HelFernZoneChance 0.0667 -> 0.1` once (only for values still at the old default; a customised value stays); `[General] PlantDefaultsRevision = 1`
- [ ] `plants: vegetation ...` lines: `max 0.2 per zone` (Baldr's Tear) / `max 0.1 per zone` (fern), `altitude 0.5..1000 m above sea, block True`; fern: `lava mask 0-0.15`
- [ ] `ip_veg`: both entries show `max 0.2/zone` / `max 0.1/zone`, `altitude 0.5..1000 m above sea`, `ocean depth 0-0 (equal = off)`, `block True`; fern `vegetation mask 0-0.15`

In the world
- [ ] Ashlands: no Hel's Ember Fern in the sea, on the shore line or on/at the edge of lava; groups on solid ash ground only
- [ ] Mountains: no Baldr's Tear in water
- [ ] Ferns noticeably more common than before (about 1 Ashlands zone in 10), Baldr's Tear about 1 Mountains zone in 5

Item stands (vanilla behaviour, see decompile notes)
- [ ] Horizontal item stand: a veil mead (hotbar key while looking at the stand) is placed, like a vanilla `MeadHealthMinor`
- [ ] Wall item stand: a veil mead is refused ("can't attach"), exactly like a vanilla mead
- [ ] Mead bases are refused by both stands, like vanilla mead bases

## Jötunn 2.30.0 (2026-10-03)

Setup: replace `BepInEx/plugins/Jotunn/Jotunn.dll` with the one from `ValheimModding-Jotunn-2.30.0` (Thunderstore) so the
client matches the dedicated server; `make build`, `make run`.

- [ ] Log: `Loading [Jotunn 2.30.0]`, `Patch health: N targets patched, 0 missing`, no `[Error  :InvisibilityPotion]` lines
- [ ] Cultivator: Huldra's Hair, Baldr's Tear and Hel's Ember Fern sprouts appear in the same build menu tag as the vanilla crop
  saplings, and selecting each one places that sprout (2.30.0 had a "wrong piece placed" bug, fixed in 2.30.1)
- [ ] Join the dedicated server (Jötunn 2.30.0) with this client: accepted. Optional: with Jötunn 2.30.2 on the client the join is
  refused with Jötunn's version mismatch window (`docs/compatibility.md` §7)

## 0.3.1 – misplaced wild plants removed (2026-10-03)

Setup: an Ashlands/Mountains area generated before the dry-land rules (e.g. the Hel's Ember Fern in the Ashlands sea from the
screenshot); one cultivated Baldr's Tear or fern nearby; `make build`, `make run`.

- [ ] Log: `Patch health: N targets patched, 0 missing` (now including `Plant.Grow`); `[Plants] RemoveMisplacedWildPlants = true` in the config
- [ ] Before walking up to the old zone, `ip_plants` near it lists the plant with `misplaced (below sea level)` or `misplaced (on lava)`
  (with the switch set to false first, so it is still there), and the summary line `misplaced wild plants in range: N`
- [ ] With the switch on (default): within a few seconds of the zone loading the fern in the sea is gone; log `plants: removed IP_... at (x,y,z): below sea level` (or `on lava`) once per reason; `ip_plants` shows `removed by this peer since start: N`
- [ ] Wild plants on dry ash ground / dry mountain ground stay; no `misplaced` in `ip_plants`
- [ ] A plant grown from a sapling after this update shows `cultivated,` in `ip_plants` and is never removed; an older cultivated plant on cultivated ground stays too

## 0.4.0 – wild plants for old zones, seed-rate chances (2026-10-03)

Setup: `make build` (game closed), `make run` into `testing` (an old world whose Mountains/Ashlands zones were generated before 0.4.0 or before the plants existed). Paste `Patch health`, every `Config migration (plant defaults revision 2)` line and every `plants: retrofit zone` line.

Result 2026-10-04 (world `testing`, host, Ashlands): migration 0.2/0.1 -> 0.5 logged, 25 targets patched, 55 zones checked, 10 ferns placed in 4 zones, `ip_retrofit here` in a zone with ferns: `HasPlants`; no misplaced removals, no exceptions. User confirmed the ferns in game.
- [ ] Log: `Patch health: 25 targets patched, 0 missing`; `Config migration (plant defaults revision 2): [Plants] BaldrZoneChance 0.2 -> 0.5, [Plants] HelFernZoneChance 0.1 -> 0.5` on a file that still had the old defaults (a customised value is listed as unchanged)
- [ ] `ip_retrofit` prints `enabled True, available True, server True` and the current zone's state
- [ ] Walk through old Mountains zones with goggles II: `plants: retrofit zone (x,y): N placed` lines appear, Baldr's Tear groups (1-2) stand on dry ground; `ip_plants` finds them; every second zone roughly
- [ ] Walk through old Ashlands zones with goggles III: fern groups (3-6) on solid ground, none on lava or in water
- [ ] Leave and come back (`ip_retrofit reset`, walk again): no second group appears in a zone that already has one (`HasPlants` in `ip_retrofit here`), a zone that rolled `NoRoll` stays empty
- [ ] `ip_retrofit here` in a zone generated with the mod that has no plant: `NoRoll` (same seed as the original generation) or `HasPlants`
- [ ] Config `RetrofitExistingZones = false` (`ip_reload_config`): `ip_retrofit` shows `enabled False`, no new `retrofit zone` lines while walking
- [ ] Dedicated server (CT 132, world modtest): the same lines in `journalctl -u valheim`, plants visible to a client with goggles after walking into old zones

## 0.3.1 – veil meads and bases on the Serving Tray (2026-10-03)

Setup: a Serving Tray, one of each veil mead (`ip_spawn MeadInvisibility_T1` .. `T3`) and base (`MeadBaseInvisibility_T1` .. `T3`)
and one vanilla mead in the inventory; `make build`, `make run`.

- [ ] Log at startup: six `tray: MeadInvisibility_T1/2/3, MeadBaseInvisibility_T1/2/3 prepared as piece ...` lines (category `Meads`,
  `resources <own item> x1`, template requirement shown), and after joining six `tray: registered ... in _FeasterPieceTable` lines;
  no `tray:` warning. A second world load in the same session logs `tray: 6 piece(s) already listed`
- [ ] `ip_tray` (and `ip_tray all` once): paste the log. It settles the tray item name, the vanilla mead entries' components,
  requirement (`MeadHealthMinor x1 recover ...`), `m_usage` and whether vanilla bases are listed (expected: not)
- [ ] Serving Tray "Mead" tab: our three meads and three bases appear next to the vanilla meads, with their icons and names
- [ ] Place each one: our bottle/bowl stands on the table at the same size as a dropped one (no vanilla health mead mesh),
  the cork wisp rises on the meads; one item leaves the inventory per placement. Placing reveals you while veiled (tool use, as for the hammer)
- [ ] Hover a placed veil mead: `[E] Drink`; drinking it starts the veil (and the Veil Cooldown), the bottle disappears, nothing drops;
  during the cooldown it is refused with `$msg_cantconsume` and stays on the table
- [ ] Hover a placed base: `[E] Pick up`; picking up returns the base. Alt-use on a placed mead picks it up instead of drinking
- [ ] Removing a placed mead/base with the tray (right click) drops exactly that item; the hammer cannot remove it
- [ ] Log out and back in: placed meads and bases are still on the table (no rigidbody, they do not fall or roll)
