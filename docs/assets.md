# Asset pipeline

Custom models reach the game through one Unity asset bundle, `ip_assets`, embedded in the plugin DLL. Design: `docs/superpowers/specs/2026-10-02-plan4-assets-design.md`; background: `docs/ideas/2026-10-02-asset-pipeline-research.md`.

## Unity project

`unity/InvisibilityPotionAssets/` (Unity 6000.0.75f1, the game's own version, built-in render pipeline, no game DLLs).

| Path | Content |
|---|---|
| `Assets/Shaders/JVLmock_Custom_*.shader` | Dummy stumps named `JVLmock_Custom/Creature`, `/Distortion`, `/LitParticles`, `/Piece`, `/Vegetation` (the last name is assumed, see below). Jötunn swaps in the vanilla shader at runtime (`fixReference: true`); only the `Shader "..."` line matters. They declare the properties the materials set (`_Color`, `_MainTex`, `_EmissionColor`, `_Glossiness`, `_Metallic`, normal map, `_Cutoff`; Distortion also `_RefractionIntensity`) so the values survive. |
| `Assets/Models/*.fbx` | Blender output, copied by `make models`; committed with their `.meta` (importer settings and material remaps are written by `AssetSetup`). |
| `Assets/Materials/*.mat` | In the bundle. One per Blender material name, written by `AssetSetup`. |
| `Assets/Prefabs/*.prefab` | In the bundle, written by `AssetSetup`. Layout see "Models and prefabs". Components come from C#. |
| `Assets/Textures/ip_white.asset` | 4x4 white albedo set as `_MainTex` on every material (the vanilla shaders' default texture is unknown). |
| `Assets/Editor/BundleBuilder.cs` | `BundleBuilder.Build`: reads `-target linux\|windows` (default linux), assigns bundle name `ip_assets` to everything under `Assets/Prefabs` and `Assets/Materials`, builds `Build/Bundles/<target>/ip_assets` with `ChunkBasedCompression \| StrictMode`, exits 1 on failure. |
| `Assets/Editor/AssetSetup.cs` | `AssetSetup.Create`: importer settings, materials and prefabs for every FBX in `Assets/Models` (no GUI needed). Replaces the spike's `SpikeSetup`. |
| `Packages/manifest.json` | Minimal: `com.unity.modules.assetbundle` (required, see below) and `com.unity.toolchain.linux-x86_64` (Unity adds it on its own on Linux). |

Never open the project in the editor while `make bundle` runs; the Makefile refuses while `Temp/UnityLockfile` exists.

## Commands

```
make models                  # every tools/blender/make_*.py headless, then copy tools/blender/out/*.fbx to Assets/Models
make models SKIP_BLENDER=1   # only copy what tools/blender/out/ already holds
make unity-setup             # AssetSetup.Create, log: build/unity-setup.log
make bundle                  # Linux bundle -> InvisibilityPotion/Assets/ip_assets, log: build/unity-bundle-linux.log
make bundle TARGET=windows   # Windows bundle -> InvisibilityPotion/Assets/ip_assets.windows, log: build/unity-bundle-windows.log
```

Underlying call (`UNITY` defaults to `~/Unity/Hub/Editor/6000.0.75f1/Editor/Unity`):

```
$UNITY -batchmode -nographics -quit -projectPath unity/InvisibilityPotionAssets \
  -executeMethod BundleBuilder.Build -target linux -logFile build/unity-bundle-linux.log
```

`InvisibilityPotion/Assets/ip_assets` is committed and embedded through `<EmbeddedResource Include="Assets\ip_assets" />`, so `make build` works without Unity.

Full rebuild after a model change: `make models && make unity-setup && make bundle && make bundle TARGET=windows`, then commit the FBX files, the generated `.mat`/`.prefab`/`.meta` files and both bundles.

## Models and prefabs

`AssetSetup.Create` turns each `Assets/Models/<file>.fbx` into `Assets/Prefabs/<Prefab>.prefab`:

| FBX | Prefab | Registered in game as | Layout |
|---|---|---|---|
| `bottle_t1..3` | `MeadBottle_T1..3` | item `MeadInvisibility_T1..3` (clone; mist added) | root -> `attach` -> `model` (`mist_tN` renderer disabled, `MistAnchor`, FBX `attach` empty) |
| `bowl_t1..3` | `MeadBowl_T1..3` | item `MeadBaseInvisibility_T1..3` (clone) | root -> `attach` -> `model` |
| `goggles_t1..3` | `Goggles_T1..3` | helmet item `VeilGoggles_T1..3` (clone) | root -> `attach` -> `model` |
| `ingredient_t1..3` | `Ingredient_T1..3` | material item `VeilIngredient_T1..3` (clone, template `Thistle`, stack 20) | root -> `attach` -> `model` |
| `plant_t1_s1..s3`, `plant_t1`, `plant_t1_s3_a/b/c`, `plant_t1_flat`, `plant_t1_flat_a/b/c`, `plant_t2`, `plant_t2_a..e`, `plant_t2_picked`, `plant_t3`, `plant_t3_a..e`, `plant_t3_picked` | `Plant_T1_S1..S3`, `Plant_T1`, `Plant_T1_S3_a/b/c`, `Plant_T1_Flat`, `Plant_T1_Flat_a/b/c`, `Plant_T2`, `Plant_T2_a..e`, `Plant_T2_picked`, `Plant_T3`, `Plant_T3_a..e`, `Plant_T3_picked` (25) | the bundle prefab itself as `CustomPrefab` with a non-persistent `ZNetView` (`ip_spawn Plant_T2`) | root -> `model` (`PickAnchor`, `EmberAnchor` on T3) |

Name rule (`AssetSetup.PrefabName`): `bottle`/`bowl`/`goggles`/`ingredient`/`plant` become `MeadBottle`/`MeadBowl`/`Goggles`/`Ingredient`/`Plant`, `tN`/`sN` upper case, `flat` -> `Flat`, other suffixes (`a`..`e`, `picked`) stay as they are. Un-suffixed plant files are variant a (identical meshes; kept so the base names exist).

Why `root -> attach -> model` for items: Valheim shows only the **direct child named `attach`** of an item prefab, instantiated at the hand/head joint or the item stand's attach point with an identity local transform (`VisEquipment.AttachItem`, `ItemStand.SetVisualItem`; `docs/decompile-notes.md`, "Asset items"). So the whole model lives inside `attach`, shifted so the FBX's own `attach` empty (grip point: bottle neck, bowl rim at +X, goggles head centre) lands on the `attach` origin. On the ground the same object is what you see; the C# `BoxCollider` is sized from the mesh bounds.

Materials (one `.mat` per Blender material name, shared across FBX files):

| Material names | Stump shader | Values |
|---|---|---|
| `bottle_glass_t1..3` | `JVLmock_Custom/Creature` (fix round 2: Distortion read as broken on a bottle; vanilla potions are opaque `Custom/Creature`) | `_Color` opaque, the Blender colour lerped 25 % toward white. `AssetSetup.UseDistortionGlass = true` restores the Distortion glass |
| `amber_lens`, `crystal_lens`, `obsidian_lens` | `JVLmock_Custom/Distortion` | `_Color` with the Blender alpha (lenses 0.6 / 0.6 / 0.88 in goggles v3), `_RefractionIntensity` 0.02, `_Glossiness` 0.9 |
| plant foliage: `huldra_strand`, `huldra_strand_shade`, `huldra_tip`, `baldr_leaf`, `baldr_petal`, `helfern_frond`, `helfern_frond_char` | `JVLmock_Custom/Vegetation` (vanilla name confirmed in the game data, round N) | `_Color`; wind (round N, vanilla values of `CarrotLeafMat`): `_RippleDistance` 0.3, `_RippleSpeed` 100, `_RippleDeadzoneMin/Max` 0.1/0.1, `_SwayDistance` 0.5, `_SwaySpeed` 20, `_PushDistance` 0.3, `_Height` 2 on every foliage material, fern fronds included |
| `bottle_mist_t1..3` | `JVLmock_Custom/Creature` | renderer disabled; C# reads the tier colour for the cork wisp |
| everything else (wood, metal, leather, cork, brew, stones, berries, glowing parts) | `JVLmock_Custom/Creature` (chosen over `/Piece` for one shader everywhere; the `/Piece` stump stays for a comparison) | `_Color` opaque; `_EmissionColor` = colour x Blender emission strength for the 12 emissive materials |

Where the values come from: `_Color` and alpha from the FBX import (Unity's Standard material import, read once before the remap); emission from the `MATS` tables in `tools/blender/make_*.py` (Blender's FBX exporter writes `EmissiveColor` 0,0,0).

C# side: `Items/AssetBundles.cs` (load, asset list, shader safety net), `Items/ModelPrefabs.cs` (item components, size factors, cork wisp, plants), `Items/ModelScale.Core.cs` (size factor per group), `Items/PotionItems.cs` (meads and bases), `Items/GoggleItems.cs`, `Items/IngredientItems.cs`, `Items/ModelItems.cs` (goggles, ingredients, plants, unload), dev commands in `Dev/AssetCommands.cs`, `Dev/MeshExport.cs` (`ip_exportmesh`) and `Dev/MaterialDump.cs` (`ip_matdump`).

Size factors (`Items/ModelScale.Core.cs`; fix round 2, round K, items round L): two per group, the **world** size (dropped item on the ground, plant prop) and the **attach** size (worn, held, item stand, armour stand).

| Group | World | Attach |
|---|---|---|
| bottles (`MeadBottle_*`) | x2.4 | x2.0 |
| bowls (`MeadBowl_*`) | x3.0 | x3.0 |
| goggles (`Goggles_*`) | x2.2 | x1 |
| ingredients (`Ingredient_*`, never worn) | x8 | x8 |
| plants `Plant_T2*`, `Plant_T3*` (props) | x2.2 | – |
| lichen `Plant_T1*` (props) | x4.5 | – |

Mechanism (decompile, `docs/decompile-notes.md` "Items round L"): the worn and stand views clone only the `attach` child and keep its scale; the dropped item is the whole prefab, but `ItemDrop.SetQuality` resets the root's `localScale` to `ItemData.GetScale()` (1 at quality 1) in `Awake`, `Load` and `DropItem`, so a root factor is lost. So the item prefab's `attach` carries the attach factor, and items whose world size differs (bottles, goggles) get a `DroppedItemScale` component on the root that multiplies its own instance's `attach` by world / attach in `Awake` (world instances only; the attach clones do not carry the root component). The box collider is sized for the dropped view. Plants scale their `model` child by the world factor. The bundle itself stays at Blender scale.

Helmet items (goggles) have no durability (items round L): `ModelPrefabs.MakeItem` sets `m_useDurability = false`, `m_maxDurability = 100` and the item's `m_durability = 100` for every `ItemType.Helmet` (the `HelmetLeather` template wears down and shows a bar).

Wind (plan 5 round N, verified from the game data): the property names and values were read from the installed game (Valheim 1.0.16) by decompressing the SoftRef bundle `StreamingAssets/SoftRef/Bundles/c4210710` (UnityFS, LZ4 blocks) and parsing the serialized `Custom/Vegetation` shader and its materials. The shader declares `_SwaySpeed` (default 15), `_SwayDistance` (0.5), `_RippleSpeed` (100), `_RippleDistance` (0.5), `_RippleDeadzoneMin` (0.3), `_RippleDeadzoneMax` (2), `_PushDistance` (0), `_Height` (15), `_PushClothMode`, `_AddSnow` (1), `_AddRain` (1), `_CamCull` (1), `_TwoSidedNormals`, `_SphereNormals`, `_Cull` and the moss/metal set; the sway is driven by the global `_GlobalWind*` vectors that `EnvMan` sets. Vanilla materials: `Bush01` ripple 0.5 / speed 150, sway 3 / 30, height 3, push 2; `shrub` ripple 3 / 200, sway 2 / 30, height 1, push 3; `CarrotLeafMat` ripple 0.3 / 100, deadzone 0.1 / 0.1, sway 0.5 / 20, height 2, push 0.3; `FernAshlands_mat` ripple 1 / 15, sway 220.8 / 29.8, height 15. Our foliage materials (`huldra_*`, `baldr_*`, `helfern_frond`) take the `CarrotLeafMat` values (crop leaves of about our plants' size), all set explicitly and declared in the stub so they serialize; a property missing on the material would take the shader default after Jötunn's swap (height 15). The fern sways too since round N (ruling 5: T2 and T3). Stems and other parts stay on `Custom/Creature` and do not move. The Debug build logs the vanilla references once per world load (`wind:` lines, same as `ip_matdump Bush01` / `shrub_2` / `Pickable_Thistle` plus our two plants) for an in-game cross-check.

Cork wisp (fix round 2): the opaque bottle hides interior mist, so `ModelPrefabs.AddMist` moves `MistAnchor` to the top centre of the renderer bounds (the cork) and adds a thin wisp: 2 particles/s, size 0.03..0.06 m, lifetime 1.5 s, rising 0.04..0.06 m/s in world space, tier colour at alpha 0.5.

Fitting references: `ip_exportmesh <prefab>` / `ip_exportmesh head` write OBJ files to `<game>/BepInEx/export/` (x negated, winding reversed: import in Blender with the OBJ defaults, forward -Z, up Y). `head_body.obj` is the player's body baked in the helmet joint's space, i.e. the space of the goggles' `attach` origin; `head_HelmetLeather.obj` is the vanilla leather helmet in the same space.

## Spike results

### S3: Personal licence in batch mode (2026-10-02) — works

The headless editor connects to the Hub's running licensing client (`Unity.Licensing.Client`, channel `LicenseClient-<user>`) and resolves the Personal entitlement: `Product: Unity Personal, Type: Assigned, Expiration: Unlimited`. No username/password or `.ulf` flow needed. Every log has one harmless line `[Licensing::Module] Error: Access token is unavailable; failed to update` before the entitlement resolves. Requirement: Unity Hub has been signed in once (the licensing client runs in the background).

### S7: editor version and warnings (2026-10-02) — clean

Logs scanned for warn/deprecat/obsolete/upgrade/mismatch/not supported. Nothing about the 6000.0.75f1 vs Jötunn's documented 6000.0.61. Harmless noise in every run: `libxml2.so.2: no version information available (required by libfbxsdk.so)` on stdout, two `Gtk-CRITICAL ... gtk_label_set_text` lines, the licensing access-token line above.

Pitfalls found:
- Opening a folder without `ProjectSettings/` makes Unity treat it as a new project and **overwrite** `Packages/manifest.json` with the default template (ads, purchasing, timeline, ugui, ...; purchasing logs a deprecation warning). The manifest was trimmed back after the first run.
- Without `com.unity.modules.assetbundle` the build logs `'AssetBundle' is not supported because the module AssetBundle is disabled in the build.` (also for `AssetBundleManifest`). Keep that module.

### Timings (this machine)

| Step | Duration |
|---|---|
| `make unity-setup`, first ever run (project creation, default package import) | 73 s |
| `make bundle` (linux), first run after trimming the manifest | 12 s |
| `make bundle` (linux), warm | 7 s |
| `make bundle TARGET=windows` | 6 s |

### First test bundle (input for S1/S2/S5)

Prefabs `SpikeCube_Creature`, `SpikeCube_Distortion`, `SpikeCube_Piece`, `SpikeCube_Standard` (0.5 m cube, pivot at the bottom centre) with materials `Spike_Creature` (green), `Spike_Distortion` (light blue, `_Color.a` 0.35), `Spike_Piece` (brown), `Spike_Standard` (Unity `Standard`, red), plus `Spike_LitParticles` (material only).

| Target | File | Size | Shader code inside |
|---|---|---|---|
| linux | `InvisibilityPotion/Assets/ip_assets` | 168449 bytes (164.5 KB) | `glcore` + `vulkan` only |
| windows | `InvisibilityPotion/Assets/ip_assets.windows` | 63452 bytes (62.0 KB) | `d3d11` only |

Findings that feed S1/S2:
- The stump shaders are **compiled into the bundle** (about 31 KB uncompressed each on Linux) for the target's graphics APIs only. They render nothing useful anyway; what matters is that Jötunn replaces them. If the replacement fails, a Linux bundle shows pink on Windows and vice versa.
- A material with Unity `Standard` pulls `Standard` from `unity_builtin_extra` into the bundle (112.7 KB of 333 KB uncompressed on Linux), again per graphics API. So `Standard` would work only on the platform the bundle was built for (S2 in-game check still open).
- Possible follow-up: shrink the stumps to a one-line unlit shader to cut bundle size.
- Plan 5 round N (wind values): Linux `ip_assets` 902544 bytes, Windows `ip_assets.windows` 858519 bytes. `make unity-setup` also rewrites the fileIDs of generated child objects in unchanged prefabs (content-equivalent churn).

### First full integration (2026-10-02, build time)

Observed while building (in-game results still open, see `docs/testing.md`, "Plan 4 – first integration"):
- 30 FBX files, 73 materials, 30 prefabs. Bundle sizes: Linux `ip_assets` 857504 bytes (837 KB), Windows `ip_assets.windows` 811759 bytes (793 KB). `make unity-setup` about 7 s, `make bundle` about 7 s per target (warm).
- Import: every FBX root comes in with identity rotation and scale 1 (Blender's `bake_space_transform` works); heights match the Blender scripts (bottles 0.162 / 0.231 / 0.26 m, bowls 0.062 m, goggles 0.237 m wide). Goggles: Y is up, the strap ring spans Z.
- Alpha survives the FBX import (`_Color.a` 0.45 on the glass, the lens alphas as in `make_goggles.py`); emission does not (see above).
- Batch-mode material remapping works: `ModelImporter.AddRemap(SourceAssetIdentifier(typeof(Material), name), mat)` + `SaveAndReimport`. Reading the embedded materials needs the remaps removed first, and the embedded `Material` objects are destroyed by the reimport.
- `PrefabUtility.SaveAsPrefabAsset` gives newly created GameObjects new local file IDs on every run, so rerunning `make unity-setup` rewrites the item prefabs (`attach` wrapper) with only file-ID changes; plant prefabs stay byte-identical. Revert those diffs if no model changed.
- Jötunn `PrefabManager.CreateClonedPrefab(name, prefab)` refuses a name that `GetPrefab` already resolves, and its cache can see the loaded bundle asset of the same name: items are therefore cloned under their item names, plants are registered as the bundle prefabs themselves.
- S5 is expected to work: `AssetUtils.LoadAssetBundleFromResources` matches the manifest name by `EndsWith` (`InvisibilityPotion.Assets.ip_assets`; the Windows bundle is `....ip_assets.windows`, which does not end with `ip_assets`). The Debug build logs the manifest names to confirm.

Open, settled in-game by the plan-4 checklist: S1 (Windows load), S2 (do the mock shaders resolve, does `Custom/Vegetation` exist, does the glass read as glass), S4 (`ip_components MeadHealthMinor`), S6 (held/stand visuals through `attach`).

### Loader pitfall (2026-10-02, round I crash)

`AssetUtils.LoadAssetBundleFromResources` (Jötunn 2.30.0 and 2.30.2) calls `AssetBundle.LoadFromStream` inside a `using` block, so the stream is disposed right after the header is read; the first `LoadAllAssets` then fails with `ManagedStream object must be readable` and "Mismatched serialization in the builtin class 'Mesh'" errors, followed by a native crash (signal 5). `Items/AssetBundles.cs` therefore copies the resource into a byte array and uses `AssetBundle.LoadFromMemory`.
