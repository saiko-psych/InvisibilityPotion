using System.Collections.Generic;
using InvisibilityPotion.Config;
using InvisibilityPotion.Net;
using UnityEngine;

namespace InvisibilityPotion.Visuals
{
    /// <summary>Drives the veil for every player from ZDO state, so local and remote players look the same. Attached to the plugin GameObject.</summary>
    public sealed class VeilController : MonoBehaviour
    {
        private static VeilController _instance;
        private readonly FogVeil _veil = new FogVeil();
        private readonly HashSet<Player> _seen = new HashSet<Player>();
        private readonly HashSet<Player> _veiled = new HashSet<Player>();
        private float _timer;

        private void Awake()
        {
            _instance = this;
            FogVeil.LoadFromConfig();
            PluginConfig.Changed += OnConfigChanged;
        }

        private void OnDestroy()
        {
            PluginConfig.Changed -= OnConfigChanged;
            if (_instance == this) _instance = null;
        }

        /// <summary>Startup, ip_reload_config, server sync: re-read the look and re-apply it.</summary>
        private static void OnConfigChanged()
        {
            FogVeil.LoadFromConfig();
            ForceRefreshAll();
        }

        private void Update()
        {
            _timer += Time.deltaTime;
            if (_timer < 0.25f) return;
            _timer = 0f;
            Refresh();
        }

        public static void ForceRefresh(Player p)
        {
            if (_instance == null || p == null) return;
            _instance._veil.Remove(p);
            _instance._veiled.Remove(p);
            _instance.Refresh();
        }

        /// <summary>Restores and re-applies the veil on every veiled player (after a look change).</summary>
        public static void ForceRefreshAll()
        {
            if (_instance == null) return;
            foreach (var p in _instance._veiled) _instance._veil.Remove(p);
            _instance._veiled.Clear();
            _instance.Refresh();
        }

        private void Refresh()
        {
            var seen = _seen;
            seen.Clear();
            var local = Player.m_localPlayer;
            foreach (var p in Player.GetAllPlayers())
            {
                if (p == null) continue;
                seen.Add(p);
                var tier = HiddenState.HiddenTier(p);
                if (tier > 0)
                {
                    var isLocal = p == local;
                    // A remote player hidden from players must not be drawn here either (the listen host gets the real position,
                    // the position spoof only applies to forwarded packets).
                    var forceHide = !isLocal && HiddenState.IsHiddenFromPlayers(p);
                    _veil.Apply(p, tier, isLocal, forceHide);
                    _veiled.Add(p);
                }
                else if (_veiled.Remove(p)) _veil.Remove(p);
            }
            _veiled.RemoveWhere(p => { if (p == null || !seen.Contains(p)) { _veil.Remove(p); return true; } return false; });
            _veil.PruneDead();
        }
    }
}
