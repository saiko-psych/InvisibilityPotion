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
        private const int MaxCacheEntries = 256;
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
            if (_cache.Count >= MaxCacheEntries) _cache.Clear();   // every player spawn is a new ZDOID; keep a long server session bounded
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

        /// <summary>
        /// Closest player within <paramref name="range"/> that enemies may target: not ignored by enemies, not in debug fly or ghost mode
        /// (the two exclusions vanilla's FindEnemy fallback applies, BaseAI.cs:1419). Null when there is none. No allocation.
        /// </summary>
        public static Player ClosestTargetablePlayer(Vector3 position, float range)
        {
            Player best = null;
            var bestSq = range * range;
            foreach (var p in Player.GetAllPlayers())
            {
                if (p == null || p.InDebugFlyMode() || p.InGhostMode() || IsIgnoredByEnemies(p)) continue;
                var d = (p.transform.position - position).sqrMagnitude;
                if (d <= bestSq) { bestSq = d; best = p; }
            }
            return best;
        }
    }
}
