# Configuration

The file is `BepInEx/config/saikopsych.InvisibilityPotion.cfg`, created on first start.

## Server sync and editing

- Gameplay values (`[General]`, `[Tier1-3]`, `[Plants]`, `[Goggles]`) are **synced from the server**. In game only admins can change them. Edit the server's file, then restart (or change them in game as admin).
- Look values (`[Fog]`, `[Fog.Tier1-3]`, `[Veil]`) are **client-side**: everyone tunes their own fog.
- With [ConfigurationManager](https://thunderstore.io/c/valheim/p/shudnal/ConfigurationManager/) installed (F1 by default) you can edit values in game.
- **Read at startup, not synced:** mead and goggle recipes, `HuldraCultivable`, `CultivateMinutes`, `BaldrZoneChance`, `HelFernZoneChance`. Keep them identical in every player's file, or leave the defaults.

## [General]

| Key | Meaning |
|---|---|
| RevealOnDamage | Taking damage reveals you |
| RevealOnBlock | A blocked hit or parry reveals you |
| RevealOnBowDraw | Drawing a bow reveals you |
| RevealOnToolUse | Axe, pickaxe, hammer, hoe, cultivator reveal you (picking by hand does not) |
| DrainStaminaOnAttackReveal | Your own action that breaks the veil empties your stamina |
| ShowSelfFaintly | You still see a faint version of yourself |
| AllowPvpInvisibility | Server switch for hiding players from other players (tier III) |
| FogCutoffLight / FogCutoffDense / FogCutoffSelf | How strongly your body is cut away for tier I / tier II-III / your own view |

`PlantDefaultsRevision` and `GameplayDefaultsRevision` are internal (see Migration below).

## [Tier1], [Tier2], [Tier3]

| Key | Meaning |
|---|---|
| Duration | Effect length in seconds (60 / 120 / 180) |
| StealthModifier, NoiseModifier | Tier I only: fraction of vanilla visibility / noise (0.25 = a quarter) |
| IgnoredByEnemies | Enemies cannot see or hear you at all (tier II, III) |
| HiddenFromPlayers | Other players cannot see your position, model or nameplate (tier III) |
| AggroLossTime | Seconds a chasing enemy keeps searching before giving up (above 30 has no effect) |
| RehideDelay | Seconds without attacking until hidden again; 0 = an attack ends the effect |
| DebuffStaminaRegenMultiplier, DebuffEitrRegenMultiplier, DebuffHealthRegenMultiplier | Veil Broken: regeneration as a fraction of normal (1 = no penalty) |
| DebuffSpeedModifier | Veil Broken: speed change (-0.5 = half speed, 0 = none). Sprinting is always blocked |
| DebuffDuration | Veil Broken length in seconds; applies to tier I (tiers II/III last until the veil returns). 0 = no debuff |
| CarryWeightMultiplier | Max carry weight while active (1 = no penalty) |
| Cooldown | Shared Veil Cooldown in seconds after drinking this tier; 0 = off |
| Recipe | Mead base recipe at the Mead Ketill |
| BodyVeilMode | How your body is drawn: Off, Cutoff, Hide, Tint, Ghost, Distortion, Shadow, Spirit (case-sensitive) |

## [Plants]

| Key | Meaning |
|---|---|
| LichenTreeChance | Chance that an eligible Black Forest fir/pine carries Huldra's Hair (0.025) |
| LichenStageMinutes | Minutes per growth stage of the lichen; regrowth takes twice this (120) |
| LichenRevealDistance | Metres within which goggles show the lichen; 0 = no limit (40) |
| HuldraCultivable | Huldra's Hair can be planted on trunks |
| CultivateMinutes | Minutes until a planted sprout is grown (240) |
| BaldrZoneChance, HelFernZoneChance | Chance per newly generated Mountains / Ashlands zone to get a plant group (0.2 / 0.1) |
| RemoveMisplacedWildPlants | Remove wild plants in water or on lava when their zone loads |
| GroundRegrowMinutes | Minutes until a picked ground plant is ripe again (240) |
| YieldT1, YieldT2, YieldT3 | Items per pick as min-max (1-2, 1-1, 2-4) |

## [Goggles]

| Key | Meaning |
|---|---|
| RecipeT1, RecipeT2, RecipeT3 | Goggle recipes |
| RevealHiddenPlayers | Allfather's Eye shows Shadow Veil players |

## Recipe strings

Format: `Item:Amount,Item:Amount` using prefab names, for example `Honey:10,Thistle:5,VeilIngredient_T1:2`. Recipes are read once at startup from the local file. A customised mead recipe without the tier's veil ingredient is kept as is and a warning is logged (the mead is then craftable without plants (?)).

## [Fog], [Fog.Tier1-3], [Veil] (look)

Purely visual, client-side. The first entry of `[Fog]` explains the fog layers in the file. Per tier you can change: the body cloud (`Enabled`, `Rate`, `Size`, `Lifetime`, `Alpha`), the wide fog around you (`OuterEnabled`, `OuterRadius`, `OuterAlpha`, `OuterRate`, `OuterSize`, `OuterLifetime`, `OuterHeightSigma`), the ground fog (`GroundEnabled`, `GroundAlpha`, `GroundRadius`, `GroundLifetime`), the colour (`Color` as r,g,b, `DynamicColor` to follow weather and night) and the glow (`Emission`). `[Veil]` holds the colours of the body modes (`GhostColor`, `ShadowColor`, `SpiritColor`, ...). Every key has a description in the file.

## Migration of defaults

When a new version changes a default, values still at the **old default** move to the new one once. Values you changed are kept. The "revision" keys (`GameplayDefaultsRevision`, `PlantDefaultsRevision`, `[Fog] LookDefaultsRevision`) record which version of the defaults your file has. Do not edit them.

The **look** migration is stronger: when the fog defaults get a new revision, the `[Fog.Tier1]` and `[Fog.Tier2]` sections are reset **completely**, including values you tuned. Tier III is not touched. Each replaced value is written to `BepInEx/LogOutput.log` ("Config migration: ... is reset"), so you can copy your values back. To keep a custom look, note it down before updating.
