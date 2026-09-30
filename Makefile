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
# World and character the Debug build auto-joins (see InvisibilityPotion/Dev/AutoJoin.cs). Empty = normal menu.
IP_DEV_WORLD ?= testing
IP_DEV_CHARACTER ?=

run: build ## Build, then start Valheim with BepInEx, console enabled, windowed. Steam must be running.
	@pgrep -x steam > /dev/null || { echo "Steam is not running. Start Steam first."; exit 1; }
	cd "$(VALHEIM_INSTALL)" && IP_DEV_WORLD="$(IP_DEV_WORLD)" IP_DEV_CHARACTER="$(IP_DEV_CHARACTER)" ./start_game_bepinex.sh $(GAME_ARGS)

log: ## Follow the BepInEx log
	tail -n 50 -f "$(VALHEIM_INSTALL)/BepInEx/LogOutput.log"
