using System;
using UnityEngine;

namespace InvisibilityPotion.Goggles
{
    /// <summary>
    /// The local player's veil goggle level (spec plan 5 §3.5). Polled every 0.5 s by <see cref="Plants.VeilSightDriver"/> from the
    /// helmet slot (Humanoid.m_helmetItem, Humanoid.cs:85; ItemData.m_dropPrefab, ItemDrop.cs:450): no patch, no status effect.
    /// On a change it raises <see cref="Changed"/> and writes IP_Goggles (int) on the local player's ZDO (owner write, like
    /// HiddenState.Write) so the server can lift the tier III position spoof for level III wearers.
    /// </summary>
    public static class GogglesLevel
    {
        public static readonly int HashGoggles = "IP_Goggles".GetStableHashCode();

        /// <summary>Current level 0..3 of the local player (0 without a player).</summary>
        public static int Local { get; private set; }

#if DEBUG
        /// <summary>Debug override (ip_goggles); null = read the helmet slot.</summary>
        public static int? DevOverride { get; set; }
#else
        private static int? DevOverride => null;
#endif

        /// <summary>The helmet-slot level before the override (for ip_state).</summary>
        public static int FromHelmet { get; private set; }

        public static event Action<int> Changed;

        private static ZDOID _writtenFor = ZDOID.None;
        private static int _written = -1;
        private static bool _loggedFirst;

        /// <summary>Reads the helmet slot and applies a change. Cheap: one field chain and a string compare.</summary>
        public static void Tick()
        {
            var p = Player.m_localPlayer;
            var helmet = p != null ? p.m_helmetItem : null;
            FromHelmet = GoggleLevel.LevelOf(helmet?.m_dropPrefab != null ? helmet.m_dropPrefab.name : null);
            var level = p == null ? 0 : GoggleLevel.Clamp(DevOverride ?? FromHelmet);
            if (p == null) _loggedFirst = false;   // log again after the next spawn/login
            if (p != null && !_loggedFirst)
            {
                _loggedFirst = true;
                // S8: proves whether the helmet slot is filled at the first tick after spawn (no re-equip hook needed).
                Plugin.Log.LogInfo($"goggles: first tick with a local player: helmet {(helmet?.m_dropPrefab != null ? helmet.m_dropPrefab.name : "none")}, level {level}");
            }
            if (level != Local)
            {
                var old = Local;
                Local = level;
                Plugin.Log.LogInfo($"goggles: level {old} -> {level}{(DevOverride.HasValue ? " (ip_goggles override)" : "")}");
                try { Changed?.Invoke(level); }
                catch (Exception e) { Plugin.Log.LogError($"goggles: Changed handler failed: {e}"); }
            }
            Write(p, level);
        }

        /// <summary>Writes IP_Goggles when the value or the player ZDO (respawn, relog) changed.</summary>
        private static void Write(Player p, int level)
        {
            var nview = p != null ? p.m_nview : null;
            if (nview == null || !nview.IsValid() || !nview.IsOwner()) return;
            var zdo = nview.GetZDO();
            if (zdo.m_uid == _writtenFor && level == _written) return;
            zdo.Set(HashGoggles, level);
            _writtenFor = zdo.m_uid;
            _written = level;
        }

        /// <summary>The level a player ZDO claims (server side for HeaderPosition, ip_state). 0 for null.</summary>
        public static int Read(ZDO zdo) => zdo == null ? 0 : GoggleLevel.Clamp(zdo.GetInt(HashGoggles, 0));

        /// <summary>The local viewer sees tier-III-hidden players (level III and the server switch).</summary>
        public static bool LocalSeesHiddenPlayers => GoggleLevel.SeesHiddenPlayers(Local, Config.PluginConfig.RevealHiddenPlayers);
    }
}
