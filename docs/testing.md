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

> Historical round record. Current defaults: body mode Distortion for all tiers (the `LookDefaultsRevision` migration moves an old T1 Off to Distortion); fog look per tier in `[Fog.TierN]`. The per-tier text below describes the state when this round ran.

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

> Historical round record. Current defaults: body mode Distortion for all tiers (the `LookDefaultsRevision` migration moves an old T1 Off to Distortion); see Round G for the current fog defaults. The defaults below are those of round E.

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

## Plan 2 – Round G: tier I/II looks, outer disc, fog brightness, draggable sliders (task 10g)

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
