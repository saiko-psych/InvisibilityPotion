---
name: new-patch
description: Harmony patch checklist for this mod. Use when adding a Harmony patch (new target method, prefix, postfix or transpiler), moving a patch to another target, or removing one.
argument-hint: "[Type.Method, e.g. MonsterAI.UpdateSleep]"
---

# New Harmony patch

A patch touches five places that must agree: the decompile notes, the patch class, `Plugin.ExpectedPatchTargets`, the docs that
name the target count, and the in-game checklist. `PatchTargetCountTests` fails while the docs disagree with
`ExpectedPatchTargets`, so `make test` is the proof the count was carried everywhere.

## 1. Verify the target

- Find the method in `tools/decompiled/` (missing: `make decompile`, it needs the installed game; without it, tell the user and
  stop, because an unverified target is not written). Note the declaring type, exact signature, overloads, access, whether it is
  virtual or overridden, and the line number.
- Note who runs it in multiplayer (the ZDO owner, the server, every peer): the table in `docs/decompile-notes.md` "Where each hook runs
  (multiplayer)" is the model.
- Record it in `docs/decompile-notes.md`, next to the related entries, with `File.cs:line`, before writing code. Read
  "Findings that affect the design" there first.

Done when the notes entry names the signature, the line and the machine that runs it.

## 2. Write the patch

- One class per target in `InvisibilityPotion/Patches/<Area>Patches.cs` (a Debug-only patch goes into `InvisibilityPotion/Dev/`
  behind `#if DEBUG`). `[HarmonyPatch(typeof(DeclaringType), "Method")]`; overloads take explicit argument types (see
  `PerceptionPatches.cs`); add `[HarmonyPriority]` only with a reason in `docs/compatibility.md` §1.
- Keep the project rules: reveal hooks call only `MarkRevealed`; status effects are added only from `UpdateStatusEffect`; visuals
  come from `VeilController` reading ZDO state; every exit of the effect goes through `SE_Invisibility.Stop()`.
- A patch that calls into our code wraps it in `try/catch` and logs once (`VisualPatches.cs`, `CultivationPatches.cs`): a failure
  in the mod must leave vanilla running. A patch on a per-frame path allocates nothing (no LINQ, no `GetComponent` per call,
  reuse buffers).
- Logic without game types goes into a `*.Core.cs` file, linked in `InvisibilityPotion.Tests/InvisibilityPotion.Tests.csproj`,
  with an xunit test that fails before the change and passes after it.
- A transpiler matches one exact IL pattern, leaves the IL untouched otherwise and reports that to patch health (the
  `SpoofActive` pattern in `PlayerHidePatches.cs` and its use in `Plugin.cs`).

## 3. Register it

- `PatchHealth.TargetKey("DeclaringType", "Method")` in `Plugin.ExpectedPatchTargets`: the type that declares the method, even
  when patched through a subclass; Debug-only targets inside the `#if DEBUG` block at the top.
- Update the counts `make test` checks: `InvisibilityPotion/Package/README.md`, `docs/wiki/01-Installation.md`,
  `docs/wiki/05-Multiplayer-and-Server.md` (Release count), `docs/compatibility.md` (both counts) and `docs/release.md`. Add the
  row to the patch table in `docs/compatibility.md` §1 (kind, skips the original or not, priority, what it does).
- The CHANGELOG entry of the version names the new target and the new counts (`23 targets in Release builds, 25 in Debug`).

## 4. Prove it

- `make ci` passes (tests including `PatchTargetCountTests`, the Roslyn syntax check, scripts).
- `docs/testing.md` gets unchecked items under the version: the startup line `Patch health: N targets patched, 0 missing` with
  the new Debug count, and the behaviour the patch exists for, on the machine that runs it (singleplayer, host, dedicated server,
  second client). Without a game run here, mark the change `not verified in game` in the commit message and the PR.
- `make build` (game closed) and `make run` are the user's; ask for the startup log lines.

The patch is finished when all four sections are done and the user has the in-game items.
