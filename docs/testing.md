# In-game testing checklist

Run `make run`, press F5 for the console, `devcommands` once per session. Update this file per plan.

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
