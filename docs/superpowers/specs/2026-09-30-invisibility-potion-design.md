# Invisibility Potion – Design Spec

Date: 2026-09-30 · Status: approved by the author, ready for planning
Supersedes the open questions in [`docs/concept.md`](../../concept.md) v0.2. Decisions are recorded in [`docs/decisions/`](../../decisions/).

## 1. Goal

A Valheim mod (BepInEx + Jötunn) that adds three tiers of invisibility potions. Enemies perceive the player less (tier I) or not at all (tier II/III). Attacking reveals the player and slows stamina regeneration. Tier II/III re-hide after a short time without attacking. Tier III also hides the player from other players (PvP). The mod works in singleplayer, on dedicated servers, and in PvP.

## 2. Decisions taken

| Topic | Decision |
|---|---|
| Game mode | Multiplayer including PvP is a first-class target. Network sync is part of the core, not a later step. |
| Framework | Jötunn 2.30.2 on BepInEx 5.4.23.x (ADR 0001). |
| Perception | Own Harmony patches + tier flag in the player ZDO (ADR 0004). |
| Visuals | Fog for tier I/II, shimmer for tier III with dense-fog fallback (ADR 0002). |
| Licence | MIT (ADR 0003). |
| Tier I attack | Ends the effect immediately. Debuff still applies. |
| Chasing enemies | Drop the target; vanilla then walks to the last known position and searches briefly. |
| Reveal triggers | Melee swing, bow/crossbow shot, staff cast, throw, drawing a bow, blocking/parrying, taking damage. Not: harvesting, doors, chests, pickups. |
| Bottles | Three distinct meshes, one per tier, built in Unity 6000.0.75f1. Placeholder: tinted clones of `MeadHealthMinor` until then. |
| Unity | Installed as part of the initial setup. |
| Plugin identity | Name `InvisibilityPotion`, GUID `saikopsych.InvisibilityPotion`. |
| Language | Everything in English: code, docs, commits, config. |
| Development | CLI only (`dotnet build`, Makefile), no IDE. |
| Git | Local repository from the first commit. Gitea remote added later. |

## 3. Targets and versions

| Component | Version | Source |
|---|---|---|
| Valheim | 1.0.16, network version 40 | local `Player.log` |
| Unity | 6000.0.75f1 | local `UnityPlayer.so`, Valheim-Modding Wiki |
| BepInEx | 5.4.23.5 (BepInExPack Valheim 5.4.2350) | local `LogOutput.log` |
| Jötunn | 2.30.2 (NuGet `JotunnLib`) | Thunderstore / NuGet, 2026-09-21 |
| .NET SDK | 8.0 (builds `net48` through reference assemblies) | pacman `dotnet-sdk-8.0` |
| Steam build id | 25527674 | `appmanifest_892970.acf` |

Game-code names are verified against the decompiled `assembly_valheim.dll` before use. Names in this spec marked *(verify)* were found as strings in the assembly but their semantics were not yet read from source. All such markers have since been resolved against the 1.0.16 decompile; see [`docs/decompile-notes.md`](../../decompile-notes.md).

## 4. Repository layout

```
invisibility-potion/                  git root, MIT
├── InvisibilityPotion/               C# plugin, derived from JotunnModStub (MIT-0)
│   ├── InvisibilityPotion.csproj     net48, JotunnLib 2.30.2, DebugType portable
│   ├── Plugin.cs                     BepInEx entry: config, registration, patch health check
│   ├── Config/                       tier config, sync attributes
│   ├── Effects/                      SE_Invisibility, SE_Revealed
│   ├── Patches/                      Perception, Aggro, Reveal, PlayerHide
│   ├── Net/                          HiddenState (ZDO read/write helper)
│   ├── Visuals/                      IVeil, FogVeil, ShimmerVeil
│   ├── Items/                        potion items, conversions, icons
│   ├── Dev/                          auto-join patch, dev console commands (DEBUG builds only)
│   └── Package/                      Thunderstore manifest.json, README.md, icon.png
├── InvisibilityPotion.Tests/         xunit tests for pure logic (no game DLLs)
├── InvisibilityPotionUnity/          Unity 6000.0.75f1 project for bottle meshes and the asset bundle
├── docs/                             concept, ADRs, specs, plans, decompile notes, testing checklist
├── tools/decompiled/                 ilspycmd output of assembly_valheim.dll (gitignored)
├── Environment.props                 VALHEIM_INSTALL, MOD_DEPLOYPATH (gitignored; Environment.props.example committed)
├── DoPrebuild.props                  ExecutePrebuild=true so Jötunn publicizes the game DLLs
├── Makefile                          build, run, log, decompile, test, deploy-server, package
├── .gitignore, LICENSE, README.md, CLAUDE.md
```

## 5. Components

### 5.1 Plugin entry (`Plugin.cs`)

- `[BepInPlugin("saikopsych.InvisibilityPotion", "InvisibilityPotion", "0.1.0")]`, `[BepInDependency(Jotunn.Main.ModGuid)]`, `[NetworkCompatibility(CompatibilityLevel.EveryoneMustHaveMod, VersionStrictness.Minor)]`.
- `Awake`: bind config, register status effects and items (inside `PrefabManager.OnVanillaPrefabsAvailable`), apply Harmony patches, run the patch health check.
- Patch health check: after `PatchAll`, iterate the expected patched methods and confirm each has a patch from our Harmony id. Missing patches are logged as errors with the method name. The mod stays loaded so the rest still works.

### 5.2 Config (`Config/`)

BepInEx config file `saikopsych.InvisibilityPotion.cfg`, synced through Jötunn (`ConfigurationManagerAttributes { IsAdminOnly = true }`). Server values override client values.

Per tier (section `Tier1`, `Tier2`, `Tier3`):

| Key | T1 default | T2 default | T3 default |
|---|---|---|---|
| `Duration` (s) | 60 | 120 | 180 |
| `StealthModifier` | 0.25 | – | – |
| `NoiseModifier` | 0.25 | – | – |
| `IgnoredByEnemies` | false | true | true |
| `AggroLossTime` (s) | 5 | 1 | 1 |
| `RehideDelay` (s) | – (attack ends effect) | 12 | 8 |
| `DebuffStaminaRegenMultiplier` | 0.5 | 0.5 | 0.5 |
| `DebuffDuration` (s) | 20 | 20 | 20 |
| `Cooldown` (s) | 0 | 0 | 0 |
| `HiddenFromPlayers` | false | false | true |
| `Recipe` | `Honey:10,Thistle:5` | `Honey:10,Thistle:5,Bloodbag:3` | `Honey:10,Thistle:5,Bloodbag:3,YmirRemains:1` |

Global (section `General`): `AllowPvpInvisibility` (true), `RevealOnDamage` (true), `RevealOnBlock` (true), `RevealOnBowDraw` (true), `ShowSelfFaintly` (true), `LogLevel`.

Recipe strings are `Item:Amount` pairs separated by commas and are parsed by a pure function that is unit-tested. Recipe defaults are placeholders for balancing.

### 5.3 Status effects (`Effects/`)

**`SE_Invisibility : SE_Stats`** – one class, three registered instances `SE_Invisibility_T1`, `_T2`, `_T3` (Jötunn `CustomStatusEffect`). Each instance holds a `TierConfig` reference.

State:

```
enum Phase { Hidden, Revealed }
Phase phase; float rehideTimer;
```

Lifecycle:

| Event | Behaviour |
|---|---|
| `Setup` (drink) | If a higher or equal tier is active: refuse with HUD message, item not consumed (differs: the refusal must be a postfix on `Player.CanConsumeItem`, see decompile-notes.md §Consume path). If a lower tier is active: remove it (its `Stop` runs cleanup), then apply. Set `m_ttl` from config. For tier I set `m_stealthModifier` / `m_noiseModifier` from config. Enter `Hidden`: write ZDO (`IP_Tier`, `IP_Hidden = true`), apply veil, request aggro drop. |
| `OnReveal()` (attack trigger) | Apply/restart `SE_Revealed`. Tier I: `m_time = m_ttl` so the effect ends this frame. Tier II/III: if `Hidden`, enter `Revealed`: write `IP_Hidden = false`, remove veil. Restart `rehideTimer = RehideDelay`. |
| `UpdateStatusEffect(dt)` | If `Revealed`: count down `rehideTimer`; at zero enter `Hidden` again (ZDO, veil, aggro drop). |
| `Stop()` (duration expired, death, logout, replaced, removed) | The single cleanup path: `IP_Tier = 0`, `IP_Hidden = false`, veil removed, cached state cleared. Idempotent. |

**`SE_Revealed : SE_Stats`** – stamina-regen debuff. Sets `m_staminaRegenMultiplier` (verified: the field alone multiplies regen for values ≤ 1, no override needed) from config, `m_ttl = DebuffDuration`. Re-applying restarts the timer (`ResetTime`).

### 5.4 Network state (`Net/HiddenState`)

Two ZDO keys on the player's ZDO: `IP_Tier` (int, 0 = none) and `IP_Hidden` (bool). Written only by the owning client from `SE_Invisibility`. Read by every patch through `HiddenState.Get(Character)`, which returns `(tier, hidden)` and caches per `ZDOID` with a short TTL so `CanSenseTarget` does not hash strings every call. Remote players' effects are not simulated; only the ZDO is read.

### 5.5 Perception and aggro (`Patches/Perception`, `Patches/Aggro`)

- Prefix `BaseAI.CanSenseTarget(Character target)` (differs: see decompile-notes.md §Perception, hook the static `CanHearTarget`/`CanSeeTarget` and `FindEnemy` instead): if `target is Player` and `HiddenState.Get(target)` says hidden with `IgnoredByEnemies`, set `__result = false`, return `false`.
- Aggro drop on entering `Hidden`: `Character.GetCharactersInRange(pos, range, list)`; for each `MonsterAI` with `GetTargetCreature() == player`, set `m_targetCreature = null`, `m_alerted = false` (differs: `m_targetCreature` is `MonsterAI`, `m_alerted` is `BaseAI` and must be set via `SetAlerted(false)`, owner only; see decompile-notes.md §Aggro loss). Executed locally; because the zone owner's AI also stops sensing the player through the prefix, its own lose-target logic finishes the job.
- Per-tier lose-target time: patch the method that compares `m_timeSinceSensedTargetCreature` against the vanilla threshold (verified: `MonsterAI.UpdateTarget`, literal `30f`, see decompile-notes.md §Aggro loss) and substitute `AggroLossTime` when the target is hidden. Vanilla then walks to the last known position and searches, which matches the decision for chasing enemies.
- Tier I is not patched here; its reduced perception comes from the vanilla stealth math via the effect's modifiers.

### 5.6 Reveal triggers (`Patches/Reveal`)

Each trigger is a small postfix that resolves the acting `Player`, checks for an active `SE_Invisibility`, and calls `OnReveal()`. Candidates from the research, confirmed in the decompile before coding:

| Trigger | Hook (confidence) | Config gate |
|---|---|---|
| Melee, bow/crossbow shot, staff, throw | `Humanoid.StartAttack` postfix when `__result` is true (high) | always |
| Drawing a bow | `Humanoid.IsDrawingBow` / `Attack` bow-draw state (medium) | `RevealOnBowDraw` |
| Blocking / parrying | `Humanoid.BlockAttack` postfix (high) | `RevealOnBlock` |
| Taking damage | `Character.Damage` / `RPC_Damage` prefix on the victim side (high) | `RevealOnDamage` |

Harvesting, doors, chests and pickups are deliberately not hooked.

### 5.7 Hidden from players, tier III (`Patches/PlayerHide`)

Only active when the tier config has `HiddenFromPlayers` and `AllowPvpInvisibility` is true. Reuses the Server Devcommands technique (Unlicense):

- Prefix/postfix on `ZDOMan.SendZDOs`: while sending the hidden player's ZDO to another player peer, temporarily move its position far away (`y = 10000`) and restore afterwards, so other clients never receive the real position. The server peer still gets the real position so AI on a server-owned zone keeps working.
- Prefix on `ZNet.SendPeriodicData`: force `m_publicReferencePosition = false` while hidden, so no shared map pin.
- Viewer side: `EnemyHud` skips the nameplate of a hidden remote player; `Minimap.UpdatePlayerPins` skips the pin.
- Voice/chat are untouched.

### 5.8 Visuals (`Visuals/`)

```
interface IVeil { void Apply(Player p, int tier); void Remove(Player p); }
```

- `VeilController` picks `FogVeil` for tier I/II and `ShimmerVeil` for tier III (falls back to `FogVeil` dense mode if `ShimmerVeil.IsSupported` is false).
- `FogVeil`: snapshot all renderers of the player (body, hair, beard, armour, cape, held items, via `GetComponentsInChildren<Renderer>`), raise `_Cutoff` on `Custom/Player` materials to thin them, attach a looping particle fog prefab from the asset bundle (light or dense). The local player keeps a faint version (`ShowSelfFaintly`).
- `ShimmerVeil`: swap materials to `Custom/Distortion` on the same renderer set. Prototyped in build step 4; visual quality decides whether it ships.
- Equipment changes while hidden: postfix on the visual-equipment update (differs: `UpdateEquipmentVisuals` runs every frame, use `VisEquipment.UpdateLodgroup`; see decompile-notes.md §Visuals) re-applies the veil.
- `Remove` restores the exact snapshot. Called only from `SE_Invisibility.Stop` and from the `Revealed` transition.
- Remote players run the same controller from a per-frame check of `HiddenState` on visible players, so viewers render the veil without an RPC.

### 5.9 Items (`Items/`)

- Six prefabs: `MeadBaseInvisibility_T1/T2/T3` (cauldron) and `MeadInvisibility_T1/T2/T3` (fermenter output). Registered with Jötunn `CustomItem` + `ItemConfig` (recipe from config) and `CustomItemConversion` with `FermenterConversionConfig`.
- The mead sets `m_itemData.m_shared.m_consumeStatusEffect` to its tier effect and `m_itemType = Consumable`.
- Placeholder phase: `PrefabManager.Instance.CreateClonedPrefab(name, "MeadHealthMinor")` with tinted materials (green, blue, violet). Icons via `RenderManager.Instance.Render(prefab, RenderManager.IsometricRotation)`.
- Final phase: prefabs loaded from the asset bundle (`AssetUtils.LoadAssetBundleFromResources`), three distinct bottle meshes, Jötunn shader mocking (`JVLmock_` prefix, `fixReference: true`). Icons still rendered at runtime.
- Localization through Jötunn `CustomLocalization` with an English JSON file.

### 5.10 Dev tooling (`Dev/`, DEBUG builds only)

- `AutoJoin`: if env var `IP_DEV_WORLD` is set, patch `FejdStartup` to skip the menu and load that world with the character in `IP_DEV_CHARACTER`.
- Console commands via Jötunn `CommandManager`: `ip_give <tier>` (drink instantly), `ip_state` (print own and nearby players' hidden state and active patches), `ip_spawn <prefab> [n]` (shortcut around `devcommands`).
- Compiled out of Release builds with `#if DEBUG`.

## 6. Build and test loop

Makefile targets:

| Target | Does |
|---|---|
| `setup` | prints the sudo commands to run (`pacman -S dotnet-sdk-8.0`, `yay -S unityhub`), installs `ilspycmd` as a dotnet tool, creates `Environment.props` from the example |
| `build` | `dotnet build -c Debug`; Jötunn's `publish.sh` copies the DLL and pdb into `$VALHEIM_INSTALL/BepInEx/plugins/InvisibilityPotion/` |
| `run` | `build`, then starts `start_game_bepinex.sh -console -screen-fullscreen 0 -screen-width 1600 -screen-height 900` with `IP_DEV_WORLD=testing`; BepInEx log streams to the terminal |
| `log` | `tail -f BepInEx/LogOutput.log` |
| `test` | `dotnet test` on the xunit project |
| `decompile` | `ilspycmd -p -o tools/decompiled assembly_valheim.dll`; rerun after game updates |
| `package` | `dotnet build -c Release`, assembles `Package/` into a Thunderstore zip |
| `deploy-server` | rsync plugin + Jötunn to the LXC `BepInEx/plugins`, restart the server service (host, user, paths from `Environment.props`) |

Edit-to-in-game target: under 60 seconds.

Testing:

- Unit tests (xunit, `net8.0`, no game DLLs): phase transitions and timers of a pure `InvisibilityStateMachine` extracted from `SE_Invisibility`, recipe-string parsing, config validation.
- In-game checklist in `docs/testing.md` per build step, based on the concept's test matrix: singleplayer, self-hosted, dedicated with self as zone owner, dedicated with another player as zone owner, player joins while hidden.
- Cleanup stress test: drink, attack, die, logout, upgrade tier, remove via console; every path ends with normal visuals and `IP_Tier = 0`.

## 7. Build order

1. Setup: toolchain, JotunnModStub-derived project, Makefile `build` / `run` / `decompile`, dev auto-join, CI-free. Verify the plugin loads and logs its version in-game.
2. Decompile and locate hooks: confirm every *(verify)* item above, write `docs/decompile-notes.md`.
3. `SE_Invisibility` with placeholder items: tier I stealth modifiers, tier II/III `CanSenseTarget` prefix, ZDO state, aggro drop, patch health check. Singleplayer test.
4. Reveal triggers and `SE_Revealed` debuff.
5. Visuals: `FogVeil`, cleanup path, equipment-change handling. Shimmer prototype.
6. Tier II/III re-hide.
7. Server deploy and multiplayer test matrix with the LXC server.
8. Tier III hidden-from-players (PvP).
9. Unity project, bottle meshes, asset bundle, icons, recipes, balancing, Thunderstore package.

## 8. Risks

| Risk | Mitigation |
|---|---|
| Hook names change with game updates | patch health check at startup; `make decompile` after updates; hooks isolated in `Patches/` |
| AI runs on zone owner | ZDO state from step 3, not retrofitted |
| Player stuck veiled | single `Stop()` cleanup, idempotent, stress-tested |
| Shimmer looks wrong | `IVeil` interface, dense fog fallback |
| Unity version mismatch | 6000.0.75f1 pinned in the Unity project and in `docs/` |
| Jötunn version mismatch client/server | `NetworkCompatibility` attribute, same Jötunn deployed by `make deploy-server` |

## 9. Out of scope

- Unity and PvP two-player testing infrastructure (second Steam account) is decided when step 7/8 is reached.
- Gitea remote: added when the URL is available.
- Localizations other than English.
