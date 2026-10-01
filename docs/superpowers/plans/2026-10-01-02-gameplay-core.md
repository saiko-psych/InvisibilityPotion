# Plan 2: Gameplay Core Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Three working invisibility tiers in singleplayer: placeholder potions, enemies that lose or ignore the player, reveal on attack with a stamina debuff, re-hide for tier II/III, a fog veil, and config values reloadable in-game.

**Architecture:** One BepInEx plugin (already built in plan 1). Pure logic (`*.Core.cs`: tier config, modifier math, recipe parser, state machine) has no game types and is unit-tested. `SE_Invisibility` owns the per-player state and writes two keys to the player's ZDO; every Harmony patch and the veil controller read only that ZDO state, so all peers behave the same. Patches are small classes under `Patches/`, one file per concern, each registered in the startup patch health check.

**Tech Stack:** C# net48, BepInEx 5.4.23.5, HarmonyX, Jötunn 2.30.2 (`CustomStatusEffect`, `CustomItem`, `CustomItemConversion`, `RenderManager`, `CommandManager`, config sync), xunit on net8.0, `make build` / `make run` / `make test` from plan 1.

**Spec:** `docs/superpowers/specs/2026-10-01-plan2-gameplay-core-design.md` (amendment, binding) on top of `docs/superpowers/specs/2026-09-30-invisibility-potion-design.md` (main spec). Verified game facts: `docs/decompile-notes.md` (cite it, do not re-derive).

## Global Constraints

- Plugin GUID `saikopsych.InvisibilityPotion`, name `InvisibilityPotion`; version stays `0.1.0` until plan 2 is merged, then `0.2.0` (Task 11).
- Targets: Valheim 1.0.16, BepInEx 5.4.23.5, Jötunn 2.30.2. Game member names must match `docs/decompile-notes.md`; anything not listed there is verified in `tools/decompiled/assembly_valheim/` before use.
- Everything in files is English. Conventional commits; the attribution trailer is whatever the harness provides.
- Dev-only code lives under `InvisibilityPotion/Dev/` inside `#if DEBUG`. Every dev console command writes its output through a `Say()` helper to both the console and `Plugin.Log`.
- Pure logic goes into `*.Core.cs` files (no `UnityEngine`, `BepInEx`, `Jotunn`, `HarmonyLib` or game types) linked into `InvisibilityPotion.Tests`.
- Every Harmony patch target is added to `Plugin.ExpectedPatchTargets` as `PatchHealth.TargetKey("DeclaringType", "Method")` using the declaring type.
- Never add or remove status effects from inside `StatusEffect.OnDamaged` or from any code that can run inside `SEMan.OnDamaged`; adds are allowed inside `UpdateStatusEffect`, removes are not (decompile-notes §"Status effect internals").
- Single cleanup path: `SE_Invisibility.Cleanup()` is idempotent and called from `Stop()` and `OnDestroy()`.
- The game is tested through `make run` (Steam launch, auto-join `testing`). Implementers cannot see the game; in-game steps are relayed to the user by the controller.
- Config defaults (main spec §5.2): Tier1 Duration 60, StealthModifier 0.25, NoiseModifier 0.25, IgnoredByEnemies false, AggroLossTime 5, RehideDelay 0 (attack ends the effect); Tier2 Duration 120, IgnoredByEnemies true, AggroLossTime 1, RehideDelay 12; Tier3 Duration 180, IgnoredByEnemies true, AggroLossTime 1, RehideDelay 8; all tiers DebuffStaminaRegenMultiplier 0.5, DebuffDuration 20, Cooldown 0. Global: RevealOnDamage true, RevealOnBlock true, RevealOnBowDraw true, ShowSelfFaintly true.

## Review Focus

1. A monster whose target is a hidden player but whose owner is another peer: the aggro drop must happen on the owner from ZDO state alone (Task 6 test: `HiddenState` is read, never the local `SE_Invisibility`).
2. Reveal arriving through `OnDamaged` while `SEMan.Update` is iterating: only a flag may change (Task 4 test: `MarkRevealed` sets a flag; the state machine applies it on the next `Tick`).
3. Drinking a lower tier while a higher one is active must leave the bottle in the inventory (Task 10: `CanConsumeItem` postfix, in-game check).
4. Config value `StealthModifier = 1.0` must mean "vanilla" and `0.25` must mean "a quarter of normal visibility" (Task 2 tests on `ModifierMath`).
5. A player who logs out while hidden must not leave a veil on their next login or a stale state on other clients (Task 9 stress test; `OnDestroy` cleanup in Task 4).

---

## File map

| Path | Responsibility |
|---|---|
| `docs/decisions/0005-hot-reload.md` | outcome of the ScriptEngine spike |
| `InvisibilityPotion/Config/TierConfig.Core.cs` | `TierConfig` value class, `ModifierMath`, `RecipeParser` (pure) |
| `InvisibilityPotion/Config/PluginConfig.cs` | BepInEx bindings with Jötunn admin-only sync, `Tiers`, `Global`, `Reload()` |
| `InvisibilityPotion/Effects/InvisibilityStateMachine.Core.cs` | phases, timers, pending reveal (pure) |
| `InvisibilityPotion/Effects/SE_Invisibility.cs` | status effect: state machine host, ZDO writes, debuff application, cleanup |
| `InvisibilityPotion/Effects/SE_Revealed.cs` | stamina-regen debuff |
| `InvisibilityPotion/Effects/StatusEffects.cs` | creates and registers the four effects with Jötunn; name hashes |
| `InvisibilityPotion/Net/HiddenState.cs` | ZDO keys `IP_Tier`/`IP_Hidden`, read with a short cache, owner-side write |
| `InvisibilityPotion/Patches/PerceptionPatches.cs` | `CanHearTarget`, `CanSeeTarget`, `FindEnemy` |
| `InvisibilityPotion/Patches/AggroPatches.cs` | `MonsterAI.UpdateTarget`, `MonsterAI.UpdateSleep` |
| `InvisibilityPotion/Patches/StealthPatches.cs` | `Player.UpdateStealth` (tier I standing) |
| `InvisibilityPotion/Patches/RevealPatches.cs` | `Character.Damage`, `Attack.StartDraw`, `Humanoid.StartAttack`, `Humanoid.BlockAttack` |
| `InvisibilityPotion/Patches/ConsumePatches.cs` | `Player.CanConsumeItem` refusal |
| `InvisibilityPotion/Patches/VisualPatches.cs` | `VisEquipment.UpdateLodgroup` re-veil |
| `InvisibilityPotion/Visuals/IVeil.cs`, `FogVeil.cs`, `VeilController.cs` | veil interface, fog implementation, per-player driver reading ZDO state |
| `InvisibilityPotion/Items/PotionItems.cs` | six cloned mead prefabs, recipes, fermenter conversions, icons |
| `InvisibilityPotion/Items/Localization.cs` + `Items/English.json` | item and message strings |
| `InvisibilityPotion/Dev/DevCommands.cs` | `ip_state` (extended), `ip_give`, `ip_spawn`, `ip_reload_config` |
| `InvisibilityPotion/Plugin.cs` | wiring: config, effects, items, veil controller, patch targets |
| `InvisibilityPotion.Tests/*Tests.cs` | tests for every `*.Core.cs` |
| `docs/testing.md` | plan-2 in-game checklist |

Branch: `plan-2/gameplay-core` from `main`.

---

### Task 1: Hot-reload spike (ScriptEngine) and ADR 0005

**Files:**
- Create: `docs/decisions/0005-hot-reload.md`
- Temporary (deleted at the end of the task unless the spike succeeds): `InvisibilityPotion.HotReloadProbe/InvisibilityPotion.HotReloadProbe.csproj`, `InvisibilityPotion.HotReloadProbe/ProbePlugin.cs`

**Interfaces:**
- Produces: a decision. If "works", Task 11 adds a follow-up task description to this plan for splitting patches into a reloadable assembly; nothing else in plan 2 changes.

- [ ] **Step 1: Install ScriptEngine into the game folder**

Download `https://github.com/BepInEx/BepInEx.Debug/releases/latest/download/ScriptEngine.zip` into the scratchpad, unzip, copy `ScriptEngine.dll` to `$VALHEIM_INSTALL/BepInEx/plugins/ScriptEngine/ScriptEngine.dll`. Create `$VALHEIM_INSTALL/BepInEx/scripts/`. After the first game start, set in `$VALHEIM_INSTALL/BepInEx/config/com.bepis.bepinex.scriptengine.cfg`: `EnableFileSystemWatcher = true`, `AutoReloadDelay = 3`.

- [ ] **Step 2: Write a throwaway probe plugin**

`InvisibilityPotion.HotReloadProbe/InvisibilityPotion.HotReloadProbe.csproj`: copy `InvisibilityPotion/InvisibilityPotion.csproj`, change `AssemblyName`/`RootNamespace` to `InvisibilityPotion.HotReloadProbe`, remove the `PublishToGame` target, add after the `JotunnLib` reference:

```xml
  <Target Name="DeployToScripts" AfterTargets="Build">
    <Copy SourceFiles="$(TargetDir)$(TargetFileName)" DestinationFolder="$(VALHEIM_INSTALL)/BepInEx/scripts" />
  </Target>
```

`ProbePlugin.cs`:

```csharp
using BepInEx;
using HarmonyLib;

namespace InvisibilityPotion.HotReloadProbe
{
    [BepInPlugin("saikopsych.InvisibilityPotion.HotReloadProbe", "HotReloadProbe", "0.0.1")]
    public class ProbePlugin : BaseUnityPlugin
    {
        public const string Marker = "PROBE_V1"; // bump to V2, V3 between reloads
        private Harmony _harmony;

        private void Awake()
        {
            _harmony = new Harmony("saikopsych.InvisibilityPotion.HotReloadProbe");
            _harmony.PatchAll(typeof(ProbePlugin).Assembly);
            Logger.LogInfo($"probe loaded {Marker}");
        }

        private void OnDestroy()
        {
            _harmony?.UnpatchSelf();
            Logger.LogInfo($"probe unloaded {Marker}");
        }
    }

    [HarmonyPatch(typeof(Player), nameof(Player.IsCrouching))]
    internal static class CrouchProbe
    {
        private static float _last;
        [HarmonyPostfix]
        private static void Postfix(Player __instance)
        {
            if (__instance != Player.m_localPlayer || UnityEngine.Time.time - _last < 5f) return;
            _last = UnityEngine.Time.time;
            UnityEngine.Debug.Log($"[probe] crouch poll {ProbePlugin.Marker}");
        }
    }
}
```

Add the probe project to `InvisibilityPotion.sln` temporarily or build it directly: `dotnet build InvisibilityPotion.HotReloadProbe -c Debug`.

- [ ] **Step 3: Run the probe (relayed to the user)**

The user runs `make run`, then in-game: confirm the log shows `probe loaded PROBE_V1` and `[probe] crouch poll PROBE_V1` every 5 s. Implementer changes `Marker` to `PROBE_V2`, rebuilds the probe project (the DLL lands in `BepInEx/scripts`), the user waits 3 s or presses F6. Expected: `probe unloaded PROBE_V1`, `probe loaded PROBE_V2`, poll lines now say V2 and appear once per 5 s (not twice: a doubled line means the old patch survived). Repeat with V3. The user also runs `ip_state`: `patched:` must not list the probe twice. Record the exact log lines.

- [ ] **Step 4: Write ADR 0005**

```markdown
# ADR 0005 – Hot reload of patch code

Status: accepted · 2026-10-0X

## Context
The user wants to avoid restarting the game for every code change. Jötunn content (items, status effects) is registered once at scene load and cannot be reloaded; Harmony patches can be unpatched and re-applied.

## Probe
BepInEx ScriptEngine rX with a throwaway plugin that patches `Player.IsCrouching` and logs a marker. Reloaded three times (V1→V2→V3).

## Result
<works | does not work>: <the observed log lines, whether the old patch was removed, any errors>.

## Decision
<If works:> Plan 2 keeps a single assembly for delivery; after plan 2 a bounded change moves `Patches/*` into `InvisibilityPotion.Patches.dll` loaded from `BepInEx/scripts`, with `Plugin` exposing `HiddenState`, `PluginConfig` and `StatusEffects` for it. `make run` deploys both.
<If not:> Single assembly stays. Fast iteration relies on `ip_reload_config`, `ip_give`, `ip_spawn` and the 40 s auto-join restart.

## Consequences
<one or two lines>
```

- [ ] **Step 5: Clean up and commit**

Delete `InvisibilityPotion.HotReloadProbe/` and the `BepInEx/scripts/*.dll` probe (leave ScriptEngine installed only if the result is "works"; otherwise remove `BepInEx/plugins/ScriptEngine/` too and say so in the ADR). Remove the probe from the `.sln` if it was added.

```bash
git add docs/decisions/0005-hot-reload.md
git commit -m "docs: ADR 0005 hot-reload spike result"
```

---

### Task 2: Tier config, modifier math, recipe parser, config binding and reload

**Files:**
- Create: `InvisibilityPotion/Config/TierConfig.Core.cs`
- Create: `InvisibilityPotion/Config/PluginConfig.cs`
- Create: `InvisibilityPotion.Tests/TierConfigTests.cs`
- Modify: `InvisibilityPotion.Tests/InvisibilityPotion.Tests.csproj` (link the Core file)
- Modify: `InvisibilityPotion/Plugin.cs` (create config in `Awake`)
- Modify: `InvisibilityPotion/Dev/DevCommands.cs` (`ip_reload_config`)

**Interfaces:**
- Produces:
  - `InvisibilityPotion.Config.TierConfig` (sealed class) with `int Tier; float Duration; float StealthModifier; float NoiseModifier; bool IgnoredByEnemies; float AggroLossTime; float RehideDelay; float DebuffStaminaRegenMultiplier; float DebuffDuration; float Cooldown; string Recipe;` and `bool EndsOnReveal => RehideDelay <= 0f;`
  - `InvisibilityPotion.Config.ModifierMath.ToAdditive(float fraction) : float` (0.25 → -0.75, 1 → 0)
  - `InvisibilityPotion.Config.RecipeParser.Parse(string recipe) : IReadOnlyList<(string item, int amount)>` throws `FormatException` on bad input
  - `InvisibilityPotion.Config.GlobalConfig` (sealed class) with `bool RevealOnDamage, RevealOnBlock, RevealOnBowDraw, ShowSelfFaintly; float FogCutoffLight, FogCutoffDense, FogCutoffSelf;`
  - `InvisibilityPotion.Config.PluginConfig` static: `void Bind(ConfigFile file)`, `TierConfig Tier(int tier)` (1..3, throws otherwise), `GlobalConfig Global`, `void Refresh()` (re-reads bound values into fresh `TierConfig`/`GlobalConfig` objects), `event Action Changed`.

- [ ] **Step 1: Write the failing tests**

`InvisibilityPotion.Tests/TierConfigTests.cs`:

```csharp
using System;
using InvisibilityPotion.Config;
using Xunit;

public class ModifierMathTests
{
    [Theory]
    [InlineData(1.0f, 0.0f)]
    [InlineData(0.25f, -0.75f)]
    [InlineData(0.0f, -1.0f)]
    [InlineData(1.5f, 0.5f)]
    public void ToAdditive_ConvertsFractionOfVanillaToVanillaDelta(float fraction, float expected)
    {
        Assert.Equal(expected, ModifierMath.ToAdditive(fraction), 5);
    }
}

public class RecipeParserTests
{
    [Fact]
    public void Parse_ReadsItemAmountPairs()
    {
        var r = RecipeParser.Parse("Honey:10,Thistle:5");
        Assert.Equal(2, r.Count);
        Assert.Equal(("Honey", 10), r[0]);
        Assert.Equal(("Thistle", 5), r[1]);
    }

    [Fact]
    public void Parse_TrimsWhitespaceAndIgnoresEmptyEntries()
    {
        var r = RecipeParser.Parse(" Honey : 10 , , Thistle:5 ,");
        Assert.Equal(2, r.Count);
        Assert.Equal(("Honey", 10), r[0]);
    }

    [Theory]
    [InlineData("Honey")]
    [InlineData("Honey:zero")]
    [InlineData("Honey:0")]
    [InlineData(":5")]
    public void Parse_ThrowsOnMalformedEntry(string recipe)
    {
        Assert.Throws<FormatException>(() => RecipeParser.Parse(recipe));
    }

    [Fact]
    public void Parse_EmptyRecipeGivesNoRequirements()
    {
        Assert.Empty(RecipeParser.Parse(""));
        Assert.Empty(RecipeParser.Parse(null));
    }
}

public class TierConfigTests
{
    [Fact]
    public void EndsOnReveal_IsTrueOnlyWhenRehideDelayIsZeroOrLess()
    {
        Assert.True(new TierConfig { Tier = 1, RehideDelay = 0f }.EndsOnReveal);
        Assert.False(new TierConfig { Tier = 2, RehideDelay = 12f }.EndsOnReveal);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(4)]
    public void Validate_RejectsTierOutsideOneToThree(int tier)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new TierConfig { Tier = tier, Duration = 1f }.Validate());
    }

    [Fact]
    public void Validate_RejectsNonPositiveDuration()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new TierConfig { Tier = 1, Duration = 0f }.Validate());
    }
}
```

Add to the test csproj ItemGroup: `<Compile Include="../InvisibilityPotion/Config/TierConfig.Core.cs" Link="TierConfig.Core.cs" />`.

- [ ] **Step 2: Run tests to verify they fail**

Run: `make test 2>&1 | tail -5`
Expected: build error, `TierConfig`/`ModifierMath`/`RecipeParser` not found.

- [ ] **Step 3: Write TierConfig.Core.cs**

```csharp
using System;
using System.Collections.Generic;

namespace InvisibilityPotion.Config
{
    /// <summary>Per-tier values as plain data. Built from BepInEx config entries by PluginConfig; no game types here.</summary>
    public sealed class TierConfig
    {
        public int Tier;
        public float Duration;
        public float StealthModifier = 1f;   // fraction of vanilla visibility, 1 = unchanged
        public float NoiseModifier = 1f;     // fraction of vanilla noise, 1 = unchanged
        public bool IgnoredByEnemies;
        public float AggroLossTime;
        public float RehideDelay;            // <= 0: an attack ends the effect
        public float DebuffStaminaRegenMultiplier = 1f;
        public float DebuffDuration;
        public float Cooldown;
        public string Recipe = "";

        public bool EndsOnReveal => RehideDelay <= 0f;

        public void Validate()
        {
            if (Tier < 1 || Tier > 3) throw new ArgumentOutOfRangeException(nameof(Tier), Tier, "tier must be 1..3");
            if (Duration <= 0f) throw new ArgumentOutOfRangeException(nameof(Duration), Duration, "duration must be > 0");
            if (AggroLossTime < 0f) throw new ArgumentOutOfRangeException(nameof(AggroLossTime));
            if (DebuffDuration < 0f) throw new ArgumentOutOfRangeException(nameof(DebuffDuration));
        }
    }

    public sealed class GlobalConfig
    {
        public bool RevealOnDamage = true;
        public bool RevealOnBlock = true;
        public bool RevealOnBowDraw = true;
        public bool ShowSelfFaintly = true;
        public float FogCutoffLight = 0.5f;
        public float FogCutoffDense = 0.8f;
        public float FogCutoffSelf = 0.3f;
    }

    public static class ModifierMath
    {
        /// <summary>Vanilla SE_Stats modifiers are additive fractions of the base value (stealth += base * m). A config fraction f of vanilla maps to m = f - 1.</summary>
        public static float ToAdditive(float fraction) => fraction - 1f;
    }

    public static class RecipeParser
    {
        /// <summary>Parses "Item:Amount,Item:Amount". Empty entries are ignored. Throws FormatException on anything else.</summary>
        public static IReadOnlyList<(string item, int amount)> Parse(string recipe)
        {
            var result = new List<(string, int)>();
            if (string.IsNullOrWhiteSpace(recipe)) return result;
            foreach (var raw in recipe.Split(','))
            {
                var entry = raw.Trim();
                if (entry.Length == 0) continue;
                var colon = entry.IndexOf(':');
                if (colon <= 0) throw new FormatException($"recipe entry '{entry}' must be Item:Amount");
                var item = entry.Substring(0, colon).Trim();
                var amountText = entry.Substring(colon + 1).Trim();
                if (item.Length == 0 || !int.TryParse(amountText, out var amount) || amount <= 0)
                    throw new FormatException($"recipe entry '{entry}' must be Item:Amount with Amount > 0");
                result.Add((item, amount));
            }
            return result;
        }
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `make test 2>&1 | tail -3`
Expected: all pass (16 existing + 12 new).

- [ ] **Step 5: Write PluginConfig.cs (game side)**

Check the attribute class first: `grep -c "ConfigurationManagerAttributes" ~/.nuget/packages/jotunnlib/2.30.2/lib/net462/Jotunn.xml` must be > 0 (namespace `Jotunn.Utils`).

```csharp
using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using Jotunn.Utils;

namespace InvisibilityPotion.Config
{
    /// <summary>Binds every config entry (server-synced via Jötunn, admin only) and exposes plain TierConfig/GlobalConfig snapshots.</summary>
    public static class PluginConfig
    {
        private static ConfigFile _file;
        private static readonly Dictionary<int, Dictionary<string, ConfigEntryBase>> _tierEntries = new Dictionary<int, Dictionary<string, ConfigEntryBase>>();
        private static readonly Dictionary<string, ConfigEntryBase> _globalEntries = new Dictionary<string, ConfigEntryBase>();
        private static readonly TierConfig[] _tiers = new TierConfig[4];

        public static GlobalConfig Global { get; private set; } = new GlobalConfig();
        public static event Action Changed;

        public static TierConfig Tier(int tier)
        {
            if (tier < 1 || tier > 3) throw new ArgumentOutOfRangeException(nameof(tier));
            return _tiers[tier];
        }

        public static void Bind(ConfigFile file)
        {
            _file = file;
            BindTier(1, 60f, 0.25f, 0.25f, false, 5f, 0f, "Honey:10,Thistle:5");
            BindTier(2, 120f, 1f, 1f, true, 1f, 12f, "Honey:10,Thistle:5,Bloodbag:3");
            BindTier(3, 180f, 1f, 1f, true, 1f, 8f, "Honey:10,Thistle:5,Bloodbag:3,YmirRemains:1");
            BindGlobal("RevealOnDamage", true, "Taking damage reveals a hidden player");
            BindGlobal("RevealOnBlock", true, "A blocked hit or parry reveals a hidden player");
            BindGlobal("RevealOnBowDraw", true, "Drawing a bow reveals a hidden player");
            BindGlobal("ShowSelfFaintly", true, "The hidden player still sees a faint version of themselves");
            BindGlobal("FogCutoffLight", 0.5f, "Alpha cutoff applied to the player's materials for the light veil (tier I)");
            BindGlobal("FogCutoffDense", 0.8f, "Alpha cutoff for the dense veil (tier II/III)");
            BindGlobal("FogCutoffSelf", 0.3f, "Alpha cutoff the hidden player sees on themselves");
            Refresh();
        }

        private static void BindTier(int tier, float duration, float stealth, float noise, bool ignored, float aggroLoss, float rehide, string recipe)
        {
            var section = $"Tier{tier}";
            var e = new Dictionary<string, ConfigEntryBase>
            {
                ["Duration"] = Bind(section, "Duration", duration, "Effect duration in seconds"),
                ["StealthModifier"] = Bind(section, "StealthModifier", stealth, "Fraction of vanilla visibility while hidden (1 = vanilla, 0.25 = a quarter). Tier I only."),
                ["NoiseModifier"] = Bind(section, "NoiseModifier", noise, "Fraction of vanilla noise while hidden (1 = vanilla). Tier I only."),
                ["IgnoredByEnemies"] = Bind(section, "IgnoredByEnemies", ignored, "Enemies cannot see or hear the player at all"),
                ["AggroLossTime"] = Bind(section, "AggroLossTime", aggroLoss, "Seconds a chasing enemy keeps searching before it gives up"),
                ["RehideDelay"] = Bind(section, "RehideDelay", rehide, "Seconds without attacking until hidden again; 0 = an attack ends the effect"),
                ["DebuffStaminaRegenMultiplier"] = Bind(section, "DebuffStaminaRegenMultiplier", 0.5f, "Stamina regeneration multiplier after revealing"),
                ["DebuffDuration"] = Bind(section, "DebuffDuration", 20f, "Debuff duration in seconds, restarted on every reveal"),
                ["Cooldown"] = Bind(section, "Cooldown", 0f, "Reserved; not used in plan 2"),
                ["Recipe"] = Bind(section, "Recipe", recipe, "Mead base recipe at the cauldron: Item:Amount,Item:Amount"),
            };
            _tierEntries[tier] = e;
        }

        private static void BindGlobal<T>(string key, T value, string description) =>
            _globalEntries[key] = Bind("General", key, value, description);

        private static ConfigEntry<T> Bind<T>(string section, string key, T value, string description) =>
            _file.Bind(section, key, value, new ConfigDescription(description, null, new ConfigurationManagerAttributes { IsAdminOnly = true }));

        private static T Get<T>(Dictionary<string, ConfigEntryBase> entries, string key) => ((ConfigEntry<T>)entries[key]).Value;

        /// <summary>Rebuilds the snapshots from the bound entries and raises Changed. Call after Config.Reload() or a sync.</summary>
        public static void Refresh()
        {
            for (var t = 1; t <= 3; t++)
            {
                var e = _tierEntries[t];
                var tc = new TierConfig
                {
                    Tier = t,
                    Duration = Get<float>(e, "Duration"),
                    StealthModifier = Get<float>(e, "StealthModifier"),
                    NoiseModifier = Get<float>(e, "NoiseModifier"),
                    IgnoredByEnemies = Get<bool>(e, "IgnoredByEnemies"),
                    AggroLossTime = Get<float>(e, "AggroLossTime"),
                    RehideDelay = Get<float>(e, "RehideDelay"),
                    DebuffStaminaRegenMultiplier = Get<float>(e, "DebuffStaminaRegenMultiplier"),
                    DebuffDuration = Get<float>(e, "DebuffDuration"),
                    Cooldown = Get<float>(e, "Cooldown"),
                    Recipe = Get<string>(e, "Recipe"),
                };
                tc.Validate();
                _tiers[t] = tc;
            }
            Global = new GlobalConfig
            {
                RevealOnDamage = Get<bool>(_globalEntries, "RevealOnDamage"),
                RevealOnBlock = Get<bool>(_globalEntries, "RevealOnBlock"),
                RevealOnBowDraw = Get<bool>(_globalEntries, "RevealOnBowDraw"),
                ShowSelfFaintly = Get<bool>(_globalEntries, "ShowSelfFaintly"),
                FogCutoffLight = Get<float>(_globalEntries, "FogCutoffLight"),
                FogCutoffDense = Get<float>(_globalEntries, "FogCutoffDense"),
                FogCutoffSelf = Get<float>(_globalEntries, "FogCutoffSelf"),
            };
            Changed?.Invoke();
        }

        /// <summary>Re-reads the file from disk and refreshes. Used by ip_reload_config.</summary>
        public static void Reload()
        {
            _file.Reload();
            Refresh();
        }
    }
}
```

- [ ] **Step 6: Wire into Plugin.cs and add `ip_reload_config`**

In `Plugin.Awake`, right after `Log = Logger;`, add `Config.SaveOnConfigSet = true; Config.PluginConfig.Bind(Config);` (the BepInEx `Config` property is the `ConfigFile`; use the full name `InvisibilityPotion.Config.PluginConfig.Bind(Config)` to avoid the namespace/property clash, or add `using Cfg = InvisibilityPotion.Config;`). Also subscribe to Jötunn's sync so refreshed values arrive after a server sync: `Jotunn.Managers.SynchronizationManager.OnConfigurationSynchronized += (s, e) => Cfg.PluginConfig.Refresh();`.

In `DevCommands.cs` add a command and register it:

```csharp
        private class ReloadConfigCommand : ConsoleCommand
        {
            public override string Name => "ip_reload_config";
            public override string Help => "InvisibilityPotion: re-read the config file; Duration applies to new effects only";

            public override void Run(string[] args)
            {
                try
                {
                    Config.PluginConfig.Reload();
                    var t2 = Config.PluginConfig.Tier(2);
                    Say($"config reloaded: T1 duration {Config.PluginConfig.Tier(1).Duration}s, T2 rehide {t2.RehideDelay}s, T3 aggro-loss {Config.PluginConfig.Tier(3).AggroLossTime}s");
                }
                catch (System.Exception e)
                {
                    Say($"config reload failed: {e.Message}");
                }
            }
        }
```

Move `Say` from `StateCommand` to a `private static void Say(string line)` on `DevCommands` so every command shares it.

- [ ] **Step 7: Build and verify the config file**

Run: `make build 2>&1 | tail -3` → 0 errors. Ask the user (via the controller) to run `make run`; then `cat "$VALHEIM_INSTALL/BepInEx/config/saikopsych.InvisibilityPotion.cfg"` must show sections `[General]`, `[Tier1]`, `[Tier2]`, `[Tier3]` with the defaults. In-game: edit `RehideDelay` under `[Tier2]` to `3`, run `ip_reload_config` → the line prints `T2 rehide 3s`.

- [ ] **Step 8: Commit**

```bash
git add InvisibilityPotion/Config InvisibilityPotion.Tests/TierConfigTests.cs InvisibilityPotion.Tests/InvisibilityPotion.Tests.csproj InvisibilityPotion/Plugin.cs InvisibilityPotion/Dev/DevCommands.cs
git commit -m "feat(config): tier config with server sync, modifier math, recipe parser, ip_reload_config"
```

---

### Task 3: Invisibility state machine (pure) with tests

**Files:**
- Create: `InvisibilityPotion/Effects/InvisibilityStateMachine.Core.cs`
- Create: `InvisibilityPotion.Tests/InvisibilityStateMachineTests.cs`
- Modify: `InvisibilityPotion.Tests/InvisibilityPotion.Tests.csproj`

**Interfaces:**
- Consumes: `TierConfig` (Task 2).
- Produces: `InvisibilityPotion.Effects.InvisibilityStateMachine` with
  `enum Phase { Hidden, Revealed, Ended }`,
  `InvisibilityStateMachine(TierConfig tier)`,
  `Phase Phase`, `float Elapsed`, `float RehideTimer`, `bool PendingReveal`,
  `void MarkRevealed()`,
  `StepResult Tick(float dt)`,
  `struct StepResult { bool ApplyDebuff, EnterHidden, EnterRevealed, End; }`.

- [ ] **Step 1: Write the failing tests**

```csharp
using InvisibilityPotion.Config;
using InvisibilityPotion.Effects;
using Xunit;

public class InvisibilityStateMachineTests
{
    private static TierConfig Tier1() => new TierConfig { Tier = 1, Duration = 60f, RehideDelay = 0f, DebuffDuration = 20f };
    private static TierConfig Tier2() => new TierConfig { Tier = 2, Duration = 120f, RehideDelay = 12f, DebuffDuration = 20f };

    [Fact]
    public void StartsHidden()
    {
        var sm = new InvisibilityStateMachine(Tier2());
        Assert.Equal(InvisibilityStateMachine.Phase.Hidden, sm.Phase);
        Assert.False(sm.PendingReveal);
    }

    [Fact]
    public void Tick_WithoutEvents_OnlyAdvancesElapsed()
    {
        var sm = new InvisibilityStateMachine(Tier2());
        var r = sm.Tick(1f);
        Assert.Equal(1f, sm.Elapsed);
        Assert.False(r.ApplyDebuff || r.EnterHidden || r.EnterRevealed || r.End);
    }

    [Fact]
    public void MarkRevealed_OnlySetsFlag_UntilNextTick()
    {
        var sm = new InvisibilityStateMachine(Tier2());
        sm.MarkRevealed();
        Assert.True(sm.PendingReveal);
        Assert.Equal(InvisibilityStateMachine.Phase.Hidden, sm.Phase);
    }

    [Fact]
    public void Tier1_RevealEndsEffectAndAppliesDebuff()
    {
        var sm = new InvisibilityStateMachine(Tier1());
        sm.MarkRevealed();
        var r = sm.Tick(0.02f);
        Assert.True(r.ApplyDebuff);
        Assert.True(r.End);
        Assert.Equal(InvisibilityStateMachine.Phase.Ended, sm.Phase);
        Assert.False(sm.PendingReveal);
    }

    [Fact]
    public void Tier2_RevealEntersRevealedAndStartsRehideTimer()
    {
        var sm = new InvisibilityStateMachine(Tier2());
        sm.MarkRevealed();
        var r = sm.Tick(0.02f);
        Assert.True(r.ApplyDebuff);
        Assert.True(r.EnterRevealed);
        Assert.False(r.End);
        Assert.Equal(InvisibilityStateMachine.Phase.Revealed, sm.Phase);
        Assert.Equal(12f, sm.RehideTimer, 3);
    }

    [Fact]
    public void Tier2_RehidesAfterDelayWithoutNewReveal()
    {
        var sm = new InvisibilityStateMachine(Tier2());
        sm.MarkRevealed();
        sm.Tick(0.02f);
        var r1 = sm.Tick(11.9f);
        Assert.False(r1.EnterHidden);
        var r2 = sm.Tick(0.2f);
        Assert.True(r2.EnterHidden);
        Assert.Equal(InvisibilityStateMachine.Phase.Hidden, sm.Phase);
    }

    [Fact]
    public void Tier2_RepeatedRevealRestartsTimerAndDebuff()
    {
        var sm = new InvisibilityStateMachine(Tier2());
        sm.MarkRevealed();
        sm.Tick(0.02f);
        sm.Tick(10f);
        sm.MarkRevealed();
        var r = sm.Tick(0.02f);
        Assert.True(r.ApplyDebuff);
        Assert.False(r.EnterRevealed);          // already revealed
        Assert.Equal(12f, sm.RehideTimer, 3);
    }

    [Fact]
    public void DurationKeepsRunningWhileRevealed()
    {
        var sm = new InvisibilityStateMachine(Tier2());
        sm.MarkRevealed();
        sm.Tick(0.02f);
        var r = sm.Tick(119.99f);
        Assert.True(r.End);
        Assert.Equal(InvisibilityStateMachine.Phase.Ended, sm.Phase);
    }

    [Fact]
    public void PendingRevealIsProcessedExactlyOnce()
    {
        var sm = new InvisibilityStateMachine(Tier2());
        sm.MarkRevealed();
        var first = sm.Tick(0.02f);
        var second = sm.Tick(0.02f);
        Assert.True(first.ApplyDebuff);
        Assert.False(second.ApplyDebuff);
    }

    [Fact]
    public void Ended_IgnoresFurtherEvents()
    {
        var sm = new InvisibilityStateMachine(Tier1());
        sm.MarkRevealed();
        sm.Tick(0.02f);
        sm.MarkRevealed();
        var r = sm.Tick(1f);
        Assert.False(r.ApplyDebuff || r.EnterHidden || r.EnterRevealed);
        Assert.True(r.End);
    }
}
```

Add `<Compile Include="../InvisibilityPotion/Effects/InvisibilityStateMachine.Core.cs" Link="InvisibilityStateMachine.Core.cs" />` to the test csproj.

- [ ] **Step 2: Run tests to verify they fail**

Run: `make test 2>&1 | tail -3` → build error, `InvisibilityStateMachine` not found.

- [ ] **Step 3: Write the state machine**

```csharp
using InvisibilityPotion.Config;

namespace InvisibilityPotion.Effects
{
    /// <summary>Phase and timer logic of one invisibility effect. Pure: the game side calls MarkRevealed from hooks and Tick from UpdateStatusEffect and acts on the returned flags.</summary>
    public sealed class InvisibilityStateMachine
    {
        public enum Phase { Hidden, Revealed, Ended }

        public struct StepResult
        {
            public bool ApplyDebuff;    // add or reset SE_Revealed
            public bool EnterHidden;    // write IP_Hidden = true
            public bool EnterRevealed;  // write IP_Hidden = false
            public bool End;            // the effect is over, let it expire
        }

        private readonly TierConfig _tier;

        public Phase Phase { get; private set; } = Phase.Hidden;
        public float Elapsed { get; private set; }
        public float RehideTimer { get; private set; }
        public bool PendingReveal { get; private set; }

        public InvisibilityStateMachine(TierConfig tier) { _tier = tier; }

        /// <summary>Safe to call from any hook, including OnDamaged: only sets a flag.</summary>
        public void MarkRevealed() { if (Phase != Phase.Ended) PendingReveal = true; }

        public StepResult Tick(float dt)
        {
            var r = new StepResult();
            if (Phase == Phase.Ended) { r.End = true; return r; }

            Elapsed += dt;

            if (PendingReveal)
            {
                PendingReveal = false;
                r.ApplyDebuff = true;
                if (_tier.EndsOnReveal)
                {
                    Phase = Phase.Ended;
                    r.End = true;
                    return r;
                }
                if (Phase == Phase.Hidden) { Phase = Phase.Revealed; r.EnterRevealed = true; }
                RehideTimer = _tier.RehideDelay;
            }
            else if (Phase == Phase.Revealed)
            {
                RehideTimer -= dt;
                if (RehideTimer <= 0f) { Phase = Phase.Hidden; RehideTimer = 0f; r.EnterHidden = true; }
            }

            if (Elapsed >= _tier.Duration) { Phase = Phase.Ended; r.End = true; }
            return r;
        }
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `make test 2>&1 | tail -3` → all pass (28 + 10).

- [ ] **Step 5: Commit**

```bash
git add InvisibilityPotion/Effects/InvisibilityStateMachine.Core.cs InvisibilityPotion.Tests/InvisibilityStateMachineTests.cs InvisibilityPotion.Tests/InvisibilityPotion.Tests.csproj
git commit -m "feat(effects): pure invisibility state machine with tests"
```

---

### Task 4: HiddenState (ZDO), SE_Invisibility, SE_Revealed, registration, `ip_give`, extended `ip_state`

**Files:**
- Create: `InvisibilityPotion/Net/HiddenState.cs`
- Create: `InvisibilityPotion/Effects/SE_Invisibility.cs`, `SE_Revealed.cs`, `StatusEffects.cs`
- Modify: `InvisibilityPotion/Plugin.cs`, `InvisibilityPotion/Dev/DevCommands.cs`

**Interfaces:**
- Consumes: `PluginConfig.Tier(int)`, `InvisibilityStateMachine`.
- Produces:
  - `InvisibilityPotion.Net.HiddenState`: `static (int tier, bool hidden) Get(Character c)`, `static bool IsIgnoredByEnemies(Character c)` (hidden and `Tier(t).IgnoredByEnemies`), `static int HiddenTier(Character c)` (0 when not hidden), `static void Write(Player owner, int tier, bool hidden)`, `static void Invalidate(ZDOID id)`.
  - `InvisibilityPotion.Effects.StatusEffects`: `static void Register()`, `static int NameHash(int tier)`, `static int RevealedHash`, `static string EffectName(int tier)` = `"SE_Invisibility_T{tier}"`.
  - `SE_Invisibility`: `int Tier`, `InvisibilityStateMachine Machine`, `void MarkRevealed(string reason)`, `static SE_Invisibility ActiveOn(Player p)` (null if none).
  - `enum RevealReason { DamageDealt, BowDraw, StaffCast, Block, DamageTaken, Command }` in `SE_Invisibility.cs`.

- [ ] **Step 1: Write HiddenState.cs**

```csharp
using System.Collections.Generic;
using InvisibilityPotion.Config;
using UnityEngine;

namespace InvisibilityPotion.Net
{
    /// <summary>The replicated "who is hidden" state. Written by the owning client from SE_Invisibility, read by every patch on every peer. Pattern: Player.IsDebugFlying (decompile-notes §ZDO).</summary>
    public static class HiddenState
    {
        public static readonly int HashTier = "IP_Tier".GetStableHashCode();
        public static readonly int HashHidden = "IP_Hidden".GetStableHashCode();

        private const float CacheSeconds = 0.2f;
        private struct Entry { public float Time; public int Tier; public bool Hidden; }
        private static readonly Dictionary<ZDOID, Entry> _cache = new Dictionary<ZDOID, Entry>();

        public static (int tier, bool hidden) Get(Character c)
        {
            if (c == null || !(c is Player)) return (0, false);
            var nview = c.m_nview;
            if (nview == null || !nview.IsValid()) return (0, false);
            var zdo = nview.GetZDO();
            var id = zdo.m_uid;
            var now = Time.time;
            if (_cache.TryGetValue(id, out var e) && now - e.Time < CacheSeconds) return (e.Tier, e.Hidden);
            var tier = zdo.GetInt(HashTier, 0);
            var hidden = zdo.GetBool(HashHidden, false);
            _cache[id] = new Entry { Time = now, Tier = tier, Hidden = hidden };
            return (tier, hidden);
        }

        public static int HiddenTier(Character c)
        {
            var (tier, hidden) = Get(c);
            return hidden ? tier : 0;
        }

        public static bool IsIgnoredByEnemies(Character c)
        {
            var tier = HiddenTier(c);
            return tier > 0 && PluginConfig.Tier(tier).IgnoredByEnemies;
        }

        /// <summary>Owner side only (SE_Invisibility runs only on the owner).</summary>
        public static void Write(Player owner, int tier, bool hidden)
        {
            var nview = owner?.m_nview;
            if (nview == null || !nview.IsValid()) return;
            var zdo = nview.GetZDO();
            zdo.Set(HashTier, tier);
            zdo.Set(HashHidden, hidden);
            Invalidate(zdo.m_uid);
        }

        public static void Invalidate(ZDOID id) => _cache.Remove(id);
    }
}
```

Verify `ZDO.m_uid` is public: `grep -n "public ZDOID m_uid" tools/decompiled/assembly_valheim/ZDO.cs`.

- [ ] **Step 2: Write SE_Revealed.cs**

```csharp
namespace InvisibilityPotion.Effects
{
    /// <summary>Stamina-regeneration debuff applied on every reveal. Values come from the tier that revealed; the effect is one shared prefab, so the values are set on the clone in Setup via RevealedParams.</summary>
    public class SE_Revealed : SE_Stats
    {
        /// <summary>Set by SE_Invisibility right before AddStatusEffect; read once in Setup. Single-threaded game loop, so a static handoff is safe.</summary>
        public static float NextMultiplier = 0.5f;
        public static float NextDuration = 20f;

        public override void Setup(Character character)
        {
            m_staminaRegenMultiplier = NextMultiplier;
            m_ttl = NextDuration;
            base.Setup(character);
        }

        public override void ResetTime()
        {
            m_staminaRegenMultiplier = NextMultiplier;
            m_ttl = NextDuration;
            base.ResetTime();
        }
    }
}
```

- [ ] **Step 3: Write SE_Invisibility.cs**

```csharp
using InvisibilityPotion.Config;
using InvisibilityPotion.Net;

namespace InvisibilityPotion.Effects
{
    public enum RevealReason { DamageDealt, BowDraw, StaffCast, Block, DamageTaken, Command }

    /// <summary>Owner-side status effect. Hosts the state machine, writes the ZDO state, applies the debuff. Visuals are driven separately from the ZDO by VeilController (Task 9).</summary>
    public class SE_Invisibility : SE_Stats
    {
        public int Tier;                       // set on the prefab by StatusEffects.Register
        public InvisibilityStateMachine Machine { get; private set; }
        private bool _cleaned;

        private TierConfig Cfg => PluginConfig.Tier(Tier);
        private Player Owner => m_character as Player;

        public static SE_Invisibility ActiveOn(Player p)
        {
            if (p == null) return null;
            foreach (var se in p.GetSEMan().GetStatusEffects())
                if (se is SE_Invisibility inv) return inv;
            return null;
        }

        public override void Setup(Character character)
        {
            var cfg = Cfg;
            m_ttl = cfg.Duration;
            m_stealthModifier = ModifierMath.ToAdditive(cfg.StealthModifier);
            m_noiseModifier = ModifierMath.ToAdditive(cfg.NoiseModifier);
            base.Setup(character);
            Machine = new InvisibilityStateMachine(cfg);
            _cleaned = false;
            var owner = character as Player;
            if (owner != null)
            {
                // A lower tier that is still active gives way; its Stop() runs Cleanup, so write our state afterwards.
                foreach (var se in owner.GetSEMan().GetStatusEffects().ToArray())
                    if (se is SE_Invisibility other && other != this) owner.GetSEMan().RemoveStatusEffect(other, true);
                HiddenState.Write(owner, Tier, true);
            }
            Plugin.Log.LogInfo($"SE_Invisibility T{Tier} started for {character?.GetHoverName()}");
        }

        public void MarkRevealed(RevealReason reason)
        {
            if (Machine == null) return;
            Machine.MarkRevealed();
            Plugin.Log.LogInfo($"T{Tier} reveal marked: {reason}");
        }

        /// <summary>Runs inside SEMan.OnDamaged's foreach: flag only.</summary>
        public override void OnDamaged(HitData hit, Character attacker)
        {
            if (PluginConfig.Global.RevealOnDamage) MarkRevealed(RevealReason.DamageTaken);
        }

        public override void UpdateStatusEffect(float dt)
        {
            base.UpdateStatusEffect(dt);
            if (Machine == null || Owner == null) return;
            var r = Machine.Tick(dt);
            if (r.ApplyDebuff)
            {
                var cfg = Cfg;
                SE_Revealed.NextMultiplier = cfg.DebuffStaminaRegenMultiplier;
                SE_Revealed.NextDuration = cfg.DebuffDuration;
                Owner.GetSEMan().AddStatusEffect(StatusEffects.RevealedHash, resetTime: true);   // add is safe inside SEMan.Update
            }
            if (r.EnterRevealed) HiddenState.Write(Owner, Tier, false);
            if (r.EnterHidden) HiddenState.Write(Owner, Tier, true);
            if (r.End) m_time = m_ttl;   // IsDone on the next SEMan.Update; vanilla then calls Stop()
        }

        public override void Stop()
        {
            base.Stop();
            Cleanup();
        }

        public override void OnDestroy()
        {
            base.OnDestroy();
            Cleanup();   // vanilla does not call Stop() on logout
        }

        /// <summary>The single cleanup path. Idempotent.</summary>
        public void Cleanup()
        {
            if (_cleaned) return;
            _cleaned = true;
            var owner = Owner;
            if (owner != null) HiddenState.Write(owner, 0, false);
            Plugin.Log.LogInfo($"SE_Invisibility T{Tier} cleaned up");
        }
    }
}
```

Check `SEMan.GetStatusEffects()` exists and returns `List<StatusEffect>`: `grep -n "GetStatusEffects" tools/decompiled/assembly_valheim/SEMan.cs`. Add `using System.Linq;` for `ToArray()`.

- [ ] **Step 4: Write StatusEffects.cs (registration)**

```csharp
using Jotunn.Entities;
using Jotunn.Managers;
using UnityEngine;

namespace InvisibilityPotion.Effects
{
    public static class StatusEffects
    {
        public const string RevealedName = "SE_IP_Revealed";
        public static readonly int RevealedHash = RevealedName.GetStableHashCode();

        public static string EffectName(int tier) => $"SE_Invisibility_T{tier}";
        public static int NameHash(int tier) => EffectName(tier).GetStableHashCode();

        public static void Register()
        {
            for (var t = 1; t <= 3; t++)
            {
                var se = ScriptableObject.CreateInstance<SE_Invisibility>();
                se.name = EffectName(t);
                se.Tier = t;
                se.m_name = $"$ip_se_name_t{t}";
                se.m_tooltip = $"$ip_se_tooltip_t{t}";
                se.m_startMessage = "$ip_se_start";
                se.m_stopMessage = "$ip_se_stop";
                se.m_startMessageType = MessageHud.MessageType.Center;
                se.m_stopMessageType = MessageHud.MessageType.Center;
                // no m_category: a shared category makes vanilla refuse tier upgrades (decompile-notes §Consume)
                ItemManager.Instance.AddStatusEffect(new CustomStatusEffect(se, fixReference: false));
            }
            var rev = ScriptableObject.CreateInstance<SE_Revealed>();
            rev.name = RevealedName;
            rev.m_name = "$ip_se_revealed_name";
            rev.m_tooltip = "$ip_se_revealed_tooltip";
            ItemManager.Instance.AddStatusEffect(new CustomStatusEffect(rev, fixReference: false));
        }
    }
}
```

Icons are added in Task 10 from the potion items. Verify field names `m_startMessage`, `m_stopMessage`, `m_startMessageType`, `m_tooltip` in `tools/decompiled/assembly_valheim/StatusEffect.cs`.

- [ ] **Step 5: Wire up and add `ip_give`, extend `ip_state`**

`Plugin.Awake`: after config binding, call `Effects.StatusEffects.Register();` (Jötunn accepts status-effect registration in `Awake`).

`DevCommands.cs`: register `GiveCommand` and extend `StateCommand.Run`:

```csharp
        private class GiveCommand : ConsoleCommand
        {
            public override string Name => "ip_give";
            public override string Help => "ip_give <1|2|3>: apply the invisibility tier effect to yourself";

            public override void Run(string[] args)
            {
                var p = Player.m_localPlayer;
                if (p == null) { Say("no local player"); return; }
                if (args.Length < 1 || !int.TryParse(args[0], out var tier) || tier < 1 || tier > 3) { Say(Help); return; }
                var se = p.GetSEMan().AddStatusEffect(Effects.StatusEffects.NameHash(tier), resetTime: true);
                Say(se != null ? $"applied T{tier}" : $"T{tier} not applied (already active or not registered)");
            }
        }
```

In `StateCommand.Run`, after the existing three lines:

```csharp
                var p = Player.m_localPlayer;
                if (p != null)
                {
                    var inv = Effects.SE_Invisibility.ActiveOn(p);
                    var (tier, hidden) = Net.HiddenState.Get(p);
                    Say(inv == null
                        ? $"self: no effect; zdo tier={tier} hidden={hidden}"
                        : $"self: T{inv.Tier} phase={inv.Machine.Phase} elapsed={inv.Machine.Elapsed:F1}s rehide={inv.Machine.RehideTimer:F1}s pending={inv.Machine.PendingReveal}; zdo tier={tier} hidden={hidden}");
                    var mine = ZDOMan.GetSessionID();
                    foreach (var c in Character.GetAllCharacters())
                    {
                        if (c == null || c.IsPlayer()) continue;
                        if (Vector3.Distance(c.transform.position, p.transform.position) > 30f) continue;
                        var ai = c.GetComponent<MonsterAI>();
                        if (ai == null) continue;
                        var owner = c.m_nview?.GetZDO()?.GetOwner() ?? 0;
                        var target = ai.GetTargetCreature();
                        Say($"  {c.name.Replace("(Clone)", "")}: target={(target == null ? "-" : target.GetHoverName())} unsensed={ai.m_timeSinceSensedTargetCreature:F1}s alerted={ai.IsAlerted()} owner={(owner == mine ? "local" : owner.ToString())}");
                    }
                }
```

Verify `ZDOMan.GetSessionID()` is public static: `grep -n "GetSessionID" tools/decompiled/assembly_valheim/ZDOMan.cs`.

- [ ] **Step 6: Build and in-game check (relayed)**

`make build` → 0 errors. User: `make run`, then `ip_give 2` → HUD message, log `SE_Invisibility T2 started`; `ip_state` → `self: T2 phase=Hidden ... zdo tier=2 hidden=True`; wait 120 s or `ip_give 3` (upgrade) → log shows T2 cleaned up then T3 started; `ip_state` → `zdo tier=3`.

- [ ] **Step 7: Commit**

```bash
git add InvisibilityPotion/Net InvisibilityPotion/Effects InvisibilityPotion/Plugin.cs InvisibilityPotion/Dev/DevCommands.cs
git commit -m "feat(effects): SE_Invisibility with ZDO state, SE_Revealed debuff, ip_give and extended ip_state"
```

---

### Task 5: Perception patches (tier II/III)

**Files:**
- Create: `InvisibilityPotion/Patches/PerceptionPatches.cs`
- Modify: `InvisibilityPotion/Plugin.cs` (`ExpectedPatchTargets`)

**Interfaces:**
- Consumes: `HiddenState.IsIgnoredByEnemies(Character)`.

- [ ] **Step 1: Write the patches**

```csharp
using HarmonyLib;
using InvisibilityPotion.Net;
using UnityEngine;

namespace InvisibilityPotion.Patches
{
    /// <summary>Tier II/III: enemies neither hear nor see the player. Patched where vanilla checks ghost mode (decompile-notes §Perception). Runs on the monster's owner.</summary>
    [HarmonyPatch]
    internal static class PerceptionPatches
    {
        [HarmonyPatch(typeof(BaseAI), nameof(BaseAI.CanHearTarget), new[] { typeof(Transform), typeof(float), typeof(Character) })]
        [HarmonyPrefix]
        private static bool CanHearTarget_Prefix(Character target, ref bool __result)
        {
            if (!HiddenState.IsIgnoredByEnemies(target)) return true;
            __result = false;
            return false;
        }

        [HarmonyPatch(typeof(BaseAI), nameof(BaseAI.CanSeeTarget), new[] { typeof(Transform), typeof(Vector3), typeof(float), typeof(float), typeof(bool), typeof(bool), typeof(Character) })]
        [HarmonyPrefix]
        private static bool CanSeeTarget_Prefix(Character target, ref bool __result)
        {
            if (!HiddenState.IsIgnoredByEnemies(target)) return true;
            __result = false;
            return false;
        }

        /// <summary>HuntPlayer monsters (raids, events) pick the closest player without any perception check (BaseAI.cs:1419).</summary>
        [HarmonyPatch(typeof(BaseAI), "FindEnemy")]
        [HarmonyPostfix]
        private static void FindEnemy_Postfix(ref Character __result)
        {
            if (__result != null && HiddenState.IsIgnoredByEnemies(__result)) __result = null;
        }
    }
}
```

`[HarmonyPatch]` on the class plus per-method attributes is the HarmonyX "multiple patches in one class" form; if `CreateClassProcessor` only sees the class-level attribute, split into three nested classes each with its own `[HarmonyPatch(...)]`, as `AutoJoin.cs` does.

- [ ] **Step 2: Register targets**

In `Plugin.ExpectedPatchTargets` add (outside `#if DEBUG`):

```csharp
            PatchHealth.TargetKey("BaseAI", "CanHearTarget"),
            PatchHealth.TargetKey("BaseAI", "CanSeeTarget"),
            PatchHealth.TargetKey("BaseAI", "FindEnemy"),
```

- [ ] **Step 3: Build and in-game check (relayed)**

`make build` → 0 errors; log on start: `Patch health: 4 targets patched, 0 missing`. User: `ip_spawn` does not exist yet, so use vanilla: F5 `devcommands`, `spawn Greydwarf 3`, let them aggro, then `ip_give 2`. Expected: they stop attacking within a few seconds, walk to the last position, and (for now) search for up to 30 s (Task 6 shortens that), `ip_state` lists them with `target=-` after 30 s. Chop a tree while hidden: they do not react.

- [ ] **Step 4: Commit**

```bash
git add InvisibilityPotion/Patches/PerceptionPatches.cs InvisibilityPotion/Plugin.cs
git commit -m "feat(patches): enemies cannot hear, see or hunt hidden tier II/III players"
```

---

### Task 6: Aggro loss after AggroLossTime and sleeping monsters

**Files:**
- Create: `InvisibilityPotion/Patches/AggroPatches.cs`
- Modify: `InvisibilityPotion/Plugin.cs`

**Interfaces:**
- Consumes: `HiddenState.HiddenTier`, `HiddenState.IsIgnoredByEnemies`, `PluginConfig.Tier`.

- [ ] **Step 1: Write the patches**

```csharp
using HarmonyLib;
using InvisibilityPotion.Config;
using InvisibilityPotion.Net;

namespace InvisibilityPotion.Patches
{
    /// <summary>Lose a hidden target after the tier's AggroLossTime instead of vanilla's inlined 30 s (MonsterAI.cs:336). Runs on the monster's owner from ZDO state only.</summary>
    [HarmonyPatch(typeof(MonsterAI), "UpdateTarget")]
    internal static class UpdateTargetPatch
    {
        [HarmonyPostfix]
        private static void Postfix(MonsterAI __instance)
        {
            var target = __instance.m_targetCreature;
            if (target == null) return;
            var tier = HiddenState.HiddenTier(target);
            if (tier == 0) return;
            if (__instance.m_timeSinceSensedTargetCreature <= PluginConfig.Tier(tier).AggroLossTime) return;
            // Same drop vanilla performs at 30 s.
            __instance.SetAlerted(false);
            __instance.m_targetCreature = null;
            __instance.m_targetStatic = null;
            __instance.m_timeSinceAttacking = 0f;
            __instance.m_updateTargetTimer = 5f;
        }
    }

    /// <summary>Tier II/III do not wake sleeping monsters. Vanilla wakes on the closest player in range without perception (MonsterAI.cs:817). Hunting monsters keep vanilla behaviour.</summary>
    [HarmonyPatch(typeof(MonsterAI), "UpdateSleep")]
    internal static class UpdateSleepPatch
    {
        [HarmonyPrefix]
        private static bool Prefix(MonsterAI __instance, float dt)
        {
            if (!__instance.IsSleeping() || __instance.HuntPlayer()) return true;
            var range = __instance.m_wakeupRange > __instance.m_maxNoiseWakeupRange ? __instance.m_wakeupRange : __instance.m_maxNoiseWakeupRange;
            if (range <= 0f) return true;
            var closest = Player.GetClosestPlayer(__instance.transform.position, range);
            if (closest == null || !HiddenState.IsIgnoredByEnemies(closest)) return true;
            // Closest player is hidden: skip this tick's wake-up checks but keep the sleep timer running like vanilla.
            __instance.m_sleepTimer += dt;
            return false;
        }
    }
}
```

`SetAlerted` is `protected virtual` on `BaseAI`; with the publicized assembly it is callable. Verify field names `m_wakeupRange`, `m_maxNoiseWakeupRange`, `m_sleepTimer`, `m_targetStatic`, `m_timeSinceAttacking`, `m_updateTargetTimer` exist in `MonsterAI.cs` (decompile-notes §Aggro lists them).

- [ ] **Step 2: Register targets**

```csharp
            PatchHealth.TargetKey("MonsterAI", "UpdateTarget"),
            PatchHealth.TargetKey("MonsterAI", "UpdateSleep"),
```

- [ ] **Step 3: Build and in-game check (relayed)**

`make build` → 0 errors, `Patch health: 6 targets patched`. User: `devcommands`, `spawn Troll`, let it chase, `ip_give 2` → within about 1 s (`AggroLossTime` 1) the troll stops, `ip_state` shows `target=-`; walk away. Sleeping check: `spawn Draugr` near a crypt is awake; instead use `ip_give 2` then approach a Draugr that is sleeping in a Swamp crypt or spawn `Draugr` and run `spawn Draugr_Elite` … if no sleeper is available, record the test as pending for the test matrix in `docs/testing.md`.

- [ ] **Step 4: Commit**

```bash
git add InvisibilityPotion/Patches/AggroPatches.cs InvisibilityPotion/Plugin.cs
git commit -m "feat(patches): aggro loss after AggroLossTime, hidden tier II/III do not wake sleepers"
```

---

### Task 7: Tier I visibility while standing

**Files:**
- Create: `InvisibilityPotion/Patches/StealthPatches.cs`
- Modify: `InvisibilityPotion/Plugin.cs`

- [ ] **Step 1: Write the patch**

```csharp
using HarmonyLib;
using InvisibilityPotion.Config;
using InvisibilityPotion.Net;

namespace InvisibilityPotion.Patches
{
    /// <summary>Vanilla applies stealth modifiers only while crouching (Player.cs:6961) and resets the target to 1 when standing (Player.cs:6966). For a hidden tier I player we keep the target at the configured fraction while standing. Runs on the player's owner; the factor reaches monster owners via ZDOVars.s_stealth.</summary>
    [HarmonyPatch(typeof(Player), "UpdateStealth")]
    internal static class UpdateStealthPatch
    {
        [HarmonyPostfix]
        private static void Postfix(Player __instance)
        {
            if (__instance.IsCrouching()) return;
            var tier = HiddenState.HiddenTier(__instance);
            if (tier == 0) return;
            var cfg = PluginConfig.Tier(tier);
            if (cfg.IgnoredByEnemies) return;               // tier II/III are handled by the perception patches
            var fraction = cfg.StealthModifier;
            if (__instance.m_stealthFactorTarget > fraction) __instance.m_stealthFactorTarget = fraction;
        }
    }
}
```

The factor then moves towards the target at 0.25/s (vanilla, Player.cs:6969): hiding takes about 3 s, which matches the main spec's "lose aggro faster than vanilla" for tier I.

- [ ] **Step 2: Register target**

`PatchHealth.TargetKey("Player", "UpdateStealth"),`

- [ ] **Step 3: Build and in-game check (relayed)**

`make build` → 0 errors. User: stand still in the open near Greydwarfs (spawned 20 m away), `ip_give 1`; after about 3 s the vanilla stealth meter/behaviour: they should not notice the player until within a quarter of their normal view range. `ip_state` on self shows `T1 phase=Hidden`. Chop a tree: they do not react (tier I noise ×0.25). Attack one: the effect ends (`cleaned up` in the log) and the stamina bar regenerates slowly for 20 s.

- [ ] **Step 4: Commit**

```bash
git add InvisibilityPotion/Patches/StealthPatches.cs InvisibilityPotion/Plugin.cs
git commit -m "feat(patches): tier I reduces visibility while standing"
```

---

### Task 8: Reveal triggers

**Files:**
- Create: `InvisibilityPotion/Patches/RevealPatches.cs`
- Modify: `InvisibilityPotion/Plugin.cs`

**Interfaces:**
- Consumes: `SE_Invisibility.ActiveOn(Player)`, `MarkRevealed(RevealReason)`, `PluginConfig.Global`.

- [ ] **Step 1: Write the patches**

```csharp
using HarmonyLib;
using InvisibilityPotion.Config;
using InvisibilityPotion.Effects;

namespace InvisibilityPotion.Patches
{
    /// <summary>All reveal triggers run on the hidden player's own client and only call MarkRevealed (decompile-notes §Reveal triggers).</summary>
    internal static class Reveal
    {
        internal static void Mark(Player p, RevealReason reason)
        {
            if (p == null || p != Player.m_localPlayer) return;
            SE_Invisibility.ActiveOn(p)?.MarkRevealed(reason);
        }
    }

    /// <summary>Damaging a creature. Character.Damage runs on the attacker's machine before the RPC (Character.cs:2232).</summary>
    [HarmonyPatch(typeof(Character), nameof(Character.Damage))]
    internal static class DamagePatch
    {
        [HarmonyPrefix]
        private static void Prefix(Character __instance, HitData hit)
        {
            if (__instance == null || hit == null) return;
            var attacker = hit.GetAttacker() as Player;
            if (attacker == null || attacker == __instance) return;
            Reveal.Mark(attacker, RevealReason.DamageDealt);
        }
    }

    /// <summary>Drawing a bow (Attack.cs:346, called from Player.UpdateAttackBowDraw).</summary>
    [HarmonyPatch(typeof(Attack), nameof(Attack.StartDraw))]
    internal static class StartDrawPatch
    {
        [HarmonyPostfix]
        private static void Postfix(Humanoid character, bool __result)
        {
            if (!__result || !PluginConfig.Global.RevealOnBowDraw) return;
            Reveal.Mark(character as Player, RevealReason.BowDraw);
        }
    }

    /// <summary>Starting a staff cast. Melee swings are not revealed here (they reveal on hit via DamagePatch), so harvesting stays silent.</summary>
    [HarmonyPatch(typeof(Humanoid), nameof(Humanoid.StartAttack))]
    internal static class StartAttackPatch
    {
        [HarmonyPostfix]
        private static void Postfix(Humanoid __instance, bool __result)
        {
            if (!__result) return;
            var p = __instance as Player;
            if (p == null) return;
            var weapon = p.GetCurrentWeapon();
            var skill = weapon?.m_shared?.m_skillType;
            if (skill == Skills.SkillType.ElementalMagic || skill == Skills.SkillType.BloodMagic)
                Reveal.Mark(p, RevealReason.StaffCast);
        }
    }

    /// <summary>A blocked hit or parry (Humanoid.cs:1751, runs inside RPC_Damage on the victim's owner).</summary>
    [HarmonyPatch(typeof(Humanoid), "BlockAttack")]
    internal static class BlockAttackPatch
    {
        [HarmonyPostfix]
        private static void Postfix(Humanoid __instance, bool __result)
        {
            if (!__result || !PluginConfig.Global.RevealOnBlock) return;
            Reveal.Mark(__instance as Player, RevealReason.Block);
        }
    }
}
```

Verify: `Humanoid.GetCurrentWeapon()` is public (`grep -n "public ItemDrop.ItemData GetCurrentWeapon" tools/decompiled/assembly_valheim/Humanoid.cs`), and the skill enum names `ElementalMagic`, `BloodMagic` exist in `Skills.cs`. If `BlockAttack` runs inside `RPC_Damage` after `SEMan.OnDamaged`'s `foreach` has finished, `MarkRevealed` (flag only) is safe regardless.

- [ ] **Step 2: Register targets**

```csharp
            PatchHealth.TargetKey("Character", "Damage"),
            PatchHealth.TargetKey("Attack", "StartDraw"),
            PatchHealth.TargetKey("Humanoid", "StartAttack"),
            PatchHealth.TargetKey("Humanoid", "BlockAttack"),
```

- [ ] **Step 3: Build and in-game check (relayed)**

`make build` → 0 errors, `Patch health: 11 targets patched`. User, with `ip_give 2` each time: (a) hit a Greydwarf → log `reveal marked: DamageDealt`, `ip_state` phase=Revealed, stamina regen slowed, after 12 s phase=Hidden again and the Greydwarf loses interest; (b) draw a bow without shooting → `BowDraw`; (c) let a Greydwarf hit you with the shield up → `Block`; (d) take a hit → `DamageTaken`; (e) chop a tree, open a chest, swing at air → no reveal line. With `ip_give 1`: hitting a Greydwarf ends the effect.

- [ ] **Step 4: Commit**

```bash
git add InvisibilityPotion/Patches/RevealPatches.cs InvisibilityPotion/Plugin.cs
git commit -m "feat(patches): reveal on creature hit, bow draw, staff cast, blocked hit and damage taken"
```

---

### Task 9: Fog veil, veil controller, re-veil on equipment change, cleanup stress test

**Files:**
- Create: `InvisibilityPotion/Visuals/IVeil.cs`, `InvisibilityPotion/Visuals/FogVeil.cs`, `InvisibilityPotion/Visuals/VeilController.cs`
- Create: `InvisibilityPotion/Patches/VisualPatches.cs`
- Modify: `InvisibilityPotion/Plugin.cs`

**Interfaces:**
- Produces: `IVeil { void Apply(Player p, int tier, bool isLocal); void Remove(Player p); }`, `FogVeil : IVeil`, `VeilController : MonoBehaviour` (added to the plugin GameObject; polls every 0.25 s; `static void ForceRefresh(Player p)`).
- Consumes: `HiddenState.HiddenTier`, `PluginConfig.Global`.

- [ ] **Step 1: Write IVeil.cs and FogVeil.cs**

```csharp
namespace InvisibilityPotion.Visuals
{
    public interface IVeil
    {
        void Apply(Player p, int tier, bool isLocal);
        void Remove(Player p);
    }
}
```

```csharp
using System.Collections.Generic;
using InvisibilityPotion.Config;
using UnityEngine;

namespace InvisibilityPotion.Visuals
{
    /// <summary>Thins the player's materials via the Custom/Player _Cutoff property and attaches a fog particle. Restores the exact snapshot on Remove.</summary>
    public sealed class FogVeil : IVeil
    {
        private static readonly int CutoffId = Shader.PropertyToID("_Cutoff");
        private const string FogPrefabName = "vfx_ghost_smoke"; // placeholder picked in step 2; replaced by an asset bundle in plan 4

        private sealed class Snapshot
        {
            public readonly Dictionary<Material, float> Cutoffs = new Dictionary<Material, float>();
            public GameObject Fog;
            public int Tier;
        }
        private readonly Dictionary<Player, Snapshot> _snapshots = new Dictionary<Player, Snapshot>();

        public void Apply(Player p, int tier, bool isLocal)
        {
            if (p == null) return;
            if (_snapshots.TryGetValue(p, out var existing) && existing.Tier == tier) { ApplyCutoff(p, existing, tier, isLocal); return; }
            Remove(p);
            var snap = new Snapshot { Tier = tier };
            _snapshots[p] = snap;
            ApplyCutoff(p, snap, tier, isLocal);
            var prefab = ZNetScene.instance?.GetPrefab(FogPrefabName);
            if (prefab != null)
            {
                snap.Fog = Object.Instantiate(prefab, p.transform);
                snap.Fog.transform.localPosition = new Vector3(0f, 1f, 0f);
                var ps = snap.Fog.GetComponentInChildren<ParticleSystem>();
                if (ps != null) { var main = ps.main; main.loop = true; }
            }
        }

        private static void ApplyCutoff(Player p, Snapshot snap, int tier, bool isLocal)
        {
            var g = PluginConfig.Global;
            var cutoff = isLocal && g.ShowSelfFaintly ? g.FogCutoffSelf : (tier == 1 ? g.FogCutoffLight : g.FogCutoffDense);
            foreach (var r in p.GetComponentsInChildren<Renderer>(true))
            {
                foreach (var m in r.materials)   // instance materials, never sharedMaterials: vanilla players share them
                {
                    if (m == null || !m.HasProperty(CutoffId)) continue;
                    if (!snap.Cutoffs.ContainsKey(m)) snap.Cutoffs[m] = m.GetFloat(CutoffId);
                    m.SetFloat(CutoffId, cutoff);
                }
            }
        }

        public void Remove(Player p)
        {
            if (p == null || !_snapshots.TryGetValue(p, out var snap)) return;
            foreach (var kv in snap.Cutoffs) if (kv.Key != null) kv.Key.SetFloat(CutoffId, kv.Value);
            if (snap.Fog != null) Object.Destroy(snap.Fog);
            _snapshots.Remove(p);
        }
    }
}
```

- [ ] **Step 2: Pick the placeholder fog prefab**

In-game (relayed): the user runs a one-off dev command added for this task, `ip_prefabs <substring>`, which lists `ZNetScene.instance.m_prefabs` names containing the substring (`fog`, `smoke`, `mist`, `ghost`) through `Say`. Pick one that is a looping particle effect (prefer a `vfx_` or `sfx`-free name). Set `FogPrefabName` to it, keep the command (useful later).

- [ ] **Step 3: Write VeilController.cs**

```csharp
using System.Collections.Generic;
using InvisibilityPotion.Net;
using UnityEngine;

namespace InvisibilityPotion.Visuals
{
    /// <summary>Drives the veil for every player from ZDO state, so local and remote players look the same. Attached to the plugin GameObject.</summary>
    public sealed class VeilController : MonoBehaviour
    {
        private static VeilController _instance;
        private readonly IVeil _veil = new FogVeil();
        private readonly HashSet<Player> _veiled = new HashSet<Player>();
        private float _timer;

        private void Awake() { _instance = this; }

        private void Update()
        {
            _timer += Time.deltaTime;
            if (_timer < 0.25f) return;
            _timer = 0f;
            Refresh();
        }

        public static void ForceRefresh(Player p)
        {
            if (_instance == null || p == null) return;
            _instance._veil.Remove(p);
            _instance._veiled.Remove(p);
            _instance.Refresh();
        }

        private void Refresh()
        {
            var seen = new HashSet<Player>();
            foreach (var p in Player.GetAllPlayers())
            {
                if (p == null) continue;
                seen.Add(p);
                var tier = HiddenState.HiddenTier(p);
                if (tier > 0) { _veil.Apply(p, tier, p == Player.m_localPlayer); _veiled.Add(p); }
                else if (_veiled.Remove(p)) _veil.Remove(p);
            }
            _veiled.RemoveWhere(p => { if (p == null || !seen.Contains(p)) { _veil.Remove(p); return true; } return false; });
        }
    }
}
```

In `Plugin.Awake` add `gameObject.AddComponent<Visuals.VeilController>();`. Verify `Player.GetAllPlayers()` is public static (`grep -n "GetAllPlayers" tools/decompiled/assembly_valheim/Player.cs`).

- [ ] **Step 4: Re-veil on equipment change**

`InvisibilityPotion/Patches/VisualPatches.cs`:

```csharp
using HarmonyLib;
using InvisibilityPotion.Visuals;

namespace InvisibilityPotion.Patches
{
    /// <summary>UpdateLodgroup runs only when equipment actually changed (VisEquipment.cs:831); new renderers need the veil again.</summary>
    [HarmonyPatch(typeof(VisEquipment), "UpdateLodgroup")]
    internal static class UpdateLodgroupPatch
    {
        [HarmonyPostfix]
        private static void Postfix(VisEquipment __instance)
        {
            var p = __instance.GetComponent<Player>();
            if (p != null) VeilController.ForceRefresh(p);
        }
    }
}
```

Register `PatchHealth.TargetKey("VisEquipment", "UpdateLodgroup"),`.

- [ ] **Step 5: Build and cleanup stress test (relayed)**

`make build` → 0 errors, `Patch health: 12 targets patched`. User: `ip_give 2` → body thins, fog appears; equip/unequip armour → veil persists; hit a Greydwarf → veil off during Revealed, back after 12 s; wait for expiry → normal; `ip_give 3` then die (`spawn Troll` and stand still, or `killme` if available) → respawn normal, `ip_state` zdo tier=0; `ip_give 2` then log out to the menu and back in → normal, log shows `cleaned up`; `ip_give 1` then `ip_give 3` → upgrade clean. Any case leaving the player thinned or with fog is a blocker.

- [ ] **Step 6: Commit**

```bash
git add InvisibilityPotion/Visuals InvisibilityPotion/Patches/VisualPatches.cs InvisibilityPotion/Plugin.cs InvisibilityPotion/Dev/DevCommands.cs
git commit -m "feat(visuals): fog veil driven by ZDO state, re-veil on equipment change"
```

---

### Task 10: Placeholder potions, recipes, conversions, localization, drink refusal, `ip_spawn`

**Files:**
- Create: `InvisibilityPotion/Items/PotionItems.cs`, `InvisibilityPotion/Items/Localization.cs`, `InvisibilityPotion/Items/English.json`
- Create: `InvisibilityPotion/Patches/ConsumePatches.cs`
- Modify: `InvisibilityPotion/InvisibilityPotion.csproj` (embed `English.json`), `Plugin.cs`, `Dev/DevCommands.cs`, `Effects/StatusEffects.cs` (icons)

**Interfaces:**
- Produces: prefabs `MeadBaseInvisibility_T1..3`, `MeadInvisibility_T1..3`; `PotionItems.Register()` (called inside `PrefabManager.OnVanillaPrefabsAvailable`); `Localization.Register()`.

- [ ] **Step 1: Localization**

`English.json`:

```json
{
  "item_meadbaseinvisibility_t1": "Mead base: Faint Veil",
  "item_meadbaseinvisibility_t1_description": "Ferment to brew a faint veil mead.",
  "item_meadbaseinvisibility_t2": "Mead base: Deep Veil",
  "item_meadbaseinvisibility_t2_description": "Ferment to brew a deep veil mead.",
  "item_meadbaseinvisibility_t3": "Mead base: Shadow Veil",
  "item_meadbaseinvisibility_t3_description": "Ferment to brew a shadow veil mead.",
  "item_meadinvisibility_t1": "Faint Veil Mead",
  "item_meadinvisibility_t1_description": "Enemies notice you less. Attacking ends the veil and tires you.",
  "item_meadinvisibility_t2": "Deep Veil Mead",
  "item_meadinvisibility_t2_description": "Enemies cannot see or hear you. Attacking reveals you for a while.",
  "item_meadinvisibility_t3": "Shadow Veil Mead",
  "item_meadinvisibility_t3_description": "Enemies cannot see or hear you. Attacking reveals you briefly.",
  "ip_se_name_t1": "Faint Veil",
  "ip_se_name_t2": "Deep Veil",
  "ip_se_name_t3": "Shadow Veil",
  "ip_se_tooltip_t1": "Enemies notice you less.",
  "ip_se_tooltip_t2": "Enemies ignore you until you attack.",
  "ip_se_tooltip_t3": "Enemies ignore you until you attack.",
  "ip_se_start": "The veil settles over you",
  "ip_se_stop": "The veil fades",
  "ip_se_revealed_name": "Revealed",
  "ip_se_revealed_tooltip": "Stamina regenerates slowly.",
  "ip_msg_lower_tier": "A stronger veil is already active"
}
```

csproj: `<EmbeddedResource Include="Items\English.json" />`. `Localization.cs`:

```csharp
using System.IO;
using System.Reflection;
using Jotunn.Entities;
using Jotunn.Managers;

namespace InvisibilityPotion.Items
{
    public static class Localization
    {
        public static void Register()
        {
            var loc = LocalizationManager.Instance.GetLocalization();
            using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("InvisibilityPotion.Items.English.json"))
            using (var reader = new StreamReader(stream))
                loc.AddJsonFile("English", reader.ReadToEnd());
        }
    }
}
```

Verify the resource name with `grep -n "AddJsonFile" ~/.nuget/packages/jotunnlib/2.30.2/lib/net462/Jotunn.xml` and, after building, `strings InvisibilityPotion/bin/Debug/net48/InvisibilityPotion.dll | grep English.json`.

- [ ] **Step 2: Items**

```csharp
using System.Collections.Generic;
using InvisibilityPotion.Config;
using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;
using UnityEngine;

namespace InvisibilityPotion.Items
{
    /// <summary>Placeholder potions: cloned vanilla meads, tinted per tier. Replaced by custom bottles in plan 4.</summary>
    public static class PotionItems
    {
        private static readonly Color[] Tints = { Color.white, new Color(0.5f, 0.9f, 0.5f), new Color(0.5f, 0.6f, 1f), new Color(0.7f, 0.4f, 0.9f) };

        public static string BaseName(int tier) => $"MeadBaseInvisibility_T{tier}";
        public static string MeadName(int tier) => $"MeadInvisibility_T{tier}";

        /// <summary>Call from PrefabManager.OnVanillaPrefabsAvailable (unsubscribe after the first call).</summary>
        public static void Register()
        {
            for (var t = 1; t <= 3; t++)
            {
                var cfg = PluginConfig.Tier(t);
                var requirements = new List<RequirementConfig>();
                foreach (var (item, amount) in RecipeParser.Parse(cfg.Recipe))
                    requirements.Add(new RequirementConfig { Item = item, Amount = amount });

                var baseItem = new CustomItem(BaseName(t), "MeadBaseHealthMinor", new ItemConfig
                {
                    Name = $"$item_meadbaseinvisibility_t{t}",
                    Description = $"$item_meadbaseinvisibility_t{t}_description",
                    CraftingStation = "piece_cauldron",
                    MinStationLevel = 1,
                    Requirements = requirements.ToArray(),
                });
                Tint(baseItem.ItemPrefab, Tints[t]);
                ItemManager.Instance.AddItem(baseItem);

                var mead = new CustomItem(MeadName(t), "MeadHealthMinor", new ItemConfig
                {
                    Name = $"$item_meadinvisibility_t{t}",
                    Description = $"$item_meadinvisibility_t{t}_description",
                    Enabled = false,   // no crafting recipe; produced by the fermenter
                });
                var shared = mead.ItemDrop.m_itemData.m_shared;
                shared.m_itemType = ItemDrop.ItemData.ItemType.Consumable;
                shared.m_consumeStatusEffect = ObjectDB.instance.GetStatusEffect(Effects.StatusEffects.NameHash(t));
                shared.m_food = 0f;
                shared.m_foodStamina = 0f;
                shared.m_foodRegen = 0f;
                Tint(mead.ItemPrefab, Tints[t]);
                ItemManager.Instance.AddItem(mead);

                ItemManager.Instance.AddItemConversion(new CustomItemConversion(new FermenterConversionConfig
                {
                    FromItem = BaseName(t),
                    ToItem = MeadName(t),
                    ProducedItems = 4,
                }));
            }
            // Icons: render the tinted bottles for the items and reuse them for the status effects.
            for (var t = 1; t <= 3; t++)
            {
                var prefab = PrefabManager.Instance.GetPrefab(MeadName(t));
                var sprite = RenderManager.Instance.Render(prefab, RenderManager.IsometricRotation);
                if (sprite == null) continue;
                prefab.GetComponent<ItemDrop>().m_itemData.m_shared.m_icons = new[] { sprite };
                var se = ObjectDB.instance.GetStatusEffect(Effects.StatusEffects.NameHash(t));
                if (se != null) se.m_icon = sprite;
                var basePrefab = PrefabManager.Instance.GetPrefab(BaseName(t));
                var baseSprite = RenderManager.Instance.Render(basePrefab, RenderManager.IsometricRotation);
                if (baseSprite != null) basePrefab.GetComponent<ItemDrop>().m_itemData.m_shared.m_icons = new[] { baseSprite };
            }
        }

        private static void Tint(GameObject prefab, Color tint)
        {
            foreach (var r in prefab.GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.sharedMaterials;
                for (var i = 0; i < mats.Length; i++)
                {
                    if (mats[i] == null) continue;
                    var copy = new Material(mats[i]);   // never tint the vanilla shared material
                    if (copy.HasProperty("_Color")) copy.color = tint;
                    mats[i] = copy;
                }
                r.sharedMaterials = mats;
            }
        }
    }
}
```

Checks: the vanilla prefab names `MeadBaseHealthMinor`/`MeadHealthMinor` exist (`grep -rl "MeadHealthMinor" tools/decompiled/assembly_valheim/ | head -1` or the Jötunn prefab list); `ObjectDB.instance.GetStatusEffect(int)` signature; whether `ItemConfig.Enabled = false` suppresses the recipe in Jötunn 2.30 (`grep -n "Enabled" ~/.nuget/packages/jotunnlib/2.30.2/lib/net462/Jotunn.xml`); whether the mead prefab's consume effect is also stored on a `ConsumeStatusEffect` field that must be cleared (search `m_consumeStatusEffect` in `ItemDrop.cs`). Status effects must already be in `ObjectDB` when `OnVanillaPrefabsAvailable` fires; Jötunn registers `CustomStatusEffect`s before that event, which is why Task 4 registers them in `Awake`.

- [ ] **Step 3: Drink refusal**

```csharp
using HarmonyLib;
using InvisibilityPotion.Effects;

namespace InvisibilityPotion.Patches
{
    /// <summary>Refuse a lower or equal tier while a higher one is active, before the bottle is consumed (Player.cs:6026; ConsumeItem ignores Setup's result).</summary>
    [HarmonyPatch(typeof(Player), nameof(Player.CanConsumeItem))]
    internal static class CanConsumeItemPatch
    {
        [HarmonyPostfix]
        private static void Postfix(Player __instance, ItemDrop.ItemData item, ref bool __result)
        {
            if (!__result || item?.m_shared?.m_consumeStatusEffect == null) return;
            var incoming = item.m_shared.m_consumeStatusEffect as SE_Invisibility;
            if (incoming == null) return;
            var active = SE_Invisibility.ActiveOn(__instance);
            if (active == null || incoming.Tier > active.Tier) return;
            __instance.Message(MessageHud.MessageType.Center, "$ip_msg_lower_tier");
            __result = false;
        }
    }
}
```

Equal tier is refused too (vanilla would refuse it anyway through `HaveStatusEffect`). Register `PatchHealth.TargetKey("Player", "CanConsumeItem"),`.

- [ ] **Step 4: Wire up and `ip_spawn`**

`Plugin.Awake`: `Items.Localization.Register();` and

```csharp
            PrefabManager.OnVanillaPrefabsAvailable += RegisterItemsOnce;
...
        private void RegisterItemsOnce()
        {
            PrefabManager.OnVanillaPrefabsAvailable -= RegisterItemsOnce;
            try { Items.PotionItems.Register(); }
            catch (Exception e) { Log.LogError($"Item registration failed: {e}"); }
        }
```

`DevCommands.cs`:

```csharp
        private class SpawnCommand : ConsoleCommand
        {
            public override string Name => "ip_spawn";
            public override string Help => "ip_spawn <prefab> [count] [level]: spawn creatures 5 m in front of you";

            public override void Run(string[] args)
            {
                var p = Player.m_localPlayer;
                if (p == null || args.Length < 1) { Say(Help); return; }
                var prefab = ZNetScene.instance.GetPrefab(args[0]);
                if (prefab == null) { Say($"unknown prefab {args[0]}"); return; }
                var count = args.Length > 1 && int.TryParse(args[1], out var c) ? c : 1;
                var level = args.Length > 2 && int.TryParse(args[2], out var l) ? l : 1;
                for (var i = 0; i < count; i++)
                {
                    var pos = p.transform.position + p.transform.forward * 5f + Vector3.right * i;
                    var go = Object.Instantiate(prefab, pos, Quaternion.identity);
                    var ch = go.GetComponent<Character>();
                    if (ch != null && level > 1) ch.SetLevel(level);
                }
                Say($"spawned {count} x {args[0]} (level {level})");
            }

            public override List<string> CommandOptionList() => ZNetScene.instance?.GetPrefabNames() ?? new List<string>();
        }
```

Verify `ZNetScene.GetPrefabNames()` and `Character.SetLevel(int)` exist (grep). Register `GiveCommand`, `SpawnCommand`, `ReloadConfigCommand`, `PrefabsCommand` (from Task 9) in `Register()`.

- [ ] **Step 5: Build and in-game check (relayed)**

`make build` → 0 errors, `Patch health: 13 targets patched`. User: `devcommands`, `spawn Honey 10`, `spawn Thistle 5`; craft `Mead base: Faint Veil` at a cauldron (tinted green bottle with icon); put it into a fermenter (`spawn Fermenter` via hammer or `spawn`), wait or `skiptime 2000`; drink `Faint Veil Mead` → effect T1 with icon in the HUD. `spawn MeadInvisibility_T3 1`, drink → T3; then try `MeadInvisibility_T1` → HUD "A stronger veil is already active" and the bottle stays. `ip_spawn Greydwarf 3` works and tab-completes.

- [ ] **Step 6: Commit**

```bash
git add InvisibilityPotion/Items InvisibilityPotion/Patches/ConsumePatches.cs InvisibilityPotion/InvisibilityPotion.csproj InvisibilityPotion/Plugin.cs InvisibilityPotion/Dev/DevCommands.cs InvisibilityPotion/Effects/StatusEffects.cs
git commit -m "feat(items): placeholder veil meads with recipes, fermenter conversions, icons, drink refusal, ip_spawn"
```

---

### Task 11: Docs, checklist, version bump, backlog

**Files:**
- Modify: `docs/testing.md`, `CLAUDE.md`, `README.md`, `InvisibilityPotion/Plugin.cs` + `InvisibilityPotion.csproj` + `Package/manifest.json` (version `0.2.0`), `Makefile`
- Modify: `docs/superpowers/plans/2026-10-01-02-gameplay-core.md` (if ADR 0005 says hot reload works: append "Task 12: reloadable patch assembly" with the same structure as the other tasks, to be executed after the merge as its own bounded change)

- [ ] **Step 1: Testing checklist**

Append to `docs/testing.md` a "Plan 2 – gameplay core" section with one checkbox per in-game check from Tasks 2 and 4 to 10, each phrased as "given / do / expect", plus the cleanup stress list from Task 9. Mark the ones the user confirmed during execution; leave the sleeping-monster and any skipped item unchecked with a note.

- [ ] **Step 2: CLAUDE.md and README**

CLAUDE.md: add the dev commands (`ip_give`, `ip_spawn`, `ip_reload_config`, `ip_prefabs`, `ip_state`), the rule "reveal hooks only call MarkRevealed; effects are added only from UpdateStatusEffect", and "visuals are driven by VeilController from ZDO state, never from the status effect". README: status line `Status: tiers I–III work in singleplayer with placeholder meads; multiplayer and custom bottles pending. Licence: MIT.`

- [ ] **Step 3: Version 0.2.0**

`Plugin.PluginVersion = "0.2.0"`, csproj `<Version>0.2.0</Version>`, `manifest.json` `"version_number": "0.2.0"`.

- [ ] **Step 4: Backlog items**

Makefile: derive `VALHEIM_INSTALL` from `Environment.props` when present:

```makefile
VALHEIM_INSTALL ?= $(shell sed -n 's|.*<VALHEIM_INSTALL>\(.*\)</VALHEIM_INSTALL>.*|\1|p' Environment.props 2>/dev/null || true)
VALHEIM_INSTALL := $(if $(VALHEIM_INSTALL),$(VALHEIM_INSTALL),$(HOME)/.local/share/Steam/steamapps/common/Valheim)
```

Verify with `make -n run | head -3` that the path is the props value. Patch-health overload ambiguity: add to CLAUDE.md that `CanHearTarget`/`CanSeeTarget` keys cover the static overloads only because those are the only ones patched; the key format stays `Type.Method`.

- [ ] **Step 5: Release check and commit**

`make test` → all pass. `make package` → zip; `strings InvisibilityPotion/bin/Release/net48/InvisibilityPotion.dll | grep -c 'ip_give\|ip_spawn\|AutoJoin'` and `strings -el ... | grep -c ...` both 0.

```bash
git add docs/testing.md CLAUDE.md README.md InvisibilityPotion/Plugin.cs InvisibilityPotion/InvisibilityPotion.csproj InvisibilityPotion/Package/manifest.json Makefile docs/superpowers/plans/2026-10-01-02-gameplay-core.md
git commit -m "docs: plan 2 checklist, conventions, version 0.2.0"
```

---

## Self-review

**Spec coverage** (amendment §1–§12): goal (all tasks); decisions table (Tasks 6, 7, 8, 1, 2); §3 perception/aggro/tier I (Tasks 5, 6, 7; `HiddenState` cache in Task 4); §4 reveal triggers (Task 8; damage-taken via `OnDamaged` in Task 4); §5 lifecycle (Tasks 3, 4; refusal in Task 10); §6 config and reload (Task 2); §7 dev tooling (Tasks 2, 4, 9, 10); §8 spike (Task 1); §9 items (Task 10); §10 visuals (Task 9); §11 tests (Tasks 2, 3; checklist Task 11); §12 order matches. Deviation from amendment §5 table: `Setup`/`EnterHidden` do not touch visuals; `VeilController` drives them from the ZDO for local and remote players alike (one code path, simpler cleanup). Re-hide (step 8 of the order) is inside the state machine and verified in Tasks 8 and 9.

**Placeholders:** none. `FogPrefabName` is a named placeholder chosen in Task 9 step 2 by listing real prefabs.

**Type consistency:** `HiddenState.Get/HiddenTier/IsIgnoredByEnemies/Write` used identically in Tasks 4–9; `SE_Invisibility.ActiveOn/MarkRevealed/Tier/Machine` in Tasks 4, 8, 10; `StatusEffects.NameHash/RevealedHash` in Tasks 4, 10; `PluginConfig.Tier/Global/Reload/Refresh` in Tasks 2, 4, 6, 7, 8, 9; `InvisibilityStateMachine.Tick/MarkRevealed/StepResult` in Tasks 3, 4; `VeilController.ForceRefresh` in Task 9; `Say` shared on `DevCommands` from Task 2.

**Review Focus:** 1 → Task 6 (reads `HiddenState` only, in-game "owner=local" check through `ip_state`); 2 → Task 3 test `MarkRevealed_OnlySetsFlag_UntilNextTick` and Task 4 `OnDamaged`; 3 → Task 10 step 5; 4 → Task 2 `ModifierMathTests`; 5 → Task 4 `OnDestroy` + Task 9 stress test.

---

### Task 12: Tier III hidden from other players (added 2026-10-01 at the user's request)

**Files:**
- Create: `InvisibilityPotion/Patches/PlayerHidePatches.cs`
- Modify: `InvisibilityPotion/Plugin.cs` (`ExpectedPatchTargets`), `InvisibilityPotion/Config/PluginConfig.cs` (`AllowPvpInvisibility`, per-tier `HiddenFromPlayers`), `InvisibilityPotion/Config/TierConfig.Core.cs`, `InvisibilityPotion/Net/HiddenState.cs` (`IsHiddenFromPlayers`), `InvisibilityPotion/Dev/DevCommands.cs` (`ip_state` prints hidden-from-players), `docs/testing.md`

**Interfaces:**
- Consumes: `HiddenState.Get(Character)` / ZDO keys; `PluginConfig.Tier(int)`; `PluginConfig.Global`.
- Produces: `HiddenState.IsHiddenFromPlayers(Character c)` (hidden and `Tier(t).HiddenFromPlayers` and `Global.AllowPvpInvisibility`), `HiddenState.IsHiddenFromPlayers(ZDO zdo)` (server-side lookup by ZDO without a Character instance).

Spec: main spec §5.7 and amendment decision table ("Tier III also hides the player from other players"). Facts: `docs/decompile-notes.md` §Network and §Visuals (`EnemyHud.TestShow` EnemyHud.cs:101; `ZNet.UpdatePlayerList` ZNet.cs:2454 with `m_publicPosition` at :2469/:2500; `ZDOMan.SendZDOs(ZDOPeer peer, bool flush)` ZDOMan.cs:1074 with the header write `zPackage.Write(item2.GetPosition())` at :1126; `ZDOPeer` is a private nested class with `public ZNetPeer m_peer`; `ZNetPeer.m_uid`, `m_characterID`; the hidden player's own client sends the real position to the server, the server forwards to other clients; zone ownership uses `m_refPos`, not the ZDO position).

- [ ] **Step 1: Config**

`TierConfig` gains `public bool HiddenFromPlayers;` (defaults T1 false, T2 false, T3 true, section key `HiddenFromPlayers`, description "Other players cannot see this player's position, model or nameplate"). `GlobalConfig` gains `public bool AllowPvpInvisibility = true;` (key `AllowPvpInvisibility`, "Server switch for hiding players from other players (tier III)"). Bind, Refresh and `Validate` unchanged otherwise. Add a unit test that `TierConfig` default `HiddenFromPlayers` is false.

- [ ] **Step 2: HiddenState**

```csharp
public static bool IsHiddenFromPlayers(Character c)
{
    var tier = HiddenTier(c);
    return tier > 0 && PluginConfig.Global.AllowPvpInvisibility && PluginConfig.Tier(tier).HiddenFromPlayers;
}

/// <summary>Server-side check by ZDO (no Character instance needed). Reads the two keys directly; no cache.</summary>
public static bool IsHiddenFromPlayers(ZDO zdo)
{
    if (zdo == null || !PluginConfig.Global.AllowPvpInvisibility) return false;
    var tier = zdo.GetInt(HashTier, 0);
    if (tier < 1 || tier > 3 || !zdo.GetBool(HashHidden, false)) return false;
    return PluginConfig.Tier(tier).HiddenFromPlayers;
}
```

- [ ] **Step 3: Nameplate (viewer side)**

```csharp
[HarmonyPatch(typeof(EnemyHud), "TestShow")]
internal static class EnemyHudTestShowPatch
{
    [HarmonyPostfix]
    private static void Postfix(Character c, ref bool __result)
    {
        if (!__result || c == null || !c.IsPlayer() || c == Player.m_localPlayer) return;
        if (HiddenState.IsHiddenFromPlayers(c)) __result = false;
    }
}
```

`TestShow(Character c, bool isVisible)` is private (EnemyHud.cs:101); parameter name `c` must match the decompile (verify with `sed -n '101p'`).

- [ ] **Step 4: Map pin and player list (server/host side)**

Postfix on `ZNet.UpdatePlayerList()` (private, ZNet.cs:2454): iterate `__instance.m_players` (verify the list field name and the `ZNet.PlayerInfo` struct members `m_characterID`, `m_publicPosition`, `m_position` in ZNet.cs). For each entry whose character ZDO (`ZDOMan.instance.GetZDO(info.m_characterID)`) satisfies `IsHiddenFromPlayers(zdo)`, set `m_publicPosition = false` and `m_position = Vector3.zero`. `PlayerInfo` is a struct: write the modified copy back into the list by index. Runs on the server (listen host or dedicated) because `UpdatePlayerList` is only called there (verify the caller `SendPlayerList`/`SendPeriodicData` branch).

- [ ] **Step 5: Position spoof in the ZDO header (server side)**

Transpiler on `ZDOMan.SendZDOs` (private; the nested `ZDOPeer` type is obtained via `AccessTools.Inner(typeof(ZDOMan), "ZDOPeer")`; declare the patch with `[HarmonyPatch]` + a `TargetMethod()` returning `AccessTools.Method(typeof(ZDOMan), "SendZDOs", new[] { AccessTools.Inner(typeof(ZDOMan), "ZDOPeer"), typeof(bool) })`). The transpiler replaces the single `callvirt ZDO.GetPosition()` that immediately precedes `ZPackage.Write(Vector3)` in the header block (ZDOMan.cs:1126 in the decompile: the sequence `Write(GetOwner())`, `Write(GetPosition())`) with a call to

```csharp
public static Vector3 HeaderPosition(ZDO zdo, object zdoPeer)
{
    var real = zdo.GetPosition();
    if (zdoPeer == null || !HiddenState.IsHiddenFromPlayers(zdo)) return real;
    var peer = PeerOf(zdoPeer);                       // ZNetPeer via AccessTools.Field(ZDOPeer, "m_peer")
    if (peer == null || zdo.GetOwner() == peer.m_uid) return real;   // the owner always gets its own real position
    return new Vector3(real.x, SpoofHeight, real.z);  // SpoofHeight = 10000f: far above the world, same sector column
}
```

Implementation notes: the transpiler must load the `peer` argument (`ldarg.1`) before the call and verify exactly one replacement happened (log an error and leave IL untouched otherwise; the patch health check then reports `ZDOMan.SendZDOs` as patched but a `PatchHealth` warning line "SendZDOs transpiler: pattern not found" must appear). Use `AccessTools.FieldRefAccess` or a cached `FieldInfo` for `m_peer`. The spoof must stop the instant `IP_Hidden` becomes false (it reads the ZDO each send, so it does). Only the header position is changed; the ZDO data, sectors and `m_refPos` are untouched (decompile-notes §Network). Dedicated servers must run the mod (already enforced by `NetworkCompatibility`).

If the publicized assembly exposes `ZDOMan.ZDOPeer` directly, a plain `[HarmonyPatch(typeof(ZDOMan), "SendZDOs")]` with a typed parameter is acceptable; say which in the report (open question 4 of the decompile notes).

- [ ] **Step 6: Patch health, ip_state, docs**

Add `PatchHealth.TargetKey("EnemyHud", "TestShow")`, `("ZNet", "UpdatePlayerList")`, `("ZDOMan", "SendZDOs")`. `ip_state` prints `hiddenFromPlayers=<bool>` for self. `docs/testing.md`: add the two-client checks (second client: no nameplate, no map pin, player not rendered while hidden; appears within one send interval after a reveal; AI on the second client's zone still ignores the hidden player) marked "pending, needs plan 3 server".

- [ ] **Step 7: Build, test, commit**

`make build` (0/0), `make test`; commit `feat(patches): tier III hidden from other players (nameplate, map pin, position spoof)`.
