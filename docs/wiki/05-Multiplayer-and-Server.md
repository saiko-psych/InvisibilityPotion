# Multiplayer and Server

## Who needs the mod

InvisibilityPotion is set to **EveryoneMustHaveMod**. The server and every client need it, with the same major.minor version (0.3.x with 0.3.y works, 0.2 with 0.3 is refused). A vanilla client cannot join a server running the mod, and a client with the mod cannot join a server without it.

## Jötunn version rule

Server and clients need the **same Jötunn version down to the patch number**. The mod is built against Jötunn 2.30.0 and BepInExPack Valheim 5.4.2350. A mismatch shows a "Mod version mismatch Jotunn" dialog and the server disconnects the client. The BepInExPack version is not checked.

## Dedicated server

1. Stop the server.
2. Install [BepInExPack Valheim](https://thunderstore.io/c/valheim/p/denikson/BepInExPack_Valheim/) in the server folder (on Linux this includes `start_server_bepinex.sh`).
3. Put `Jotunn.dll` (2.30.0) into `BepInEx/plugins/Jotunn/` and `InvisibilityPotion.dll` into `BepInEx/plugins/InvisibilityPotion/`.
4. Start the server with BepInEx's start script instead of the plain one. Copy the script first and put your own arguments (`-name`, `-port`, `-world`, `-password`, ...) into the copy, because BepInExPack updates replace the original. Run it from the server folder.
5. If you use systemd: point `ExecStart` at the copied script, set `WorkingDirectory` to the server folder and use `KillSignal=SIGINT` so the world is saved on stop.
6. Check `BepInEx/LogOutput.log` for `Loading [Jotunn 2.30.0]`, `Loading [InvisibilityPotion 0.4.x]` and `Patch health: 23 targets patched, 0 missing`.

No `LogOutput.log` means BepInEx did not load: wrong start script or wrong working directory.

The server's config file is the one that counts: gameplay values are pushed to every client on join and on change. See [Configuration](04-Configuration) for the exceptions (recipes and world-generation values are read from each machine's own file).

## What tier III does on the network

- The **server** hides the player's real position from other players (no position updates) and removes the map pin. This is why the server needs the mod.
- Other clients see no nameplate and no player-list entry for the hidden player.
- A player wearing Allfather's Eye sees the nameplate, veiled body and real position, but never a map pin.
- Server switch: `AllowPvpInvisibility` in `[General]`.

## Known limitations

- **Mumble positional audio** sends your position to other Mumble users with positional audio and can reveal a tier III player. Outside the mod's control; tell PvP groups.
- **Map pin:** hidden players have no pin for others, and goggles never show one.
- **Admin-only values:** only admins can change gameplay values in game.
- **Recipes are local:** read at startup from each machine's file, not synced.
- **Retrofit is server-side:** wild plants for zones generated before the mod are placed by the server (`RetrofitExistingZones`); the server's config value counts, and the server does the work (one zone per player per 0.1 s, only zones near players). The server log shows `plants: retrofit zone (x,y): N placed` per zone with new plants and, every 25 evaluated zones, a summary line with the counters. `RetrofitReportOnly = true` is a dry run (counts, places nothing).
- Mods that replace AI perception, rewrite `ZDOMan.SendZDOs`, or replace nameplates may break hiding. See the FAQ for compatibility.

## Admin console commands

Open the console with F5 (the game needs `-console` in its launch options). On a dedicated server only admins can use them; the host and a local game always can. Every line also goes to `BepInEx/LogOutput.log`.

- `ip_plants [radius=64]`: every hidden plant in range with distance, direction, stage and growth timer; lichen tree counts; every sprout of this mod with its status and grow time. `ip_plants nearest` sets a map pin on the nearest one, `ip_plants unpin` removes it.
- `ip_retrofit`: state and counters of the zone retrofit. `ip_retrofit here` checks your current zone now (server/host only), `ip_retrofit reset` forgets which zones were checked this session.
