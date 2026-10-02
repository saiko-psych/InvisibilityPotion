# Publishing InvisibilityPotion

Where to publish, what each site needs, and ready-to-paste descriptions. Screenshots live in `docs/screenshots/` (in-game shots as JPG, model previews as PNG).

## Where

| Site | Why | Needs | Notes |
|---|---|---|---|
| **Thunderstore** (thunderstore.io/c/valheim) | The main Valheim mod hub; r2modman / Thunderstore Mod Manager install from it; most players and server admins look here first | Account (Discord/GitHub login), a **team** name (the package becomes `Team-InvisibilityPotion`), the zip from `make package` | `manifest.json`, `README.md`, `CHANGELOG.md`, `icon.png` are inside the zip; `website_url` may point to the public repo. Dependencies are declared in the manifest, so the mod manager installs BepInExPack + Jötunn automatically. New versions = upload a new zip with a higher `version_number` |
| **Nexus Mods** (nexusmods.com/valheim) | Second-largest audience, manual installers, good for screenshots and a long description | Account; upload the same zip as a "main file"; fill in description, screenshots, requirements (BepInEx, Jötunn), permissions/licence | Nexus has no dependency automation: the description must say "install BepInExPack Valheim and Jötunn first, copy `plugins/InvisibilityPotion.dll` to `BepInEx/plugins/`" |
| **Gitea / GitHub release** | Source + release tag; link from both sites | `git tag v0.3.0`, attach the zip as a release asset | A public mirror on GitHub is useful because Thunderstore/Nexus users expect a public issue tracker; the Gitea instance is private (LAN) |
| **Valheim Modding Discord** (#mod-releases) | Announcement and feedback from other modders | Discord account | Optional |

Order: Thunderstore first (dependency handling), then Nexus with the same zip, then the release tag; post the Discord announcement last with both links.

## Short description (≤ 250 characters, Thunderstore manifest / Nexus summary)

> Three veil meads that hide you from enemies and, at tier III, from other players. Brew them from hidden plants only veil goggles reveal. Attacking or using tools breaks the veil. Server + clients need the mod.

## Long description (Nexus / Thunderstore README intro)

> **InvisibilityPotion** adds three tiers of *veil meads* to Valheim. Drink one and a soft fog settles around you: at tier I enemies barely notice you, at tier II they lose you completely (sleeping monsters stay asleep, raids ignore you), and the tier III **Shadow Veil** hides you from other players as well — no nameplate, no map pin, no real position on their client.
>
> Invisibility is a tool, not a weapon: dealing or taking damage, blocking, drawing a bow, casting, chopping, mining or building breaks the veil. Breaking it yourself empties your stamina and leaves you with **Veil Broken** — slow, no sprint, barely any regeneration — until the veil returns. Every mead reduces your carry weight while it lasts, and a shared cooldown stops you from chaining them.
>
> The ingredients are **veil plants** that only **veil goggles** reveal: Huldra's Hair, a lichen hidden on Black Forest firs and pines; Baldr's Tear, a glowing snow flower of the Mountains; and Hel's Ember Fern in the Ashlands. Three goggles (Forge → Black forge) unlock them tier by tier; the Allfather's Eye also shows players hidden by a Shadow Veil. All three plants can be cultivated — the lichen on a tree trunk, the others on cultivated ground in their own biome.
>
> Every value — durations, reveal rules, debuff strength, cooldowns, plant rarity, yields, recipes, fog look — is configurable and synced from the server (admins only). Works with the BepInEx Configuration Manager for in-game tuning.
>
> Requires BepInExPack Valheim and Jötunn on the server and on every client.

## Feature bullets (Nexus "features" box)

- 3 veil meads (60 / 120 / 180 s) with AI-side stealth, not just visuals
- Tier III: invisible to other players (PvP-ready, server-authoritative position hiding)
- Reveal rules: damage, block, bow, staff, tools; configurable
- Veil Broken debuff + stamina drain + carry-weight penalty + shared cooldown
- 3 hidden plants, 3 goggles, cultivation, 15 plant variants with wind
- Custom low-poly models (bottles, bowls, goggles, plants) and status icons
- Fully configurable, server-synced; Configuration Manager friendly

## Screenshot set (docs/screenshots)

In-game: `fog-tier1-faint-veil.jpg`, `fog-tier2-deep-veil.jpg`, `tier3-shadow-veil.jpg`, `goggles-1..3-*.jpg`, `plant-baldrs-tear-mountains.jpg`. Models: `models-bottles.png`, `models-bowls.png`, `models-goggles.png`, `models-plants.png`, `models-ingredients.png`, `status-icons.png`.

Still worth taking (HUD off with Ctrl+F3): a fern group in the Ashlands at night, the lichen on a trunk with goggles I, a Shadow Veil player seen through the Allfather's Eye (needs two clients).

## Tags

Thunderstore categories: Gameplay, Items, Crafting, Server-side, Client-side, Misc. Nexus: Gameplay, Items and Objects, Crafting, Visuals and Graphics, Multiplayer.
