using System.Collections.Generic;
using InvisibilityPotion.Goggles;
using UnityEngine;

namespace InvisibilityPotion.Plants
{
    /// <summary>
    /// Per-plant viewer gate and stage model swap (spec plan 5 §3.1). Hidden = every Renderer, Collider, Light, ParticleSystem
    /// and AudioSource below this object is off: no draw, no shadow, no hover, no interaction, no blocked ray (a disabled
    /// collider is invisible to Physics.Raycast; Player.FindHoverObject, Player.cs:4259-4300). Revealed = the parts of the active
    /// stage plus the parts outside every stage are on. Never SetActive (enabled flags survive SetActive cycles, research §3).
    /// The parts are cached in Awake and switched off at once, so a fresh instance is hidden before its first frame (fail closed);
    /// <see cref="VeilSightDriver"/> reveals it on its next tick and <see cref="Start"/> applies the current state right away.
    /// </summary>
    public sealed class VeilSight : MonoBehaviour
    {
        /// <summary>Every live sight (driver iteration, ip_plants).</summary>
        public static readonly HashSet<VeilSight> All = new HashSet<VeilSight>();

        /// <summary>Goggle level needed (1..3); 0 = always visible.</summary>
        public int RequiredLevel = 1;
        /// <summary>Metres from the local player within which the plant is shown; 0 = no limit. Lichen: [Plants] LichenRevealDistance.</summary>
        public float MaxDistance;
        /// <summary>When true, MaxDistance is read live from [Plants] LichenRevealDistance.</summary>
        public bool UseLichenDistance;
        /// <summary>Stage roots: index = stage - 1 (lichen S1..S3; ground plants picked, ripe). Empty = one stage, everything common.</summary>
        public GameObject[] Stages = new GameObject[0];

        /// <summary>Stage index (0-based) to show; set by VeilHarvest, applied on the driver's next tick.</summary>
        [System.NonSerialized] public int ActiveStage;

        public bool Revealed { get; private set; }

        private struct Part
        {
            public int Stage;   // -1 = common (outside every stage root)
            public Component C;
        }

        private readonly List<Part> _parts = new List<Part>();
        private bool _applied;
        private bool _lastRevealed;
        private int _lastStage = -1;

        private void Awake()
        {
            Cache();
            ActiveStage = Stages.Length > 0 ? Stages.Length - 1 : 0;   // ripe until VeilHarvest says otherwise
            Apply(false, ActiveStage);
            All.Add(this);
        }

        private void Start()
        {
            var p = Player.m_localPlayer;
            Evaluate(GogglesLevel.Local, p != null, p != null ? p.transform.position : Vector3.zero, true);
        }

        private void OnDestroy() => All.Remove(this);

        private void Cache()
        {
            _parts.Clear();
            Add<Renderer>();
            Add<Collider>();
            Add<Light>();
            Add<ParticleSystem>();
            Add<AudioSource>();
        }

        private void Add<T>() where T : Component
        {
            foreach (var c in GetComponentsInChildren<T>(true)) _parts.Add(new Part { Stage = StageOf(c.transform), C = c });
        }

        private int StageOf(Transform t)
        {
            for (var cur = t; cur != null && cur != transform.parent; cur = cur.parent)
                for (var i = 0; i < Stages.Length; i++)
                    if (Stages[i] != null && cur == Stages[i].transform) return i;
            return -1;
        }

        /// <summary>Driver tick: computes the reveal and applies it when something changed (or when forced).</summary>
        public void Evaluate(int level, bool hasViewer, Vector3 viewer, bool force)
        {
            var revealed = hasViewer && GoggleLevel.Reveals(level, RequiredLevel);
            var max = UseLichenDistance ? Config.PluginConfig.LichenRevealDistance : MaxDistance;
            if (revealed && max > 0f && (transform.position - viewer).sqrMagnitude > max * max) revealed = false;
            var stage = Stages.Length > 0 ? Mathf.Clamp(ActiveStage, 0, Stages.Length - 1) : 0;
            if (!force && _applied && revealed == _lastRevealed && stage == _lastStage) return;
            Apply(revealed, stage);
        }

        private void Apply(bool revealed, int stage)
        {
            foreach (var part in _parts)
            {
                if (part.C == null) continue;
                var on = revealed && (part.Stage < 0 || part.Stage == stage);
                switch (part.C)
                {
                    case Renderer r: r.enabled = on; break;
                    case Collider col: col.enabled = on; break;
                    case Light l: l.enabled = on; break;
                    case ParticleSystem ps:
                        if (on) { if (!ps.isPlaying) ps.Play(false); }
                        else if (ps.isPlaying || ps.particleCount > 0) ps.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                        break;
                    case AudioSource a:
                        if (on) { if (!a.isPlaying && a.clip != null) a.Play(); }
                        else if (a.isPlaying) a.Stop();
                        break;
                }
            }
            Revealed = revealed;
            _lastRevealed = revealed;
            _lastStage = stage;
            _applied = true;
        }
    }
}
