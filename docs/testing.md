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

Two clients (pending, needs plan 3 server):
- [ ] Second client: no nameplate over the hidden player
- [ ] Second client: no map pin for the hidden player (even with "share position" on)
- [ ] Second client: the hidden player is not rendered near their real position while hidden
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

Two clients (pending, plan 3 server): goggles III wearer sees a tier-III-hidden player's nameplate and veiled body at the real position, a non-wearer does not; no map pin for either; a pick by client A updates client B within 5 s; a double pick yields once.
