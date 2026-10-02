# Dedicated server (Linux)

How to run InvisibilityPotion on a Linux Valheim dedicated server (plan 3 groundwork). Everything goes over SSH from the dev
machine; no secrets are stored in the repository. Set up user, port and key for the server in `~/.ssh/config` and pass the
`Host` alias as `<ssh-host>`; the scripts use `ssh -o BatchMode=yes`, so key-based login must work without a prompt.

Why the server needs the mod: `[NetworkCompatibility(EveryoneMustHaveMod, Minor)]` makes Jötunn refuse connections unless
server and clients run the same 0.3.x, the server hides tier III positions (`ZDOMan.SendZDOs`) and map pins
(`ZNet.UpdatePlayerList`), and the server's config values are pushed to every client (see `docs/compatibility.md` §4).

## Scripts

| Script | Use |
|---|---|
| `scripts/server-setup.sh [--dry-run] [--force] <ssh-host> <server-path>` | One time on a vanilla server: installs BepInExPack_Valheim, Jötunn and our DLL. Rerunnable. |
| `scripts/deploy-server.sh [--dry-run] [--force] <ssh-host> [server-path]` | Every later update: copies only our DLL (the asset bundle is embedded). `server-path` defaults to `$IP_SERVER_PATH`. |

Both take `--dll FILE` (default `InvisibilityPotion/Package/plugins/InvisibilityPotion.dll`, the output of `make package`) and
refuse a Debug DLL (it contains the dev console commands). Both refuse while `valheim_server.x86_64` runs on the server unless
`--force`; a plugin is only loaded at startup, so the server needs a restart anyway. `server-path` is absolute or relative to the
remote home directory (no `~`). Nothing on the server is ever deleted.

`server-setup.sh` in detail:

1. Checks that `<server-path>/valheim_server.x86_64` exists.
2. Downloads `denikson-BepInExPack_Valheim` (default 5.4.2351, `--bepinex`) and `ValheimModding-Jotunn` (default 2.30.2,
   `--jotunn`) from `https://thunderstore.io/package/download/<namespace>/<name>/<version>/` into `build/server/` (cached,
   gitignored). Package layout checked on 2026-10-02: the pack's files live under `BepInExPack_Valheim/` in the zip
   (`start_server_bepinex.sh`, `doorstop_config.ini`, `.doorstop_version`, `doorstop_libs/`, `BepInEx/core/`,
   `BepInEx/config/BepInEx.cfg`, plus the Windows `winhttp.dll` and client `start_game_bepinex.sh`, which are skipped); Jötunn
   ships `plugins\Jotunn.dll` (backslash paths, so the script extracts with Python instead of `unzip`). The script fails if
   either layout changes.
3. Stages `BepInEx/plugins/Jotunn/Jotunn.dll` and `BepInEx/plugins/InvisibilityPotion/InvisibilityPotion.dll` next to the pack
   files in `build/server/stage/`.
4. `rsync` to the server in two passes: everything except `BepInEx/config/` and `start_server_bepinex.sh`, then those two only
   when they do not exist yet (`--ignore-existing`), so a customised `BepInEx.cfg` or start script survives a rerun.
5. Prints the next steps below with the real host and path filled in.

## First-time setup, in order

1. On the dev machine, with the game closed: `make package` (Release DLL + Thunderstore zip). If the game is running, build
   Release directly: `dotnet build InvisibilityPotion/InvisibilityPotion.csproj -c Release` (a Release build never touches the
   game folder; only the Makefile guard refuses).
2. Stop the Valheim server on the host.
3. `scripts/server-setup.sh --dry-run <ssh-host> <server-path>`, read the rsync list, then run it without `--dry-run`.
4. Switch the server's start command to BepInEx. `start_server_bepinex.sh` exports the doorstop variables
   (`DOORSTOP_ENABLED=1`, `DOORSTOP_TARGET_ASSEMBLY=./BepInEx/core/BepInEx.Preloader.dll`, `LD_PRELOAD=libdoorstop_x64.so`,
   `LD_LIBRARY_PATH=./doorstop_libs:./linux64`, `SteamAppId=892970`) and ends with
   `exec ./valheim_server.x86_64 -name "My server" -port 2456 -world "Dedicated" -password "secret"`. Copy it once and put your
   current arguments into the copy (BepInExPack updates replace the original):
   ```sh
   cd <server-path>
   cp -n start_server_bepinex.sh start_server_ip.sh && chmod +x start_server_ip.sh
   # edit the exec line: -name, -world, -password, -public, -crossplay, -savedir ... as in the current start command
   ```
   It must run with the server directory as working directory (relative `./BepInEx`, `./doorstop_libs`, `./linux64`).
5. If the server runs under systemd, point the unit at the copy (installing or editing a unit needs `sudo`, run by the user):
   ```ini
   [Unit]
   Description=Valheim dedicated server (BepInEx)
   After=network-online.target
   Wants=network-online.target

   [Service]
   User=valheim
   WorkingDirectory=/absolute/server-path
   ExecStart=/absolute/server-path/start_server_ip.sh
   KillSignal=SIGINT
   TimeoutStopSec=60
   Restart=on-failure

   [Install]
   WantedBy=multi-user.target
   ```
   `KillSignal=SIGINT` lets the server save the world on stop. Then `sudo systemctl daemon-reload && sudo systemctl restart <unit>`.
6. Start the server and verify (next section).

## Verify

```sh
ssh <ssh-host> "grep -E 'BepInEx|Jotunn|InvisibilityPotion|Patch health' <server-path>/BepInEx/LogOutput.log"
```

Expected lines:

- `Loading [Jotunn 2.30.2]` and `Loading [InvisibilityPotion 0.3.x]`
- `Patch health: N targets patched, 0 missing` (Release builds list no Debug-only targets)
- `InvisibilityPotion 0.3.x loaded`
- No `SendZDOs transpiler: pattern not found` (that line means the tier III position spoof is off; patch health does not report
  it, see `docs/compatibility.md`).

No `LogOutput.log` at all means BepInEx did not load: the server was started without `start_server_bepinex.sh`, or the
working directory is wrong. A client that connects with a different InvisibilityPotion major.minor (or without it) is
refused by Jötunn with a version mismatch dialog.

## Updates

```sh
make package                                            # or the dotnet Release build above while the game runs
scripts/deploy-server.sh --dry-run <ssh-host> <server-path>
# stop the server
scripts/deploy-server.sh <ssh-host> <server-path>
# start the server, verify as above
```

`deploy-server.sh` checks that BepInEx (`BepInEx/core/BepInEx.Preloader.dll`) and a `Jotunn.dll` under `BepInEx/plugins/`
exist before copying. For a new Jötunn or BepInExPack version, rerun `server-setup.sh` with `--jotunn`/`--bepinex` (and update
`InvisibilityPotion/Package/manifest.json`).

## Configuration on the server

- File: `<server-path>/BepInEx/config/saikopsych.InvisibilityPotion.cfg`. It is created on the first start with the mod; stop
  the server before editing it by hand (the mod writes the file when values change).
- Gameplay sections (`[TierN]`, `[General]`, `[Plants]`, `[Goggles]`) are server-synced through Jötunn: on join every client
  gets the server's values, and the server's values win over the client files for the whole session.
- Admins (Steam/platform IDs in the server's `adminlist.txt`) can also change those values in game with ConfigurationManager
  (F1); Jötunn sends the change to the server, which saves it and pushes it to every client. Non-admins see them read-only.
- Not effectively synced (read once at startup from each machine's own file): mead base and goggle recipes,
  `HuldraCultivable`, `CultivateMinutes`, `BaldrZoneChance`, `HelFernZoneChance`. Give players the same values (or keep the
  defaults) or crafting and world generation differ between machines.
- Look settings (`[Fog]`, `[Fog.TierN]`, `[Veil]`) are client-only; the server's values do not matter.

## Notes

- The plugin is not yet tested on a headless dedicated server (asset bundle load and icon rendering run at startup; both log
  errors instead of failing the plugin). The first server start is the test: check the log for `Asset bundle load failed`,
  `Item registration failed` or other `[Error  :InvisibilityPotion]` lines.
- The user's server is an LXC container with a vanilla Valheim server and no BepInEx yet; host and path come later.
