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
