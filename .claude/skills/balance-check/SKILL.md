---
name: balance-check
description: Check the mod's tuning values (veil tiers, cooldowns, Veil Broken, carry weight, plant rates, yields, recipes) against their stated targets and vanilla.
disable-model-invocation: true
argument-hint: "[veil | economy | plants | progression | all]"
---

<!-- Adapted from Claude Code Game Studios, .claude/skills/balance-check (MIT, (c) 2026 Donchitos,
     https://github.com/Donchitos/Claude-Code-Game-Studios): value-against-target analysis, degenerate strategies, the
     NOT ASSESSED rule and the health verdict, rewritten for this mod's data. -->

# Balance check

No argument is `all`. The check judges the **defaults** shipped in the code; a server's own config file can differ, and is
checked only when the user hands it over (then say which file).

## 1. Collect values and targets

**Values**, read from the code (each with file:line):

- Tiers: `BindTier(...)` calls in `InvisibilityPotion/Config/PluginConfig.cs` (duration, stealth, noise, ignored by enemies,
  hidden from players, AggroLossTime, RehideDelay, carry weight); `GameplayDefaults` in `Config/TierConfig.Core.cs` (Cooldown,
  Veil Broken multipliers, speed, DebuffDuration).
- Plants: `PlantDefaults` in `Plants/PlantDefaults.Core.cs` (zone and lichen chances), `PlantYield.DefaultYields`,
  `[Plants]` minutes in `Config/PluginConfig.Plants.cs`.
- Recipes: `MeadRecipes.NewDefault` (mead bases), `GoggleLevel.DefaultRecipe` (goggles, with their crafting stations in
  `Items/GoggleItems.cs`).

**Targets**, in this order: the user's recorded decisions (comments that cite a ruling or a date, the CHANGELOG, the design
specs in `docs/superpowers/specs/`), then vanilla references written in the code or `docs/decompile-notes.md` (thistle regrow
240 min, the vanilla carrot/turnip seed rates, the vanilla mead cooldowns logged at startup). A value with no target anywhere is
listed as **not judged**, never as fine.

If a domain has no values to read, its section is **NOT ASSESSED** with what is missing. A check that could not run is reported
as such.

## 2. Analyse

Every value against its target first (outside the target = outlier, with both numbers). Then per domain:

- **Veil**: uptime per tier (duration against Cooldown, upgrade rule in `Patches/ConsumePatches.cs`), the seconds a reveal costs
  (RehideDelay, Veil Broken length and strength), whether a lower tier is ever the better choice, whether tier III's extra
  (hidden from players) is paid for by its cost.
- **Economy**: ingredient per pick (`YieldTN`, scaled by plant size) against the recipe cost (2 per base); picks and world
  minutes per mead; how many minutes of invisibility one plant buys at each tier.
- **Plants**: rarity against the vanilla rates the defaults cite; regrow and cultivation times against thistle; whether
  cultivation makes the wild plants pointless.
- **Progression**: the station and biome a tier needs (goggles at forge / forge 3 / black forge 2, plants in Black Forest /
  Mountains / Ashlands) against the power the tier gives at that stage.

A **degenerate strategy** is one choice that beats every alternative or removes the cost the design intends: chaining meads
into permanent invisibility, a debuff that never bites, a farm that makes the rare ingredient common. Show the arithmetic.

## 3. Report

```
## Balance check: <domains>
Sources: <files read, and the server config if one was given>
Verdict: CRITICAL ISSUES | CONCERNS | NOT ASSESSED | HEALTHY
Outliers:            value | target | actual | source
Degenerate strategies: description and arithmetic
Not judged:          values without a stated target
Recommendations:     priority | change | effect | config key
```

Verdict, worst first: **CRITICAL ISSUES** (a degenerate strategy), **CONCERNS** (outliers a tuning pass fixes),
**NOT ASSESSED** (a domain without data, or values that could not be judged; name them), **HEALTHY** (every value judged against
a target and inside it).

## 4. After the report

Changing a default is a design decision: name the config key, the old and new value, and wait for the user's choice. A
changed default needs a migration revision (`GameplayDefaults.Revision` / `PlantDefaults.Revision` with a `Migrate` step and its
test) so existing config files that still hold the old default move along, and a CHANGELOG line naming the key.
