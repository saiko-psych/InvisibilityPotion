# Changelog

## 0.3.0

- Custom models for the three veil meads, their mead bases, the veil ingredients and three levels of veil goggles.
- Hidden plants: Huldra's Hair grows on some Black Forest firs and pines, Baldr's Tear in the Mountains, Hel's Ember Fern in the Ashlands. They are invisible without veil goggles of at least their tier.
- Veil goggles (helmet slot): Watchman's Glass (forge), Mimir's Glass (forge level 3), Allfather's Eye (black forge level 2). Level III also shows players hidden by a Shadow Veil Mead (server switch).
- Cultivation: plant Huldra's Hair on a fir or pine trunk, Baldr's Tear and Hel's Ember Fern on cultivated ground in their own biome.
- Mead base recipes now need 2 of the tier's veil ingredient. Unchanged old default recipes in the config are migrated automatically.
- Fog veil around a hidden player instead of plain transparency; tiered looks, configurable.
- Carry weight is reduced while a veil is active (tier I 75 %, tier II 60 %, tier III 50 %).
- Tool use reveals you: axe and pickaxe swings, building with the hammer, hoe and cultivator (config `RevealOnToolUse`).
- Veil Cooldown: one shared cooldown after drinking (90/180/240 s by tier). The reveal debuff is now "Veil Broken": stamina regeneration 25 % for 15 s. Unchanged old defaults in the config are migrated automatically.

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
