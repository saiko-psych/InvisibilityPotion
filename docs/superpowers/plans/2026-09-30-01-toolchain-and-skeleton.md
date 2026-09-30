# Plan 1: Toolchain, Plugin Skeleton and Fast Test Loop

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A buildable, loadable InvisibilityPotion plugin with a sub-60-second edit-to-in-game loop, a decompiled game assembly, and every game hook from the spec verified and documented.

**Architecture:** SDK-style `net48` C# project derived from JotunnModStub. Jötunn's NuGet package supplies the game/BepInEx references and a prebuild publicizer. A Makefile wraps `dotnet build`, game launch, log tailing, decompile and tests. Dev-only code (auto-join, console commands) is compiled only in Debug builds.

**Tech Stack:** .NET SDK 8.0 (`dotnet`), JotunnLib 2.30.2, BepInEx 5.4.23.5, HarmonyX, ilspycmd 11.x, xunit, GNU make, Unity Hub + Unity 6000.0.75f1 (installed now, used in a later plan).

**Spec:** `docs/superpowers/specs/2026-09-30-invisibility-potion-design.md` (sections 3, 4, 5.1, 5.10, 6, build steps 1 and 2)

## Global Constraints

- Plugin GUID `saikopsych.InvisibilityPotion`, name `InvisibilityPotion`, version `0.1.0`.
- Targets: Valheim 1.0.16 (network version 40), Unity 6000.0.75f1, BepInEx 5.4.23.5, Jötunn 2.30.2.
- Valheim install: `/home/kitschekko/.local/share/Steam/steamapps/common/Valheim` (also reachable as `~/.steam/steam/steamapps/common/Valheim`). BepInEx lives directly in that folder.
- Everything in English: code, comments, docs, commit messages.
- Licence MIT. Copying code is allowed only from MIT / MIT-0 / Unlicense sources.
- Commands needing `sudo` are run by the user, not by the agent. The agent prints them and waits.
- Existing local world for testing is named `testing`. Do not create or delete worlds.
- `#if DEBUG` guards everything under `InvisibilityPotion/Dev/`.
- Game-code member names must be verified in `tools/decompiled/` before they are used in code.

---

## File map

| Path | Responsibility |
|---|---|
| `Makefile` | `setup`, `build`, `run`, `log`, `test`, `decompile`, `package` targets |
| `Environment.props.example` | template for the gitignored `Environment.props` (VALHEIM_INSTALL) |
| `DoPrebuild.props` | `ExecutePrebuild=true` so Jötunn publicizes game DLLs |
| `InvisibilityPotion.sln` | solution so `$(SolutionDir)` resolves for Jötunn's props |
| `scripts/publish.sh` | copies DLL/pdb to BepInEx/plugins (Debug) or zips the package (Release); from JotunnModStub (MIT-0) |
| `InvisibilityPotion/InvisibilityPotion.csproj` | project: net48, JotunnLib reference, post-build publish |
| `InvisibilityPotion/Plugin.cs` | BepInEx entry point: logger, Harmony, patch health check, dev hooks |
| `InvisibilityPotion/PatchHealth.cs` | lists expected patch targets and reports which are missing |
| `InvisibilityPotion/Properties/IgnoreAccessModifiers.cs` | `SkipVerification` attribute so publicized members work at runtime |
| `InvisibilityPotion/Dev/AutoJoin.cs` | DEBUG: skip the menu and load the world/character named by env vars |
| `InvisibilityPotion/Dev/DevCommands.cs` | DEBUG: `ip_state` console command |
| `InvisibilityPotion/Package/manifest.json`, `README.md`, `icon.png` | Thunderstore package metadata |
| `InvisibilityPotion.Tests/InvisibilityPotion.Tests.csproj` | xunit, `net8.0`, no game DLLs |
| `InvisibilityPotion.Tests/PatchHealthTests.cs` | tests for the pure "which patches are missing" logic |
| `docs/decompile-notes.md` | verified signatures for every hook the spec marks *(verify)* |
| `docs/testing.md` | in-game checklist |
| `CLAUDE.md` | project conventions and commands for future sessions |

---

### Task 1: Toolchain installation

**Files:**
- Create: `Environment.props.example`
- Create: `Environment.props` (gitignored)

**Interfaces:**
- Produces: `dotnet` (SDK 8.x) and `ilspycmd` on PATH; `unityhub` installed; `Environment.props` with `VALHEIM_INSTALL`.

- [ ] **Step 1: Ask the user to run the sudo installs**

Print exactly this and wait for confirmation that both finished:

```
! sudo pacman -S --needed dotnet-sdk-8.0 make rsync zip
! yay -S --needed unityhub
```

- [ ] **Step 2: Verify dotnet and make**

Run: `dotnet --list-sdks && make --version | head -1`
Expected: one line starting with `8.0.` and `GNU Make 4.x`.

- [ ] **Step 3: Install ilspycmd as a dotnet global tool**

Run: `dotnet tool install --global ilspycmd --version 11.1.0.9782`
Then: `export PATH="$PATH:$HOME/.dotnet/tools" && ilspycmd --version`
Expected: `ilspycmd: 11.1.0...`. Tell the user to add `export PATH="$PATH:$HOME/.dotnet/tools"` to their shell rc if it is not there yet (check with `grep -n dotnet/tools ~/.bashrc ~/.zshrc`).

- [ ] **Step 4: Install Unity Editor 6000.0.75f1 through the Hub, headless**

Run: `unityhub --headless install --version 6000.0.75f1 --changeset 26349cd2a5c8 --module linux-il2cpp 2>&1 | tail -5`
Expected: ends with an "installed" confirmation. This downloads several GB; run it in the background with `run_in_background` and continue with the next steps. If the Hub needs a first-run licence login, tell the user to open `unityhub` once, sign in, and re-run the command. The Editor is not needed before plan 4.

- [ ] **Step 5: Write Environment.props.example and the real Environment.props**

`Environment.props.example`:

```xml
<?xml version="1.0" encoding="utf-8"?>
<!-- Copy to Environment.props (gitignored) and adjust paths. Consumed by JotunnLib's build/Paths.props. -->
<Project ToolsVersion="Current" xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
  <PropertyGroup>
    <!-- Valheim game folder that contains valheim.x86_64 and BepInEx/ -->
    <VALHEIM_INSTALL>/home/USER/.local/share/Steam/steamapps/common/Valheim</VALHEIM_INSTALL>
    <!-- Optional: where Debug builds are copied. Defaults to $(VALHEIM_INSTALL)/BepInEx/plugins -->
    <!-- <MOD_DEPLOYPATH>/path/to/BepInEx/plugins</MOD_DEPLOYPATH> -->
  </PropertyGroup>
</Project>
```

`Environment.props` is the same file with `/home/kitschekko/.local/share/Steam/steamapps/common/Valheim`.

- [ ] **Step 6: Commit**

```bash
git add Environment.props.example
git commit -m "chore: add Environment.props template for local game path"
```

---

### Task 2: Decompile the game assembly

**Files:**
- Create: `Makefile` (first targets: `decompile`, `help`)
- Create: `tools/.gitkeep`

**Interfaces:**
- Produces: `tools/decompiled/assembly_valheim/` with one `.cs` per game class; `make decompile`.

- [ ] **Step 1: Write the Makefile with the decompile target**

```makefile
# InvisibilityPotion – developer commands. Run `make help`.
SHELL := /bin/bash
VALHEIM_INSTALL ?= $(HOME)/.local/share/Steam/steamapps/common/Valheim
MANAGED := $(VALHEIM_INSTALL)/valheim_Data/Managed
DECOMPILE_DIR := tools/decompiled
ILSPY := $(HOME)/.dotnet/tools/ilspycmd

.PHONY: help decompile

help: ## Show this help
	@grep -E '^[a-zA-Z_-]+:.*?## ' $(MAKEFILE_LIST) | awk 'BEGIN {FS = ":.*?## "}; {printf "  %-14s %s\n", $$1, $$2}'

decompile: ## Decompile assembly_valheim.dll into tools/decompiled (rerun after game updates)
	@rm -rf $(DECOMPILE_DIR)/assembly_valheim
	@mkdir -p $(DECOMPILE_DIR)
	$(ILSPY) -p -o $(DECOMPILE_DIR)/assembly_valheim --nested-directories $(MANAGED)/assembly_valheim.dll
	@echo "Game version: $$(grep -oE 'l-[0-9]+\.[0-9]+\.[0-9]+' $(HOME)/.config/unity3d/IronGate/Valheim/Player.log | tail -1)" > $(DECOMPILE_DIR)/VERSION
	@ls $(DECOMPILE_DIR)/assembly_valheim | wc -l | xargs echo "Decompiled files:"
```

Note: Makefile recipes must be indented with a real TAB character.

- [ ] **Step 2: Run the decompile**

Run: `make decompile`
Expected: "Decompiled files:" followed by a number above 500. If `--nested-directories` is rejected by this ilspycmd version, drop the flag and rerun.

- [ ] **Step 3: Verify the classes the spec relies on exist**

Run: `cd tools/decompiled/assembly_valheim && ls BaseAI.cs MonsterAI.cs Humanoid.cs Character.cs SE_Stats.cs StatusEffect.cs FejdStartup.cs ZDOMan.cs ZNet.cs VisEquipment.cs EnemyHud.cs Minimap.cs Player.cs Attack.cs`
Expected: all fourteen files listed, no "No such file". If files are in nested folders, use `find . -name BaseAI.cs` and note the layout in the next step.

- [ ] **Step 4: Commit**

```bash
git add Makefile tools/.gitkeep
git commit -m "build: add Makefile with decompile target"
```

(`tools/decompiled/` is gitignored already.)

---

### Task 3: Plugin skeleton that builds and deploys

**Files:**
- Create: `InvisibilityPotion.sln`
- Create: `DoPrebuild.props`
- Create: `scripts/publish.sh`
- Create: `InvisibilityPotion/InvisibilityPotion.csproj`
- Create: `InvisibilityPotion/Plugin.cs`
- Create: `InvisibilityPotion/Properties/IgnoreAccessModifiers.cs`
- Create: `InvisibilityPotion/Package/manifest.json`, `InvisibilityPotion/Package/README.md`, `InvisibilityPotion/Package/icon.png`
- Modify: `Makefile` (add `build`, `package`)

**Interfaces:**
- Produces: `InvisibilityPotion.Plugin` with `public const string PluginGuid = "saikopsych.InvisibilityPotion"`, `PluginName`, `PluginVersion`, `public static ManualLogSource Log`, `public static Harmony HarmonyInstance`.

- [ ] **Step 1: Write the solution file**

`InvisibilityPotion.sln`:

```
Microsoft Visual Studio Solution File, Format Version 12.00
# Visual Studio Version 17
Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "InvisibilityPotion", "InvisibilityPotion\InvisibilityPotion.csproj", "{7A1C4F2E-3B6D-4E8A-9C0F-1D2E3F4A5B6C}"
EndProject
Global
	GlobalSection(SolutionConfigurationPlatforms) = preSolution
		Debug|Any CPU = Debug|Any CPU
		Release|Any CPU = Release|Any CPU
	EndGlobalSection
	GlobalSection(ProjectConfigurationPlatforms) = postSolution
		{7A1C4F2E-3B6D-4E8A-9C0F-1D2E3F4A5B6C}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
		{7A1C4F2E-3B6D-4E8A-9C0F-1D2E3F4A5B6C}.Debug|Any CPU.Build.0 = Debug|Any CPU
		{7A1C4F2E-3B6D-4E8A-9C0F-1D2E3F4A5B6C}.Release|Any CPU.ActiveCfg = Release|Any CPU
		{7A1C4F2E-3B6D-4E8A-9C0F-1D2E3F4A5B6C}.Release|Any CPU.Build.0 = Release|Any CPU
	EndGlobalSection
EndGlobal
```

- [ ] **Step 2: Write DoPrebuild.props**

```xml
<?xml version="1.0" encoding="utf-8"?>
<Project ToolsVersion="Current" xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
  <PropertyGroup>
    <!-- true: JotunnLib's build task writes publicized game DLLs into valheim_Data/Managed/publicized_assemblies before every build -->
    <ExecutePrebuild>true</ExecutePrebuild>
  </PropertyGroup>
</Project>
```

- [ ] **Step 3: Write scripts/publish.sh (from JotunnModStub, MIT-0)**

```sh
#!/bin/sh
# Post-build publish step. Debug: copy dll+pdb into BepInEx/plugins/<name>/. Release: build the Thunderstore zip.
# Adapted from JotunnModStub (MIT-0): https://github.com/Valheim-Modding/JotunnModStub

target="Debug"
targetPath=""
targetAssembly="InvisibilityPotion.dll"
valheimPath=""
bepinexPath=""
deployPath=""
projectPath="./InvisibilityPotion"

while [ "$#" -gt 0 ]; do
  case "$1" in
    --target)          target="$2"; shift 2 ;;
    --target-path)     targetPath="$2"; shift 2 ;;
    --target-assembly) targetAssembly="$2"; shift 2 ;;
    --valheim-path)    valheimPath="$2"; shift 2 ;;
    --bepinex-path)    bepinexPath="$2"; shift 2 ;;
    --deploy-path)     deployPath="$2"; shift 2 ;;
    --project-path)    projectPath="$2"; shift 2 ;;
    *) echo "Warning: Unknown argument $1" >&2; shift ;;
  esac
done

# precedence: MOD_DEPLOYPATH > BEPINEX_PATH > VALHEIM_INSTALL > default
if [ -z "$deployPath" ]; then
  if [ -n "$bepinexPath" ]; then deployPath="$bepinexPath/plugins"
  elif [ -n "$valheimPath" ]; then deployPath="$valheimPath/BepInEx/plugins"
  else deployPath="$HOME/.local/share/Steam/steamapps/common/Valheim/BepInEx/plugins"
  fi
fi

name=$(echo "$targetAssembly" | sed 's/\.dll$//')

if [ "$target" = "Debug" ]; then
  plug="$deployPath/$name"
  echo "Deploying $targetAssembly to $plug"
  mkdir -p "$plug"
  cp "$targetPath/$targetAssembly" "$plug/"
  [ -e "$targetPath/$name.pdb" ] && cp "$targetPath/$name.pdb" "$plug/"
fi

if [ "$target" = "Release" ]; then
  packagePath="$projectPath/Package"
  mkdir -p "$packagePath/plugins"
  cp "$targetPath/$targetAssembly" "$packagePath/plugins/"
  if command -v zip > /dev/null; then
    [ -e "$projectPath/$name.zip" ] && rm "$projectPath/$name.zip"
    (cd "$packagePath" && zip -r "../$name.zip" . -x '*.zip' > /dev/null)
    echo "Package ready: $(realpath "$projectPath/$name.zip")"
  else
    echo "zip not installed, skipping package"
  fi
fi
```

Run: `chmod +x scripts/publish.sh`

- [ ] **Step 4: Write the csproj**

`InvisibilityPotion/InvisibilityPotion.csproj`:

```xml
<?xml version="1.0" encoding="utf-8"?>
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Library</OutputType>
    <TargetFramework>net48</TargetFramework>
    <RootNamespace>InvisibilityPotion</RootNamespace>
    <AssemblyName>InvisibilityPotion</AssemblyName>
    <Version>0.1.0</Version>
    <LangVersion>10</LangVersion>
    <Nullable>disable</Nullable>
    <Deterministic>true</Deterministic>
    <GenerateAssemblyInfo>true</GenerateAssemblyInfo>
    <!-- portable is the only pdb kind the Linux SDK can emit; BepInEx reads it for stack traces -->
    <DebugType>portable</DebugType>
    <DebugSymbols>true</DebugSymbols>
    <WarningLevel>4</WarningLevel>
    <TreatWarningsAsErrors>false</TreatWarningsAsErrors>
  </PropertyGroup>

  <PropertyGroup Condition=" '$(Configuration)' == 'Debug' ">
    <DefineConstants>DEBUG;TRACE</DefineConstants>
    <Optimize>false</Optimize>
  </PropertyGroup>

  <PropertyGroup Condition=" '$(Configuration)' == 'Release' ">
    <Optimize>true</Optimize>
  </PropertyGroup>

  <ItemGroup>
    <!-- Supplies game, BepInEx, Harmony and Unity references from $(VALHEIM_INSTALL) and the publicizer prebuild task -->
    <PackageReference Include="JotunnLib" Version="2.30.2" />
  </ItemGroup>

  <ItemGroup>
    <None Include="Package\**" />
  </ItemGroup>

  <Target Name="PublishToGame" AfterTargets="Build" Condition=" '$(OS)' == 'Unix' ">
    <Exec Command="sh &quot;$(SolutionDir)scripts/publish.sh&quot; --target &quot;$(Configuration)&quot; --target-path &quot;$(TargetDir)&quot; --target-assembly &quot;$(TargetFileName)&quot; --valheim-path &quot;$(VALHEIM_INSTALL)&quot; --deploy-path &quot;$(MOD_DEPLOYPATH)&quot; --project-path &quot;$(ProjectDir)&quot;" />
  </Target>
</Project>
```

- [ ] **Step 5: Write IgnoreAccessModifiers.cs**

`InvisibilityPotion/Properties/IgnoreAccessModifiers.cs`:

```csharp
// Lets the Mono JIT skip access checks so members made public by the publicizer
// at compile time can be used against the original game assembly at runtime.
// Pattern from JotunnModStub (MIT-0).
using System.Security.Permissions;

#pragma warning disable CS0618 // SecurityPermission is obsolete but still honoured by Mono
[assembly: SecurityPermission(SecurityAction.RequestMinimum, SkipVerification = true)]
```

- [ ] **Step 6: Write Plugin.cs**

`InvisibilityPotion/Plugin.cs`:

```csharp
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using Jotunn.Utils;

namespace InvisibilityPotion
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInDependency(Jotunn.Main.ModGuid)]
    [NetworkCompatibility(CompatibilityLevel.EveryoneMustHaveMod, VersionStrictness.Minor)]
    public class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid = "saikopsych.InvisibilityPotion";
        public const string PluginName = "InvisibilityPotion";
        public const string PluginVersion = "0.1.0";

        public static ManualLogSource Log { get; private set; }
        public static Harmony HarmonyInstance { get; private set; }

        private void Awake()
        {
            Log = Logger;
            HarmonyInstance = new Harmony(PluginGuid);
            HarmonyInstance.PatchAll(typeof(Plugin).Assembly);
            Log.LogInfo($"{PluginName} {PluginVersion} loaded");
        }

        private void OnDestroy()
        {
            HarmonyInstance?.UnpatchSelf();
        }
    }
}
```

- [ ] **Step 7: Write the Thunderstore package files**

`InvisibilityPotion/Package/manifest.json`:

```json
{
  "name": "InvisibilityPotion",
  "description": "Three tiers of invisibility potions. Enemies lose you, attacking reveals you.",
  "version_number": "0.1.0",
  "website_url": "",
  "dependencies": [
    "denikson-BepInExPack_Valheim-5.4.2350",
    "ValheimModding-Jotunn-2.30.2"
  ]
}
```

`InvisibilityPotion/Package/README.md`:

```markdown
# InvisibilityPotion

Three tiers of invisibility potions for Valheim. Enemies perceive you less (tier I) or not at all (tier II/III). Attacking reveals you and slows stamina regeneration. Higher tiers re-hide you after a few seconds. Tier III also hides you from other players.

Requires BepInEx and Jötunn on client and server. Configuration is server-synced.
```

`icon.png`: a 256x256 PNG is required by Thunderstore. Generate a placeholder:

Run: `python3 -c "import zlib,struct;w=h=256;raw=b''.join(b'\x00'+bytes([40,20,60,255])*w for _ in range(h));d=lambda t,b:struct.pack('>I',len(b))+t+b+struct.pack('>I',zlib.crc32(t+b)&0xffffffff);open('InvisibilityPotion/Package/icon.png','wb').write(b'\x89PNG\r\n\x1a\n'+d(b'IHDR',struct.pack('>IIBBBBB',w,h,8,6,0,0,0))+d(b'IDAT',zlib.compress(raw))+d(b'IEND',b''))"`

- [ ] **Step 8: Add build and package targets to the Makefile**

Append to `Makefile` (keep the TAB indentation):

```makefile
.PHONY: build package

build: ## Build Debug and deploy the DLL into BepInEx/plugins (via scripts/publish.sh)
	dotnet build InvisibilityPotion.sln -c Debug -nologo -v minimal

package: ## Build Release and zip the Thunderstore package
	dotnet build InvisibilityPotion.sln -c Release -nologo -v minimal
```

Add `build package` to the existing `.PHONY` line or keep the second line; both are fine.

- [ ] **Step 9: Build**

Run: `make build 2>&1 | tail -20`
Expected: "Executing Jotunn Prebuild Task" on the first build (publicized DLLs written to `valheim_Data/Managed/publicized_assemblies/`), then `Deploying InvisibilityPotion.dll to .../BepInEx/plugins/InvisibilityPotion`, then `Build succeeded` with 0 errors.

If the error mentions `Microsoft.NETFramework.ReferenceAssemblies`, add `<PackageReference Include="Microsoft.NETFramework.ReferenceAssemblies" Version="1.0.3" PrivateAssets="all" />` to the csproj and rebuild.

Then: `ls -la "$HOME/.local/share/Steam/steamapps/common/Valheim/BepInEx/plugins/InvisibilityPotion/"`
Expected: `InvisibilityPotion.dll` and `InvisibilityPotion.pdb`.

- [ ] **Step 10: Commit**

```bash
git add InvisibilityPotion.sln DoPrebuild.props scripts/publish.sh InvisibilityPotion/ Makefile
git commit -m "feat: plugin skeleton that builds on Linux and deploys to BepInEx"
```

---

### Task 4: Launch the game from the Makefile and verify the plugin loads

**Files:**
- Modify: `Makefile` (add `run`, `log`)

**Interfaces:**
- Produces: `make run` (build + launch with console, windowed), `make log`.

- [ ] **Step 1: Add run and log targets**

Append to `Makefile`:

```makefile
.PHONY: run log
GAME_ARGS ?= -console -screen-fullscreen 0 -screen-width 1600 -screen-height 900
# World and character the Debug build auto-joins (see InvisibilityPotion/Dev/AutoJoin.cs). Empty = normal menu.
IP_DEV_WORLD ?= testing
IP_DEV_CHARACTER ?=

run: build ## Build, then start Valheim with BepInEx, console enabled, windowed. Steam must be running.
	@pgrep -x steam > /dev/null || { echo "Steam is not running. Start Steam first."; exit 1; }
	cd "$(VALHEIM_INSTALL)" && IP_DEV_WORLD="$(IP_DEV_WORLD)" IP_DEV_CHARACTER="$(IP_DEV_CHARACTER)" ./start_game_bepinex.sh $(GAME_ARGS)

log: ## Follow the BepInEx log
	tail -n 50 -f "$(VALHEIM_INSTALL)/BepInEx/LogOutput.log"
```

- [ ] **Step 2: Launch and verify the load line**

Ask the user to run `make run` in their own terminal (the game needs a display; running it from the agent's shell may hang the session). Expected in the terminal and in `make log`:

```
[Info   :InvisibilityPotion] InvisibilityPotion 0.1.0 loaded
```

and a line from Jötunn listing the mod in the compatibility data. Ask the user to confirm the line appeared, then quit the game.

- [ ] **Step 3: Commit**

```bash
git add Makefile
git commit -m "build: add run and log targets for the in-game test loop"
```

---

### Task 5: Dev auto-join into the test world (DEBUG only)

**Files:**
- Create: `InvisibilityPotion/Dev/AutoJoin.cs`
- Modify: `InvisibilityPotion/Plugin.cs`

**Interfaces:**
- Consumes: `FejdStartup` (menu controller), `PlayerProfile.GetAllPlayerProfiles()`, `SaveSystem.GetWorldList()`, `Game.SetProfile(...)`, `ZNet.SetServer(...)`, `ZNet.ResetServerHost()`, `FejdStartup.LoadMainScene()`. All exact signatures are read from the decompile in step 1.
- Produces: env vars `IP_DEV_WORLD` and `IP_DEV_CHARACTER` (name of an existing local world/character). With `IP_DEV_WORLD` set, the game loads that world after the menu appears.

- [ ] **Step 1: Read how vanilla starts a local world**

Run: `grep -n "OnWorldStart\|LoadMainScene\|SetServer\|SetProfile\|GetWorldList\|GetAllPlayerProfiles\|m_startingWorld\|OnStartGame\|OnSelectWorld" tools/decompiled/assembly_valheim/FejdStartup.cs | head -40`

Then read `FejdStartup.OnWorldStart()` and `FejdStartup.OnStartGame()` (or whatever method `OnWorldStart` calls) fully with `sed -n`. Write down: (a) how the selected `World` gets from the list into `ZNet.SetServer`, (b) the exact `ZNet.SetServer` parameter list, (c) how the profile is selected (`Game.SetProfile(string filename, FileHelpers.FileSource source)` or similar), (d) whether `LoadMainScene` is public. Also run: `grep -n "public static.*GetWorldList\|public static.*GetAllPlayerProfiles" tools/decompiled/assembly_valheim/SaveSystem.cs tools/decompiled/assembly_valheim/PlayerProfile.cs`.

- [ ] **Step 2: Write AutoJoin.cs using the signatures you found**

Template (adjust parameter lists to what step 1 showed; keep the structure):

```csharp
#if DEBUG
using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;

namespace InvisibilityPotion.Dev
{
    /// <summary>
    /// Debug-only: when IP_DEV_WORLD is set, skip the main menu and load that local world
    /// with the character named by IP_DEV_CHARACTER (or the first character found).
    /// </summary>
    [HarmonyPatch(typeof(FejdStartup), nameof(FejdStartup.Start))]
    internal static class AutoJoin
    {
        private static bool _done;

        [HarmonyPostfix]
        private static void Postfix(FejdStartup __instance)
        {
            if (_done) return;
            var worldName = Environment.GetEnvironmentVariable("IP_DEV_WORLD");
            if (string.IsNullOrEmpty(worldName)) return;
            _done = true;
            __instance.StartCoroutine(JoinNextFrame(__instance, worldName,
                Environment.GetEnvironmentVariable("IP_DEV_CHARACTER")));
        }

        private static System.Collections.IEnumerator JoinNextFrame(FejdStartup startup, string worldName, string characterName)
        {
            // Give FejdStartup one frame to finish its own initialisation (profiles, world list).
            yield return null;
            try
            {
                var profiles = PlayerProfile.GetAllPlayerProfiles();
                var profile = string.IsNullOrEmpty(characterName)
                    ? profiles.FirstOrDefault()
                    : profiles.FirstOrDefault(p => string.Equals(p.GetName(), characterName, StringComparison.OrdinalIgnoreCase));
                if (profile == null)
                {
                    Plugin.Log.LogError($"AutoJoin: character '{characterName}' not found. Available: {string.Join(", ", profiles.Select(p => p.GetName()))}");
                    yield break;
                }

                var worlds = SaveSystem.GetWorldList();
                var world = worlds.FirstOrDefault(w => string.Equals(w.m_name, worldName, StringComparison.OrdinalIgnoreCase));
                if (world == null)
                {
                    Plugin.Log.LogError($"AutoJoin: world '{worldName}' not found. Available: {string.Join(", ", worlds.Select(w => w.m_name))}");
                    yield break;
                }

                Plugin.Log.LogInfo($"AutoJoin: loading world '{world.m_name}' as '{profile.GetName()}'");
                // Mirror FejdStartup.OnWorldStart: select profile, configure a local (non-public) server, load main scene.
                Game.SetProfile(profile.GetFilename(), profile.m_fileSource);
                ZNet.SetServer(true, false, false, world.m_name, "", world);
                ZNet.ResetServerHost();
                startup.LoadMainScene();
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"AutoJoin failed: {e}");
            }
        }
    }
}
#endif
```

The `try/catch` cannot wrap a `yield`; if the compiler complains, move the body into a separate `static void Join(...)` method called after `yield return null` and wrap that call in `try/catch`.

- [ ] **Step 3: Build and run**

Run: `make build 2>&1 | tail -5` → `Build succeeded`.
Ask the user to run `make run`. Expected: the game boots straight into the `testing` world within a few seconds of the menu appearing, and the log shows `AutoJoin: loading world 'testing' as '<name>'`. If the character list is empty, ask the user which character name to use and set `IP_DEV_CHARACTER` in the Makefile.

- [ ] **Step 4: Commit**

```bash
git add InvisibilityPotion/Dev/AutoJoin.cs
git commit -m "feat(dev): auto-join a local world from IP_DEV_WORLD in Debug builds"
```

---

### Task 6: Patch health check with unit tests, and `ip_state` dev command

**Files:**
- Create: `InvisibilityPotion.Tests/InvisibilityPotion.Tests.csproj`
- Create: `InvisibilityPotion.Tests/PatchHealthTests.cs`
- Create: `InvisibilityPotion/PatchHealth.cs`
- Create: `InvisibilityPotion/Dev/DevCommands.cs`
- Modify: `InvisibilityPotion/Plugin.cs`
- Modify: `InvisibilityPotion.sln`, `Makefile` (add `test`)

**Interfaces:**
- Produces: `InvisibilityPotion.PatchHealth` with
  `public static IReadOnlyList<string> MissingTargets(IEnumerable<string> expected, IEnumerable<string> patched)` (pure, tested) and
  `public static void Report(Harmony harmony, IEnumerable<string> expectedTargets)` (game-side, logs errors).
  Target strings use the form `"TypeName.MethodName"`.
- Later plans append their own targets to `Plugin.ExpectedPatchTargets`.

- [ ] **Step 1: Create the test project**

`InvisibilityPotion.Tests/InvisibilityPotion.Tests.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>disable</Nullable>
    <IsPackable>false</IsPackable>
    <LangVersion>10</LangVersion>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.11.1" />
    <PackageReference Include="xunit" Version="2.9.2" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.8.2" />
  </ItemGroup>
  <ItemGroup>
    <!-- Pure logic is compiled straight into the test assembly; the game project cannot be referenced from net8.0 -->
    <Compile Include="../InvisibilityPotion/PatchHealth.Core.cs" Link="PatchHealth.Core.cs" />
  </ItemGroup>
</Project>
```

Note the split: `PatchHealth.Core.cs` (pure, no game types) and `PatchHealth.cs` (Harmony-side, `partial` class). Only the core file is compiled into tests.

- [ ] **Step 2: Write the failing test**

`InvisibilityPotion.Tests/PatchHealthTests.cs`:

```csharp
using System.Linq;
using InvisibilityPotion;
using Xunit;

public class PatchHealthTests
{
    [Fact]
    public void MissingTargets_ReturnsEmpty_WhenAllExpectedArePatched()
    {
        var expected = new[] { "BaseAI.CanSenseTarget", "Humanoid.StartAttack" };
        var patched = new[] { "Humanoid.StartAttack", "BaseAI.CanSenseTarget", "FejdStartup.Start" };

        var missing = PatchHealth.MissingTargets(expected, patched);

        Assert.Empty(missing);
    }

    [Fact]
    public void MissingTargets_ListsOnlyUnpatchedExpectedTargets_InExpectedOrder()
    {
        var expected = new[] { "BaseAI.CanSenseTarget", "Humanoid.StartAttack", "ZDOMan.SendZDOs" };
        var patched = new[] { "Humanoid.StartAttack" };

        var missing = PatchHealth.MissingTargets(expected, patched);

        Assert.Equal(new[] { "BaseAI.CanSenseTarget", "ZDOMan.SendZDOs" }, missing);
    }

    [Fact]
    public void MissingTargets_IsCaseSensitive()
    {
        var missing = PatchHealth.MissingTargets(new[] { "BaseAI.CanSenseTarget" }, new[] { "baseai.cansensetarget" });

        Assert.Single(missing);
    }

    [Fact]
    public void TargetKey_FormatsAsTypeDotMethod()
    {
        Assert.Equal("BaseAI.CanSenseTarget", PatchHealth.TargetKey("BaseAI", "CanSenseTarget"));
    }
}
```

- [ ] **Step 3: Run the test to verify it fails**

Run: `dotnet test InvisibilityPotion.Tests -nologo -v quiet 2>&1 | tail -5`
Expected: build error `The type or namespace name 'PatchHealth' could not be found` (the core file does not exist yet).

- [ ] **Step 4: Write PatchHealth.Core.cs (pure)**

`InvisibilityPotion/PatchHealth.Core.cs`:

```csharp
using System.Collections.Generic;
using System.Linq;

namespace InvisibilityPotion
{
    /// <summary>
    /// Pure logic for the startup patch health check. No game or Harmony types here so it can be unit-tested.
    /// </summary>
    public static partial class PatchHealth
    {
        public static string TargetKey(string typeName, string methodName) => typeName + "." + methodName;

        /// <summary>Expected targets that do not appear in <paramref name="patched"/>, in the expected order.</summary>
        public static IReadOnlyList<string> MissingTargets(IEnumerable<string> expected, IEnumerable<string> patched)
        {
            var patchedSet = new HashSet<string>(patched);
            return expected.Where(t => !patchedSet.Contains(t)).ToList();
        }
    }
}
```

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test InvisibilityPotion.Tests -nologo -v quiet 2>&1 | tail -3`
Expected: `Passed! - Failed: 0, Passed: 4`.

- [ ] **Step 6: Write PatchHealth.cs (Harmony side)**

`InvisibilityPotion/PatchHealth.cs`:

```csharp
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;

namespace InvisibilityPotion
{
    public static partial class PatchHealth
    {
        /// <summary>Every "Type.Method" that currently carries at least one patch owned by <paramref name="harmony"/>.</summary>
        public static IReadOnlyList<string> PatchedTargets(Harmony harmony)
        {
            return harmony.GetPatchedMethods()
                .Where(m => Harmony.GetPatchInfo(m)?.Owners.Contains(harmony.Id) == true)
                .Select(m => TargetKey(m.DeclaringType?.Name ?? "?", m.Name))
                .Distinct()
                .ToList();
        }

        /// <summary>Logs one error per expected target that did not get patched. Returns the missing list.</summary>
        public static IReadOnlyList<string> Report(Harmony harmony, IEnumerable<string> expectedTargets)
        {
            var patched = PatchedTargets(harmony);
            var missing = MissingTargets(expectedTargets, patched);
            foreach (var target in missing)
            {
                Plugin.Log.LogError($"Patch missing: {target}. The game may have changed; this feature is disabled.");
            }
            Plugin.Log.LogInfo($"Patch health: {patched.Count} targets patched, {missing.Count} missing");
            return missing;
        }
    }
}
```

- [ ] **Step 7: Wire it into Plugin.cs and add the dev command**

Replace `Plugin.cs` with:

```csharp
using System.Collections.Generic;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using Jotunn.Utils;

namespace InvisibilityPotion
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInDependency(Jotunn.Main.ModGuid)]
    [NetworkCompatibility(CompatibilityLevel.EveryoneMustHaveMod, VersionStrictness.Minor)]
    public class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid = "saikopsych.InvisibilityPotion";
        public const string PluginName = "InvisibilityPotion";
        public const string PluginVersion = "0.1.0";

        public static ManualLogSource Log { get; private set; }
        public static Harmony HarmonyInstance { get; private set; }

        /// <summary>Game methods this mod must have patched for its features to work. Later plans add entries.</summary>
        public static readonly List<string> ExpectedPatchTargets = new List<string>
        {
#if DEBUG
            PatchHealth.TargetKey("FejdStartup", "Start"),
#endif
        };

        public static IReadOnlyList<string> MissingPatches { get; private set; } = new List<string>();

        private void Awake()
        {
            Log = Logger;
            HarmonyInstance = new Harmony(PluginGuid);
            HarmonyInstance.PatchAll(typeof(Plugin).Assembly);
            MissingPatches = PatchHealth.Report(HarmonyInstance, ExpectedPatchTargets);
#if DEBUG
            Dev.DevCommands.Register();
#endif
            Log.LogInfo($"{PluginName} {PluginVersion} loaded");
        }

        private void OnDestroy()
        {
            HarmonyInstance?.UnpatchSelf();
        }
    }
}
```

`InvisibilityPotion/Dev/DevCommands.cs`:

```csharp
#if DEBUG
using Jotunn.Entities;
using Jotunn.Managers;

namespace InvisibilityPotion.Dev
{
    /// <summary>Debug-only console commands. Open the console with F5 (game started with -console).</summary>
    internal static class DevCommands
    {
        public static void Register()
        {
            CommandManager.Instance.AddConsoleCommand(new StateCommand());
        }

        private class StateCommand : ConsoleCommand
        {
            public override string Name => "ip_state";
            public override string Help => "InvisibilityPotion: print patch health and plugin state";

            public override void Run(string[] args)
            {
                var patched = PatchHealth.PatchedTargets(Plugin.HarmonyInstance);
                Console.instance.Print($"{Plugin.PluginName} {Plugin.PluginVersion}");
                Console.instance.Print($"patched: {string.Join(", ", patched)}");
                Console.instance.Print($"missing: {(Plugin.MissingPatches.Count == 0 ? "none" : string.Join(", ", Plugin.MissingPatches))}");
            }
        }
    }
}
#endif
```

- [ ] **Step 8: Add the test project to the solution and Makefile**

In `InvisibilityPotion.sln` add after the first `EndProject`:

```
Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "InvisibilityPotion.Tests", "InvisibilityPotion.Tests\InvisibilityPotion.Tests.csproj", "{9B2D5E3F-4C7E-4F9B-AD1A-2E3F4A5B6C7D}"
EndProject
```

and the four matching `{9B2D5E3F-...}.Debug|Any CPU...` / `Release` lines in `ProjectConfigurationPlatforms`, copied from the existing project's block with the new GUID.

Change `build` and `package` in the Makefile to build only the game project so the test project does not go through the game's publish step:

```makefile
build: ## Build Debug and deploy the DLL into BepInEx/plugins (via scripts/publish.sh)
	dotnet build InvisibilityPotion/InvisibilityPotion.csproj -c Debug -nologo -v minimal

package: ## Build Release and zip the Thunderstore package
	dotnet build InvisibilityPotion/InvisibilityPotion.csproj -c Release -nologo -v minimal

test: ## Run unit tests (pure logic, no game DLLs)
	dotnet test InvisibilityPotion.Tests -nologo -v quiet
```

Add `test` to `.PHONY`. Because the csproj is built directly, `$(SolutionDir)` is empty; Jötunn's `Paths.props` then imports `Environment.props` from two folders above the package, which is wrong. Fix by adding to the csproj's first `<PropertyGroup>`:

```xml
    <SolutionDir Condition="'$(SolutionDir)' == ''">$(MSBuildProjectDirectory)/../</SolutionDir>
```

- [ ] **Step 9: Build, test, and verify in-game**

Run: `make test 2>&1 | tail -3` → `Passed! ... Passed: 4`.
Run: `make build 2>&1 | tail -5` → `Build succeeded`.
Ask the user to `make run`, press F5 in-game, type `ip_state`. Expected console output lists `FejdStartup.Start` under patched and `missing: none`; the log shows `Patch health: 1 targets patched, 0 missing`.

- [ ] **Step 10: Commit**

```bash
git add InvisibilityPotion.Tests InvisibilityPotion/PatchHealth.Core.cs InvisibilityPotion/PatchHealth.cs InvisibilityPotion/Dev/DevCommands.cs InvisibilityPotion/Plugin.cs InvisibilityPotion.sln Makefile InvisibilityPotion/InvisibilityPotion.csproj
git commit -m "feat: patch health check with unit tests and ip_state dev command"
```

---

### Task 7: Verify every game hook from the spec in the decompile

**Files:**
- Create: `docs/decompile-notes.md`

**Interfaces:**
- Produces: confirmed signatures and semantics that plans 2 and 3 use verbatim.

- [ ] **Step 1: Read the perception path**

In `tools/decompiled/assembly_valheim/BaseAI.cs` read `CanSenseTarget`, `CanSeeTarget`, `CanHearTarget`, `FindEnemy`, `IsEnemy`, `SetAlerted`. In `MonsterAI.cs` read `UpdateTarget` (or wherever `m_timeSinceSensedTargetCreature` is compared) and `UpdateAI`. Record for each: full signature, whether it is `public`/`private`, what it returns for a `Player` target, and the constant that decides when a target is dropped (search `m_timeSinceSensedTargetCreature >`). Also record whether `Player.InGhostMode()` is consulted anywhere in BaseAI/MonsterAI (`grep -n InGhostMode tools/decompiled/assembly_valheim/*.cs`).

- [ ] **Step 2: Read the stealth math**

In `Character.cs` read `GetStealthFactor`, `GetNoiseRange`; in `Player.cs` read `UpdateStealth`; in `SE_Stats.cs` read `ModifyStealth`, `ModifyNoise`, `ModifyStaminaRegen`, and the fields `m_stealthModifier`, `m_noiseModifier`, `m_staminaRegenMultiplier`. Record how tier I's ×0.25 maps onto these (multiplier vs. additive) and whether `m_staminaRegenMultiplier` alone gives the debuff or `ModifyStaminaRegen` must be overridden.

- [ ] **Step 3: Read the reveal triggers**

Read `Humanoid.StartAttack` (signature, return value meaning), `Humanoid.BlockAttack`, `Humanoid.IsDrawingBow` and where bow drawing starts (`Attack.cs`, search `m_bowDraw`), `Character.Damage(HitData)` and `Character.RPC_Damage`, and how `HitData.GetAttacker()` resolves. Record which method runs on the attacker's client and which on the victim's.

- [ ] **Step 4: Read the network and visual hooks**

Read `ZDOMan.SendZDOs` (signature, how the peer is known, whether the peer is the server), `ZNet.SendPeriodicData`, `ZNet.m_publicReferencePosition` usage, `EnemyHud.UpdateHuds` or `TestShow`, `Minimap.UpdatePlayerPins`, `VisEquipment.UpdateEquipmentVisuals` (or the method that (re)creates armour/item renderers), and the `ZDO` string/int/bool `Set`/`Get` overloads with their hash helper (`ZDOVars` or `StringExtensionMethods.GetStableHashCode`).

- [ ] **Step 5: Read the consume path**

Read `Humanoid.UseItem` / `Player.ConsumeItem` / `Player.CanConsumeItem` (search `m_consumeStatusEffect` in `Player.cs` and `Humanoid.cs`). Record where the status effect is applied and whether a refusal (lower tier while higher is active) can stop the item from being consumed and what the vanilla "already have effect" check looks like. Also read `SEMan.AddStatusEffect` overloads and `StatusEffect.Setup`, `Stop`, `UpdateStatusEffect`, `ResetTime`, `IsDone`.

- [ ] **Step 6: Write docs/decompile-notes.md**

Structure, one section per spec item, with the exact signature copied from the decompile and a one-line finding:

```markdown
# Decompile notes – Valheim 1.0.16 (build 25527674)

Source: `make decompile` output, ilspycmd 11.1. Rerun after every game update and diff this file.

## Perception
- `BaseAI.CanSenseTarget(Character target)` – `public bool`, calls CanSeeTarget || CanHearTarget … (finding)
- …

## Aggro loss
- `MonsterAI.UpdateTarget(...)` – drops `m_targetCreature` when `m_timeSinceSensedTargetCreature > 30f` at line N …

## Stealth math
## Reveal triggers
## Consume path
## Network
## Visuals
## Ghost mode (for reference)
## Open questions for plan 2
```

Every entry must state `public`/`private` and the file:line in `tools/decompiled/`.

- [ ] **Step 7: Update the spec markers**

In `docs/superpowers/specs/2026-09-30-invisibility-potion-design.md` replace each *(verify ...)* marker with the verified name or a note "differs: see decompile-notes.md §X". Do not change decisions.

- [ ] **Step 8: Commit**

```bash
git add docs/decompile-notes.md docs/superpowers/specs/2026-09-30-invisibility-potion-design.md
git commit -m "docs: verify game hooks against the 1.0.16 decompile"
```

---

### Task 8: Project conventions and testing checklist

**Files:**
- Create: `CLAUDE.md`
- Create: `docs/testing.md`
- Modify: `README.md`

- [ ] **Step 1: Write CLAUDE.md**

```markdown
# InvisibilityPotion – project conventions

Valheim mod (BepInEx 5.4.23.5 + Jötunn 2.30.2), C# net48, MIT. Everything in English.

## Commands
- `make build` – Debug build, deploys to `$VALHEIM_INSTALL/BepInEx/plugins/InvisibilityPotion/`
- `make run` – build + start the game windowed with console; auto-joins world `$IP_DEV_WORLD` (default `testing`)
- `make log` – follow BepInEx/LogOutput.log
- `make test` – xunit tests (pure logic only)
- `make decompile` – regenerate `tools/decompiled/` from the installed game; rerun after game updates
- `make package` – Release build + Thunderstore zip

## Rules
- Verify every game member name in `tools/decompiled/` (and record it in `docs/decompile-notes.md`) before using it.
- Every Harmony patch target is listed in `Plugin.ExpectedPatchTargets`; the startup health check logs missing ones.
- Dev-only code lives in `InvisibilityPotion/Dev/` behind `#if DEBUG`.
- Pure logic goes into `*.Core.cs` files with no game types so it can be unit-tested from `InvisibilityPotion.Tests`.
- One cleanup path: `SE_Invisibility.Stop()` restores everything. Every exit must go through it.
- Copy code only from MIT / MIT-0 / Unlicense sources and credit them in a comment.
- `sudo` commands are run by the user; print them and wait.

## Layout
See `docs/superpowers/specs/2026-09-30-invisibility-potion-design.md` §4. Plans live in `docs/superpowers/plans/`.

## Environment
- Game: `~/.local/share/Steam/steamapps/common/Valheim` (BepInEx installed directly, no mod manager profile)
- Local test world: `testing`
- `Environment.props` (gitignored) holds `VALHEIM_INSTALL`
```

- [ ] **Step 2: Write docs/testing.md**

```markdown
# In-game testing checklist

Run `make run`, press F5 for the console, `devcommands` once per session. Update this file per plan.

## Plan 1 – skeleton
- [ ] Log shows `InvisibilityPotion 0.1.0 loaded` and `Patch health: … 0 missing`
- [ ] `IP_DEV_WORLD=testing make run` lands in the world without touching the menu
- [ ] `ip_state` prints patched targets and `missing: none`
- [ ] Release build (`make package`) contains no `ip_state` command and no auto-join

## Test matrix (from the spec, filled in from plan 2 on)

| Setup | Tier I | Tier II | Tier III |
|---|---|---|---|
| Singleplayer | | | |
| Self-hosted, I am host | | | |
| Dedicated, I own the zone | | | |
| Dedicated, another player owns the zone | | | |
| Player joins while I am hidden | | | |
```

- [ ] **Step 3: Update README.md**

Replace the `Status:` line with `Status: skeleton loads in-game, no gameplay yet. Licence: MIT.` and add a "Development" section:

```markdown
## Development

Linux, CLI only. Install `dotnet-sdk-8.0`, then `make help`. See `CLAUDE.md` for the workflow and `docs/testing.md` for the in-game checklist.
```

- [ ] **Step 4: Verify the Release build strips dev code**

Run: `make package 2>&1 | tail -3 && strings InvisibilityPotion/bin/Release/net48/InvisibilityPotion.dll | grep -c 'ip_state\|AutoJoin'`
Expected: `Package ready: ...` and `0`.

- [ ] **Step 5: Commit**

```bash
git add CLAUDE.md docs/testing.md README.md
git commit -m "docs: add project conventions, testing checklist and dev section"
```

---

## Self-review

**Spec coverage (plan 1 scope: build steps 1 and 2, spec §3, §4, §5.1, §5.10, §6):** toolchain (Task 1), decompile (Task 2), csproj/skeleton/`NetworkCompatibility`/publish (Task 3), `make run`/`log` (Task 4), auto-join env vars (Task 5), patch health check + unit test project + `ip_state` (Task 6), hook verification and spec marker cleanup (Task 7), CLAUDE.md/testing.md (Task 8). `ip_give` and `ip_spawn` from §5.10 need the status effect and belong to plan 2. `make deploy-server` belongs to plan 3. Unity Editor install is started in Task 1 but used in plan 4.

**Placeholders:** none. The auto-join code is a template whose parameter lists are confirmed in Task 5 step 1, which is an explicit investigation step with commands.

**Type consistency:** `PatchHealth.TargetKey`, `MissingTargets`, `PatchedTargets`, `Report` are used with the same names in Tasks 6 and CLAUDE.md. `Plugin.Log`, `Plugin.HarmonyInstance`, `Plugin.ExpectedPatchTargets`, `Plugin.MissingPatches` match between Plugin.cs and DevCommands.cs.

## Follow-up plans

- Plan 2: status effect, ZDO state, perception/aggro patches, reveal triggers, debuff, fog veil, re-hide, placeholder items (spec build steps 3–6).
- Plan 3: server deploy (LXC), multiplayer test matrix, tier III hidden-from-players (steps 7–8).
- Plan 4: Unity project, bottle meshes, asset bundle, icons, recipes, balancing, Thunderstore release (step 9).
