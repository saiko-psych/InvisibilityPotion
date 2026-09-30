# ADR 0002 – Veil visuals

Status: accepted · 2026-09-30

## Context

`Custom/Player` only exposes `_Cutoff` (Valheim 1.0.7 shader list), so translucency via a material parameter is not possible.

## Options

| | How | Cost | Risk |
|---|---|---|---|
| V1 fog | hide renderers partly/fully + particle fog (e.g. `Custom/LitParticles`) | low | low |
| V2 shimmer | swap materials to `Custom/Distortion` while hidden | medium | look on skinned meshes unknown |
| V3 custom shader | own shader via asset bundle | high | Unity version match; material conflicts with other mods |

## Decision

V1 fog for tier I and II. V2 shimmer for tier III, prototyped early; dense fog is the fallback if shimmer looks wrong on skinned meshes. V3 only if V1/V2 are not good enough. Decided 2026-09-30.

## Consequences

The visual controller hides the chosen option behind one interface so it can be swapped.

## References

- <https://valheim-modding.github.io/Jotunn/data/prefabs/shader-list.html>
- <https://github.com/Vassteel/ValheimHelmsman/issues/1>
