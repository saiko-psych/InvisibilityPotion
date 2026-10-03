# Installation

The mod needs **BepInExPack Valheim** and **Jötunn**. It must be installed on the server **and** on every client.

## Mod manager (recommended)

Install **EdgeExploxers-InvisibilityPotion** (https://thunderstore.io/c/valheim/p/EdgeExploxers/InvisibilityPotion/) with r2modman or the Thunderstore Mod Manager. BepInExPack Valheim and Jötunn are installed automatically.

## Manual

1. Install [BepInExPack Valheim](https://thunderstore.io/c/valheim/p/denikson/BepInExPack_Valheim/): unzip it into the Valheim folder so `BepInEx/` sits next to the Valheim executable.
2. Put `Jotunn.dll` from [Jötunn 2.30.0](https://thunderstore.io/c/valheim/p/ValheimModding/Jotunn/) into `BepInEx/plugins/Jotunn/`.
3. Put `InvisibilityPotion.dll` (from the `plugins` folder of the mod zip) into `BepInEx/plugins/InvisibilityPotion/`.

Result:

```
Valheim/
└── BepInEx/
    ├── config/saikopsych.InvisibilityPotion.cfg   (created on first start)
    └── plugins/
        ├── Jotunn/Jotunn.dll
        └── InvisibilityPotion/InvisibilityPotion.dll
```

Linux players start Valheim with the Steam launch option `./start_game_bepinex.sh %command%`.

## Servers and the Jötunn version

- The mod must run on the server. See [Multiplayer and Server](05-Multiplayer-and-Server) for a dedicated server.
- Server and clients need the **same Jötunn version, down to the patch number**. The mod is built against Jötunn 2.30.0. A client with 2.30.2 cannot join a server with 2.30.0, and the other way round.
- Server and clients also need the same InvisibilityPotion major.minor version (0.3.x with 0.3.y is fine).

## Check that it loaded

Open `BepInEx/LogOutput.log` in the Valheim folder (or use "View logs" in your mod manager) and look for:

```
Patch health: 24 targets patched, 0 missing
```

"0 missing" means every game hook was found. If some are missing, another mod or a game update changed something. Please report it with the log on the [issue tracker](https://github.com/saiko-psych/InvisibilityPotion/issues).

## First steps

Brew a mead base at the **Mead Ketill**, ferment it, drink it. Find the ingredients with veil goggles (crafted at the Forge). See [Veil Meads](02-Veil-Meads) and [Plants and Goggles](03-Plants-and-Goggles).
