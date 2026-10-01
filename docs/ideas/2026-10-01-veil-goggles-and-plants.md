# Idea: Veil Goggles and hidden ingredient plants

Captured 2026-10-01 from the author. Not designed yet; candidate for its own plan after plan 2 (gameplay core) is merged, ideally combined with plan 4 (Unity assets) because it needs models.

## Idea

- Three ingredient plants, one per potion tier, each spawning in a different biome: Black Forest (tier I), Mountains (tier II), Ashlands (tier III).
- The plants are "veiled": invisible and not interactable unless the player wears Veil Goggles.
- Veil Goggles are a wearable item with three upgrade levels; level N reveals the plant of tier N (and lower). Goggles could also reveal hidden players (counter-play for PvP) at a higher level.
- Each potion tier's recipe requires its plant, so progression is: goggles level 1 → Black Forest plant → tier I mead; upgrade goggles → next biome → next tier.

## Open design questions

- Reveal mechanic: hide plant renderers and pickable interaction unless the viewer wears goggles of the right level (client-side), or spawn plants only for goggle wearers (server-side)?
- Do goggles reveal hidden players? At which level? Does that break PvP balance?
- Spawn rules: Jötunn `CustomVegetation` / `SpawnConfig` per biome, density, min/max altitude.
- Goggle upgrade materials and crafting station.
- Models: plants, goggles (helmet slot or new slot?), icons; all in Unity (plan 4 pipeline).

## Touches

Items (plants, goggles), vegetation spawning, a visibility patch for pickables, an equip status effect for goggles, localization, Unity assets.
