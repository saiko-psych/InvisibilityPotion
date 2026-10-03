# InvisibilityPotion – project conventions

Valheim mod (BepInEx 5.4.23.5 from BepInExPack_Valheim 5.4.2350 + Jötunn 2.30.0, the versions of the dedicated servers; Jötunn refuses a client whose Jötunn patch version differs from the server, see `docs/compatibility.md` §7), C# net48, MIT. Everything in files is English.

## Commands (`make help` lists them)
- `make build` – Debug build, deploys the DLL to `$VALHEIM_INSTALL/BepInEx/plugins/InvisibilityPotion/` (via `scripts/publish.sh`). `build`, `package` and `run` refuse while Valheim runs (overwriting the DLL crashes the game); `FORCE=1` overrides
- `make run` – build, launch Valheim through Steam (windowed, console), auto-join world `$IP_DEV_WORLD` (default `testing`, character `$IP_DEV_CHARACTER`, empty = first), then tail the BepInEx log
- `make log` – follow `BepInEx/LogOutput.log`
- Dev console commands (Debug only, all log through `Plugin.Log`): `ip_state`, `ip_give <1|2|3>`, `ip_end` (removes every InvisibilityPotion status effect from yourself), `ip_give goggles <1|2|3>`, `ip_give ingredient <1|2|3> [amount]`, `ip_spawn <prefab> [count] [level]` (any prefab, e.g. `Plant_T2`, `VeilGoggles_T3`, `VeilIngredient_T2`), `ip_reload_config`, `ip_prefabs <substring>`, `ip_veil`, `ip_fog`, `ip_fogui`, `ip_components <prefab>`, `ip_shaderdump <prefab>`, `ip_bundle`, `ip_exportmesh <prefab|head>` (OBJ to `BepInEx/export/`), `ip_matdump <prefab> | clutter [substring]` (material shader properties), `ip_goggles <0..3|off>`, `ip_plants [radius|nearest|unpin]`, `ip_grow [stage]`, `ip_lichen <force|off|clear|roll>`, `ip_veg` (`Dev/DevCommands.cs` has the exact syntax in each `Help`)
- `make test` – xunit tests (pure logic only, `InvisibilityPotion.Tests`, net8.0)
- `make compat-check` – compiles against the newest Jötunn (`JOTUNN_LATEST`, default 2.30.2) into `build/compat/` to prove the pinned 2.30.0 build also runs on newer releases; run it before every release
- `make decompile` – regenerate `tools/decompiled/` from the installed game; rerun after game updates
- `make package` – Release build + Thunderstore zip (`InvisibilityPotion/InvisibilityPotion.zip`); package files in `InvisibilityPotion/Package/` (manifest, README, CHANGELOG, icon from `blender -b --python tools/blender/make_bottle.py -- --icon`). Bump the version in `Plugin.cs`, the csproj and `manifest.json` together
- `scripts/server-setup.sh <ssh-host> <server-path>` (one time: BepInExPack + Jötunn + DLL on a Linux dedicated server) and `scripts/deploy-server.sh <ssh-host> [server-path]` (DLL updates); both have `--dry-run`, see `docs/server.md`. Mod compatibility notes: `docs/compatibility.md`
- `make models` – Blender scripts → FBX into the Unity project; `make bundle [TARGET=linux|windows]` – headless Unity bundle build → `InvisibilityPotion/Assets/ip_assets[.windows]` (committed, embedded); see `docs/assets.md`

## Rules
- Verify every game member in `tools/decompiled/` and record it in `docs/decompile-notes.md` before use. That file is the authority for game hooks and holds the findings that affect the design (read "Findings that affect the design" first).
- Every Harmony patch target is listed in `Plugin.ExpectedPatchTargets`; startup logs `Patch health: N targets patched, M missing`.
- Reveal hooks only call `MarkRevealed`; status effects are added only from `UpdateStatusEffect`.
- Visuals are driven by `VeilController` from ZDO state, never from the status effect.
- Never build while `valheim.x86_64` runs: a deploy under a running game crashes it (pdb mismatch). `make build`/`package`/`run` refuse; `FORCE=1` overrides.
- Patch health keys are `Type.Method`; the `CanHearTarget`/`CanSeeTarget` keys cover the static overloads only because those are the only ones patched.
- Dev-only code lives in `InvisibilityPotion/Dev/` behind `#if DEBUG`; Release builds must not contain it.
- Pure logic goes into `*.Core.cs` files without game types; they are linked into `InvisibilityPotion.Tests`.
- Game stdout goes to Steam, so the BepInEx log is the only readable output. The in-game F5 console cannot be copied from: dev console commands must also write their output to `Plugin.Log`.
- Patch health keys use the declaring type of the method (for example a method declared on `Character` is `Character.X` even when patched through `Player`).
- One cleanup path: `SE_Invisibility.Stop()` restores everything. Every exit must go through it.
- Copy code only from MIT / MIT-0 / Unlicense sources and credit them in a comment.
- `sudo` commands are run by the user; print them and wait.

## Layout
See `docs/superpowers/specs/2026-09-30-invisibility-potion-design.md` §4. Plans live in `docs/superpowers/plans/`. In-game checklist: `docs/testing.md`.

## Environment
- Game: `~/.local/share/Steam/steamapps/common/Valheim` (BepInEx installed directly, no mod manager profile)
- One-time Steam launch option on Valheim: `./start_game_bepinex.sh %command%`. Use `make run`; launching the binary directly is untested on this machine.
- Dev auto-join reads `<game>/BepInEx/config/InvisibilityPotion.autojoin` (`world=`, `character=`). `make run` writes that file from the Makefile variables `IP_DEV_WORLD` (default `testing`) and `IP_DEV_CHARACTER` (default empty = first character), overridable as `make run IP_DEV_WORLD=other`. The plugin falls back to the env vars of the same names only when the file or a key is missing (env vars do not reach a game started through Steam). The file is one-shot: the plugin deletes it after reading, so a normal Steam start does not auto-join.
- Local test world: `testing`
- The author's server (Proxmox host `proxymoxy`, LXC CT 132) is deployed only with `scripts/deploy-proxmox.sh` (after `make package`), never with `server-setup.sh`/`deploy-server.sh`; see `docs/server.md`.
- `Environment.props` (gitignored) holds `VALHEIM_INSTALL`; root `Directory.Build.props` sets `SolutionDir` so Jötunn's props load when building the csproj directly.
- `InvisibilityPotion/Package/plugins/` and `*.zip` are build output (gitignored).
