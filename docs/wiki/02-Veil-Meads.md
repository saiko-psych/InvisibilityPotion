# Veil Meads

![Faint Veil](https://raw.githubusercontent.com/kitschekko/InvisibilityPotion/main/docs/screenshots/fog-tier1-faint-veil.jpg)

## The three tiers

| Mead | Duration | Enemies | Other players | After a reveal | Carry weight |
|---|---|---|---|---|---|
| Faint Veil Mead (I) | 60 s | notice you far less (a quarter of the usual visibility and noise), even standing | see you | the veil ends | 75 % |
| Deep Veil Mead (II) | 120 s | cannot see or hear you | see you | hidden again after 12 s without attacking | 60 % |
| Shadow Veil Mead (III) | 180 s | as tier II | no nameplate, no map pin, no real position | hidden again after 8 s | 50 % |

Brew the **mead base** at the **Mead Ketill**, ferment it, drink the result. A base needs honey, thistle, a bloodbag (II and III), Ymir flesh (III) and 2 of the tier's veil ingredient (see [Plants and Goggles](03-Plants-and-Goggles)).

Default recipes: I `Honey:10, Thistle:5`; II adds `Bloodbag:3`; III adds `YmirRemains:1`; each plus `VeilIngredient_TN:2`.

## What reveals you

- Dealing damage, taking damage, blocking or parrying, drawing a bow, casting with a staff.
- Tool use: swinging an axe or pickaxe (at anything, even air), building with the hammer, using the hoe or the cultivator.
- Picking plants by hand does **not** reveal you.

Each trigger can be switched off in the [config](04-Configuration).

## Veil Broken

A reveal gives you the **Veil Broken** debuff until the veil returns (tier II/III: the re-hide delay, tier I: 20 s). It restarts on every reveal.

- Stamina regeneration 10 %, eitr 15 %, health 35 %.
- Movement 50 % slower (tier I: 35 %).
- No sprinting.

The veil is for sneaking past, not for fighting.

## Stamina drain

If **you** break your veil by acting (hitting, drawing a bow, casting, using a tool), your stamina bar is emptied. Taking damage or blocking does not. Switch: `DrainStaminaOnAttackReveal`.

## Carry weight

While a veil is active your maximum carry weight is reduced (75 % / 60 % / 50 % by tier). Being over the limit slows you down as in vanilla.

## Cooldown

Drinking a veil mead starts one **shared** Veil Cooldown: tier I 30 s, II 60 s, III 90 s, counted from drinking. No veil mead can be drunk until it runs out. It is shorter than the veil itself, so after the cooldown a stronger mead can replace a running veil.

## Upgrade rule

You cannot drink a weaker or equal mead while a stronger veil is active. A stronger mead replaces the running one cleanly (after the cooldown).

## Serving tray

Veil meads and mead bases can be placed with the **Serving Tray** (tab "Mead"), like vanilla meads. Drink a placed mead with Use, pick it up with alt-use, or remove the tray to get it back. Meads also fit the horizontal item stand, but not the wall item stand; mead bases fit neither stand.

## What the AI does

- **Tier I:** enemies see and hear you at a fraction of the usual range, even when you stand still.
- **Tier II and III:** enemies cannot see or hear you at all.
- A chasing enemy gives up after a short while once it loses you (`AggroLossTime`, 1 s by default for tier II and III, 5 s for tier I; vanilla's own cap is 30 s).
- **Sleeping monsters stay asleep** while you are hidden (tier II/III; tier I: not tested (?)).
- **Raids** do not pick you as a target while you are hidden (tier II/III).
- Revealed means visible again: the veil only works while you are hidden.
