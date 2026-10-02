# Plan 4 asset pipeline: research (2026-10-02)

Fact base for the Unity asset bundle plan. "(?)" = unverified, needs the spike named next to it. Sources: J = Jötunn docs `https://valheim-modding.github.io/Jotunn/tutorials/<page>.html`; JS = Jötunn source `https://github.com/Valheim-Modding/Jotunn/blob/dev/JotunnLib/...` (read via `gh api`, tag v2.30.2 is latest); D = `tools/decompiled/assembly_valheim/` (main checkout; the worktree has no copy, `make decompile` regenerates it).

## 1. Jötunn and asset bundles

**Loading.** `AssetUtils.LoadAssetBundleFromResources("name")` loads a bundle compiled into the DLL; mark the file as embedded resource, then `bundle.LoadAsset<GameObject>("PrefabName")` (J asset-loading). Side-loading is easier to iterate on but must ship next to the DLL (J asset-loading). Our csproj already embeds `Items\English.json` (`InvisibilityPotion/InvisibilityPotion.csproj`), so the same pattern applies.

**Registering.** `ItemManager.Instance.AddItem(new CustomItem(prefab, fixReference: true, itemConfig))`; `ItemConfig` exposes crafting station, requirements, name and description (J items). `CustomItem(bundle, assetName, fixReference, config)` and `CustomPrefab(AssetBundle, string, bool fixReference)` also exist (Jötunn API docs). `RegisterCustomItems` calls `ItemPrefab.FixReferences(...)` and `m_shared.FixReferences()` when `FixReference || FixConfig` is set (JS Managers/ItemManager.cs:392-398). The prefab must carry an `ItemDrop`, otherwise `AddItem` throws "has no ItemDrop component attached" (ItemManager.cs:443-446). `PrefabManager.CreateEmptyPrefab` adds a `ZNetView` by default, but bundle prefabs get none, so a bundle prefab needs its `ZNetView` either in the editor or added from C# before registration (JS PrefabManager.cs:164-188).

**Mock system.** Prefix `JVLmock_` (JS MockSystem/MockManager.cs:44); separator `__` for children (:49). In Unity you create empty placeholder assets (icon, material, mesh, prefab, effect) named exactly like the vanilla asset with the prefix and assign them in your components; with `fixReference: true` Jötunn swaps in the vanilla object at runtime (J asset-mocking). Caveat: referencing the real asset directly in Unity yields the original, not the mock (J asset-mocking).

**Shaders: the answer.**
- `fixReference` does **not** replace a shader by plain name. `FixShader` only acts when the material's shader name is a mock name (`IsMockName`), looks up `PrefabManager.Cache.GetPrefab<Shader>(cleanedName)` and assigns it; otherwise it returns without change (JS MockManager.cs:539-562). The cache is filled from `Resources.FindObjectsOfTypeAll` (JS PrefabManager.cs:605), so any shader loaded in the game can be addressed by its `Shader.name`.
- So: a material whose shader is a dummy shader named `JVLmock_Custom/Creature` becomes `Custom/Creature` at runtime, and the properties set on the material are kept (J asset-mocking "Shader Mocking": "create a new stump shader ... change the first line to contain the `JVLmock_` prefix ... Jötunn will resolve the right Shader for you. Properties that are set on the material will be used by the resolved shader"). The docs example is `Shader "JVLmock_Custom/Piece"` with a dummy surface shader body.
- Materials are fixed lazily: if the vanilla shader is not found yet the material is queued and retried at `ZoneSystem.Start` (MockManager.cs:77-79, 456-466). Shader fixing is skipped on a headless server (`GUIUtils.IsHeadless`, :480-484).
- `ShaderHelper` (JS Utils/ShaderHelper.cs) does not replace shaders. It lists renderers and materials and has `ShaderDump(GameObject)`, which logs shader names, keywords and texture properties of a prefab: use it as the in-game check below.
- Fallback with no Unity stub shader (?): after loading, set `material.shader = PrefabManager.Cache.GetPrefab<Shader>("Custom/Creature")` in C#. Not in the docs; spike S2.
- Shaders that are **not** mocked and are not built-ins compile into the bundle. Built-in (`Standard`, `Legacy Shaders/...`) may be stripped or missing in Valheim's player (?); spike S2.

**Unity-side stubs: two options.**

| | A. Pure assets, C# adds components | B. Valheim stub project (game DLLs in Unity) |
|---|---|---|
| Editor content | Mesh, materials, textures, empty children (`attach`, `MistAnchor`) | Full prefab with `ItemDrop`, `ZNetView`, `ZSyncTransform`, ... |
| Setup | Fresh Unity 6 project, no game DLLs | Copy `assembly_*.dll` and the other DLLs listed in J asset-creation into `Assets/Assemblies` by file system (never import via Unity, or the script GUIDs change); the `.meta` files come from a ripped project or the JotunnModStub (`https://github.com/Valheim-Modding/JotunnModStub`) |
| Pros | No game binaries in the repo (the licence stays clean), no dependency on a Valheim rip, survives game updates, easy to diff | What Jötunn's own tutorial does; tune `m_shared` in the inspector; prefab is complete at load |
| Cons | All `ItemDrop` data in C# (we already do this in `PotionItems.cs`), colliders/Rigidbody added in code | Copyrighted DLLs must stay out of git; `.meta` GUID gymnastics; Unity 6 stub is not documented for 6000.0.75f1 (?) |

Jötunn docs name **Unity 6000.0.61** (J asset-creation); the game is 6000.0.75f1. Both are Unity 6.0 LTS patch versions; the minor mismatch is not mentioned as a problem (?). There is no separate "JotunnUnityPackage" for Unity 6; the stub is the JotunnModStub repo's `JotunnModUnity` folder (J asset-creation). Recommendation: **Option A.** Our meads are clones configured in C# today, so the bundle only has to supply meshes, materials and textures; this also keeps the Unity project free of game code.

## 2. Unity 6000.0.75f1 batch build on Linux

**Command line** (flags from `https://docs.unity3d.com/6000.0/Documentation/Manual/EditorCommandLineArguments.html`):

```
~/Unity/Hub/Editor/6000.0.75f1/Editor/Unity -batchmode -nographics -quit \
  -projectPath unity/InvisibilityPotionAssets \
  -executeMethod BundleBuilder.Build -logFile build/unity-bundle.log
```

`-executeMethod` needs a static method in an `Editor/` folder. `-quit` is documented as quitting after the commands finish. Check the exit code and grep the log for `error`. `-nographics` skips the GPU, but texture import and shader compilation still run (?).

```csharp
// unity/InvisibilityPotionAssets/Assets/Editor/BundleBuilder.cs
using UnityEditor; using System.IO;
public static class BundleBuilder {
    public static void Build() {
        var dir = "Build/Bundles"; Directory.CreateDirectory(dir);
        BuildPipeline.BuildAssetBundles(dir,
            BuildAssetBundleOptions.ChunkBasedCompression | BuildAssetBundleOptions.StrictMode,
            BuildTarget.StandaloneLinux64);   // see platform note
    }
}
```

Signature (output dir, options, target) confirmed at `https://docs.unity3d.com/6000.0/Documentation/Manual/AssetBundles-Building.html`. Assets get their bundle name from the importer (`AssetImporter.GetAtPath(p).assetBundleName = "ip_assets"`, set by an editor script or in the inspector) (?).

**Which BuildTarget.**
- The user's game is the native Linux build: `valheim.x86_64` plus `UnityPlayer.so` in the install dir, Vulkan graphics (`~/.config/unity3d/IronGate/Valheim/Player.log`: `[Vulkan init]`).
- Platform bundles differ only in their shaders: a Windows bundle loaded on another platform shows pink shaders, while shader-free content loads (`https://discussions.unity.com/t/cross-platform-asset-bundles/680590`, `https://discussions.unity.com/t/assetbundles-with-shaders-for-all-standalone-platforms/747496`; forum posts, not official docs; Unity manual says nothing on this, WebFetch of AssetBundles-Building). The same threads say a Windows bundle can carry Vulkan/GLCore variants usable on Linux; no official support.
- Consequence: with **JVLmock_ shaders** (vanilla shaders replaced at runtime) the bundle contains no real shader code, so **one bundle** should work on Linux and Windows (?). Spike S1 settles it. If we ship our own shaders, we need one bundle per platform.
- **The installed editor can only build Linux.** `~/Unity/Hub/Editor/6000.0.75f1/modules.json` has `linux-il2cpp` selected, `windows-mono` not; `Editor/Data/PlaybackEngines/` contains only `LinuxStandaloneSupport`. `BuildTarget.StandaloneWindows64` therefore needs the `windows-mono` module added via Hub (a user-local download, no sudo; Hub ID `windows-mono`). Recommendation: first build `StandaloneLinux64` (matches the dev machine); add the Windows module only for the Thunderstore release, and only if S1 shows a Linux bundle breaks on Windows.

**Licence in batch mode (?).** Docs mention `-username/-password/-serial` (serial needs `-batchmode`) and `-manualLicenseFile` for activation (EditorCommandLineArguments). With a Personal licence acquired through Hub, a headless run normally finds the Hub-managed licence (Unity Licensing Client); not confirmed for Linux 6000.0 batch mode. Spike S3: run the build once; if the log says "No valid Unity Editor license", open the project once through Hub, or use the Personal activation flow (`-username/-password` or manual `.alf`/`.ulf` file).

**FBX import settings that matter** (Unity manual pages not fetched here, general knowledge (?)): Scale Factor 1 and `Convert Units` consistent with Blender's metre scale (Blender FBX export: Apply Scalings `FBX Units Scale`, Forward `-Z`, Up `Y`, `Apply Transform` ticked, which is `Bake Axis Conversion` on the Unity side); disable "Import Cameras/Lights"; Generate Colliders off (add a simple collider via C#); Read/Write off; keep normals imported. Verify with a one-bottle test that 0.16 m in Blender is 0.16 m in game (prior notes: tier 1/2/3 are 0.16 / 0.22 / 0.26 m in `docs/ideas/2026-10-01-veil-goggles-and-plants.md`).

## 3. Item prefab anatomy for a consumable mead

**ItemDrop fields** (D ItemDrop.cs): `m_itemType` (:101, `Consumable`), `m_attachOverride` (:105), `m_isDrink` (:192; only read by `GetHoverText` for placed consumables, :1496, see `docs/decompile-notes.md:240`), `m_consumeStatusEffect` (:405; added on use, :1667-1669), `m_equipStatusEffect` (:148), `m_armor` (:205). `ItemDrop.Awake` uses `GetComponent<ZNetView>()` (:1285), so a dropped item needs a `ZNetView`. `m_food*` fields stay 0 for non-food (`PotionItems.cs`).

**Current clone path** (`InvisibilityPotion/Items/PotionItems.cs`): `new CustomItem(MeadName(t), "MeadHealthMinor", new ItemConfig{ Enabled=false ... })`, i.e. Jötunn clones the vanilla prefab under a new name, then C# sets `m_itemType`, `m_isDrink`, `m_consumeStatusEffect` and zeroes `m_food*`; `Tint()` copies the materials. The base (ingredient) item clones `MeadBaseHealthMinor` and is crafted at `CraftingStations.MeadKetill` (`piece_MeadCauldron`); a `CustomItemConversion` turns base into mead in the fermenter. Icons come from `RenderManager.Instance.Render(prefab, IsometricRotation)` after registration.

**Components of vanilla `MeadHealthMinor` (?).** The prefab is a bundle asset, not in the decompile. Expected (not verified): `ItemDrop`, `ZNetView`, `ZSyncTransform`, `Rigidbody`, `Collider`, `Hoverable` via `ItemDrop`, a visible mesh child, an `attach` child. Verified in code: held/stand/ground display uses a child named `attach` (or `attach_skin`; `attach_back` for back slots) found by name (D VisEquipment.cs:1347-1372; also ItemStand.cs:343, PickableItem.cs:144, CookingStation.cs:424); `equipoffset` is looked up too (VisEquipment.cs:1355). Spike S4 lists the real components.

**Dump of the vanilla mead for the spike.** `ShaderHelper.ShaderDump(prefab)` plus a component dump, run from a Debug console command (extend `Dev/DevCommands.cs`; output must go to `Plugin.Log` per CLAUDE.md).

**Shaders of glass/mead (?).** The decompile has no shader names (`grep` finds only `Hidden/CameraHeatDistort`, `HeatDistortImageEffect.cs:67`); shaders live in the game bundle. Known from the existing notes (`docs/decompile-notes.md:351-357`, read with UnityPy): `Custom/Distortion` (`_RefractionIntensity`, `_Color`, glossiness, vanilla `Aspect_mat`, `ForceField`), `Custom/Creature` (alpha-tested, no blend), `Custom/LitParticles`. `Custom/Distortion` is the best candidate for semi-transparent glass; the vanilla bottle may use `Custom/Piece` or `Standard`.

**In-game checks for the glass shader** (Debug build, F5 console):
1. `ip_prefabs Mead` (existing, `Dev/DevCommands.cs:321`) to list names; also `ip_prefabs glass`, `ip_prefabs bottle`, `ip_prefabs Aspect`, `ip_prefabs ForceField`.
2. New command `ip_shaderdump <prefab>`: `ShaderHelper.ShaderDump(prefab)` and log render queue, `_Mode`/blend keywords for `MeadHealthMinor`, `Aspect`, and the Frost orb shard.
3. Spawn the mead and a placed Jötunn test cube with a `JVLmock_Custom/Distortion` material (spike S2) and look at it against the sky.

## 4. Plants and goggles (short, for a later plan)

**Pickable plant** (D Pickable.cs): `Pickable` has `m_hideWhenPicked`, `m_itemPrefab`, `m_amount`, `m_respawnTimeMinutes`, `m_pickEffector`, `m_overrideName`, `m_hoverOffset` (:8-36); it implements `Hoverable, Interactable` (:4), so it needs a `Collider` and a `ZNetView` (persistent) in the prefab. Vanilla prefab expected layout (?): root with `ZNetView`, `Pickable`, `Collider`, mesh child, a "picked" state child hidden via `m_hideWhenPicked`.

**Spawning.** `CustomVegetation(prefab, fixReference, VegetationConfig)` (JS Entities/CustomVegetation.cs:57). `VegetationConfig` fields (JS Configs/VegetationConfig.cs): `Biome`, `BiomeArea`, `BlockCheck`, `ForcePlacement`, `Min`, `Max` (count per zone), `MinAltitude`, `MaxAltitude`, `MinOceanDepth`, `MaxOceanDepth`, `MinTerrainDelta`, `MaxTerrainDelta`, `MinTilt`, `MaxTilt`, `InForest`, `ForestThresholdMin/Max`, `TerrainDeltaRadius`, `ScaleMin`, `ScaleMax`, `GroupSizeMin`, `GroupSizeMax`, `GroupRadius`, `GroundOffset`. Docs: J zones.

**Goggles.** `ItemDrop` with `m_itemType = Helmet`, `m_armor` (D :205), an `attach_skin` or `attach` child; `VisEquipment.SetHelmetEquipped` uses `AttachItem(hash, 0, m_helmet)` (D VisEquipment.cs:1184), and the same `attach` rule from section 3 applies. `m_equipStatusEffect` (D ItemDrop.cs:148) gives the reveal effect. Three levels: three separate items or `m_maxQuality` with upgrades (?); see J items.

## 5. Recommended layout and pipeline

```
unity/InvisibilityPotionAssets/
  Assets/Models/*.fbx            # Blender output (committed, or built by make models)
  Assets/Materials/  Textures/   # materials use dummy JVLmock_ shaders
  Assets/Shaders/JVLmock_*.shader # dummy stumps copied from J asset-mocking
  Assets/Editor/BundleBuilder.cs
  Packages/manifest.json  ProjectSettings/   # committed
  .gitignore                     # Library/ Temp/ Logs/ obj/ UserSettings/ Build/ *.csproj *.sln
```

Note: the root `.gitignore` already ignores `InvisibilityPotionUnity/Library/` etc.; either use that directory name or update those lines to `unity/InvisibilityPotionAssets/`.

**Makefile.**
- `make models`: `blender -b -P tools/blender/make_bottle.py -- --out unity/InvisibilityPotionAssets/Assets/Models` (script lives in `tools/blender/`).
- `make bundle`: refuse when a Unity instance has the project open (the `Temp/UnityLockfile` exists), run the batch command above, then `cp unity/InvisibilityPotionAssets/Build/Bundles/ip_assets InvisibilityPotion/Assets/ip_assets` (the plain filename without extension is what Unity outputs; rename if wanted).
- `build`/`package` depend on the copied file existing and fail with a clear message otherwise (the bundle is gitignored build output, or committed in `InvisibilityPotion/Assets/`; decide in the plan, a committed binary keeps `make build` working without Unity).

**csproj.** `<EmbeddedResource Include="Assets\ip_assets" />`; Jötunn finds the resource by the name suffix, so `AssetUtils.LoadAssetBundleFromResources("ip_assets")` works if the manifest resource name ends with it (?; the docs only show the call, spike S5).

**C# load** (new `Items/AssetBundles.cs`): load once in `Plugin.Awake`, keep the bundle, `Unload(false)` after all prefabs are registered; `LoadAsset<GameObject>("MeadBottle_T1")`, add components (`ItemDrop`, `ZNetView`, collider) if option A, set `m_shared`, then `new CustomItem(prefab, fixReference: true, cfg)`. Replace the `CustomItem(name, "MeadHealthMinor", ...)` clone path and the `Tint` step; icon rendering keeps working through `RenderManager`. Patch health stays unaffected (no new Harmony targets).

**Open questions and the spike that settles each.**

| # | Question | Spike |
|---|---|---|
| S1 | Does a `StandaloneLinux64` (and a Windows) bundle with only mock shaders load on both platforms with no pink? | Build a bundle with one cube and a `JVLmock_Custom/Creature` material, load it via `make run`, log `ShaderHelper.ShaderDump` and take a screenshot; repeat with the Windows module later. |
| S2 | Does `JVLmock_Custom/Distortion` (or `Creature`/`Piece`) resolve, and does the glass look right (transparency, refraction)? Is plain `Standard` usable in a bundle? | Same bundle with three cubes: `JVLmock_Custom/Distortion`, `JVLmock_Custom/Piece`, `Standard`; log `material.shader.name` after `ZoneSystem.Start`. |
| S3 | Does the Personal licence work in batch mode on Linux? | `make bundle` once with an empty project; read the log. |
| S4 | Real component list and child names of `MeadHealthMinor` and `MeadBaseHealthMinor` | `ip_components <prefab>` Debug command logging all components and children. |
| S5 | Does `LoadAssetBundleFromResources("ip_assets")` find the embedded name? | Log `Assembly.GetManifestResourceNames()` once. |
| S6 | Does the held/inventory mead show our mesh with no `attach` child, and is `attach` needed for drinking animation? | Register one custom mead without `attach`, drink it. |
| S7 | Unity 6000.0.75f1 editor vs Jötunn doc version 6000.0.61: any import warning? | Read the first batch log. |

Shaders are the highest risk; run S1 and S2 before modelling anything else.
