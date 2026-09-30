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
