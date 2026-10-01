# In-game testing checklist

Run `make run`, press F5 for the console. Run `devcommands` once per session only where vanilla cheat commands are needed (Jötunn commands such as `ip_state` work without it). Update this file per plan.

## Plan 1 – skeleton
- [x] Log shows `InvisibilityPotion 0.1.0 loaded` and `Patch health: … 0 missing`
- [x] `make run` lands in the world `testing` without touching the menu
- [x] `ip_state` prints patched targets and `missing: none`
- [x] Release build (`make package`) contains no `ip_state` command and no auto-join

## Test matrix (from the spec, filled in from plan 2 on)

| Setup | Tier I | Tier II | Tier III |
|---|---|---|---|
| Singleplayer | | | |
| Self-hosted, I am host | | | |
| Dedicated, I own the zone | | | |
| Dedicated, another player owns the zone | | | |
| Player joins while I am hidden | | | |

## Plan 2 – Tier III hidden from other players

Singleplayer (checkable now):
- [ ] `ip_give 3`, then `ip_state` prints `hiddenFromPlayers=True`; after the effect ends or a reveal it prints `False`
- [ ] Log shows `Patch health: … 0 missing` with `EnemyHud.TestShow`, `ZNet.UpdatePlayerList`, `ZDOMan.SendZDOs` patched, and no `SendZDOs transpiler: pattern not found` error

Two clients (pending, needs plan 3 server):
- [ ] Second client: no nameplate over the hidden player
- [ ] Second client: no map pin for the hidden player (even with "share position" on)
- [ ] Second client: the hidden player is not rendered near their real position while hidden
- [ ] After a reveal or the end of the effect, the player appears on the second client within one send interval
- [ ] AI in the second client's zone still ignores the hidden player
- [ ] With `AllowPvpInvisibility = false` on the server, tier III hides from enemies only
