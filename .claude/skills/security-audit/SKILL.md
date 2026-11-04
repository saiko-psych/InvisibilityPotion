---
name: security-audit
description: Security audit of the mod for multiplayer: client trust, RPCs, admin config, position leaks, Release build contents, supply chain.
disable-model-invocation: true
argument-hint: "[full | network | config | exposure | cheats | supply]"
---

<!-- Adapted from Claude Code Game Studios, .claude/skills/security-audit (MIT, (c) 2026 Donchitos,
     https://github.com/Donchitos/Claude-Code-Game-Studios): categories, severity scale and the zero-hit rule, rewritten for a
     BepInEx/Harmony Valheim mod. -->

# Security audit

Run before a release that changes anything multiplayer touches, and after a Valheim or Jötunn update. The argument picks one
category; no argument is `full`.

## Ground rules

- **A scan with zero hits is a result only when the pattern is known to exist.** Before reading zero hits as clean, confirm the
  pattern matches something it should (for example `ZDO.Set` must hit `Net/HiddenState.cs`). A category you could not check is
  **NOT ASSESSED** in the report, never clean.
- **The baseline is `docs/compatibility.md` §4 "Trust model and known limits".** Each limit listed there is an **Accepted risk**
  unless the code changed around it; re-check it, and say whether it still holds. A new finding is **Open**.
- Every finding names the file and line, the attacker (modified client, non-admin player, admin, server operator, third party),
  and what they gain.
- Valheim's trust model is the frame: each client owns and writes its own player ZDO and the ZDOs of zones it owns, the server
  relays. A finding is about what this mod adds on top of that.

## Categories

### 1. Network authority (`network`)
- Every ZDO key the mod writes (`docs/compatibility.md` §3; grep `zdo.Set(` and `GetZDO()`): who writes it, who reads it, and
  what a client gains by writing any value (`IP_Hidden`/`IP_Tier` without drinking, `IP_Goggles = 3` without goggles, plant and
  lichen stage keys).
- Every RPC (grep `.Register(`, `.Register<`, `InvokeRPC`, `ZRoutedRpc`): does the handler check `IsOwner()`, validate the
  arguments (finite floats, ranges) and stay idempotent on a repeated call (double pick)?
- The server-side spoof (`Patches/PlayerHidePatches.cs` `HeaderPosition`) and the player list (`ZNet.UpdatePlayerList`): what
  reaches a peer that should not see the hidden player.

### 2. Admin config and commands (`config`)
- Entries bound `IsAdminOnly` (`Config/PluginConfig*.cs`): which are really server-controlled and which are read once from each
  machine's own file (recipes, `HuldraCultivable`, world-generation chances); what a player gains by editing their own file.
- Values an admin can set: `TierConfig.Validate`, `AcceptableValueRange`, parsers (`RecipeParser`, `PlantYield.TryParseRange`,
  fog lists). Non-finite, negative, empty and huge values each have a defined outcome.
- Console commands in Release (`Commands/AdminCommands.cs`): where the admin check runs, and whether the server acts on anything
  a client sends.

### 3. Data exposure (`exposure`)
- Where a tier III player's position can still leak: header x/z, map pin, nameplate, player list, other mods (Mumble).
- The Release DLL holds no dev code: everything in `Dev/` behind `#if DEBUG`, and `scripts/server-lib.sh` `check_release_dll`
  still recognises a Debug build. Log lines that print more than a player could see.

### 4. Cheat vectors (`cheats`)
- What a single-file or memory edit on one client gains in a shared world: free or cheap meads, permanent invisibility, seeing
  hidden plants or players, skipping the Veil Cooldown or Veil Broken. Client-side checks are hints; name the ones the server
  could enforce and the ones it cannot.

### 5. Supply chain (`supply`)
- Downloads in `scripts/server-setup.sh` (Thunderstore zips: pinned versions, checksums, extraction paths), NuGet packages in the
  csproj files, GitHub Actions in `.github/workflows/` (pinned by tag or by commit).
- Copied code carries an MIT / MIT-0 / Unlicense credit comment (CLAUDE.md rule); grep `Adapted from`, `Pattern from`.

## Severity

| Level | Meaning here |
|---|---|
| **Critical** | Code execution on another machine, or a non-admin player changing the server's config or another player's state |
| **High** | A modified client bypassing what the server is meant to enforce (the tier III spoof, admin-only values) |
| **Medium** | A local cheat in a shared world, information a hidden player's opponents should not have, a validation gap with limited impact |
| **Low** | Hardening with no direct exploit |

Status: **Open**, **Accepted risk** (with the reason, usually the trust model), **Out of scope**.

## Report

Show the report in chat first; write it to `docs/audits/security-audit-YYYY-MM-DD.md` only after the user agrees. Sections:
scope and categories run (NOT ASSESSED ones with the reason), findings by severity (`SEC-NNN`: file:line, attacker, gain,
status, fix), accepted risks re-checked against `docs/compatibility.md` §4, and the dependency list. Findings that become
accepted risks go into `docs/compatibility.md` §4; findings to fix follow `/new-patch` when they touch a Harmony target.

The audit is done when every category of the scope has a verdict (findings, clean with a pattern that hits, or NOT ASSESSED).
