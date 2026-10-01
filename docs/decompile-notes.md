# Decompile notes – Valheim 1.0.16 (build 25527674)

Source: `make decompile` output in `tools/decompiled/assembly_valheim/` (ilspycmd 9.1.0.7988, game version `l-1.0.16`). One exception: `StringExtensionMethods` lives in `assembly_utils.dll`, decompiled ad hoc with `ilspycmd -t StringExtensionMethods assembly_utils.dll` (not part of `make decompile`). Rerun after every game update and diff this file.

Conventions: every entry gives the access modifier, the exact signature and `File.cs:line` relative to `tools/decompiled/assembly_valheim/`. "Runs on" names the machine where the code executes in multiplayer. Quotes are verbatim from the decompile.

Tip for future greps: `tools/decompiled/` is gitignored, and the shell's `grep` wrapper in this environment honours `.gitignore`. Use `command grep` to search the decompile.

## Perception

**Summary:** the spec's single prefix on `BaseAI.CanSenseTarget(Character)` is not enough. `MonsterAI.UpdateTarget` (the method that keeps an existing target alive) calls `CanHearTarget` and `CanSeeTarget` directly, and `FindClosestCreature` calls the static `CanSenseTarget` overload. The game's own "ignore this player" check (ghost mode) sits inside the two **static** methods `CanHearTarget(Transform, float, Character)` and `CanSeeTarget(Transform, Vector3, float, float, bool, bool, Character)`. Every instance overload ends up in one of them. Patch those two, as the game does.

- `BaseAI.CanSenseTarget(Character target)` – `public bool`, BaseAI.cs:717. Only forwards to the two-argument overload with `m_passiveAggresive`. Callers: `FindEnemy` (BaseAI.cs:1407), `AnimalAI.UpdateAI` (AnimalAI.cs:57). **Not** called by `MonsterAI.UpdateTarget`.
- `BaseAI.CanSenseTarget(Character target, bool passiveAggresive)` – `public bool`, BaseAI.cs:722. Forwards to the static overload.
- `BaseAI.CanSenseTarget(Transform me, Vector3 eyePoint, float hearRange, float viewRange, float viewAngle, bool alerted, bool mistVision, Character target, bool passiveAggresive, bool isTamed)` – `public static bool`, BaseAI.cs:727. Returns false under the PassiveMobs global key, else `CanHearTarget(static) || CanSeeTarget(static)`. Also called directly by `FindClosestCreature` (BaseAI.cs:1470, used by `Turret.cs:306`).
- `BaseAI.CanHearTarget(Character target)` – `public bool`, BaseAI.cs:744. Forwards to the static overload. Called directly by `MonsterAI.UpdateTarget` (MonsterAI.cs:313) and `NpcTalk.cs:140`.
- `BaseAI.CanHearTarget(Transform me, float hearRange, Character target)` – `public static bool`, BaseAI.cs:749. For a `Player` target it first returns false in debug-fly or ghost mode, then compares distance with the target's noise range:
  ```csharp
  if (player.InDebugFlyMode() || player.InGhostMode())   // BaseAI.cs:754
  ...
  if (num < target.GetNoiseRange())                       // BaseAI.cs:768
  ```
- `BaseAI.CanSeeTarget(Character target)` – `public bool`, BaseAI.cs:775. Forwards to the static overload. Called directly by `MonsterAI.UpdateTarget` (MonsterAI.cs:314), `NpcTalk.cs:139`, `Character.RPC_Damage` backstab check (Character.cs:2336).
- `BaseAI.CanSeeTarget(Transform me, Vector3 eyePoint, float viewRange, float viewAngle, bool alerted, bool mistVision, Character target)` – `public static bool`, BaseAI.cs:780. Same ghost/debug-fly check at BaseAI.cs:789, then the stealth-scaled view range:
  ```csharp
  float stealthFactor = target.GetStealthFactor();
  float num2 = viewRange * stealthFactor;               // BaseAI.cs:801
  if (num > num2) { return false; }
  ```
  Then view angle (skipped when alerted), a raycast against `m_viewBlockMask`, and mist.
- `BaseAI.CanSeeTarget(StaticTarget target)` – `protected bool`, BaseAI.cs:823. Buildings only; irrelevant.
- `BaseAI.FindEnemy()` – `protected Character`, BaseAI.cs:1395, non-virtual. Iterates `Character.GetAllCharacters()`, skips non-enemies, dead targets and `item.m_aiSkipTarget` (BaseAI.cs:1402), keeps the closest one where `CanSenseTarget(item)` is true. **Fallback for hunting monsters ignores perception completely:**
  ```csharp
  if (character == null && HuntPlayer())
  {
      Player closestPlayer = Player.GetClosestPlayer(base.transform.position, 200f);   // BaseAI.cs:1419
      if ((bool)closestPlayer && (closestPlayer.InDebugFlyMode() || closestPlayer.InGhostMode()))
          return null;
      return closestPlayer;
  ```
  Raid and event creatures (`HuntPlayer()` true) therefore find a hidden player even when both static checks say "not sensed". This needs its own patch (postfix on `FindEnemy`: if `__result` is a hidden player, return null).
- `BaseAI.FindClosestCreature(Transform me, Vector3 eyePoint, float hearRange, float viewRange, float viewAngle, bool alerted, bool mistVision, bool passiveAggresive, bool includePlayers = true, bool includeTamed = true, bool includeEnemies = true, List<Character> onlyTargets = null)` – `public static Character`, BaseAI.cs:1429. Used by turrets. Covered once the static checks are patched.
- `BaseAI.IsEnemy(Character other)` – `public bool`, BaseAI.cs:1188; `BaseAI.IsEnemy(Character a, Character b)` – `public static bool`, BaseAI.cs:1193. Faction/group/tame/aggravated logic only. Does **not** consult ghost mode or stealth. Do not patch it for invisibility: `UpdateTarget` drops a non-enemy target immediately (MonsterAI.cs:300), and the result is also used for HUD colouring and PvP-like logic.
- `BaseAI.SetAlerted(bool alert)` – `protected virtual void`, BaseAI.cs:1554. Writes `ZDOVars.s_alert` only on the owner (BaseAI.cs:1562). `MonsterAI.SetAlerted(bool alert)` – `protected override void`, MonsterAI.cs:919, resets `m_timeSinceSensedTargetCreature = 0f` when `alert` is true, then calls base.
- `BaseAI.IsAlerted()` – `public bool`, BaseAI.cs:1652, returns `m_alerted` (`private bool`, BaseAI.cs:167). On non-owners `m_alerted` is overwritten from the ZDO on every `UpdateAI` call (BaseAI.cs:313).
- `BaseAI.Alert()` – `public void`, BaseAI.cs:1531. Owner sets alerted, non-owner sends the `"Alert"` RPC to the owner.
- `Character.m_aiSkipTarget` – `public bool`, Character.cs:109. A vanilla per-character "AI should not pick me" flag, but only read in `FindEnemy` and it is a local field (not in the ZDO), so it does not work across machines. Not usable as the main mechanism.

**Recommended perception hooks (replaces the spec's `CanSenseTarget` prefix):**
1. Prefix on the static `BaseAI.CanHearTarget(Transform, float, Character)`: if `target` is a hidden player with `IgnoredByEnemies`, set `__result = false` and skip.
2. Prefix on the static `BaseAI.CanSeeTarget(Transform, Vector3, float, float, bool, bool, Character)`: same.
3. Postfix on `BaseAI.FindEnemy()`: if `__result` is a hidden player, set `__result = null` (covers `HuntPlayer`).

Harmony needs explicit argument types for `CanHearTarget` and `CanSeeTarget` because both names are overloaded (`FindEnemy()` has a single overload and needs none). Example for the 7-parameter static overload at BaseAI.cs:780: `[HarmonyPatch(typeof(BaseAI), nameof(BaseAI.CanSeeTarget), new[] { typeof(Transform), typeof(Vector3), typeof(float), typeof(float), typeof(bool), typeof(bool), typeof(Character) })]`; for CanHearTarget (BaseAI.cs:749): `new[] { typeof(Transform), typeof(float), typeof(Character) }`. Patching the static bodies also avoids any JIT-inlining doubt about the one-line instance wrappers.

Runs on: the ZDO owner of the monster only. `BaseAI.UpdateAI` returns early for non-owners:
```csharp
if (!m_nview.IsOwner())                                    // BaseAI.cs:311
{
    m_alerted = m_nview.GetZDO().GetBool(ZDOVars.s_alert);
    return false;
}
```
`MonsterAI.UpdateAI` (MonsterAI.cs:347) and `AnimalAI.UpdateAI` (AnimalAI.cs:31) return early when `base.UpdateAI` returns false. `UpdateAI` is driven by `MonoUpdaters` (MonoUpdaters.cs:47) for every `BaseAI` instance.

## Aggro loss

- `MonsterAI.UpdateTarget(Humanoid humanoid, float dt, out bool canHearTarget, out bool canSeeTarget)` – `private void`, MonsterAI.cs:233. Confirmed as the method that compares `m_timeSinceSensedTargetCreature`. Every 2 s (6 s when no player within 50 m) it calls `FindEnemy()` and replaces the target if one is found. Then:
  ```csharp
  canHearTarget = CanHearTarget(m_targetCreature);         // MonsterAI.cs:313
  canSeeTarget = CanSeeTarget(m_targetCreature);
  if (canSeeTarget | canHearTarget)
  {
      m_timeSinceSensedTargetCreature = 0f;
  }
  ...
  m_timeSinceSensedTargetCreature += dt;                   // MonsterAI.cs:329
  ...
  if (m_timeSinceSensedTargetCreature > 30f || (!flag5 && (m_timeSinceAttacking > num || (m_maxChaseDistance > 0f && m_timeSinceSensedTargetCreature > 1f && num2 > m_maxChaseDistance))))   // MonsterAI.cs:336
  {
      SetAlerted(alert: false);
      m_targetCreature = null;
      m_targetStatic = null;
      m_timeSinceAttacking = 0f;
      m_updateTargetTimer = 5f;
  }
  ```
  The threshold is the literal `30f`. The constant `private const float m_giveUpTime = 30f;` (MonsterAI.cs:13) exists but the compiler inlined it, so changing the constant is impossible; a transpiler would have to find `ldc.r4 30`. Simpler and robust: **postfix on `UpdateTarget`**: if `m_targetCreature` is a hidden player and `m_timeSinceSensedTargetCreature > AggroLossTime`, run the same four assignments plus `SetAlerted(false)`. Because the static perception prefixes keep `canSee`/`canHear` false, the timer grows from the moment the player becomes hidden.
- Fields (all on `MonsterAI`, all `private`, accessible through Jötunn's publicized assembly or `AccessTools`):
  - `Character m_targetCreature` – MonsterAI.cs:114 (confirmed).
  - `Vector3 m_lastKnownTargetPos` – MonsterAI.cs:116; `bool m_beenAtLastPos` – MonsterAI.cs:118.
  - `StaticTarget m_targetStatic` – MonsterAI.cs:120.
  - `float m_timeSinceAttacking` – MonsterAI.cs:122.
  - `float m_timeSinceSensedTargetCreature` – MonsterAI.cs:124 (confirmed).
  - `float m_updateTargetTimer` – MonsterAI.cs:126.
  - `m_alerted` is **not** a `MonsterAI` field: it is `private bool m_alerted` on `BaseAI` (BaseAI.cs:167). Setting it directly skips the ZDO write and the animator bool; call `SetAlerted(false)` instead.
- `MonsterAI.GetTargetCreature()` – `public override Character`, MonsterAI.cs:807 (base `public virtual Character GetTargetCreature()` BaseAI.cs:1766). Only meaningful on the owner: `m_targetCreature` is never synced. Non-owners only see `ZDOVars.s_haveTargetHash` via `BaseAI.HaveTarget()` (written by `SetTargetInfo`, BaseAI.cs:1657).
- "Walk to the last known position and search": this vanilla behaviour only happens **while the target is still set but not sensed**. In `MonsterAI.UpdateAI`:
  ```csharp
  if (canHearTarget || canSeeTarget || (HuntPlayer() && m_targetCreature.IsPlayer()))   // MonsterAI.cs:524
  { ... m_lastKnownTargetPos = m_targetCreature.transform.position; ... }
  else
  {
      ...
      if (m_beenAtLastPos) { RandomMovement(dt, m_lastKnownTargetPos); ... }          // MonsterAI.cs:577
      else if (MoveTo(dt, m_lastKnownTargetPos, 0f, IsAlerted())) { m_beenAtLastPos = true; }
  ```
  Once `m_targetCreature` is null the monster falls through to `IdleMovement(dt)` (MonsterAI.cs:490) or follow. So the search phase lasts exactly `AggroLossTime` seconds. An immediate `m_targetCreature = null` on entering Hidden skips it entirely (see "Findings that affect the design").
- The `HuntPlayer()` branch at MonsterAI.cs:524 keeps updating `m_lastKnownTargetPos` from the real position even when not sensed. With the `FindEnemy` postfix and the `UpdateTarget` postfix the target is dropped after `AggroLossTime`, so this is bounded.
- Re-acquisition paths that bypass perception: `MonsterAI.OnDamaged(float damage, Character attacker)` – `protected override void`, MonsterAI.cs:184 → `SetTarget(attacker)` (`private void`, MonsterAI.cs:192); `MonsterAI.RPC_OnNearProjectileHit` (MonsterAI.cs:203) → `SetTarget`. Both are triggered by the hidden player attacking, which reveals anyway.
- `MonsterAI.UpdateSleep(float dt)` – `private void`, MonsterAI.cs:817. Wakes sleeping monsters on `Player.GetClosestPlayer(..., m_wakeupRange)` or `Player.GetPlayerNoiseRange`, skipping ghost/debug-fly players only (MonsterAI.cs:823, 852). It does not use the perception methods, so a hidden player still wakes sleeping monsters by proximity. See "Open questions".

Runs on: monster ZDO owner (all of the above is under `UpdateAI`).

## Stealth math

- `Character.GetStealthFactor()` – `public virtual float`, Character.cs:3792, returns `1f`.
- `Player.GetStealthFactor()` – `public override float`, Player.cs:6977. Owner returns `m_stealthFactor`; non-owners read `ZDOVars.s_stealth` from the ZDO. So stealth is synced and works on remote zone owners.
- `Player.UpdateStealth(float dt)` – `private void`, Player.cs:6947, called from `Player.FixedUpdate` on the owner (Player.cs:844). **Status-effect stealth modifiers only apply while crouching:**
  ```csharp
  if (IsCrouching())
  {
      ...
      float lightFactor = StealthSystem.instance.GetLightFactor(GetCenterPoint());
      m_stealthFactorTarget = Mathf.Lerp(0.5f + lightFactor * 0.5f, 0.2f + lightFactor * 0.4f, skillFactor);
      m_stealthFactorTarget = Mathf.Clamp01(m_stealthFactorTarget);
      m_seman.ModifyStealth(m_stealthFactorTarget, ref m_stealthFactorTarget);   // Player.cs:6961
      m_stealthFactorTarget = Mathf.Clamp01(m_stealthFactorTarget);
  }
  else
  {
      m_stealthFactorTarget = 1f;                                               // Player.cs:6966
  }
  ...
  float num = Mathf.MoveTowards(m_stealthFactor, m_stealthFactorTarget, dt / 4f);   // Player.cs:6969
  ```
  The target is recomputed every 0.5 s, and the actual factor moves at 0.25 per second, so going from 1.0 to 0.25 takes 3 s. The value is written to `ZDOVars.s_stealth` whenever it changes.
- How the factor is used: `CanSeeTarget` multiplies `viewRange` by it (BaseAI.cs:801); `MonsterAI.UpdateAI` multiplies `m_alertRange` by it (`float num2 = m_alertRange * m_targetCreature.GetStealthFactor();`, MonsterAI.cs:529). Lower is stealthier.
- `Character.GetNoiseRange()` – `public float`, Character.cs:3830. Owner returns `m_noiseRange`, non-owners read `ZDOVars.s_noise`. `Character.UpdateNoise(float dt)` – `private void`, Character.cs:3797, decays by 4 per second and syncs every 0.5 s. `Character.AddNoise(float range)` – `public void`, Character.cs:3808, routes to the owner; `Character.RPC_AddNoise(long sender, float range)` – `private void`, Character.cs:3821, applies the modifier on the owner:
  ```csharp
  m_noiseRange = range;
  m_seman.ModifyNoise(m_noiseRange, ref m_noiseRange);   // Character.cs:3826
  ```
  Noise modifiers apply whether crouching or not.
- `SE_Stats.ModifyStealth(float baseStealth, ref float stealth)` – `public override void`, SE_Stats.cs:345: `stealth += baseStealth * m_stealthModifier;`
- `SE_Stats.ModifyNoise(float baseNoise, ref float noise)` – `public override void`, SE_Stats.cs:340: `noise += baseNoise * m_noiseModifier;`
- Fields: `public float m_stealthModifier;` SE_Stats.cs:116, `public float m_noiseModifier;` SE_Stats.cs:114. Both default 0.
- **Mapping for tier I's "×0.25":** both modifiers are **additive fractions of the base value**, not multipliers. ×0.25 means `m_stealthModifier = 0.25 - 1 = -0.75` and `m_noiseModifier = -0.75`. Setting them to `0.25` (as the config value reads) would *increase* stealth factor and noise by 25 %, the opposite of the intent. The config value must be converted (`modifier = configValue - 1`), or the config key redefined. With several effects active the deltas add up (each uses the same `baseStealth`), and the result is clamped to [0, 1] for stealth.
- `SE_Stats.ModifyStaminaRegen(ref float staminaRegen)` – `public override void`, SE_Stats.cs:280:
  ```csharp
  if (m_staminaRegenMultiplier > 1f) { staminaRegen += m_staminaRegenMultiplier - 1f; }
  else { staminaRegen *= m_staminaRegenMultiplier; }
  ```
  Field `public float m_staminaRegenMultiplier = 1f;` SE_Stats.cs:80. Applied in `Player` stamina update: `m_seman.ModifyStaminaRegen(ref staminaMultiplier);` (Player.cs:2110), then `num2 *= staminaMultiplier;`. **The field alone gives the debuff**: a value ≤ 1 multiplies. 0.5 halves regeneration. No override needed. Runs on the owner.

## StealthSystem

`StealthSystem` (StealthSystem.cs, 83 lines) – `public class StealthSystem : MonoBehaviour`, singleton exposed through the static property `public static StealthSystem instance => m_instance;` (StealthSystem.cs:19), backed by `private static StealthSystem m_instance` set in `Awake`.

- `public float GetLightFactor(Vector3 point)` – StealthSystem.cs:31: `Utils.LerpStep(m_minLightLevel, m_maxLightLevel, GetLightLevel(point))` with defaults 0.2 and 1.6.
- `public float GetLightLevel(Vector3 point)` – StealthSystem.cs:37: ambient intensity plus every `Light` in the scene (list refreshed once per second), reduced by shadow raycasts.
- The only caller in the whole assembly is `Player.UpdateStealth` (Player.cs:6958), inside the crouching branch.

Finding: `StealthSystem` is a light-level helper for the player's own sneak calculation, not an AI-side perception system. It does not see targets, does not run on monsters and is not consulted by `BaseAI`. It changes nothing about where perception is hooked. Its only relevance: tier I stealth through `m_stealthModifier` depends on light *and* on crouching, because both live in the same crouch-only branch.

## Reveal triggers

- `Humanoid.StartAttack(Character target, bool secondaryAttack)` – `public override bool`, Humanoid.cs:280 (base `public virtual bool StartAttack(Character target, bool charge)` Character.cs:3373 returns false; `Player` does not override it, so patch `typeof(Humanoid)`). Returns `true` only when `attack.Start(...)` succeeded and the attack became `m_currentAttack`:
  ```csharp
  if (attack.Start(this, m_body, m_zanim, m_animEvent, m_visEquipment, currentWeapon, m_previousAttack, m_timeSinceLastAttack, GetAttackDrawPercentage()))
  {
      ...
      m_lastCombatTimer = 0f;          // Humanoid.cs:312
      return true;
  }
  return false;
  ```
  Covers melee, bow release (`Player.UpdateAttackBowDraw` calls `StartAttack(null, false)` on release), crossbow shot, staff casts and throws (secondary attacks). Also called by monster AI, so the postfix must check `__instance == Player.m_localPlayer` (or `is Player` and owner). **Also fires for axe and pickaxe swings at trees, rocks and ore**: harvesting with a weapon-type tool goes through `StartAttack`. Hammer/hoe/cultivator do not (place mode returns early in `Player.PlayerAttackInput`: `if (InPlaceMode()) return;` at Player.cs:1807; the method signature is Player.cs:1805). See "Findings that affect the design".
  Runs on: the attacker's client (owner of the player). `Player.PlayerAttackInput` is called from `Player.FixedUpdate` (call at Player.cs:830) only after the owner guard `if (!m_nview.IsOwner()) { return; }` (Player.cs:817–820) and the `if (m_localPlayer != this)` check (Player.cs:821).
- `Humanoid.IsDrawingBow()` – `public override bool`, Humanoid.cs:1742 (base `public virtual bool IsDrawingBow()` Character.cs:3883). A query: `m_attackDrawTime > 0` and the current weapon has `m_attack.m_bowDraw`. Polled by many callers; not an event. On remote players `m_attackDrawTime` is local and stays 0, so it is false there.
- Where the draw starts: `Player.UpdateAttackBowDraw(ItemDrop.ItemData weapon, float dt)` – `private void`, Player.cs:1905, called from `PlayerAttackInput` when `currentWeapon.m_shared.m_attack.m_bowDraw` (Player.cs:1813). The start of a draw is:
  ```csharp
  if (m_attackDrawTime == 0f)
  {
      if (!weapon.m_shared.m_attack.StartDraw(this, weapon))   // Player.cs:1937
  ```
  `Attack.StartDraw(Humanoid character, ItemDrop.ItemData weapon)` – `public bool`, Attack.cs:346, returns false when there is no ammo. **Recommended hook for "drawing a bow": postfix on `Attack.StartDraw` when `__result` is true and `character == Player.m_localPlayer`.** Fires once per draw. `m_bowDraw` is `true` for bows (and any weapon whose attack is configured that way); crossbows do not draw, they load (`UpdateWeaponLoading`), and their shot is caught by `StartAttack`.
  Runs on: the attacker's client.
- `Humanoid.BlockAttack(HitData hit, Character attacker)` – `protected override bool`, Humanoid.cs:1751 (base `protected virtual bool` Character.cs:2704). Returns false if the hit comes from behind or there is no blocker, `true` at the end (Humanoid.cs:1875). A parry is the timed-block branch inside the same method:
  ```csharp
  bool flag = currentBlocker.m_shared.m_timedBlockBonus > 1f && m_blockTimer != -1f && m_blockTimer < 0.25f;   // Humanoid.cs:1762
  ```
  Called only from `Character.RPC_Damage` when a blockable hit arrives while `IsBlocking()` (Character.cs:2349). So it fires when a hit is actually blocked, **not** when the player merely raises the shield. If raising the shield should reveal, hook `Humanoid.UpdateBlock(float dt)` (`private void`, Humanoid.cs:1891) where `m_internalBlockingState = true;` is set (Humanoid.cs:1900), or poll `IsBlocking()` from the status effect's `UpdateStatusEffect`.
  Runs on: the victim's client (owner of the blocking player), inside `RPC_Damage`.
- `Character.Damage(HitData hit)` – `public void`, Character.cs:2232. Runs on the **attacker's** machine (wherever the attack/projectile hit is computed). It only sets the weak spot and calls `m_nview.InvokeRPC("RPC_Damage", hit)`, which `ZNetView.InvokeRPC(string, params object[])` (ZNetView.cs:331) routes to the victim's ZDO owner:
  ```csharp
  ZRoutedRpc.instance.InvokeRoutedRPC(m_zdo.GetOwner(), m_zdo.m_uid, method, parameters);   // ZNetView.cs:333
  ```
  When the target is local, `ZRoutedRpc.InvokeRoutedRPC` handles it synchronously (`HandleRoutedRPC`).
- `Character.RPC_Damage(long sender, HitData hit)` – `private void`, Character.cs:2241, registered at Character.cs:705. Runs on the victim's ZDO owner (for a player victim: the victim's own client). Guard:
  ```csharp
  if (!m_nview.IsOwner())        // Character.cs:2252
  {
      return;
  }
  ```
  It returns early for debug fly, dead, teleporting, cutscene, dodge invincibility and for PvP hits when the victim has PvP off. After those checks it calls `m_seman.OnDamaged(hit, attacker);` (Character.cs:2331), before the block check.
- **Recommended hook for "taking damage": no Harmony patch.** `StatusEffect.OnDamaged(HitData hit, Character attacker)` – `public virtual void`, StatusEffect.cs:346 – is called for every active effect by `SEMan.OnDamaged(HitData hit, Character attacker)` (`public void`, SEMan.cs:547) from `RPC_Damage`. `SE_Invisibility` overrides it. It runs on the victim's owner, only for hits that passed the early returns, and also for blocked hits (the call precedes `BlockAttack`). **Caveat:** `SEMan.OnDamaged` iterates the effect list with `foreach` (SEMan.cs:547–553), so the override must not add or remove status effects (applying `SE_Revealed` the first time is an add and throws). Inside the override only set fields: `m_time = m_ttl` for tier I, a pending-reveal flag for tier II/III. Apply `SE_Revealed` from the next `SE_Invisibility.UpdateStatusEffect` (an add inside `SEMan.Update`'s `for` loop is safe; a remove is not), or from a postfix on `SEMan.OnDamaged` / `Character.RPC_Damage` (after the `foreach` has finished; still no removes there, since `RPC_Damage` can run nested inside `SEMan.Update` through a damage tick).
- `HitData.GetAttacker()` – `public Character`, HitData.cs:1053. Resolves `public ZDOID m_attacker` (HitData.cs:700) via `ZNetScene.instance.FindInstance(m_attacker)` and `GetComponent<Character>()`. Returns null if the attacker's object is not instantiated on this machine. `HitData.HaveAttacker()` (HitData.cs:1048) tells "had an attacker" from "attacker unknown here".

## Consume path

- `Humanoid.UseItem(Inventory inventory, ItemDrop.ItemData item, bool fromInventoryGui)` – `public void`, Humanoid.cs:890. For `ItemType.Consumable` it calls `ConsumeItem(inventory, item, checkWorldLevel: true)` (Humanoid.cs:913) and plays the eat animation only if that returns true.
- `Humanoid.CanConsumeItem(ItemDrop.ItemData item, bool checkWorldLevel = false)` – `public virtual bool`, Humanoid.cs:1035 (type and world-level check).
- `Humanoid.ConsumeItem(Inventory inventory, ItemDrop.ItemData item, bool checkWorldLevel = false)` – `public virtual bool`, Humanoid.cs:1049 (base returns false).
- `Player.CanConsumeItem(ItemDrop.ItemData item, bool checkWorldLevel = false)` – `public override bool`, Player.cs:6026. The vanilla "already have effect" check, which refuses on the same effect **or the same category**:
  ```csharp
  if (m_seman.HaveStatusEffect(item.m_shared.m_consumeStatusEffect.NameHash()) || m_seman.HaveStatusEffectCategory(consumeStatusEffect.m_category))
  {
      Message(MessageHud.MessageType.Center, "$msg_cantconsume");   // Player.cs:6041
      return false;
  }
  ```
  `SEMan.HaveStatusEffectCategory(string cat)` (SEMan.cs:258) returns false for an empty category.
- `Player.ConsumeItem(Inventory inventory, ItemDrop.ItemData item, bool checkWorldLevel = false)` – `public override bool`, Player.cs:6048. Order:
  ```csharp
  if (!CanConsumeItem(item, checkWorldLevel)) { return false; }
  if ((bool)item.m_shared.m_consumeStatusEffect)
  {
      _ = item.m_shared.m_consumeStatusEffect;
      m_seman.AddStatusEffect(item.m_shared.m_consumeStatusEffect, resetTime: true, 0, 0f, -1);   // Player.cs:6057
  }
  if (item.m_shared.m_food > 0f) { EatFood(item); }
  inventory.RemoveOneItem(item);                                                              // Player.cs:6063
  return true;
  ```
  The return value of `AddStatusEffect` is ignored. **A refusal in `SE_Invisibility.Setup` (or `CanAdd`) cannot stop the item from being consumed.** The only refusal point that keeps the item is `CanConsumeItem` (or a prefix on `ConsumeItem` that returns false and skips the original).
- Other `CanConsumeItem` callers, all consume-intent: `Feast.cs:166`, `ItemDrop.cs:1539` and `ItemDrop.cs:1657` (eating a placed food piece), `Attack.cs:732` (consumable ammo). A postfix on `Player.CanConsumeItem` therefore does not misfire in UI-only contexts.
- `ItemDrop.ItemData.SharedData.m_consumeStatusEffect` – `public StatusEffect`, ItemDrop.cs:405.
- `ItemDrop.ItemData.SharedData.m_isDrink` – `public bool`, ItemDrop.cs:192. Its only reader is `ItemDrop.GetHoverText` (ItemDrop.cs:1496), and only for a consumable placed as a piece (`IsPiece()`): `$item_drink` vs `$item_eat`. Drinking from the inventory never reads it: `Humanoid.UseItem` always fires `m_consumeItemEffects` (the player's own consume sound, Humanoid.cs:915) and `m_zanim.SetTrigger("eat")` (Humanoid.cs:916) for every consumable, vanilla meads included. A veil mead therefore matches a vanilla mead once `m_isDrink = true`, `m_itemType = Consumable` and `m_consumeStatusEffect` are set.
- Timing (Jötunn 2.30.2, decompiled): `ItemManager.RegisterCustomStatusEffects(ObjectDB)` adds `CustomStatusEffect.StatusEffect` itself (`objectDB.m_StatusEffects.Add(statusEffect)`) and runs from the `ObjectDB.Awake` hook, after `PrefabManager.OnVanillaPrefabsAvailable`. Item code registered at that event must reference the status effect instances directly; `ObjectDB.instance.GetStatusEffect` returns null there.
- Crafting station: the Mead Ketill is `piece_MeadCauldron` (Jötunn `CraftingStations.MeadKetill`); `piece_cauldron` is the cooking cauldron.
- **Recommended consume hooks:** postfix on `Player.CanConsumeItem`: if the item's effect is an `SE_Invisibility` and an equal or higher tier is active, show the HUD message and set `__result = false`. Lower-tier removal can stay in `SE_Invisibility.Setup` as the spec says: `Setup` runs inside `SEMan.AddStatusEffect` after `CanConsumeItem` has passed and before `RemoveOneItem`, and `AddStatusEffect` is not iterating the list, so `RemoveStatusEffect(lowerHash)` there is safe. The three tier effects must use **different `m_category` values or an empty one**; a shared category makes vanilla refuse every upgrade with `$msg_cantconsume`.
  Runs on: the drinking player's client (owner). `SEMan.AddStatusEffect(StatusEffect, ...)` does not check ownership, but the call only happens on the local player.

## Status effect internals and sync

- `SEMan` storage: `private readonly List<StatusEffect> m_statusEffects` (SEMan.cs:8) plus `HashSet<int> m_statusEffectsHashSet` (SEMan.cs:6). Constructed per character: `m_seman = new SEMan(this, m_nview);` (Character.cs:693), exposed by `public SEMan GetSEMan()` (Character.cs:4356).
- `SEMan.AddStatusEffect(StatusEffect statusEffect, bool resetTime = false, int itemLevel = 0, float skillLevel = 0f, short variant = -1)` – `public StatusEffect`, SEMan.cs:184. If the same name hash is active: calls `ResetTime()` and `SetLevel` when `resetTime` is true, returns **null**, no new `Setup`. Otherwise checks `CanAdd`, then:
  ```csharp
  StatusEffect statusEffect3 = statusEffect.Clone();    // SEMan.cs:201
  m_statusEffects.Add(statusEffect3);
  m_statusEffectsHashSet.Add(statusEffect3.NameHash());
  statusEffect3.m_hitVariant = variant;
  statusEffect3.Setup(m_character);                    // SEMan.cs:205
  ```
  `Clone()` is `MemberwiseClone()` (StatusEffect.cs:77): value fields are per instance, reference fields (e.g. a `TierConfig`) are shared with the prefab, which is what the spec wants. No owner check in this overload.
- `SEMan.AddStatusEffect(int nameHash, bool resetTime = false, int itemLevel = 0, float skillLevel = 0f, short variant = -1)` – `public StatusEffect`, SEMan.cs:137. Owner: adds locally (looks up `ObjectDB.instance.GetStatusEffect(nameHash)`); non-owner: sends `"RPC_AddStatusEffect"` to the owner and returns null. This is the only cross-machine path for status effects; it requires the effect to be registered in `ObjectDB` (Jötunn's `CustomStatusEffect` does that).
- `SEMan.RemoveStatusEffect(int nameHash, bool quiet = false)` – `public bool`, SEMan.cs:219, and `RemoveStatusEffect(StatusEffect se, bool quiet = false)` SEMan.cs:214. Call `Stop()` and remove from the list immediately. **Never call these from inside `SEMan.Update`'s loop or from `OnDamaged`.** `Update` iterates with a `for` loop over a cached count (SEMan.cs:106): a removal shifts indices and skips or overruns; an *add* during that loop is safe (it is appended past the cached count and first updated next tick). `SEMan.OnDamaged` (SEMan.cs:547–553) iterates with `foreach`, so **any** `AddStatusEffect` (`m_statusEffects.Add`, SEMan.cs:202) or `RemoveStatusEffect` (`m_statusEffects.Remove`, SEMan.cs:235) from inside an `OnDamaged` override throws `InvalidOperationException` (collection modified). A damage tick from an `SE_Stats` (`m_character.Damage(hitData);`, SE_Stats.cs:229) reaches `RPC_Damage` synchronously on the owner, so `OnDamaged` can even run nested inside `SEMan.Update`.
- `SEMan.RemoveAllStatusEffects(bool quiet = false)` – `public void`, SEMan.cs:243. Called by `Player.OnDeath()` (`public override void`, Player.cs:3312) at Player.cs:3432, so `Stop()` runs on death.
- `SEMan.Update(ZDO zdo, float dt)` – `public void`, SEMan.cs:103. For each effect: `UpdateStatusEffect(dt)`, then `IsDone()`; done effects get `Stop()` and are removed after the loop (SEMan.cs:111, 124). Writes the OR of all `m_attributes` to `ZDOVars.s_seAttrib`. Called only on the character's owner:
  ```csharp
  bool num = zDO.IsOwner();      // Character.cs:816
  ...
  m_seman.Update(zDO, dt);       // Character.cs:830 (inside if (num))
  ```
- `SEMan.OnDestroy()` – `public void`, SEMan.cs:59, called from `Character.OnDestroy()` (`protected virtual void`, Character.cs:757, at Character.cs:759). It calls `StatusEffect.OnDestroy()` (`public virtual void`, StatusEffect.cs:111, only removes start effects), **not `Stop()`**. So on logout or when the player object is destroyed, `Stop()` is not called. Status effects are not saved to the profile: `Player.Save(ZPackage pkg)` (Player.cs:4677) writes many things (inventory, recipes, skills, foods, …) but no status effects, so nothing persists.
- `StatusEffect.Setup(Character character)` – `public virtual void`, StatusEffect.cs:87: sets `m_character`, start message, start effects. `SE_Stats.Setup(Character character)` – `public override void`, SE_Stats.cs:159: calls base, then uses `m_ttl` for over-time durations, then `StartupEffects()`. Set `m_ttl` before calling `base.Setup`.
- `StatusEffect.Stop()` – `public virtual void`, StatusEffect.cs:154: removes start effects, plays stop effects and stop message.
- `StatusEffect.UpdateStatusEffect(float dt)` – `public virtual void`, StatusEffect.cs:167: `m_time += dt;` (StatusEffect.cs:169) plus repeat message. `SE_Stats.UpdateStatusEffect(float dt)` – `public override void`, SE_Stats.cs:208, calls base first.
- `StatusEffect.IsDone()` – `public virtual bool`, StatusEffect.cs:181:
  ```csharp
  if (m_ttl > 0f && m_time > m_ttl)    // StatusEffect.cs:183
  ```
  Strictly greater. The spec's tier I `m_time = m_ttl` works because the next `SEMan.Update` adds `dt` before checking `IsDone`, so the effect ends on the owner's next fixed update, not "this frame". This is also the safe way to end an effect from inside `OnDamaged`: it only sets a field.
- `StatusEffect.ResetTime()` – `public virtual void`, StatusEffect.cs:190: `m_time = 0f;`. `SE_Stats.ResetTime()` – `public override void`, SE_Stats.cs:202, also re-runs `StartupEffects()`. Re-applying `SE_Revealed` through `SEMan.AddStatusEffect(se, resetTime: true)` calls `ResetTime()` (SEMan.cs:192) on the existing instance. Confirmed.
- Fields: `public float m_ttl;` StatusEffect.cs:51, `protected float m_time;` StatusEffect.cs:71, `public Character m_character;` StatusEffect.cs:69, `public string m_category` StatusEffect.cs:18.
- `StatusEffect.CanAdd(Character character)` – `public virtual bool`, StatusEffect.cs:82. Honoured by `AddStatusEffect` but not by the consume path (see above).
- **Sync:** status effects exist only on the owning client. Other clients never instantiate them; the only replicated status data are `ZDOVars.s_seAttrib` (attribute bit mask) and values the owner writes itself (stealth, noise, stamina, …). This confirms the spec: remote players' effects are not simulated, the ZDO keys are the state channel.

## ZDO state channel

- How `Character`/`Player` exposes its view: `protected ZNetView m_nview;` (Character.cs:342). Jötunn's publicized assembly makes it reachable; otherwise `GetComponent<ZNetView>()`. Also `public ZDOID GetZDOID()` (Character.cs:3387).
- `ZNetView.GetZDO()` – `public ZDO`, ZNetView.cs:253 (null once destroyed). `ZNetView.IsValid()` – `public bool`, ZNetView.cs:258. `ZNetView.IsOwner()` – `public bool`, ZNetView.cs:227, false when invalid, else `m_zdo.IsOwner()`. `ZDO.IsOwner()` – `public bool`, ZDO.cs:1363; `ZDO.GetOwner()` – `public long`, ZDO.cs:1354.
- Write overloads (`public void` on `ZDO`):
  - `Set(string name, int value)` ZDO.cs:389 → `Set(name.GetStableHashCode(), value)`.
  - `Set(int hash, int value, bool okForNotOwner = false)` ZDO.cs:394. The `okForNotOwner` parameter is unused in the body; there is **no owner check** in `ZDO.Set`. Writes go to `ZDOExtraData` and bump the data revision when the value changed.
  - `Set(string name, bool value)` ZDO.cs:426; `Set(int hash, bool value)` ZDO.cs:431 stores the bool as an int `1`/`0` (`Set(hash, value ? 1 : 0)`). An int key and a bool key with the same hash are the same slot.
  - Also float (ZDO.cs:342/347), long (436/441), string (462/467), Vector3, Quaternion, byte[], ZDOID.
- Read overloads (`public` on `ZDO`): `int GetInt(string name, int defaultValue = 0)` ZDO.cs:658; `int GetInt(int hash, int defaultValue = 0)` ZDO.cs:663; `bool GetInt(int hash, out int value)` ZDO.cs:673; `bool GetBool(string name, bool defaultValue = false)` ZDO.cs:678; `bool GetBool(int hash, bool defaultValue = false)` ZDO.cs:683 (`GetInt(...) != 0`); `bool GetBool(int hash, out bool value)` ZDO.cs:693.
- Hashing: `public static int GetStableHashCode(this string str)` in `public static class StringExtensionMethods`, **`assembly_utils.dll`** (not in assembly_valheim). A deterministic DJB2-style hash over alternating characters, identical on every machine. Vanilla precomputes keys as `public static readonly int s_x = "x".GetStableHashCode();` in `ZDOVars` (ZDOVars.cs, e.g. `s_stealth` ZDOVars.cs:289). Do the same: `static readonly int IP_Tier = "IP_Tier".GetStableHashCode();`, then use the `int hash` overloads. This removes the spec's concern about hashing strings in `CanSeeTarget`; a per-ZDOID cache is still optional (the read is a dictionary lookup in `ZDOExtraData`).
- Who may write: technically anyone, but vanilla only writes a player's keys on the owner (examples: `ToggleDebugFly` Player.cs:7524, `UpdateStealth` Player.cs:6972, `SetPVP` Player.cs:6149). The owner of a player's ZDO is that player's own client. `SE_Invisibility` runs only on the owner (`SEMan.Update` is owner-only), so writing from it automatically satisfies "owner only".
- Who reads: any machine that has the ZDO. ZDO extra data is fully serialized in `ZDO.Serialize(ZPackage pkg)` (ZDO.cs:858), so late joiners receive current values.
- Vanilla precedent for exactly this pattern: debug fly. `Player.IsDebugFlying()` – `public override bool`, Player.cs:4496: owner returns the field, others read `ZDOVars.s_debugFly`. Same for `GetStealthFactor` and `GetNoiseRange`. `HiddenState.Get` should follow it: owner reads its own cached state, others read the ZDO.
- Lifetime: the player ZDO is destroyed with the player object on logout, so `IP_Tier`/`IP_Hidden` cannot leak into the next session.

## Network

- `ZDOMan.SendZDOs(ZDOPeer peer, bool flush)` – `private bool`, ZDOMan.cs:1074. `ZDOPeer` is a **private nested class** (`private class ZDOPeer`, ZDOMan.cs:10) with `public ZNetPeer m_peer;` (ZDOMan.cs:28). A Harmony patch must take the argument as `object peer` (or `__0`) and read `m_peer` via `AccessTools`/Traverse; Jötunn's publicizer may or may not expose nested private types (check in plan 2). The receiving player is `peer.m_peer.m_characterID` (`public ZDOID m_characterID`, ZNetPeer.cs:21) and `peer.m_peer.m_uid`.
  The position is written in the per-ZDO header, separately from the ZDO data:
  ```csharp
  zPackage.Write(item2.GetOwner());
  zPackage.Write(item2.GetPosition());     // ZDOMan.cs:1126
  zPackage2.Clear();
  item2.Serialize(zPackage2);
  ```
  `ZDO.Serialize` does not contain the position. A transpiler that replaces this `GetPosition()` call with a helper `(zdo, peer) => spoofed or real position` is cleaner than temporarily calling `ZDO.SetPosition`: `SetPosition` → `InternalSetPosition` (ZDO.cs:480) calls `SetSector(...)`, i.e. it **moves the ZDO between sector lists** on the server, which a prefix/postfix pair would do twice per send. The Server Devcommands technique needs to be checked against this before reuse.
- **Topology:** `ZDOMan.Update` (ZDOMan.cs:878) sends ZDOs to every entry in `m_peers` via `SendZDOToPeers2` (ZDOMan.cs:903). A client's only peer is the server. So the hidden player's own client always sends the **real** position to the server, and it is the **server** that forwards the hidden player's ZDO to other clients. The position spoof therefore has to run on the server (dedicated server or listen host). The spec's "the server peer still gets the real position" is automatically true; the patch must also skip the peer that owns the ZDO (`zdo.GetOwner() == peer.m_peer.m_uid`).
- Zone ownership (who runs AI) is decided on the server from each peer's **reference position**, not from the player ZDO position: `ZDOMan.ReleaseNearbyZDOS(Vector3 refPosition, long uid)` (`private void`, ZDOMan.cs:967) is called with `peer.m_peer.m_refPos` (ZDOMan.cs:949). The ref position comes from `ZNet.SendPeriodicData`. So the ZDO position spoof does not disturb ownership, but the periodic-data patch must not touch `m_referencePosition`.
- `ZNet.SendPeriodicData(float dt)` – `private void`, ZNet.cs:1553, every 2 s. On the server it sends time and the player list and returns; on a client it calls `SendServerSyncPlayerData(ZNetPeer peer)` (`private void`, ZNet.cs:1581), which writes:
  ```csharp
  zPackage.Write(m_referencePosition);         // ZNet.cs:1584
  zPackage.Write(m_publicReferencePosition);   // ZNet.cs:1585
  ```
  The server stores them in `RPC_ServerSyncedPlayerData` (ZNet.cs:1611) as `peer.m_refPos` / `peer.m_publicRefPos` (`public bool m_publicRefPos;` ZNetPeer.cs:19).
- `ZNet.m_publicReferencePosition` – `private bool`, ZNet.cs:221. Public accessors `public void SetPublicReferencePosition(bool pub)` ZNet.cs:2213 and `public bool IsReferencePositionPublic()` ZNet.cs:2218 (the map's "share position" toggle).
- **Host gap:** a listen-server host never runs the client branch of `SendPeriodicData`. Its own entry is built in `ZNet.UpdatePlayerList()` (`private void`, ZNet.cs:2454) directly from `m_publicReferencePosition` (`m_publicPosition = m_publicReferencePosition`, ZNet.cs:2469). Peers' entries use `peer.m_publicRefPos` (ZNet.cs:2500). **Recommended: one server-side postfix on `ZNet.UpdatePlayerList`** that clears `m_publicPosition` (and `m_position`) of every entry whose `m_characterID` ZDO has `IP_Hidden` with tier III. It covers clients and the host in one place and needs no save/restore of the user's toggle. The spec's `SendPeriodicData` prefix only covers non-host clients and has to restore the user's flag afterwards.
- Plan 2 Task 12 verification: `ZNet.m_players` is `private List<PlayerInfo>` (ZNet.cs:283); `public struct PlayerInfo` (ZNet.cs:109) with `m_characterID` (:113), `m_publicPosition` (:117), `m_position` (:119). `UpdatePlayerList` has one caller, `SendPlayerList` (ZNet.cs:2517), which is called only behind `IsServer()`/`m_isServer` (ZNet.cs:388, 1150, 1239 in `Disconnect`, 1564 in `SendPeriodicData`). The SendZDOs IL contains exactly one `callvirt ZDO::GetPosition()`, at IL_017d, directly followed by `callvirt ZPackage::Write(Vector3)` (IL_0182) (`ilspycmd --ilcode -t ZDOMan`). `SendZDOs` also runs on clients (their only peer is the server), so the header spoof must check `ZNet.instance.IsServer()`; otherwise the hidden player's own client would send the spoofed position to the server. On the receiver, `RPC_ZDOData` applies the header position with `InternalSetPosition` (ZDOMan.cs:1195), which bumps the data revision only on the owner.
- `ZNet.GetOtherPublicPlayers(List<PlayerInfo> playerList)` – `public void`, ZNet.cs:2730. Returns only entries with `m_publicPosition` and not the local character.

## Visuals

- `Minimap.UpdatePlayerPins(float dt)` – `private void`, Minimap.cs:1491. Builds pins solely from `ZNet.instance.GetOtherPublicPlayers(m_tempPlayerInfo)`. If the server already clears the public flag, no viewer-side patch is needed. Viewer-side fallback: postfix on `ZNet.GetOtherPublicPlayers` removing hidden character IDs. Runs on every client.
- `EnemyHud.LateUpdate()` – `private void`, EnemyHud.cs:80: for every character except the local player, shows a HUD if `!m_hideHud && TestShow(c, isVisible: false)`.
- `EnemyHud.TestShow(Character c, bool isVisible)` – `private bool`, EnemyHud.cs:101. Also called for existing HUDs in `UpdateHuds` (`private void UpdateHuds(Player player, Sadle sadle, float dt)`, EnemyHud.cs:160), which destroys the HUD when it returns false. Vanilla already hides crouching players there:
  ```csharp
  if (c.IsPlayer() && c.IsCrouching())
  {
      return false;
  }
  ```
  **Recommended hook: postfix on `EnemyHud.TestShow`**, `__result = false` for a hidden remote player. Covers creation and removal. (`Character.m_hideHud` is a local public field read only at creation.) Runs on every client (viewer side). With the position spoof active a hidden T3 player is out of HUD range anyway; this hook matters for the window before the spoof reaches the viewer and for tiers that might hide nameplates later.
- `VisEquipment.UpdateEquipmentVisuals()` – `private void`, VisEquipment.cs:721. Exists, but it is driven by `MonoUpdaters` (frequency to confirm) for every `VisEquipment`: `CustomUpdate` (VisEquipment.cs:612) → `UpdateVisuals()` (VisEquipment.cs:617) → `UpdateEquipmentVisuals()` (VisEquipment.cs:624), driven by `MonoUpdaters` (MonoUpdaters.cs:78). It reads the item hashes from the ZDO and calls the `Set*Equipped` methods, which attach/destroy item instances only on change. A postfix on it would re-apply the veil on every call.
- **Recommended hook: postfix on `VisEquipment.UpdateLodgroup()`** – `private void`, VisEquipment.cs:866. It is called only when something actually changed:
  ```csharp
  if (flag)
  {
      UpdateLodgroup();      // VisEquipment.cs:831
  }
  ```
  It runs on every client for every visible player (the ZDO drives it), so remote viewers re-apply their veil too. Note: `UpdateLodgroup` returns early when `m_lodGroup == null`; a Harmony postfix still runs.
- `VisEquipment.UpdateBaseModel()` – `private void`, VisEquipment.cs:675. When the model index changes it assigns `m_bodyModel.sharedMesh` and sets `_MainTex` on `m_bodyModel.materials[0]`, i.e. on whatever material is in slot 0 at that moment, including a veil material. Rare (model change), but the veil must tolerate it.
- `Character.SetVisible(bool visible)` – `protected void`, Character.cs:4299, called from `CustomFixedUpdate` with `zDO.HasOwner()` (Character.cs:823). Hides a character by moving its LOD group's `localReferencePoint` to `(999999, 999999, 999999)`. Shows that vanilla's own "hide this character" trick is the LOD reference point, which a veil must not fight with.
- Renderer source: `VisEquipment.UpdateLodgroup` itself collects `m_visual.GetComponentsInChildren<Renderer>()` (VisEquipment.cs:872), matching the spec's snapshot approach.

### Veil look (task 10c)

- `Character.m_animator` – `protected Animator`, Character.cs:346, set in `Awake` via `GetComponentInChildren<Animator>()` (Character.cs:667). Publicized.
- `Utils.GetBoneTransform(Animator animator, HumanBodyBones humanBoneId)` – `public static Transform`, assembly_utils (not in `tools/decompiled/`; read with `ilspycmd -t Utils assembly_utils.dll`). Body: `return FindChild(animator.transform, humanBoneId.ToString());` – a name search, not `Animator.GetBoneTransform`. Vanilla use: `m_head = Utils.GetBoneTransform(m_animator, HumanBodyBones.Head)` (Character.cs:674). The player rig has no bone named `Chest`; the fog falls back to `Spine2`/`Spine1`/`Spine` via `Utils.FindChild(Transform aParent, string aName, IterativeSearchType searchType = DepthFirst)`.
- `StatusEffect.m_startEffects` / `m_stopEffects` – `public EffectList`, StatusEffect.cs:55/57. Start effects are created in `TriggerStartEffects` (StatusEffect.cs:~128, called from `Setup`) parented to the character; stop effects in `Stop()` (StatusEffect.cs:159). `EffectList.m_effectPrefabs` – `public EffectData[]`, EffectList.cs:35. Effect prefabs with a `ZNetView` are networked by vanilla's own EffectList (same as every vanilla mead).
- Shader properties (read statically from bundle `StreamingAssets/SoftRef/Bundles/c4210710` with UnityPy):
  - `Custom/Distortion`: `_Color`, `_MainTex`, `_NormalTex`, `_Glossiness`, `_Metallic`, `_NormalScale`, `_RefractionIntensity`, `_WaveVel`, `_DepthFade`. No emission, no blend-mode properties. Vanilla materials using it: `Aspect_mat` (`_Color` 0.28,0.52,0.66 – the blue tint, `_Glossiness` 0.43, `_RefractionIntensity` 2), `staff_FrostOrbs_shard`, `staff_shield_shard`, `ForceField`.
  - `Custom/Creature` (`Ghost_mat`): `_Color`, `_MainTex`, `_Hue/_Saturation/_Value`, `_EmissionMap`, `_EmissionColor`, `_MetallicGlossMap`, `_Metallic`, `_MetalColor`, `_Glossiness`, `_MetalGloss`, `_BumpMap`, `_BumpScale`, `_TwoSidedNormals`, `_Cull`, `_Cutoff`, `_AddRain`, `_NoiseGlow*`, `_UseStyles`, `_StyleTex`, `_Style`, `_SnowCover`. No blend-mode properties: alpha-tested, not translucent. `Ghost_mat` values: `_EmissionColor` 2.83 (white HDR glow), `_Cutoff` 0.63, `_Cull` 0.
  - `Custom/LitParticles` (`swamp_mist`): `_Color`, `_MainTex`, `_EmissionColor`, `_NormalTex`, `_BumpScale`, `_LightNormalFactor`, `_ZFadeDistance`, `_CameraFadeDistanceMin/Max`, `_CameraYFadeDistance`, `_Billboard`, `_SkyMask`, `_AlphaChannel`. `swamp_mist`: `_Color` white alpha 1, emission black.

### Veil iteration 3 and tuning window (task 10d)

- `RenderSettings.fogColor` is set every frame by `EnvMan` from the current environment (`m_fogColorNight/Day/Morning/Evening`, EnvMan.cs:857–861). The dynamic fog colour samples it.
- Bone names for the new anchors: `Utils.GetBoneTransform` searches by `HumanBodyBones` name (see above). Shoulder anchors map to `HumanBodyBones.LeftUpperArm`/`RightUpperArm`; fallbacks by Mixamo name (`LeftArm`, `LeftUpLeg`, `LeftLeg`) in case the rig does not use the Unity names. Not yet confirmed in-game: a missing bone logs `bone for anchor '<name>' not found` once.
- `EffectList.Create(Vector3, Quaternion, Transform baseParent, ...)` – EffectList.cs:37. Parents the instance to `baseParent` (or `m_childTransform` under it) only when `EffectData.m_attach` is set (EffectList.cs:~107); `m_follow` adds a `ParentConstraint` instead. `StatusEffect.TriggerStartEffects` passes `m_character.transform` (StatusEffect.cs:128). Consequence: only attached status vfx are children of the player and get hidden by the Distortion/Ghost particle hiding; `m_follow`-only effects are not.
- `GameCamera.m_mouseCapture` – `private bool`, GameCamera.cs:87 (publicized). `GameCamera.LateUpdate` (GameCamera.cs:167) calls `UpdateMouseCapture()` (GameCamera.cs:185), which locks and hides the cursor while `m_mouseCapture` is true and no vanilla UI is open, and unlocks/shows it otherwise. The Debug tuning window sets it to false while it owns the mouse and restores it on close.
- `PlayerController.TakeInput(bool look = false)` – `private bool`, PlayerController.cs:223. Gates movement/attack/block (`FixedUpdate`, PlayerController.cs:82) and mouse look (`LateUpdate`, PlayerController.cs:264). Debug-only postfix forces `false` while the tuning window owns the mouse; patch health key `PlayerController.TakeInput` (Debug builds only).

## Ghost mode (for reference)

- `private bool m_ghostMode;` Player.cs:324. `public void SetGhostMode(bool ghostmode)` Player.cs:4464. `public override bool InGhostMode()` Player.cs:4469 (base `public virtual bool InGhostMode()` Character.cs:3848 returns false). Set only by the `ghost` console command (Terminal.cs:1768).
- **Not synced.** `m_ghostMode` is a local field with no ZDO key, so on any other machine `InGhostMode()` of that player returns false. Vanilla ghost mode only hides a player from monsters that the ghost player's own client owns. This is the strongest argument for the spec's ZDO design (ADR 0004). The `InDebugFlyMode()` check used next to it (Player.cs:5653, returns the local `m_debugFly`) has the same limitation, while `IsDebugFlying()` is the ZDO-backed variant.
- Every place AI/spawning consults it:
  - `BaseAI.CanHearTarget` static – BaseAI.cs:754 (returns false).
  - `BaseAI.CanSeeTarget` static – BaseAI.cs:789 (returns false).
  - `BaseAI.FindEnemy` hunt-player fallback – BaseAI.cs:1420 (returns null).
  - `MonsterAI.UpdateSleep` wake-up range – MonsterAI.cs:823; noise wake-up – MonsterAI.cs:852.
  - `EggHatch.cs:32` (eggs do not hatch near a ghost).
  - `SpawnSystem` and `RandEventSystem`: **no** ghost-mode checks (grep finds none). Spawning around a ghost player continues.
- Non-AI uses: `Character.ApplyDamage` marks a monster as cheated (no drops) when the attacker is in ghost mode (Character.cs:2449) and keeps a ghost at 1 HP (Character.cs:2459). Never implement invisibility by setting ghost mode: kills would be flagged as cheated.
- The game's own list of "enemy must ignore this player" paths is therefore exactly the perception hooks recommended above (two static checks + `FindEnemy` fallback) plus `UpdateSleep`.

## Where each hook runs (multiplayer)

| Hook | Runs on | Evidence |
|---|---|---|
| `BaseAI.CanHearTarget`/`CanSeeTarget` static, `FindEnemy`, `MonsterAI.UpdateTarget`, `UpdateSleep` | ZDO owner of the monster (zone owner, usually the client whose ref position first covered the zone) | `BaseAI.UpdateAI` owner guard BaseAI.cs:311; ownership: `ZDOMan.ReleaseNearbyZDOS` ZDOMan.cs:967 |
| Aggro drop by setting `m_targetCreature`/`SetAlerted(false)` | Effective only on the monster's owner; elsewhere `m_targetCreature` is unused and `m_alerted` is overwritten from the ZDO | BaseAI.cs:313, MonsterAI.cs:114 |
| `HiddenState` reads (`ZDO.GetInt/GetBool`) | Any machine | ZDO.cs:663, 683 |
| `HiddenState` writes, `SE_Invisibility` lifecycle, `SE_Revealed` | Hidden player's own client (player ZDO owner) | `SEMan.Update` only under `zDO.IsOwner()`, Character.cs:816/830 |
| Stealth/noise modifiers | Computed on the player's client, consumed by the monster owner through `ZDOVars.s_stealth` / `s_noise` | Player.cs:6972/6977, Character.cs:3804/3830 |
| Stamina regen debuff | Player's own client | Player.cs:2110 |
| `Humanoid.StartAttack` postfix, `Attack.StartDraw` postfix | Attacker's client (also monster owners for AI attacks: filter on local player) | owner guard Player.cs:817–820, `m_localPlayer != this` check Player.cs:821 |
| `Character.Damage` | Attacker's machine (sends RPC) | Character.cs:2232 |
| `Character.RPC_Damage`, `StatusEffect.OnDamaged`, `Humanoid.BlockAttack` | Victim's ZDO owner (the hidden player's client for a player victim) | Character.cs:2252 |
| `Player.CanConsumeItem`/`ConsumeItem` | Drinking player's client | Local-input callers: `InventoryGui.cs:878` calls `Player.m_localPlayer.UseItem(...)`; `Feast`/`ItemDrop` paths call `Player.m_localPlayer.CanConsumeItem` (Feast.cs:166 via local player, ItemDrop.cs:1539/1657). No owner guard inside the methods themselves; "local client" is inferred from the callers. |
| `ZDOMan.SendZDOs` position spoof | **Server** (dedicated or listen host); clients only send to the server | ZDOMan.cs:878/903/1074 |
| `ZNet.UpdatePlayerList` (recommended) | Server | ZNet.cs:2454, called from `SendPlayerList` ZNet.cs:2517 |
| `ZNet.SendPeriodicData` (spec) | Each non-host client | ZNet.cs:1561 server branch returns |
| `EnemyHud.TestShow`, `Minimap.UpdatePlayerPins`, `VisEquipment.UpdateLodgroup`, remote veil | Every client (viewer side) | MonoBehaviour updates, no owner guard |

## Findings that affect the design

The spec decisions are left untouched; these are the points where a decision cannot be implemented as written or needs a choice.

1. **Perception hook (spec §5.5).** A prefix on `BaseAI.CanSenseTarget(Character)` alone does not hide the player from a monster that already targets them: `MonsterAI.UpdateTarget` calls `CanHearTarget`/`CanSeeTarget` directly (MonsterAI.cs:313–314), so the "sensed" timer keeps resetting and the monster never loses aggro. Turrets (`FindClosestCreature`) also bypass it. Correct hooks: prefixes on the static `BaseAI.CanHearTarget(Transform, float, Character)` and `BaseAI.CanSeeTarget(Transform, Vector3, float, float, bool, bool, Character)`, the places vanilla checks ghost mode.
2. **Hunting monsters (raids, event creatures).** `FindEnemy` returns the closest player within 200 m for `HuntPlayer()` monsters without any perception check (BaseAI.cs:1419). A postfix on `BaseAI.FindEnemy()` is needed in addition, or tier II/III players are tracked by every raid.
3. **Lose-target threshold.** The `30f` is an inlined literal (MonsterAI.cs:336). "Substitute `AggroLossTime`" is best done as a postfix on `MonsterAI.UpdateTarget` that performs the same drop when `m_timeSinceSensedTargetCreature > AggroLossTime` for a hidden target, rather than a constant replacement.
4. **Immediate aggro drop vs. "walk to last known position and search".** Vanilla only walks to `m_lastKnownTargetPos` and searches while `m_targetCreature` is still set but not sensed (MonsterAI.cs:524–585). Setting `m_targetCreature = null` on entering Hidden skips the search and goes straight to `IdleMovement`. Also, `m_targetCreature` only exists on the monster's owner, which on a server is often not the hidden player's client, so the local drop is a no-op there. To get the decided behaviour, rely on the `UpdateTarget` postfix with `AggroLossTime` (the search lasts that long) and drop the immediate null, or keep the immediate drop and accept no search phase. `m_alerted` is a `BaseAI` field; use `SetAlerted(false)`.
5. **Tier I stealth applies only while crouching.** `m_seman.ModifyStealth` is only called inside `if (IsCrouching())` in `Player.UpdateStealth` (Player.cs:6961); standing players always have stealth factor 1. Tier I's view-range reduction therefore only works while sneaking; the noise reduction works always. If tier I should also hide a standing player, plan 2 needs a postfix on `Player.UpdateStealth` that adjusts `m_stealthFactorTarget` (owner side; it reaches the monster owner through `ZDOVars.s_stealth`). The factor also moves at only 0.25/s (Player.cs:6969), a 3 s fade.
6. **Tier I modifier values.** `m_stealthModifier`/`m_noiseModifier` are additive (`stealth += baseStealth * m_stealthModifier`). The config default `0.25` meant as "×0.25" must be applied as `-0.75`. Written as-is it makes the player 25 % louder and more visible.
7. **Refusal on drink (spec §5.3 `Setup`).** `Player.ConsumeItem` ignores `AddStatusEffect`'s result and removes the item afterwards (Player.cs:6057, 6063). A refusal inside `Setup`/`CanAdd` still consumes the potion. The refusal must be a postfix on `Player.CanConsumeItem`. The three tier effects must not share a non-empty `m_category`, or vanilla refuses every upgrade itself.
8. **`Stop()` is not called on logout.** `Character.OnDestroy` → `SEMan.OnDestroy` → `StatusEffect.OnDestroy` (StatusEffect.cs:111), not `Stop()`. The spec's "single cleanup path including logout" is not reached on logout. Harmless for the ZDO (destroyed with the player) and for local visuals (object destroyed), but any global state (static caches, the public-position flag, registered callbacks) must also be cleaned in an `OnDestroy` override.
9. **Harvesting reveals under the `StartAttack` hook.** Axe and pickaxe swings at trees and rocks go through `Humanoid.StartAttack`. The decision "harvesting does not reveal" needs a filter (e.g. reveal only when the attack hits a `Character`, or exclude by `m_shared.m_skillType`/target), or the decision has to change.
10. **Blocking reveals only on a blocked hit.** `BlockAttack` runs inside `RPC_Damage` on the victim when a hit is blocked. Raising the shield without being hit does not reveal unless `UpdateBlock`/`IsBlocking()` is hooked instead.
11. **Taking damage needs no patch, but the override must defer.** Override `StatusEffect.OnDamaged(HitData, Character)`; it runs on the victim's owner from `RPC_Damage`. It is called from a `foreach` over the effect list (SEMan.cs:547–553), so it must only set fields (pending-reveal flag, `m_time = m_ttl`); applying `SE_Revealed` there (spec §5.3 `OnReveal()`) throws `InvalidOperationException`. Process the flag in the next `UpdateStatusEffect` (adds are safe there, removes are not) or in a postfix on `SEMan.OnDamaged`/`Character.RPC_Damage`. `OnReveal()` as specified must therefore be split into "mark" and "apply" when triggered by damage. The spec's `Character.Damage` prefix would run on the attacker's machine, which is the wrong side for "the hidden player takes damage".
12. **Tier III position spoof runs on the server and the dedicated server must have the mod.** Clients only talk to the server; the server forwards player ZDOs. The spoof belongs in the server's `ZDOMan.SendZDOs` and should replace the header position (ZDOMan.cs:1126) instead of calling `ZDO.SetPosition`, which moves the ZDO between sector lists. Monster owners other than the hidden player also receive the spoofed position, so tier III hides the player from AI on other clients' zones twice over; the spoof must stop the moment the player is revealed, or those monsters cannot fight back.
13. **Public map position.** `SendPeriodicData` does not run the client branch on a listen host, so a hidden host stays on everyone's map. A server-side postfix on `ZNet.UpdatePlayerList` covers all players and needs no save/restore of the user's toggle.
14. **Equipment-change hook.** `VisEquipment.UpdateEquipmentVisuals` is driven by MonoUpdaters (frequency to confirm); use `VisEquipment.UpdateLodgroup` (called only on change) for re-applying the veil.

## Open questions for plan 2

Unresolved by static reading, each with what would settle it:

1. **Sleeping monsters.** `MonsterAI.UpdateSleep` wakes monsters on player proximity without perception checks (MonsterAI.cs:817). Should a hidden player wake them? If not, plan 2 needs a patch (transpiler or a prefix that replicates the method). Settle: a design decision, then test next to a sleeping Draugr/Fuling.
2. **Dedicated server as AI owner.** On a dedicated server `ReleaseNearbyZDOS` is also called with the server's own reference position (ZDOMan.cs:946). Whether a dedicated server ever owns monster ZDOs in practice is unresolved. Settle in-game: `ip_state` printing `GetOwner()` of nearby monsters on the LXC server.
3. **Side effects of the spoofed position on receiving clients** (`y = 10000`): interpolation by `ZSyncTransform`, ships/carts the hidden player rides, voice positioning, ragdolls. Unresolved. Settle in the PvP two-client test.
4. **Publicizer and private nested types.** Settled (plan 2, Task 12): a probe `ZNetPeer P(ZDOMan.ZDOPeer p) => p.m_peer;` compiles against the publicized assembly, so the nested type is exposed at compile time. `PlayerHidePatches` still takes the peer as `object` and reads `m_peer` through `AccessTools.FieldRefAccess<object, ZNetPeer>` so no runtime access to the private type is needed.
5. **Cost of `HiddenState` reads in `CanSeeTarget`/`CanHearTarget`.** These run for every monster × character every target update (2–6 s per monster) plus on every AI tick for the current target: `UpdateTarget` runs inside `UpdateAI`, which `MonoUpdaters` drives at a fixed 0.05 s step (MonoUpdaters.cs:45–47), i.e. 20 times per second per owned monster. With precomputed int hashes the read is one dictionary lookup; whether a cache is needed is unresolved. Settle: profile with many monsters in-game.
6. **Player ZDO reaching other clients when hidden.** Whether the hidden player's ZDO is still sent to a client whose ref position is far from the spoofed position: `CreateSyncList` uses the server-side (real) position for sectors, so it should be. Settle in the two-client test.
7. **UpdateEquipmentVisuals frequency.** Confirm how often `UpdateEquipmentVisuals` runs (MonoUpdaters). Settle: read MonoUpdaters.cs:78 and its update loop, or log call counts in-game.
