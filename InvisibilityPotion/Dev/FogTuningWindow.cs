#if DEBUG
using System;
using System.Collections.Generic;
using System.Globalization;
using HarmonyLib;
using InvisibilityPotion.Visuals;
using UnityEngine;
using Cfg = InvisibilityPotion.Config;

namespace InvisibilityPotion.Dev
{
    /// <summary>
    /// Debug-only IMGUI window for live veil tuning (ip_fogui). Edits FogVeil's per-tier look in memory and re-applies it
    /// through VeilController.LookChanged (debounced while a slider is dragged); Save writes [Fog.TierN]/[Veil] (and the
    /// body mode of a tier with an override), Reset re-reads the file. While the window owns the mouse, the game camera
    /// releases the cursor and player input is blocked (patch below); F7 hands the mouse back to the game and returns it.
    /// </summary>
    internal sealed class FogTuningWindow : MonoBehaviour
    {
        private const int WindowId = 0x1F06;
        private const float Width = 420f;
        private const float ApplyDelay = 0.2f;
        private const KeyCode MouseToggleKey = KeyCode.F7;
        private static readonly string[] Tabs = { "Tier 1", "Tier 2", "Tier 3" };
        private static readonly BodyVeilMode[] Modes = (BodyVeilMode[])Enum.GetValues(typeof(BodyVeilMode));

        private static FogTuningWindow _instance;

        private bool _open;
        private bool _mouseToWindow = true;
        private bool _savedCapture = true;
        private Rect _rect = new Rect(40f, 40f, Width, 200f);
        private int _tab;
        private Vector2 _scroll;
        private bool _showAnchors = true;
        private bool _dirty;
        private float _dirtySince;
        private string _status = "";
        private float _countsTimer;
        private readonly List<KeyValuePair<string, int>> _counts = new List<KeyValuePair<string, int>>();
        private GUIStyle _warn;
        private static GUIStyle _bold;

        /// <summary>True while the window is open and owns the mouse: player input is blocked.</summary>
        public static bool BlocksGameInput => _instance != null && _instance._open && _instance._mouseToWindow;

        public static bool Toggle()
        {
            if (_instance == null) return false;
            _instance.SetOpen(!_instance._open);
            return _instance._open;
        }

        private void Awake() => _instance = this;

        private void OnDestroy()
        {
            if (_open) SetOpen(false);
            if (_instance == this) _instance = null;
        }

        private void SetOpen(bool open)
        {
            if (open == _open) return;
            _open = open;
            var cam = GameCamera.instance;
            if (open)
            {
                var t = DevCommands.CurrentTier();
                if (t >= 1 && t <= 3) _tab = t - 1;
                _savedCapture = cam == null || cam.m_mouseCapture;
                _mouseToWindow = true;
                ApplyMouse();
            }
            else
            {
                if (_dirty) ApplyNow();
                if (cam != null) cam.m_mouseCapture = _savedCapture;   // the camera locks the cursor again on its next update
            }
        }

        // ---------- per frame ----------

        private void Update()
        {
            if (!_open) return;
            if (ZInput.GetKeyDown(MouseToggleKey))
            {
                _mouseToWindow = !_mouseToWindow;
                var cam = GameCamera.instance;
                if (!_mouseToWindow && cam != null) cam.m_mouseCapture = _savedCapture;
            }
            if (_dirty && Time.unscaledTime - _dirtySince >= ApplyDelay) ApplyNow();
            _countsTimer -= Time.unscaledDeltaTime;
            if (_countsTimer <= 0f)
            {
                _countsTimer = 0.5f;
                VeilController.CollectParticleCounts(Player.m_localPlayer, _counts);
            }
            ApplyMouse();
        }

        // GameCamera.UpdateMouseCapture re-locks the cursor every frame while m_mouseCapture is set; re-apply after it.
        private void LateUpdate()
        {
            if (_open) ApplyMouse();
        }

        private void ApplyMouse()
        {
            if (!_mouseToWindow) return;
            var cam = GameCamera.instance;
            if (cam != null) cam.m_mouseCapture = false;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void OnGUI()
        {
            if (!_open) return;
            if (_mouseToWindow)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            if (_bold == null) _bold = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold };
            if (_warn == null) _warn = new GUIStyle(GUI.skin.label) { normal = { textColor = new Color(1f, 0.55f, 0.35f) }, wordWrap = true };
            _rect = GUILayout.Window(WindowId, _rect, DrawWindow, "Veil tuning (ip_fogui)", GUILayout.Width(Width));
        }

        // ---------- window ----------

        private void DrawWindow(int id)
        {
            var current = DevCommands.CurrentTier();
            _tab = GUILayout.Toolbar(_tab, Tabs);
            var tier = _tab + 1;
            var s = FogVeil.Fog[tier];
            GUILayout.Label($"Active effect: {(current == 0 ? "none" : "T" + current)}   |   {MouseToggleKey}: mouse to {(_mouseToWindow ? "game" : "window")}");

            var height = Mathf.Clamp(Screen.height - 220f, 200f, 640f);
            _scroll = GUILayout.BeginScrollView(_scroll, GUILayout.Height(height));

            Header("Fog");
            s.Enabled = Toggle("Enabled", s.Enabled);
            s.DynamicColor = Toggle("Dynamic colour (50% environment fog)", s.DynamicColor);
            s.Rate = Slider("Rate /s", s.Rate, 0f, 30f);
            s.Size = Slider("Size m", s.Size, 0.1f, 3f);
            s.Lifetime = Slider("Lifetime s", s.Lifetime, 0.2f, 8f);
            s.Speed = Slider("Speed m/s", s.Speed, 0f, 1f);
            s.Alpha = Slider("Alpha", s.Alpha, 0f, 1f);
            s.R = Slider("Colour R", s.R, 0f, 1f);
            s.G = Slider("Colour G", s.G, 0f, 1f);
            s.B = Slider("Colour B", s.B, 0f, 1f);
            s.SpreadX = Slider("Spread X", s.SpreadX, 0.1f, 3f);
            s.SpreadY = Slider("Spread Y", s.SpreadY, 0.1f, 3f);
            s.SpreadZ = Slider("Spread Z", s.SpreadZ, 0.1f, 3f);
            s.Drift = Slider("Drift m/s", s.Drift, -0.5f, 0.5f);
            GUILayout.Label($"~{s.LiveParticlesInner:0} live particles per emitter (rate x lifetime){(s.OuterEnabled ? $", outer ~{s.LiveParticlesOuter:0}" : "")}");
            if (s.ExceedsParticleBudget)
                GUILayout.Label($"Over {FogSettings.ParticleWarnThreshold} particles per emitter: heavy and capped at {FogSettings.ParticleHardCap}. Lower rate or lifetime.", _warn);

            Header("Outer layer (head, chest, hips)");
            s.OuterEnabled = Toggle("Outer layer", s.OuterEnabled);
            s.OuterRadiusMultiplier = Slider("Radius x", s.OuterRadiusMultiplier, 1f, 5f);
            s.OuterAlphaFactor = Slider("Alpha x", s.OuterAlphaFactor, 0f, 1f);
            s.OuterRateFactor = Slider("Rate x", s.OuterRateFactor, 0f, 2f);
            s.OuterSizeFactor = Slider("Size x", s.OuterSizeFactor, 0.5f, 3f);

            Header("Look");
            DrawModes(tier);
            s.DistortionStrength = Slider("Distortion", s.DistortionStrength, 0f, 0.5f);
            s.DA = Slider("Distortion alpha", s.DA, 0f, 0.5f);

            GUILayout.BeginHorizontal();
            Header("Anchors");
            GUILayout.FlexibleSpace();
            if (GUILayout.Button(_showAnchors ? "hide" : "show", GUILayout.Width(60f))) _showAnchors = !_showAnchors;
            GUILayout.EndHorizontal();
            if (_showAnchors) DrawAnchors(s, tier == current);

            GUILayout.EndScrollView();
            DrawButtons(tier);
            if (_status.Length > 0) GUILayout.Label(_status);
            GUI.DragWindow(new Rect(0f, 0f, 10000f, 22f));
        }

        private void DrawModes(int tier)
        {
            var active = FogVeil.ModeFor(tier);
            var ov = FogVeil.ModeOverride[tier];
            GUILayout.Label($"Body mode: {active} ({(ov.HasValue ? "override" : "config")}; config {FogVeil.ConfiguredMode(tier)})");
            GUILayout.BeginHorizontal();
            foreach (var m in Modes)
            {
                var label = m == active ? $"[{m}]" : m.ToString();
                if (GUILayout.Button(label)) { FogVeil.ModeOverride[tier] = m; MarkDirty(); }
            }
            GUILayout.EndHorizontal();
            if (ov.HasValue && GUILayout.Button("Use config mode")) { FogVeil.ModeOverride[tier] = null; MarkDirty(); }
        }

        private void DrawAnchors(FogSettings s, bool live)
        {
            foreach (var a in s.Anchors)
            {
                GUILayout.BeginHorizontal();
                GUI.changed = false;
                var on = GUILayout.Toggle(a.Enabled, a.Name, GUILayout.Width(120f));
                if (GUI.changed) { a.Enabled = on; MarkDirty(); }
                GUILayout.Label(live ? Count(a.Name) : "", GUILayout.Width(80f));
                GUILayout.EndHorizontal();
                if (!a.Enabled) continue;
                a.Radius = Slider("   radius", a.Radius, 0f, 0.6f);
                a.X = Slider("   offset x", a.X, -0.5f, 0.5f);
                a.Y = Slider("   offset y", a.Y, -0.5f, 0.5f);
                a.Z = Slider("   offset z", a.Z, -0.5f, 0.5f);
            }
            if (!live) GUILayout.Label("Particle counts show for the tier of your active effect.");
        }

        private string Count(string anchor)
        {
            int inner = -1, outer = -1;
            foreach (var kv in _counts)
            {
                if (kv.Key == anchor) inner = kv.Value;
                else if (kv.Key == anchor + " (outer)") outer = kv.Value;
            }
            if (inner < 0) return "-";
            return outer < 0 ? $"{inner} p" : $"{inner}+{outer} p";
        }

        private void DrawButtons(int tier)
        {
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Apply now")) ApplyNow();
            if (GUILayout.Button("Save")) Save();
            if (GUILayout.Button("Reset")) Reset();
            if (GUILayout.Button($"Give T{tier}")) Give(tier);
            if (GUILayout.Button("Close")) SetOpen(false);
            GUILayout.EndHorizontal();
        }

        // ---------- actions ----------

        private void MarkDirty()
        {
            if (!_dirty) _dirtySince = Time.unscaledTime;
            _dirty = true;
        }

        private void ApplyNow()
        {
            _dirty = false;
            VeilController.LookChanged();
        }

        private void Save()
        {
            for (var t = 1; t <= 3; t++)
            {
                Cfg.PluginConfig.SaveFog(t, FogVeil.Fog[t], FogVeil.AlphaMode);
                var ov = FogVeil.ModeOverride[t];
                if (ov.HasValue) Cfg.PluginConfig.SaveBodyVeilMode(t, ov.Value.ToString());
            }
            Cfg.PluginConfig.SaveGhostLook(FogVeil.FormatColor(FogVeil.GhostColor), FogVeil.GhostEmission);
            Say("saved [Fog.Tier1..3], [Veil] and overridden body modes to the config file");
        }

        private void Reset()
        {
            for (var t = 1; t <= 3; t++) FogVeil.ModeOverride[t] = null;
            _dirty = false;
            Cfg.PluginConfig.Reload();   // raises Changed: VeilController reloads the look and re-applies it
            Say("reloaded the config file; body mode overrides cleared");
        }

        private void Give(int tier)
        {
            var p = Player.m_localPlayer;
            if (p == null) { Say("no local player"); return; }
            if (_dirty) ApplyNow();
            var se = p.GetSEMan().AddStatusEffect(Effects.StatusEffects.NameHash(tier), resetTime: true);
            Say(se != null ? $"applied T{tier}" : $"T{tier} not applied (already active or not registered)");
        }

        private void Say(string line)
        {
            _status = line;
            Plugin.Log.LogInfo($"[ip_fogui] {line}");
        }

        // ---------- controls ----------

        private static void Header(string text) => GUILayout.Label(text, _bold);

        private float Slider(string label, float value, float min, float max)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.Width(110f));
            GUI.changed = false;
            var v = GUILayout.HorizontalSlider(value, min, max);
            var changed = GUI.changed;   // only a user drag counts; an out-of-range config value is not clamped silently
            GUILayout.Label(value.ToString("0.###", CultureInfo.InvariantCulture), GUILayout.Width(44f));
            GUILayout.EndHorizontal();
            if (!changed || Mathf.Abs(v - value) < 1e-6f) return value;
            MarkDirty();
            return v;
        }

        private bool Toggle(string label, bool value)
        {
            GUI.changed = false;
            var v = GUILayout.Toggle(value, label);
            if (!GUI.changed || v == value) return value;
            MarkDirty();
            return v;
        }
    }

    /// <summary>While the tuning window owns the mouse, the player neither moves, attacks nor turns the camera (Debug only).</summary>
    [HarmonyPatch(typeof(PlayerController), "TakeInput")]
    internal static class FogTuningInputBlockPatch
    {
        [HarmonyPostfix]
        private static void Postfix(ref bool __result)
        {
            if (FogTuningWindow.BlocksGameInput) __result = false;
        }
    }
}
#endif
