# InvisibilityPotion

Three veil meads for Valheim. Drink one and a fog veil settles over you: enemies notice you less (tier I) or lose you completely (tier II and III), and the strongest mead hides you from other players too. The ingredients grow on plants that nobody can see without veil goggles.

**The mod must be installed on the server and on every client** (Jötunn refuses mismatched clients).

## The meads

| Mead | Duration | Enemies | Other players | After a reveal | Carry weight |
|---|---|---|---|---|---|
| Faint Veil Mead (I) | 60 s | notice you far less (a quarter of the usual visibility and noise), even standing | see you | the veil ends | 75 % |
| Deep Veil Mead (II) | 120 s | cannot see or hear you; sleeping monsters stay asleep, raids ignore you | see you | hidden again after 12 s without attacking | 60 % |
| Shadow Veil Mead (III) | 180 s | as tier II | no nameplate, no map pin, no real position | hidden again after 8 s | 50 % |

- Brew the mead base at the **Mead Ketill** (honey, thistle, bloodbag for II/III, Ymir flesh for III, and 2 of the tier's veil ingredient), then ferment it.
- A chasing enemy gives up after a short while once it loses you.
- You cannot drink a weaker or equal mead while a stronger veil is active.
- **Veil Cooldown**: drinking a veil mead starts one shared cooldown (tier I 30 s, II 60 s, III 90 s, counted from drinking) that blocks all three meads until it runs out; it is shorter than the veil itself, so a stronger mead can still replace a running veil once the cooldown has passed. `[TierN] Cooldown`, 0 = off.

### What reveals you

- Dealing damage, taking damage, blocking or parrying, drawing a bow, casting with a staff.
- Tool use: swinging an axe or pickaxe (at anything, even air), building with the hammer, using the hoe or the cultivator. Picking plants by hand does not reveal you.
- A reveal gives the **Veil Broken** debuff until the veil returns (tier II/III: `RehideDelay`, tier I: 20 s, restarted on every reveal): stamina regeneration 10 %, eitr regeneration 15 %, health regeneration 35 %, movement 50 % slower (tier I 35 %), and no sprinting. The veil is for sneaking past, not for fighting.
- Breaking your own veil by acting (hitting, drawing a bow, casting, using a tool) also empties your stamina bar (`DrainStaminaOnAttackReveal`). Taking damage or blocking does not.

Each trigger can be switched off in the config (`RevealOnDamage`, `RevealOnBlock`, `RevealOnBowDraw`, `RevealOnToolUse`).

## Veil plants and goggles

| Tier | Plant | Where | Ingredient per pick |
|---|---|---|---|
| I | Huldra's Hair (lichen on the bark) | some Black Forest firs and pines | 1-2 Huldra's Hair |
| II | Baldr's Tear | Mountains | 1 Baldr's Tear |
| III | Hel's Ember Fern | Ashlands | 2-4 Hel's Ember Spore |

Veil plants are invisible unless you wear **veil goggles** (helmet slot) of at least their tier:

| Goggles | Crafted at | Shows |
|---|---|---|
| Watchman's Glass (I) | Forge | Huldra's Hair |
| Mimir's Glass (II) | Forge level 3, needs Watchman's Glass | tier I and II plants |
| Allfather's Eye (III) | Black forge level 2, needs Mimir's Glass | every veil plant, and players hidden by a Shadow Veil Mead (nameplate, veiled body, real position; never a map pin) |

Picked plants grow back about as fast as vanilla thistle (configurable).

### Cultivation

With the **Cultivator** (costs 1 of the ingredient):

- Huldra's Hair sprout: on the trunk of a fir or pine (aim at the bark).
- Baldr's Tear sprout: on cultivated ground in the Mountains.
- Hel's Ember Fern sprout: on cultivated ground in the Ashlands.

Sprouts are veil plants too: you need goggles to see them grow.

## Configuration

`BepInEx/config/saikopsych.InvisibilityPotion.cfg`. Gameplay values are **synced from the server and only admins can change them** in game.

Highlights:

- `[TierN]` `Duration`, `Cooldown`, `RehideDelay`, `AggroLossTime`, `CarryWeightMultiplier`, `DebuffStaminaRegenMultiplier`, `DebuffEitrRegenMultiplier`, `DebuffHealthRegenMultiplier`, `DebuffSpeedModifier`, `DebuffDuration`, `Recipe`.
- `[General]` the reveal switches above, `DrainStaminaOnAttackReveal`, `AllowPvpInvisibility` (server switch for tier III hiding from players).
- `[Goggles]` `RecipeT1..3`, `RevealHiddenPlayers`.
- `[Plants]` lichen chance per tree, zone chances for the ground plants, growth and regrowth times, yields.
- `[Fog.TierN]`, `[Veil]` the look of the veil (client side).

With [ConfigurationManager](https://thunderstore.io/c/valheim/p/shudnal/ConfigurationManager/) installed (F1 by default) you can edit the values in game.

## Known limitations

- Wild Baldr's Tear and Hel's Ember Fern spawn only in **newly generated zones**. In an existing world, explore new land (or cultivate them). Huldra's Hair on trees works in existing worlds.
- The server must run the mod: hiding a player's position from other players happens on the server.
- Features that need two players (tier III hiding from other players, goggles III revealing them, plants picked by another player) have only been tested on a single machine so far.
- Mead and goggle recipes are read once at startup from the local config file.
- Veil meads go on the horizontal item stand but not on the wall item stand, and mead bases on neither, like vanilla meads and mead bases.

## Source and license

MIT. Built with [BepInEx](https://github.com/BepInEx/BepInEx) and [Jötunn](https://github.com/Valheim-Modding/Jotunn).
