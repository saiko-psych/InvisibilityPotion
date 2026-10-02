# Mod compatibility (0.3.0)

Assessment of where InvisibilityPotion touches the game and where other mods can collide with it. Written for the first public
release (2026-10-02). Game hooks are documented in `docs/decompile-notes.md`; this file covers what other mods see.

Harmony behaviour that matters below: BepInEx ships **HarmonyX**, which runs every prefix even after one returned false
("cancelling running a prefix will not cancel running the subsequent prefixes", HarmonyX wiki, *Difference between Harmony and
HarmonyX*). A skipping prefix therefore never hides other mods' prefixes; it only skips the original method. Within one priority,
patches run in registration order (plugin load order; ours loads after every mod installed here, see the BepInEx log).

## 1. Harmony patches

All Release patches are listed in `Plugin.ExpectedPatchTargets`; startup logs `Patch health: N targets patched, M missing`.

| Target | Kind | Skips original? | Priority | Notes |
|---|---|---|---|---|
| `BaseAI.CanHearTarget` (static overload) | prefix | **yes**, for tier II/III hidden targets (`__result = false`) | Low | Runs last among prefixes, so our `false` wins over another mod's prefix that sets `__result`. A postfix of another mod can still set it back to true. |
| `BaseAI.CanSeeTarget` (static overload) | prefix | **yes**, same rule | Low | As above. Mods that patch the instance overloads only (`CanSeeTarget(Character)`) are unaffected; those overloads call the static one. |
| `BaseAI.FindEnemy` | postfix | no | Low | Nulls a hidden result (raid/HuntPlayer monsters). Runs after other postfixes. |
| `MonsterAI.UpdateTarget` | postfix | no | Normal | Drops a hidden target after `AggroLossTime`. |
| `MonsterAI.UpdateSleep` | prefix | **yes**, while the closest player in wake range is hidden (keeps vanilla's sleep timer running) | Normal | A mod with its own wake-up logic in a prefix still runs (HarmonyX). |
| `Player.UpdateStealth` | postfix | no | Normal | Caps `m_stealthFactorTarget` for tier I while standing. |
| `Character.Damage` | prefix | no | Normal | Reveal trigger only. |
| `Attack.StartDraw` | postfix | no | Normal | Reveal trigger. |
| `Humanoid.StartAttack` | postfix | no | Normal | Reveal trigger (staff cast, axe/pickaxe swing). |
| `Humanoid.BlockAttack` | postfix | no | Normal | Reveal trigger. |
| `Player.PlacePiece` | postfix | no | Normal | Reveal trigger (hammer, hoe, cultivator). |
| `VisEquipment.UpdateLodgroup` | postfix | no | Normal | Re-applies the veil to new renderers. |
| `VisEquipment.SetChestEquipped`, `SetLegEquipped` | prefix + postfix | no | Normal | Hands the original body materials back for the duration of the call. Armor/visual mods that write `m_bodyModel` materials outside these methods can lose the veil until the next equipment change. |
| `Player.CanConsumeItem` | postfix | no (may set `__result = false`) | Normal | Refuses a weaker or equal mead, and every veil mead while the shared Veil Cooldown runs. Other consume/cooldown mods see vanilla's result before ours. |
| `EnemyHud.TestShow` | postfix | no | Normal | Hides the nameplate of a tier III hidden player. Nameplate/HUD mods that draw their own player labels bypass this. |
| `ZNet.UpdatePlayerList` | postfix | no | Normal | Server: clears the map position of hidden players. |
| `ZDOMan.SendZDOs` | **transpiler** | no | Normal | Server: replaces the one `ZDO.GetPosition()` call that feeds `ZPackage.Write(Vector3)` in the ZDO header with `HeaderPosition` (spoofed height for hidden players). **Most fragile patch**, see below. |
| `Player.UpdatePlacementGhost` | postfix | no | Normal | Huldra's Hair sprout: trunk check and ghost snap. |
| `Player.GetMaxCarryWeight` | postfix | no | Normal | Multiplies the final value by `CarryWeightMultiplier`. A later postfix of another mod that adds a flat bonus is not reduced; candidate for `Priority.Low` (`Patches/CarryWeightPatches.cs`, not changed here). |
| Debug only: `FejdStartup.Start`, `PlayerController.TakeInput` | postfix | no | Normal | Not in Release builds. |

### The `SendZDOs` transpiler

- It expects exactly one `callvirt ZDO::GetPosition()` immediately followed by `callvirt ZPackage::Write(Vector3)`. Anything else
  (no match, or two) leaves the IL untouched and logs `SendZDOs transpiler: pattern not found, position spoof disabled`.
- **Patch health does not catch this**: the transpiler is still registered, so `ZDOMan.SendZDOs` counts as patched. Search the
  server log for the error line instead.
- Failure is safe (vanilla behaviour: other players receive the real position of a tier III player; the nameplate and map pin
  are still hidden), never a crash.
- Breaks when another mod transpiles the same instruction pair before us, or replaces `SendZDOs` with a skipping prefix
  (network/performance mods that rewrite ZDO sending). A transpiler that runs after ours and keeps our call is fine.

## 2. Vanilla prefab modifications

| What | How | Effect on other mods |
|---|---|---|
| Tree prefabs `FirTree`, `Pinetree_01`, `FirTree_big` | `TreeLichen` component added to the prefab root (at `OnVanillaPrefabsAvailable` and on every `OnPrefabsRegistered`). A lichen child object is instantiated only on trees that win the roll. RPC `IP_LichenPlant` registered on the tree's `ZNetView`. | Mods that clone these trees after us copy the component: `TreeLichen.Start` does not check the prefab hash, so such clones can roll lichen too in the Black Forest (harmless, but planting a sprout on them is refused because `TreeSapling` checks the hash). Mods that replace the tree prefabs entirely lose wild lichen. |
| Crop saplings (`sapling_carrot`, else turnip/onion/barley) | **Cloned** (`CreateClonedPrefab`) into `IP_HuldraSapling`, `IP_BaldrSapling`, `IP_FernSapling`; the vanilla prefab is not changed. | None. |
| `MeadHealthMinor`, `MeadBaseHealthMinor`, `Thistle`, `HelmetLeather` | Read as data templates for our items (shared data copied onto our own bundle prefabs; plain clones as fallback). Not modified. | None. |
| Vanilla mead consume status effect (`[Veil] PotionVfxSource`, default `MeadFrostResist`) | Its start/stop `EffectList`s are **shared by reference** with our status effects. | A mod that edits those effect lists in place also changes our potion start/stop effect. |
| Pick effects (`Pickable_Thistle` etc.) | `m_pickEffector` read for the pick sound. | None. |
| Vegetation | Two `ZoneVegetation` entries through Jötunn's `ZoneManager` (Mountains, Ashlands). | Mods that rebuild `ZoneSystem.m_vegetation` from their own list (world-gen overhauls such as Expand World Data) can drop ours unless they read the live list. |

Prefabs and items added: `MeadInvisibility_T1..3`, `MeadBaseInvisibility_T1..3`, `VeilIngredient_T1..3`, `VeilGoggles_T1..3`,
`IP_Lichen`, `IP_BaldrsTear*`, `IP_HelsEmberFern*`, `IP_HuldraGround_*` (legacy), `IP_HuldraSapling`, `IP_BaldrSapling`,
`IP_FernSapling`. The veil plants use their own `VeilHarvest` component instead of `Pickable`, so pick-all / bulk-harvest mods
do not find them (by design).

## 3. ZDO keys and RPCs

All namespaced `IP_*` (no collision with vanilla or other mods expected):

- Player ZDO: `IP_Tier`, `IP_Hidden` (veil state, read by every peer), `IP_Goggles` (goggle level, read by the server).
- Tree ZDO: `IP_LichenForce`, `IP_LichenH`, `IP_LichenAngle`, `IP_LichenStage`, `IP_LichenTime`, `IP_SaplingUsed`.
- Plant ZDO: `IP_PlantStage`, `IP_PlantTime`, `IP_PlantScale`, `IP_PlantVariant`, `IP_PlantYaw`.
- RPCs: `IP_LichenPlant` (tree view), `IP_Harvest`, `IP_Harvested` (plant or tree view).

## 4. Network and config sync

- `[NetworkCompatibility(EveryoneMustHaveMod, VersionStrictness.Minor)]`: Jötunn compares the mod lists at connect. Server and
  every client need InvisibilityPotion with the **same major.minor** (0.3.x with 0.3.y is fine, 0.2 with 0.3 is refused). A
  vanilla client cannot join a server running the mod, and a client with the mod cannot join a server without it.
- Gameplay config (`[TierN]`, `[General]`, `[Plants]`, `[Goggles]`) is bound with `ConfigurationManagerAttributes.IsAdminOnly`:
  Jötunn's `SynchronizationManager` pushes the server values to clients on join and on change, and only admins (server
  `adminlist.txt`) can edit them in game. Look values (`[Fog]`, `[Fog.TierN]`, `[Veil]`) are client-local.
- **Read-at-startup values are not affected by the sync**: mead and goggle recipes, `HuldraCultivable`, `CultivateMinutes`,
  `BaldrZoneChance`, `HelFernZoneChance`. Recipes are registered from each machine's own file, and zones are generated by the
  client that first enters them, so these must be the same in every player's config file (or left at the defaults).
- The server must run the mod for the tier III position spoof (`SendZDOs`) and the hidden map pin (`UpdatePlayerList`).

## 5. Mods installed on the development machine

From `BepInEx/plugins/` and `BepInEx/LogOutput.log` (load order: PlantEasily, ComfyGizmo, Configuration Manager, Script Engine,
Jötunn, Recipe Pinner, Quick Stack Store, Mumble Positional Audio, Pinnacle, InvisibilityPotion, CameraTweaks).

| Mod | Interaction points | Assessment |
|---|---|---|
| **PlantEasily** 2.2.0 (Advize) | Grid planting, snapping and bulk harvest for cultivator pieces; it works on the placement ghost and adds extra ghosts for rows/columns. | Our Baldr/Fern sprouts are normal crop saplings and should work with grid planting. **Huldra's Hair sprout**: only the main ghost (`m_placementGhost`) gets our trunk check and snap; extra grid ghosts (Rows/Columns > 1) or PlantEasily's snapping could place sprouts off the trunk. Keep 1x1 (the installed config) for Huldra's Hair. Our postfix runs after PlantEasily's (same priority, later load), so our snap wins for the main ghost. `PreventInvalidPlanting` may judge the Huldra sprout by vanilla crop rules (it does not need cultivated ground). Bulk harvest does not touch veil plants (no `Pickable`). Needs an in-game check. |
| **QuickStackStore** 1.4.15 | Inventory stacking, sorting, trash, restock. | Our carry weight change is a postfix on `Player.GetMaxCarryWeight`; every reader (including inventory UIs) sees the reduced value. Quick stacking to nearby containers does not reveal a hidden player (not a reveal trigger, by design). No conflict expected. |
| **ConfigurationManager** 19.0 (BepInEx, `com.bepis.bepinex.configurationmanager`, F1) | IMGUI window that unlocks the cursor while `DisplayingWindow` is true and restores the previous cursor state on close. | Our gameplay entries show in it; admin-only entries are handled by Jötunn. The user reported a freeze with F1: the Debug-only `ip_fogui` window also unlocks the cursor and writes `GameCamera.m_mouseCapture`. Fixed: the window touches the cursor and `m_mouseCapture` only while it is open and owns the mouse, and handing the mouse back (close, F7) now waits while any plugin's `DisplayingWindow` is true (reflection, `Dev/FogTuningWindow.cs` `ExternalWindow`), so `GameCamera` does not re-lock the cursor under the config window. Release builds do not contain the window. Separately, vanilla `GameCamera.UpdateMouseCapture` re-locks the cursor every frame while no vanilla UI is open (decompile-notes §GameCamera); this generic CM build does not know Valheim's UI, so the cursor can still fight with the camera without our mod. The Valheim fork `shudnal-ConfigurationManager` handles that and is the one the README recommends. |
| **CameraTweaks** 1.4.0 (Searica) | Field of view and camera distance. | No shared hooks. |
| **ComfyGizmo** 1.16.0 | Rotation gizmo for the placement ghost. | Touches the ghost's rotation; our Huldra snap sets position and rotation in our `UpdatePlacementGhost` postfix, which runs after ComfyGizmo's patches at equal priority (later load). If a future ComfyGizmo version applies rotation later (e.g. in `LateUpdate`), the trunk patch may face the wrong way. Low risk. |
| **Pinnacle** 1.17.0 | Minimap pin UI, pin list. | Does not publish player positions; the hidden player's map pin is cleared server side before any client sees it. No conflict expected. |
| **Mumble Positional Audio** 3.1.0 | Sends the local player's position to the Mumble client. | **Leaks a tier III player's position through voice chat** to other Mumble users with positional audio. Outside our control; mention to PvP groups. |
| **RecipePinner** 1.5.1 | Pins recipes on the HUD. `HotkeyToggleVisibility = F7`. | Our recipes are normal ObjectDB recipes. **F7 collides with the Debug tuning window's mouse toggle** (Debug builds only, no player impact). |
| **ScriptEngine** 11.1 | Hot-reloads plugins from `BepInEx/scripts`. | Do not hot-reload InvisibilityPotion: Jötunn registrations and the tree prefab component do not survive a reload. |

## 6. Known incompatibility classes (not installed here)

- AI overhauls that replace `BaseAI.CanSeeTarget`/`CanHearTarget` callers with their own perception code: hidden players are seen.
- Nameplate or player-list mods with their own rendering: tier III nameplates or list entries may show.
- Network optimisation mods that rewrite `ZDOMan.SendZDOs`: position spoof disabled (log line above).
- Carry weight mods that add a postfix after ours: their bonus is not reduced while a veil is active.
- World-generation overhauls that replace the vegetation list: no wild Baldr's Tear / Hel's Ember Fern.
- Other invisibility/stealth mods that also write `m_stealthFactorTarget` or player materials: unpredictable mixed visuals.
