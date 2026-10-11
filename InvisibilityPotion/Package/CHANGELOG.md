# Changelog

## 0.4.1 (2026-10-11)

- Huldra's Hair is less rare: `[Plants] LichenTreeChance` 0.025 -> 0.1 (1 eligible fir/pine in 10 instead of 1 in 40). The roll is deterministic per tree and world seed, so the new rate applies to every tree at once, old zones included. Migrated once in config files that still hold the old default; a customised value stays.
- `ip_plants` and `ip_retrofit` are now in the Release build for server admins (a local game and the host always pass; on a dedicated server Jötunn's admin status is required). `ip_plants` lists the hidden plants, lichen trees and sprouts around you; `ip_retrofit` shows the retrofit counters, `ip_retrofit here` runs it for your zone.
- Retrofit log: every 25 evaluated zones the server logs a summary ("N zones evaluated: P got plants, H already had plants, R roll missed or wrong biome, S no valid spot"), so a quiet log still proves the retrofit ran. New `[Plants] RetrofitReportOnly` (default off, server-side): dry run that logs what would be placed and changes nothing.
- A grown sprout logs one line on its owner ("cultivation: IP_BaldrSapling grew into IP_BaldrsTear at (x, z) zone (a,b) after N min"), so growth is visible in a server log.
- `[Plants] CultivateMinutes` is applied again after the server's config arrives, so the server's value counts for every client (it used to be the client's own file; the default 240 minutes made sprouts look stuck).
- New `[Logging] Verbose` (default off, local): the startup dumps (tree colliders and LODs, mesh bounds, bundle asset lists, per-tree lichen placement) go to Debug level unless it is on; the summary lines stay at Info.
- Two tree names that do not exist in Valheim 1.0.17 are no longer looked up at startup (`PineTree_01_dead`, `Pine_tree_normal_small`; they were only inspected, never used).
- Correction to the 0.4.0 notes: Release builds patch 23 targets (25 in Debug builds, which add two Debug-only hooks). No new patch target in 0.4.1. Built and checked against Valheim 1.0.17.

## 0.4.0 (2026-10-03)

- Wild Baldr's Tear and Hel's Ember Fern now also appear in zones that were generated before the mod was installed: when a player comes near such a zone, the server rolls its chance once, with the same seed vanilla uses for new zones, and places the group under the usual rules. Zones that already hold a wild plant are skipped; a zone that misses its roll misses on every load, so nothing accumulates. Switch `[Plants] RetrofitExistingZones` (default on, server-side). Debug build: `ip_retrofit`.
- Wild plant rates raised to the rate of vanilla carrot and turnip seeds: `BaldrZoneChance` 0.2 -> 0.5, `HelFernZoneChance` 0.1 -> 0.5 (migrated once in existing config files that still hold an old default; customised values stay).
- New patch target `ZoneSystem.CreateGhostZones` (23 targets in Release builds, 25 in Debug builds).

## 0.3.2 (2026-10-03)

- Package page: screenshot gallery, install guide with the Thunderstore package name, source and support links. No gameplay change.

## 0.3.1 (2026-10-03)

- Wild Baldr's Tear and Hel's Ember Fern left in water or (ferns) on lava by zones generated before 0.3.0's dry-land rules are removed when their zone loads. Cultivated plants are never removed. Config `[Plants] RemoveMisplacedWildPlants` (default on, server-synced).
- The veil meads and their mead bases can be placed with the Serving Tray ("Mead" tab) like vanilla meads: drink a placed mead with Use, pick it up with alt-use, or remove it with the tray to get it back.

## 0.3.0

- Custom models for the three veil meads, their mead bases, the veil ingredients and three levels of veil goggles.
- Hidden plants: Huldra's Hair grows on some Black Forest firs and pines, Baldr's Tear in the Mountains, Hel's Ember Fern in the Ashlands. They are invisible without veil goggles of at least their tier.
- Veil goggles (helmet slot): Watchman's Glass (forge), Mimir's Glass (forge level 3), Allfather's Eye (black forge level 2). Level III also shows players hidden by a Shadow Veil Mead (server switch).
- Cultivation: plant Huldra's Hair on a fir or pine trunk, Baldr's Tear and Hel's Ember Fern on cultivated ground in their own biome.
- Mead base recipes now need 2 of the tier's veil ingredient. Unchanged old default recipes in the config are migrated automatically.
- Fog veil around a hidden player instead of plain transparency; tiered looks, configurable.
- Carry weight is reduced while a veil is active (tier I 75 %, tier II 60 %, tier III 50 %).
- Tool use reveals you: axe and pickaxe swings, building with the hammer, hoe and cultivator (config `RevealOnToolUse`).
- Veil Cooldown: one shared cooldown after drinking (30/60/90 s by tier, shorter than the veil so a stronger mead can still replace a running one). The reveal debuff is now "Veil Broken": stamina regeneration 25 % for 15 s. Breaking the veil by your own action empties the stamina bar. Unchanged old defaults in the config are migrated automatically.
- Harsher Veil Broken, lasting until the veil returns (tier II/III; tier I 15 s): stamina regeneration 15 %, eitr 25 %, health 50 %, movement 30 % slower (tier I 20 %); new `[TierN]` keys `DebuffSpeedModifier`, `DebuffEitrRegenMultiplier`, `DebuffHealthRegenMultiplier`.
- Veil Broken harsher still (tier I lasts 20 s): stamina regeneration 10 %, eitr 15 %, health 35 %, movement 50 % slower (tier I 35 %), no sprinting. Unchanged old defaults are migrated automatically.
- The fog veil is lighter (you stay visible to other players; the veil hides you from monsters, the fog is only a hint) and sits low: dense near the ground and near you, thinning with height and distance.
- Requires Jötunn 2.30.0 and BepInExPack_Valheim 5.4.2350 (was 2.30.2 / 5.4.2351). Jötunn refuses a client whose Jötunn version differs from the server's, down to the patch number: use the same Jötunn version on the server and every client.

## 0.2.0

- Gameplay core: three tiers of invisibility.
  - Tier I (Faint Veil): enemies notice you less, even while standing.
  - Tier II (Deep Veil) and III (Shadow Veil): enemies neither see nor hear you, sleeping monsters stay asleep, raid monsters do not pick you.
  - Tier III also hides you from other players: no nameplate, no map pin, no real position sent to them.
- Reveal rules: dealing damage, taking damage, blocking or parrying, drawing a bow and casting a staff reveal you; tier II and III hide you again after a few seconds without attacking.
- "Revealed" debuff: slower stamina regeneration after a reveal.
- A weaker or equal mead cannot be drunk while a stronger veil is active.
- All values configurable and synced from the server (admin only).

## 0.1.0

- Project skeleton: BepInEx + Jötunn plugin, patch health check at startup, build and packaging scripts.
