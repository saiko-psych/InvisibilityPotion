# InvisibilityPotion – developer commands. Run `make help`.
SHELL := /bin/bash
VALHEIM_INSTALL ?= $(shell sed -n 's|.*<VALHEIM_INSTALL>\(.*\)</VALHEIM_INSTALL>.*|\1|p' Environment.props 2>/dev/null || true)
VALHEIM_INSTALL := $(if $(VALHEIM_INSTALL),$(VALHEIM_INSTALL),$(HOME)/.local/share/Steam/steamapps/common/Valheim)
MANAGED := $(VALHEIM_INSTALL)/valheim_Data/Managed
DECOMPILE_DIR := tools/decompiled
ILSPY := $(HOME)/.dotnet/tools/ilspycmd

.PHONY: help decompile build package test syntax check-scripts ci run play log bundle unity-setup models compat-check

help: ## Show this help
	@grep -E '^[a-zA-Z_-]+:.*?## ' $(MAKEFILE_LIST) | awk 'BEGIN {FS = ":.*?## "}; {printf "  %-14s %s\n", $$1, $$2}'

decompile: ## Decompile assembly_valheim.dll into tools/decompiled (rerun after game updates)
	@test -x "$(ILSPY)" || { echo "ilspycmd not found at $(ILSPY) (dotnet tool install -g ilspycmd)"; exit 1; }
	@test -f "$(MANAGED)/assembly_valheim.dll" || { echo "$(MANAGED)/assembly_valheim.dll not found (VALHEIM_INSTALL?)"; exit 1; }
	@rm -rf "$(DECOMPILE_DIR)/assembly_valheim.new" && mkdir -p "$(DECOMPILE_DIR)"
	"$(ILSPY)" -p -o "$(DECOMPILE_DIR)/assembly_valheim.new" --nested-directories "$(MANAGED)/assembly_valheim.dll"
	@rm -rf "$(DECOMPILE_DIR)/assembly_valheim" && mv "$(DECOMPILE_DIR)/assembly_valheim.new" "$(DECOMPILE_DIR)/assembly_valheim"   # the old decompile stays if ilspycmd fails
	@echo "Game version: $$(grep -oE 'l-[0-9]+\.[0-9]+\.[0-9]+' $(HOME)/.config/unity3d/IronGate/Valheim/Player.log | tail -1)" > $(DECOMPILE_DIR)/VERSION
	@ls $(DECOMPILE_DIR)/assembly_valheim | wc -l | xargs echo "Decompiled files:"

# Overwriting the deployed DLL under a running game crashes it. The [v] keeps pgrep from matching this recipe's own shell.
FORCE ?=
define guard_game
	@if [ "$(FORCE)" != "1" ] && pgrep -f '[v]alheim\.x86_64' > /dev/null; then \
		echo "Valheim is running: close the game first (overwriting the deployed DLL crashes it), or override with FORCE=1."; exit 1; fi
endef

build: ## Build Debug and deploy the DLL into BepInEx/plugins (refuses while Valheim runs; FORCE=1 overrides)
	$(guard_game)
	dotnet build InvisibilityPotion/InvisibilityPotion.csproj -c Debug -nologo -v minimal

package: ## Build Release and zip the Thunderstore package (refuses while Valheim runs; FORCE=1 overrides)
	$(guard_game)
	dotnet build InvisibilityPotion/InvisibilityPotion.csproj -c Release -nologo -v minimal

# Proves the code compiles against the newest Jötunn (API superset of the pinned 2.30.0); nothing is deployed or packaged
# (IP_SKIP_PUBLISH: the Release publish step used to copy this build into Package/ and rebuild the Thunderstore zip).
JOTUNN_LATEST ?= 2.30.2
compat-check: ## Compile Release against JOTUNN_LATEST into build/compat (no deploy)
	dotnet build InvisibilityPotion/InvisibilityPotion.csproj -c Release -nologo -v minimal -p:JotunnVersion=$(JOTUNN_LATEST) -p:IP_SKIP_PUBLISH=1 -p:OutDir=$(CURDIR)/build/compat/bin/
	@dotnet restore InvisibilityPotion/InvisibilityPotion.csproj -v quiet > /dev/null   # back to the pinned version for the next normal build
	@echo "Compiled against Jotunn $(JOTUNN_LATEST): OK"

test: ## Run unit tests (pure logic, no game DLLs)
	dotnet test InvisibilityPotion.Tests -nologo -v quiet

syntax: ## Parse the mod code with Roslyn, Debug and Release symbols (no game DLLs; catches syntax and #if mistakes only)
	dotnet run --project tools/syntax-check -- InvisibilityPotion

check-scripts: ## bash -n and shellcheck (warnings) on scripts/*.sh, frontmatter of .claude/skills/*/SKILL.md
	@for f in scripts/*.sh; do bash -n "$$f" || exit 1; done
	shellcheck -S warning -x scripts/*.sh
	python3 -I tools/ci/check_skills.py

ci: test syntax check-scripts ## What GitHub Actions runs (.github/workflows/ci.yml)

GAME_ARGS ?= -console -screen-fullscreen 0 -screen-width 1600 -screen-height 900
STEAM_APPID := 892970
# World and character the Debug build auto-joins (see InvisibilityPotion/Dev/AutoJoin.cs). Empty world = normal menu.
# Written to BepInEx/config/InvisibilityPotion.autojoin because env vars do not reach a game started through Steam.
IP_DEV_WORLD ?= testing
IP_DEV_CHARACTER ?=
AUTOJOIN_FILE := $(VALHEIM_INSTALL)/BepInEx/config/InvisibilityPotion.autojoin
BEPINEX_LOG := $(VALHEIM_INSTALL)/BepInEx/LogOutput.log

run: build ## Build, then `make play` (sequential even under -j: play must not start the game during the build)
	@$(MAKE) --no-print-directory play

# Launch without building: use this while the working tree is mid-edit so the deployed DLL stays the last finished build.
play: ## Launch Valheim through Steam (needs launch option "./start_game_bepinex.sh %command%") with auto-join, then follow the BepInEx log
	$(guard_game)
	@pgrep -x steam > /dev/null || { echo "Steam is not running. Start Steam first."; exit 1; }
	@mkdir -p "$(dir $(AUTOJOIN_FILE))"
	@printf 'world=%s\ncharacter=%s\n' "$(IP_DEV_WORLD)" "$(IP_DEV_CHARACTER)" > "$(AUTOJOIN_FILE)"
	@: > "$(BEPINEX_LOG)"
	steam -applaunch $(STEAM_APPID) $(GAME_ARGS)
	@echo "Game starting via Steam. Following $(BEPINEX_LOG) (Ctrl-C stops following, not the game)."
	@tail -n +1 -f "$(BEPINEX_LOG)"

log: ## Follow the BepInEx log
	tail -n 50 -f "$(BEPINEX_LOG)"

# Unity asset bundle (see docs/assets.md). Headless batch mode; the Personal licence comes from Unity Hub.
UNITY ?= $(HOME)/Unity/Hub/Editor/6000.0.75f1/Editor/Unity
UNITY_PROJECT := unity/InvisibilityPotionAssets
TARGET ?= linux
define guard_unity
	@if [ -e "$(UNITY_PROJECT)/Temp/UnityLockfile" ]; then \
		echo "$(UNITY_PROJECT) is open in a Unity editor (Temp/UnityLockfile exists): close it first."; exit 1; fi
endef

unity-setup: ## Import Assets/Models/*.fbx, write materials and prefabs (AssetSetup.Create) headless
	$(guard_unity)
	@mkdir -p build
	"$(UNITY)" -batchmode -nographics -quit -projectPath "$(UNITY_PROJECT)" \
		-executeMethod AssetSetup.Create -logFile build/unity-setup.log
	@echo "Log: build/unity-setup.log"

bundle: ## Build the asset bundle headless (TARGET=linux|windows) and copy it to InvisibilityPotion/Assets
	$(guard_unity)
	@case "$(TARGET)" in linux|windows) ;; *) echo "TARGET must be linux or windows"; exit 1;; esac
	@mkdir -p build InvisibilityPotion/Assets
	"$(UNITY)" -batchmode -nographics -quit -projectPath "$(UNITY_PROJECT)" \
		-executeMethod BundleBuilder.Build -target $(TARGET) -logFile build/unity-bundle-$(TARGET).log \
		|| { echo "Unity failed, see build/unity-bundle-$(TARGET).log"; exit 1; }
	@dest=InvisibilityPotion/Assets/ip_assets$(if $(filter windows,$(TARGET)),.windows,); \
		cp "$(UNITY_PROJECT)/Build/Bundles/$(TARGET)/ip_assets" "$$dest" && ls -l "$$dest"

# Blender models (see tools/blender/README.md). SKIP_BLENDER=1 only copies what tools/blender/out/ already holds.
BLENDER ?= blender
SKIP_BLENDER ?=
models: ## Run every tools/blender/make_*.py headless, then copy tools/blender/out/*.fbx into the Unity project
	@mkdir -p build
	@if [ "$(SKIP_BLENDER)" != "1" ]; then \
		for s in tools/blender/make_*.py; do echo "== $$s"; "$(BLENDER)" -b --python-exit-code 1 --python "$$s" > "build/blender-$$(basename $$s .py).log" 2>&1 \
			|| { echo "$$s failed, see build/blender-$$(basename $$s .py).log"; exit 1; }; done; fi
	@mkdir -p "$(UNITY_PROJECT)/Assets/Models"
	@ls tools/blender/out/*.fbx > /dev/null 2>&1 || { echo "no FBX in tools/blender/out/"; exit 1; }
	cp tools/blender/out/*.fbx "$(UNITY_PROJECT)/Assets/Models/"
	@ls "$(UNITY_PROJECT)/Assets/Models/"*.fbx | wc -l | xargs echo "FBX files in Assets/Models:"
