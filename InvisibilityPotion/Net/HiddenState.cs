using System.Collections.Generic;
using InvisibilityPotion.Config;
using UnityEngine;

namespace InvisibilityPotion.Net
{
    /// <summary>The replicated "who is hidden" state. Written by the owning client from SE_Invisibility, read by every patch on every peer. Pattern: Player.IsDebugFlying (decompile-notes §ZDO).</summary>
    public static class HiddenState
    {
        public static readonly int HashTier = "IP_Tier".GetStableHashCode();
        public static readonly int HashHidden = "IP_Hidden".GetStableHashCode();

        private const float CacheSeconds = 0.2f;
        private struct Entry { public float Time; public int Tier; public bool Hidden; }
        private static readonly Dictionary<ZDOID, Entry> _cache = new Dictionary<ZDOID, Entry>();

        public static (int tier, bool hidden) Get(Character c)
        {
            if (c == null || !(c is Player)) return (0, false);
            var nview = c.m_nview;
            if (nview == null || !nview.IsValid()) return (0, false);
            var zdo = nview.GetZDO();
            var id = zdo.m_uid;
            var now = Time.time;
            if (_cache.TryGetValue(id, out var e) && now - e.Time < CacheSeconds) return (e.Tier, e.Hidden);
            var tier = zdo.GetInt(HashTier, 0);
            var hidden = zdo.GetBool(HashHidden, false);
            _cache[id] = new Entry { Time = now, Tier = tier, Hidden = hidden };
            return (tier, hidden);
        }

        public static int HiddenTier(Character c)
        {
            var (tier, hidden) = Get(c);
            return hidden ? TierRange.Clamp(tier) : 0; // out-of-range tiers (crafted ZDO, newer mod version) count as not hidden
        }

        public static bool IsIgnoredByEnemies(Character c)
        {
            var tier = HiddenTier(c);
            return tier > 0 && PluginConfig.Tier(tier).IgnoredByEnemies;
        }

        /// <summary>Tier III: other players cannot see this player (nameplate, map pin, position). Server switch: Global.AllowPvpInvisibility.</summary>
        public static bool IsHiddenFromPlayers(Character c)
        {
            var tier = HiddenTier(c);
            return tier > 0 && PluginConfig.Global.AllowPvpInvisibility && PluginConfig.Tier(tier).HiddenFromPlayers;
        }

        /// <summary>Server-side check by ZDO (no Character instance needed). Reads the two keys directly; no cache, no allocation. Runs per ZDO per peer per send.</summary>
        public static bool IsHiddenFromPlayers(ZDO zdo)
        {
            if (zdo == null || !PluginConfig.Global.AllowPvpInvisibility) return false;
            var tier = zdo.GetInt(HashTier, 0);
            if (!TierRange.IsValid(tier) || !zdo.GetBool(HashHidden, false)) return false;
            return PluginConfig.Tier(tier).HiddenFromPlayers;
        }

        /// <summary>Owner side only (SE_Invisibility runs only on the owner).</summary>
        public static void Write(Player owner, int tier, bool hidden)
        {
            var nview = owner?.m_nview;
            if (nview == null || !nview.IsValid()) return;
            var zdo = nview.GetZDO();
            zdo.Set(HashTier, tier);
            zdo.Set(HashHidden, hidden);
            Invalidate(zdo.m_uid);
        }

        public static void Invalidate(ZDOID id) => _cache.Remove(id);
    }
}
