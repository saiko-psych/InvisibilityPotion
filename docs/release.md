# Release checklist

One page for cutting a version, in order. Details live in the linked files; this list only says when to do what. In Claude Code,
`/release <v>` walks this list with you (`.claude/skills/release/`); `/new-patch` is the checklist for adding a Harmony target.
`<v>` is the new version (for example `0.4.2`), `<prev>` the last released one.

## 1. Before the build

- [ ] The version is the same in `InvisibilityPotion/Plugin.cs` (`PluginVersion`), `InvisibilityPotion/InvisibilityPotion.csproj`
      (`<Version>`) and `InvisibilityPotion/Package/manifest.json` (`version_number`). The newest heading of
      `InvisibilityPotion/Package/CHANGELOG.md` is `## <v>` (add the date on release day: `## <v> (YYYY-MM-DD)`).
      `ReleaseVersionTests` fails the tests otherwise, and also when the manifest's Jötunn dependency differs from `JotunnVersion`
      in the csproj.
- [ ] CHANGELOG wording is for players: what changed for them, config keys that are new or migrated, and whether
      server and clients may mix `<prev>` and `<v>` (only major.minor has to match, `docs/compatibility.md` §4).
- [ ] `make ci` passes (tests, Roslyn syntax check, scripts); the same runs on every pull request (`.github/workflows/ci.yml`).
- [ ] Changes that multiplayer touches (ZDO keys, RPCs, admin config, the tier III spoof): `/security-audit`. Changed
      defaults (durations, cooldowns, debuffs, plant rates, yields, recipes): `/balance-check`.
- [ ] Valheim updated since the last release? `make decompile`, diff the patch targets against `docs/decompile-notes.md`, record
      what changed. Check Jötunn and BepInExPack_Valheim on Thunderstore for new versions (`docs/compatibility.md` §7: the
      servers decide the Jötunn pin, clients must match it to the patch number).

## 2. Build and test (game closed for every build)

- [ ] `make build`, then `make run`: the version's section in `docs/testing.md` (startup line
      `Patch health: 25 targets patched, 0 missing` in Debug).
- [ ] Changes in `Visuals/`, `Net/` or `Plants/`: the soak test in `docs/testing.md` ("Soak test").
- [ ] `make compat-check` (compiles against the newest Jötunn into `build/compat/`; it neither deploys nor packages).
- [ ] `make package`: Release DLL + `InvisibilityPotion/InvisibilityPotion.zip`. Check the zip:
      `unzip -l InvisibilityPotion/InvisibilityPotion.zip` lists `manifest.json`, `README.md`, `CHANGELOG.md`, `icon.png` and
      `plugins/InvisibilityPotion.dll`.

## 3. Server

- [ ] The author's server: `scripts/deploy-proxmox.sh` (refuses a Debug DLL), connect once to wake it, then
      `scripts/deploy-proxmox.sh --logs`: `Loading [InvisibilityPotion <v>]`, `Patch health: 23 targets patched, 0 missing`.
      Other servers: `scripts/deploy-server.sh` (`docs/server.md`).
- [ ] Join with a client on `<v>` (and, if the CHANGELOG says they mix, one on `<prev>`).

## 4. Publish (`docs/publishing.md` has the site details)

- [ ] Merge the release pull request into `main`.
- [ ] Thunderstore: upload the zip to the EdgeExploxers team. Update the wiki pages whose source changed:
      `git diff v<prev> -- docs/wiki/`.
- [ ] Nexus Mods: "Update existing file" with the same zip, archive the old file, set display name and version, tick
      "Update mod version", paste the CHANGELOG entry.
- [ ] Tag and GitHub release: `git tag v<v>` on the merge commit, push the tag, attach the zip to the release.
- [ ] `docs/publishing.md` "Live listings": add the version and date.
- [ ] Optional: announcement in the Valheim Modding Discord with both links.
