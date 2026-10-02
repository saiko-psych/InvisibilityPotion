#if DEBUG
using System;
using System.Collections.Generic;
using System.Globalization;
using BepInEx.Configuration;
using HarmonyLib;
using InvisibilityPotion.Visuals;
using UnityEngine;
using Cfg = InvisibilityPotion.Config;

namespace InvisibilityPotion.Dev
{
    /// <summary>
    /// Debug-only IMGUI window for live veil tuning (ip_fogui), layout per docs/ideas/2026-10-02-imgui-tuning-ux.md. Edits
    /// FogVeil's per-tier look in memory and re-applies it through VeilController.LookChanged (debounced 0.2 s); Save writes
    /// [Fog.TierN]/[Veil] (and the body mode of a tier with an override), Reset re-reads the file. The whole window is scaled
    /// with GUI.matrix ([Dev] TuningWindowScale, 0 = screen height / 1080, at least 1); position and size are remembered in
    /// [Dev] TuningWindowRect. Every numeric value is one Row: label (yellow with * when it differs from the saved file), big
    /// slider (drag or click anywhere on it, mouse wheel steps), -, typed field (Enter or focus loss applies), +, R (default). Shift = step/10, Ctrl = step×10.
    /// While the window owns the mouse, the game camera releases the cursor and player input is blocked (patch below); F7 hands
    /// the mouse back to the game and returns it. Cursor and GameCamera.m_mouseCapture are touched only while the window is open
    /// and owns the mouse. Handing the mouse back (close, F7) waits while another IMGUI config window is open (ConfigurationManager,
    /// F1: <see cref="ExternalWindow"/>), so the camera does not re-lock the cursor under that window.
    /// </summary>
    internal sealed class FogTuningWindow : MonoBehaviour
    {
        private const int WindowId = 0x1F06;
        private const float ApplyDelay = 0.2f;
        private const float PreviewDelay = 0.6f;
        private const KeyCode MouseToggleKey = KeyCode.F7;
        private const float LabelWidth = 150f;
        private const float FieldWidth = 64f;
        private const float SmallFieldWidth = 52f;
        private const float ButtonWidth = 28f;
        private const float ThumbWidth = 16f;
        private const float RowHeight = 26f;
        private const float MinWidth = 520f;
        private const float MinHeight = 320f;
        private const float DefaultWidth = 620f;
        private const float HeaderHeight = 104f;   // title bar, tabs, info and status lines
        private const float FooterHeight = 74f;    // two button rows
        private const float GripSize = 18f;
        private const float MaxScale = 2.5f;
        private static readonly string[] Tabs = { "Tier 1", "Tier 2", "Tier 3" };
        private static readonly BodyVeilMode[] Modes = (BodyVeilMode[])Enum.GetValues(typeof(BodyVeilMode));
        private static readonly Color ChangedColor = new Color(1f, 0.85f, 0.35f);
        private static readonly FogSettings[] Defaults = { null, FogSettings.Defaults(1), FogSettings.Defaults(2), FogSettings.Defaults(3) };

        private static FogTuningWindow _instance;
        private static ConfigEntry<float> _scaleEntry;
        private static ConfigEntry<string> _rectEntry;

        private bool _open;
        private bool _mouseToWindow = true;
        private bool _savedCapture = true;
        private bool _restorePending;   // capture hand-back deferred until the external config window closes
        private Rect _rect;
        private bool _rectLoaded;
        private float _scale = 1f;
        private int _tab;
        private Vector2 _scroll;
        private bool _foldInner = true, _foldOuter = true, _foldGround = true, _foldAnchors, _foldLook = true;
        private bool _dirty;
        private float _dirtySince;
        private string _appliedAt = "-";
        private string _status = "";
        private float _countsTimer;
        private readonly List<KeyValuePair<string, int>> _counts = new List<KeyValuePair<string, int>>();
        private bool _previewFollowsTab;
        private float _previewAt = -1f;
        private bool _resizing;
        private Vector2 _resizeMouse, _resizeSize;
        private int _sliderHot;   // control id of the slider being dragged (0 = none), see Slider
        private LookState _undo;
        // Typed fields: text while a field has focus, keyed by control name; committed on Enter or when focus leaves it.
        private readonly Dictionary<string, string> _buf = new Dictionary<string, string>();
        private string _prevFocus = "";
        private string _commitId;
        private GUIStyle _bold, _warn, _wrap, _fold, _slider, _thumb, _status1, _grip;

        /// <summary>True while the window is open and owns the mouse: player input is blocked.</summary>
        public static bool BlocksGameInput => _instance != null && _instance._open && _instance._mouseToWindow;

        /// <summary>Binds [Dev] TuningWindowScale / TuningWindowRect. Called from PluginConfig.Bind before orphans are dropped.</summary>
        public static void BindConfig(ConfigFile file)
        {
            _scaleEntry = file.Bind("Dev", "TuningWindowScale", 0f,
                "Scale of the ip_fogui window (Debug builds); 0 = automatic (screen height / 1080, at least 1). Clamped to 1..2.5");
            _rectEntry = file.Bind("Dev", "TuningWindowRect", "",
                "Position and size of the ip_fogui window as x,y,width,height in unscaled window units (Debug builds); empty = default");
        }

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
                if (!_restorePending) _savedCapture = cam == null || cam.m_mouseCapture;   // still false from our last open otherwise
                _restorePending = false;
                _mouseToWindow = true;
                ApplyMouse();
            }
            else
            {
                if (_dirty) ApplyNow();
                _resizing = false;
                _sliderHot = 0;   // may run outside OnGUI: only our grab state, GUIUtility.hotControl is released on the next press
                _buf.Clear();
                _commitId = null;
                GUIUtility.keyboardControl = 0;   // a focused text field must not keep swallowing game keys
                SaveRect();
                RestoreCapture();
            }
        }

        /// <summary>Hands the mouse back to the camera (it locks the cursor again on its next update), or defers that while an
        /// external config window is open: re-locking under it is what made the cursor fight with ConfigurationManager.</summary>
        private void RestoreCapture()
        {
            if (ExternalWindow.IsOpen()) { _restorePending = true; return; }
            _restorePending = false;
            var cam = GameCamera.instance;
            if (cam != null) cam.m_mouseCapture = _savedCapture;
        }

        // ---------- per frame ----------

        private void Update()
        {
            if (!_open)
            {
                if (_restorePending) RestoreCapture();   // retried every frame until the external window is closed
                return;
            }
            if (ZInput.GetKeyDown(MouseToggleKey))
            {
                _mouseToWindow = !_mouseToWindow;
                _sliderHot = 0;
                if (_mouseToWindow) _restorePending = false;
                else RestoreCapture();
            }
            else if (!_mouseToWindow && _restorePending) RestoreCapture();
            if (_dirty && Time.unscaledTime - _dirtySince >= ApplyDelay) ApplyNow();
            if (_previewAt >= 0f && Time.unscaledTime >= _previewAt)
            {
                _previewAt = -1f;
                var tier = _tab + 1;
                if (DevCommands.CurrentTier() != tier) Give(tier);
            }
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
            EnsureStyles();
            _scale = CurrentScale();
            if (!_rectLoaded) LoadRect();
            var matrix = GUI.matrix;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(_scale, _scale, 1f));   // mouse events are transformed too
            try
            {
                HandleResize();
                if (Event.current.rawType == EventType.MouseDown) ReleaseSlider();   // a lost MouseUp must not keep a slider grabbed
                _rect = GUI.Window(WindowId, _rect, DrawWindow, "Veil tuning (ip_fogui)");
                ClampRect();
            }
            finally
            {
                GUI.matrix = matrix;
            }
        }

        // ---------- window geometry ----------

        private static float CurrentScale()
        {
            var v = _scaleEntry != null ? _scaleEntry.Value : 0f;
            if (v <= 0f) v = Screen.height / 1080f;
            return Mathf.Clamp(v, 1f, MaxScale);
        }

        private void ChangeScale(float delta)
        {
            var v = Mathf.Clamp(Mathf.Round((CurrentScale() + delta) * 10f) / 10f, 1f, MaxScale);
            if (_scaleEntry != null) _scaleEntry.Value = v;
            _scale = v;
        }

        private void LoadRect()
        {
            _rectLoaded = true;
            var screenH = Screen.height / _scale;
            _rect = new Rect(40f, 40f, DefaultWidth, Mathf.Min(900f, screenH - 80f));
            if (_rectEntry != null && FloatList.TryParse(_rectEntry.Value, 4, out var v))
                _rect = new Rect(v[0], v[1], Mathf.Max(MinWidth, v[2]), Mathf.Max(MinHeight, v[3]));
            ClampRect();
        }

        private void SaveRect()
        {
            if (_rectEntry == null || !_rectLoaded) return;
            var text = FloatList.Format(Mathf.Round(_rect.x), Mathf.Round(_rect.y), Mathf.Round(_rect.width), Mathf.Round(_rect.height));
            if (_rectEntry.Value != text) _rectEntry.Value = text;   // SaveOnConfigSet writes the file
        }

        /// <summary>Keeps the window on screen (screen size in scaled window units).</summary>
        private void ClampRect()
        {
            var w = Screen.width / _scale;
            var h = Screen.height / _scale;
            _rect.width = Mathf.Clamp(_rect.width, MinWidth, Mathf.Max(MinWidth, w));
            _rect.height = Mathf.Clamp(_rect.height, MinHeight, Mathf.Max(MinHeight, h));
            _rect.x = Mathf.Clamp(_rect.x, 0f, Mathf.Max(0f, w - _rect.width));
            _rect.y = Mathf.Clamp(_rect.y, 0f, Mathf.Max(0f, h - _rect.height));
        }

        /// <summary>Resize drag, started by the grip inside the window; handled outside it so it keeps working when the mouse leaves the window.</summary>
        private void HandleResize()
        {
            if (!_resizing) return;
            var e = Event.current;
            if (e.rawType == EventType.MouseUp)
            {
                _resizing = false;
                SaveRect();
                return;
            }
            if (e.rawType != EventType.MouseDrag) return;
            var d = e.mousePosition - _resizeMouse;
            _rect.width = Mathf.Max(MinWidth, _resizeSize.x + d.x);
            _rect.height = Mathf.Max(MinHeight, _resizeSize.y + d.y);
        }

        private void DrawGrip()
        {
            var grip = new Rect(_rect.width - GripSize - 2f, _rect.height - GripSize - 2f, GripSize, GripSize);
            GUI.Box(grip, GUIContent.none, _grip);
            var e = Event.current;
            if (e.type == EventType.MouseDown && e.button == 0 && grip.Contains(e.mousePosition))
            {
                _resizing = true;
                _resizeMouse = e.mousePosition + _rect.position;   // window-local → window units
                _resizeSize = _rect.size;
                e.Use();
            }
        }

        // ---------- window ----------

        private void DrawWindow(int id)
        {
            TrackFocus();
            var current = DevCommands.CurrentTier();

            GUILayout.BeginHorizontal();
            var tab = GUILayout.Toolbar(_tab, Tabs, GUILayout.Height(RowHeight));
            GUILayout.Space(8f);
            GUILayout.Label($"Scale {_scale.ToString("0.0", CultureInfo.InvariantCulture)}", GUILayout.Width(64f));
            if (GUILayout.Button("-", GUILayout.Width(ButtonWidth), GUILayout.Height(RowHeight))) ChangeScale(-0.1f);
            if (GUILayout.Button("+", GUILayout.Width(ButtonWidth), GUILayout.Height(RowHeight))) ChangeScale(0.1f);
            GUILayout.EndHorizontal();
            if (tab != _tab) { _tab = tab; OnTabChanged(); }
            var tier = _tab + 1;
            var s = FogVeil.Fog[tier];
            var saved = Cfg.PluginConfig.Fog(tier);
            var def = Defaults[tier];

            GUILayout.Label($"Active: {(current == 0 ? "none" : "T" + current)}  |  {MouseToggleKey}: mouse to {(_mouseToWindow ? "game" : "window")}  |  Shift fine, Ctrl coarse");
            GUILayout.Label((_dirty ? "Pending…" : $"Applied {_appliedAt}") + (_status.Length > 0 ? "   " + _status : ""), _status1);

            var bodyHeight = Mathf.Max(80f, _rect.height - HeaderHeight - FooterHeight);
            _scroll = GUILayout.BeginScrollView(_scroll, GUILayout.Height(bodyHeight));
            _foldInner = Foldout(_foldInner, "Fog inner layer");
            if (_foldInner) DrawInner(s, saved, def, tier == current);
            _foldOuter = Foldout(_foldOuter, "Wide fog (outer layer, Outer* keys)");
            if (_foldOuter) DrawOuter(s, saved, def);
            _foldGround = Foldout(_foldGround, "Fog ground field (flat patches at the feet, spread along the path)");
            if (_foldGround) DrawGround(s, saved, def);
            _foldAnchors = Foldout(_foldAnchors, "Anchors (inner bones: radius, offset x y z)");
            if (_foldAnchors) DrawAnchors(s, saved, tier == current);
            _foldLook = Foldout(_foldLook, "Look (body mode, distortion, shadow, spirit)");
            if (_foldLook) DrawLook(s, saved, def, tier);
            GUILayout.EndScrollView();

            DrawFooter(tier);
            _commitId = null;   // a commit whose row was not drawn (folded, other tab) is dropped
            DrawGrip();
            GUI.DragWindow(new Rect(0f, 0f, 10000f, 20f));
        }

        private void DrawInner(FogSettings s, FogSettings saved, FogSettings def, bool live)
        {
            ToggleRow("Enabled", ref s.Enabled, def.Enabled, saved.Enabled);
            ToggleRow("Trail (world space: particles stay behind)", ref s.Trail, def.Trail, saved.Trail);
            ToggleRow("Dynamic colour (50% environment fog, darker in rain/night)", ref s.DynamicColor, def.DynamicColor, saved.DynamicColor);
            DrawEmitterMode(s, live);
            if (s.EmitterMode == FogEmitterMode.Mesh)
            {
                Row("Mesh rate /s (0 = auto)", ref s.MeshRate, 0f, 100f, 1f, def.MeshRate, saved.MeshRate);
                Row("Mesh offset m", ref s.MeshOffset, 0f, 0.3f, 0.01f, def.MeshOffset, saved.MeshOffset);
            }
            Row(s.EmitterMode == FogEmitterMode.Mesh ? "Rate /s per bone" : "Rate /s", ref s.Rate, 0f, 30f, 0.5f, def.Rate, saved.Rate);
            Row("Size m", ref s.Size, 0.05f, 3f, 0.05f, def.Size, saved.Size);
            Row("Lifetime s", ref s.Lifetime, 0.2f, 10f, 0.25f, def.Lifetime, saved.Lifetime);
            Row("Speed m/s", ref s.Speed, 0f, 1f, 0.01f, def.Speed, saved.Speed);
            Row("Alpha", ref s.Alpha, 0f, 1f, 0.05f, def.Alpha, saved.Alpha);
            Row("Colour R", ref s.R, 0f, 1f, 0.05f, def.R, saved.R);
            Row("Colour G", ref s.G, 0f, 1f, 0.05f, def.G, saved.G);
            Row("Colour B", ref s.B, 0f, 1f, 0.05f, def.B, saved.B);
            Row("Emission (glow)", ref s.Emission, 0f, 1f, 0.05f, def.Emission, saved.Emission);
            Row("Spread X", ref s.SpreadX, 0.05f, 3f, 0.05f, def.SpreadX, saved.SpreadX);
            Row("Spread Y", ref s.SpreadY, 0.05f, 3f, 0.05f, def.SpreadY, saved.SpreadY);
            Row("Spread Z", ref s.SpreadZ, 0.05f, 3f, 0.05f, def.SpreadZ, saved.SpreadZ);
            Row("Drift m/s", ref s.Drift, -0.5f, 0.5f, 0.01f, def.Drift, saved.Drift);
            DrawFogMaterial(s);
            var inner = s.EmitterMode == FogEmitterMode.Mesh ? $"mesh ~{s.LiveParticlesMesh:0}" : $"~{s.LiveParticlesInner:0} per bone emitter";
            GUILayout.Label($"Live particles (rate x lifetime): {inner}{(s.OuterEnabled ? $", outer ~{s.LiveParticlesOuter:0} per emitter" : "")}" +
                            $"{(s.GroundEnabled ? $", ground ~{s.LiveParticlesGround:0} running" : "")}", _wrap);
            if (s.ExceedsParticleBudget)
                GUILayout.Label($"Over {FogSettings.ParticleWarnThreshold} particles per emitter: heavy and capped at {FogSettings.ParticleHardCap}. Lower rate or lifetime.", _warn);
        }

        private void DrawOuter(FogSettings s, FogSettings saved, FogSettings def)
        {
            ToggleRow("Outer layer", ref s.OuterEnabled, def.OuterEnabled, saved.OuterEnabled);
            ToggleRow("Outer trail (world space)", ref s.OuterTrail, def.OuterTrail, saved.OuterTrail);
            ToggleRow("Outer flat (quads parallel to the ground)", ref s.OuterHorizontal, def.OuterHorizontal, saved.OuterHorizontal);
            DrawOuterShape(s, saved);
            var solo = GUILayout.Toggle(FogVeil.SoloOuter, "Solo outer (inner and ground off; not saved, all tiers)", GUILayout.Height(22f));
            if (solo != FogVeil.SoloOuter) { FogVeil.SoloOuter = solo; MarkDirty(); }
            Row("Radius m", ref s.OuterRadius, 0.1f, 14f, 0.1f, def.OuterRadius, saved.OuterRadius);
            if (s.OuterShape == FogOuterShape.Volume)
                Row("Height sigma m (ground-heavy)", ref s.OuterHeightSigma, FogVolumeShape.HeightFloor, 3f, 0.05f, def.OuterHeightSigma, saved.OuterHeightSigma);
            else
                Row("Band height (x radius)", ref s.OuterSpreadY, 0.01f, 1f, 0.01f, def.OuterSpreadY, saved.OuterSpreadY);
            Row("Rotation deg/s", ref s.OuterRotation, -60f, 60f, 1f, def.OuterRotation, saved.OuterRotation);
            Row("Offset Y m", ref s.OuterOffsetY, -1f, 1f, 0.05f, def.OuterOffsetY, saved.OuterOffsetY);
            Row("Alpha", ref s.OuterAlpha, 0f, 1f, 0.005f, def.OuterAlpha, saved.OuterAlpha);
            Row("Rate /s per anchor", ref s.OuterRate, 0f, 40f, 1f, def.OuterRate, saved.OuterRate);
            Row("Rate /m walked (trail only)", ref s.OuterRateDistance, 0f, 10f, 0.5f, def.OuterRateDistance, saved.OuterRateDistance);
            Row("Burst at start (volume)", ref s.OuterBurst, 0f, 150f, 1f, def.OuterBurst, saved.OuterBurst);
            Row("Size m", ref s.OuterSize, 0.1f, 9f, 0.1f, def.OuterSize, saved.OuterSize);
            Row("Lifetime s", ref s.OuterLifetime, 0.2f, 20f, 0.25f, def.OuterLifetime, saved.OuterLifetime);
            var anchorsChanged = s.Get("OuterAnchors") != saved.Get("OuterAnchors");
            var c = GUI.color;
            if (anchorsChanged) GUI.color = ChangedColor;
            GUILayout.Label((anchorsChanged ? "* " : "") + "Outer anchors (offset from the anchor, radius above):", _wrap);
            GUI.color = c;
            var names = FogSettings.AnchorNames;
            const int perRow = 3;
            for (var i = 0; i < names.Length; i += perRow)
            {
                GUILayout.BeginHorizontal();
                for (var j = i; j < Mathf.Min(i + perRow, names.Length); j++)
                {
                    var on = s.OuterAnchors.Contains(names[j]);
                    var v = GUILayout.Toggle(on, names[j], GUILayout.Width(150f));
                    if (v == on) continue;
                    if (v) s.OuterAnchors.Add(names[j]);
                    else s.OuterAnchors.Remove(names[j]);
                    MarkDirty();
                }
                GUILayout.EndHorizontal();
            }
        }

        private void DrawGround(FogSettings s, FogSettings saved, FogSettings def)
        {
            ToggleRow("Ground field", ref s.GroundEnabled, def.GroundEnabled, saved.GroundEnabled);
            Row("Rate /s", ref s.GroundRate, 0f, 30f, 0.5f, def.GroundRate, saved.GroundRate);
            Row("Rate per metre walked", ref s.GroundRateDistance, 0f, 10f, 0.25f, def.GroundRateDistance, saved.GroundRateDistance);
            Row("Size m (at birth)", ref s.GroundSize, 0.1f, 4f, 0.05f, def.GroundSize, saved.GroundSize);
            Row("Grow x (size at death)", ref s.GroundGrow, 0.5f, 8f, 0.1f, def.GroundGrow, saved.GroundGrow);
            Row("Lifetime s", ref s.GroundLifetime, 0.5f, 20f, 0.5f, def.GroundLifetime, saved.GroundLifetime);
            Row("Alpha", ref s.GroundAlpha, 0f, 1f, 0.02f, def.GroundAlpha, saved.GroundAlpha);
            Row("Radius m", ref s.GroundRadius, 0f, 3f, 0.05f, def.GroundRadius, saved.GroundRadius);
            Row("Height m (above feet)", ref s.GroundHeight, -0.5f, 2f, 0.05f, def.GroundHeight, saved.GroundHeight);
            Row("Drift m/s (random)", ref s.GroundDrift, 0f, 0.5f, 0.01f, def.GroundDrift, saved.GroundDrift);
            GUILayout.Label($"Live particles: ~{s.LiveParticlesGround:0} while running ({FogSettings.GroundBudgetSpeed:0} m/s), max {s.GroundLowerMaxParticles} + {s.GroundUpperMaxParticles} (cap {FogSettings.GroundParticleBudget})", _wrap);
        }

        private void DrawLook(FogSettings s, FogSettings saved, FogSettings def, int tier)
        {
            DrawModes(tier);
            Row("Distortion", ref s.DistortionStrength, 0f, 0.5f, 0.01f, def.DistortionStrength, saved.DistortionStrength);
            Row("Distortion alpha (body opacity)", ref s.DA, 0f, 1f, 0.01f, def.DA, saved.DA);
            DrawWave(s, saved);

            GUILayout.Label("Shadow / Spirit (all tiers)", _bold);
            var g = Cfg.PluginConfig.Global;
            var shadow = FogVeil.ShadowColor;
            var shadowAlpha = shadow.a;
            var savedShadow = FloatList.TryParse(g.ShadowColor, 4, out var sc) ? sc[3] : FogVeil.DefaultShadowColor.a;
            if (Row("Shadow alpha", ref shadowAlpha, 0f, 0.6f, 0.02f, FogVeil.DefaultShadowColor.a, savedShadow)) { shadow.a = shadowAlpha; FogVeil.ShadowColor = shadow; }
            var strength = FogVeil.SpiritStrength;
            if (Row("Spirit strength", ref strength, 0f, 3f, 0.05f, FogVeil.DefaultSpiritStrength, g.SpiritStrength)) FogVeil.SpiritStrength = strength;
            var spirit = FogVeil.SpiritColor;
            var savedSpirit = FloatList.TryParse(g.SpiritColor, 3, out var sp) ? new Color(sp[0], sp[1], sp[2]) : FogVeil.DefaultSpiritColor;
            float r = spirit.r, gg = spirit.g, b = spirit.b;
            var changed = Row("Spirit R", ref r, 0f, 1f, 0.05f, FogVeil.DefaultSpiritColor.r, savedSpirit.r);
            changed |= Row("Spirit G", ref gg, 0f, 1f, 0.05f, FogVeil.DefaultSpiritColor.g, savedSpirit.g);
            changed |= Row("Spirit B", ref b, 0f, 1f, 0.05f, FogVeil.DefaultSpiritColor.b, savedSpirit.b);
            if (changed) FogVeil.SpiritColor = new Color(r, gg, b, 1f);
        }

        private void DrawModes(int tier)
        {
            var active = FogVeil.ModeFor(tier);
            var ov = FogVeil.ModeOverride[tier];
            var c = GUI.color;
            if (active != FogVeil.ConfiguredMode(tier)) GUI.color = ChangedColor;
            GUILayout.Label($"Body mode: {active} ({(ov.HasValue ? "override" : "config")}; config {FogVeil.ConfiguredMode(tier)})");
            GUI.color = c;
            const int perRow = 4;
            for (var i = 0; i < Modes.Length; i += perRow)
            {
                GUILayout.BeginHorizontal();
                for (var j = i; j < Mathf.Min(i + perRow, Modes.Length); j++)
                {
                    var m = Modes[j];
                    if (GUILayout.Button(m == active ? $"[{m}]" : m.ToString(), GUILayout.Height(RowHeight))) { FogVeil.ModeOverride[tier] = m; MarkDirty(); }
                }
                GUILayout.EndHorizontal();
            }
            if (tier == 3) GUILayout.Label("T3 candidates to compare: Spirit (faint light shell, keeps armour) and Shadow (dark see-through silhouette).", _wrap);
            if (ov.HasValue && GUILayout.Button("Use config mode")) { FogVeil.ModeOverride[tier] = null; MarkDirty(); }
        }

        private void DrawFogMaterial(FogSettings s)
        {
            GUILayout.Label($"Fog material: {s.FogMaterial}");
            var names = FogSettings.FogMaterialNames;
            for (var i = 0; i < names.Length; i += 2)
            {
                GUILayout.BeginHorizontal();
                for (var j = i; j < Mathf.Min(i + 2, names.Length); j++)
                {
                    var label = names[j] == s.FogMaterial ? $"[{names[j]}]" : names[j];
                    if (GUILayout.Button(label, GUILayout.Height(RowHeight)) && names[j] != s.FogMaterial) { s.FogMaterial = names[j]; MarkDirty(); }
                }
                GUILayout.EndHorizontal();
            }
        }

        /// <summary>OuterShape buttons (round L): Volume = fog around the player, Ring = the round J band near the rim.</summary>
        private void DrawOuterShape(FogSettings s, FogSettings saved)
        {
            GUILayout.BeginHorizontal();
            var c = GUI.color;
            if (s.OuterShape != saved.OuterShape) GUI.color = ChangedColor;
            GUILayout.Label((s.OuterShape != saved.OuterShape ? "* " : "") + "Outer shape", GUILayout.Width(LabelWidth));
            GUI.color = c;
            foreach (FogOuterShape m in Enum.GetValues(typeof(FogOuterShape)))
            {
                var label = m == s.OuterShape ? $"[{m}]" : m.ToString();
                if (GUILayout.Button(label, GUILayout.Height(RowHeight)) && m != s.OuterShape) { s.OuterShape = m; MarkDirty(); }
            }
            GUILayout.EndHorizontal();
        }

        private void DrawEmitterMode(FogSettings s, bool live)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("Emitter", GUILayout.Width(LabelWidth));
            foreach (FogEmitterMode m in Enum.GetValues(typeof(FogEmitterMode)))
            {
                var label = m == s.EmitterMode ? $"[{m}]" : m.ToString();
                if (GUILayout.Button(label, GUILayout.Height(RowHeight)) && m != s.EmitterMode) { s.EmitterMode = m; MarkDirty(); }
            }
            GUILayout.EndHorizontal();
            if (s.EmitterMode == FogEmitterMode.Mesh)
                GUILayout.Label($"Mesh: one emitter on the body surface{(live ? $", live {Count("Mesh")}" : "")}; falls back to Bones when the body mesh is not readable (see log).", _wrap);
        }

        private void DrawWave(FogSettings s, FogSettings saved)
        {
            var borrowed = FogVeil.BorrowedDistortionWave;
            var fallback = float.IsNaN(borrowed) ? 5f : borrowed;
            var wave = s.DistortionWave >= 0f ? s.DistortionWave : fallback;
            var savedWave = saved.DistortionWave >= 0f ? saved.DistortionWave : fallback;
            if (Row(s.DistortionWave >= 0f ? "Distortion wave" : "Distortion wave (borrowed)", ref wave, 0f, 10f, 0.5f, fallback, savedWave)) s.DistortionWave = wave;
            if (s.DistortionWave >= 0f && GUILayout.Button("Use the borrowed vanilla wave")) { s.DistortionWave = -1f; MarkDirty(); }
        }

        private void DrawAnchors(FogSettings s, FogSettings saved, bool live)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("", GUILayout.Width(140f));
            GUILayout.Label("live", GUILayout.Width(60f));
            foreach (var h in new[] { "radius", "x", "y", "z" }) GUILayout.Label(h, GUILayout.Width(SmallFieldWidth + 4f));
            GUILayout.EndHorizontal();
            foreach (var a in s.Anchors)
            {
                var sa = saved.Anchor(a.Name);
                var changed = sa == null || sa.Format() != a.Format();
                GUILayout.BeginHorizontal();
                var c = GUI.color;
                if (changed) GUI.color = ChangedColor;
                var on = GUILayout.Toggle(a.Enabled, (changed ? "* " : "") + a.Name, GUILayout.Width(140f));
                GUI.color = c;
                if (on != a.Enabled) { a.Enabled = on; MarkDirty(); }
                GUILayout.Label(live ? Count(a.Name) : "", GUILayout.Width(60f));
                var key = $"a{_tab}.{a.Name}.";
                if (Field(key + "r", ref a.Radius, 0f, 2f, 0.01f, SmallFieldWidth)) MarkDirty();
                if (Field(key + "x", ref a.X, -1f, 1f, 0.01f, SmallFieldWidth)) MarkDirty();
                if (Field(key + "y", ref a.Y, -1f, 1f, 0.01f, SmallFieldWidth)) MarkDirty();
                if (Field(key + "z", ref a.Z, -1f, 1f, 0.01f, SmallFieldWidth)) MarkDirty();
                GUILayout.EndHorizontal();
            }
            GUILayout.Label(live ? "Mouse wheel over a field steps it (Shift fine, Ctrl coarse)." : "Particle counts show for the tier of your active effect.", _wrap);
        }

        private string Count(string anchor)
        {
            int inner = -1, outer = -1;
            foreach (var kv in _counts)
            {
                if (kv.Key == anchor) inner = kv.Value;
                else if (kv.Key == anchor + " (outer)") outer = kv.Value;
            }
            if (inner < 0 && outer < 0) return "-";
            if (outer < 0) return $"{inner} p";
            return inner < 0 ? $"0+{outer} p" : $"{inner}+{outer} p";
        }

        private void DrawFooter(int tier)
        {
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Save", GUILayout.Height(RowHeight))) Save();
            if (GUILayout.Button("Reset", GUILayout.Height(RowHeight))) Reset();
            if (GUILayout.Button($"Defaults T{tier}", GUILayout.Height(RowHeight))) ApplyDefaults(tier);
            var en = GUI.enabled;
            GUI.enabled = _undo != null;
            if (GUILayout.Button("Undo", GUILayout.Height(RowHeight))) Undo();
            GUI.enabled = en;
            if (GUILayout.Button($"Give T{tier}", GUILayout.Height(RowHeight))) Give(tier);
            if (GUILayout.Button("Close", GUILayout.Height(RowHeight))) SetOpen(false);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label($"Copy T{tier} to", GUILayout.Width(80f));
            for (var t = 1; t <= 3; t++)
                if (t != tier && GUILayout.Button($"T{t}", GUILayout.Width(48f), GUILayout.Height(RowHeight))) Copy(tier, t);
            GUILayout.FlexibleSpace();
            var follow = GUILayout.Toggle(_previewFollowsTab, "Preview follows tab (gives the tab's tier)");
            if (follow != _previewFollowsTab)
            {
                _previewFollowsTab = follow;
                if (follow) _previewAt = Time.unscaledTime + PreviewDelay;
            }
            GUILayout.Space(GripSize);
            GUILayout.EndHorizontal();
        }

        private void OnTabChanged()
        {
            GUIUtility.keyboardControl = 0;
            if (_previewFollowsTab) _previewAt = Time.unscaledTime + PreviewDelay;   // debounced: rapid tab clicks give one effect
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
            _appliedAt = DateTime.Now.ToString("HH:mm:ss", CultureInfo.InvariantCulture);
        }

        private void Save()
        {
            if (_dirty) ApplyNow();
            for (var t = 1; t <= 3; t++)
            {
                Cfg.PluginConfig.SaveFog(t, FogVeil.Fog[t], FogVeil.AlphaMode);
                var ov = FogVeil.ModeOverride[t];
                if (ov.HasValue) Cfg.PluginConfig.SaveBodyVeilMode(t, ov.Value.ToString());
            }
            FogVeil.SaveGlobalLook();
            Cfg.PluginConfig.Refresh();   // saved values (yellow marks) and configured modes follow the file; raises Changed, which re-applies
            for (var t = 1; t <= 3; t++)
                if (FogVeil.ModeOverride[t] == FogVeil.ConfiguredMode(t)) FogVeil.ModeOverride[t] = null;
            Say("saved [Fog.Tier1..3], [Veil] (ghost, shadow, spirit) and overridden body modes to the config file");
        }

        private void Reset()
        {
            PushUndo();
            for (var t = 1; t <= 3; t++) FogVeil.ModeOverride[t] = null;
            _dirty = false;
            _buf.Clear();
            Cfg.PluginConfig.Reload();   // raises Changed: VeilController reloads the look and re-applies it
            Say("reloaded the config file; body mode overrides cleared (Undo brings the edits back)");
        }

        private void ApplyDefaults(int tier)
        {
            PushUndo();
            FogVeil.Fog[tier] = FogSettings.Defaults(tier);
            FogVeil.TryParseMode(Cfg.PluginConfig.DefaultBodyVeilMode(tier), out var mode);
            FogVeil.ModeOverride[tier] = mode == FogVeil.ConfiguredMode(tier) ? (BodyVeilMode?)null : mode;
            _buf.Clear();
            MarkDirty();
            Say($"T{tier} set to the plugin defaults (body mode {mode}); Save to keep, Undo to go back");
        }

        private void Copy(int from, int to)
        {
            PushUndo();
            FogVeil.Fog[to] = FogVeil.Fog[from].Clone();
            var mode = FogVeil.ModeFor(from);
            FogVeil.ModeOverride[to] = mode == FogVeil.ConfiguredMode(to) ? (BodyVeilMode?)null : mode;
            MarkDirty();
            Say($"copied the T{from} look (fog, distortion, body mode {mode}) to T{to}; Undo to go back");
        }

        private void PushUndo() => _undo = LookState.Capture();

        private void Undo()
        {
            if (_undo == null) return;
            _undo.Restore();
            _undo = null;
            _buf.Clear();
            ApplyNow();
            Say("undone");
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

        /// <summary>One level of undo: every value the window edits.</summary>
        private sealed class LookState
        {
            private readonly FogSettings[] _fog = new FogSettings[4];
            private readonly BodyVeilMode?[] _modes = new BodyVeilMode?[4];
            private Color _shadow, _spirit;
            private float _spiritStrength;

            public static LookState Capture()
            {
                var s = new LookState { _shadow = FogVeil.ShadowColor, _spirit = FogVeil.SpiritColor, _spiritStrength = FogVeil.SpiritStrength };
                for (var t = 1; t <= 3; t++) { s._fog[t] = FogVeil.Fog[t].Clone(); s._modes[t] = FogVeil.ModeOverride[t]; }
                return s;
            }

            public void Restore()
            {
                for (var t = 1; t <= 3; t++) { FogVeil.Fog[t] = _fog[t].Clone(); FogVeil.ModeOverride[t] = _modes[t]; }
                FogVeil.ShadowColor = _shadow;
                FogVeil.SpiritColor = _spirit;
                FogVeil.SpiritStrength = _spiritStrength;
            }
        }

        // ---------- controls ----------

        private void EnsureStyles()
        {
            if (_slider != null) return;
            _bold = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold };
            _warn = new GUIStyle(GUI.skin.label) { normal = { textColor = new Color(1f, 0.55f, 0.35f) }, wordWrap = true };
            _wrap = new GUIStyle(GUI.skin.label) { wordWrap = true };
            _status1 = new GUIStyle(GUI.skin.label) { normal = { textColor = new Color(0.6f, 0.9f, 0.6f) } };
            _fold = new GUIStyle(GUI.skin.button) { fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft, fixedHeight = RowHeight };
            // Tall slider area (easy to hit) with a thin dark track drawn in its middle, and a big bright thumb.
            _slider = new GUIStyle(GUI.skin.horizontalSlider)
            {
                fixedHeight = 22f, margin = new RectOffset(4, 4, 2, 2), padding = new RectOffset(0, 0, 0, 0), border = new RectOffset(0, 0, 0, 0),
            };
            var track = Tex(4, 22, (x, y) => y >= 8 && y <= 13 ? new Color(0.12f, 0.12f, 0.14f, 1f) : new Color(0f, 0f, 0f, 0f));
            _slider.normal.background = _slider.hover.background = _slider.active.background = _slider.focused.background = track;
            _thumb = new GUIStyle(GUI.skin.horizontalSliderThumb)
            {
                fixedWidth = ThumbWidth, fixedHeight = 22f, margin = new RectOffset(0, 0, 0, 0), padding = new RectOffset(0, 0, 0, 0),
                border = new RectOffset(0, 0, 0, 0), overflow = new RectOffset(0, 0, 0, 0),
            };
            _thumb.normal.background = Tex(1, 1, (x, y) => new Color(0.78f, 0.8f, 0.86f, 1f));
            _thumb.hover.background = Tex(1, 1, (x, y) => new Color(0.92f, 0.94f, 1f, 1f));
            _thumb.active.background = _thumb.focused.background = Tex(1, 1, (x, y) => new Color(1f, 0.85f, 0.35f, 1f));
            _grip = new GUIStyle(GUI.skin.box) { normal = { background = Tex(1, 1, (x, y) => new Color(0.6f, 0.62f, 0.7f, 0.8f)) } };
        }

        private static Texture2D Tex(int w, int h, Func<int, int, Color> pixel)
        {
            var t = new Texture2D(w, h, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Point };
            for (var y = 0; y < h; y++)
            for (var x = 0; x < w; x++)
                t.SetPixel(x, y, pixel(x, y));
            t.Apply();
            return t;
        }

        private bool Foldout(bool open, string title) => GUILayout.Toggle(open, (open ? "[-]  " : "[+]  ") + title, _fold);

        /// <summary>Focus moved away from a typed field since the last pass: that field commits its text (in its own Row/Field call).</summary>
        private void TrackFocus()
        {
            var focus = GUI.GetNameOfFocusedControl() ?? "";
            if (focus == _prevFocus) return;
            if (_prevFocus.Length > 0 && _buf.ContainsKey(_prevFocus)) _commitId = _prevFocus;
            _buf.Remove(focus);   // a newly focused field starts from the current value
            _prevFocus = focus;
        }

        private static float StepModifier(Event e) => e.shift ? 0.1f : e.control ? 10f : 1f;

        private static float Snap(float v, float grid) => grid > 0f ? Mathf.Round(v / grid) * grid : v;

        private static string Fmt(float v) => v.ToString("0.####", CultureInfo.InvariantCulture);

        private static bool Same(float a, float b) => Mathf.Abs(a - b) < 1e-5f;

        /// <summary>
        /// [label][slider][-][field][+][R]. The label turns yellow with * when the value differs from the saved file; R returns to
        /// the default. Slider drag, wheel and +/- snap to step/10; typed values are taken as typed (clamped to min..max).
        /// A value outside min..max (from the config or the console) is never clamped unless the user edits it. True when changed.
        /// </summary>
        private bool Row(string label, ref float v, float min, float max, float step, float def, float saved)
        {
            var e = Event.current;
            var old = v;
            var mod = StepModifier(e);
            var grid = step * 0.1f;
            GUILayout.BeginHorizontal();
            var changedFromSaved = !Same(v, saved);
            var c = GUI.color;
            if (changedFromSaved) GUI.color = ChangedColor;
            GUILayout.Label(new GUIContent((changedFromSaved ? "* " : "") + label, $"saved {Fmt(saved)}, default {Fmt(def)}"),
                            GUILayout.Width(LabelWidth), GUILayout.Height(RowHeight));
            GUI.color = c;

            var sliderRect = GUILayoutUtility.GetRect(90f, 10000f, RowHeight, RowHeight, GUILayout.MinWidth(90f), GUILayout.ExpandWidth(true));
            if (Slider(sliderRect, v, min, max, out var slid)) v = Snap(slid, grid);
            if (e.type == EventType.ScrollWheel && sliderRect.Contains(e.mousePosition))
            {
                v = Mathf.Clamp(Snap(v - Mathf.Sign(e.delta.y) * step * mod, grid * Mathf.Min(1f, mod)), min, max);
                e.Use();   // the scroll view must not scroll as well
            }

            if (GUILayout.Button("-", GUILayout.Width(ButtonWidth), GUILayout.Height(RowHeight))) v = Mathf.Clamp(Snap(v - step * mod, grid * Mathf.Min(1f, mod)), min, max);
            TypedField("r" + _tab + "." + label, ref v, min, max, FieldWidth);
            if (GUILayout.Button("+", GUILayout.Width(ButtonWidth), GUILayout.Height(RowHeight))) v = Mathf.Clamp(Snap(v + step * mod, grid * Mathf.Min(1f, mod)), min, max);
            var en = GUI.enabled;
            GUI.enabled = en && !Same(v, def);
            if (GUILayout.Button(new GUIContent("R", $"default {Fmt(def)}"), GUILayout.Width(ButtonWidth), GUILayout.Height(RowHeight))) v = def;
            GUI.enabled = en;
            GUILayout.EndHorizontal();

            if (Same(v, old)) { v = old; return false; }
            MarkDirty();
            return true;
        }

        /// <summary>
        /// Draggable slider (replaces GUILayout.HorizontalSlider, whose thumb could not be dragged in game): a press anywhere on
        /// the track grabs it (hotControl) and sets the value from the mouse x; while grabbed the value follows the mouse until the
        /// button is released. The grab is kept in our own field (not only GUIUtility.hotControl) and drag/up are read from
        /// rawType, so a drag still reaches the grabbed slider when IMGUI marks the event Used or Ignore for this window (another
        /// OnGUI, window focus under the scaled matrix); it also follows the mouse on Repaint, in case no MouseDrag arrives at all.
        /// Any new mouse press releases a grab whose MouseUp got lost (OnGUI). True when it set a new value.
        /// </summary>
        private bool Slider(Rect r, float v, float min, float max, out float value)
        {
            value = v;
            var id = GUIUtility.GetControlID(FocusType.Passive);
            var e = Event.current;
            var hot = id == _sliderHot;
            switch (e.rawType)
            {
                case EventType.MouseDown:
                    if (e.type != EventType.MouseDown || e.button != 0 || !r.Contains(e.mousePosition)) break;
                    GUIUtility.hotControl = _sliderHot = id;
                    GUIUtility.keyboardControl = 0;   // a focused typed field must not keep its stale text
                    value = ValueAt(r, e.mousePosition.x, min, max);
                    e.Use();
                    return true;
                case EventType.MouseDrag:
                    if (!hot) break;
                    value = ValueAt(r, e.mousePosition.x, min, max);
                    e.Use();
                    return true;
                case EventType.MouseUp:
                    if (!hot) break;
                    ReleaseSlider();
                    e.Use();
                    break;
                case EventType.Repaint:
                    var thumbX = r.x + (r.width - ThumbWidth) * Mathf.InverseLerp(min, max, Mathf.Clamp(v, min, max));
                    var track = new Rect(r.x, r.y + (r.height - _slider.fixedHeight) * 0.5f, r.width, _slider.fixedHeight);
                    _slider.Draw(track, GUIContent.none, id, false, r.Contains(e.mousePosition));
                    _thumb.Draw(new Rect(thumbX, track.y, ThumbWidth, track.height), GUIContent.none, id, hot, r.Contains(e.mousePosition));
                    if (hot)
                    {
                        var follow = ValueAt(r, e.mousePosition.x, min, max);
                        if (!Same(follow, Mathf.Clamp(v, min, max))) { value = follow; return true; }
                    }
                    break;
            }
            return false;
        }

        private void ReleaseSlider()
        {
            if (_sliderHot != 0 && GUIUtility.hotControl == _sliderHot) GUIUtility.hotControl = 0;
            _sliderHot = 0;
        }

        /// <summary>Slider value for a mouse x: the thumb centre follows the mouse, clamped to min..max.</summary>
        private static float ValueAt(Rect r, float x, float min, float max) =>
            Mathf.Lerp(min, max, Mathf.InverseLerp(r.x + ThumbWidth * 0.5f, r.xMax - ThumbWidth * 0.5f, x));

        /// <summary>Compact typed field (anchor grid): wheel over it steps by step (Shift/Ctrl modifiers). True when changed.</summary>
        private bool Field(string id, ref float v, float min, float max, float step, float width)
        {
            var e = Event.current;
            var old = v;
            TypedField(id, ref v, min, max, width);
            if (e.type == EventType.ScrollWheel && GUILayoutUtility.GetLastRect().Contains(e.mousePosition))
            {
                var mod = StepModifier(e);
                v = Mathf.Clamp(Snap(v - Mathf.Sign(e.delta.y) * step * mod, step * 0.1f * Mathf.Min(1f, mod)), min, max);
                e.Use();
            }
            return !Same(v, old);
        }

        /// <summary>Text field that shows the value and applies typed text only on Enter or when it loses focus (never per keystroke).</summary>
        private void TypedField(string id, ref float v, float min, float max, float width)
        {
            var e = Event.current;
            var focused = GUI.GetNameOfFocusedControl() == id;
            var commit = _commitId == id;
            if (focused && e.type == EventType.KeyDown && (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter))
            {
                commit = true;
                e.Use();
                GUIUtility.keyboardControl = 0;
            }
            var shown = focused && _buf.TryGetValue(id, out var b) ? b : Fmt(v);
            GUI.SetNextControlName(id);
            var text = GUILayout.TextField(shown, GUILayout.Width(width), GUILayout.Height(RowHeight));
            if (focused && !commit) _buf[id] = text;
            if (!commit) return;
            if (_commitId == id) _commitId = null;
            if (_buf.TryGetValue(id, out var typed) && FloatList.TryParseOne(typed, out var parsed)) v = Mathf.Clamp(parsed, min, max);
            _buf.Remove(id);
        }

        private bool ToggleRow(string label, ref bool v, bool def, bool saved)
        {
            GUILayout.BeginHorizontal();
            var c = GUI.color;
            if (v != saved) GUI.color = ChangedColor;
            var nv = GUILayout.Toggle(v, (v != saved ? "* " : "") + label, GUILayout.Height(22f));
            GUI.color = c;
            GUILayout.FlexibleSpace();
            var en = GUI.enabled;
            GUI.enabled = en && nv != def;
            if (GUILayout.Button(new GUIContent("R", $"default {(def ? "on" : "off")}"), GUILayout.Width(ButtonWidth), GUILayout.Height(22f))) nv = def;
            GUI.enabled = en;
            GUILayout.EndHorizontal();
            if (nv == v) return false;
            v = nv;
            MarkDirty();
            return true;
        }
    }

    /// <summary>
    /// Detects an open IMGUI config window of another plugin: any loaded plugin whose instance has a public bool property
    /// DisplayingWindow (BepInEx ConfigurationManager, com.bepis.bepinex.configurationmanager v19, and its Valheim forks use that
    /// name). Resolved once per second until found, by reflection only, so there is no reference to those plugins.
    /// </summary>
    internal static class ExternalWindow
    {
        private static readonly List<KeyValuePair<object, System.Reflection.PropertyInfo>> Found = new List<KeyValuePair<object, System.Reflection.PropertyInfo>>();
        private static float _nextScan;
        private static bool _logged;

        public static bool IsOpen()
        {
            if (Found.Count == 0 && Time.unscaledTime >= _nextScan) Scan();
            foreach (var kv in Found)
            {
                try
                {
                    if (kv.Key is UnityEngine.Object o && o == null) continue;   // destroyed plugin
                    if (kv.Value.GetValue(kv.Key, null) is bool b && b) return true;
                }
                catch (Exception) { /* a throwing getter counts as closed */ }
            }
            return false;
        }

        private static void Scan()
        {
            _nextScan = Time.unscaledTime + 1f;
            foreach (var info in BepInEx.Bootstrap.Chainloader.PluginInfos.Values)
            {
                var inst = info?.Instance;
                if (inst == null || inst.GetType().Assembly == typeof(ExternalWindow).Assembly) continue;
                var prop = AccessTools.Property(inst.GetType(), "DisplayingWindow");
                if (prop == null || prop.PropertyType != typeof(bool) || prop.GetGetMethod() == null) continue;
                Found.Add(new KeyValuePair<object, System.Reflection.PropertyInfo>(inst, prop));
                if (!_logged) Plugin.Log.LogInfo($"ip_fogui: cursor hand-back waits while {info.Metadata.GUID}.DisplayingWindow is true");
            }
            if (Found.Count > 0) _logged = true;
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
