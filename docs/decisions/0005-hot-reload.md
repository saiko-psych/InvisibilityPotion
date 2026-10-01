# ADR 0005 – Hot reload of patch code

Status: accepted · 2026-10-01

## Context
The user wants to avoid restarting the game for every code change. Jötunn content (items, status effects) is registered once at scene load and cannot be reloaded; Harmony patches can be unpatched and re-applied.

## Probe
BepInEx ScriptEngine r11.1 (BepInEx.Debug, installed to `BepInEx/plugins/ScriptEngine/ScriptEngine.dll`) with a throwaway plugin that patches `Player.IsCrouching` and logs a marker. Reloaded twice: V1→V1 by F6, V1→V2 by the file watcher after rebuilding into `BepInEx/scripts`. The planned third reload (V3) was skipped as two clean reloads were sufficient evidence.

## Result
Works. Observed log lines:

- After F6: `[Info :Script Engine] Loading plugins from .../InvisibilityPotion.HotReloadProbe.dll`, `Loading saikopsych.InvisibilityPotion.HotReloadProbe`, `Reloaded all plugins!`, `[Info :HotReloadProbe] probe loaded PROBE_V1`, `[probe] crouch poll PROBE_V1`.
- Second reload: `probe unloaded PROBE_V1`, `probe loaded PROBE_V1`, then single `[probe] crouch poll PROBE_V1` lines about every 5 s (never doubled, so the old patch was removed).
- Watcher reload after deploying V2: `probe unloaded PROBE_V1`, `probe loaded PROBE_V2`, then single `[probe] crouch poll PROBE_V2` lines about every 5 s, no V1 lines, no errors.
- `ip_state` lists only `FejdStartup.Start` (the probe's patch is owned by its own Harmony id).

Gotchas found:
- ScriptEngine r11.1 loads assemblies with `ReadSymbols=true`. Without symbols it fails with `SymbolsNotFoundException: No symbol found for file`. Any assembly in `BepInEx/scripts` must be built with `<DebugType>embedded</DebugType>`.
- The game regenerates `com.bepis.bepinex.scriptengine.cfg` on first start with `LoadOnStart = false`. Required settings: `[General] LoadOnStart = true`, `[AutoReload] EnableFileSystemWatcher = true`, `AutoReloadDelay = 3`.

## Decision
Plan 2 keeps a single assembly for delivery; after plan 2 a bounded change moves `Patches/*` into `InvisibilityPotion.Patches.dll` loaded from `BepInEx/scripts`, with `Plugin` exposing `HiddenState`, `PluginConfig` and `StatusEffects` for it. `make run` deploys both. ScriptEngine stays installed in the game folder.

## Consequences
Patch-only changes can be iterated without restarting the game once the split is done. Content registered through Jötunn still needs a restart. The patches assembly must use embedded debug symbols.
