# Plan 4 – Custom 3D assets: Unity asset bundle, bottles, bowls, plants, goggles

Status: draft for review · 2026-10-02 · amends the main spec (`2026-09-30-invisibility-potion-design.md`) §5.9 Items and §4 layout.

## 1. Goal

Replace the cloned vanilla mead prefabs with the mod's own low-poly models, delivered through one Unity asset bundle embedded in the plugin DLL, and establish a repeatable pipeline (Blender script → FBX → Unity batch build → bundle → C#) that later plans reuse for the plants and the goggles. At the end of plan 4 the three veil meads and their three unfermented bases have their own look in the inventory, in the hand, on the ground and on item stands, with animated mist inside the finished meads; the plant and goggle models are in the bundle and spawnable through dev commands for look iteration, but have no gameplay yet.

## 2. Decisions taken

| Decision | Choice | Why |
|---|---|---|
| Unity version | 6000.0.75f1 (the game's own version, installed) | Bundle serialization must match the player; verified from `Player.log` |
| Unity project content | **Option A: pure assets.** Meshes, materials, dummy mock shaders, empty anchors. No game DLLs in Unity; C# adds `ItemDrop`, `ZNetView`, colliders and sets `m_shared` (as `PotionItems.cs` already does) | No copyrighted binaries in the repo, no stub-GUID gymnastics, survives game updates; research §1 |
| Shaders | Only `JVLmock_<vanilla shader name>` dummy shaders in the bundle; Jötunn swaps the real shader at load with `fixReference: true`. Candidates: `Custom/Creature` (opaque body), `Custom/Distortion` (glass), `Custom/LitParticles` (mist) — settled by spike S2 | Research §1: `fixReference` only resolves mock-named shaders; mock shaders make the bundle platform-neutral |
| Build targets | `StandaloneLinux64` for development; `StandaloneWindows64` added after the user installs the `windows-mono` Hub module (decided: now). Spike S1 decides whether one bundle serves both | Research §2 |
| Bundle in git | **Committed** at `InvisibilityPotion/Assets/ip_assets` (user decision) | `make build` works without Unity on any checkout |
| Mead base look | **Own model**: a wooden bowl in the manner of the vanilla mead bases, with tier-coloured, murky contents and no mist (user decision) | Reads as "not yet fermented"; distinct from the finished bottles |
| Finished meads | Bottles from `tools/blender/make_bottle.py` v5 (fixes from the v4 review: slimmer silver base cup on T3, readable facets, label band/rune, no red caps — none have them) | Already generated and reviewed |
| Mist | A code-built `ParticleSystem` on the `MistAnchor` child, reusing `FogVeil`'s material borrowing (`swamp_mist` copy) with small size, inside the glass; only on finished meads | One technique for all fog; no bundle particles needed |
| Plants, goggles | In the bundle as plain `CustomPrefab`s (`Plant_T1..3`, `Goggles_T1..3`), spawnable with `ip_spawn`; items, pickables and vegetation are plan 5 | Look iteration now, gameplay later |
| Icons | `RenderManager.Instance.Render(prefab, IsometricRotation)` as today | Works for any prefab |
| Textures | None yet: flat material colours per part (Valheim-like palette). Alpha-card textures (lichen, fern leaflets) are a follow-up once the pipeline runs | Keep plan 4 about the pipeline |
| Directory | `unity/InvisibilityPotionAssets/` (the old `InvisibilityPotionUnity/` ignore lines in `.gitignore` are replaced) | Matches the research layout |

## 3. Targets and versions

Unchanged from the main spec: Valheim 1.0.16, BepInEx 5.4.23.5, Jötunn 2.30.2, C# net48. Blender 5.2 (`blender` on PATH), Unity 6000.0.75f1 at `~/Unity/Hub/Editor/6000.0.75f1/Editor/Unity`.

## 4. Repository layout (additions)

```
tools/blender/make_bottle.py        # v5
tools/blender/make_bowl.py          # mead bases
tools/blender/make_plants.py        # exists (v2)
tools/blender/make_goggles.py       # exists (v1)
tools/blender/common.py             # shared: materials, FBX export (exact anchor names), preview scene
unity/InvisibilityPotionAssets/
  Assets/Models/*.fbx               # written by `make models`; committed
  Assets/Materials/*.mat            # one per material name used by the scripts
  Assets/Shaders/JVLmock_*.shader   # dummy stumps
  Assets/Prefabs/*.prefab           # MeadBottle_T1..3, MeadBowl_T1..3, Plant_T1..3, Goggles_T1..3
  Assets/Editor/BundleBuilder.cs    # BuildPipeline.BuildAssetBundles, target from -target arg
  Packages/manifest.json, ProjectSettings/   # committed
  .gitignore                        # Library/ Temp/ Logs/ obj/ UserSettings/ Build/ *.csproj *.sln
InvisibilityPotion/Assets/ip_assets  # committed bundle (EmbeddedResource)
InvisibilityPotion/Items/AssetBundles.cs   # load once, LoadAsset, Unload(false) after registration
InvisibilityPotion/Items/ModelPrefabs.cs   # adds ItemDrop/ZNetView/collider/mist to a bundle prefab
docs/assets.md                      # the pipeline guide (how to add a model, naming rules, spikes' results)
```

Makefile: `make models` (all Blender scripts → `Assets/Models`), `make bundle [TARGET=linux|windows]` (refuses while `Temp/UnityLockfile` exists, batch build, copies the bundle), `make assets` = both. `build`/`package` fail with a clear message when `InvisibilityPotion/Assets/ip_assets` is missing.

## 5. Components

### 5.1 Blender side
- Every script writes `<name>.fbx` (Y-up, 1 unit = 1 m, pivot per asset: bottles/bowls/plants at the base, goggles at the head centre), materials named exactly as the Unity `.mat` files, anchor empties with exact names (`MistAnchor`, `PickAnchor`, `EmberAnchor`, `attach`) — the `.001` suffix bug of the bottle script is fixed in `common.py`.
- `attach` child: every item prefab gets an empty `attach` at the grip point (bottles: neck; bowls: rim; goggles: head centre) because `VisEquipment` finds the held/placed visual by that name (research §3). Spike S6 confirms whether it is required; it is added regardless.
- Previews: one PNG per script, controller-inspected before the user sees them (project rule).

### 5.2 Unity side
- Materials: flat colour, shader = dummy mock. Transparent glass uses `JVLmock_Custom/Distortion` with `_Color.a` from the bottle script's `GLASS_ALPHA`; if S2 shows no usable transparency, fallback is an opaque tinted glass with `Custom/Creature`.
- Prefabs: root (empty) → `model` (MeshRenderer) → anchors. Nothing else; components come from C#.
- `BundleBuilder.Build`: reads `-target linux|windows` from the command line, builds into `Build/Bundles/<target>/`, `ChunkBasedCompression | StrictMode`, assigns the bundle name `ip_assets` to everything under `Assets/Prefabs` and `Assets/Materials`.

### 5.3 C# side
- `AssetBundles.Load()` in `Plugin.Awake` (before `OnVanillaPrefabsAvailable`); logs the manifest resource names once in Debug (S5).
- `ModelPrefabs.MakeItem(GameObject prefab, ItemDrop template)`: adds `ZNetView` (persistent), `ZSyncTransform`, `Rigidbody`, a `BoxCollider` sized from the renderer bounds, `ItemDrop` with `m_itemData.m_shared` copied from the vanilla template (`MeadHealthMinor` / `MeadBaseHealthMinor` via `PrefabManager.Cache`) and then overridden as today (name, type, `m_isDrink`, status effect, food zeros); `MakeMist(prefab)` attaches the mist system to `MistAnchor`.
- `PotionItems.Register` switches from `CustomItem(name, "MeadHealthMinor", …)` clones to `new CustomItem(builtPrefab, fixReference: true, ItemConfig)`; recipes, fermenter conversions and localization unchanged. The `Tint()` material copy is removed.
- Plants and goggles: `PrefabManager.Instance.AddPrefab(new CustomPrefab(prefab, fixReference: true))` with a `ZNetView` only, so `ip_spawn Plant_T1` works.
- New Debug commands: `ip_components <prefab>` (components + children, S4), `ip_shaderdump <prefab>` (`ShaderHelper.ShaderDump`, S2), `ip_bundle` (lists the bundle's assets and the resolved shader of each material after load).

### 5.4 Spikes (Task 1 of the plan, before any model goes in)
S1 cross-platform mock bundle · S2 glass shader and `Standard` viability · S3 Personal licence in batch mode · S4 vanilla mead component dump · S5 embedded resource name · S6 held item without `attach` · S7 editor-version warnings. Each spike's result is recorded in `docs/assets.md`; S2's outcome picks the glass shader.

## 6. Build and test loop

`make assets && make build && make play`; in-game: `ip_give 1..3` shows the new bottles in the inventory and the hand, drop one on the ground and on an item stand, open the fermenter list, `ip_spawn Plant_T1`, `ip_spawn Goggles_T3`, screenshots to the chat; `ip_bundle` output in the log. Checklist section "Plan 4" in `docs/testing.md`.

## 7. Build order

1. Spikes S1–S7 with a one-cube bundle (settles shaders, licence, resource name).
2. Unity project skeleton, `BundleBuilder`, Makefile targets, `docs/assets.md`.
3. `common.py` + bottle v5 + bowl script + previews.
4. Bundle with all prefabs; C# `AssetBundles`/`ModelPrefabs`; meads switched; mist.
5. Plants and goggles as spawnable prefabs; dev commands; checklist; Windows bundle once the Hub module is installed.

## 8. Risks

- Shaders: `Custom/Distortion` may not read as glass or may be stripped on a dedicated server (Jötunn skips shader fixing on headless, research §1) → fallback opaque tint; headless does not render.
- Unity batch mode may need licence activation (S3) → the user activates once through the Hub.
- A bundle built on Linux may not load on Windows if any real shader slips in (S1) → the Windows module is installed and `make bundle TARGET=windows` exists.
- `attach` naming and item-stand orientation are learned in-game (S4/S6); one iteration round expected.

## 9. Out of scope

Plant gameplay (pickables, vegetation spawning, goggle items, reveal effect) → plan 5. Textures and alpha cards → follow-up. Wisps as creatures → idea backlog.
