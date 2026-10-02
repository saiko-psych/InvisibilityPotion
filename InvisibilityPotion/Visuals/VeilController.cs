using System;
using System.Collections.Generic;
using InvisibilityPotion.Config;
using InvisibilityPotion.Net;
using UnityEngine;

namespace InvisibilityPotion.Visuals
{
    /// <summary>
    /// Drives the veil for every player from ZDO state, so local and remote players look the same. Attached to the plugin GameObject.
    /// <see cref="Refresh"/> is the only caller of <see cref="FogVeil.Apply"/>: it re-reads <see cref="HiddenState.HiddenTier"/>
    /// for every player and removes the veil for tier 0 before anything is (re)built. Every look change (console, tuning window,
    /// config reload) goes through <see cref="ForceRefresh"/> / <see cref="ForceRefreshAll"/>, which only Remove and then Refresh,
    /// so tuning can never bring back a veil whose effect has ended.
    /// </summary>
    public sealed class VeilController : MonoBehaviour
    {
        private static VeilController _instance;
        private readonly FogVeil _veil = new FogVeil();   // concrete: SuspendBody/ResumeBody/HasVeil/... are not part of IVeil
        private readonly HashSet<Player> _seen = new HashSet<Player>();
        private readonly HashSet<Player> _veiled = new HashSet<Player>();
        private readonly HashSet<string> _loggedErrors = new HashSet<string>();
        private System.Predicate<Player> _removeUnseen;
        private float _timer;

        private void Awake()
        {
            _instance = this;
            _removeUnseen = RemoveIfUnseen;
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

        /// <summary>
        /// Rebuilds one player's veil (UpdateLodgroup postfix after an equipment change). Order audit (task 10f): vanilla calls
        /// UpdateLodgroup only after every Set*Equipped of the update has run (VisEquipment.cs:812-831), so the new item renderers
        /// already carry their vanilla materials. Remove restores the old renderers (destroyed ones are skipped, renderers vanilla
        /// re-materialised are left alone), then Refresh → Apply snapshots the new renderers' current, vanilla materials. No delay
        /// is needed. The body renderer is not recreated; its armour textures are kept correct by SuspendBody/ResumeBody around
        /// SetChestEquipped/SetLegEquipped (Patches/VisualPatches.cs).
        /// </summary>
        public static void ForceRefresh(Player p)
        {
            if (_instance == null || p == null) return;
            _instance._veil.Remove(p);
            _instance._veiled.Remove(p);
            _instance.Refresh();
        }

        /// <summary>Removes every veil and lets Refresh re-apply from the current state (after a look change).</summary>
        public static void ForceRefreshAll()
        {
            if (_instance == null) return;
            _instance._veil.RemoveAll();
            _instance._veiled.Clear();
            _instance.Refresh();
        }

        /// <summary>A look value changed (console, tuning window): re-tune shared materials, then remove and re-apply from state.</summary>
        public static void LookChanged()
        {
            FogVeil.Bump();
            ForceRefreshAll();
        }

        /// <summary>Prefix of an armour write into the body material: hands the original body materials back (see FogVeil.SuspendBody).</summary>
        public static bool SuspendBody(VisEquipment ve)
        {
            if (_instance == null || ve == null || ve.m_bodyModel == null) return false;
            var p = ve.GetComponent<Player>();
            return p != null && _instance._veil.SuspendBody(p, ve.m_bodyModel);
        }

        /// <summary>Postfix of <see cref="SuspendBody"/>: snapshots the updated body materials and swaps again.</summary>
        public static void ResumeBody(VisEquipment ve)
        {
            if (_instance == null || ve == null) return;
            var p = ve.GetComponent<Player>();
            if (p != null) _instance._veil.ResumeBody(p);
        }

        /// <summary>Live particle count per emitter of a player's veil (Debug tuning window).</summary>
        public static void CollectParticleCounts(Player p, List<KeyValuePair<string, int>> into)
        {
            if (_instance == null) { into.Clear(); return; }
            _instance._veil.CollectParticleCounts(p, into);
        }

#if DEBUG
        /// <summary>Every fact about a player's fog emitters (ip_fog dump, ip_state). Empty when the player has no veil.</summary>
        public static void DumpFog(Player p, List<string> into)
        {
            if (_instance == null) { into.Clear(); return; }
            _instance._veil.DumpFog(p, into);
        }
#endif

        private bool RemoveIfUnseen(Player p)
        {
            if (p != null && _seen.Contains(p)) return false;
            _veil.Remove(p);
            return true;
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
                if (!TierRange.IsValid(tier))
                {
                    // Keyed on the veil's own record, not on _veiled: a veil can never outlive its effect, whatever path built it.
                    _veiled.Remove(p);
                    if (_veil.HasVeil(p)) _veil.Remove(p);
                    _veil.SweepStrayFog(p);
                    continue;
                }
                var isLocal = p == local;
                // A remote player hidden from players must not be drawn here either (the listen host gets the real position,
                // the position spoof only applies to forwarded packets).
                var forceHide = !isLocal && HiddenState.IsHiddenFromPlayers(p);
                try
                {
                    _veil.Apply(p, tier, isLocal, forceHide);
                    _veiled.Add(p);
                }
                catch (Exception e)
                {
                    // A half-built veil is removed at once instead of being retried (and leaking fog) every refresh.
                    if (_loggedErrors.Add(e.GetType().Name + e.Message)) Plugin.Log.LogError($"veil apply T{tier} on {p.GetPlayerName()} failed: {e}");
                    _veil.Remove(p);
                    _veiled.Remove(p);
                }
            }
            _veiled.RemoveWhere(_removeUnseen);
            _veil.PruneDead();
        }
    }
}
