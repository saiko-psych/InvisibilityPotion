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
            var configured = PluginConfig.Global.BodyVeilMode;
            if (!FogVeil.TryParseMode(configured, out var mode))
                Plugin.Log.LogWarning($"BodyVeilMode '{configured}' is not one of Off, Cutoff, Hide, Tint, Ghost, Distortion; using Hide");
            FogVeil.CurrentMode = mode;
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

        /// <summary>Restores and re-applies the veil on every veiled player (after a body-mode switch).</summary>
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
            foreach (var p in Player.GetAllPlayers())
            {
                if (p == null) continue;
                seen.Add(p);
                var tier = HiddenState.HiddenTier(p);
                if (tier > 0) { _veil.Apply(p, tier, p == Player.m_localPlayer); _veiled.Add(p); }
                else if (_veiled.Remove(p)) _veil.Remove(p);
            }
            _veiled.RemoveWhere(p => { if (p == null || !seen.Contains(p)) { _veil.Remove(p); return true; } return false; });
            _veil.PruneDead();
        }
    }
}
