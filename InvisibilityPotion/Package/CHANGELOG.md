# Changelog

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
