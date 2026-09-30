# InvisibilityPotion – developer commands. Run `make help`.
SHELL := /bin/bash
VALHEIM_INSTALL ?= $(HOME)/.local/share/Steam/steamapps/common/Valheim
MANAGED := $(VALHEIM_INSTALL)/valheim_Data/Managed
DECOMPILE_DIR := tools/decompiled
ILSPY := $(HOME)/.dotnet/tools/ilspycmd

.PHONY: help decompile build package run log

help: ## Show this help
	@grep -E '^[a-zA-Z_-]+:.*?## ' $(MAKEFILE_LIST) | awk 'BEGIN {FS = ":.*?## "}; {printf "  %-14s %s\n", $$1, $$2}'

decompile: ## Decompile assembly_valheim.dll into tools/decompiled (rerun after game updates)
	@rm -rf $(DECOMPILE_DIR)/assembly_valheim
	@mkdir -p $(DECOMPILE_DIR)
	$(ILSPY) -p -o $(DECOMPILE_DIR)/assembly_valheim --nested-directories $(MANAGED)/assembly_valheim.dll
	@echo "Game version: $$(grep -oE 'l-[0-9]+\.[0-9]+\.[0-9]+' $(HOME)/.config/unity3d/IronGate/Valheim/Player.log | tail -1)" > $(DECOMPILE_DIR)/VERSION
	@ls $(DECOMPILE_DIR)/assembly_valheim | wc -l | xargs echo "Decompiled files:"

build: ## Build Debug and deploy the DLL into BepInEx/plugins (via scripts/publish.sh)
	dotnet build InvisibilityPotion.sln -c Debug -nologo -v minimal

package: ## Build Release and zip the Thunderstore package
	dotnet build InvisibilityPotion.sln -c Release -nologo -v minimal

GAME_ARGS ?= -console -screen-fullscreen 0 -screen-width 1600 -screen-height 900
STEAM_APPID := 892970
# World and character the Debug build auto-joins (see InvisibilityPotion/Dev/AutoJoin.cs). Empty world = normal menu.
# Written to BepInEx/config/InvisibilityPotion.autojoin because env vars do not reach a game started through Steam.
IP_DEV_WORLD ?= testing
IP_DEV_CHARACTER ?=
AUTOJOIN_FILE := $(VALHEIM_INSTALL)/BepInEx/config/InvisibilityPotion.autojoin
BEPINEX_LOG := $(VALHEIM_INSTALL)/BepInEx/LogOutput.log

run: build ## Build, launch Valheim through Steam (needs launch option "./start_game_bepinex.sh %command%"), then follow the BepInEx log
	@pgrep -x steam > /dev/null || { echo "Steam is not running. Start Steam first."; exit 1; }
	@mkdir -p "$(dir $(AUTOJOIN_FILE))"
	@printf 'world=%s\ncharacter=%s\n' "$(IP_DEV_WORLD)" "$(IP_DEV_CHARACTER)" > "$(AUTOJOIN_FILE)"
	@: > "$(BEPINEX_LOG)"
	steam -applaunch $(STEAM_APPID) $(GAME_ARGS)
	@echo "Game starting via Steam. Following $(BEPINEX_LOG) (Ctrl-C stops following, not the game)."
	@tail -n +1 -f "$(BEPINEX_LOG)"

log: ## Follow the BepInEx log
	tail -n 50 -f "$(BEPINEX_LOG)"
