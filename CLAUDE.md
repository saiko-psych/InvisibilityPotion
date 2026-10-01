# InvisibilityPotion – project conventions

Valheim mod (BepInEx 5.4.23.5 + Jötunn 2.30.2), C# net48, MIT. Everything in files is English.

## Commands (`make help` lists them)
- `make build` – Debug build, deploys the DLL to `$VALHEIM_INSTALL/BepInEx/plugins/` (via `scripts/publish.sh`)
- `make run` – build, launch Valheim through Steam (windowed, console), auto-join world `$IP_DEV_WORLD` (default `testing`, character `$IP_DEV_CHARACTER`, empty = first), then tail the BepInEx log
- `make log` – follow `BepInEx/LogOutput.log`
- `make test` – xunit tests (pure logic only, `InvisibilityPotion.Tests`, net8.0)
- `make decompile` – regenerate `tools/decompiled/` from the installed game; rerun after game updates
- `make package` – Release build + Thunderstore zip (`InvisibilityPotion/InvisibilityPotion.zip`)

## Rules
- Verify every game member in `tools/decompiled/` and record it in `docs/decompile-notes.md` before use. That file is the authority for game hooks and holds the findings that affect the design (read "Findings that affect the design" first).
- Every Harmony patch target is listed in `Plugin.ExpectedPatchTargets`; startup logs `Patch health: N targets patched, M missing`.
- Dev-only code lives in `InvisibilityPotion/Dev/` behind `#if DEBUG`; Release builds must not contain it.
- Pure logic goes into `*.Core.cs` files without game types; they are linked into `InvisibilityPotion.Tests`.
- Game stdout goes to Steam, so the BepInEx log is the only readable output. The in-game F5 console cannot be copied from: dev console commands must also write their output to `Plugin.Log`.
- One cleanup path: `SE_Invisibility.Stop()` restores everything. Every exit must go through it.
- Copy code only from MIT / MIT-0 / Unlicense sources and credit them in a comment.
- `sudo` commands are run by the user; print them and wait.

## Layout
See `docs/superpowers/specs/2026-09-30-invisibility-potion-design.md` §4. Plans live in `docs/superpowers/plans/`. In-game checklist: `docs/testing.md`.

## Environment
- Game: `~/.local/share/Steam/steamapps/common/Valheim` (BepInEx installed directly, no mod manager profile)
- One-time Steam launch option on Valheim: `./start_game_bepinex.sh %command%`. Starting the game binary directly from a terminal does not work here; always use `make run`.
- Dev auto-join reads `<game>/BepInEx/config/InvisibilityPotion.autojoin` (`world=`, `character=`), written by `make run`; env vars are only a fallback.
- Local test world: `testing`
- `Environment.props` (gitignored) holds `VALHEIM_INSTALL`; root `Directory.Build.props` sets `SolutionDir` so Jötunn's props load when building the csproj directly.
- `InvisibilityPotion/Package/plugins/` and `*.zip` are build output (gitignored).
