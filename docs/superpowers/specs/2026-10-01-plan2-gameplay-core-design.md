# Plan 2 – Gameplay Core: Design Amendment

Date: 2026-10-01 · Status: approved by the author in conversation, ready for planning
Amends [`2026-09-30-invisibility-potion-design.md`](2026-09-30-invisibility-potion-design.md) (the main spec) for build steps 3 to 6. Every change below follows from [`docs/decompile-notes.md`](../../decompile-notes.md) ("Findings that affect the design", items 1 to 11 and 14) and from decisions taken on 2026-10-01. Where this document and the main spec disagree, this document wins for the components it covers. Main-spec decisions it does not mention stay in force.

## 1. Goal of plan 2

After plan 2, in singleplayer: a placeholder potion of each tier can be drunk (or applied with a dev command); enemies perceive the player less (tier I) or not at all (tier II/III); attacking reveals the player and applies the stamina debuff; tier II/III re-hide after the configured delay; the player looks veiled in fog; every tier value is reloadable from the config file without restarting the game. Out of scope: tier III hidden-from-players and the dedicated server (plan 3), custom bottle meshes and the shimmer veil (plan 4).

## 2. Decisions taken on 2026-10-01

| Topic | Decision |
|---|---|
| Aggro loss | No immediate target drop. A postfix on `MonsterAI.UpdateTarget` drops a hidden target once it has been unsensed for `AggroLossTime`. The vanilla "walk to last known position and search" phase therefore lasts `AggroLossTime`. |
| Tier I standing | Tier I also reduces visibility while standing (vanilla only applies stealth while crouching). |
| Reveal on harvesting | Harvesting, swinging at air, doors, chests never reveal. A melee/projectile/throw reveals only when it damages a `Character`. |
| Reveal on blocking | Only a blocked hit or parry reveals (vanilla `BlockAttack`), not raising the shield. |
| Sleeping monsters | Tier II/III do not wake sleeping monsters; tier I does (vanilla). |
| Hot reload | Plan 2 starts with a time-boxed spike on BepInEx ScriptEngine for the patch assembly. Its outcome decides the final assembly split. |
| Dev commands | `ip_give`, `ip_spawn`, `ip_reload_config`, extended `ip_state`; all also log to `Plugin.Log`. |

## 3. Perception and aggro (replaces main spec §5.5)

All hooks read `HiddenState.Get(Character)` (main spec §5.4) and never the local status effect, so they behave the same on every peer.

| Purpose | Hook | Behaviour |
|---|---|---|
| Ignore hidden players (tier ≥ 2) | Prefix on static `BaseAI.CanHearTarget(Transform me, float hearRange, Character target)` and static `BaseAI.CanSeeTarget(Transform me, Vector3 eyePoint, float viewRange, float viewAngle, bool alerted, bool mistVision, Character target)` | If `target` is a `Player` whose state is hidden with `IgnoredByEnemies`: `__result = false`, skip original. Covers target search, running pursuit (`MonsterAI.UpdateTarget` calls these directly) and turrets. |
| Raid and hunting monsters | Postfix on `BaseAI.FindEnemy()` | If the result is a hidden tier ≥ 2 player, replace it with the closest non-hidden enemy using the same vanilla search, or `null`. |
| Sleeping monsters | Prefix on `MonsterAI.UpdateSleep(float dt)` | If the monster is asleep and the only players in wake range are hidden tier ≥ 2, skip the wake-up check for this tick (exact logic copied from the decompile in the plan). |
| Lose target after `AggroLossTime` | Postfix on `MonsterAI.UpdateTarget(Humanoid humanoid, float dt, out bool canHearTarget, out bool canSeeTarget)` *(signature confirmed in the plan)* | If `m_targetCreature` is a hidden player and `m_timeSinceSensedTargetCreature > tier.AggroLossTime`, perform the same drop vanilla does at 30 s: clear `m_targetCreature`, `m_targetStatic`, call `SetAlerted(false)`. Runs on the monster's owner. |
| Tier I visibility | `SE_Invisibility` sets `m_stealthModifier = StealthModifier - 1` and `m_noiseModifier = NoiseModifier - 1` (the vanilla formula is additive: `stealth += base * modifier`). Postfix on `Player.UpdateStealth(float dt)`: while hidden at tier I and not crouching, set `m_stealthFactorTarget = tier.StealthModifier`. | Config `StealthModifier = 0.25` means "25 % of normal visibility" exactly as the main spec table reads. Reaches monster owners through the vanilla `s_stealth` ZDO value. |

`HiddenState` caches per `ZDOID` for 0.2 s because `CanSeeTarget`/`CanHearTarget` run 20 times per second per owned monster.

Startup patch health check: all of the above are added to `Plugin.ExpectedPatchTargets`. Overloaded names are listed with their declaring type as `Type.Method`; the plan adds the parameter count to the key if the check cannot distinguish overloads.

## 4. Reveal triggers (replaces main spec §5.6)

All triggers run on the hidden player's own client and call `SE_Invisibility.MarkRevealed(RevealReason)`; nothing else happens inside the hook.

| Trigger | Hook | Config gate |
|---|---|---|
| Damaging a creature (melee, arrow, bolt, spell, throw) | Prefix on `Character.Damage(HitData hit)`: `hit.GetAttacker()` is the local player and the instance is a `Character` (not a destructible) | always |
| Drawing a bow or crossbow | Postfix on `Attack.StartDraw(Humanoid character, ItemDrop.ItemData weapon)` when it returns `true` | `RevealOnBowDraw` |
| Starting a staff cast | Postfix on `Humanoid.StartAttack(Character target, bool secondaryAttack)` when it returns `true` and the current weapon's skill is `ElementalMagic` or `BloodMagic` | always |
| Blocked hit or parry | Postfix on `Humanoid.BlockAttack(HitData hit, Character attacker)` when it returns `true` | `RevealOnBlock` |
| Taking damage | Override `StatusEffect.OnDamaged(HitData hit, Character attacker)` in `SE_Invisibility`; sets the pending flag only (it runs inside a `foreach` over the effect list, adding or removing effects there throws) | `RevealOnDamage` |

Not hooked: swings that hit nothing or hit trees, rocks, pieces; doors, chests, pickups, harvesting.

## 5. Status effect lifecycle (replaces main spec §5.3)

**Pure state machine** `InvisibilityStateMachine` in `Effects/InvisibilityStateMachine.Core.cs` (no game types, unit-tested):

```
enum Phase { Hidden, Revealed, Ended }
record TierRules(int Tier, float Duration, float RehideDelay, bool EndsOnReveal)
Phase Phase; float Elapsed; float RehideTimer; bool PendingReveal
void MarkRevealed()                       // sets PendingReveal
StepResult Tick(float dt)                 // applies pending reveal, counts timers, returns what the game side must do
```

`StepResult` carries flags the game side acts on: `ApplyDebuff`, `EnterHidden`, `EnterRevealed`, `End`.

**`SE_Invisibility : SE_Stats`**, three registered instances `SE_Invisibility_T1/T2/T3`, each with its `TierConfig`. No shared `m_category` (otherwise vanilla refuses upgrades). Responsibilities:

| Event | Behaviour |
|---|---|
| `Setup(Character)` | create the state machine from the tier config; set tier I modifiers (§3); write ZDO `IP_Tier`, `IP_Hidden = true`; apply the veil; log. |
| `MarkRevealed(reason)` | forward to the state machine; log the reason. |
| `OnDamaged(hit, attacker)` | `MarkRevealed(Damage)` if `RevealOnDamage`; nothing else. |
| `UpdateStatusEffect(dt)` | `Tick(dt)`; on `ApplyDebuff` add or reset `SE_Revealed` (safe here: adds are allowed inside `SEMan.Update`); on `EnterRevealed` write `IP_Hidden = false`, remove the veil; on `EnterHidden` write `IP_Hidden = true`, apply the veil; on `End` set `m_time = m_ttl`. |
| `Stop()` | the single cleanup: ZDO `IP_Tier = 0`, `IP_Hidden = false`, veil removed, caches cleared. Idempotent. Called by vanilla on expiry, death, removal, replacement. |
| `OnDestroy()` | calls the same cleanup (vanilla does not call `Stop()` on logout). |

**Drinking:** postfix on `Player.CanConsumeItem(ItemDrop.ItemData item)`: if the item's consume effect is a tier lower than or equal to the active tier, set `__result = false` and show the HUD message `$ip_msg_lower_tier`. A higher tier is allowed; `Setup` of the new effect removes the old one first (its `Stop()` runs). Vanilla consumes the item only when `CanConsumeItem` returned true, so a refused potion stays in the inventory.

**`SE_Revealed : SE_Stats`**: `m_staminaRegenMultiplier = DebuffStaminaRegenMultiplier`, `m_ttl = DebuffDuration`; re-application resets the timer. Removed by vanilla on expiry.

## 6. Config and reload (extends main spec §5.2)

- Keys as in the main spec §5.2. `StealthModifier` and `NoiseModifier` are documented as "fraction of normal visibility / noise, 1 = vanilla" and converted to the additive vanilla modifier internally (`value - 1`), with a unit test.
- `ip_reload_config` (Debug) calls `Config.Reload()` and re-reads every tier object. Active effects pick up `RehideDelay`, `AggroLossTime`, `StealthModifier`, `NoiseModifier` and the debuff values on their next tick. `Duration` applies only to effects applied after the reload; the command help says so.
- A pure `TierConfig` record is built from the bound entries so the state machine and tests never touch BepInEx types.

## 7. Dev tooling (extends main spec §5.10, Debug builds only)

| Command | Does |
|---|---|
| `ip_give <1|2|3>` | applies the tier effect to the local player without an item |
| `ip_spawn <prefab> [count] [level]` | enables `devcommands` if needed and spawns in front of the player |
| `ip_reload_config` | see §6 |
| `ip_state` | own state: tier, phase, elapsed, rehide timer, pending reveal, ZDO values; for every `MonsterAI` within 30 m: prefab, target name, `m_timeSinceSensedTargetCreature`, alerted, ZDO owner (local or peer id); patch health as today |

Every command writes its lines to the console and to `Plugin.Log`.

## 8. Hot-reload spike (first task of plan 2)

Question: can BepInEx ScriptEngine reload a separate assembly that contains only our Harmony patch classes while the game runs, without breaking the content registered by the main plugin?

Probe: install ScriptEngine into the game folder; move one trivial patch (for example the `FejdStartup.Start` auto-join postfix or a throwaway log patch) into `InvisibilityPotion.Patches.dll` loaded from `BepInEx/scripts/`; press F6 after a rebuild; verify the old patch is removed (`UnpatchSelf` in `OnDestroy`) and the new one applied, three times in a row, with no duplicate patches reported by `ip_state`. Time box: one task. Output: a recommendation in `docs/decisions/0005-hot-reload.md`. If it works, plan 2 keeps two assemblies (content + reloadable patches, both committed) and `make run` deploys both; if not, everything stays in one assembly and the spike code is deleted.

## 9. Placeholder items (as main spec §5.9, placeholder phase only)

Six clones of `MeadHealthMinor`: `MeadBaseInvisibility_T1/T2/T3` (cauldron, recipe from config) and `MeadInvisibility_T1/T2/T3` (fermenter output, `m_consumeStatusEffect` = tier effect, `m_itemType = Consumable`). Materials tinted green, blue, violet. Icons from `RenderManager`. Registered in `PrefabManager.OnVanillaPrefabsAvailable`. English names and descriptions through Jötunn localization.

## 10. Visuals, fog only (narrows main spec §5.8)

`IVeil` interface and `VeilController` as in the main spec. Plan 2 ships `FogVeil` for all tiers (light for I, dense for II and III); `ShimmerVeil` is plan 4. Renderer snapshot covers `GetComponentsInChildren<Renderer>(true)` of the player; `_Cutoff` raised on `Custom/Player` materials. The fog particle is a vanilla particle effect reused as a placeholder (the plan picks one from the prefab list, for example the Wisp or ghost smoke effect) until plan 4 ships an asset bundle. Re-apply on equipment change: postfix on `VisEquipment.UpdateLodgroup()` (called only when equipment changes). Remote players: a per-frame scan of visible players reading `HiddenState` drives the same controller. The local player always keeps a faint self-view.

## 11. Testing

- Unit tests (xunit): state machine transitions for all three tiers (reveal in tier I ends; reveal in II/III enters Revealed and re-hides after `RehideDelay`; repeated reveals restart the timer; duration keeps running while revealed; pending reveal processed exactly once), modifier conversion, recipe parsing, `TierConfig` validation.
- `docs/testing.md` gains a plan-2 checklist: Greydwarf group loses aggro within `AggroLossTime`; a chasing Troll searches then idles; sleeping Draugr stay asleep at tier II, wake at tier I; bow draw, blocked hit, damage taken, and a hit on a Greydwarf each reveal; chopping a tree does not; `ip_reload_config` changes `RehideDelay` without restart; cleanup after expiry, death, logout, tier upgrade leaves normal visuals and `IP_Tier = 0`.

## 12. Task order for the plan

1. Hot-reload spike (§8) and ADR 0005.
2. Config binding, `TierConfig`, state machine with tests, `ip_reload_config`.
3. `SE_Invisibility`/`SE_Revealed` registration, `HiddenState` ZDO helper, `ip_give`, extended `ip_state`.
4. Perception prefixes and `FindEnemy` postfix (tier II/III), patch health entries.
5. `UpdateTarget` aggro postfix and `UpdateSleep` prefix.
6. Tier I modifiers and `UpdateStealth` postfix.
7. Reveal triggers and debuff.
8. Re-hide (already in the state machine; in-game verification and tuning).
9. `FogVeil` and cleanup stress test.
10. Placeholder items, recipes, localization, `ip_spawn`.
11. Docs, checklist, backlog items (patch-health key parameter count, Makefile path duplication).
