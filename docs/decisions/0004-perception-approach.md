# ADR 0004 – Enemy perception approach

Status: accepted · 2026-09-30

## Context

Tier II/III must make enemies ignore the player entirely; tier I only reduces perception. Enemy AI runs on the zone owner's machine, so the hidden state must be visible to every peer.

## Options

**A) Own Harmony patches + tier flag in the player ZDO** – prefix on `BaseAI.CanSenseTarget` for tier II/III (pattern used by GreydwarfCloak and Valheim Legends), vanilla stealth/noise modifiers for tier I, aggro-loss threshold shortened per tier. State replicated through the player's ZDO.

**B) Reuse vanilla ghost mode** – `Player.SetGhostMode(true)` during the effect, synced through a ZDO key like Server Devcommands does. Binary, unclear side effects (spawns, events), conflicts with admin tools.

**C) Vanilla stealth values only** – extreme `m_stealthModifier` / `m_noiseModifier` like Enhanced Potions. Update-safe but cannot reach "ignored" and gives no fast aggro loss.

## Decision

A. Decided 2026-09-30.

## Consequences

- Three to four Harmony patches that can break on game updates; the plugin verifies at startup that every patch applied and logs missing ones.
- Hidden state lives in the player ZDO (`IP_Tier`, `IP_Hidden`); all patches read it through one helper.
- Tier III "hidden from players" reuses the Server Devcommands technique (Unlicense): spoof position in outgoing ZDO packets, disable public reference position, hide nameplate.

## References

- <https://github.com/JereKuusela/valheim-dev> (Unlicense)
- <https://thunderstore.io/c/valheim/p/sephalon/GreydwarfCloak/> (no licence, approach only)
- <https://github.com/TorannD/ValheimLegends> (no licence, approach only)
