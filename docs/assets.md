# Asset pipeline

Custom models reach the game through one Unity asset bundle, `ip_assets`, embedded in the plugin DLL. Design: `docs/superpowers/specs/2026-10-02-plan4-assets-design.md`; background: `docs/ideas/2026-10-02-asset-pipeline-research.md`.

## Unity project

`unity/InvisibilityPotionAssets/` (Unity 6000.0.75f1, the game's own version, built-in render pipeline, no game DLLs).

| Path | Content |
|---|---|
| `Assets/Shaders/JVLmock_Custom_*.shader` | Dummy stumps named `JVLmock_Custom/Creature`, `/Distortion`, `/LitParticles`, `/Piece`. Jötunn swaps in the vanilla shader at runtime (`fixReference: true`); only the `Shader "..."` line matters. They declare `_Color` and `_MainTex` so the material keeps those values. |
| `Assets/Materials/*.mat` | In the bundle. |
| `Assets/Prefabs/*.prefab` | In the bundle. Layout: root (empty) -> `model` (MeshFilter + MeshRenderer) -> anchors. Components come from C#. |
| `Assets/Meshes/` | Meshes referenced by prefabs; pulled into the bundle as dependencies. |
| `Assets/Editor/BundleBuilder.cs` | `BundleBuilder.Build`: reads `-target linux\|windows` (default linux), assigns bundle name `ip_assets` to everything under `Assets/Prefabs` and `Assets/Materials`, builds `Build/Bundles/<target>/ip_assets` with `ChunkBasedCompression \| StrictMode`, exits 1 on failure. |
| `Assets/Editor/SpikeSetup.cs` | `SpikeSetup.Create`: generates the spike cube mesh, materials and prefabs (no GUI needed). |
| `Packages/manifest.json` | Minimal: `com.unity.modules.assetbundle` (required, see below) and `com.unity.toolchain.linux-x86_64` (Unity adds it on its own on Linux). |

Never open the project in the editor while `make bundle` runs; the Makefile refuses while `Temp/UnityLockfile` exists.

## Commands

```
make unity-setup             # SpikeSetup.Create, log: build/unity-setup.log
make bundle                  # Linux bundle -> InvisibilityPotion/Assets/ip_assets, log: build/unity-bundle-linux.log
make bundle TARGET=windows   # Windows bundle -> InvisibilityPotion/Assets/ip_assets.windows, log: build/unity-bundle-windows.log
```

Underlying call (`UNITY` defaults to `~/Unity/Hub/Editor/6000.0.75f1/Editor/Unity`):

```
$UNITY -batchmode -nographics -quit -projectPath unity/InvisibilityPotionAssets \
  -executeMethod BundleBuilder.Build -target linux -logFile build/unity-bundle-linux.log
```

`InvisibilityPotion/Assets/ip_assets` is committed and embedded through `<EmbeddedResource Include="Assets\ip_assets" />`, so `make build` works without Unity.

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

Open: S1 (cross-platform load), S2 (glass shader, `Standard` viability), S4, S5 (embedded resource name), S6 need the in-game spike with a C# loader.
