# FAQ

**I don't see any plants.**
Put on veil goggles. Plants are invisible without goggles of at least their tier (Watchman's Glass shows Huldra's Hair, Mimir's Glass adds Baldr's Tear, Allfather's Eye adds the Ashlands fern). Wild Baldr's Tear and ferns only spawn in **newly generated zones**, so in an old world walk into unexplored land, or cultivate them. Huldra's Hair grows on only about 1 in 40 Black Forest firs and pines and shows within 40 m.

**Where is the config?**
`BepInEx/config/saikopsych.InvisibilityPotion.cfg`. On a server, the server's file counts. See [Configuration](04-Configuration).

**The fog is too bright or too dark.**
With ConfigurationManager (F1), open `[Fog.Tier1]`, `[Fog.Tier2]` or `[Fog.Tier3]` and change `Alpha` / `OuterAlpha` (visibility), `Emission` (0 = matte and dark at night, 1 = glows) and `DynamicColor` (true = follows weather and night). Fog is client-side; only you see your own changes.

**I can't drink a second mead.**
All veil meads share one Veil Cooldown (30 / 60 / 90 s after drinking tier I / II / III). The status bar shows it. You also cannot drink a weaker or equal mead while a stronger veil is running.

**Why am I revealed when I chop a tree?**
Tool use reveals you: axe and pickaxe swings (even at air), hammer, hoe and cultivator. Picking plants by hand does not. You can switch it off with `RevealOnToolUse`.

**Why is my stamina at zero?**
You broke your own veil by acting (hit, bow, staff, tool). That empties stamina (`DrainStaminaOnAttackReveal`) and starts Veil Broken.

**My Mimir's Glass (goggles II) recipe is locked.**
It needs a Forge at **level 3** (two upgrades). Allfather's Eye needs a Black forge at level 2.

**There are plants in the water.**
Old zones generated before 0.3.0's dry-land rules could have them. Since 0.3.1 they are removed when the zone loads (`RemoveMisplacedWildPlants`). Cultivated plants stay.

**Can I place meads on item stands?**
Veil meads fit the horizontal item stand but not the wall item stand. Mead bases fit neither. The Serving Tray ("Mead" tab) takes both meads and bases.

**Can I see other players who drink the Shadow Veil?**
Only with Allfather's Eye (goggles III). Nobody else sees a nameplate, map pin or real position.

**Does it work with mod X?**
Usually yes. The mod patches monster perception, sleeping monsters, nameplates, carry weight and player position sync. Mods that replace AI perception, rewrite `ZDOMan.SendZDOs`, replace nameplates, add carry-weight postfixes, or replace the vegetation list can conflict (no hiding, no position spoof, no bonus reduction, no wild plants). Other stealth mods that change player materials give unpredictable visuals. Do not hot-reload the mod with ScriptEngine. Report problems with your log on GitHub.

**Does it work on Linux?**
Yes. It is developed on Linux. Use the Steam launch option `./start_game_bepinex.sh %command%`.

**Can I join a server with a different Jötunn version?**
No. Jötunn checks its version to the patch number. Install the server's Jötunn version.

**Can an existing world use the mod?**
Yes. Huldra's Hair appears on existing trees. Ground plants only in new zones.

**Where do I report bugs or say thanks?**
Issues: https://github.com/kitschekko/InvisibilityPotion/issues. Support: https://buymeacoffee.com/kitschekko
