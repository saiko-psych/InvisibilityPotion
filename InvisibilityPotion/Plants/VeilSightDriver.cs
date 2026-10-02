using System;
using System.Collections.Generic;
using InvisibilityPotion.Goggles;
using UnityEngine;

namespace InvisibilityPotion.Plants
{
    /// <summary>
    /// One ticker on the plugin GameObject (like VeilController): every 0.5 s it polls the goggle level and re-evaluates every
    /// registered <see cref="VeilSight"/> (plants in loaded zones only). A sight is re-applied when its (revealed, stage) pair
    /// changed, and every tenth tick regardless (cheap; repairs a collider some other code re-enabled).
    /// </summary>
    public sealed class VeilSightDriver : MonoBehaviour
    {
        public const float Interval = 0.5f;
        private const int FullPassEvery = 10;
        private float _timer;
        private int _ticks;
        private readonly List<VeilSight> _snapshot = new List<VeilSight>();
        private readonly HashSet<string> _loggedErrors = new HashSet<string>();

        private void Update()
        {
            _timer += Time.deltaTime;
            if (_timer < Interval) return;
            _timer = 0f;
            _ticks++;
            try { GogglesLevel.Tick(); }
            catch (Exception e) { LogOnce("goggles", e); }
            var p = Player.m_localPlayer;
            var level = GogglesLevel.Local;
            var hasViewer = p != null;
            var viewer = hasViewer ? p.transform.position : Vector3.zero;
            var force = _ticks % FullPassEvery == 0;
            _snapshot.Clear();
            _snapshot.AddRange(VeilSight.All);
            foreach (var s in _snapshot)
            {
                if (s == null) continue;
                try { s.Evaluate(level, hasViewer, viewer, force); }
                catch (Exception e) { LogOnce("sight", e); }
            }
        }

        /// <summary>After every Update, so a queued sapling message replaces the vanilla one set in the same frame.</summary>
        private void LateUpdate()
        {
            try { TreeSapling.FlushMessage(); }
            catch (Exception e) { LogOnce("sapling message", e); }
        }

        private void LogOnce(string where, Exception e)
        {
            if (_loggedErrors.Add(where + e.GetType().Name + e.Message)) Plugin.Log.LogError($"veil sight ({where}) failed: {e}");
        }
    }
}
