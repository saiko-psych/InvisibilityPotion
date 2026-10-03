# Plants and Goggles

The mead ingredients grow as **veil plants**. They are invisible, cannot be hovered and cannot be picked unless you wear **veil goggles** (helmet slot) of at least the plant's tier.

![Baldr's Tear in the Mountains](https://raw.githubusercontent.com/kitschekko/InvisibilityPotion/main/docs/screenshots/plant-baldrs-tear-mountains.jpg)

## The plants

| Tier | Plant | Where | Rarity (default) | Yield per pick |
|---|---|---|---|---|
| I | Huldra's Hair (lichen on the bark) | Black Forest firs and pines | about 1 eligible tree in 40 (2.5 %) | 1-2 Huldra's Hair |
| II | Baldr's Tear | Mountains, on dry land | a group of 1-2 plants in about 1 of 5 new zones | 1 Baldr's Tear |
| III | Hel's Ember Fern | Ashlands, on solid ground, not on lava | a group of 3-6 ferns in about 1 of 10 new zones | 2-4 Hel's Ember Spore |

- Bigger plants give up to 25 % more, smaller ones up to 25 % less (ground plants only).
- **Regrowth:** a picked ground plant is ripe again after 240 minutes of world time, the same as vanilla thistle. The lichen needs two growth stages of 120 minutes each.
- **Wild ground plants only appear in newly generated zones.** In an existing world, explore new land or cultivate them. The lichen on trees works in existing worlds.
- Plants come in several model variants (5 for Baldr's Tear and the fern) and are mixed by position. Their leaves and strands sway gently in the wind.

## The goggles

![Allfather's Eye](https://raw.githubusercontent.com/kitschekko/InvisibilityPotion/main/docs/screenshots/goggles-3-allfathers-eye.jpg)

| Goggles | Station | Default recipe | Shows |
|---|---|---|---|
| Watchman's Glass (I) | Forge, level 1 | Bronze 5, Resin 4, Troll Hide 2 | Huldra's Hair |
| Mimir's Glass (II) | Forge, **level 3** | Silver 5, Crystal 2, Wolf Pelt 3, Watchman's Glass | tier I and II plants |
| Allfather's Eye (III) | Black forge, level 2 | Flametal 5, Black Core 1, Obsidian 2, Mimir's Glass | every veil plant, **and players hidden by a Shadow Veil** |

Goggles II and III use up the previous goggles in their recipe.

Allfather's Eye shows a Shadow Veil player with nameplate, veiled body and real position. It never shows a map pin. A server switch (`AllowPvpInvisibility`) turns tier III hiding off, and `RevealHiddenPlayers` turns the goggles' player reveal off.

## Cultivation

Use the **Cultivator**. A sprout costs 1 of the ingredient and takes about 240 minutes to grow.

- **Huldra's Hair:** aim at the bark of a fir or pine trunk (within 1 m).
- **Baldr's Tear:** cultivated ground in the Mountains.
- **Hel's Ember Fern:** cultivated ground in the Ashlands.

Sprouts are veil plants too: you need goggles to see them grow. Planted plants regrow after picking like wild ones.

## Plants in water or on lava

Older zones could contain Baldr's Tear or ferns standing in water or on lava. Since 0.3.1 such wild plants are removed when their zone loads (`RemoveMisplacedWildPlants`). Cultivated plants are never removed.
