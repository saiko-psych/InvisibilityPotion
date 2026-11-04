---
name: release
description: Walk docs/release.md for one version, item by item, stopping before every outward-facing step.
disable-model-invocation: true
argument-hint: "[version, e.g. 0.4.2]"
---

# Release

`docs/release.md` is the checklist and the single source of truth for what a release contains; this skill is how to walk it.
Read it in full first. When the two disagree, the checklist wins: fix this file afterwards.

## 1. Pin the versions

- `<v>`: the argument, else `PluginVersion` in `InvisibilityPotion/Plugin.cs`. `<prev>`: `git tag --list 'v*' | sort -V | tail -1`.
- Say both to the user and wait for a confirmation before anything else. A `<v>` that is not higher than `<prev>` stops here.

## 2. Walk the checklist

Go through `docs/release.md` top to bottom and give every item exactly one status in a running table (`item | status | evidence`):

- **done**: you ran it here and the evidence is the command output (a passing `make ci`, the grep that shows the three versions
  agree, the zip listing).
- **user**: it needs the game, Steam, the dedicated server or an account (`make build`, `make run`, `make package`,
  `make compat-check`, `make decompile`, `scripts/deploy-proxmox.sh`, the uploads). Hand the user the exact command in its own
  `bash` block and what output to paste back; mark it done only from the pasted output. Builds need the game closed.
- **skipped**: only when the user says so, with their reason in the evidence column.

Item notes:

- **CHANGELOG wording**: the entry for `<v>` is read by players on Thunderstore and Nexus. Each line says what changes for the
  player (behaviour, a config key, compatibility between `<prev>` and `<v>`); class names, file names and review jargon go. Name
  config keys exactly as they appear in the cfg file. Show the rewritten entry before writing it.
- **The version's section in `docs/testing.md`**: list its unchecked items and walk them with the user like the rest; a
  `not verified in game` item stays open until the user reports the result.
- **Release date**: on release day the heading becomes `## <v> (YYYY-MM-DD)`; `make test` must still pass afterwards
  (`ReleaseVersionTests` reads the first heading).

## 3. Outward-facing steps

Deploying to a server, merging, pushing a tag, uploading to Thunderstore or Nexus and posting announcements are visible to other
people and hard to take back. Before each one, name the exact action and wait for an explicit go for that step; a go for one step
covers only that step. Pushes are plain pushes.

## 4. Finish

The release is finished when every checklist item is **done** or **skipped** with a reason. Then:

- `docs/publishing.md` "Live listings" gets `<v>` and the date (the last commit of the release).
- Report the final table to the user, with the open items from `docs/testing.md` that were skipped.
