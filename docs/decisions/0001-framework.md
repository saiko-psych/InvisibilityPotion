# ADR 0001 – Modding framework

Status: accepted · 2026-09-30

## Context

We need items (incl. a custom bottle from an asset bundle), status effects, config and server sync. Harmony patches for the AI are needed either way.

## Options

**A) Jötunn** – `CustomItem`, `CustomStatusEffect`, asset loading, localisation, network version check. Extensive docs and an example mod. Used by Enhanced Potions and PotionsPlus. Players need Jötunn installed (mod managers handle this).

**B) blaxxun ItemManager + ServerSync (+ AzuMatt StatusEffectManager)** – items with auto-generated, server-synced config; asset bundle embedded in the DLL, libraries merged via ILRepack. No extra dependency for players; more build setup, less unified docs.

## Decision

A) Jötunn 2.30.2, for the documentation, the JotunnModStub template (MIT-0, builds on Linux with `dotnet build`) and the example project. Decided 2026-09-30.

## Consequences

- `Jotunn` becomes a dependency.
- Verify early that Jötunn's config sync covers all tier values.

## References

- <https://valheim-modding.github.io/Jotunn/tutorials/status-effects.html>
- <https://github.com/Valheim-Modding/JotunnModExample>
- <https://github.com/blaxxun-boop/ItemManager>
- <https://github.com/AzumattDev/StatusEffectManagerModTemplate>
