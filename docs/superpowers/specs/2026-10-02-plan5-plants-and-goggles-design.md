# Plan 5 – Hidden plants and veil goggles

Status: draft for review · 2026-10-02 · amends the main spec (`2026-09-30-invisibility-potion-design.md`) §5.2 Config, §5.7 Hidden from players, §5.9 Items, §5.10 Dev tooling. Builds on plan 4 (bundle prefabs `Plant_*`, items `VeilIngredient_T1..3`, `VeilGoggles_T1..3`).

Mechanics authority: `docs/ideas/2026-10-02-plan5-mechanics-research.md` (cited as "R §n"). `File.cs:line` means `tools/decompiled/assembly_valheim/File.cs` (1.0.16). `(?)` marks an unverified game fact; each one names the spike (S-n, §7 task 1) that settles it.

## 1. Goal

Make the veil meads a progression: each tier's mead base needs an ingredient from a plant that only a wearer of veil goggles of that tier (or higher) can see and pick. Three plants (Black Forest lichen on trees, Mountains flower, Ashlands fern), three craftable goggles, and a level-III goggle exception that lets the wearer see tier-III-hidden players. At the end of plan 5 a fresh character can go goggles I → lichen → tier I mead → goggles II → … → tier III, in an existing world for tier I and in newly explored zones for tiers II/III.

## 2. Decisions

| Topic | Decision | Why |
|---|---|---|
| Plant 1 | **Huldra's Hair** (`VeilIngredient_T1`), Black Forest, grows on vanilla fir/pine trunks as a flush bark patch (`Plant_T1_S1..S3` meshes, no change to the tree's own meshes or colliders). ~1 in 40 eligible trees. Stages S1 sprout → S2 → S3 harvestable, 120 min per stage (S1 → S3 = 240 min, the user's regrowth time); picking resets to S1. Hidden and non-interactable without goggles ≥ I; the tree looks like any other | User decision |
| Plant 1, cultivated | Also plantable on cultivated ground with the Cultivator, cost 1 `VeilIngredient_T1`, `Plant_T1_Flat[_a/_b/_c]` meshes, same stages, no biome restriction | User decision |
| Plant 2 | **Baldr's Tear** (`VeilIngredient_T2`), Mountains, ground vegetation, ~1 in 6 zones, 1–2 plants per zone, variants a/b/c, picked model `Plant_T2_picked`, regrowth 240 min, hidden without goggles ≥ II | User decision |
| Plant 3 | **Hel's Ember Fern** (`VeilIngredient_T3`), Ashlands, as plant 2 with `Plant_T3[_a/_b/_c]`, `Plant_T3_picked`, goggles ≥ III | User decision |
| Harvest component | **Own component `VeilHarvest`** for all three plants, not vanilla `Pickable` | See §3.3 |
| Lichen rarity seed | `hash(world seed, quantized tree position, salt)`, not `ZDOID` | ZDOIDs are reassigned on every load (ZDO.cs:1298, R §2.4) |
| Goggles | Three helmet items `VeilGoggles_T1..3`, armour 2/4/6 (unchanged), no other effect, no equip status effect. Level N reveals plants ≤ N; level III also reveals tier-III-hidden players (nameplate, body, real position), no map pin | User decision; no SE = no HUD icon and no effect besides sight |
| Goggle recipes | I: `forge` — `Bronze:5,Resin:4,TrollHide:2`. II: `forge` — `Silver:5,Crystal:2,WolfPelt:3,VeilGoggles_T1:1`. III: `blackforge` — `FlametalNew:5,BlackCore:1,Obsidian:2,VeilGoggles_T2:1`. Station level 1 | User decision. `FlametalNew` (?): the SoftRef manifest lists both `Flametal` (legacy) and `FlametalNew`; S2 confirms which is the Ashlands bar (`ip_prefabs flametal` + its `m_name`) |
| Goggle level source | Poll `Player.m_localPlayer.m_helmetItem` every 0.5 s (R §5) | Local truth, no patch target; SE would add a HUD icon the user does not want |
| Mead recipes | Tier N base recipe additionally needs `VeilIngredient_TN:2`, via the existing config strings (new defaults + migration of untouched old defaults) | User decision |
| New patch targets | **None.** The level-III exceptions extend the existing `EnemyHud.TestShow` postfix and `ZDOMan.SendZDOs` helper | Fewer hooks to break on game updates |

## 3. Components

New files: `Plants/VeilSight.cs`, `Plants/VeilHarvest.cs`, `Plants/HarvestStage.Core.cs`, `Plants/TreeLichen.cs`, `Plants/LichenRoll.Core.cs`, `Plants/PlantPrefabs.cs` (builds the runtime prefabs), `Plants/PlantVegetation.cs`, `Goggles/GogglesLevel.cs`, `Goggles/GoggleLevel.Core.cs`, `Net/PlayerReveal.Core.cs`, `Dev/PlantCommands.cs`. Changed: `Items/GoggleItems.cs` (recipes), `Items/PotionItems.cs` (defaults), `Config/PluginConfig.cs`, `Patches/PlayerHidePatches.cs`, `Visuals/VeilController.cs`, `Items/English.json`, `Plugin.cs` (registration order).

### 3.1 `VeilSight` (per-plant viewer gate and model swap)

- One component per plant instance, on the plant root (ground plants) or on the lichen child (trees). Fields: `RequiredLevel` (1..3), `MaxDistance` (0 = none; lichen uses 40 m, see LOD below), `StageModels[]` (S1, S2, S3 or ripe/picked children), `ActiveStage`.
- `Awake` caches `GetComponentsInChildren<Renderer|Collider|Light|ParticleSystem|AudioSource>(true)` per stage model. Registers itself in a static set; `OnDestroy` unregisters.
- A single driver `VeilSightDriver` (MonoBehaviour on the plugin object, like `VeilController`) ticks every 0.5 s: reads `GogglesLevel.Local` and the local player position, then for every registered `VeilSight` computes `revealed = GoggleLevel.Reveals(level, RequiredLevel) && withinDistance` and applies only on change of `(revealed, ActiveStage)`.
- Apply: for the active stage model `Renderer.enabled`, `Collider.enabled`, `Light.enabled`, particle `Play()`/`Stop(true, StopEmittingAndClear)`, audio `Play/Stop` = `revealed`; all other stage models fully off. Never `SetActive` (R §3: hover raycasts ignore disabled colliders, Player.cs:4259-4300; `enabled` flags survive SetActive cycles). Hidden means: no draw, no shadow, no hover, no interaction, no ray blocking (R §3 (a)/(b)).
- Stage swap: `VeilHarvest` sets `ActiveStage`; the driver applies it on its next tick (≤ 0.5 s).
- Cost: one pass over registered plants every 0.5 s; plants in loaded zones only. Expected tens to low hundreds of entries (most trees lose the roll and destroy their child, §3.2).
- Tree LOD (?): a child renderer outside the tree's `LODGroup` (TreeBase.cs:81-85) may draw at every distance while the tree is a billboard. Default design: distance gate `MaxDistance = 40 m` on the lichen. S5 decides whether adding the lichen renderers to LOD0 is needed instead.

### 3.2 `TreeLichen` (lichen on vanilla trees)

- **Prefabs**: in `PrefabManager.OnVanillaPrefabsAvailable`, add a child `IP_Lichen` (lichen meshes S1/S2/S3 + one small collider + `VeilSight(1)` + `VeilHarvest` + `TreeLichen`) to the eligible vanilla tree prefabs. In-memory change: every later `ZNetScene.CreateObject` (ZNetScene.cs:92-116) and `ZoneSystem.PlaceVegetation` (ZoneSystem.cs:1551) instantiation carries it, in existing worlds too (R §2.2). Eligible list (?): `FirTree`, `Pinetree_01`, possibly `FirTree_big`, `FirTree_small`; never `*_dead`, `*_log*`, `*_Stub`, `FirTree_snowfall`. S1 settles names, which use `TreeBase` vs `Destructible`, trunk collider layers, LODGroup, and a per-prefab trunk offset (local position/rotation/height of the patch). Parent under the root, not `m_trunk` (`GrowAnimation` clones the trunk, TreeBase.cs:77-99; the ±1.5° shake is invisible on a flush patch, R §3).
- **No nested ZNetView** (ZNetView.cs:51-100 creates a leaking ZDO per instantiation, R §2.3). `TreeLichen` uses `GetComponentInParent<ZNetView>()` and the tree's ZDO, the vanilla pattern of `MaterialVariation`/`SpawnPrefab` (R §2.3).
- **Roll in `Start`**, never in `Awake`: ghost-spawned trees are destroyed before `Start` (ZoneSystem.cs:1547-1566). Order: (1) tree ZDO key `IP_LichenForce` (int; 1 = forced on, -1 = forced off, written only by `ip_lichen`) overrides; (2) biome gate: `WorldGenerator.instance.GetBiome(position)` (WorldGenerator.cs:746) must be `BlackForest` (firs also grow in Meadows/Mountains); (3) `LichenRoll.Has(seed, x, z, chance)` with `seed = WorldGenerator.instance.GetSeed()` (WorldGenerator.cs:1445; available on clients (?) — S3). A failed roll destroys the child in the same frame, so the tree is identical to vanilla at runtime.
- **`LichenRoll.Core.cs`**: quantize `qx = RoundToInt(x*10)`, `qz = RoundToInt(z*10)` (position from the tree ZDO, identical on all peers and across restarts), splitmix64 over `(seed, qx, qz, salt "IP_Lichen")`, map to [0,1), compare with `LichenTreeChance` (0.025). No `string.GetHashCode`, no `UnityEngine.Random`. Player-planted firs also roll; accepted (cultivation exists anyway).
- **State** in the tree ZDO, written only on change, so the many unpicked trees stay untouched (R §2.4): `IP_LichenStage` (int, stage at `IP_LichenTime`), `IP_LichenTime` (long, `ZNet.instance.GetTime().Ticks`, ZNet.cs:2802). No keys = S3, ripe. Current stage = `HarvestStage.At(baseStage, elapsedMinutes, stageMinutes)` evaluated on every client (§3.3); no growth timer or RPC.
- **Hover/interact**: `VeilHarvest` implements `Hoverable`/`Interactable` on the child; the Hud and `Player.Interact` resolve them with `GetComponentInParent` from the hit collider (Hud.cs:829-831, Player.cs:4306-4309), so the lichen collider is hoverable and the trunk stays as vanilla. Lichen collider layer and trigger-vs-solid (?): S4 (must be in `m_interactMask`, Player.cs:671, without blocking building or camera).
- **Felled tree**: `TreeBase.RPC_Damage` deactivates and destroys the whole hierarchy (TreeBase.cs:165-166): the lichen goes with it, no drop. Logs and stumps are other prefabs and get no lichen. An axe hit on the revealed lichen collider damages the tree (`IDestructible` found in the parent), as hitting the trunk does.
- **Existing worlds**: works at once, because trees are recreated from the modified prefab. Clients without the mod are refused by `NetworkCompatibility` anyway.

### 3.3 `VeilHarvest` and the ground plants

**Decision: own component, not vanilla `Pickable` + `m_hideWhenPicked`.** Reasons from R §1.1/§3: (1) `Pickable` reads `GetComponent<ZNetView>()` on its own object (Pickable.cs:80) and throws when nested, so the lichen needs an own harvest component anyway; (2) `Pickable` has two states and no picked-model or stage replay (Pickable.cs:269-292); (3) it drives `m_hideWhenPicked.SetActive` in four places (Pickable.cs:98, 151, 274, 314), a second writer next to `VeilSight`; (4) auto-pick and "pick all" mods enumerate `Pickable` and would harvest invisible plants. Cost: hover text, pick RPC and drop are ours (≈ 150 lines, one implementation for all three plants).

- `VeilHarvest` fields: `ItemPrefab`, `Yield` (config), `RequiredLevel`, `Stages` (3 for Huldra; 2 for ground plants: 1 = picked model, 2 = ripe), `StageMinutes`, ZDO key names (`IP_LichenStage/IP_LichenTime` on trees, `IP_PlantStage/IP_PlantTime` on ground plants' own ZDO), `PickEffects` (`EffectList` copied from a vanilla pickable's `m_pickEffector`, e.g. `Pickable_Thistle` (?) — S1 lists the names).
- `Start`: `m_nview = GetComponentInParent<ZNetView>()`; if invalid, disable. Register RPC `IP_Harvest` once on that view (`ZNetView.Register` uses `Dictionary.Add`, ZNetView.cs:275, so a duplicate name throws; one `VeilHarvest` per view by construction).
- Stage tick: every 5 s (staggered) read the two keys, compute the stage with `HarvestStage.At`, set `VeilSight.ActiveStage`. Timestamp-based, so regrowth also advances while the zone was unloaded.
- `GetHoverText`: ripe → `"<name>\n[<color=yellow><b>$KEY_Use</b></color>] $inventory_pickup"` (same string as `Pickable.GetHoverText`, Pickable.cs:114-120, localized with `Localization.instance.Localize`); growing → `"<name> ($ip_plant_growing)"`. `Interact` refuses below `RequiredLevel` (client-side; colliders are off anyway) and sends `InvokeRPC("IP_Harvest")`.
- `RPC_Harvest` (runs on the ZDO owner, like `Pickable.RPC_Pick`, Pickable.cs:235): if the current stage is not ripe, ignore (serialises double picks); else write `stage = 1`, `time = now`, drop `Yield` items like `Pickable.Drop` (Pickable.cs:331), play `PickEffects` via a broadcast `IP_HarvestFx` RPC.
- **Ground plant prefabs** `IP_BaldrsTear_a/b/c`, `IP_HelsEmberFern_a/b/c` built by `PlantPrefabs` from the bundle: root with persistent `ZNetView`, `VeilHarvest`, `VeilSight(2|3)`, children `ripe` (`Plant_T2_x` model + collider) and `picked` (`Plant_T2_picked`, no collider). T3 keeps its `EmberAnchor` light/particles under `ripe` so they are gated too.
- **Spawning**: `ZoneManager.Instance.AddCustomVegetation(new CustomVegetation(prefab, true, VegetationConfig { … }))` per variant (R §4.2). Rarity: `PlaceVegetation` treats `m_max < 1` as a per-zone probability with one placement batch (ZoneSystem.cs:1399-1405), and `GroupSizeMin/Max` make the 1–2 plant cluster (ZoneSystem.cs:1425-1429). Three variants share the zone chance: each gets `Max = ZoneChance / 3`. Defaults:

| Field | Baldr's Tear | Hel's Ember Fern |
|---|---|---|
| `Biome` | `Mountain` | `AshLands` |
| `Max` (per variant) | 0.167 / 3 = 0.056 | 0.056 |
| `GroupSizeMin/Max`, `GroupRadius` | 1 / 2, 4 m | 1 / 2, 4 m |
| `MinTilt/MaxTilt` | 0 / 25 | 0 / 30 |
| `MinAltitude` | above the snow line (?) S6 | – |
| `BlockCheck`, `BiomeArea` | true, 3 | true, 3 |

  Placement checks can fail after the roll, so the real rate is below 1 in 6 (?): S6 samples zones in a new world (`ip_plants` counts) and tunes `ZoneChance`.
- **Existing worlds**: only zones generated after installing the mod get wild T2/T3 plants (ZoneSystem.cs:1337, R §4.1); the dedicated server must run the mod. Retrofit for explored zones is out of scope (§9).

### 3.4 Cultivation (Huldra's Hair on the ground)

- Sapling prefab `IP_HuldraSapling`: root with persistent `ZNetView`, `Piece` (`m_cultivatedGroundOnly = true`, `m_groundOnly = true`, no `m_onlyInBiome`; Piece.cs:145-147, 208), `Plant`, `Destructible` (`Plant.Destroy` needs an `IDestructible`, Plant.cs:365-374), `VeilSight(1)`.
- `Plant` settings (Plant.cs:24-61): `m_growTime = m_growTimeMax = 2 × StageMinutes × 60` s, `m_healthy = m_unhealthy = Plant_T1_Flat` S1-sized child, `m_healthyGrown = m_unhealthyGrown` S2-sized child (Plant swaps at 50 %, Plant.cs:152-159), `m_grownPrefabs = [IP_HuldraGround_a/b/c]`, `m_needCultivatedGround = true`, `m_biome` = all biomes, `m_tolerateCold = m_tolerateHeat = true`, `m_growRadius` 0.5 m. `Plant` itself picks the visible child with `SetActive` (Plant.cs:152-159); the sapling's `VeilSight` has one stage group (all children) and only toggles `enabled` flags, so the two never fight.
- `Grow` instantiates the grown prefab and destroys the sapling (Plant.cs:181-213). `IP_HuldraGround_a/b/c` = root `ZNetView` + `VeilHarvest(3 stages, keys IP_PlantStage/IP_PlantTime)` + `VeilSight(1)`; no keys = S3 ripe, so the chain is S1 → S2 (Plant) → S3 (harvest); after a pick it replays S1 → S2 → S3 inside `VeilHarvest` (same model as on trees).
- Registration: Jötunn `CustomPiece` with `PieceConfig { PieceTable = "_CultivatorPieceTable", Category = "Misc", Requirements = [VeilIngredient_T1:1] }` (R §1.3; class/field names (?) S7 checks the DLL). Known-recipe rule (PieceTable.cs:75): the piece should become available once `VeilIngredient_T1` was picked up (?) S7.
- Risks: `Plant.HaveRoof` reports NoSun under colliders on `Default/static_solid/piece` (Plant.cs:376-386); fine in a garden, S7 checks under forest canopy. A sapling is invisible to players without goggles (accepted: only goggle wearers get the ingredient).
- Plants 2 and 3 are not cultivable in v1 (open question 2).

### 3.5 Goggles

- `GoggleItems` gets `ItemConfig { CraftingStation = "forge" | "blackforge", MinStationLevel = 1, Requirements = parsed }` from config strings (`RecipeParser`, same fallback-to-default behaviour as `PotionItems.RegisterTier`); `Enabled = true`. Item names, armour and hair settings stay as in plan 4. Descriptions change from "not yet awake" to what they reveal.
- `GoggleLevel.Core.cs` (pure): `LevelOf(string dropPrefabName)` (`VeilGoggles_T1..3` → 1..3, else 0), `Reveals(int level, int required)`, `SeesHiddenPlayers(int level, bool configSwitch)` (level ≥ 3 && switch).
- `GogglesLevel` (static service, ticked by `VeilSightDriver`): `Local = DevOverride ?? LevelOf(Player.m_localPlayer?.m_helmetItem?.m_dropPrefab?.name)` (`m_helmetItem` Humanoid.cs:85, publicized; `m_dropPrefab` ItemDrop.cs:450). On change: raise `Changed`, write `IP_Goggles` (int) on the local player's ZDO (owner write, same pattern as `HiddenState.Write`). Re-equip on login/respawn needs no hook because the poll reads the slot (?) S8 confirms the slot is filled before the first tick after spawn.
- **Level III player reveal** (R §6), all gated by `[Goggles] RevealHiddenPlayers`:
  1. Nameplate: `TestShow_Postfix` keeps `__result` when `GoggleLevel.SeesHiddenPlayers(GogglesLevel.Local, …)`.
  2. Body: `VeilController.Refresh` uses `forceHide = !isLocal && IsHiddenFromPlayers(p) && !SeesHiddenPlayers(local)`. The wearer then sees the hidden player with the configured tier-III `BodyVeilMode` (a shimmering figure, not a plain body).
  3. Position (server): `HeaderPosition` asks `PlayerReveal.ShouldSpoof(isHiddenPlayer, peerIsOwner, peerGoggles, switch)`; `peerGoggles = ZDOMan.instance.GetZDO(peer.m_characterID)?.GetInt(IP_Goggles)` (ZNetPeer.cs:21, set at ZNet.cs:2113). Read only for hidden player ZDOs, so the hot path is unchanged. Without part 3 the wearer would get y = 10000 and see nothing (R §6).
  4. Map pin: none (`SendPlayerList` sends one package to all peers, ZNet.cs:2517-2532).
- Trust: the level is client-asserted, like `IP_Hidden` (R §6.6); accepted.

### 3.6 Config (all server-synced, admin only, like the existing entries)

| Section / key | Default | Note |
|---|---|---|
| `[Plants] LichenTreeChance` | 0.025 | per eligible tree |
| `[Plants] LichenStageMinutes` | 120 | two transitions after a pick = 240 min |
| `[Plants] LichenRevealDistance` | 40 | m, see §3.1 |
| `[Plants] HuldraCultivable` | true | hides the Cultivator piece when false |
| `[Plants] BaldrZoneChance`, `HelFernZoneChance` | 0.167 | split over three variants; read at registration (needs restart) |
| `[Plants] GroundRegrowMinutes` | 240 | T2/T3 picked → ripe |
| `[Plants] YieldT1..T3` | 2 / 2 / 2 | items per pick (?) balancing |
| `[Goggles] RecipeT1..T3` | as §2 | `Item:Amount` strings |
| `[Goggles] RevealHiddenPlayers` | true | level III cancels tier III for the wearer |
| `[TierN] Recipe` | old default + `,VeilIngredient_TN:2` | migration below |

Recipes and the vegetation chance are applied at registration; like the existing `Recipe` keys they are read once at startup and not live-synced (main spec §5.2 note in `PluginConfig`). Migration: a new config revision step in `PluginConfig` (pattern of `MigrateLookDefaults`) replaces a stored `[TierN] Recipe` that equals the old default with the new default and logs it; customised recipes are kept and a warning names the missing ingredient. Stage/regrow minutes and chances are read live (server value wins).

### 3.7 Dev tooling (`Dev/PlantCommands.cs`, `#if DEBUG`, all output to `Plugin.Log`)

- `ip_plants [radius=64]`: every `VeilHarvest`/sapling in range: prefab, position, stage, base stage/time keys, required level, revealed, ZDO owner; summary counts per prefab and the number of lichen trees vs eligible trees in loaded zones (S6 sampling).
- `ip_lichen force|off|clear|roll`: camera raycast to the looked-at tree (`TreeBase` in parents); writes `IP_LichenForce` (1 / -1 / removed) and instantiates or destroys the child at once; `roll` prints the hash value, chance and biome.
- `ip_goggles <0..3|off>`: sets `GogglesLevel.DevOverride` (and therefore `IP_Goggles`); `off` returns to the helmet slot.
- `ip_grow [stage]`: looked-at or nearest plant; `VeilHarvest`: sets base stage (default next) and time = now (owner write via RPC if not owner); `Plant` sapling: moves `plantTime` (`ZDOVars.s_plantTime`, ZDOVars.cs:11) back so it grows on the next `SUpdate`.
- `ip_state` additionally prints goggles level, `IP_Goggles`, and per nearby hidden player whether the reveal applies.

### 3.8 Localization (`Items/English.json`)

New: `ip_plant_huldrashair` "Huldra's Hair", `ip_plant_baldrstear` "Baldr's Tear", `ip_plant_helsemberfern` "Hel's Ember Fern", `ip_plant_growing` "growing", `piece_ip_huldrasapling` "Huldra's Hair sprout" + description. Changed: the three goggle descriptions (what each level reveals; III mentions hidden wanderers). Ingredient names stay (`Hel's Ember Spore` is the item, the plant is the fern).

## 4. Data flow

| Key | Object | Written by | Read by |
|---|---|---|---|
| `IP_LichenStage` int, `IP_LichenTime` long | tree ZDO | tree ZDO owner in `RPC_Harvest`; `ip_grow` | every client's `VeilHarvest` tick |
| `IP_LichenForce` int | tree ZDO | `ip_lichen` (Debug) | `TreeLichen.Start` on every client (Release too) |
| `IP_PlantStage` int, `IP_PlantTime` long | ground plant ZDO | plant ZDO owner in `RPC_Harvest` | every client |
| `plantTime` (vanilla) | sapling ZDO | vanilla `Plant` owner | vanilla `Plant` |
| `IP_Goggles` int | player ZDO | owning client (`GogglesLevel` on change) | server in `HeaderPosition`; `ip_state` |

Rules: only the ZDO owner writes; the pick request travels as RPC to the owner (`InvokeRPC` routes by ZDOID, R §2.3). Growth is derived from timestamps on every client, so no growth RPCs exist. Goggle level, visibility and stage visuals are per viewer and never networked, except `IP_Goggles` for the server-side position exception.

## 5. Error handling

- Missing bundle: no plants, no lichen child, goggles fall back as in plan 4; mead recipes then drop the ingredient requirement with an error log (otherwise the meads are uncraftable).
- Unknown recipe item (e.g. `FlametalNew` wrong): Jötunn logs it; the registration log lists every resolved requirement, and a missing prefab falls back to the default string once, then the goggle is registered without a recipe and an error is logged.
- Eligible tree prefab not found: warning per name, the others still get the child. The registration log prints which trees carry `IP_Lichen`.
- Invalid parent view or `WorldGenerator.instance == null` at `Start`: destroy the lichen child (fail closed: no lichen rather than unsynced lichen) and log once per session.
- RPC on a non-ripe plant, negative elapsed time (clock change): `HarvestStage.At` clamps to the base stage.
- Every component catches and logs once per error type (pattern of `VeilController._loggedErrors`).

## 6. Testing

Unit tests (`InvisibilityPotion.Tests`, linked `*.Core.cs`):
- `GoggleLevel`: name mapping incl. null/unknown/case, `Reveals` table 0..3 × 1..3, `SeesHiddenPlayers` with switch.
- `HarvestStage`: absent keys = ripe; pick → stage 1; boundaries at exactly n × stageMinutes; cap at max stage; negative elapsed; 2-stage ground mode (picked → ripe after regrow minutes).
- `LichenRoll`: determinism, different seeds/salts differ, rate within ±0.3 % of 0.025 over 200k grid positions, stability under ±0.04 m float jitter except at rounding edges (documented).
- `PlayerReveal.ShouldSpoof`: owner, non-player, not hidden, goggles 0..3, switch off.
- `RecipeParser` on the new defaults; recipe migration (old default → new, custom kept).

In-game checklist (`docs/testing.md`, "Plan 5"; batched per the batch-testing rule):
- Goggles: craft I/II/III at forge/blackforge with the listed materials; II/III consume the previous goggles; worn look; `ip_state` level; unequip → 0 within 0.5 s.
- Lichen: `ip_plants` shows ~1/40 lichen trees in a Black Forest; without goggles the tree looks and hovers like vanilla (screenshot pair); with I: visible, stage S3 pickable, yields 2, resets to S1; `ip_grow` S1 → S2 → S3; restart the world: same trees carry lichen; fell the tree: lichen gone; distant billboards show no floating lichen.
- Ground plants: new world, Mountains/Ashlands sampling; goggles I sees no Baldr's Tear, II does; picked model, regrowth after `GroundRegrowMinutes`; T3 ember light hidden without III.
- Cultivation: sapling on cultivated ground anywhere, not on raw ground; grows S1 → S2 → S3; pick replays.
- Meads: base needs 2 ingredients; an old cfg with the old default migrates, a custom recipe is kept with a warning.
- Two clients (pending, needs plan 3 server): goggles III wearer sees a tier-III-hidden player's nameplate and veiled body at the real position, a non-wearer does not; no map pin for either; lichen pick by client A updates client B's stage within 5 s; double pick yields once.

## 7. Build order

1. **Spike round** (one Debug build, one in-game session): S1 tree prefab names, hierarchy, colliders, LODGroup, `TreeBase` vs `Destructible`, vanilla pickable names for effects; S2 `FlametalNew`; S3 `WorldGenerator` seed and biome on a client; S4 lichen collider layer/trigger hover; S5 LOD behaviour of a child renderer; S6 snow-line altitude; S7 Jötunn `PieceConfig` and known-piece rule; S8 helmet slot after spawn. Results into `docs/decompile-notes.md` ("Plan 5") with every member used.
2. Pure cores + tests: `GoggleLevel`, `HarvestStage`, `LichenRoll`, `PlayerReveal`, recipe migration.
3. Goggle recipes, `GogglesLevel`, `IP_Goggles`, `ip_goggles`, `ip_state` lines. Test: craft and equip.
4. `VeilSight` + `VeilSightDriver` + `VeilHarvest` on a ground plant spawned with `ip_spawn IP_BaldrsTear_a`; `ip_plants`, `ip_grow`. Test: gate, pick, regrowth.
5. `TreeLichen` on the vanilla trees, `ip_lichen`. Test: rarity, identity without goggles, restart stability.
6. Ground vegetation registration (T2/T3) and S6 sampling in a fresh world.
7. Cultivation piece and the `Plant` chain.
8. Mead recipe defaults and migration.
9. Level-III player reveal (nameplate, `forceHide`, `HeaderPosition`); single-client parts tested with `ip_goggles 3` on a listen host, two-client parts pending plan 3.
10. Localization, `docs/testing.md`, README section, decompile-notes completed.

## 8. Risks

| Risk | Mitigation |
|---|---|
| Lichen floats or clips on some trunks | per-prefab offsets from S1; eligible list limited to measured prefabs |
| LOD/billboard shows lichen far away | 40 m distance gate; LOD0 registration if S5 demands |
| Hidden collider still blocks rays (trigger semantics) | `Collider.enabled = false`, S4 |
| Rarity drifts after restart | position hash, no ZDOID; restart check in the checklist |
| Ground plants absent in explored worlds | documented; cultivation for T1; retrofit spawner is a backlog item |
| A modified client sees/picks hidden plants | accepted (PvE gate, R §3); not a security boundary |
| `HeaderPosition` exception exposes the real position to a cheating client claiming level III | same trust level as `IP_Hidden`; server switch `RevealHiddenPlayers` |
| `Custom/Vegetation` mock shader missing (plan 4 S2) | plan 4 fallback to `Custom/Creature` |

## 9. Out of scope

Alpha-card textures for lichen/fern; wisps (visual or creature); PvP balance of the level-III reveal beyond the server switch; map pins for goggle wearers; retrofit spawning in explored zones; cultivation of plants 2 and 3; other localizations.

## Rulings on the draft's open questions (controller, 2026-10-02)

1. Lichen timing: 120 min per stage, so a pick (→ S1) is ripe again after 240 min, matching the user's regrowth decision.
2. Baldr's Tear and Hel's Ember Fern are not cultivable in v1 (backlog; the wild spawn is the search mechanic the goggles are for).
3. Yield per pick is a per-plant random range (lichen 1–2, Baldr's Tear 1, Hel's Ember Fern 2–4), scaled mildly by the plant's size; felling a lichen tree drops nothing (the lichen is lost with the tree).
5. (user, 17:53) Hel's Ember Fern is rarer (1 in 15 zones) but grows in groups of 3–6 within 6 m; Baldr's Tear stays 1 in 6 zones with 1–2 plants. Ground plants get a random size (Baldr's Tear 0.8–1.3×, fern 0.7–1.6×) stored in the ZDO so every client sees the same, and the model variants a/b/c are mixed randomly.
4. Lichen only on Black Forest trees; saplings and growing plants are invisible to players without goggles.
